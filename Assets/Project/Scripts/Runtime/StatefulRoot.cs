using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using System.Linq;
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

        // Cache per-path and per-property binding info for fast reflection
        private Dictionary<string, Dictionary<string, PropertyBinding>> bindingCache = new Dictionary<string, Dictionary<string, PropertyBinding>>();

        [Serializable]
        private class PropertyBinding
        {
            public Component component;
            public GameObject targetGameObject;
            public string propertyName;
            public Action<Component, float> setter;
            public Action<Component, UnityEngine.Object> setterObj;
        }

        // Track inspector-driven changes at runtime
        private int prevStateIndex = -1;

        #if UNITY_EDITOR
        [SerializeField]
        private AnimationClip editorClip;
        #endif

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
            // Tier 1: Group Settings Property Override
            if (groupSettings != null && groupSettings.propertyOverrides != null)
            {
                var groupSpecificRule = groupSettings.propertyOverrides.FirstOrDefault(r =>
                    r.propertyName == propertyName &&
                    !string.IsNullOrEmpty(r.componentType) &&
                    r.componentType == componentTypeFullName);
                if (groupSpecificRule != null) return groupSpecificRule;

                var groupGeneralRule = groupSettings.propertyOverrides.FirstOrDefault(r =>
                    r.propertyName == propertyName &&
                    string.IsNullOrEmpty(r.componentType));
                if (groupGeneralRule != null) return groupGeneralRule;
            }

            // Tier 2: Global Settings Property Override
            return StatefulGlobalSettings.GetGlobalPropertyOverrideRule(propertyName, componentTypeFullName);
        }

        void Awake()
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
        }

        /// <summary>
        /// Immediately apply all property values in the given state.
        /// </summary>
        public void SnapToState(string stateName)
        {
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
        /// Tween all numeric properties in the given state over time.
        /// Object references will snap instantly.
        /// </summary>
        public void TweenToState(string stateName, float? duration = null, Ease? ease = null)
        {
            if (stateMachine == null || stateMachine.states == null)
            {
                Debug.LogWarning($"State machine not loaded or has no states. Cannot tween to '{stateName}'.", this);
                return;
            }
            
            DOTween.Kill(this, true); // Kill existing tweens on this target
            
            var state = stateMachine.states.Find(s => s.name == stateName);
            if (state == null)
            {
                Debug.LogWarning($"State not found: {stateName}", this);
                return;
            }

            float overallDuration = duration ?? GetEffectiveTransitionTime();
            Ease overallEase = ease ?? GetEffectiveEase();

            List<Property> propertiesToSnap = new List<Property>();
            Dictionary<(float duration, Ease ease), List<(Property prop, PropertyBinding binding, float initialValue)>> propertiesToTweenGrouped =
                new Dictionary<(float, Ease), List<(Property, PropertyBinding, float)>>();

            foreach (var prop in state.properties)
            {
                float finalPropDuration = overallDuration;
                Ease finalPropEase = overallEase;

                var binding = GetOrCreateBinding(prop, out Transform target); // Target transform also available via out param
                if (binding == null)
                {
                    // GetOrCreateBinding already logs a warning if path/component not found
                    continue;
                }

                PropertyOverrideRule rule = GetPropertyOverrideRule(prop.propertyName, prop.componentType);
                bool handledBySpecialRule = false;

                if (rule != null)
                {
                    if (prop.propertyName == "m_IsActive" && rule.instantEnableDelayedDisable)
                    {
                        if (binding.targetGameObject != null)
                        {
                            bool targetActiveState = prop.value > 0.5f;
                            if (targetActiveState) 
                            {
                                binding.targetGameObject.SetActive(true); 
                            }
                            else 
                            {
                                var gameObjectToDisable = binding.targetGameObject;
                                DOVirtual.DelayedCall(overallDuration, () => { // Uses overallDuration for delay
                                    if (gameObjectToDisable != null) gameObjectToDisable.SetActive(false);
                                }, false)
                                .SetTarget(this)
    #if UNITY_EDITOR
                                .SetUpdate(Application.isPlaying ? UpdateType.Normal : UpdateType.Manual)
    #endif
                                ;
                            }
                        }
                        handledBySpecialRule = true;
                    }
                    else if (rule.instantEnableDelayedDisable) // For non-m_IsActive properties that should be instant
                    {
                        finalPropDuration = 0f;
                    }
                    
                    if (!handledBySpecialRule)
                    {
                        // Apply duration override only if not made instant by 'instantEnableDelayedDisable' for non-m_IsActive
                        if (!(rule.instantEnableDelayedDisable && prop.propertyName != "m_IsActive"))
                        {
                            if (rule.overrideDuration) finalPropDuration = rule.duration;
                        }
                        // Ease can always be overridden if the rule specifies it
                        if (rule.overrideEase) finalPropEase = rule.ease;
                    }
                }

                if (handledBySpecialRule)
                {
                    continue; // Property was fully handled by a special rule
                }

                bool isObjectRef = !string.IsNullOrEmpty(prop.objectReference) || binding.setterObj != null;
                // Check if it's m_IsActive (and not the special rule version which was 'continue'd)
                // binding.targetGameObject check ensures we only consider it if it's a valid GameObject active state property
                bool isGenericActiveProperty = prop.propertyName == "m_IsActive" && binding.targetGameObject != null;

                if (finalPropDuration == 0f || isObjectRef || isGenericActiveProperty)
                {
                    propertiesToSnap.Add(prop);
                }
                else if (binding.setter != null && binding.component != null) // Numeric, tweenable property
                {
                    float initialValue = GetCurrentValue(binding);
                    var key = (duration: finalPropDuration, ease: finalPropEase);
                    if (!propertiesToTweenGrouped.ContainsKey(key))
                    {
                        propertiesToTweenGrouped[key] = new List<(Property prop, PropertyBinding binding, float initialValue)>();
                    }
                    propertiesToTweenGrouped[key].Add((prop, binding, initialValue));
                }
                else
                {
                    // This case implies it's not snappable by the above conditions, and not a standard numeric tweenable property.
                    // e.g. m_IsActive but targetGameObject was somehow null in binding (should be caught earlier by binding creation).
                    // Or a property that has no setter but also isn't an object ref.
                    Debug.LogWarning($"Property '{prop.propertyName}' on path '{prop.path}' for component type '{prop.componentType}' could not be categorized for tweening or snapping. It may be missing a valid setter or not be an object reference.", this);
                }
            }

            // Apply all snapped properties first
            foreach (var propToSnap in propertiesToSnap)
            {
                ApplyProperty(propToSnap);
            }

            // Create grouped tweens
            foreach (var kvp in propertiesToTweenGrouped)
            {
                var groupKey = kvp.Key; // (duration, ease)
                var propsInGroup = kvp.Value; // List of (Property, PropertyBinding, initialValue)

                if (propsInGroup.Count == 0) continue;

                // This DOVirtual.Float tweens a dummy value from 0 to 1,
                // representing the normalized progress of this group's animation.
                DOVirtual.Float(0f, 1f, groupKey.duration, progress =>
                {
                    foreach (var item in propsInGroup)
                    {
                        // item is (Property prop, PropertyBinding binding, float initialValue)
                        // LerpUnclamped is used because the ease is applied to the 'progress' value itself.
                        float interpolatedValue = Mathf.LerpUnclamped(item.initialValue, item.prop.value, progress);
                        
                        if (item.binding.component != null) // Ensure component is not null
                        {
                            item.binding.setter(item.binding.component, interpolatedValue);
    #if UNITY_EDITOR
                            if (!Application.isPlaying) // Only in Editor and not Play Mode
                            {
                                // Mark the specific component whose property was changed as dirty.
                                // This encourages the editor to repaint views that display this component's state.
                                EditorUtility.SetDirty(item.binding.component);
                            }
    #endif
                        }
                    }
                })
                .SetEase(groupKey.ease)
                .SetTarget(this)
    #if UNITY_EDITOR
                .SetUpdate(Application.isPlaying ? UpdateType.Normal : UpdateType.Manual)
    #endif
                ;
            }
        }

        private void ApplyProperty(Property prop)
        {
            var binding = GetOrCreateBinding(prop, out Transform target);
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
                // Load object from Resources (path without extension)
                var obj = Resources.Load(prop.objectReference);
                binding.setterObj(binding.component, obj);
    #if UNITY_EDITOR
                if (!Application.isPlaying) EditorUtility.SetDirty(binding.component);
    #endif
            }
            else if (binding.setter != null && binding.component != null)
            {
                binding.setter(binding.component, prop.value);
    #if UNITY_EDITOR
                if (!Application.isPlaying) EditorUtility.SetDirty(binding.component);
    #endif
            }
        }

        private void TweenProperty(Property prop, float duration, Ease ease)
        {
            // This method is no longer used. Its logic has been integrated into TweenToState and ApplyProperty.
            // Consider removing this method to avoid confusion.
            // For now, let's make it a no-op or log a warning if called.
            Debug.LogWarning("TweenProperty is deprecated and should not be called directly.", this);
        }

        /// <summary>
        /// Retrieves or creates a binding for this property.
        /// </summary>
        private PropertyBinding GetOrCreateBinding(Property prop, out Transform target)
        {
            target = transform.Find(prop.path);
            if (target == null)
            {
                Debug.LogWarning($"Path not found: {prop.path}");
                return null;
            }

            if (!bindingCache.TryGetValue(prop.path, out var compMap))
            {
                compMap = new Dictionary<string, PropertyBinding>();
                bindingCache[prop.path] = compMap;
            }

            string cacheKey = $"{prop.componentType}_{prop.propertyName}"; // Ensure unique key if componentType can vary for same prop name
            if (compMap.TryGetValue(cacheKey, out var binding))
            {
                return binding;
            }
            
            // Special handling for GameObject.m_IsActive
            if (prop.propertyName == "m_IsActive" && 
                (string.IsNullOrEmpty(prop.componentType) || prop.componentType == typeof(GameObject).FullName || prop.componentType == typeof(GameObject).AssemblyQualifiedName))
            {
                binding = new PropertyBinding { targetGameObject = target.gameObject, propertyName = prop.propertyName };
                compMap[cacheKey] = binding;
                return binding;
            }

            // Create new binding for regular components
            var compType = Type.GetType(prop.componentType);
            if (compType == null)
            {
                Debug.LogWarning($"Component type not found: {prop.componentType}");
                return null;
            }

            var comp = target.GetComponent(compType);
            if (comp == null)
            {
                Debug.LogWarning($"Component {compType} not found on {prop.path}");
                return null;
            }

            binding = new PropertyBinding { component = comp, targetGameObject = target.gameObject, propertyName = prop.propertyName };

            bool isObjectRef = !string.IsNullOrEmpty(prop.objectReference);

            if (isObjectRef)
            {
                // Setup setterObj for object references
                var pi = compType.GetProperty(prop.propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var fi = compType.GetField(prop.propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (pi != null && pi.CanWrite)
                {
                    var setter = pi.GetSetMethod(true);
                    binding.setterObj = (c, o) => setter.Invoke(c, new object[] { o });
                }
                else if (fi != null)
                {
                    binding.setterObj = (c, o) => fi.SetValue(c, o);
                }
            }
            else
            {
                // Setup numeric setter (handles struct fields like m_Color.r or m_AnchoredPosition.x)
                if (prop.propertyName.Contains("."))
                {
                    var parts = prop.propertyName.Split('.');
                    var mainName = parts[0];
                    var subName = parts[1];
                    // Try the internal member name
                    var pi = compType.GetProperty(mainName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var fi = compType.GetField(mainName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    // Fallback to public property if backing field not found
                    if (pi == null && fi == null && mainName.StartsWith("m_"))
                    {
                        var fallbackName = char.ToLowerInvariant(mainName[2]) + mainName.Substring(3);
                        pi = compType.GetProperty(fallbackName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                    if (pi != null && pi.CanRead && pi.CanWrite)
                    {
                        binding.setter = (c, val) =>
                        {
                            var structVal = pi.GetValue(c, null);
                            var stType = structVal.GetType();
                            // Try property or field on the struct
                            var subPi = stType.GetProperty(subName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            var subFi = stType.GetField(subName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (subPi != null && subPi.CanWrite)
                                subPi.SetValue(structVal, Convert.ChangeType(val, subPi.PropertyType), null);
                            else if (subFi != null)
                                subFi.SetValue(structVal, Convert.ChangeType(val, subFi.FieldType));
                            // Write back composite struct
                            pi.SetValue(c, structVal, null);
                        };
                    }
                    else if (fi != null)
                    {
                        binding.setter = (c, val) =>
                        {
                            var structVal = fi.GetValue(c);
                            var stType = structVal.GetType();
                            var subPi = stType.GetProperty(subName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            var subFi = stType.GetField(subName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (subPi != null && subPi.CanWrite)
                                subPi.SetValue(structVal, Convert.ChangeType(val, subPi.PropertyType), null);
                            else if (subFi != null)
                                subFi.SetValue(structVal, Convert.ChangeType(val, subFi.FieldType));
                            fi.SetValue(c, structVal);
                        };
                    }
                }
                else
                {
                    var pi = compType.GetProperty(prop.propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var fi = compType.GetField(prop.propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (pi != null && pi.CanWrite)
                    {
                        binding.setter = (c, val) => pi.SetValue(c, Convert.ChangeType(val, pi.PropertyType), null);
                    }
                    else if (fi != null)
                    {
                        binding.setter = (c, val) => fi.SetValue(c, Convert.ChangeType(val, fi.FieldType));
                    }
                }
            }

            compMap[cacheKey] = binding;
            return binding;
        }

        /// <summary>
        /// Get the current numeric value for the binding.
        /// </summary>
        private float GetCurrentValue(PropertyBinding binding)
        {
            if (binding.propertyName == "m_IsActive" && binding.targetGameObject != null)
            {
                return binding.targetGameObject.activeSelf ? 1f : 0f;
            }
            if (binding.component == null)
            {
                Debug.LogWarning($"Cannot get current value for '{binding.propertyName}': Component is null in binding.");
                return 0f;
            }

            var compType = binding.component.GetType();
            var name = binding.propertyName;
            if (name.Contains("."))
            {
                var parts = name.Split('.');
                var mainName = parts[0];
                var subName = parts[1];
                // Try the internal member name
                var pi = compType.GetProperty(mainName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var fi = compType.GetField(mainName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                // Fallback to public property if backing field not found
                if (pi == null && fi == null && mainName.StartsWith("m_"))
                {
                    var fallbackName = char.ToLowerInvariant(mainName[2]) + mainName.Substring(3);
                    pi = compType.GetProperty(fallbackName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                }
                object structVal = null;
                if (pi != null) structVal = pi.GetValue(binding.component, null);
                else if (fi != null) structVal = fi.GetValue(binding.component);
                if (structVal == null) return 0f;
                var stType = structVal.GetType();
                // Get sub-member
                var subPi = stType.GetProperty(subName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var subFi = stType.GetField(subName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (subPi != null)
                    return Convert.ToSingle(subPi.GetValue(structVal, null));
                if (subFi != null)
                    return Convert.ToSingle(subFi.GetValue(structVal));
                Debug.LogWarning($"Sub-member not found: {subName} on {stType}");
                return 0f;
            }
            else
            {
                var pi = compType.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var fi = compType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var val = pi != null ? pi.GetValue(binding.component, null) : fi.GetValue(binding.component);
                return Convert.ToSingle(val);
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
            // Note: `groupSettings` changes don't directly alter `stateNames`
            // but might influence behavior if `Update()` calls `TweenToState` due to `currentStateIndex` change.
        }

        [ContextMenu("Update State Names Array")]
        public void UpdateStateNamesArray()
        {
            if (stateMachine != null && stateMachine.states != null)
                stateNames = stateMachine.states.Select(s => s.name).ToArray();
            else
                stateNames = new string[0];
            
            if (!Application.isPlaying) EditorUtility.SetDirty(this);
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
            if (!Application.isPlaying) EditorUtility.SetDirty(this);
        }
        #else
        public void UpdateStateNamesArray()
        {
        }
        #endif

        private void Update()
        {
            if (Application.isPlaying && currentStateIndex != prevStateIndex) // Only run this logic in play mode
            {
                prevStateIndex = currentStateIndex;
                if (stateNames != null && currentStateIndex >= 0 && currentStateIndex < stateNames.Length)
                {
                    // TweenToState will use GetEffectiveTransitionTime and GetEffectiveEase
                    TweenToState(stateNames[currentStateIndex]); 
                }
            }
        }
    }
}