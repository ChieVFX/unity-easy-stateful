using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EasyStateful.Runtime
{
    /// <summary>
    /// Handles the core tweening functionality for StatefulRoot
    /// </summary>
    public class StatefulTweenEngine
    {
        private StatefulRoot root;
        private StatefulEasingManager easingManager;

        // Pooled collections for TweenToState to reduce GC
        private List<Property> _pooledPropertiesToSnap = new List<Property>();
        private List<(Property prop, PropertyBinding binding, float initialValue, Ease ease, PropertyTransitionInfo transitionInfo)> _pooledPropertiesToTween = 
            new List<(Property, PropertyBinding, float, Ease, PropertyTransitionInfo)>();
        
        // Pre-allocated arrays for the tween loop to avoid foreach allocations
        private TweenItem[] _tweenItemsArray = new TweenItem[32]; // Start with reasonable size
        private int _tweenItemsCount = 0;

        // UniTask cancellation
        private CancellationTokenSource _currentTweenCancellation;

        // Add a field to track deactivation properties
        private List<Property> _pooledPropertiesToDeactivate = new List<Property>();

        // Struct to avoid allocations in the tween loop
        private struct TweenItem
        {
            public PropertyBinding binding;
            public float initialValue;
            public float targetValue;
            public Ease ease;
            public PropertyTransitionInfo transitionInfo;
        }

        // Tween state
        private bool _isTweening = false;
        private float _tweenStartTime;
        private float _tweenDuration;
        private CancellationTokenSource _activeTweenCancellation;
        private Action _tweenCompletionCallback;

        public bool IsTweening => _isTweening;

        public StatefulTweenEngine(StatefulRoot root, StatefulEasingManager easingManager)
        {
            this.root = root;
            this.easingManager = easingManager;
        }

        public void Cleanup()
        {
            _currentTweenCancellation?.Cancel();
            _currentTweenCancellation?.Dispose();
        }

        public async UniTask TweenToState(State targetState, float duration, Ease ease, 
            PropertyBindingManager bindingManager, StatefulSettingsResolver settingsResolver,
            Dictionary<Property, PropertyTransitionInfo> propertyTransitionCache)
        {
            // Cancel any existing tween
            _currentTweenCancellation?.Cancel();
            _currentTweenCancellation?.Dispose();
            _currentTweenCancellation = new CancellationTokenSource();

            // Clear pooled collections
            _pooledPropertiesToSnap.Clear();
            _pooledPropertiesToTween.Clear();
            _pooledPropertiesToDeactivate.Clear();

            // Process properties
            ProcessProperties(targetState, duration, ease, bindingManager, settingsResolver, propertyTransitionCache);

            // Apply all snapped properties first
            for (int i = 0; i < _pooledPropertiesToSnap.Count; i++)
            {
                ApplyProperty(_pooledPropertiesToSnap[i], bindingManager);
            }

            // If no properties to tween, apply deactivations and we're done
            if (_pooledPropertiesToTween.Count == 0)
            {
                ApplyDeactivations(bindingManager);
                return;
            }

            PrepareArrayBasedTween();

            // Use a completion source that gets resolved in Update
            var completionSource = new UniTaskCompletionSource();
            _tweenCompletionCallback = () => 
            {
                ApplyDeactivations(bindingManager);
                completionSource.TrySetResult();
            };
            
            StartTween(duration);
            
            await completionSource.Task;
        }

        private void ProcessProperties(State state, float duration, Ease ease, PropertyBindingManager bindingManager, 
            StatefulSettingsResolver settingsResolver, Dictionary<Property, PropertyTransitionInfo> propertyTransitionCache)
        {
            // Process properties without foreach to avoid enumerator allocation
            for (int propIndex = 0; propIndex < state.properties.Count; propIndex++)
            {
                var prop = state.properties[propIndex];
                
                PropertyTransitionInfo transitionInfo;

                // Always use cached info when available, even in editor
                if (propertyTransitionCache.TryGetValue(prop, out transitionInfo))
                {
                    // Use cached transition info - no allocation here
                }
                else
                {
                    // This should rarely happen if caching is working properly
                    Debug.LogWarning($"Property transition info not cached for {prop.propertyName} on {prop.path}. This may cause GC allocations.");
                    
                    // Create minimal transition info without rule lookup to avoid allocations
                    transitionInfo = new PropertyTransitionInfo
                    {
                        duration = duration,
                        ease = ease,
                        useCustomTiming = false,
                        customTimingStart = 0f,
                        customTimingEnd = 0f
                    };
                }
                
                // Get the binding for this property - this should use cached bindings
                var binding = bindingManager.GetOrCreateBinding(prop, out Transform target);

                if (binding == null)
                    continue;

                // Special handling for m_IsActive - always immediate
                if (prop.propertyName == "m_IsActive" && binding.targetGameObject != null)
                {
                    // If turning ON: activate immediately (first frame)
                    // If turning OFF: we'll handle this at the end of the tween
                    if (prop.value > 0.5f)
                    {
                        _pooledPropertiesToSnap.Add(prop); // Turn on immediately
                    }
                    else
                    {
                        _pooledPropertiesToDeactivate.Add(prop); // Deactivate at end
                    }
                    continue;
                }

                // Cache these checks to avoid repeated property access
                bool hasObjectRef = !string.IsNullOrEmpty(prop.objectReference);
                bool hasSetterObj = binding.setterObj != null;
                bool isObjectRef = hasObjectRef || hasSetterObj;
                float effectiveDuration = transitionInfo.GetEffectiveDuration();

                if (effectiveDuration == 0f || isObjectRef)
                {
                    _pooledPropertiesToSnap.Add(prop);
                }
                else if (binding.setter != null && binding.component != null && binding.getter != null) // Numeric, tweenable property
                {
                    float initialValue = bindingManager.GetCurrentValue(binding);
                    _pooledPropertiesToTween.Add((prop, binding, initialValue, transitionInfo.ease, transitionInfo));
                }
                else
                {
                    Debug.LogWarning($"Property '{prop.propertyName}' on path '{prop.path}' for component type '{prop.componentType}' could not be categorized for tweening or snapping. It may be missing a valid setter or not be an object reference.");
                }
            }
        }

        private void ApplyProperty(Property prop, PropertyBindingManager bindingManager)
        {
            var binding = bindingManager.GetOrCreateBinding(prop, out Transform target);
            if (binding == null) return; // Target itself might be null if path is bad

            if (prop.propertyName == "m_IsActive" && binding.targetGameObject != null)
            {
                binding.targetGameObject.SetActive(prop.value > 0.5f); // Assuming 1 for true, 0 for false
#if UNITY_EDITOR
                if (!Application.isPlaying) EditorUtility.SetDirty(binding.targetGameObject);
#endif
            }
            else if (!string.IsNullOrEmpty(prop.objectReference) && binding.setterObj != null && binding.component != null)
            {
                var obj = Resources.Load(prop.objectReference);
                try
                {
                    binding.setterObj(binding.component, obj);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error executing compiled object setter for {prop.propertyName} on {binding.component.GetType().Name} with object {obj?.name} (Path: {prop.objectReference}): {ex.Message}");
                }
#if UNITY_EDITOR
                if (!Application.isPlaying) EditorUtility.SetDirty(binding.component);
#endif
            }
            else if (binding.setter != null && binding.component != null)
            {
                try
                {
                    binding.setter(binding.component, prop.value);
                }
                catch (Exception ex)
                {
                     Debug.LogError($"Error executing compiled numeric setter for {prop.propertyName} on {binding.component.GetType().Name}: {ex.Message}");
                }
#if UNITY_EDITOR
                if (!Application.isPlaying) EditorUtility.SetDirty(binding.component);
#endif
            }
        }

        private void StartTween(float duration)
        {
            _isTweening = true;
            _tweenDuration = duration;
            _activeTweenCancellation = _currentTweenCancellation;
            
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                _tweenStartTime = (float)EditorApplication.timeSinceStartup;
            }
            else
#endif
            {
                _tweenStartTime = Time.time;
            }
        }

        /// <summary>
        /// Prepare array-based tween data to avoid foreach allocations during the tween loop
        /// </summary>
        private void PrepareArrayBasedTween()
        {
            _tweenItemsCount = _pooledPropertiesToTween.Count;
            
            // Resize array if needed
            if (_tweenItemsArray.Length < _tweenItemsCount)
            {
                _tweenItemsArray = new TweenItem[Mathf.NextPowerOfTwo(_tweenItemsCount)];
            }

            // Copy data to array
            for (int i = 0; i < _tweenItemsCount; i++)
            {
                var item = _pooledPropertiesToTween[i];
                _tweenItemsArray[i] = new TweenItem
                {
                    binding = item.binding,
                    initialValue = item.initialValue,
                    targetValue = item.prop.value,
                    ease = item.ease,
                    transitionInfo = item.transitionInfo
                };
            }
        }

        public void UpdateTween()
        {
            if (!_isTweening) return;

            // Check cancellation
            if (_activeTweenCancellation != _currentTweenCancellation || 
                (_activeTweenCancellation != null && _activeTweenCancellation.IsCancellationRequested))
            {
                _isTweening = false;
                _tweenCompletionCallback?.Invoke();
                _tweenCompletionCallback = null;
                return;
            }

            float currentTime;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                currentTime = (float)EditorApplication.timeSinceStartup;
            }
            else
#endif
            {
                currentTime = Time.time;
            }

            float elapsedTime = currentTime - _tweenStartTime;
            float normalizedTime = Mathf.Clamp01(elapsedTime / _tweenDuration);

            // Update all properties with their respective easings and custom timing
            for (int i = 0; i < _tweenItemsCount; i++)
            {
                var item = _tweenItemsArray[i];
                if (item.binding.component != null)
                {
                    // Apply custom timing if enabled
                    float curveTime = item.transitionInfo.GetNormalizedCurveTime(normalizedTime);
                    float easedProgress = easingManager.SampleEasingInline(item.ease, curveTime);
                    float interpolatedValue = Mathf.LerpUnclamped(item.initialValue, item.targetValue, easedProgress);
                    item.binding.setter(item.binding.component, interpolatedValue);
                    
#if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        EditorUtility.SetDirty(item.binding.component);
                    }
#endif
                }
            }

            // Check if tween is complete
            if (normalizedTime >= 1f)
            {
                // Ensure final values are set
                for (int i = 0; i < _tweenItemsCount; i++)
                {
                    var item = _tweenItemsArray[i];
                    if (item.binding.component != null)
                    {
                        item.binding.setter(item.binding.component, item.targetValue);
#if UNITY_EDITOR
                        if (!Application.isPlaying)
                        {
                            EditorUtility.SetDirty(item.binding.component);
                        }
#endif
                    }
                }
                
                _isTweening = false;
                _tweenCompletionCallback?.Invoke();
                _tweenCompletionCallback = null;
            }
        }

        private void ApplyDeactivations(PropertyBindingManager bindingManager)
        {
            for (int i = 0; i < _pooledPropertiesToDeactivate.Count; i++)
            {
                ApplyProperty(_pooledPropertiesToDeactivate[i], bindingManager);
            }
        }
    }
} 