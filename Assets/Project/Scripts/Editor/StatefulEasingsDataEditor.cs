using UnityEngine;
using UnityEditor;
using EasyStateful.Runtime;

namespace EasyStateful.Editor {
    [CustomEditor(typeof(StatefulEasingsData))]
    public class StatefulEasingsDataEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var data = (StatefulEasingsData)target;
            var easeNames = System.Enum.GetNames(typeof(Ease));
            var easeValues = System.Enum.GetValues(typeof(Ease));

            EditorGUILayout.LabelField("Easing Curves", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            for (int i = 0; i < easeNames.Length; i++)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(easeNames[i], GUILayout.Width(120));
                data.curves[i] = EditorGUILayout.CurveField(data.curves[i], GUILayout.Height(20));
                EditorGUILayout.EndHorizontal();
            }
            if (EditorGUI.EndChangeCheck())
            {
                EditorUtility.SetDirty(data);
            }

            EditorGUILayout.Space();
            if (GUILayout.Button("Reset To Default"))
            {
                Undo.RecordObject(data, "Reset Easings To Default");
                data.ResetToDefault();
                EditorUtility.SetDirty(data);
            }
        }
    }
}
