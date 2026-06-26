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

        [MenuItem("Tools/Cloud Save/Setup UI Prefabs/CloudSaveUI", priority = 100)]
        static void GenerateCloudSaveUI()
        {
            var path = CloudSaveUIPath;
            DeletePrefab(path);
            var go = new GameObject("CloudSaveUI");
            var ui = go.AddComponent<CloudSaveUI>();
            ui.BuildDefaultUI();
            SavePrefab(go, path);
        }

        [MenuItem("Tools/Cloud Save/Setup UI Prefabs/SyncStatusUI", priority = 101)]
        static void GenerateSyncStatusUI()
        {
            var path = SyncStatusUIPath;
            DeletePrefab(path);
            var go = new GameObject("SyncStatusUI");
            var ui = go.AddComponent<SyncStatusUI>();
            ui.BuildDefaultUI();
            SavePrefab(go, path);
        }

        [MenuItem("Tools/Cloud Save/Setup UI Prefabs/CloudAuthUI", priority = 102)]
        static void GenerateCloudAuthUI()
        {
            var path = CloudAuthUIPath;
            DeletePrefab(path);
            var go = new GameObject("CloudAuthUI");
            var ui = go.AddComponent<CloudAuthUI>();
            ui.BuildDefaultUI();
            SavePrefab(go, path);
        }

        [MenuItem("Tools/Cloud Save/Setup UI Prefabs/All", priority = 90)]
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

            var saved = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);

            if (saved != null)
            {
                var instance = Object.Instantiate(saved);
                instance.name = go.name;
                Debug.Log($"[CloudSave] Prefab generated at {path}");
                Selection.activeGameObject = instance;
                EditorGUIUtility.PingObject(instance);
            }
            else
            {
                Debug.LogError($"[CloudSave] Failed to generate prefab at {path}");
            }
        }
    }
}
