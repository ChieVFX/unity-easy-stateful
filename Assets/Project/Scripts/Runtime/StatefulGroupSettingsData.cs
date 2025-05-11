using UnityEngine;
using DG.Tweening;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "NewStatefulGroupSettings", menuName = "Stateful UI/Group Settings", order = 1)]
public class StatefulGroupSettingsData : ScriptableObject
{
    public bool overrideGlobalDefaultTransitionTime = false;
    [Tooltip("Custom default transition time for this group if override is enabled.")]
    public float customDefaultTransitionTime = 0.3f;

    public bool overrideGlobalDefaultEase = false;
    [Tooltip("Custom default ease for this group if override is enabled.")]
    public Ease customDefaultEase = Ease.InOutQuad;

    [Tooltip("These rules override global property rules and apply before them.")]
    public List<PropertyOverrideRule> propertyOverrides = new List<PropertyOverrideRule>();
}
