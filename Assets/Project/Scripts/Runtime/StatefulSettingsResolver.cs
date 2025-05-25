using UnityEngine;
using System.Collections.Generic;

namespace EasyStateful.Runtime
{
    /// <summary>
    /// Handles settings resolution hierarchy for StatefulRoot
    /// </summary>
    public class StatefulSettingsResolver
    {
        private StatefulRoot statefulRoot;
        private Dictionary<string, PropertyOverrideRule> propertyOverrideCache;
        
#if UNITY_EDITOR
        private static event System.Action OnSettingsChanged;
        
        static StatefulSettingsResolver()
        {
            // Subscribe to settings changes in editor
            StatefulGlobalSettings.OnSettingsChanged += InvalidateAllCaches;
        }
        
        public static void InvalidateAllCaches()
        {
            OnSettingsChanged?.Invoke();
        }
#endif

        public StatefulSettingsResolver(StatefulRoot root)
        {
            statefulRoot = root;
            
#if UNITY_EDITOR
            OnSettingsChanged += InvalidateCache;
#endif
        }

#if UNITY_EDITOR
        ~StatefulSettingsResolver()
        {
            OnSettingsChanged -= InvalidateCache;
        }
        
        private void InvalidateCache()
        {
            propertyOverrideCache?.Clear();
            BuildPropertyOverrideCache();
            
            // Also invalidate state manager cache
            statefulRoot?.InvalidatePropertyTransitionCache();
        }
#endif

        public float GetEffectiveTransitionTime()
        {
            // Tier 3: StatefulRoot instance override
            if (statefulRoot.overrideDefaultTransitionTime)
            {
                return statefulRoot.customDefaultTransitionTime;
            }
            // Tier 4: GroupSettings override
            if (statefulRoot.groupSettings != null && statefulRoot.groupSettings.overrideGlobalDefaultTransitionTime)
            {
                return statefulRoot.groupSettings.customDefaultTransitionTime;
            }
            // Tier 5: Global Settings
            return StatefulGlobalSettings.DefaultTime;
        }

        public Ease GetEffectiveEase()
        {
            // Tier 3: StatefulRoot instance override
            if (statefulRoot.overrideDefaultEase)
            {
                return statefulRoot.customDefaultEase;
            }
            // Tier 4: GroupSettings override
            if (statefulRoot.groupSettings != null && statefulRoot.groupSettings.overrideGlobalDefaultEase)
            {
                return statefulRoot.groupSettings.customDefaultEase;
            }
            // Tier 5: Global Settings
            return StatefulGlobalSettings.DefaultEase;
        }

        public PropertyOverrideRule GetPropertyOverrideRule(string propertyName, string componentTypeFullName, string path = "")
        {
            // Create cache key
            string cacheKey;
            if (!string.IsNullOrEmpty(path))
            {
                cacheKey = $"{propertyName}|{componentTypeFullName}|{path}";
            }
            else
            {
                cacheKey = $"{propertyName}|{componentTypeFullName}";
            }

            // Check cache first
            if (propertyOverrideCache != null && propertyOverrideCache.TryGetValue(cacheKey, out var cachedRule))
                return cachedRule;

            // Find matching rule
            var matchingRule = FindMatchingRule(propertyName, componentTypeFullName, path);
            
            // Cache the result
            if (propertyOverrideCache == null)
                propertyOverrideCache = new Dictionary<string, PropertyOverrideRule>();
            
            propertyOverrideCache[cacheKey] = matchingRule;
            return matchingRule;
        }

        private PropertyOverrideRule FindMatchingRule(string propertyName, string componentTypeFullName, string path)
        {
            // Check group settings first
            if (statefulRoot.groupSettings != null && statefulRoot.groupSettings.propertyOverrides != null)
            {
                foreach (var rule in statefulRoot.groupSettings.propertyOverrides)
                {
                    if (rule.Matches(propertyName, componentTypeFullName, path))
                        return rule;
                }
            }

            // Check global settings
            return StatefulGlobalSettings.GetGlobalPropertyOverrideRule(propertyName, componentTypeFullName, path);
        }

        public StatefulEasingsData GetEffectiveEasingsData()
        {
            // Only use Global Settings
            if (StatefulGlobalSettings.EasingsData != null)
                return StatefulGlobalSettings.EasingsData;
            
            // Fallback: Create default curves on demand
            return null;
        }

        public void BuildPropertyOverrideCache()
        {
            if (propertyOverrideCache == null)
                propertyOverrideCache = new Dictionary<string, PropertyOverrideRule>();
            else
                propertyOverrideCache.Clear();

            // Pre-cache group settings rules (non-wildcard only for simple lookup)
            if (statefulRoot.groupSettings != null && statefulRoot.groupSettings.propertyOverrides != null)
            {
                foreach (var r in statefulRoot.groupSettings.propertyOverrides)
                {
                    // Only cache non-wildcard rules for simple property+component lookups
                    if (string.IsNullOrEmpty(r.pathWildcard))
                    {
                        string key = $"{r.propertyName}|{r.componentType ?? ""}";
                        propertyOverrideCache[key] = r;
                    }
                }
            }

            // Pre-cache global settings rules (non-wildcard only)
            var globalRules = StatefulGlobalSettings.PropertyOverrides;
            if (globalRules != null)
            {
                foreach (var r in globalRules)
                {
                    if (string.IsNullOrEmpty(r.pathWildcard))
                    {
                        string key = $"{r.propertyName}|{r.componentType ?? ""}";
                        // Only add if not already cached from group settings (group takes priority)
                        if (!propertyOverrideCache.ContainsKey(key))
                        {
                            propertyOverrideCache[key] = r;
                        }
                    }
                }
            }
        }
    }
} 