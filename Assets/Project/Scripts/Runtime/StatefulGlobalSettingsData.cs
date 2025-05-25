using UnityEngine;
using System.Collections.Generic;

namespace EasyStateful.Runtime {
    [System.Serializable]
    public class PropertyOverrideRule
    {
        public string propertyName = "m_Color"; // Example
        public string componentType = ""; // Optional: Full name, e.g., UnityEngine.UI.Image
        public string pathWildcard = ""; // Optional: Path wildcard, e.g., "*_first" or "*button*"
        public bool overrideEase = false;
        public Ease ease = Ease.Linear;

        // This is the primary toggle for "Instant Change" behavior in UI and logic
        public bool instantEnableDelayedDisable = false;

        public PropertyOverrideRule() {}

        public PropertyOverrideRule(string propName, string compType = "", bool useInstantChange = false, bool useLinearEase = false, string pathPattern = "")
        {
            propertyName = propName;
            componentType = compType;
            pathWildcard = pathPattern;
            instantEnableDelayedDisable = useInstantChange;
            if (useLinearEase)
            {
                overrideEase = true;
                ease = Ease.Linear;
            }
        }

        /// <summary>
        /// Check if this rule matches the given property and path
        /// </summary>
        public bool Matches(string propName, string compType, string path)
        {
            // Check property name match (if specified)
            if (!string.IsNullOrEmpty(propertyName) && propertyName != propName)
                return false;

            // Check component type match (if specified)
            if (!string.IsNullOrEmpty(componentType) && componentType != compType)
                return false;

            // Check path wildcard match (if specified)
            if (!string.IsNullOrEmpty(pathWildcard))
            {
                return MatchesWildcard(path, pathWildcard);
            }

            return true;
        }

        private static bool MatchesWildcard(string path, string pattern)
        {
            if (string.IsNullOrEmpty(pattern)) return true;
            if (string.IsNullOrEmpty(path)) return false;

            // Simple wildcard matching with * support
            return IsWildcardMatch(path, pattern);
        }

        private static bool IsWildcardMatch(string text, string pattern)
        {
            int textIndex = 0;
            int patternIndex = 0;
            int starIndex = -1;
            int match = 0;

            while (textIndex < text.Length)
            {
                if (patternIndex < pattern.Length && (pattern[patternIndex] == '?' || pattern[patternIndex] == text[textIndex]))
                {
                    textIndex++;
                    patternIndex++;
                }
                else if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
                {
                    starIndex = patternIndex;
                    match = textIndex;
                    patternIndex++;
                }
                else if (starIndex != -1)
                {
                    patternIndex = starIndex + 1;
                    match++;
                    textIndex = match;
                }
                else
                {
                    return false;
                }
            }

            while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                patternIndex++;
            }

            return patternIndex == pattern.Length;
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