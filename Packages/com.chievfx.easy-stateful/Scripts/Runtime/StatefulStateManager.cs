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
        public bool useCustomTiming;
        public float customTimingStart;
        public float customTimingEnd;
        
        public float GetTotalTimingMultiplier()
        {
            if (!useCustomTiming || instantEnableDelayedDisable)
                return 1f;
            
            return customTimingStart + 1f + customTimingEnd;
        }
        
        public float GetEffectiveDuration()
        {
            return duration * GetTotalTimingMultiplier();
        }
        
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
                    bool useCustomTiming = false;
                    float customTimingStart = 0f;
                    float customTimingEnd = 0f;

                    var rule = settingsResolver.GetPropertyOverrideRule(prop.propertyName, prop.componentType, prop.path);
                    if (rule != null)
                    {
                        if (rule.overrideEase) ease = rule.ease;
                        instantEnableDelayedDisable = rule.instantEnableDelayedDisable;
                        useCustomTiming = rule.useCustomTiming;
                        customTimingStart = rule.customTimingStart;
                        customTimingEnd = rule.customTimingEnd;
                    }

                    _propertyTransitionCache[prop] = new PropertyTransitionInfo
                    {
                        duration = duration,
                        ease = ease,
                        instantEnableDelayedDisable = instantEnableDelayedDisable,
                        useCustomTiming = useCustomTiming,
                        customTimingStart = customTimingStart,
                        customTimingEnd = customTimingEnd
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