using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EasyStateful.Runtime {
    /// <summary>
    /// Attach this to your root UI GameObject. Assign the exported StatefulDataAsset.
    /// Then call SnapToState(name) or TweenToState(name, duration, ease) to transition.
    /// </summary>
    public class StatefulRoot : MonoBehaviour
    {
        public StatefulDataAsset statefulDataAsset;
        private UIStateMachine stateMachine;
        private PropertyBindingManager bindingManager;

        [Header("Settings")]
        [Tooltip("Optional: Assign Group Settings to override Global Settings for this instance and its children.")]
        public StatefulGroupSettingsData groupSettings;

        [Header("States")]
        public string[] stateNames;

        [Header("Debug")]
        public int currentStateIndex = 0;

        [Tooltip("Overrides Group and Global default transition time.")]
        public bool overrideDefaultTransitionTime = false;
        [Tooltip("Custom default transition time for this instance if override is enabled.")]
        public float customDefaultTransitionTime = 0.5f;

        [Tooltip("Overrides Group and Global default ease.")]
        public bool overrideDefaultEase = false;
        [Tooltip("Custom default ease for this instance if override is enabled.")]
        public Ease customDefaultEase = Ease.Linear;

        // Pooled collections for TweenToState to reduce GC
        private List<Property> _pooledPropertiesToSnap = new List<Property>();
        private List<(Property prop, PropertyBinding binding, float initialValue, Ease ease)> _pooledPropertiesToTween = 
            new List<(Property, PropertyBinding, float, Ease)>();
        
        // Pre-allocated arrays for the tween loop to avoid foreach allocations
        private TweenItem[] _tweenItemsArray = new TweenItem[32]; // Start with reasonable size
        private int _tweenItemsCount = 0;

        // Instance-specific caches
        private Dictionary<(string propertyName, string componentType), PropertyOverrideRule> _propertyOverrideCache;
        private Dictionary<Property, PropertyTransitionInfo> _propertyTransitionCache = new();

        // UniTask cancellation
        private CancellationTokenSource _currentTweenCancellation;

        // Struct to avoid allocations in the tween loop
        private struct TweenItem
        {
            public PropertyBinding binding;
            public float initialValue;
            public float targetValue;
            public Ease ease;
        }

        // At class level:
        private struct PropertyTransitionInfo
        {
            public float duration;
            public Ease ease;
            public bool instantEnableDelayedDisable;
        }

        // Track inspector-driven changes at runtime
        private int prevStateIndex = -1;

        #if UNITY_EDITOR
        [SerializeField]
        private AnimationClip editorClip;
        #endif

        // Add these fields at class level (replace the existing ones)
        private bool _isTweening = false;
        private float _tweenStartTime;
        private float _tweenDuration;
        private CancellationTokenSource _activeTweenCancellation; // Keep reference to check if cancelled
        private TaskCompletionSource<bool> _tweenCompletionSource; // Use regular TaskCompletionSource instead

        // Alternative: Completely eliminate async/await
        private Action _tweenCompletionCallback;

        // Cache for default curves to avoid repeated creation
        private static readonly Dictionary<Ease, AnimationCurve> _defaultCurveCache = new Dictionary<Ease, AnimationCurve>();

        private float GetEffectiveTransitionTime()
        {
            // Tier 3: StatefulRoot instance override
            if (overrideDefaultTransitionTime)
            {
                return customDefaultTransitionTime;
            }
            // Tier 4: GroupSettings override
            if (groupSettings != null && groupSettings.overrideGlobalDefaultTransitionTime)
            {
                return groupSettings.customDefaultTransitionTime;
            }
            // Tier 5: Global Settings
            return StatefulGlobalSettings.DefaultTime;
        }

        private Ease GetEffectiveEase()
        {
            // Tier 3: StatefulRoot instance override
            if (overrideDefaultEase)
            {
                return customDefaultEase;
            }
            // Tier 4: GroupSettings override
            if (groupSettings != null && groupSettings.overrideGlobalDefaultEase)
            {
                return groupSettings.customDefaultEase;
            }
            // Tier 5: Global Settings
            return StatefulGlobalSettings.DefaultEase;
        }

        /// <summary>
        /// Gets the highest priority property override rule.
        /// Tier 1: Group Settings
        /// Tier 2: Global Settings
        /// </summary>
        private PropertyOverrideRule GetPropertyOverrideRule(string propertyName, string componentTypeFullName)
        {
            // Tier 1: Group Settings Property Override (cached)
            if (_propertyOverrideCache != null)
            {
                if (_propertyOverrideCache.TryGetValue((propertyName, componentTypeFullName), out var rule))
                    return rule;
                if (_propertyOverrideCache.TryGetValue((propertyName, ""), out rule))
                    return rule;
            }
            // Tier 2: Global Settings Property Override
            return StatefulGlobalSettings.GetGlobalPropertyOverrideRule(propertyName, componentTypeFullName);
        }

        /// <summary>
        /// Gets the effective easings data with proper fallback hierarchy
        /// </summary>
        private StatefulEasingsData GetEffectiveEasingsData()
        {
            // Tier 1: Group Settings
            if (groupSettings != null && groupSettings.easingsData != null)
                return groupSettings.easingsData;
            
            // Tier 2: Global Settings
            if (StatefulGlobalSettings.EasingsData != null)
                return StatefulGlobalSettings.EasingsData;
            
            // Fallback: Create default curves on demand
            return null;
        }

        /// <summary>
        /// Sample an easing curve at the given time (0-1)
        /// </summary>
        private float SampleEasing(Ease ease, float time)
        {
            return SampleEasing((int)ease, time);
        }

        /// <summary>
        /// Sample an easing curve at the given time (0-1) using curve index
        /// </summary>
        private float SampleEasing(int easeIndex, float time)
        {
            var easingsData = GetEffectiveEasingsData();
            
            if (easingsData != null && easingsData.curves != null && 
                easeIndex >= 0 && easeIndex < easingsData.curves.Length && 
                easingsData.curves[easeIndex] != null)
            {
                return easingsData.curves[easeIndex].Evaluate(time);
            }
            
            // Fallback to default curve generation
            var defaultCurve = StatefulEasingsData.GetDefaultCurve((Ease)easeIndex);
            return defaultCurve.Evaluate(time);
        }

        void Awake()
        {
            bindingManager = new PropertyBindingManager(transform);
            
            if (statefulDataAsset != null)
            {
                LoadFromAsset(statefulDataAsset);
            }
            else
            {
                stateMachine = new UIStateMachine();
                UpdateStateNamesArray(); 
            }
            BuildPropertyOverrideCache();
            BuildPropertyTransitionCache();
        }

        void OnDestroy()
        {
            // Cancel any running tweens
            _currentTweenCancellation?.Cancel();
            _currentTweenCancellation?.Dispose();
        }

        /// <summary>
        /// Load the state machine data from a StatefulDataAsset.
        /// </summary>
        public void LoadFromAsset(StatefulDataAsset dataAsset)
        {
            if (dataAsset == null || dataAsset.stateMachine == null)
            {
                stateMachine = new UIStateMachine(); 
            }
            else
            {
                stateMachine = dataAsset.stateMachine;
            }
            UpdateStateNamesArray();
            BuildPropertyOverrideCache();
            BuildPropertyTransitionCache();
        }

        /// <summary>
        /// Immediately apply all property values in the given state.
        /// </summary>
        public void SnapToState(string stateName)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // In editor, always rebuild the cache before each state change for live preview
                BuildPropertyTransitionCache();
            }
#endif

            if (stateMachine == null || stateMachine.states == null) return;
            var state = stateMachine.states.Find(s => s.name == stateName);
            if (state == null)
            {
                Debug.LogWarning($"State not found: {stateName}", this);
                return;
            }

            foreach (var prop in state.properties)
            {
                ApplyProperty(prop);
            }
        }

        /// <summary>
        /// Non-async version that uses callbacks instead
        /// </summary>
        public UniTask TaskTweenToState(string stateName, float? duration = null, Ease? ease = null, CancellationToken cancellationToken = default)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // In editor, always rebuild the cache before each state change for live preview
                BuildPropertyTransitionCache();
            }
