using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using UnityEditorInternal;
using EasyStateful.Runtime;

namespace EasyStateful.Editor {
    [CustomEditor(typeof(StatefulRoot))]
    public class StatefulRootEditor : UnityEditor.Editor
    {
        private StatefulRoot root;
        private SerializedProperty statefulDataAssetProp;
        private SerializedProperty editorClipProp;
        
        // Instance level overrides
        private SerializedProperty overrideDefaultTransitionTimeProp;
        private SerializedProperty customDefaultTransitionTimeProp;
        private SerializedProperty overrideDefaultEaseProp;
        private SerializedProperty customDefaultEaseProp;

        // Group Settings
        private SerializedProperty groupSettingsProp;
        private UnityEditor.Editor groupSettingsEditor; // To draw the group settings inline
        private bool _showGroupSettingsFoldout = false; // For foldable group settings

        private SerializedObject rootSO;

        private bool _isShowJustInCase = false;
        private double lastUpdateTime;

        // Add this static field:
        private static GameObject lastWorkModeGameObject;

        void OnEnable()
        {
            root = (StatefulRoot)target;
            rootSO = new SerializedObject(root);
            statefulDataAssetProp = rootSO.FindProperty("statefulDataAsset");
            editorClipProp = rootSO.FindProperty("editorClip");

            overrideDefaultTransitionTimeProp = rootSO.FindProperty("overrideDefaultTransitionTime");
            customDefaultTransitionTimeProp = rootSO.FindProperty("customDefaultTransitionTime");
            overrideDefaultEaseProp = rootSO.FindProperty("overrideDefaultEase");
            customDefaultEaseProp = rootSO.FindProperty("customDefaultEase");

            groupSettingsProp = rootSO.FindProperty("groupSettings");

            // Create an editor for group settings if it's assigned
            if (root.groupSettings != null)
            {
                groupSettingsEditor = CreateEditor(root.groupSettings);
            }
        }

        void OnDisable()
        {
            // Clean up the group settings editor
            if (groupSettingsEditor != null)
            {
                DestroyImmediate(groupSettingsEditor);
                groupSettingsEditor = null;
            }
        }

