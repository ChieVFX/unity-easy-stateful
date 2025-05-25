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

        public PropertyOverrideRule GetPropertyOverrideRule(string propertyName, string componentTypeFullName)
        {
            // Tier 1: Group Settings Property Override (cached)
            if (_propertyOverrideCache != null)
            {
                if (_propertyOverrideCache.TryGetValue((propertyName, componentTypeFullName), out var rule))
                    return rule;
                if (_propertyOverrideCache.TryGetValue((propertyName, ""), out rule))
                    return rule;
            }
            // Tier 2: Global Settings Property Override
            return StatefulGlobalSettings.GetGlobalPropertyOverrideRule(propertyName, componentTypeFullName);
        }

        public StatefulEasingsData GetEffectiveEasingsData()
        {
            // Tier 1: Group Settings
            if (root.groupSettings != null && root.groupSettings.easingsData != null)
                return root.groupSettings.easingsData;
            
            // Tier 2: Global Settings
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
                
            if (root.groupSettings != null && root.groupSettings.propertyOverrides != null)
            {
                foreach (var r in root.groupSettings.propertyOverrides)
                {
                    var key = (r.propertyName, r.componentType ?? "");
                    _propertyOverrideCache[key] = r;
                }
            }
        }
    }
} 