using UnityEditor;
using UnityEngine;
using Wagenheimer.CloudSave.UI;

namespace Wagenheimer.CloudSave.Editor.UI
{
    public static class CloudSaveDebugOverlayEditor
    {
        [MenuItem("Tools/Wagenheimer/Cloud Save/Add Cloud Save Debug Overlay to Scene", priority = 19)]
        public static void AddDebugOverlayToScene()
        {
            var existing = Object.FindFirstObjectByType<CloudSaveDebugOverlay>();
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                EditorGUIUtility.PingObject(existing.gameObject);
                Debug.Log("[CloudSave] CloudSaveDebugOverlay already exists in the current scene.");
                return;
            }

            var go = new GameObject("[CloudSaveDebugOverlay]");
            go.AddComponent<CloudSaveDebugOverlay>();
            Undo.RegisterCreatedObjectUndo(go, "Create Cloud Save Debug Overlay");
            Selection.activeGameObject = go;
            Debug.Log("[CloudSave] Added CloudSaveDebugOverlay to active scene.");
        }
    }
}
