using UnityEngine;
using UnityEditor;
using EasyStateful.Runtime;

namespace EasyStateful.Editor {
    [CustomEditor(typeof(StatefulEasingsData))]
    public class StatefulEasingsDataEditor : UnityEditor.Editor
    {
        private bool showBuiltInEasings = false;

        public override void OnInspectorGUI()
        {
            var data = (StatefulEasingsData)target;
            var easeNames = System.Enum.GetNames(typeof(Ease));
            var easeValues = System.Enum.GetValues(typeof(Ease));

            EditorGUI.BeginChangeCheck();
            
            // Show user custom easings first and expanded by default
            EditorGUILayout.LabelField("Custom User Easing Curves", EditorStyles.boldLabel);
            
            bool hasUserEasings = false;
            for (int i = 0; i < easeNames.Length; i++)
            {
                Ease easeType = (Ease)easeValues.GetValue(i);
                
                // Only show user custom easings in this section
                if (!IsUserCustomEasing(easeType))
                    continue;
                    
                hasUserEasings = true;
                EditorGUILayout.BeginHorizontal();
                string displayName = StatefulEasingsData.GetEaseDisplayName(easeType);
                EditorGUILayout.LabelField(displayName, GUILayout.Width(120));
                data.curves[i] = EditorGUILayout.CurveField(data.curves[i], GUILayout.Height(20));
                EditorGUILayout.EndHorizontal();
            }
            
            if (!hasUserEasings)
            {
                EditorGUILayout.HelpBox("No user custom easings found. Make sure the Ease enum includes User00-User09.", MessageType.Info);
            }
            
            EditorGUILayout.Space();
            
            // Show built-in easings under a foldout (collapsed by default)
            showBuiltInEasings = EditorGUILayout.Foldout(showBuiltInEasings, "Built-in Easing Curves", true);
            
            if (showBuiltInEasings)
            {
                EditorGUI.indentLevel++;
                
                for (int i = 0; i < easeNames.Length; i++)
                {
                    Ease easeType = (Ease)easeValues.GetValue(i);
                    
                    // Skip user custom easings for the built-in section
                    if (IsUserCustomEasing(easeType))
                        continue;
                        
                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.LabelField(easeNames[i], GUILayout.Width(120));
                    data.curves[i] = EditorGUILayout.CurveField(data.curves[i], GUILayout.Height(20));
                    EditorGUILayout.EndHorizontal();
                }
                
                EditorGUI.indentLevel--;
            }
            
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(data);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset Built-in Easings to Default"))
            {
                Undo.RecordObject(data, "Reset Built-in Easings To Default");
                data.ResetToDefault();
                EditorUtility.SetDirty(data);
            }
            
            EditorGUILayout.HelpBox("Note: 'Reset to Default' only affects built-in easings. Your custom user easings will be preserved.", MessageType.Info);
        }
        
        private static bool IsUserCustomEasing(Ease ease)
        {
            string easeName = ease.ToString();
            return easeName.StartsWith("User");
        }
    }
}
