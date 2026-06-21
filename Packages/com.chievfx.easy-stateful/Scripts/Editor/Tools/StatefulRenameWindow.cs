using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using EasyStateful.Runtime;

namespace EasyStateful.Editor
{
    public class StatefulRenameWindow : EditorWindow
    {
        private GameObject targetObject;
        private string originalName;
        private string newName;
        private string originalPath;
        private string newPath;
        private List<StatefulDataAsset> affectedStatefulAssets = new List<StatefulDataAsset>();
        private List<AnimationClip> affectedAnimationClips = new List<AnimationClip>();
        private List<StatefulRoot> relatedStatefulRoots = new List<StatefulRoot>();
        private Vector2 scrollPosition;
        private bool shouldFocusTextField = true;

        [MenuItem("Edit/Stateful Rename GameObject &2", false, 0)]
        public static void ShowWindow()
        {
            var selectedObject = Selection.activeGameObject;
            if (selectedObject == null)
            {
                EditorUtility.DisplayDialog("No Selection", "Please select a GameObject to rename.", "OK");
                return;
            }

            var window = GetWindow<StatefulRenameWindow>("Stateful Rename");
            window.Initialize(selectedObject);
            window.Show();
        }

        [MenuItem("Edit/Stateful Rename GameObject &2", true)]
        public static bool ValidateShowWindow()
        {
            return Selection.activeGameObject != null;
        }

        private void Initialize(GameObject obj)
        {
            targetObject = obj;
            originalName = obj.name;
            newName = originalName;
            originalPath = GetRelativeGameObjectPath(obj);
            shouldFocusTextField = true;
            
            FindAffectedAssets();
        }

