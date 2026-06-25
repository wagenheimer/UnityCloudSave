using System.IO;
using UnityEditor;
using UnityEngine;
using Wagenheimer.CloudSave;

namespace Wagenheimer.CloudSave.Editor
{
    /// <summary>
    /// Generates a full CloudSaveUI prefab with all child GameObjects and
    /// serialized references pre-assigned.
    ///
    /// Run via: Tools &gt; Cloud Save &gt; Generate UI Prefab
    /// </summary>
    public static class CloudSaveUIPrefabGenerator
    {
        const string PrefabPath = "Assets/Resources/CloudSaveUI.prefab";

        [MenuItem("Tools/Cloud Save/Generate UI Prefab", priority = 100)]
        static void Generate()
        {
            var go = new GameObject("CloudSaveUI");
            var ui = go.AddComponent<CloudSaveUI>();
            ui.BuildDefaultUI();

            var dir = Path.GetDirectoryName(PrefabPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (existing != null)
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out var success);
            else
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath, out var success);

            Object.DestroyImmediate(go);
            AssetDatabase.Refresh();

            if (success)
                Debug.Log($"[CloudSaveUI] Prefab generated at {PrefabPath}");
            else
                Debug.LogError("[CloudSaveUI] Failed to generate prefab");
        }
    }
}
