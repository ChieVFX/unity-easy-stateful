using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace EasyStateful.Runtime
{
    // Add this struct
    public struct PropertyTransitionInfo
    {
        public float duration;
        public Ease ease;
        public bool instantEnableDelayedDisable;
    }

    /// <summary>
    /// Manages state operations and caching for StatefulRoot
    /// </summary>
    public class StatefulStateManager
    {
        private UIStateMachine stateMachine;
        private Dictionary<Property, PropertyTransitionInfo> _propertyTransitionCache = new();

        public string[] StateNames { get; private set; } = new string[0];

        public void LoadStateMachine(UIStateMachine stateMachine)
        {
            this.stateMachine = stateMachine;
            UpdateStateNamesArray();
        }

        public State FindState(string stateName)
        {
            if (stateMachine?.states == null) return null;
            
            for (int i = 0; i < stateMachine.states.Count; i++)
            {
                if (stateMachine.states[i].name == stateName)
                {
                    return stateMachine.states[i];
                }
            }
            return null;
        }

        public void UpdateStateNamesArray()
        {
            if (stateMachine?.states != null)
                StateNames = stateMachine.states.Select(s => s.name).ToArray();
            else
                StateNames = new string[0];
        }

        public void BuildPropertyTransitionCache(StatefulSettingsResolver settingsResolver)
        {
            _propertyTransitionCache.Clear();
            if (stateMachine?.states == null) return;

            foreach (var state in stateMachine.states)
            {
                foreach (var prop in state.properties)
                {
                    float duration = settingsResolver.GetEffectiveTransitionTime();
                    Ease ease = settingsResolver.GetEffectiveEase();
                    bool instantEnableDelayedDisable = false;

                    var rule = settingsResolver.GetPropertyOverrideRule(prop.propertyName, prop.componentType, prop.path);
                    if (rule != null)
                    {
                        if (rule.overrideEase) ease = rule.ease;
                        instantEnableDelayedDisable = rule.instantEnableDelayedDisable;
                    }

                    _propertyTransitionCache[prop] = new PropertyTransitionInfo
                    {
                        duration = duration,
                        ease = ease,
                        instantEnableDelayedDisable = instantEnableDelayedDisable
                    };
                }
            }
        }

        public Dictionary<Property, PropertyTransitionInfo> GetPropertyTransitionCache() => _propertyTransitionCache;

#if UNITY_EDITOR
        public void InvalidatePropertyTransitionCache()
        {
            _propertyTransitionCache?.Clear();
        }
#endif
    }
} 