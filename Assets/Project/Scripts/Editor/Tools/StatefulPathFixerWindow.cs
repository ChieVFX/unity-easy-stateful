using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using EasyStateful.Runtime;

namespace EasyStateful.Editor
{
    public class StatefulPathFixerWindow : EditorWindow
    {
        private GameObject selectedObject;
        private StatefulRoot closestStatefulRoot;
        private List<BrokenPathGroup> brokenPathGroups = new List<BrokenPathGroup>();
        private Vector2 scrollPosition;

        [System.Serializable]
        private class BrokenPathGroup
        {
            public string path;
            public GameObject assignedObject;
            public List<BrokenPropertyInfo> properties = new List<BrokenPropertyInfo>();
            public bool foundInStatefulData;
            public bool foundInAnimationClip;
        }

        [System.Serializable]
        private class BrokenPropertyInfo
        {
            public string componentType;
            public string propertyName;
            public bool foundInStatefulData;
            public bool foundInAnimationClip;
        }

        [MenuItem("Edit/Stateful Fix Broken Paths &3", false, 1)]
        public static void ShowWindow()
        {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select a GameObject to use as replacement.", "OK");
                return;
            }

            var window = GetWindow<StatefulPathFixerWindow>("Stateful Path Fixer");
            window.Initialize(selectedObject);
            window.Show();
        }

        [MenuItem("Edit/Stateful Fix Broken Paths &3", true)]
        public static bool ValidateShowWindow()
        {
            return Selection.activeGameObject != null;
        }

        private void Initialize(GameObject obj)
        {
            selectedObject = obj;
            FindClosestStatefulRoot();
            FindBrokenPaths();
        }

