using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

namespace EasyStateful.Runtime {
    [System.Serializable]
    public class PropertyOverrideRule
    {
        public string propertyName = "m_Color"; // Example
        public string componentType = ""; // Optional: Full name, e.g., UnityEngine.UI.Image
        public bool overrideEase = false;
        public Ease ease = Ease.Linear;

        // This is the primary toggle for "Instant Change" behavior in UI and logic
        public bool instantEnableDelayedDisable = false;

        public PropertyOverrideRule() {}

        public PropertyOverrideRule(string propName, string compType = "", bool useInstantChange = false, bool useLinearEase = false)
        {
            propertyName = propName;
            componentType = compType;
            if (useInstantChange)
            {
                instantEnableDelayedDisable = true;
            } else if (useLinearEase) {
                overrideEase = true;
                ease = Ease.Linear;
            }
        }
    }

    [CreateAssetMenu(fileName = "StatefulGlobalSettings", menuName = "Stateful UI/Global Settings", order = 0)]
    public class StatefulGlobalSettingsData : ScriptableObject
    {
        [Header("Global Default Transition Values")]
        public float defaultTransitionTime = 0.5f;
        public Ease defaultEase = Ease.Linear;

        [Header("Global Property Overrides")]
        public List<PropertyOverrideRule> propertyOverrides = new List<PropertyOverrideRule>()
        {
            new PropertyOverrideRule("m_IsActive", "", true, false),
            new PropertyOverrideRule("m_Color.r", "", false, true),
            new PropertyOverrideRule("m_Color.g", "", false, true),
            new PropertyOverrideRule("m_Color.b", "", false, true),
            new PropertyOverrideRule("m_Color.a", "", false, true),
        };

        [Header("Default Save Paths")]
        [Tooltip("Default directory within Assets/ for saving new StatefulDataAssets (e.g., 'MyStatefulData'). Leave empty for project root.")]
        public string defaultBinarySavePath = "StatefulData";
        [Tooltip("Default directory within Assets/ for saving new Animation Clips (e.g., 'Animations/Stateful'). Leave empty for project root.")]
        public string defaultAnimationSavePath = "Animations";

        public StatefulEasingsData easingsData;
    }
}