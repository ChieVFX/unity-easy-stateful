using UnityEditor;
using UnityEngine;
using DG.Tweening; // For Ease enum

[CustomPropertyDrawer(typeof(PropertyOverrideRule))]
public class PropertyOverrideRuleDrawer : PropertyDrawer
{
    private const float ToggleWidth = 18f; // Width for a compact toggle
    private const float IndentWidth = 15f; // Standard indent width

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect foldoutRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        property.isExpanded = EditorGUI.Foldout(foldoutRect, property.isExpanded, label, true);

        if (property.isExpanded)
        {
            var indentLevel = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 1; // Set a base indent for content within the foldout

            Rect currentRect = new Rect(
                position.x + IndentWidth, 
                position.y + EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing, 
                position.width - IndentWidth, 
                EditorGUIUtility.singleLineHeight
            );

            SerializedProperty propName = property.FindPropertyRelative("propertyName");
            SerializedProperty compType = property.FindPropertyRelative("componentType");
            SerializedProperty overrideEaseProp = property.FindPropertyRelative("overrideEase");
            SerializedProperty easeProp = property.FindPropertyRelative("ease");
            SerializedProperty overrideDurationProp = property.FindPropertyRelative("overrideDuration");
            SerializedProperty durationProp = property.FindPropertyRelative("duration");
            SerializedProperty instantEnableDelayedDisableProp = property.FindPropertyRelative("instantEnableDelayedDisable");

            EditorGUI.PropertyField(currentRect, propName);
            currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            EditorGUI.PropertyField(currentRect, compType, new GUIContent("Component Type (Optional)"));
            currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
            
            // "Instant Change" toggle - always visible
            Rect instantChangeToggleRect = new Rect(currentRect.x, currentRect.y, ToggleWidth, currentRect.height);
            Rect instantChangeLabelRect = new Rect(currentRect.x + ToggleWidth, currentRect.y, EditorGUIUtility.labelWidth - ToggleWidth, currentRect.height);
            
            EditorGUI.PropertyField(instantChangeToggleRect, instantEnableDelayedDisableProp, GUIContent.none);
            EditorGUI.LabelField(instantChangeLabelRect, "Instant Change");
            currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;

            bool showStandardOverrides = !instantEnableDelayedDisableProp.boolValue;

            if (showStandardOverrides)
            {
                // Override Duration
                DrawOverrideableProperty(ref currentRect, overrideDurationProp, durationProp, "Duration");

                // Override Ease
                DrawOverrideableProperty(ref currentRect, overrideEaseProp, easeProp, "Ease");
            }
            
            EditorGUI.indentLevel = indentLevel; // Reset indent level
        }
        EditorGUI.EndProperty();
    }

    private void DrawOverrideableProperty(ref Rect currentRect, SerializedProperty overrideBoolProp, SerializedProperty valueProp, string label)
    {
        Rect toggleRect = new Rect(currentRect.x, currentRect.y, ToggleWidth, currentRect.height);
        Rect labelRect = new Rect(currentRect.x + ToggleWidth, currentRect.y, EditorGUIUtility.labelWidth - ToggleWidth, currentRect.height);
        Rect fieldRect = new Rect(currentRect.x + EditorGUIUtility.labelWidth, currentRect.y, currentRect.width - EditorGUIUtility.labelWidth, currentRect.height);

        EditorGUI.PropertyField(toggleRect, overrideBoolProp, GUIContent.none);
        EditorGUI.LabelField(labelRect, label);
        using (new EditorGUI.DisabledGroupScope(!overrideBoolProp.boolValue))
        {
            EditorGUI.PropertyField(fieldRect, valueProp, GUIContent.none);
        }
        currentRect.y += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        float height = EditorGUIUtility.singleLineHeight; // Foldout
        if (property.isExpanded)
        {
            height += EditorGUIUtility.standardVerticalSpacing; // Space after foldout
            
            height += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2; // propName, compType
            height += EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing; // "Instant Change" line

            SerializedProperty instantEnableDelayedDisableProp = property.FindPropertyRelative("instantEnableDelayedDisable");
            bool showStandardOverrides = !instantEnableDelayedDisableProp.boolValue;

            if (showStandardOverrides)
            {
                height += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing); // Duration line
                height += (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing); // Ease line
            }
        }
        return height;
    }
} 