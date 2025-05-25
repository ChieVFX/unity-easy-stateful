using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using EasyStateful.Runtime;

namespace EasyStateful.Editor {
    public class StatefulGlobalSettingsEditorWindow : EditorWindow
    {
        private StatefulGlobalSettingsData settingsData;
        private SerializedObject serializedSettings;
        private SerializedProperty defaultTimeProp;
        private SerializedProperty defaultEaseProp;
        private SerializedProperty easingsDataProp;
        private SerializedProperty overridesProp;
        private SerializedProperty defaultBinarySavePathProp;
        private SerializedProperty defaultAnimationSavePathProp;
        private Vector2 scrollPosition;

        [MenuItem("Window/Stateful UI/Global Settings")] // Updated Menu Path
        public static void ShowWindow()
        {
            GetWindow<StatefulGlobalSettingsEditorWindow>("Global Stateful Settings");
        }

        private void OnEnable()
        {
            LoadOrCreateSettings();
        }

        void LoadOrCreateSettings()
        {
#if UNITY_EDITOR
            StatefulGlobalSettings.ClearCachedInstance(); 
#endif
            
            // Try to load existing settings first
            string resourcesFolder = "Assets/Resources";
            string settingsAssetPath = Path.Combine(resourcesFolder, $"{StatefulGlobalSettings.GlobalSettingsPathConstant}.asset");
            
            settingsData = AssetDatabase.LoadAssetAtPath<StatefulGlobalSettingsData>(settingsAssetPath);

            if (settingsData == null) 
            {
                Debug.Log("Creating new StatefulGlobalSettings and StatefulEasingsData assets...");
                
                // Create Resources folder if it doesn't exist
                if (!AssetDatabase.IsValidFolder(resourcesFolder))
                {
                    AssetDatabase.CreateFolder("Assets", "Resources");
                }

                // Create the easings data first
                var easingsData = CreateInstance<StatefulEasingsData>();
                easingsData.InitializeWithDefaults();
                
                string easingsAssetPath = Path.Combine(resourcesFolder, "StatefulEasingsData.asset");
                AssetDatabase.CreateAsset(easingsData, easingsAssetPath);
                
                // Create the global settings
                settingsData = CreateInstance<StatefulGlobalSettingsData>();
                settingsData.defaultTransitionTime = 0.5f;
                settingsData.defaultEase = Ease.OutExpo;
                settingsData.propertyOverrides = new List<PropertyOverrideRule>() {
                    new PropertyOverrideRule("m_IsActive", "", true, false),
                    new PropertyOverrideRule("m_Color.r", "", false, true),
                    new PropertyOverrideRule("m_Color.g", "", false, true),
                    new PropertyOverrideRule("m_Color.b", "", false, true),
                    new PropertyOverrideRule("m_Color.a", "", false, true),
                };
                settingsData.defaultBinarySavePath = "StatefulData";
                settingsData.defaultAnimationSavePath = "Animations";
                settingsData.easingsData = easingsData; // Link the easings data
                
                AssetDatabase.CreateAsset(settingsData, settingsAssetPath);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                
                Debug.Log($"Created StatefulGlobalSettings at {settingsAssetPath}");
                Debug.Log($"Created StatefulEasingsData at {easingsAssetPath}");
                
#if UNITY_EDITOR
                StatefulGlobalSettings.ClearCachedInstance(); 
#endif
            }
            
            if (settingsData != null)
            {
                // If easings data is missing, create it
                if (settingsData.easingsData == null)
                {
                    Debug.Log("Creating missing StatefulEasingsData...");
                    var easingsData = CreateInstance<StatefulEasingsData>();
                    easingsData.InitializeWithDefaults();
                    
                    string easingsAssetPath = Path.Combine(resourcesFolder, "StatefulEasingsData.asset");
                    AssetDatabase.CreateAsset(easingsData, easingsAssetPath);
                    
                    settingsData.easingsData = easingsData;
                    EditorUtility.SetDirty(settingsData);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    
                    Debug.Log($"Created missing StatefulEasingsData at {easingsAssetPath}");
                }
                
                serializedSettings = new SerializedObject(settingsData);
                defaultTimeProp = serializedSettings.FindProperty("defaultTransitionTime");
                defaultEaseProp = serializedSettings.FindProperty("defaultEase");
                easingsDataProp = serializedSettings.FindProperty("easingsData");
                overridesProp = serializedSettings.FindProperty("propertyOverrides");
                defaultBinarySavePathProp = serializedSettings.FindProperty("defaultBinarySavePath");
                defaultAnimationSavePathProp = serializedSettings.FindProperty("defaultAnimationSavePath");
            }
            else
            {
                Debug.LogError("Failed to load or create StatefulGlobalSettingsData.");
            }
        }

        private void OnGUI()
        {
            if (settingsData == null || serializedSettings == null)
            {
                EditorGUILayout.HelpBox($"StatefulGlobalSettingsData asset not found or failed to initialize. It should be at 'Assets/Resources/{StatefulGlobalSettings.GlobalSettingsPathConstant}.asset'. Try reopening the window.", MessageType.Error);
                if (GUILayout.Button("Attempt to Load/Create Global Settings"))
                {
                    LoadOrCreateSettings(); 
                }
                return;
            }

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

            serializedSettings.Update();

            EditorGUILayout.PropertyField(easingsDataProp, new GUIContent("Easings Data"));
            
            // Add button to reset built-in easings to default
            if (GUILayout.Button("Reset Built-in Easings to Default"))
            {
                if (settingsData.easingsData != null)
                {
                    settingsData.easingsData.ResetToDefault();
                    EditorUtility.SetDirty(settingsData.easingsData);
                }
            }

            EditorGUILayout.LabelField("Global Default Transition Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(defaultTimeProp, new GUIContent("Default Time"));
            EditorGUILayout.PropertyField(defaultEaseProp, new GUIContent("Default Ease"));

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Global Property Specific Overrides", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(overridesProp, true);

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Default Save Paths (within Assets/)", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(defaultBinarySavePathProp, new GUIContent("Binary Data Path", "e.g., 'MyGame/StatefulData'. Path is relative to Assets/"));
            EditorGUILayout.PropertyField(defaultAnimationSavePathProp, new GUIContent("Animation Clip Path", "e.g., 'MyGame/Animations'. Path is relative to Assets/"));

            if (serializedSettings.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(settingsData);
#if UNITY_EDITOR
                StatefulGlobalSettings.ClearCachedInstance(); // Ensure changes are picked up immediately
#endif
            }

            EditorGUILayout.EndScrollView();
        }
    }
}