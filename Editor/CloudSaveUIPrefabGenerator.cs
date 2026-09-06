using UnityEditor;
using UnityEngine;
using Wagenheimer.CloudSave;

namespace Wagenheimer.CloudSave.Editor
{
    public static class CloudSaveUIPrefabGenerator
    {
        const string CloudSaveUIPath  = "Assets/Resources/CloudSaveUI.prefab";
        const string SyncStatusUIPath = "Assets/Resources/SyncStatusUI.prefab";
        const string CloudAuthUIPath  = "Assets/Resources/CloudAuthUI.prefab";

        [MenuItem("Tools/Wagenheimer/Cloud Save/Setup UI Prefabs/Cloud Save UI", priority = 21)]
        static void GenerateCloudSaveUI()
        {
            var path = CloudSaveUIPath;
            DeletePrefab(path);
            var go = new GameObject("CloudSaveUI");
            var ui = go.AddComponent<CloudSaveUI>();
            ui.BuildDefaultUI();
            SavePrefab(go, path);
        }

        [MenuItem("Tools/Wagenheimer/Cloud Save/Setup UI Prefabs/Sync Status UI", priority = 22)]
        static void GenerateSyncStatusUI()
        {
            var path = SyncStatusUIPath;
            DeletePrefab(path);
            var go = new GameObject("SyncStatusUI");
            var ui = go.AddComponent<SyncStatusUI>();
            ui.BuildDefaultUI();
            SavePrefab(go, path);
        }

        [MenuItem("Tools/Wagenheimer/Cloud Save/Setup UI Prefabs/Cloud Auth UI", priority = 23)]
        static void GenerateCloudAuthUI()
        {
            var path = CloudAuthUIPath;
            DeletePrefab(path);
            var go = new GameObject("CloudAuthUI");
            var ui = go.AddComponent<CloudAuthUI>();
            ui.BuildDefaultUI();
            SavePrefab(go, path);
        }

        [MenuItem("Tools/Wagenheimer/Cloud Save/Setup UI Prefabs/All", priority = 20)]
        static void GenerateAll()
        {
            GenerateCloudSaveUI();
            GenerateSyncStatusUI();
            GenerateCloudAuthUI();
            Debug.Log("[CloudSave] All UI prefabs generated");
        }

        static void DeletePrefab(string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (existing != null)
                AssetDatabase.DeleteAsset(path);
        }

        static void SavePrefab(GameObject go, string path)
        {
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                var parent = System.IO.Path.GetDirectoryName(dir);
                var name   = System.IO.Path.GetFileName(dir);
                if (!string.IsNullOrEmpty(parent) && AssetDatabase.IsValidFolder(parent))
                    AssetDatabase.CreateFolder(parent, name);
                else
                    System.IO.Directory.CreateDirectory(dir);
                AssetDatabase.Refresh();
            }

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path, out bool success);
            Object.DestroyImmediate(go);

            if (success && saved != null)
            {
                Debug.Log($"[CloudSave] Prefab generated at {path}");
                Selection.activeObject = saved;
                EditorGUIUtility.PingObject(saved);
            }
            else
            {
                Debug.LogError($"[CloudSave] Failed to generate prefab at {path}");
            }
        }
    }
}
