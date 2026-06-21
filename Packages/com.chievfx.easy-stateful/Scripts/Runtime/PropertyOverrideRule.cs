using UnityEngine;

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

        public bool useCustomTiming = false;
        [Tooltip("Time units to pause at start (sampling curve at 0). Animation time is divided by (start + 1 + end).")]
        public float customTimingStart = 0f;
        [Tooltip("Time units to pause at end (sampling curve at 1). Animation time is divided by (start + 1 + end).")]
        public float customTimingEnd = 0f;

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
            useCustomTiming = false;
            customTimingStart = 0f;
            customTimingEnd = 0f;
        }

        /// <summary>
        /// Get the total timing multiplier (start + 1 + end)
        /// </summary>
        public float GetTotalTimingMultiplier()
        {
            if (!useCustomTiming || instantEnableDelayedDisable)
                return 1f;
            
            return customTimingStart + 1f + customTimingEnd;
        }

        /// <summary>
        /// Get the normalized time for the actual animation curve sampling
        /// </summary>
        public float GetNormalizedCurveTime(float totalNormalizedTime)
        {
            if (!useCustomTiming || instantEnableDelayedDisable)
                return totalNormalizedTime;

            float totalMultiplier = GetTotalTimingMultiplier();
            float startPhase = customTimingStart / totalMultiplier;
            float animPhase = 1f / totalMultiplier;
            
            if (totalNormalizedTime <= startPhase)
            {
                // In start pause phase
                return 0f;
            }
            else if (totalNormalizedTime >= startPhase + animPhase)
            {
                // In end pause phase
                return 1f;
            }
            else
            {
                // In animation phase
                float animProgress = (totalNormalizedTime - startPhase) / animPhase;
                return Mathf.Clamp01(animProgress);
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
}