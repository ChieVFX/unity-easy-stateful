using UnityEditor;
using EasyStateful.Runtime;
using UnityEditorInternal;

namespace EasyStateful.Editor
{
    /// <summary>
    /// Manages editor-time tweening for StatefulRoot preview
    /// </summary>
    public class StatefulEditorTweenManager
    {
        private StatefulRoot root;
        private double lastUpdateTime;

        public StatefulEditorTweenManager(StatefulRoot root)
        {
            this.root = root;
        }

        public void TriggerState(string stateName)
        {
            if (root == null) return;

            root.TweenToState(stateName);
            if (!EditorApplication.isPlaying)
            {
                lastUpdateTime = EditorApplication.timeSinceStartup;
                EditorApplication.update -= EditorManualUpdate;
                EditorApplication.update += EditorManualUpdate;
                EditorApplication.QueuePlayerLoopUpdate();
            }
        }

        private void EditorManualUpdate()
        {
            // Check if the root object still exists and is valid
            if (root == null)
            {
                EditorApplication.update -= EditorManualUpdate;
                return;
            }

            double now = EditorApplication.timeSinceStartup;
            lastUpdateTime = now;
            
            // Force the StatefulRoot to update its tweens in editor mode
            root.EditorUpdate();
            
            SceneView.RepaintAll();
            InternalEditorUtility.RepaintAllViews();
            
            // Check if we should stop updating - stop when no tween is active
            if (!root.IsTweening)
            {
                EditorApplication.update -= EditorManualUpdate;
            }
        }

        public void Cleanup()
        {
            EditorApplication.update -= EditorManualUpdate;
        }
    }
} 