        public override void OnInspectorGUI()
        {
            rootSO.Update();
            bool hasAnim = root.GetComponent<Animation>() != null;

            // Work / Runtime mode
            if (!hasAnim)
            {
                if (GUILayout.Button("Work Mode"))
                    EnterWorkMode();
            }
            else
            {
                if (GUILayout.Button("Runtime Mode"))
                    ExitWorkMode();
                GUILayout.Space(5);
                if (GUILayout.Button("New State"))
                    NewStatePrompt.Show(promptName => AddNewStateEvent(promptName));
            }
            GUILayout.Space(10);
            
            EditorGUILayout.PropertyField(statefulDataAssetProp, new GUIContent("Stateful Data Asset"));
            
            // Group Settings Field
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(groupSettingsProp, new GUIContent("Group Settings"));
            if (EditorGUI.EndChangeCheck())
            {
                rootSO.ApplyModifiedProperties(); // Apply the change to groupSettingsProp first
                if (groupSettingsEditor != null) // Destroy old editor
                {
                    DestroyImmediate(groupSettingsEditor);
                    groupSettingsEditor = null;
                }
                if (root.groupSettings != null) // Create new editor if an asset is assigned
                {
                    groupSettingsEditor = CreateEditor(root.groupSettings);
                }
                _showGroupSettingsFoldout = (root.groupSettings != null); // Optionally auto-expand if an asset is assigned
            }

            // Display Group Settings inline if an asset is assigned, now within a foldout
            if (root.groupSettings != null && groupSettingsEditor != null)
            {
                EditorGUILayout.Space();
                // Use EditorGUILayout.Foldout with EditorStyles.foldoutHeader for a styled, simpler foldout
                _showGroupSettingsFoldout = EditorGUILayout.Foldout(_showGroupSettingsFoldout, "Group Settings Overrides", true, EditorStyles.foldoutHeader);
                if (_showGroupSettingsFoldout)
                {
                    EditorGUI.indentLevel+=2;
                    // The nested editor (groupSettingsEditor) will manage its own indentation.
                    groupSettingsEditor.OnInspectorGUI();
                    EditorGUI.indentLevel-=2;
                }
                // EditorGUILayout.EndFoldoutHeaderGroup(); // This is removed
                EditorGUILayout.Space();
            }
            
            EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
            EditorGUILayout.LabelField("Instance Specific Overrides", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // Instance Default Transition Time Override
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(overrideDefaultTransitionTimeProp, GUIContent.none, GUILayout.Width(20));
            EditorGUILayout.LabelField("Time", GUILayout.Width(EditorGUIUtility.labelWidth - 24));
            using (new EditorGUI.DisabledGroupScope(!overrideDefaultTransitionTimeProp.boolValue))
            {
                EditorGUILayout.PropertyField(customDefaultTransitionTimeProp, GUIContent.none);
            }
            EditorGUILayout.EndHorizontal();

            if (!overrideDefaultTransitionTimeProp.boolValue)
            {
                EditorGUI.indentLevel++;
                float effectiveTime;
                string source;
                if (root.groupSettings != null && root.groupSettings.overrideGlobalDefaultTransitionTime)
                {
                    effectiveTime = root.groupSettings.customDefaultTransitionTime;
                    source = "Group Settings";
                }
                else
                {
                    effectiveTime = StatefulGlobalSettings.DefaultTime;
                    source = "Global Settings";
                }
                EditorGUILayout.LabelField($"Using: {effectiveTime:F2}s ({source})", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            // Instance Default Ease Override
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(overrideDefaultEaseProp, GUIContent.none, GUILayout.Width(20));
            EditorGUILayout.LabelField("Ease", GUILayout.Width(EditorGUIUtility.labelWidth - 24));
            using (new EditorGUI.DisabledGroupScope(!overrideDefaultEaseProp.boolValue))
            {
                EditorGUILayout.PropertyField(customDefaultEaseProp, GUIContent.none);
            }
            EditorGUILayout.EndHorizontal();

            if (!overrideDefaultEaseProp.boolValue)
            {
                EditorGUI.indentLevel++;
                Ease effectiveEase;
                string source;
                if (root.groupSettings != null && root.groupSettings.overrideGlobalDefaultEase)
                {
                    effectiveEase = root.groupSettings.customDefaultEase;
                    source = "Group Settings";
                }
                else
                {
                    effectiveEase = StatefulGlobalSettings.DefaultEase;
                    source = "Global Settings";
                }
                EditorGUILayout.LabelField($"Using: {effectiveEase} ({source})", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            EditorGUI.indentLevel--; // Back from "Instance Specific Overrides"

            // State buttons
            string[] displayStateNames = root.stateNames;
            if (hasAnim)
            {
                AnimationClip currentClip = editorClipProp.objectReferenceValue as AnimationClip;
                if (currentClip != null)
                {
                    var events = AnimationUtility.GetAnimationEvents(currentClip);
                    displayStateNames = events
                        .Select(ev => !string.IsNullOrEmpty(ev.functionName) ? ev.functionName : ev.stringParameter)
                        .Where(name => !string.IsNullOrEmpty(name))
                        .Distinct()
                        .ToArray();
                }
            }

            if (displayStateNames != null && displayStateNames.Length > 0)
            {
                EditorGUILayout.LabelField("States");
                foreach (var stateName in displayStateNames)
                {
                    if (GUILayout.Button(stateName))
                    {
                        // If in work mode (Animation component present), set AnimationWindow time to event time
                        if (hasAnim)
                        {
                            AnimationClip currentClip = editorClipProp.objectReferenceValue as AnimationClip;
                            if (currentClip != null)
                            {
                                var events = AnimationUtility.GetAnimationEvents(currentClip);
                                var ev = events.FirstOrDefault(e => (e.functionName == stateName) || (e.stringParameter == stateName));
                                if (ev != null)
                                {
                                    var animWindow = Resources.FindObjectsOfTypeAll<AnimationWindow>().FirstOrDefault();
                                    if (animWindow != null)
                                    {
                                        animWindow.Focus();
                                        animWindow.animationClip = currentClip;
                                        animWindow.previewing = true;
                                        animWindow.time = ev.time;
                                        int frame = Mathf.RoundToInt(ev.time * currentClip.frameRate);
                                        animWindow.frame = frame;
                                        animWindow.Repaint();
                                        Debug.Log($"[StatefulRootEditor] Set AnimationWindow to state '{stateName}' at time {ev.time} (frame {frame})");
                                    }
                                    else
                                    {
                                        Debug.LogWarning("[StatefulRootEditor] AnimationWindow not found.");
                                    }
                                }
                                else
                                {
                                    Debug.LogWarning($"[StatefulRootEditor] No animation event found for state '{stateName}'.");
                                }
                            }
                        }
                        else
                        {
                            TriggerState(stateName);
                        }
                    }
                }
            }

            // Just in case foldout
            _isShowJustInCase = EditorGUILayout.BeginFoldoutHeaderGroup(_isShowJustInCase, "Just in case");
            if (_isShowJustInCase)
            {
                EditorGUILayout.PropertyField(editorClipProp);
                if (GUILayout.Button("Dump State Machine to JSON"))
                {
                    DumpStateMachineToJson();
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            rootSO.ApplyModifiedProperties();
        }

        private string GetSanitizedPath(string pathInput)
        {
            // Remove leading/trailing slashes and ensure it's a relative path
            if (string.IsNullOrWhiteSpace(pathInput)) return "";
            pathInput = pathInput.Trim('/', '\\');
            return pathInput;
        }
        
        private string GetDefaultPath(string configuredPath, string defaultFolder)
        {
            string finalPath = "Assets";
            string sanitizedConfigPath = GetSanitizedPath(configuredPath);

            if (!string.IsNullOrEmpty(sanitizedConfigPath))
            {
                finalPath = Path.Combine(finalPath, sanitizedConfigPath);
            }
            else if (!string.IsNullOrEmpty(defaultFolder)) // Fallback to a general default if specific one is empty
            {
                finalPath = Path.Combine(finalPath, GetSanitizedPath(defaultFolder));
            }
            
            // Ensure the directory exists
            if (!Directory.Exists(finalPath))
            {
                Directory.CreateDirectory(finalPath); // This creates all directories in the path if they don't exist
                AssetDatabase.Refresh();
            }
            return finalPath;
        }

        private void EnterWorkMode()
        {
            rootSO.Update();

            AnimationWindow existingAnimWindow = null;
            bool wasRecordingAndOpen = false;

            // Check if an Animation Window is already open and recording
            var openAnimationWindows = Resources.FindObjectsOfTypeAll<AnimationWindow>();
            if (openAnimationWindows.Length > 0)
            {
                existingAnimWindow = openAnimationWindows[0]; // Typically only one
                // Check if it's recording AND if the current active object for animation is our root.
                // This check could be more sophisticated if needed, e.g. checking animWindow.selection.activeGameObject
                if (existingAnimWindow.recording && Selection.activeGameObject == root.gameObject) 
                {
                    wasRecordingAndOpen = true;
                    existingAnimWindow.recording = false; // Temporarily disable recording
                }
            }

            AnimationClip clip = editorClipProp.objectReferenceValue as AnimationClip;
            bool newClipCreated = false;

            if (clip == null)
            {
                string defaultAnimPath = GetDefaultPath(StatefulGlobalSettings.DefaultAnimationSavePath, "Animations");
                string path = EditorUtility.SaveFilePanelInProject(
                    "Create Animation Clip",
                    root.gameObject.name + ".anim", "anim",
                    "Select location for new AnimationClip",
                    defaultAnimPath);
                if (string.IsNullOrEmpty(path))
                {
                    // If user cancelled, and we turned off recording, restore it.
                    if (wasRecordingAndOpen && existingAnimWindow != null)
                    {
                        existingAnimWindow.recording = true;
                    }
                    return;
                }
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, path);
                newClipCreated = true;
            }

            // Assign the clip to the StatefulRoot's property
            editorClipProp.objectReferenceValue = clip;
            // Apply changes to the StatefulRoot serialized object
            rootSO.ApplyModifiedProperties();

            if (newClipCreated)
            {
                AssetDatabase.SaveAssets(); // Ensure the new clip asset is saved to disk
                AssetDatabase.Refresh();    // Make sure the AssetDatabase knows about it
            }

            // Setup the Animation component on the GameObject
            if (root.TryGetComponent<Animation>(out var anim))
                Undo.DestroyObjectImmediate(anim);
            anim = Undo.AddComponent<Animation>(root.gameObject);
            SetAnimations(anim, clip); // This configures the Animation component with the clip

            // Add default state event to the AnimationClip if none exist
            var events = AnimationUtility.GetAnimationEvents(clip);
            if (events == null || events.Length == 0)
            {
                var ev = new AnimationEvent { time = 0f, functionName = "Default" };
                AnimationUtility.SetAnimationEvents(clip, new[] { ev });
                EditorUtility.SetDirty(clip); // Mark the clip dirty
                AssetDatabase.SaveAssets();   // Save changes to the clip asset
            }

            // Now, ensure the Animation window is open, correctly targeted, and recording
            EditorApplication.ExecuteMenuItem("Window/Animation/Animation"); // Opens/focuses the Animation window
            var animWindow = EditorWindow.GetWindow<AnimationWindow>();

            // Ensure our target GameObject is selected for the Animation window
            if (Selection.activeGameObject != root.gameObject)
            {
                Selection.activeGameObject = root.gameObject;
                // Yielding might be needed if selection change doesn't instantly reflect in anim window
                // EditorApplication.delayCall += () => {
                // animWindow.Focus(); 
                // animWindow.Lock(); 
                // animWindow.recording = true;
                // };
            }
            
            animWindow.recording = true; // Set to record for Work Mode
            animWindow.Focus();
            animWindow.Lock(); // Lock to the current selection (root.gameObject)

            lastWorkModeGameObject = root.gameObject; // Save reference
        }

        private void SetAnimations(Animation anim, AnimationClip clip)
        {
            anim.enabled = false;
            anim.playAutomatically = false;
            clip.legacy = true;
            anim.AddClip(clip, clip.name);
            anim.clip = clip;
            // Set clips to show up in the "Animations" list
            AnimationClip[] clips = new AnimationClip[] { clip };

            // Using SerializedObject to make it editable in the editor
            SerializedObject so = new SerializedObject(anim);
            SerializedProperty animationsProp = so.FindProperty("m_Animations");
            animationsProp.arraySize = clips.Length;
            for (int i = 0; i < clips.Length; i++)
            {
                animationsProp.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
            }
            so.ApplyModifiedProperties();
        }

        private void ExitWorkMode()
        {
            rootSO.Update();
            var clip = editorClipProp.objectReferenceValue as AnimationClip;
            if (clip != null)
            {
                UIStateMachine sm = BuildStateMachineFromClip(clip);
                if (sm == null) {
                    Debug.LogError("Failed to build StateMachine from clip.", root);
                    return;
                }

                StatefulDataAsset dataAsset = statefulDataAssetProp.objectReferenceValue as StatefulDataAsset;
                if (dataAsset == null)
                {
                    string defaultBinaryPath = GetDefaultPath(StatefulGlobalSettings.DefaultBinarySavePath, "StatefulData");
                    string assetPath = EditorUtility.SaveFilePanelInProject(
                        "Create Stateful Data Asset", 
                        root.gameObject.name + "_StateData.asset", 
                        "asset",
                        "Select location for new Stateful Data Asset",
                        defaultBinaryPath); // Use default path
                    if (!string.IsNullOrEmpty(assetPath))
                    {
                        dataAsset = ScriptableObject.CreateInstance<StatefulDataAsset>();
                        dataAsset.stateMachine = sm;
                        AssetDatabase.CreateAsset(dataAsset, assetPath);
                        statefulDataAssetProp.objectReferenceValue = dataAsset;
                    }
                    else return; 
                }
                else
                {
                    Undo.RecordObject(dataAsset, "Update Stateful Data Asset");
                    dataAsset.stateMachine = sm;
                    EditorUtility.SetDirty(dataAsset);
                }
            }
            rootSO.ApplyModifiedProperties();

            StatefulDataAsset currentDataAsset = statefulDataAssetProp.objectReferenceValue as StatefulDataAsset;
            if (currentDataAsset != null)
            {
                root.LoadFromAsset(currentDataAsset);
                Repaint();
            }

            var anim = root.GetComponent<Animation>();
            if (anim != null)
                Undo.DestroyObjectImmediate(anim);

            var win = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(w => w.GetType().Name == "AnimationWindow");
            if (win != null)
            {
                var animWindow = EditorWindow.GetWindow<AnimationWindow>();
                animWindow.recording = false;
                animWindow.Unlock();
            }

            lastWorkModeGameObject = null; // Release reference
        }

        private void AddNewStateEvent(string stateName)
        {
            var clip = editorClipProp.objectReferenceValue as AnimationClip;
            if (clip == null) return;

            var events = AnimationUtility.GetAnimationEvents(clip).ToList();
            float frameTime = 1f / clip.frameRate;
            float newEventTime = 0f;

            if (events.Any())
            {
                newEventTime = events.Max(e => e.time) + frameTime;
            }
            
            var evt = new AnimationEvent { time = newEventTime, functionName = stateName };
            events.Add(evt);
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            EditorUtility.SetDirty(clip);

            root.UpdateStateNamesFromClip(clip); 
            Repaint();
        }

        private void TriggerState(string stateName)
        {
            root.TweenToState(stateName); // Now uses UniTask internally
            if (!EditorApplication.isPlaying)
            {
                lastUpdateTime = EditorApplication.timeSinceStartup;
                EditorApplication.update += EditorManualUpdate;
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        private void EditorManualUpdate()
        {
            double now = EditorApplication.timeSinceStartup;
            float dt = (float)(now - lastUpdateTime);
            lastUpdateTime = now;
            
            // Since we're using UniTask instead of DOTween, we don't need DOTween.ManualUpdate
            // UniTask handles its own scheduling in editor mode
            
            SceneView.RepaintAll();
            InternalEditorUtility.RepaintAllViews();
            
            // Check if we should stop updating (this is a simplified check)
            // In a real implementation, you might want to track active tweens differently
            if (now - lastUpdateTime > 5.0) // Stop after 5 seconds of no activity
                EditorApplication.update -= EditorManualUpdate;
        }

        private UIStateMachine BuildStateMachineFromClip(AnimationClip clip)
        {
            if (clip == null) return null;

            UIStateMachine sm = new UIStateMachine { states = new List<Runtime.State>() };
            var events = AnimationUtility.GetAnimationEvents(clip);
            
            var distinctEventTimes = events.Select(e => e.time).Distinct().OrderBy(t => t).ToList();

            foreach (float eventTime in distinctEventTimes)
            {
                var primaryEventForTime = events.FirstOrDefault(e => Mathf.Approximately(e.time, eventTime) && (!string.IsNullOrEmpty(e.functionName) || !string.IsNullOrEmpty(e.stringParameter)));
                if (primaryEventForTime == null) continue;

                string stateName = !string.IsNullOrEmpty(primaryEventForTime.functionName) ? primaryEventForTime.functionName : primaryEventForTime.stringParameter;
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

                        var existingProp = state.properties.FirstOrDefault(p => p.path == binding.path && p.componentType == binding.type.AssemblyQualifiedName && p.propertyName == binding.propertyName);
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

        private void DumpStateMachineToJson()
        {
            AnimationClip clip = editorClipProp.objectReferenceValue as AnimationClip;
            UIStateMachine smToDump = null;

            if (clip != null)
            {
                smToDump = BuildStateMachineFromClip(clip);
                if (smToDump == null || smToDump.states.Count == 0)
                {
                    EditorUtility.DisplayDialog("Dump to JSON", "Could not build a state machine from the animation clip. Ensure it has events and animated properties.", "OK");
                    return;
                }
            }
            else
            {
                StatefulDataAsset dataAsset = statefulDataAssetProp.objectReferenceValue as StatefulDataAsset;
                if (dataAsset != null && dataAsset.stateMachine != null && dataAsset.stateMachine.states.Count > 0)
                {
                    smToDump = dataAsset.stateMachine;
                }
            }
            
            if (smToDump == null)
            {
                EditorUtility.DisplayDialog("Dump to JSON", "No Animation Clip assigned to 'Editor Clip' field or no data in 'Stateful Data Asset' to dump.", "OK");
                return;
            }

            string json = JsonUtility.ToJson(smToDump, true);
            string defaultName = clip != null ? clip.name : (root.gameObject.name + "_StateData");
            string savePath = EditorUtility.SaveFilePanel("Save State Machine as JSON", "Assets", defaultName + "_dump", "json");
            if (!string.IsNullOrEmpty(savePath))
            {
                File.WriteAllText(savePath, json);
                AssetDatabase.Refresh(); 
            }
        }

        // Change FillMissingKeyframes to static and public, and add a menu item
        [MenuItem("Tools/UI State Machine/Fill blanks &5", priority = 1000)]
        public static void FillMissingKeyframesMenu()
        {
            GameObject targetGO = lastWorkModeGameObject;

            if (targetGO == null)
            {
                EditorUtility.DisplayDialog("No Target", "No GameObject is in Work Mode. Please enter Work Mode first.", "OK");
                return;
            }

            var root = targetGO.GetComponent<StatefulRoot>();
            if (root == null)
            {
                EditorUtility.DisplayDialog("No StatefulRoot", "Target GameObject does not have a StatefulRoot component.", "OK");
                return;
            }

            // Get the editorClip from the component
            var so = new SerializedObject(root);
            var editorClipProp = so.FindProperty("editorClip");
            var clip = editorClipProp.objectReferenceValue as AnimationClip;
            if (clip == null)
            {
                EditorUtility.DisplayDialog("No Animation Clip", "Assign an Animation Clip to the StatefulRoot's 'Editor Clip' field.", "OK");
                return;
            }

            FillMissingKeyframes(clip);
        }

        // Make this static and public so it can be called from the menu
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

        [MenuItem("Tools/UI State Machine/Select Work Mode Object &4", priority = 1001)]
        public static void SelectWorkModeGameObjectMenu()
        {
            if (lastWorkModeGameObject == null)
            {
                EditorUtility.DisplayDialog("No Work Mode Object", "No GameObject is currently in Work Mode.", "OK");
                return;
            }
            Selection.activeGameObject = lastWorkModeGameObject;
            EditorGUIUtility.PingObject(lastWorkModeGameObject);
        }
    }

    public class NewStatePrompt : EditorWindow
    {
        private static Action<string> onOkay;
        private string inputName = string.Empty;
        private const string NameTextFieldControlName = "StateNameTextField";
        private bool needsInitialFocus = false;

        public static void Show(Action<string> callback)
        {
            onOkay = callback;
            var window = CreateInstance<NewStatePrompt>();
            window.titleContent = new GUIContent("New State");
            window.position = new Rect(Screen.width / 2, Screen.height / 2, 250, 80);
            window.needsInitialFocus = true;
            window.ShowUtility();
            window.Focus();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField("State Name:");
            GUI.SetNextControlName(NameTextFieldControlName);
            inputName = EditorGUILayout.TextField(inputName);

            if (needsInitialFocus && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl(NameTextFieldControlName);
                needsInitialFocus = false;
                Repaint();
            }

            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("OK"))
            {
                onOkay?.Invoke(inputName);
                onOkay = null;
                Close();
            }
            if (GUILayout.Button("Cancel"))
            {
                onOkay = null;
                Close();
            }
            GUILayout.EndHorizontal();
        }
    } 
}