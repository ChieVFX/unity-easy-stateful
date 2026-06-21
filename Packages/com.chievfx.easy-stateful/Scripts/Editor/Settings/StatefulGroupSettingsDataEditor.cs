using UnityEngine;
using UnityEditor;
using EasyStateful.Runtime;

namespace EasyStateful.Editor {
    [CustomEditor(typeof(StatefulGroupSettingsData))]
    public class StatefulGroupSettingsDataEditor : UnityEditor.Editor
    {
        private SerializedProperty overrideGlobalDefaultTransitionTimeProp;
        private SerializedProperty customDefaultTransitionTimeProp;
        private SerializedProperty overrideGlobalDefaultEaseProp;
        private SerializedProperty customDefaultEaseProp;
        private SerializedProperty propertyOverridesProp;

        private void OnEnable()
        {
            overrideGlobalDefaultTransitionTimeProp = serializedObject.FindProperty("overrideGlobalDefaultTransitionTime");
            customDefaultTransitionTimeProp = serializedObject.FindProperty("customDefaultTransitionTime");
            overrideGlobalDefaultEaseProp = serializedObject.FindProperty("overrideGlobalDefaultEase");
            customDefaultEaseProp = serializedObject.FindProperty("customDefaultEase");
            propertyOverridesProp = serializedObject.FindProperty("propertyOverrides");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.LabelField("Group Default Transition Overrides", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            // Group Default Transition Time Override
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(overrideGlobalDefaultTransitionTimeProp, GUIContent.none, GUILayout.Width(15));
            EditorGUILayout.LabelField(new GUIContent("Time", "If checked, this group will use its own default transition time instead of the global default."), GUILayout.Width(EditorGUIUtility.labelWidth - 19));
            using (new EditorGUI.DisabledGroupScope(!overrideGlobalDefaultTransitionTimeProp.boolValue))
            {
                EditorGUILayout.PropertyField(customDefaultTransitionTimeProp, GUIContent.none);
            }
            EditorGUILayout.EndHorizontal();

            if (!overrideGlobalDefaultTransitionTimeProp.boolValue)
            {
                EditorGUI.indentLevel++; // For the mini label
                EditorGUILayout.LabelField($"Using Global: {StatefulGlobalSettings.DefaultTime:F2}s", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }

            // Group Default Ease Override
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(overrideGlobalDefaultEaseProp, GUIContent.none, GUILayout.Width(15));
            EditorGUILayout.LabelField(new GUIContent("Ease", "If checked, this group will use its own default ease instead of the global default."), GUILayout.Width(EditorGUIUtility.labelWidth - 19));
            using (new EditorGUI.DisabledGroupScope(!overrideGlobalDefaultEaseProp.boolValue))
            {
                EditorGUILayout.PropertyField(customDefaultEaseProp, GUIContent.none);
            }
            EditorGUILayout.EndHorizontal();
            
            if (!overrideGlobalDefaultEaseProp.boolValue)
            {
                EditorGUI.indentLevel++; // For the mini label
                EditorGUILayout.LabelField($"Using Global: {StatefulGlobalSettings.DefaultEase}", EditorStyles.miniLabel);
                EditorGUI.indentLevel--;
            }
            
            EditorGUI.indentLevel--; // Back from "Group Default Transition Overrides"
            EditorGUILayout.Space();

            EditorGUILayout.PropertyField(propertyOverridesProp, true);

            serializedObject.ApplyModifiedProperties();
        }
    }
}