        private void OnGUI()
        {
            if (targetObject == null)
            {
                EditorGUILayout.HelpBox("Target object is null. Please close this window and try again.", MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Stateful GameObject Rename", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Target Object:", targetObject.name);
            EditorGUILayout.LabelField("Current Path:", originalPath);
            EditorGUILayout.Space();

            EditorGUILayout.LabelField("New Name:");
            
            // Handle Enter key in text field
            GUI.SetNextControlName("NewNameField");
            string previousName = newName;
            newName = EditorGUILayout.TextField(newName);
            
            // Auto-focus and select all text when window opens
            if (shouldFocusTextField)
            {
                shouldFocusTextField = false;
                GUI.FocusControl("NewNameField");
                EditorGUI.FocusTextInControl("NewNameField");
            }
            
            // Check for Enter key press when text field is focused
            if (GUI.GetNameOfFocusedControl() == "NewNameField" && Event.current.type == EventType.KeyDown)
            {
                if (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                {
                    Event.current.Use(); // Consume the event
                    
                    bool canPerformRename = !string.IsNullOrEmpty(newName) && newName != originalName;
                    if (canPerformRename)
                    {
                        PerformRename();
                        return; // Exit early since window will close
                    }
                }
                else if (Event.current.keyCode == KeyCode.Escape)
                {
                    Event.current.Use(); // Consume the event
                    Close();
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(newName) && newName != originalName)
            {
                newPath = GetNewPath(originalPath, originalName, newName);
                EditorGUILayout.LabelField("New Path:", newPath);
            }
            else
            {
                newPath = originalPath;
            }

            EditorGUILayout.Space();

            if (relatedStatefulRoots.Count > 0)
            {
                EditorGUILayout.LabelField($"Related StatefulRoots ({relatedStatefulRoots.Count}):", EditorStyles.boldLabel);
                foreach (var root in relatedStatefulRoots)
                {
                    EditorGUILayout.ObjectField(root, typeof(StatefulRoot), true);
                }
                EditorGUILayout.Space();
            }

            if (affectedStatefulAssets.Count > 0 || affectedAnimationClips.Count > 0)
            {
                EditorGUILayout.LabelField("Affected Assets:", EditorStyles.boldLabel);
                
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                
                if (affectedStatefulAssets.Count > 0)
                {
                    EditorGUILayout.LabelField($"Stateful Data Assets ({affectedStatefulAssets.Count}):", EditorStyles.miniBoldLabel);
                    foreach (var asset in affectedStatefulAssets)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(asset, typeof(StatefulDataAsset), false);
                        EditorGUILayout.EndHorizontal();
                    }
                    EditorGUILayout.Space();
                }

                if (affectedAnimationClips.Count > 0)
                {
                    EditorGUILayout.LabelField($"Animation Clips ({affectedAnimationClips.Count}):", EditorStyles.miniBoldLabel);
                    foreach (var clip in affectedAnimationClips)
                    {
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.ObjectField(clip, typeof(AnimationClip), false);
                        EditorGUILayout.EndHorizontal();
                    }
                }
                
                EditorGUILayout.EndScrollView();
            }
            else
            {
                EditorGUILayout.HelpBox("No affected Stateful or Animation assets found.", MessageType.Info);
            }

            EditorGUILayout.Space();

            EditorGUILayout.BeginHorizontal();
            
            bool canRename = !string.IsNullOrEmpty(newName) && newName != originalName;
            using (new EditorGUI.DisabledGroupScope(!canRename))
            {
                if (GUILayout.Button("Rename and Update Assets"))
                {
                    PerformRename();
                }
            }

            if (GUILayout.Button("Cancel"))
            {
                Close();
            }

            EditorGUILayout.EndHorizontal();

            if (canRename)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox($"This will rename '{originalName}' to '{newName}' and update all path references in the affected assets.\n\nPress Enter to apply or Escape to cancel.", MessageType.Warning);
            }
        }

        private void FindAffectedAssets()
        {
            affectedStatefulAssets.Clear();
            affectedAnimationClips.Clear();
            relatedStatefulRoots.Clear();

            // Find StatefulRoots in the scene that could be affected
            FindRelatedStatefulRoots();

            // Check their assigned assets
            foreach (var root in relatedStatefulRoots)
            {
                // Check StatefulDataAsset
                if (root.statefulDataAsset != null && !affectedStatefulAssets.Contains(root.statefulDataAsset))
                {
                    if (ContainsPath(root.statefulDataAsset, originalPath))
                    {
                        affectedStatefulAssets.Add(root.statefulDataAsset);
                    }
                }

                // Check editor clip
#if UNITY_EDITOR
                var serializedObject = new SerializedObject(root);
                var editorClipProperty = serializedObject.FindProperty("editorClip");
                if (editorClipProperty != null && editorClipProperty.objectReferenceValue is AnimationClip editorClip)
                {
                    if (!affectedAnimationClips.Contains(editorClip) && ContainsPath(editorClip, originalPath))
                    {
                        affectedAnimationClips.Add(editorClip);
                    }
                }
#endif
            }
        }

        private void FindRelatedStatefulRoots()
        {
            // Find StatefulRoot on this object or parents
            Transform current = targetObject.transform;
            while (current != null)
            {
                var root = current.GetComponent<StatefulRoot>();
                if (root != null && !relatedStatefulRoots.Contains(root))
                {
                    relatedStatefulRoots.Add(root);
                }
                current = current.parent;
            }

            // If no StatefulRoot found in parents, look for any StatefulRoot in the scene
            // that might reference this object's path
            if (relatedStatefulRoots.Count == 0)
            {
                var allRoots = FindObjectsOfType<StatefulRoot>();
                foreach (var root in allRoots)
                {
                    if (CouldRootReferenceObject(root, targetObject))
                    {
                        relatedStatefulRoots.Add(root);
                    }
                }
            }
        }

        private bool CouldRootReferenceObject(StatefulRoot root, GameObject obj)
        {
            // Check if the object is a child of the StatefulRoot
            Transform current = obj.transform;
            while (current != null)
            {
                if (current == root.transform)
                    return true;
                current = current.parent;
            }
            return false;
        }

        private bool ContainsPath(StatefulDataAsset asset, string path)
        {
            if (asset.stateMachine?.states == null) return false;

            foreach (var state in asset.stateMachine.states)
            {
                if (state.properties != null)
                {
                    foreach (var prop in state.properties)
                    {
                        if (prop.path != null && (prop.path == path || prop.path.StartsWith(path + "/")))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private bool ContainsPath(AnimationClip clip, string path)
        {
            var bindings = AnimationUtility.GetCurveBindings(clip);
            foreach (var binding in bindings)
            {
                if (binding.path == path || binding.path.StartsWith(path + "/"))
                {
                    return true;
                }
            }

            var objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
            foreach (var binding in objectBindings)
            {
                if (binding.path == path || binding.path.StartsWith(path + "/"))
                {
                    return true;
                }
            }
            return false;
        }

        private string GetRelativeGameObjectPath(GameObject obj)
        {
            // Find the closest StatefulRoot parent
            Transform current = obj.transform;
            StatefulRoot closestRoot = null;
            
            while (current != null)
            {
                var root = current.GetComponent<StatefulRoot>();
                if (root != null)
                {
                    closestRoot = root;
                    break;
                }
                current = current.parent;
            }

            if (closestRoot != null)
            {
                // Build path relative to the StatefulRoot
                return GetPathRelativeTo(obj.transform, closestRoot.transform);
            }
            else
            {
                // No StatefulRoot found, use absolute path from scene root
                return GetAbsoluteGameObjectPath(obj);
            }
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
                // Target is not a child of root, return absolute path
                return GetAbsoluteGameObjectPath(target.gameObject);
            }

            pathParts.Reverse();
            return string.Join("/", pathParts);
        }

        private string GetAbsoluteGameObjectPath(GameObject obj)
        {
            string path = obj.name;
            Transform parent = obj.transform.parent;
            
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            
            return path;
        }

        private string GetNewPath(string oldPath, string oldName, string newName)
        {
            if (oldPath == oldName)
            {
                return newName;
            }
            
            if (oldPath.EndsWith("/" + oldName))
            {
                return oldPath.Substring(0, oldPath.Length - oldName.Length) + newName;
            }
            
            return oldPath;
        }

        private void PerformRename()
        {
            if (string.IsNullOrEmpty(newName) || newName == originalName)
                return;

            try
            {
                // Start undo group
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Stateful Rename GameObject");

                // Rename the GameObject
                Undo.RecordObject(targetObject, "Rename GameObject");
                targetObject.name = newName;

                // Update StatefulDataAssets
                foreach (var asset in affectedStatefulAssets)
                {
                    UpdateStatefulDataAsset(asset);
                }

                // Update AnimationClips
                foreach (var clip in affectedAnimationClips)
                {
                    UpdateAnimationClip(clip);
                }

                // Collapse undo group
                Undo.CollapseUndoOperations(undoGroup);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                EditorUtility.DisplayDialog("Rename Complete", 
                    $"Successfully renamed '{originalName}' to '{newName}' and updated {affectedStatefulAssets.Count} Stateful assets and {affectedAnimationClips.Count} Animation clips.", 
                    "OK");

                Close();
            }
            catch (System.Exception ex)
            {
                EditorUtility.DisplayDialog("Rename Failed", $"Failed to rename: {ex.Message}", "OK");
            }
        }

        private void UpdateStatefulDataAsset(StatefulDataAsset asset)
        {
            Undo.RecordObject(asset, "Update Stateful Data Paths");
            
            bool modified = false;
            if (asset.stateMachine?.states != null)
            {
                foreach (var state in asset.stateMachine.states)
                {
                    if (state.properties != null)
                    {
                        foreach (var prop in state.properties)
                        {
                            if (prop.path != null)
                            {
                                if (prop.path == originalPath)
                                {
                                    prop.path = newPath;
                                    modified = true;
                                }
                                else if (prop.path.StartsWith(originalPath + "/"))
                                {
                                    prop.path = newPath + prop.path.Substring(originalPath.Length);
                                    modified = true;
                                }
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

        private void UpdateAnimationClip(AnimationClip clip)
        {
            Undo.RecordObject(clip, "Update Animation Clip Paths");
            
            var bindings = AnimationUtility.GetCurveBindings(clip);
            bool modified = false;

            foreach (var binding in bindings)
            {
                if (binding.path == originalPath || binding.path.StartsWith(originalPath + "/"))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    if (curve != null)
                    {
                        // Remove old binding
                        AnimationUtility.SetEditorCurve(clip, binding, null);
                        
                        // Create new binding with updated path
                        var newBinding = binding;
                        if (binding.path == originalPath)
                        {
                            newBinding.path = newPath;
                        }
                        else if (binding.path.StartsWith(originalPath + "/"))
                        {
                            newBinding.path = newPath + binding.path.Substring(originalPath.Length);
                        }
                        
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
                if (binding.path == originalPath || binding.path.StartsWith(originalPath + "/"))
                {
                    var curve = AnimationUtility.GetObjectReferenceCurve(clip, binding);
                    if (curve != null)
                    {
                        // Remove old binding
                        AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
                        
                        // Create new binding with updated path
                        var newBinding = binding;
                        if (binding.path == originalPath)
                        {
                            newBinding.path = newPath;
                        }
                        else if (binding.path.StartsWith(originalPath + "/"))
                        {
                            newBinding.path = newPath + binding.path.Substring(originalPath.Length);
                        }
                        
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