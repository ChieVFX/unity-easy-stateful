using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using System.IO;
using System.Collections.Generic;
#endif

namespace EasyStateful.Editor {
    public class EditorTopMenu
    {
    #if UNITY_EDITOR
        [MenuItem("Tools/UI State Machine/Export Animation To JSON")]
        static void ExportSelectedClipToJson()
        {
            // Ensure an AnimationClip is selected
            var clip = Selection.activeObject as AnimationClip;
            if (clip == null)
            {
                EditorUtility.DisplayDialog("Export Animation To JSON", "Please select an AnimationClip in the Project window.", "OK");
                return;
            }

            // Prompt for save location
            string defaultName = clip.name + ".json";
            string path = EditorUtility.SaveFilePanelInProject(
                "Save Animation to JSON", defaultName, "json",
                "Select a location to save the JSON file.");
            if (string.IsNullOrEmpty(path))
                return;

            // Build state machine data
            StateMachine stateMachine = new StateMachine();
            stateMachine.states = new List<State>();

            // Retrieve all animation events (each marks a state)
            AnimationEvent[] events = AnimationUtility.GetAnimationEvents(clip);
            foreach (var evt in events)
            {
                // Use functionName or fallback to stringParameter
                string stateName = !string.IsNullOrEmpty(evt.functionName)
                    ? evt.functionName : evt.stringParameter;

                State state = new State();
                state.name = stateName;
                state.time = evt.time;
                state.properties = new List<Property>();

                // Numeric curves
                var curveBindings = AnimationUtility.GetCurveBindings(clip);
                foreach (var binding in curveBindings)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    float value = curve.Evaluate(evt.time);
                    var prop = new Property
                    {
                        path = binding.path,
                        componentType = binding.type.AssemblyQualifiedName,
                        propertyName = binding.propertyName,
                        value = value,
                        objectReference = null
                    };
                    state.properties.Add(prop);
                }

                // Object reference curves
                var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
                foreach (var binding in objectBindings)
                {
                    var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    foreach (var kf in keyframes)
                    {
                        if (Mathf.Approximately(kf.time, evt.time))
                        {
                            var prop = new Property
                            {
                                path = binding.path,
                                componentType = binding.type.AssemblyQualifiedName,
                                propertyName = binding.propertyName,
                                value = 0,
                                objectReference = AssetDatabase.GetAssetPath(kf.value)
                            };
                            state.properties.Add(prop);
                            break;
                        }
                    }
                }

                stateMachine.states.Add(state);
            }

            // Serialize to JSON and write file
            string json = JsonUtility.ToJson(stateMachine, true);
            File.WriteAllText(path, json);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Export Complete", "Animation JSON saved to:\n" + path, "OK");
        }

        [System.Serializable]
        public class StateMachine
        {
            public List<State> states;
        }

        [System.Serializable]
        public class State
        {
            public string name;
            public float time;
            public List<Property> properties;
        }

        [System.Serializable]
        public class Property
        {
            public string path;
            public string componentType;
            public string propertyName;
            public float value;
            public string objectReference;
        }
    #endif
    }
}