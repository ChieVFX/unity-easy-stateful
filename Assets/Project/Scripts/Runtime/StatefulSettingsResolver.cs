using UnityEngine;
using System.Collections.Generic;

namespace EasyStateful.Runtime
{
    /// <summary>
    /// Handles settings resolution hierarchy for StatefulRoot
    /// </summary>
    public class StatefulSettingsResolver
    {
        private StatefulRoot root;
        private Dictionary<(string propertyName, string componentType), PropertyOverrideRule> _propertyOverrideCache;
        private Dictionary<(string propertyName, string componentType, string path), PropertyOverrideRule> _pathBasedOverrideCache;

        public StatefulSettingsResolver(StatefulRoot root)
        {
            this.root = root;
            BuildPropertyOverrideCache();
        }

        public float GetEffectiveTransitionTime()
        {
            // Tier 3: StatefulRoot instance override
            if (root.overrideDefaultTransitionTime)
            {
                return root.customDefaultTransitionTime;
            }
            // Tier 4: GroupSettings override
            if (root.groupSettings != null && root.groupSettings.overrideGlobalDefaultTransitionTime)
            {
                return root.groupSettings.customDefaultTransitionTime;
            }
            // Tier 5: Global Settings
            return StatefulGlobalSettings.DefaultTime;
        }

        public Ease GetEffectiveEase()
        {
            // Tier 3: StatefulRoot instance override
            if (root.overrideDefaultEase)
            {
                return root.customDefaultEase;
            }
            // Tier 4: GroupSettings override
            if (root.groupSettings != null && root.groupSettings.overrideGlobalDefaultEase)
            {
                return root.groupSettings.customDefaultEase;
            }
            // Tier 5: Global Settings
            return StatefulGlobalSettings.DefaultEase;
        }

        public PropertyOverrideRule GetPropertyOverrideRule(string propertyName, string componentTypeFullName, string path = "")
        {
            // First check path-based cache if path is provided
            if (!string.IsNullOrEmpty(path))
            {
                var pathKey = (propertyName, componentTypeFullName, path);
                if (_pathBasedOverrideCache != null && _pathBasedOverrideCache.TryGetValue(pathKey, out var cachedRule))
                    return cachedRule;

                // Find matching rule and cache it
                var matchingRule = FindMatchingRule(propertyName, componentTypeFullName, path);
                if (_pathBasedOverrideCache == null)
                    _pathBasedOverrideCache = new Dictionary<(string, string, string), PropertyOverrideRule>();
                
                _pathBasedOverrideCache[pathKey] = matchingRule;
                return matchingRule;
            }

            // Fallback to original property-based cache
            if (_propertyOverrideCache != null)
            {
                if (_propertyOverrideCache.TryGetValue((propertyName, componentTypeFullName), out var rule))
                    return rule;
                if (_propertyOverrideCache.TryGetValue((propertyName, ""), out rule))
                    return rule;
            }

            // Check global settings
            return StatefulGlobalSettings.GetGlobalPropertyOverrideRule(propertyName, componentTypeFullName, path);
        }

        private PropertyOverrideRule FindMatchingRule(string propertyName, string componentTypeFullName, string path)
        {
            // Check group settings first
            if (root.groupSettings != null && root.groupSettings.propertyOverrides != null)
            {
                foreach (var rule in root.groupSettings.propertyOverrides)
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
            if (_propertyOverrideCache == null)
                _propertyOverrideCache = new Dictionary<(string, string), PropertyOverrideRule>();
            else
                _propertyOverrideCache.Clear();

            // Clear path-based cache when rebuilding
            _pathBasedOverrideCache?.Clear();
                
            if (root.groupSettings != null && root.groupSettings.propertyOverrides != null)
            {
                foreach (var r in root.groupSettings.propertyOverrides)
                {
                    // Only cache non-wildcard rules in the simple cache
                    if (string.IsNullOrEmpty(r.pathWildcard))
                    {
                        var key = (r.propertyName, r.componentType ?? "");
                        _propertyOverrideCache[key] = r;
                    }
                }
            }
        }
    }
} 