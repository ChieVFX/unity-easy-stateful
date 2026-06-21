using UnityEditor;
using UnityEngine;
using EasyStateful.Runtime;

namespace EasyStateful.Editor {
    [CustomPropertyDrawer(typeof(PropertyOverrideRule))]
    public class PropertyOverrideRuleDrawer : PropertyDrawer
    {
        private const float ToggleWidth = 18f; // Width for a compact toggle
        private const float IndentWidth = 15f; // Standard indent width

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var propertyNameProp = property.FindPropertyRelative("propertyName");
            var componentTypeProp = property.FindPropertyRelative("componentType");
            var pathWildcardProp = property.FindPropertyRelative("pathWildcard");
            var overrideEaseProp = property.FindPropertyRelative("overrideEase");
            var easeProp = property.FindPropertyRelative("ease");
            var instantChangeProp = property.FindPropertyRelative("instantEnableDelayedDisable");
            var useCustomTimingProp = property.FindPropertyRelative("useCustomTiming");
            var customTimingStartProp = property.FindPropertyRelative("customTimingStart");
            var customTimingEndProp = property.FindPropertyRelative("customTimingEnd");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            Rect currentRect = new Rect(position.x, position.y, position.width, lineHeight);

            // Property Name
            EditorGUI.PropertyField(currentRect, propertyNameProp);
            currentRect.y += lineHeight + spacing;

            // Component Type
            EditorGUI.PropertyField(currentRect, componentTypeProp);
            currentRect.y += lineHeight + spacing;

            // Path Wildcard
            EditorGUI.PropertyField(currentRect, pathWildcardProp);
            currentRect.y += lineHeight + spacing;

            // Instant Change
            EditorGUI.PropertyField(currentRect, instantChangeProp);
            currentRect.y += lineHeight + spacing;

            bool isInstantChange = instantChangeProp.boolValue;

            // Override Ease (only if not instant change)
            if (!isInstantChange)
            {
                EditorGUI.PropertyField(currentRect, overrideEaseProp);
                currentRect.y += lineHeight + spacing;

                if (overrideEaseProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    EditorGUI.PropertyField(currentRect, easeProp);
                    currentRect.y += lineHeight + spacing;
                    EditorGUI.indentLevel--;
                }

                // Custom Timing (only if not instant change)
                EditorGUI.PropertyField(currentRect, useCustomTimingProp);
                currentRect.y += lineHeight + spacing;

                if (useCustomTimingProp.boolValue)
                {
                    EditorGUI.indentLevel++;
                    
                    EditorGUI.PropertyField(currentRect, customTimingStartProp, new GUIContent("Start Pause"));
                    currentRect.y += lineHeight + spacing;
                    
                    EditorGUI.PropertyField(currentRect, customTimingEndProp, new GUIContent("End Pause"));
                    currentRect.y += lineHeight + spacing;
                    
                    // Show timing breakdown
                    float start = customTimingStartProp.floatValue;
                    float end = customTimingEndProp.floatValue;
                    float total = start + 1f + end;
                    
                    EditorGUI.LabelField(currentRect, $"Timing: {start:F1}s pause + 1s anim + {end:F1}s pause = {total:F1}x duration");
                    currentRect.y += lineHeight + spacing;
                    
                    EditorGUI.indentLevel--;
                }
            }

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var instantChangeProp = property.FindPropertyRelative("instantEnableDelayedDisable");
            var overrideEaseProp = property.FindPropertyRelative("overrideEase");
            var useCustomTimingProp = property.FindPropertyRelative("useCustomTiming");

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            
            int lines = 4; // propertyName, componentType, pathWildcard, instantChange
            
            if (!instantChangeProp.boolValue)
            {
                lines += 1; // overrideEase
                
                if (overrideEaseProp.boolValue)
                {
                    lines += 1; // ease
                }
                
                lines += 1; // useCustomTiming
                
                if (useCustomTimingProp.boolValue)
                {
                    lines += 3; // start, end, timing breakdown
                }
            }

            return lines * lineHeight + (lines - 1) * spacing;
        }
    } 
}