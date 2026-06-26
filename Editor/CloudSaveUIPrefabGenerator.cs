using UnityEditor;
using UnityEngine;
using Wagenheimer.CloudSave;

namespace Wagenheimer.CloudSave.Editor
{
    public static class CloudSaveUIPrefabGenerator
    {
        const string CloudSaveUIPath = "Assets/Resources/CloudSaveUI.prefab";
        const string SyncStatusUIPath = "Assets/Resources/SyncStatusUI.prefab";
        const string CloudAuthUIPath = "Assets/Resources/CloudAuthUI.prefab";

        [MenuItem("Tools/Cloud Save/Setup UI Prefabs/CloudSaveUI", priority = 100)]
        static void GenerateCloudSaveUI()
        {
            DeletePrefab(CloudSaveUIPath);
            var ui = CloudSaveUI.Create();
            if (ui != null) Ping(ui.gameObject, CloudSaveUIPath);
        }

        [MenuItem("Tools/Cloud Save/Setup UI Prefabs/SyncStatusUI", priority = 101)]
        static void GenerateSyncStatusUI()
        {
            DeletePrefab(SyncStatusUIPath);
            var ui = SyncStatusUI.Create();
            if (ui != null) Ping(ui.gameObject, SyncStatusUIPath);
        }

        [MenuItem("Tools/Cloud Save/Setup UI Prefabs/CloudAuthUI", priority = 102)]
        static void GenerateCloudAuthUI()
        {
            DeletePrefab(CloudAuthUIPath);
            var ui = CloudAuthUI.Create();
            if (ui != null) Ping(ui.gameObject, CloudAuthUIPath);
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

        static void Ping(GameObject go, string path)
        {
            Debug.Log($"[CloudSave] Prefab generated at {path}");
            Selection.activeGameObject = go;
            EditorGUIUtility.PingObject(go);
        }
    }
}
