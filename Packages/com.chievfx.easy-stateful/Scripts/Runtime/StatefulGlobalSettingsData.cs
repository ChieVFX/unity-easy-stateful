using UnityEngine;
using System.Collections.Generic;

namespace EasyStateful.Runtime {

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

#if UNITY_EDITOR
        private void OnValidate()
        {
            StatefulGlobalSettings.NotifySettingsChanged();
        }
#endif
    }
}