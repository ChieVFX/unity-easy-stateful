using UnityEngine;
using System.Collections.Generic;

namespace EasyStateful.Runtime {
    [CreateAssetMenu(fileName = "NewStatefulGroupSettings", menuName = "Stateful UI/Group Settings", order = 1)]
    public class StatefulGroupSettingsData : ScriptableObject
    {
        public bool overrideGlobalDefaultTransitionTime = false;
        [Tooltip("Custom default transition time for this group if override is enabled.")]
        public float customDefaultTransitionTime = 0.3f;

        public bool overrideGlobalDefaultEase = false;
        [Tooltip("Custom default ease for this group if override is enabled.")]
        public Ease customDefaultEase = Ease.InOutQuad;

        [Tooltip("These rules override global property rules and apply before them.")]
        public List<PropertyOverrideRule> propertyOverrides = new List<PropertyOverrideRule>()
        {
            new PropertyOverrideRule("m_IsActive", "", true, false),
            new PropertyOverrideRule("m_Color.r", "", false, true),
            new PropertyOverrideRule("m_Color.g", "", false, true),
            new PropertyOverrideRule("m_Color.b", "", false, true),
            new PropertyOverrideRule("m_Color.a", "", false, true),
        };

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Find all StatefulRoots that use this group settings and invalidate their caches
            var allRoots = FindObjectsOfType<StatefulRoot>();
            foreach (var root in allRoots)
            {
                if (root.groupSettings == this)
                {
                    root.InvalidatePropertyTransitionCache();
                }
            }
        }
#endif
    }
}
