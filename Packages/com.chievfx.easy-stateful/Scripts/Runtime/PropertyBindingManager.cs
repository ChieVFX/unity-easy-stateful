using UnityEngine;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq.Expressions;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace EasyStateful.Runtime
{
    /// <summary>
    /// Manages property bindings for StatefulRoot, handling creation, caching, and execution of compiled expressions.
    /// </summary>
    public class PropertyBindingManager
    {
        // Cache per-path and per-property binding info for fast reflection
        private Dictionary<string, Dictionary<(string componentType, string propertyName), PropertyBinding>> bindingCache = new();
        
        // Global caches to reduce per-instance allocations
        private static readonly Dictionary<string, Type> _typeCache = new Dictionary<string, Type>();
        private static readonly Dictionary<(Type, string), MemberInfo> _memberInfoCache = new Dictionary<(Type, string), MemberInfo>();
        
        // Pre-compiled expression cache - this is the big one for GC reduction
        private static readonly Dictionary<(Type componentType, string propertyName, bool isGetter), Delegate> _compiledExpressionCache = 
            new Dictionary<(Type, string, bool), Delegate>();

        // String pooling to reduce allocations
        private static readonly Dictionary<string, string> _stringPool = new Dictionary<string, string>();

        private Transform rootTransform;

        public PropertyBindingManager(Transform rootTransform)
        {
            this.rootTransform = rootTransform;
        }

        /// <summary>
        /// String pooling to reduce allocations from repeated string operations
        /// </summary>
        private static string GetPooledString(string str)
        {
            if (string.IsNullOrEmpty(str)) return str;
            
            if (!_stringPool.TryGetValue(str, out var pooled))
            {
                pooled = str;
                _stringPool[str] = pooled;
            }
            return pooled;
        }

        /// <summary>
        /// Retrieves or creates a binding for this property.
        /// </summary>
        public PropertyBinding GetOrCreateBinding(Property prop, out Transform target)
        {
            // Use pooled strings to reduce allocations
            string pooledPath = GetPooledString(prop.path);
            string pooledComponentType = GetPooledString(prop.componentType);
            string pooledPropertyName = GetPooledString(prop.propertyName);
            
            target = rootTransform.Find(pooledPath);
            if (target == null)
            {
                Debug.LogWarning($"Path not found: {pooledPath} for property {pooledPropertyName} on component type {pooledComponentType}");
                return null;
            }

            if (!bindingCache.TryGetValue(pooledPath, out var compMap))
            {
                compMap = new Dictionary<(string, string), PropertyBinding>();
                bindingCache[pooledPath] = compMap;
            }

            var cacheKey = (pooledComponentType, pooledPropertyName);
            if (compMap.TryGetValue(cacheKey, out var binding))
            {
                return binding;
            }

            // Special handling for GameObject.m_IsActive - avoid Type.GetType calls
            if (pooledPropertyName == "m_IsActive" && 
                (string.IsNullOrEmpty(pooledComponentType) || 
                 pooledComponentType == "UnityEngine.GameObject" || 
                 pooledComponentType == typeof(GameObject).FullName || 
                 pooledComponentType == typeof(GameObject).AssemblyQualifiedName))
            {
                binding = new PropertyBinding { 
                    targetGameObject = target.gameObject, 
                    propertyName = pooledPropertyName, 
                    isGameObjectActiveProperty = true 
                };
                binding.getter = (_) => binding.targetGameObject.activeSelf ? 1f : 0f;
                compMap[cacheKey] = binding;
                return binding;
            }

            // Type caching with better string handling
            if (!_typeCache.TryGetValue(pooledComponentType, out var compType))
            {
                compType = Type.GetType(pooledComponentType);
                if (compType != null)
                    _typeCache[pooledComponentType] = compType;
            }
            if (compType == null)
            {
                Debug.LogWarning($"Component type not found: {pooledComponentType} for path {pooledPath}, property {pooledPropertyName}");
                return null;
            }

            var comp = target.GetComponent(compType);
            if (comp == null)
            {
                Debug.LogWarning($"Component {compType.Name} not found on {pooledPath} for property {pooledPropertyName}");
                return null;
            }

            binding = new PropertyBinding { 
                component = comp, 
                targetGameObject = target.gameObject, 
                propertyName = pooledPropertyName 
            };

            bool isObjectRef = !string.IsNullOrEmpty(prop.objectReference);

            if (isObjectRef)
            {
                SetupObjectReferenceBinding(binding, prop, compType);
            }
            else
            {
                SetupNumericBinding(binding, prop, compType);
            }

            compMap[cacheKey] = binding; // Cache the binding, complete or not
            return binding;
        }

        /// <summary>
        /// Get the current numeric value for the binding.
        /// </summary>
        public float GetCurrentValue(PropertyBinding binding)
        {
            if (binding.isGameObjectActiveProperty) // Uses its own specialized getter
            {
                 if (binding.targetGameObject == null) {
                    Debug.LogWarning($"Cannot get current value for '{binding.propertyName}': targetGameObject is null in binding for an m_IsActive property.");
                    return 0f;
                 }
                return binding.getter(null); // Component parameter is not used by this getter
            }

            if (binding.component == null)
            {
                Debug.LogWarning($"Cannot get current value for '{binding.propertyName}': Component is null in binding.");
                return 0f;
            }
            if (binding.getter == null)
            {
                Debug.LogWarning($"Cannot get current value for '{binding.propertyName}' on component '{binding.component.GetType().Name}': Getter is null. Property may not be numeric/tweenable or binding setup failed.");
                return 0f; 
            }

            try
            {
                return binding.getter(binding.component);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing compiled getter for {binding.propertyName} on {binding.component.GetType().Name}: {ex.Message}");
                return 0f;
            }
        }

        /// <summary>
        /// Setup object reference binding with reduced allocations
        /// </summary>
        private void SetupObjectReferenceBinding(PropertyBinding binding, Property prop, Type compType)
        {
            string pooledPropertyName = GetPooledString(prop.propertyName);
            
            var memberKey = (compType, pooledPropertyName);
            if (!_memberInfoCache.TryGetValue(memberKey, out var memberInfo))
            {
                memberInfo = compType.GetProperty(pooledPropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) as MemberInfo
                          ?? compType.GetField(pooledPropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) as MemberInfo;
                _memberInfoCache[memberKey] = memberInfo;
            }

            var pi = memberInfo as PropertyInfo;
            var fi = memberInfo as FieldInfo;
            
            if (pi != null) binding.propInfo = pi;
            else if (fi != null) binding.fieldInfo = fi;
            else {
                Debug.LogWarning($"Object reference Property/Field '{pooledPropertyName}' not found on type '{compType.Name}' for path {prop.path}.");
                return; 
            }

            Type memberType = pi?.PropertyType ?? fi?.FieldType;

            // Use cached compiled expressions
            var expressionKey = (compType, pooledPropertyName, false); // false = setter
            if (_compiledExpressionCache.TryGetValue(expressionKey, out var cachedSetter))
            {
                binding.setterObj = (Action<Component, UnityEngine.Object>)cachedSetter;
            }
            else
            {
                // Compile and cache the expression
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
                            var castObjExpr = Expression.Convert(objectParamExpr, memberType);
                            var callSetterExpr = Expression.Call(castCompExpr, setterMethod, castObjExpr);
                            var compiledSetter = Expression.Lambda<Action<Component, UnityEngine.Object>>(callSetterExpr, componentParamExpr, objectParamExpr).Compile();
                            
                            _compiledExpressionCache[expressionKey] = compiledSetter;
                            binding.setterObj = compiledSetter;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"Failed to compile object setter for Property '{pooledPropertyName}' on type '{compType.Name}' (path {prop.path}): {ex.Message}. Will fallback to reflection invoke.");
                            binding.setterObj = (c, o) => setterMethod.Invoke(c, new object[] { o }); // Fallback
                        }
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
                        var compiledSetter = Expression.Lambda<Action<Component, UnityEngine.Object>>(assignExpr, componentParamExpr, objectParamExpr).Compile();
                        
                        _compiledExpressionCache[expressionKey] = compiledSetter;
                        binding.setterObj = compiledSetter;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Failed to compile object setter for Field '{pooledPropertyName}' on type '{compType.Name}' (path {prop.path}): {ex.Message}. Will fallback to reflection invoke.");
                        binding.setterObj = (c, o) => fi.SetValue(c, o); // Fallback
                    }
                }
            }
        }

        /// <summary>
        /// Setup numeric binding with reduced allocations
        /// </summary>
        private void SetupNumericBinding(PropertyBinding binding, Property prop, Type compType)
        {
            string pooledPropertyName = GetPooledString(prop.propertyName);
            
            // Check for cached compiled expressions first
            var getterKey = (compType, pooledPropertyName, true); // true = getter
            var setterKey = (compType, pooledPropertyName, false); // false = setter

            if (_compiledExpressionCache.TryGetValue(getterKey, out var cachedGetter))
            {
                binding.getter = (Func<Component, float>)cachedGetter;
            }

            if (_compiledExpressionCache.TryGetValue(setterKey, out var cachedSetter))
            {
                binding.setter = (Action<Component, float>)cachedSetter;
            }

            // If both are cached, we're done
            if (binding.getter != null && binding.setter != null)
                return;

            // Need to compile expressions - do reflection work
            var memberKey = (compType, pooledPropertyName);
            if (!_memberInfoCache.TryGetValue(memberKey, out var memberInfo))
            {
                memberInfo = compType.GetProperty(pooledPropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) as MemberInfo
                          ?? compType.GetField(pooledPropertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) as MemberInfo;
                _memberInfoCache[memberKey] = memberInfo;
            }

            // Parameter expressions for the delegates
            var componentParam = Expression.Parameter(typeof(Component), "c");
            var valueParam = Expression.Parameter(typeof(float), "val"); // For setter

            Expression castComponent = Expression.Convert(componentParam, compType);
            Expression finalPropertyAccess = null; 
            Type targetPropertyType = null; 

            if (pooledPropertyName.Contains("."))
            {
                binding.isSubProperty = true;
                int dotIdx = pooledPropertyName.IndexOf('.');
                string mainName = pooledPropertyName.Substring(0, dotIdx);
                string subName = pooledPropertyName.Substring(dotIdx + 1);

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
                    Debug.LogWarning($"Main member '{mainName}' not found for sub-property '{pooledPropertyName}' on type '{compType.Name}' for path {prop.path}. Cannot create binding.");
                    return;
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
                    Debug.LogWarning($"Sub member '{subName}' not found on main member '{mainName}' (type: {mainMemberType.Name}) for property '{pooledPropertyName}' on path {prop.path}. Cannot create binding.");
                    return;
                }
                binding.subValueType = targetPropertyType;
            }
            else 
            {
                binding.propInfo = memberInfo as PropertyInfo;
                binding.fieldInfo = memberInfo as FieldInfo;

                if (binding.propInfo != null) {
                    finalPropertyAccess = Expression.Property(castComponent, binding.propInfo);
                    targetPropertyType = binding.propInfo.PropertyType;
                } else if (binding.fieldInfo != null) {
                    finalPropertyAccess = Expression.Field(castComponent, binding.fieldInfo);
                    targetPropertyType = binding.fieldInfo.FieldType;
                } else {
                    Debug.LogWarning($"Property/Field '{pooledPropertyName}' not found on type '{compType.Name}' for path {prop.path}. Cannot create binding.");
                    return;
                }
                binding.valueType = targetPropertyType;
            }

            if (finalPropertyAccess == null || targetPropertyType == null)
            {
                Debug.LogWarning($"Failed to establish property access for '{pooledPropertyName}' on '{compType.Name}' (path {prop.path}). Binding will be incomplete.");
                return;
            }

            // Create and cache getter if not already cached
            if (binding.getter == null)
            {
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
                    var compiledGetter = getterLambda.Compile();
                    
                    _compiledExpressionCache[getterKey] = compiledGetter;
                    binding.getter = compiledGetter;
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to compile getter for {pooledPropertyName} on {compType.Name} (path {prop.path}): {ex.Message}. This property may not be readable for tweening.");
                    binding.getter = null; 
                }
            }
            
            // Create and cache setter if not already cached
            if (binding.setter == null)
            {
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

                if (canAttemptSetterCompilation)
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
                        var compiledSetter = setterLambda.Compile();
                        
                        _compiledExpressionCache[setterKey] = compiledSetter;
                        binding.setter = compiledSetter;
                    }
                    catch (Exception ex)
                    {
                         Debug.LogError($"Failed to compile setter for {pooledPropertyName} on {compType.Name} (path {prop.path}): {ex.Message}. This property will not be numerically tweenable.");
                         binding.setter = null; 
                    }
                }
            }
        }
    }
} 