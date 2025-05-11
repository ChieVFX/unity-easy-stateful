using UnityEngine;
using UnityEditor;
using DG.Tweening;
using System.Collections.Generic;
using System.IO; // Required for Path combine

public class StatefulGlobalSettingsEditorWindow : EditorWindow
{
    private StatefulGlobalSettingsData settingsData;
    private SerializedObject serializedSettings;
    private SerializedProperty defaultTimeProp;
    private SerializedProperty defaultEaseProp;
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
        settingsData = StatefulGlobalSettings.Instance; 

        if (settingsData == null) 
        {
            settingsData = CreateInstance<StatefulGlobalSettingsData>();
            // Initialize with defaults from the class definition if any
            settingsData.defaultTransitionTime = 0.5f;
            settingsData.defaultEase = Ease.Linear;
            settingsData.propertyOverrides = new List<PropertyOverrideRule>() {
                new PropertyOverrideRule("m_IsActive", "", true, false),
                new PropertyOverrideRule("m_Color.r", "", false, true),
                new PropertyOverrideRule("m_Color.g", "", false, true),
                new PropertyOverrideRule("m_Color.b", "", false, true),
                new PropertyOverrideRule("m_Color.a", "", false, true),
            };
            settingsData.defaultBinarySavePath = "StatefulData";
            settingsData.defaultAnimationSavePath = "Animations";


            string resourcesFolder = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }
            string assetPath = Path.Combine(resourcesFolder, $"{StatefulGlobalSettings.GlobalSettingsPathConstant}.asset");
            
            AssetDatabase.CreateAsset(settingsData, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"Created new StatefulGlobalSettingsData at {assetPath}");
            
#if UNITY_EDITOR
            StatefulGlobalSettings.ClearCachedInstance(); 
#endif
            settingsData = StatefulGlobalSettings.Instance; 
        }
        
        if (settingsData != null)
        {
            serializedSettings = new SerializedObject(settingsData);
            defaultTimeProp = serializedSettings.FindProperty("defaultTransitionTime");
            defaultEaseProp = serializedSettings.FindProperty("defaultEase");
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