        private void OnGUI()
        {
            if (selectedObject == null)
            {
                EditorGUILayout.HelpBox("Selected object is null. Please close this window and try again.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Stateful Path Fixer", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Selected Object:", selectedObject.name);
            
            if (closestStatefulRoot != null)
            {
                EditorGUILayout.LabelField("Closest StatefulRoot:", closestStatefulRoot.name);
                EditorGUILayout.LabelField("Current Path:", GetRelativeGameObjectPath(selectedObject));
            }
            else
            {
                EditorGUILayout.HelpBox("No StatefulRoot found in parent hierarchy.", MessageType.Warning);
                return;
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Refresh Broken Paths"))
            {
                FindBrokenPaths();
            }

            EditorGUILayout.Space();

            if (brokenPathGroups.Count > 0)
            {
                int totalProperties = brokenPathGroups.Sum(g => g.properties.Count);
                EditorGUILayout.LabelField($"Broken Paths Found: {brokenPathGroups.Count} objects ({totalProperties} properties)", EditorStyles.boldLabel);
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(400));
                
                for (int i = 0; i < brokenPathGroups.Count; i++)
                {
                    var pathGroup = brokenPathGroups[i];
                    
                    EditorGUILayout.BeginVertical("box");
                    
                    EditorGUILayout.LabelField($"Broken GameObject Path:", EditorStyles.miniBoldLabel);
                    EditorGUILayout.LabelField($"  Path: {pathGroup.path}");
                    
                    string foundIn = "";
                    if (pathGroup.foundInStatefulData && pathGroup.foundInAnimationClip)
                        foundIn = "StatefulData & AnimationClip";
                    else if (pathGroup.foundInStatefulData)
                        foundIn = "StatefulData";
                    else if (pathGroup.foundInAnimationClip)
                        foundIn = "AnimationClip";
                    
                    EditorGUILayout.LabelField($"  Found in: {foundIn}");
                    EditorGUILayout.LabelField($"  Affected Properties: {pathGroup.properties.Count}");
                    
                    // Show properties in a compact way
                    EditorGUILayout.Space(3);
                    EditorGUILayout.LabelField("Properties:", EditorStyles.miniLabel);
                    
                    // Group properties by component type for cleaner display
                    var componentGroups = pathGroup.properties.GroupBy(p => p.componentType).ToList();
                    foreach (var componentGroup in componentGroups)
                    {
                        string componentName = GetShortTypeName(componentGroup.Key);
                        var propertyNames = componentGroup.Select(p => p.propertyName).ToArray();
                        EditorGUILayout.LabelField($"    {componentName}: {string.Join(", ", propertyNames)}", EditorStyles.miniLabel);
                    }
                    
                    EditorGUILayout.Space(5);
                    
                    EditorGUILayout.LabelField("Assign Replacement:", EditorStyles.miniBoldLabel);
                    pathGroup.assignedObject = (GameObject)EditorGUILayout.ObjectField(
                        "New Object:", 
                        pathGroup.assignedObject, 
                        typeof(GameObject), 
                        true);
                    
                    if (pathGroup.assignedObject != null)
                    {
                        string newPath = GetRelativeGameObjectPath(pathGroup.assignedObject);
                        EditorGUILayout.LabelField($"  New Path: {newPath}");
                        
                        // Validate that the new object has the required components
                        var missingComponents = new List<string>();
                        var foundComponents = new List<string>();
                        
                        foreach (var componentGroup in componentGroups)
                        {
                            var componentType = System.Type.GetType(componentGroup.Key);
                            if (componentType != null && componentType != typeof(GameObject))
                            {
                                var component = pathGroup.assignedObject.GetComponent(componentType);
                                if (component == null)
                                {
                                    missingComponents.Add(GetShortTypeName(componentGroup.Key));
                                }
                                else
                                {
                                    foundComponents.Add(GetShortTypeName(componentGroup.Key));
                                }
                            }
                        }
                        
                        if (foundComponents.Count > 0)
                        {
                            EditorGUILayout.HelpBox($"✓ Found components: {string.Join(", ", foundComponents)}", MessageType.Info);
                        }
                        
                        if (missingComponents.Count > 0)
                        {
                            EditorGUILayout.HelpBox($"⚠ Missing components: {string.Join(", ", missingComponents)}", MessageType.Warning);
                        }
                    }
                    
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Use Selected Object"))
                    {
                        pathGroup.assignedObject = selectedObject;
                    }
                    
                    // Add individual fix button for this path
                    using (new EditorGUI.DisabledGroupScope(pathGroup.assignedObject == null))
                    {
                        if (GUILayout.Button("Fix This Path"))
                        {
                            ApplyPathFix(pathGroup);
                        }
                    }
                    EditorGUILayout.EndHorizontal();
                    
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space();
                }
                
                EditorGUILayout.EndScrollView();
                
                EditorGUILayout.Space();
                
                EditorGUILayout.BeginHorizontal();
                
                bool hasAssignments = brokenPathGroups.Any(g => g.assignedObject != null);
                using (new EditorGUI.DisabledGroupScope(!hasAssignments))
                {
                    if (GUILayout.Button("Apply All Path Fixes"))
                    {
                        ApplyAllPathFixes();
                    }
                }
                
                if (GUILayout.Button("Close"))
                {
                    Close();
                }
                
                EditorGUILayout.EndHorizontal();
            }
            else
            {
                EditorGUILayout.HelpBox("No broken paths found in the StatefulRoot's assets.", MessageType.Info);
                
                if (GUILayout.Button("Close"))
                {
                    Close();
                }
            }
        }

        private void FindClosestStatefulRoot()
        {
            Transform current = selectedObject.transform;
            while (current != null)
            {
                var root = current.GetComponent<StatefulRoot>();
                if (root != null)
                {
                    closestStatefulRoot = root;
                    return;
                }
                current = current.parent;
            }
            closestStatefulRoot = null;
        }

        private void FindBrokenPaths()
        {
            brokenPathGroups.Clear();
            
            if (closestStatefulRoot == null)
                return;

            var pathGroups = new Dictionary<string, BrokenPathGroup>();

            // Check StatefulDataAsset
            if (closestStatefulRoot.statefulDataAsset != null)
            {
                FindBrokenPathsInStatefulData(closestStatefulRoot.statefulDataAsset, pathGroups);
            }

            // Check editor clip
            var serializedObject = new SerializedObject(closestStatefulRoot);
            var editorClipProperty = serializedObject.FindProperty("editorClip");
            if (editorClipProperty != null && editorClipProperty.objectReferenceValue is AnimationClip editorClip)
            {
                FindBrokenPathsInAnimationClip(editorClip, pathGroups);
            }

            brokenPathGroups = pathGroups.Values.ToList();
        }

        private void FindBrokenPathsInStatefulData(StatefulDataAsset asset, Dictionary<string, BrokenPathGroup> pathGroups)
        {
            if (asset.stateMachine?.states == null) return;

            foreach (var state in asset.stateMachine.states)
            {
                if (state.properties != null)
                {
                    foreach (var prop in state.properties)
                    {
                        if (!string.IsNullOrEmpty(prop.path) && !PathExists(prop.path))
                        {
                            if (!pathGroups.ContainsKey(prop.path))
                            {
                                pathGroups[prop.path] = new BrokenPathGroup
                                {
                                    path = prop.path,
                                    foundInStatefulData = true,
                                    foundInAnimationClip = false
                                };
                            }
                            else
                            {
                                pathGroups[prop.path].foundInStatefulData = true;
                            }

                            var pathGroup = pathGroups[prop.path];
                            
                            // Check if this property is already in the group
                            var existingProperty = pathGroup.properties.FirstOrDefault(p => 
                                p.componentType == prop.componentType && 
                                p.propertyName == prop.propertyName);
                            
                            if (existingProperty != null)
                            {
                                existingProperty.foundInStatefulData = true;
                            }
                            else
                            {
                                pathGroup.properties.Add(new BrokenPropertyInfo
                                {
                                    componentType = prop.componentType,
                                    propertyName = prop.propertyName,
                                    foundInStatefulData = true,
                                    foundInAnimationClip = false
                                });
                            }
                        }
                    }
                }
            }
        }

        private void FindBrokenPathsInAnimationClip(AnimationClip clip, Dictionary<string, BrokenPathGroup> pathGroups)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                if (!string.IsNullOrEmpty(binding.path) && !PathExists(binding.path))
                {
                    if (!pathGroups.ContainsKey(binding.path))
                    {
                        pathGroups[binding.path] = new BrokenPathGroup
                        {
                            path = binding.path,
                            foundInStatefulData = false,
                            foundInAnimationClip = true
                        };
                    }
                    else
                    {
                        pathGroups[binding.path].foundInAnimationClip = true;
                    }

                    var pathGroup = pathGroups[binding.path];
                    string componentType = $"{binding.type.FullName}, {binding.type.Assembly.GetName().Name}";
                    
                    // Check if this property is already in the group
                    var existingProperty = pathGroup.properties.FirstOrDefault(p => 
                        p.componentType == componentType && 
                        p.propertyName == binding.propertyName);
                    
                    if (existingProperty != null)
                    {
                        existingProperty.foundInAnimationClip = true;
                    }
                    else
                    {
                        pathGroup.properties.Add(new BrokenPropertyInfo
                        {
                            componentType = componentType,
                            propertyName = binding.propertyName,
                            foundInStatefulData = false,
                            foundInAnimationClip = true
                        });
                    }
                }
            }

            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (var binding in objectBindings)
            {
                if (!string.IsNullOrEmpty(binding.path) && !PathExists(binding.path))
                {
                    if (!pathGroups.ContainsKey(binding.path))
                    {
                        pathGroups[binding.path] = new BrokenPathGroup
                        {
                            path = binding.path,
                            foundInStatefulData = false,
                            foundInAnimationClip = true
                        };
                    }
                    else
                    {
                        pathGroups[binding.path].foundInAnimationClip = true;
                    }

                    var pathGroup = pathGroups[binding.path];
                    string componentType = $"{binding.type.FullName}, {binding.type.Assembly.GetName().Name}";
                    
                    // Check if this property is already in the group
                    var existingProperty = pathGroup.properties.FirstOrDefault(p => 
                        p.componentType == componentType && 
                        p.propertyName == binding.propertyName);
                    
                    if (existingProperty != null)
                    {
                        existingProperty.foundInAnimationClip = true;
                    }
                    else
                    {
                        pathGroup.properties.Add(new BrokenPropertyInfo
                        {
                            componentType = componentType,
                            propertyName = binding.propertyName,
                            foundInStatefulData = false,
                            foundInAnimationClip = true
                        });
                    }
                }
            }
        }

