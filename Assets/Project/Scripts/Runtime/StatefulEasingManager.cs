using UnityEngine;
using System.Collections.Generic;

namespace EasyStateful.Runtime
{
    /// <summary>
    /// Manages easing calculations and curve caching
    /// </summary>
    public class StatefulEasingManager
    {
        private StatefulSettingsResolver settingsResolver;
        
        // Cache for default curves to avoid repeated creation
        private static readonly Dictionary<Ease, AnimationCurve> _defaultCurveCache = new Dictionary<Ease, AnimationCurve>();

        public StatefulEasingManager(StatefulSettingsResolver settingsResolver)
        {
            this.settingsResolver = settingsResolver;
        }

        /// <summary>
        /// Sample an easing curve at the given time (0-1)
        /// </summary>
        public float SampleEasing(Ease ease, float time)
        {
            return SampleEasing((int)ease, time);
        }

        /// <summary>
        /// Sample an easing curve at the given time (0-1) using curve index
        /// </summary>
        public float SampleEasing(int easeIndex, float time)
        {
            var easingsData = settingsResolver.GetEffectiveEasingsData();
            
            if (easingsData != null && easingsData.curves != null && 
                easeIndex >= 0 && easeIndex < easingsData.curves.Length && 
                easingsData.curves[easeIndex] != null)
            {
                return easingsData.curves[easeIndex].Evaluate(time);
            }
            
            // Fallback to default curve generation
            var defaultCurve = StatefulEasingsData.GetDefaultCurve((Ease)easeIndex);
            return defaultCurve.Evaluate(time);
        }

        /// <summary>
        /// Inline easing calculation to avoid method call overhead and potential allocations
        /// </summary>
        public float SampleEasingInline(Ease ease, float time)
        {
            var easingsData = settingsResolver.GetEffectiveEasingsData();
            
            int easeIndex = (int)ease;
            if (easingsData != null && easingsData.curves != null && 
                easeIndex >= 0 && easeIndex < easingsData.curves.Length && 
                easingsData.curves[easeIndex] != null)
            {
                return easingsData.curves[easeIndex].Evaluate(time);
            }
            
            // Fallback to default curve generation - cache this to avoid repeated creation
            return GetOrCreateDefaultCurve(ease).Evaluate(time);
        }

        /// <summary>
        /// Get or create a default curve for the given ease, with caching to avoid allocations
        /// </summary>
        private AnimationCurve GetOrCreateDefaultCurve(Ease ease)
        {
            if (!_defaultCurveCache.TryGetValue(ease, out var curve))
            {
                curve = StatefulEasingsData.GetDefaultCurve(ease);
                _defaultCurveCache[ease] = curve;
            }
            return curve;
        }
    }
} 