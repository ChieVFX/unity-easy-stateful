using UnityEngine;
using System;
using System.Reflection;

namespace EasyStateful.Runtime
{
    [Serializable]
    public class PropertyBinding
    {
        public Component component;
        public GameObject targetGameObject;
        public string propertyName; // Full property name, e.g., "m_Color.r" or "m_IsEnabled"

        // For numeric properties (float, int, bool interpreted as 0/1)
        public Action<Component, float> setter;
        public Func<Component, float> getter;

        // For object reference properties
        public Action<Component, UnityEngine.Object> setterObj;

        // Cached reflection info - used by PropertyBindingManager to compile expressions
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
} 