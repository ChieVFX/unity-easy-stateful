using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using DG.Tweening;
using System.Linq;
using System.Linq.Expressions;
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
        
        // Pooled collections for TweenToState to reduce GC
        private List<Property> _pooledPropertiesToSnap = new List<Property>();
        // For _pooledPropertiesToTweenGrouped, we'll start by clearing its internal lists if they exist,
        // and the dictionary itself. Full pooling of inner lists is more complex and can be a later step if needed.
        private Dictionary<(float duration, Ease ease), List<(Property prop, PropertyBinding binding, float initialValue)>> _pooledPropertiesToTweenGrouped =
            new Dictionary<(float, Ease), List<(Property, PropertyBinding, float)>>();

        [Serializable]
        private class PropertyBinding
        {
            public Component component;
            public GameObject targetGameObject;
            public string propertyName; // Full property name, e.g., "m_Color.r" or "m_IsEnabled"

            // For numeric properties (float, int, bool interpreted as 0/1)
            public Action<Component, float> setter;
            public Func<Component, float> getter;

            // For object reference properties
            public Action<Component, UnityEngine.Object> setterObj;

            // Cached reflection info - used by GetOrCreateBinding to compile expressions
            internal PropertyInfo propInfo;
            internal FieldInfo fieldInfo;
            internal Type valueType; // The actual type of the property/field (e.g., float, Color, Vector3)

            internal PropertyInfo mainPropInfo;
            internal FieldInfo mainFieldInfo;
            internal Type mainValueType; // Type of the container (e.g., Vector3, Color)

            internal PropertyInfo subPropInfo;
            internal FieldInfo subFieldInfo;
            internal Type subValueType; // Type of the sub-member (e.g., float)
            
            internal bool isSubProperty = false; 
            internal bool isGameObjectActiveProperty = false;
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
                // Check for specific rule (component type match)
                for (int i = 0; i < groupSettings.propertyOverrides.Count; i++)
                {
                    var r = groupSettings.propertyOverrides[i];
                    if (r.propertyName == propertyName &&
                        !string.IsNullOrEmpty(r.componentType) &&
                        r.componentType == componentTypeFullName)
                    {
                        return r;
                    }
                }
                // Check for general rule (no component type specified, matches any)
                for (int i = 0; i < groupSettings.propertyOverrides.Count; i++)
                {
                    var r = groupSettings.propertyOverrides[i];
                    if (r.propertyName == propertyName &&
                        string.IsNullOrEmpty(r.componentType))
                    {
                        return r;
                    }
                }
            }

            // Tier 2: Global Settings Property Override
            // (Assuming StatefulGlobalSettings.GetGlobalPropertyOverrideRule is already efficient or its list is small)
            // If StatefulGlobalSettings also uses a List and FirstOrDefault, it should be refactored similarly.
            // For now, only changing the local part.
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
            
            DOTween.Kill(this, true); 
            
            var state = stateMachine.states.Find(s => s.name == stateName);
            if (state == null)
            {
                Debug.LogWarning($"State not found: {stateName}", this);
                return;
            }

            float overallDuration = duration ?? GetEffectiveTransitionTime();
            Ease overallEase = ease ?? GetEffectiveEase();

            // Use pooled collections
            _pooledPropertiesToSnap.Clear();
            List<Property> propertiesToSnap = _pooledPropertiesToSnap;

            // Clear dictionary and its lists' contents
            foreach(var list in _pooledPropertiesToTweenGrouped.Values)
            {
                list.Clear(); 
            }
            _pooledPropertiesToTweenGrouped.Clear();
            Dictionary<(float duration, Ease ease), List<(Property prop, PropertyBinding binding, float initialValue)>> propertiesToTweenGrouped =
                _pooledPropertiesToTweenGrouped;

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
                else if (binding.setter != null && binding.component != null && binding.getter != null) // Numeric, tweenable property
                {
                    float initialValue = GetCurrentValue(binding);
                    var key = (duration: finalPropDuration, ease: finalPropEase);
                    if (!propertiesToTweenGrouped.ContainsKey(key))
                    {
                        // For now, still new-ing up inner lists. Pooling these is next if GC remains high from here.
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
                Debug.LogWarning($"Path not found: {prop.path} for property {prop.propertyName} on component type {prop.componentType}", this);
                return null;
            }

            if (!bindingCache.TryGetValue(prop.path, out var compMap))
            {
                compMap = new Dictionary<string, PropertyBinding>();
                bindingCache[prop.path] = compMap;
            }

            string cacheKey = $"{prop.componentType}_{prop.propertyName}";
            if (compMap.TryGetValue(cacheKey, out var binding))
            {
                return binding;
            }
            
            // Special handling for GameObject.m_IsActive (already optimized)
            if (prop.propertyName == "m_IsActive" && 
                (string.IsNullOrEmpty(prop.componentType) || prop.componentType == typeof(GameObject).FullName || prop.componentType == typeof(GameObject).AssemblyQualifiedName))
            {
                binding = new PropertyBinding { 
                    targetGameObject = target.gameObject, 
                    propertyName = prop.propertyName, 
                    isGameObjectActiveProperty = true 
                };
                // Getter for m_IsActive (simple, no component cast needed in lambda)
                binding.getter = (_) => binding.targetGameObject.activeSelf ? 1f : 0f;
                // Setter for m_IsActive is handled by ApplyProperty for snapping, not numeric tweening.
                compMap[cacheKey] = binding;
                return binding;
            }

            var compType = Type.GetType(prop.componentType);
            if (compType == null)
            {
                Debug.LogWarning($"Component type not found: {prop.componentType} for path {prop.path}, property {prop.propertyName}", this);
                return null;
            }

            var comp = target.GetComponent(compType);
            if (comp == null)
            {
                Debug.LogWarning($"Component {compType.Name} not found on {prop.path} for property {prop.propertyName}", this);
                return null;
            }

            binding = new PropertyBinding { component = comp, targetGameObject = target.gameObject, propertyName = prop.propertyName };

            bool isObjectRef = !string.IsNullOrEmpty(prop.objectReference);

            if (isObjectRef)
            {
                var pi = compType.GetProperty(prop.propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var fi = compType.GetField(prop.propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                
                if (pi != null) binding.propInfo = pi;
                else if (fi != null) binding.fieldInfo = fi;
                else {
                    Debug.LogWarning($"Object reference Property/Field '{prop.propertyName}' not found on type '{compType.Name}' for path {prop.path}.", this);
                    compMap[cacheKey] = binding; 
                    return binding; 
                }

                Type memberType = pi?.PropertyType ?? fi?.FieldType;

                if (pi != null && pi.CanWrite)
                {
                    var setterMethod = pi.GetSetMethod(true);
                    if (setterMethod != null)
                    {
                        try
                        {
                            var componentParamExpr = Expression.Parameter(typeof(Component), "c_");
                            var objectParamExpr = Expression.Parameter(typeof(UnityEngine.Object), "o_");
                            var castCompExpr = Expression.Convert(componentParamExpr, compType);
                            // It's crucial to cast the incoming UnityEngine.Object to the specific type the property expects.
                            var castObjExpr = Expression.Convert(objectParamExpr, memberType);
                            var callSetterExpr = Expression.Call(castCompExpr, setterMethod, castObjExpr);
                            binding.setterObj = Expression.Lambda<Action<Component, UnityEngine.Object>>(callSetterExpr, componentParamExpr, objectParamExpr).Compile();
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Failed to compile object setter for Property '{prop.propertyName}' on type '{compType.Name}' (path {prop.path}): {ex.Message}. Will fallback to reflection invoke.", this);
                            binding.setterObj = (c, o) => setterMethod.Invoke(c, new object[] { o }); // Fallback
                        }
                    }
                    else
                    {
                        Debug.LogWarning($"Object reference Property '{prop.propertyName}' on type '{compType.Name}' for path {prop.path} has no writable setter.", this);
                    }
                }
                else if (fi != null && !fi.IsLiteral && !fi.IsInitOnly)
                {
                    try
                    {
                        var componentParamExpr = Expression.Parameter(typeof(Component), "c_");
                        var objectParamExpr = Expression.Parameter(typeof(UnityEngine.Object), "o_");
                        var castCompExpr = Expression.Convert(componentParamExpr, compType);
                        var castObjExpr = Expression.Convert(objectParamExpr, memberType);
                        var fieldAccessExpr = Expression.Field(castCompExpr, fi);
                        var assignExpr = Expression.Assign(fieldAccessExpr, castObjExpr);
                        binding.setterObj = Expression.Lambda<Action<Component, UnityEngine.Object>>(assignExpr, componentParamExpr, objectParamExpr).Compile();
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to compile object setter for Field '{prop.propertyName}' on type '{compType.Name}' (path {prop.path}): {ex.Message}. Will fallback to reflection invoke.", this);
                        binding.setterObj = (c, o) => fi.SetValue(c, o); // Fallback
                    }
                }
                else if (fi != null && (fi.IsLiteral || fi.IsInitOnly))
                {
                     Debug.LogWarning($"Object reference Field '{prop.propertyName}' on type '{compType.Name}' for path {prop.path} is read-only.", this);
                }
            }
            else // Numeric property, use Expression Trees for getter/setter
            {
                // Parameter expressions for the delegates
                var componentParam = Expression.Parameter(typeof(Component), "c");
                var valueParam = Expression.Parameter(typeof(float), "val"); // For setter

                Expression castComponent = Expression.Convert(componentParam, compType);
                Expression finalPropertyAccess = null; 
                Type targetPropertyType = null; 

                if (prop.propertyName.Contains("."))
                {
                    binding.isSubProperty = true;
                    var parts = prop.propertyName.Split('.');
                    var mainName = parts[0];
                    var subName = parts[1];

                    binding.mainPropInfo = compType.GetProperty(mainName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (binding.mainPropInfo == null) binding.mainFieldInfo = compType.GetField(mainName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (binding.mainPropInfo == null && binding.mainFieldInfo == null && mainName.StartsWith("m_"))
                    {
                        var fallbackName = char.ToLowerInvariant(mainName[2]) + mainName.Substring(3);
                        binding.mainPropInfo = compType.GetProperty(fallbackName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    }
                    
                    Expression mainMemberAccess = null;
                    Type mainMemberType = null;

                    if (binding.mainPropInfo != null) { 
                        mainMemberAccess = Expression.Property(castComponent, binding.mainPropInfo);
                        mainMemberType = binding.mainPropInfo.PropertyType;
                    } else if (binding.mainFieldInfo != null) {
                        mainMemberAccess = Expression.Field(castComponent, binding.mainFieldInfo);
                        mainMemberType = binding.mainFieldInfo.FieldType;
                    } else {
                        Debug.LogWarning($"Main member '{mainName}' not found for sub-property '{prop.propertyName}' on type '{compType.Name}' for path {prop.path}. Cannot create binding.", this);
                        compMap[cacheKey] = binding; return binding; // Cache incomplete binding
                    }
                    binding.mainValueType = mainMemberType;

                    binding.subPropInfo = mainMemberType.GetProperty(subName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (binding.subPropInfo == null) binding.subFieldInfo = mainMemberType.GetField(subName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (binding.subPropInfo != null) {
                        finalPropertyAccess = Expression.Property(mainMemberAccess, binding.subPropInfo);
                        targetPropertyType = binding.subPropInfo.PropertyType;
                    } else if (binding.subFieldInfo != null) {
                        finalPropertyAccess = Expression.Field(mainMemberAccess, binding.subFieldInfo);
                        targetPropertyType = binding.subFieldInfo.FieldType;
                    } else {
                        Debug.LogWarning($"Sub member '{subName}' not found on main member '{mainName}' (type: {mainMemberType.Name}) for property '{prop.propertyName}' on path {prop.path}. Cannot create binding.", this);
                        compMap[cacheKey] = binding; return binding; // Cache incomplete binding
                    }
                    binding.subValueType = targetPropertyType;
                }
                else 
                {
                    binding.propInfo = compType.GetProperty(prop.propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (binding.propInfo == null) binding.fieldInfo = compType.GetField(prop.propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                    if (binding.propInfo != null) {
                        finalPropertyAccess = Expression.Property(castComponent, binding.propInfo);
                        targetPropertyType = binding.propInfo.PropertyType;
                    } else if (binding.fieldInfo != null) {
                        finalPropertyAccess = Expression.Field(castComponent, binding.fieldInfo);
                        targetPropertyType = binding.fieldInfo.FieldType;
                    } else {
                        Debug.LogWarning($"Property/Field '{prop.propertyName}' not found on type '{compType.Name}' for path {prop.path}. Cannot create binding.", this);
                        compMap[cacheKey] = binding; return binding; // Cache incomplete binding
                    }
                    binding.valueType = targetPropertyType;
                }

                if (finalPropertyAccess == null || targetPropertyType == null)
                {
                    // Should have been caught above, but as a safeguard
                    Debug.LogWarning($"Failed to establish property access for '{prop.propertyName}' on '{compType.Name}' (path {prop.path}). Binding will be incomplete.", this);
                    compMap[cacheKey] = binding; return binding;
                }

                // Create Getter: Func<Component, float>
                try
                {
                    Expression getterBody = finalPropertyAccess;
                    if (targetPropertyType != typeof(float))
                    {
                        if (targetPropertyType == typeof(bool))
                        {
                            getterBody = Expression.Condition(Expression.IsTrue(getterBody), Expression.Constant(1f), Expression.Constant(0f));
                        }
                        else
                        {
                            getterBody = Expression.Convert(getterBody, typeof(float));
                        }
                    }
                    var getterLambda = Expression.Lambda<Func<Component, float>>(getterBody, componentParam);
                    binding.getter = getterLambda.Compile();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to compile getter for {prop.propertyName} on {compType.Name} (path {prop.path}): {ex.Message}. This property may not be readable for tweening.", this);
                    binding.getter = null; 
                }
                
                // Determine if a setter can be attempted
                bool canAttemptSetterCompilation = false;
                if (binding.isSubProperty) {
                    if (binding.mainValueType.IsValueType) { // Sub-property of a struct
                        canAttemptSetterCompilation = (binding.subPropInfo != null || binding.subFieldInfo != null) && // Sub-member must exist
                                                      (binding.mainPropInfo?.CanWrite == true || binding.mainFieldInfo?.IsInitOnly == false); // Main struct must be settable
                    } else { // Sub-property of a class
                        canAttemptSetterCompilation = (binding.subPropInfo?.CanWrite == true) || (binding.subFieldInfo != null && !binding.subFieldInfo.IsInitOnly && !binding.subFieldInfo.IsLiteral);
                    }
                } else { // Simple property
                    canAttemptSetterCompilation = (binding.propInfo?.CanWrite == true) || (binding.fieldInfo != null && !binding.fieldInfo.IsInitOnly && !binding.fieldInfo.IsLiteral);
                }

                if (!canAttemptSetterCompilation)
                {
                    // Debug.LogWarning($"Property/Field '{prop.propertyName}' on type '{compType.Name}' (path {prop.path}) is not considered writable. It will not be numerically tweenable.", this);
                    // binding.setter will remain null
                }
                else
                {
                    try
                    {
                        Expression valueToSet = valueParam; 
                        if (targetPropertyType != typeof(float))
                        {
                            if (targetPropertyType == typeof(bool))
                            {
                                valueToSet = Expression.GreaterThan(valueParam, Expression.Constant(0.5f));
                            }
                            else
                            {
                                valueToSet = Expression.Convert(valueParam, targetPropertyType);
                            }
                        }

                        Expression assignExpression;
                        if (binding.isSubProperty && binding.mainValueType.IsValueType && (binding.mainPropInfo != null || binding.mainFieldInfo != null))
                        {
                            Expression mainStructReadAccess = (binding.mainPropInfo != null)
                                ? Expression.Property(castComponent, binding.mainPropInfo)
                                : Expression.Field(castComponent, binding.mainFieldInfo);

                            var structVar = Expression.Variable(binding.mainValueType, "tempStruct");
                            var assignToStructVar = Expression.Assign(structVar, mainStructReadAccess);

                            Expression subMemberAccessOnStructVar = (binding.subPropInfo != null)
                                ? Expression.Property(structVar, binding.subPropInfo)
                                : Expression.Field(structVar, binding.subFieldInfo);
                            
                            var assignToSubMember = Expression.Assign(subMemberAccessOnStructVar, valueToSet);
                            
                            Expression mainStructWriteAccess = (binding.mainPropInfo != null && binding.mainPropInfo.CanWrite)
                                ? Expression.Property(castComponent, binding.mainPropInfo) 
                                : (binding.mainFieldInfo != null && !binding.mainFieldInfo.IsInitOnly ? Expression.Field(castComponent, binding.mainFieldInfo) : null);

                            if (mainStructWriteAccess == null) {
                                throw new InvalidOperationException($"Main struct property/field '{binding.mainPropInfo?.Name ?? binding.mainFieldInfo?.Name}' is not writable.");
                            }
                            
                            var assignStructVarBack = Expression.Assign(mainStructWriteAccess, structVar);

                            assignExpression = Expression.Block(
                                new[] { structVar }, 
                                assignToStructVar,
                                assignToSubMember,
                                assignStructVarBack
                            );
                        }
                        else 
                        {
                            assignExpression = Expression.Assign(finalPropertyAccess, valueToSet);
                        }

                        var setterLambda = Expression.Lambda<Action<Component, float>>(assignExpression, componentParam, valueParam);
                        binding.setter = setterLambda.Compile();
                    }
                    catch (Exception ex)
                    {
                         Debug.LogError($"Failed to compile setter for {prop.propertyName} on {compType.Name} (path {prop.path}): {ex.Message}. This property will not be numerically tweenable.", this);
                         binding.setter = null; 
                    }
                }
            }

            compMap[cacheKey] = binding; // Cache the binding, complete or not
            return binding;
        }

        /// <summary>
        /// Get the current numeric value for the binding.
        /// </summary>
        private float GetCurrentValue(PropertyBinding binding)
        {
            if (binding.isGameObjectActiveProperty) // Uses its own specialized getter
            {
                 if (binding.targetGameObject == null) {
                    Debug.LogWarning($"Cannot get current value for '{binding.propertyName}': targetGameObject is null in binding for an m_IsActive property.", this);
                    return 0f;
                 }
                return binding.getter(null); // Component parameter is not used by this getter
            }

            if (binding.component == null)
            {
                // This path should ideally be caught by GetOrCreateBinding returning null if component is missing
                Debug.LogWarning($"Cannot get current value for '{binding.propertyName}': Component is null in binding.", this);
                return 0f;
            }
            if (binding.getter == null)
            {
                // This implies GetOrCreateBinding failed to create a getter, or it's an object ref property without a numeric getter.
                // For tweening, numeric properties must have a getter.
                Debug.LogWarning($"Cannot get current value for '{binding.propertyName}' on component '{binding.component.GetType().Name}': Getter is null. Property may not be numeric/tweenable or binding setup failed.", this);
                return 0f; 
            }

            try
            {
                return binding.getter(binding.component);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing compiled getter for {binding.propertyName} on {binding.component.GetType().Name}: {ex.Message}", this);
                return 0f;
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