        private bool PathExists(string path)
        {
            if (string.IsNullOrEmpty(path))
                return true; // Empty path is considered valid (root)

            Transform target = closestStatefulRoot.transform.Find(path);
            return target != null;
        }

        private string GetRelativeGameObjectPath(GameObject obj)
        {
            if (closestStatefulRoot == null)
                return obj.name;

            return GetPathRelativeTo(obj.transform, closestStatefulRoot.transform);
        }

        private string GetPathRelativeTo(Transform target, Transform root)
        {
            if (target == root)
                return "";

            List<string> pathParts = new List<string>();
            Transform current = target;
            
            while (current != null && current != root)
            {
                pathParts.Add(current.name);
                current = current.parent;
            }

            if (current != root)
            {
                // Target is not a child of root
                return target.name;
            }

            pathParts.Reverse();
            return string.Join("/", pathParts);
        }

        private string GetShortTypeName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
                return "Unknown";

            var parts = fullTypeName.Split(',');
            if (parts.Length > 0)
            {
                var typePart = parts[0].Trim();
                var lastDot = typePart.LastIndexOf('.');
                if (lastDot >= 0 && lastDot < typePart.Length - 1)
                {
                    return typePart.Substring(lastDot + 1);
                }
                return typePart;
            }
            return fullTypeName;
        }

        private void ApplyPathFix(BrokenPathGroup pathGroup)
        {
            if (pathGroup.assignedObject == null)
                return;

            try
            {
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName($"Fix Broken Path: {pathGroup.path}");

                string newPath = GetRelativeGameObjectPath(pathGroup.assignedObject);

                foreach (var property in pathGroup.properties)
                {
                    // Update StatefulDataAsset
                    if (property.foundInStatefulData && closestStatefulRoot.statefulDataAsset != null)
                    {
                        UpdateStatefulDataAssetPath(closestStatefulRoot.statefulDataAsset, pathGroup.path, newPath, property.componentType, property.propertyName);
                    }

                    // Update AnimationClip
                    if (property.foundInAnimationClip)
                    {
                        var serializedObject = new SerializedObject(closestStatefulRoot);
                        var editorClipProperty = serializedObject.FindProperty("editorClip");
                        if (editorClipProperty != null && editorClipProperty.objectReferenceValue is AnimationClip editorClip)
                        {
                            UpdateAnimationClipPath(editorClip, pathGroup.path, newPath, property.componentType, property.propertyName);
                        }
                    }
                }

                Undo.CollapseUndoOperations(undoGroup);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Refresh the broken paths list
                FindBrokenPaths();
                
                Debug.Log($"Fixed broken path: {pathGroup.path} -> {newPath} ({pathGroup.properties.Count} properties)");
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Fix Failed", $"Failed to fix path: {ex.Message}", "OK");
            }
        }

        private void ApplyAllPathFixes()
        {
            try
            {
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Stateful Fix All Broken Paths");

                int fixedPathsCount = 0;
                int fixedPropertiesCount = 0;

                foreach (var pathGroup in brokenPathGroups)
                {
                    if (pathGroup.assignedObject == null)
                        continue;

                    string newPath = GetRelativeGameObjectPath(pathGroup.assignedObject);

                    foreach (var property in pathGroup.properties)
                    {
                        // Update StatefulDataAsset
                        if (property.foundInStatefulData && closestStatefulRoot.statefulDataAsset != null)
                        {
                            UpdateStatefulDataAssetPath(closestStatefulRoot.statefulDataAsset, pathGroup.path, newPath, property.componentType, property.propertyName);
                        }

                        // Update AnimationClip
                        if (property.foundInAnimationClip)
                        {
                            var serializedObject = new SerializedObject(closestStatefulRoot);
                            var editorClipProperty = serializedObject.FindProperty("editorClip");
                            if (editorClipProperty != null && editorClipProperty.objectReferenceValue is AnimationClip editorClip)
                            {
                                UpdateAnimationClipPath(editorClip, pathGroup.path, newPath, property.componentType, property.propertyName);
                            }
                        }

                        fixedPropertiesCount++;
                    }

                    fixedPathsCount++;
                }

                Undo.CollapseUndoOperations(undoGroup);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Path Fixes Applied", 
                    $"Successfully fixed {fixedPathsCount} broken paths ({fixedPropertiesCount} properties).", 
                    "OK");

                // Refresh the broken paths list
                FindBrokenPaths();
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Fix Failed", $"Failed to apply path fixes: {ex.Message}", "OK");
            }
        }

        private void UpdateStatefulDataAssetPath(StatefulDataAsset asset, string oldPath, string newPath, string componentType, string propertyName)
        {
            Undo.RecordObject(asset, "Update Stateful Data Path");
            
            bool modified = false;
            if (asset.stateMachine?.states != null)
            {
                foreach (var state in asset.stateMachine.states)
                {
                    if (state.properties != null)
                    {
                        foreach (var prop in state.properties)
                        {
                            if (prop.path == oldPath && 
                                prop.componentType == componentType && 
                                prop.propertyName == propertyName)
                            {
                                prop.path = newPath;
                                modified = true;
                            }
                        }
                    }
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(asset);
            }
        }

        private void UpdateAnimationClipPath(AnimationClip clip, string oldPath, string newPath, string componentType, string propertyName)
        {
            Undo.RecordObject(clip, "Update Animation Clip Path");
            
            var componentTypeObj = System.Type.GetType(componentType);
            if (componentTypeObj == null)
                return;

            bool modified = false;

            // Handle regular curve bindings
            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                if (binding.path == oldPath && 
                    binding.type == componentTypeObj && 
                    binding.propertyName == propertyName)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve != null)
                    {
                        // Remove old binding
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                        
                        // Create new binding with updated path
                        var newBinding = binding;
                        newBinding.path = newPath;
                        
                        // Set curve with new binding
                        AnimationUtility.SetEditorCurve(clip, newBinding, curve);
                        modified = true;
                    }
                }
            }

            // Handle object reference bindings
            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (var binding in objectBindings)
            {
                if (binding.path == oldPath && 
                    binding.type == componentTypeObj && 
                    binding.propertyName == propertyName)
                {
                    var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (curve != null)
                    {
                        // Remove old binding
                        AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                        
                        // Create new binding with updated path
                        var newBinding = binding;
                        newBinding.path = newPath;
                        
                        // Set curve with new binding
                        AnimationUtility.SetObjectReferenceCurve(clip, newBinding, curve);
                        modified = true;
                    }
                }
            }

            if (modified)
            {
                EditorUtility.SetDirty(clip);
            }
        }
    }
} 