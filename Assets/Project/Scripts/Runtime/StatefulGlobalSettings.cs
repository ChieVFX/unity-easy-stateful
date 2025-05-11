using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;
using System.Linq;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class StatefulGlobalSettings
{
    public const string GlobalSettingsPathConstant = "StatefulGlobalSettings"; // Updated constant
    private static StatefulGlobalSettingsData _data;

    public static StatefulGlobalSettingsData Instance
    {
        get
        {
            if (_data == null)
            {
                LoadData();
            }
            return _data;
        }
    }

    private static void LoadData()
    {
        _data = Resources.Load<StatefulGlobalSettingsData>(GlobalSettingsPathConstant);
#if UNITY_EDITOR
        if (_data == null)
        {
            string[] guids = AssetDatabase.FindAssets($"t:{nameof(StatefulGlobalSettingsData)}");
            if (guids.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _data = AssetDatabase.LoadAssetAtPath<StatefulGlobalSettingsData>(path);
            }
        }
#endif
        if (_data == null && Application.isPlaying) 
        {
             Debug.LogWarning($"StatefulGlobalSettingsData asset not found at 'Resources/{GlobalSettingsPathConstant}'. Using hardcoded defaults. Please create/configure via 'Window/Stateful UI/Global Settings'.");
            _data = ScriptableObject.CreateInstance<StatefulGlobalSettingsData>(); 
            // Initialize with some defaults if needed, though the SO constructor should handle its own.
            _data.defaultTransitionTime = 0.5f;
            _data.defaultEase = Ease.Linear;
            _data.propertyOverrides = new List<PropertyOverrideRule>() {
                new PropertyOverrideRule("m_IsActive", "", true, false),
                new PropertyOverrideRule("m_Color.r", "", false, true),
                new PropertyOverrideRule("m_Color.g", "", false, true),
                new PropertyOverrideRule("m_Color.b", "", false, true),
                new PropertyOverrideRule("m_Color.a", "", false, true),
            };
            _data.defaultBinarySavePath = "StatefulData";
            _data.defaultAnimationSavePath = "Animations";
        }
        else if (_data == null && !Application.isPlaying)
        {
            // For editor cases where the asset might not exist yet, and we don't want runtime warnings
            // or instantiation if it's about to be created by the editor window.
            // We can allow _data to remain null; calling code should be null-safe.
        }
    }

#if UNITY_EDITOR
    public static void ClearCachedInstance() {
        _data = null;
    }
#endif

    public static float DefaultTime => Instance?.defaultTransitionTime ?? 0.5f;
    public static Ease DefaultEase => Instance?.defaultEase ?? Ease.Linear;
    public static List<PropertyOverrideRule> PropertyOverrides => Instance?.propertyOverrides ?? new List<PropertyOverrideRule>();
    public static string DefaultBinarySavePath => Instance?.defaultBinarySavePath ?? "";
    public static string DefaultAnimationSavePath => Instance?.defaultAnimationSavePath ?? "";

    // This method now specifically gets rules from the GLOBAL settings.
    public static PropertyOverrideRule GetGlobalPropertyOverrideRule(string propertyName, string componentTypeFullName)
    {
        if (Instance == null || Instance.propertyOverrides == null) return null;

        var specificRule = Instance.propertyOverrides.FirstOrDefault(r =>
            r.propertyName == propertyName &&
            !string.IsNullOrEmpty(r.componentType) &&
            r.componentType == componentTypeFullName);

        if (specificRule != null) return specificRule;

        var generalRule = Instance.propertyOverrides.FirstOrDefault(r =>
            r.propertyName == propertyName &&
            string.IsNullOrEmpty(r.componentType));

        return generalRule;
    }
}