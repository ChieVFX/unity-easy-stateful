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
        public StatefulGroupSettingsData groupSettings;
        public string[] stateNames;
        public int currentStateIndex = 0;

        public bool overrideDefaultTransitionTime = false;
        public float customDefaultTransitionTime = 0.5f;
        public bool overrideDefaultEase = false;
        public Ease customDefaultEase = Ease.Linear;

        // Managers
        private PropertyBindingManager bindingManager;
        private StatefulSettingsResolver settingsResolver;
        private StatefulEasingManager easingManager;
        private StatefulTweenEngine tweenEngine;
        private StatefulStateManager stateManager;

        // Track inspector changes
        private int prevStateIndex = -1;

#if UNITY_EDITOR
        [SerializeField] private AnimationClip editorClip;
        public bool IsTweening => tweenEngine?.IsTweening ?? false;
        public void EditorUpdate() => tweenEngine?.UpdateTween();
#endif

        void Awake()
        {
            InitializeIfNeeded();
        }

        void OnDestroy()
        {
            tweenEngine?.Cleanup();
        }

        private void InitializeIfNeeded()
        {
            if (bindingManager == null)
            {
                bindingManager = new PropertyBindingManager(transform);
                settingsResolver = new StatefulSettingsResolver(this);
                easingManager = new StatefulEasingManager(settingsResolver);
                tweenEngine = new StatefulTweenEngine(this, easingManager);
                stateManager = new StatefulStateManager();
                
                LoadFromAsset(statefulDataAsset);
            }
        }

        public void LoadFromAsset(StatefulDataAsset dataAsset)
        {
            InitializeIfNeeded();
            
            var stateMachine = dataAsset?.stateMachine ?? new UIStateMachine();
            stateManager.LoadStateMachine(stateMachine);
            stateNames = stateManager.StateNames;
            
            settingsResolver.BuildPropertyOverrideCache();
            stateManager.BuildPropertyTransitionCache(settingsResolver);
        }

        public void SnapToState(string stateName)
        {
            InitializeIfNeeded();
            var state = stateManager.FindState(stateName);
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

        public UniTask TaskTweenToState(string stateName, float? duration = null, Ease? ease = null, CancellationToken cancellationToken = default)
        {
            InitializeIfNeeded();
            
            var state = stateManager.FindState(stateName);
            if (state == null)
            {
                Debug.LogWarning($"State not found: {stateName}", this);
                return UniTask.CompletedTask;
            }

            float finalDuration = duration ?? settingsResolver.GetEffectiveTransitionTime();
            Ease finalEase = ease ?? settingsResolver.GetEffectiveEase();

            return tweenEngine.TweenToState(state, finalDuration, finalEase, bindingManager, 
                settingsResolver, stateManager.GetPropertyTransitionCache());
        }

        public void TweenToState(string stateName, float? duration = null, Ease? ease = null)
        {
            TaskTweenToState(stateName, duration, ease, CancellationToken.None).Forget();
        }

        private void Update()
        {
            // Handle inspector-driven state changes
            if (Application.isPlaying && currentStateIndex != prevStateIndex)
            {
                prevStateIndex = currentStateIndex;
                if (stateNames != null && currentStateIndex >= 0 && currentStateIndex < stateNames.Length)
                {
                    TweenToState(stateNames[currentStateIndex]);
                }
            }

            tweenEngine?.UpdateTween();
        }

        private void ApplyProperty(Property prop)
        {
            InitializeIfNeeded();
            
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
                    Debug.LogError($"Error executing compiled object setter for {prop.propertyName} on {binding.component.GetType().Name} with object {obj?.name} (Path: {prop.objectReference}): {ex.Message}", this);
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
                     Debug.LogError($"Error executing compiled numeric setter for {prop.propertyName} on {binding.component.GetType().Name}: {ex.Message}", this);
                }
#if UNITY_EDITOR
                if (!Application.isPlaying) EditorUtility.SetDirty(binding.component);
#endif
            }
        }

        public void UpdateStateNamesArray()
        {
            stateManager?.UpdateStateNamesArray();
            stateNames = stateManager?.StateNames ?? new string[0];
        }

        // Add missing OnValidate method
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (statefulDataAsset != null)
            {
                LoadFromAsset(statefulDataAsset);
            }
            else 
            {
                InitializeIfNeeded();
                stateManager.LoadStateMachine(new UIStateMachine());
                UpdateStateNamesArray();
            }
            
            // Force rebuild caches in editor - but only once per validation
            if (settingsResolver != null && stateManager != null)
            {
                settingsResolver.BuildPropertyOverrideCache();
                stateManager.BuildPropertyTransitionCache(settingsResolver);
            }
        }
#endif

        public void InvalidatePropertyTransitionCache()
        {
            if (settingsResolver != null && stateManager != null)
            {
                settingsResolver.BuildPropertyOverrideCache();
                stateManager.BuildPropertyTransitionCache(settingsResolver);
            }
        }
    }
}