#endif

            if (stateMachine == null || stateMachine.states == null)
            {
                Debug.LogWarning($"State machine not loaded or has no states. Cannot tween to '{stateName}'.", this);
                return UniTask.CompletedTask;
            }
            
            // Cancel any existing tween
            _currentTweenCancellation?.Cancel();
            _currentTweenCancellation?.Dispose();
            _currentTweenCancellation = new CancellationTokenSource();
            
            // Find state without LINQ to avoid allocation
            State state = null;
            for (int i = 0; i < stateMachine.states.Count; i++)
            {
                if (stateMachine.states[i].name == stateName)
                {
                    state = stateMachine.states[i];
                    break;
                }
            }
            
            if (state == null)
            {
                Debug.LogWarning($"State not found: {stateName}", this);
                return UniTask.CompletedTask;
            }

            float overallDuration = duration ?? GetEffectiveTransitionTime();
            Ease overallEase = ease ?? GetEffectiveEase();

            // Clear pooled collections
            _pooledPropertiesToSnap.Clear();
            _pooledPropertiesToTween.Clear();

            // Process properties without foreach to avoid enumerator allocation
            for (int propIndex = 0; propIndex < state.properties.Count; propIndex++)
            {
                var prop = state.properties[propIndex];
                
#if UNITY_EDITOR
                bool inEditor = !Application.isPlaying;
#else
                const bool inEditor = false;
#endif

                float finalPropDuration;
                Ease finalPropEase;
                bool handledBySpecialRule = false;
                bool instantEnableDelayedDisable = false;

                if (inEditor)
                {
                    // Always resolve transition info live in editor
                    finalPropDuration = duration ?? GetEffectiveTransitionTime();
                    finalPropEase = ease ?? GetEffectiveEase();
                    var rule = GetPropertyOverrideRule(prop.propertyName, prop.componentType);
                    if (rule != null)
                    {
                        if (rule.instantEnableDelayedDisable && prop.propertyName == "m_IsActive")
                        {
                            instantEnableDelayedDisable = true;
                        }
                        else if (rule.instantEnableDelayedDisable)
                        {
                            finalPropDuration = 0f;
                        }
                        if (rule.overrideEase) finalPropEase = rule.ease;
                    }
                }
                else
                {
                    // Use cached info in play mode
                    if (!_propertyTransitionCache.TryGetValue(prop, out PropertyTransitionInfo info))
                    {
                        info.duration = duration ?? GetEffectiveTransitionTime();
                        info.ease = ease ?? GetEffectiveEase();
                        info.instantEnableDelayedDisable = false;
                    }
                    finalPropDuration = duration ?? info.duration;
                    finalPropEase = ease ?? info.ease;
                    instantEnableDelayedDisable = info.instantEnableDelayedDisable;
                }

                if (instantEnableDelayedDisable && prop.propertyName == "m_IsActive")
                {
                    if (prop.value > 0.5f)
                    {
                        if (prop.objectReference != null)
                        {
                            var obj = Resources.Load(prop.objectReference);
                            if (obj != null)
                            {
                                prop.objectReference = null;
                                prop.value = 1f;
                            }
                        }
                    }
                    else
                    {
                        if (prop.objectReference != null)
                        {
                            var obj = Resources.Load(prop.objectReference);
                            if (obj != null)
                            {
                                prop.objectReference = null;
                                prop.value = 0f;
                            }
                        }
                    }
                    handledBySpecialRule = true;
                }
                else if (instantEnableDelayedDisable) // For non-m_IsActive properties that should be instant
                {
                    finalPropDuration = 0f;
                }
                
                if (handledBySpecialRule)
                {
                    continue; // Property was fully handled by a special rule
                }

                // Get the binding for this property - now using bindingManager
                var binding = bindingManager.GetOrCreateBinding(prop, out Transform target);

                if (binding == null)
                    continue;

                bool isObjectRef = !string.IsNullOrEmpty(prop.objectReference) || binding.setterObj != null;
                bool isGenericActiveProperty = prop.propertyName == "m_IsActive" && binding.targetGameObject != null;

                if (finalPropDuration == 0f || isObjectRef || isGenericActiveProperty)
                {
                    _pooledPropertiesToSnap.Add(prop);
                }
                else if (binding.setter != null && binding.component != null && binding.getter != null) // Numeric, tweenable property
                {
                    float initialValue = bindingManager.GetCurrentValue(binding);
                    _pooledPropertiesToTween.Add((prop, binding, initialValue, finalPropEase));
                }
                else
                {
                    Debug.LogWarning($"Property '{prop.propertyName}' on path '{prop.path}' for component type '{prop.componentType}' could not be categorized for tweening or snapping. It may be missing a valid setter or not be an object reference.", this);
                }
            }

            // Apply all snapped properties first
            for (int i = 0; i < _pooledPropertiesToSnap.Count; i++)
            {
                ApplyProperty(_pooledPropertiesToSnap[i]);
            }

            // If no properties to tween, we're done
            if (_pooledPropertiesToTween.Count == 0)
                return UniTask.CompletedTask;

            PrepareArrayBasedTween();

            // Use a completion source that gets resolved in Update
            var completionSource = new UniTaskCompletionSource();
            _tweenCompletionCallback = () => completionSource.TrySetResult();
            
            StartTween(overallDuration);
            
            return completionSource.Task;
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
                    ease = item.ease
                };
            }
        }

        /// <summary>
        /// Convenience method for non-async calls (maintains backward compatibility)
        /// </summary>
        public void TweenToState(string stateName, float? duration = null, Ease? ease = null)
        {
            TaskTweenToState(stateName, duration, ease, CancellationToken.None).Forget();
        }

        public void TweenToState(string stateName, float duration, Ease ease)
        {
            TaskTweenToState(stateName, duration, ease, CancellationToken.None).Forget();
        }

        private void ApplyProperty(Property prop)
        {
            var binding = bindingManager.GetOrCreateBinding(prop, out Transform target);
            if (binding == null) return; // Target itself might be null if path is bad

            if (prop.propertyName == "m_IsActive" && binding.targetGameObject != null)
            {
                binding.targetGameObject.SetActive(prop.value > 0.5f); // Assuming 1 for true, 0 for false
#if UNITY_EDITOR
                if (!Application.isPlaying && GUI.changed) EditorUtility.SetDirty(binding.targetGameObject);
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
                    Debug.LogError($"Error executing compiled object setter for {prop.propertyName} on {binding.component.GetType().Name} with object {obj?.name} (Path: {prop.objectReference}): {ex.Message}", this);
                }
#if UNITY_EDITOR
                if (!Application.isPlaying && GUI.changed) EditorUtility.SetDirty(binding.component);
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
                     Debug.LogError($"Error executing compiled numeric setter for {prop.propertyName} on {binding.component.GetType().Name}: {ex.Message}", this);
                }
