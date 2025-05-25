using UnityEngine;
using UnityEditor;
using System.Linq;
using System.Collections.Generic;
using EasyStateful.Runtime;

namespace EasyStateful.Editor
{
    /// <summary>
    /// Helper class for editor-specific StatefulRoot operations
    /// </summary>
    public static class StatefulEditorHelper
    {
        /// <summary>
        /// Updates state names from animation clip events
        /// </summary>
        public static void UpdateStateNamesFromClip(StatefulRoot root, AnimationClip clip)
        {
            if (clip != null)
            {
                var events = AnimationUtility.GetAnimationEvents(clip);
                root.stateNames = events
                    .Select(ev => !string.IsNullOrEmpty(ev.functionName) ? ev.functionName : ev.stringParameter)
                    .Where(name => !string.IsNullOrEmpty(name))
                    .Distinct()
                    .ToArray();
            }
            else
            {
                root.stateNames = new string[0];
            }
            EditorUtility.SetDirty(root);
        }

        /// <summary>
        /// Builds a state machine from an animation clip
        /// </summary>
        public static UIStateMachine BuildStateMachineFromClip(AnimationClip clip)
        {
            if (clip == null) return null;

            UIStateMachine sm = new UIStateMachine { states = new List<Runtime.State>() };
            var events = AnimationUtility.GetAnimationEvents(clip);
            
            var distinctEventTimes = events.Select(e => e.time).Distinct().OrderBy(t => t).ToList();

            foreach (float eventTime in distinctEventTimes)
            {
                var primaryEventForTime = events.FirstOrDefault(e => Mathf.Approximately(e.time, eventTime) && 
                    (!string.IsNullOrEmpty(e.functionName) || !string.IsNullOrEmpty(e.stringParameter)));
                if (primaryEventForTime == null) continue;

                string stateName = !string.IsNullOrEmpty(primaryEventForTime.functionName) ? 
                    primaryEventForTime.functionName : primaryEventForTime.stringParameter;
                if (string.IsNullOrEmpty(stateName)) continue;

                var state = new Runtime.State { name = stateName, time = eventTime, properties = new List<Property>() };
                
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    float value = curve.Evaluate(eventTime);
                    state.properties.Add(new Property
                    {
                        path = binding.path,
                        componentType = binding.type.AssemblyQualifiedName,
                        propertyName = binding.propertyName,
                        value = value,
                        objectReference = ""
                    });
                }
                
                foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
                {
                    ObjectReferenceKeyframe[] objectFrames = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    ObjectReferenceKeyframe? keyframeAtTime = null;
                    for(int i=0; i < objectFrames.Length; ++i)
                    {
                        if(Mathf.Approximately(objectFrames[i].time, eventTime))
                        {
                            keyframeAtTime = objectFrames[i];
                            break;
                        }
                        if (objectFrames[i].time <= eventTime && (!keyframeAtTime.HasValue || objectFrames[i].time > keyframeAtTime.Value.time))
                        {
                            keyframeAtTime = objectFrames[i];
                        }
                    }

                    if (keyframeAtTime.HasValue && keyframeAtTime.Value.value != null)
                    {
                        string assetPath = AssetDatabase.GetAssetPath(keyframeAtTime.Value.value);
                        string resourcesPath = "";
                        if (!string.IsNullOrEmpty(assetPath))
                        {
                            int resourcesIndex = assetPath.IndexOf("/Resources/");
                            if (resourcesIndex >= 0)
                            {
                                resourcesPath = assetPath.Substring(resourcesIndex + "/Resources/".Length);
                                int dotIndex = resourcesPath.LastIndexOf('.');
                                if (dotIndex >= 0)
                                {
                                    resourcesPath = resourcesPath.Substring(0, dotIndex);
                                }
                            }
                            else
                            {
                                resourcesPath = assetPath;
                            }
                        }

                        var existingProp = state.properties.FirstOrDefault(p => p.path == binding.path && 
                            p.componentType == binding.type.AssemblyQualifiedName && p.propertyName == binding.propertyName);
                        if (existingProp != null)
                        {
                            existingProp.objectReference = resourcesPath;
                        }
                        else
                        {
                            state.properties.Add(new Property
                            {
                                path = binding.path,
                                componentType = binding.type.AssemblyQualifiedName,
                                propertyName = binding.propertyName,
                                value = 0f,
                                objectReference = resourcesPath
                            });
                        }
                    }
                }
                sm.states.Add(state);
            }
            return sm;
        }

        /// <summary>
        /// Fills missing keyframes for all event times
        /// </summary>
        public static void FillMissingKeyframes(AnimationClip clip)
        {
            Undo.RecordObject(clip, "Fill Missing Keyframes");

            // 1. Gather all float curve bindings and their keyframes at time 0
            var floatBindings = AnimationUtility.GetCurveBindings(clip);
            var firstFrameValues = new Dictionary<EditorCurveBinding, float>();
            foreach (var binding in floatBindings)
            {
                var curve = AnimationUtility.GetEditorCurve(clip, binding);
                float valueAtZero = curve.Evaluate(0f);
                firstFrameValues[binding] = valueAtZero;
            }

            // 2. Gather all object reference curve bindings and their keyframes at time 0
            var objBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            var firstFrameObjValues = new Dictionary<EditorCurveBinding, UnityEngine.Object>();
            foreach (var binding in objBindings)
            {
                var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                UnityEngine.Object valueAtZero = null;
                foreach (var kf in keyframes)
                {
                    if (Mathf.Approximately(kf.time, 0f))
                    {
                        valueAtZero = kf.value;
                        break;
                    }
                }
                if (valueAtZero == null && keyframes.Length > 0)
                {
                    // Use the first keyframe before 0 if available
                    valueAtZero = keyframes[0].value;
                }
                firstFrameObjValues[binding] = valueAtZero;
            }

            // 3. Get all event times
            var events = AnimationUtility.GetAnimationEvents(clip);
            var eventTimes = events.Select(ev => ev.time).Distinct().OrderBy(t => t).ToList();

            // 4. For each event time, ensure all properties have a keyframe
            foreach (var t in eventTimes)
            {
                // Float curves
                foreach (var binding in floatBindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    bool hasKey = curve.keys.Any(k => Mathf.Approximately(k.time, t));
                    if (!hasKey && firstFrameValues.TryGetValue(binding, out float v))
                    {
                        var keys = curve.keys.ToList();
                        keys.Add(new Keyframe(t, v));
                        keys.Sort((a, b) => a.time.CompareTo(b.time));
                        curve.keys = keys.ToArray();
                        AnimationUtility.SetEditorCurve(clip, binding, curve);
                    }
                }

                // Object reference curves
                foreach (var binding in objBindings)
                {
                    var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding).ToList();
                    bool hasKey = keyframes.Any(kf => Mathf.Approximately(kf.time, t));
                    if (!hasKey && firstFrameObjValues.TryGetValue(binding, out var objVal))
                    {
                        keyframes.Add(new ObjectReferenceKeyframe { time = t, value = objVal });
                        keyframes.Sort((a, b) => a.time.CompareTo(b.time));
                        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes.ToArray());
                    }
                }
            }

            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Filled missing keyframes for all event frames.", clip);
        }
    }
} 