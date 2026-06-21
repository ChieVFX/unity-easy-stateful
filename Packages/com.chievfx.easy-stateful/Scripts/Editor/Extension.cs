using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;

namespace EasyStateful.Editor {
    public static class Extension
    {
        public static void Lock(this EditorWindow animationWindow)
        {
            var animationWindowType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AnimationWindow");
            if (animationWindow == null || !animationWindowType.IsInstanceOfType(animationWindow))
            {
                Debug.LogError("Invalid AnimationWindow instance.");
                return;
            }

            // Get m_LockTracker field
            var lockTrackerField = animationWindowType.GetField("m_LockTracker", BindingFlags.NonPublic | BindingFlags.Instance);
            if (lockTrackerField == null)
            {
                Debug.LogError("m_LockTracker field not found.");
                return;
            }

            var lockTracker = lockTrackerField.GetValue(animationWindow);
            if (lockTracker == null)
            {
                Debug.LogError("m_LockTracker is null.");
                return;
            }

            var lockTrackerType = lockTracker.GetType();

            // Set m_IsLocked field directly
            var isLockedBackingField = lockTrackerType.GetField("m_IsLocked", BindingFlags.NonPublic | BindingFlags.Instance);
            if (isLockedBackingField == null)
            {
                Debug.LogError("m_IsLocked field not found.");
                return;
            }

            isLockedBackingField.SetValue(lockTracker, true);

            // Invoke the lockStateChanged UnityEvent
            var lockStateChangedField = lockTrackerType.GetField("lockStateChanged", BindingFlags.NonPublic | BindingFlags.Instance);
            if (lockStateChangedField == null)
            {
                Debug.LogError("lockStateChanged field not found.");
                return;
            }

            var lockStateChanged = lockStateChangedField.GetValue(lockTracker);
            if (lockStateChanged == null)
            {
                Debug.LogError("lockStateChanged is null.");
                return;
            }

            // Call UnityEvent.Invoke(true)
            var unityEventType = lockStateChanged.GetType();
            var invokeMethod = unityEventType.GetMethod("Invoke", BindingFlags.Public | BindingFlags.Instance);
            if (invokeMethod == null)
            {
                Debug.LogError("Invoke method not found.");
                return;
            }

            invokeMethod.Invoke(lockStateChanged, new object[] { true });

            Debug.Log("Animation window locked.");
        }

        public static void Unlock(this AnimationWindow animationWindow)
        {
            // Get the internal AnimationWindow type
            var animationWindowType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AnimationWindow");
            if (animationWindowType == null)
            {
                Debug.LogError("Failed to find UnityEditor.AnimationWindow");
                return;
            }

            // Find any open animation window
            var animationWindows = Resources.FindObjectsOfTypeAll(animationWindowType);
            if (animationWindows.Length == 0)
            {
                Debug.LogWarning("No Animation Window found.");
                return;
            }

            // Access the m_LockTracker field
            var lockTrackerField = animationWindowType.GetField("m_LockTracker", BindingFlags.NonPublic | BindingFlags.Instance);
            if (lockTrackerField == null)
            {
                Debug.LogError("m_LockTracker field not found.");
                return;
            }

            var lockTracker = lockTrackerField.GetValue(animationWindow);
            if (lockTracker == null)
            {
                Debug.LogError("m_LockTracker is null.");
                return;
            }

            // Access the m_IsLocked field (not property)
            var lockTrackerType = lockTracker.GetType();
            var isLockedBackingField = lockTrackerType.GetField("m_IsLocked", BindingFlags.NonPublic | BindingFlags.Instance);
            if (isLockedBackingField == null)
            {
                Debug.LogError("m_IsLocked field not found.");
                return;
            }

            // Set isLocked to false
            isLockedBackingField.SetValue(lockTracker, false);
            Debug.Log("Animation window unlocked.");
        }
    }
}