#if UNITY_EDITOR
                if (!Application.isPlaying && GUI.changed) EditorUtility.SetDirty(binding.component);
#endif
            }
        }

        #if UNITY_EDITOR
        private void OnValidate()
        {
            if (statefulDataAsset != null)
            {
                LoadFromAsset(statefulDataAsset);
            }
            else 
            {
                stateMachine = new UIStateMachine();
                UpdateStateNamesArray();
            }
        }

        [ContextMenu("Update State Names Array")]
        public void UpdateStateNamesArray()
        {
            if (stateMachine != null && stateMachine.states != null)
                stateNames = stateMachine.states.Select(s => s.name).ToArray();
            else
                stateNames = new string[0];
            
            if (!Application.isPlaying && GUI.changed) EditorUtility.SetDirty(this);
        }

        public void UpdateStateNamesFromClip(AnimationClip clip)
        {
            if (clip != null)
            {
                var events = AnimationUtility.GetAnimationEvents(clip);
                stateNames = events
                    .Select(ev => !string.IsNullOrEmpty(ev.functionName) ? ev.functionName : ev.stringParameter)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .ToArray();
            }
            else
            {
                stateNames = new string[0];
            }
            if (!Application.isPlaying && GUI.changed) EditorUtility.SetDirty(this);
        }
        #else
        public void UpdateStateNamesArray()
        {
        }
        #endif

        /// <summary>
        /// Update method that handles the tween loop without allocations
        /// </summary>
        private void Update()
        {
            // Handle inspector-driven state changes in play mode
            if (Application.isPlaying && currentStateIndex != prevStateIndex)
            {
                prevStateIndex = currentStateIndex;
                if (stateNames != null && currentStateIndex >= 0 && currentStateIndex < stateNames.Length)
                {
                    TweenToState(stateNames[currentStateIndex]); 
                }
            }

            // Handle active tween updates
            if (_isTweening)
            {
                // Check cancellation by comparing token source references (no allocations)
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

                // Update all properties with their respective easings
                for (int i = 0; i < _tweenItemsCount; i++)
                {
                    var item = _tweenItemsArray[i];
                    if (item.binding.component != null)
                    {
                        // Inline the easing calculation to avoid method call overhead
                        float easedProgress = SampleEasingInline(item.ease, normalizedTime);
                        float interpolatedValue = Mathf.LerpUnclamped(item.initialValue, item.targetValue, easedProgress);
                        item.binding.setter(item.binding.component, interpolatedValue);
                        
#if UNITY_EDITOR
                        if (!Application.isPlaying && GUI.changed)
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
                            if (!Application.isPlaying && GUI.changed)
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
        }

        /// <summary>
        /// Inline easing calculation to avoid method call overhead and potential allocations
        /// </summary>
        private float SampleEasingInline(Ease ease, float time)
        {
            var easingsData = GetEffectiveEasingsData();
            
            int easeIndex = (int)ease;
            if (easingsData != null && easingsData.curves != null && 
                easeIndex >= 0 && easeIndex < easingsData.curves.Length && 
                easingsData.curves[easeIndex] != null)
            {
                return easingsData.curves[easeIndex].Evaluate(time);
            }
            
            // Fallback to default curve generation - cache this to avoid repeated creation
            return GetOrCreateDefaultCurve(ease).Evaluate(time);
        }

        /// <summary>
        /// Get or create a default curve for the given ease, with caching to avoid allocations
        /// </summary>
        private AnimationCurve GetOrCreateDefaultCurve(Ease ease)
        {
            if (!_defaultCurveCache.TryGetValue(ease, out var curve))
            {
                curve = StatefulEasingsData.GetDefaultCurve(ease);
                _defaultCurveCache[ease] = curve;
            }
            return curve;
        }

        private void BuildPropertyOverrideCache()
        {
            _propertyOverrideCache = new Dictionary<(string, string), PropertyOverrideRule>();
            if (groupSettings != null && groupSettings.propertyOverrides != null)
            {
                foreach (var r in groupSettings.propertyOverrides)
                {
                    var key = (r.propertyName, r.componentType ?? "");
                    _propertyOverrideCache[key] = r;
                }
            }
        }

        private void BuildPropertyTransitionCache()
        {
            _propertyTransitionCache.Clear();
            if (stateMachine == null || stateMachine.states == null) return;

            foreach (var state in stateMachine.states)
            {
                foreach (var prop in state.properties)
                {
                    float duration = GetEffectiveTransitionTime();
                    Ease ease = GetEffectiveEase();
                    bool instantEnableDelayedDisable = false;

                    var rule = GetPropertyOverrideRule(prop.propertyName, prop.componentType);
                    if (rule != null)
                    {
                        if (rule.overrideEase) ease = rule.ease;
                        instantEnableDelayedDisable = rule.instantEnableDelayedDisable;
                    }

                    _propertyTransitionCache[prop] = new PropertyTransitionInfo
                    {
                        duration = duration,
                        ease = ease,
                        instantEnableDelayedDisable = instantEnableDelayedDisable
                    };
                }
            }
        }
    }
}