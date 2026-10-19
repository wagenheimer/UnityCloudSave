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
        const string AccountDeletionUIPath = "Assets/Resources/AccountDeletionUI.prefab";

        public static void GenerateCloudSaveUI()
        {
            var path = CloudSaveUIPath;
            DeletePrefab(path);
            var go = new GameObject("CloudSaveUI");
            var ui = go.AddComponent<CloudSaveUI>();
            ui.BuildDefaultUI();
            SavePrefab(go, path);
        }

        public static void GenerateSyncStatusUI()
        {
            var path = SyncStatusUIPath;
            DeletePrefab(path);
            var go = new GameObject("SyncStatusUI");
            var ui = go.AddComponent<SyncStatusUI>();
            ui.BuildDefaultUI();
            SavePrefab(go, path);
        }

        public static void GenerateCloudAuthUI()
        {
            var path = CloudAuthUIPath;
            DeletePrefab(path);
            var go = new GameObject("CloudAuthUI");
            var ui = go.AddComponent<CloudAuthUI>();
            ui.BuildDefaultUI();
            SavePrefab(go, path);
        }

        public static void GenerateAccountDeletionUI()
        {
            var path = AccountDeletionUIPath;
            DeletePrefab(path);
            var go = new GameObject("AccountDeletionUI", typeof(RectTransform));
            var ui = go.AddComponent<AccountDeletionUI>();
            ui.BuildDefaultUI();
            SavePrefab(go, path);
        }

        [MenuItem("GameObject/UI/Cloud Save/Account Deletion UI", false, 10)]
        public static void CreateAccountDeletionUIInScene(MenuCommand menuCommand = null)
        {
            GameObject parent = null;
            if (menuCommand != null && menuCommand.context is GameObject cmdGo)
                parent = cmdGo;
            else if (Selection.activeTransform != null)
                parent = Selection.activeTransform.gameObject;

            var go = new GameObject("AccountDeletionUI", typeof(RectTransform));
            if (parent != null)
            {
                go.transform.SetParent(parent.transform, false);
            }
            else
            {
                var canvas = Object.FindObjectOfType<Canvas>();
                if (canvas != null)
                    go.transform.SetParent(canvas.transform, false);
            }

            var ui = go.AddComponent<AccountDeletionUI>();
            ui.BuildDefaultUI();

            Undo.RegisterCreatedObjectUndo(go, "Create Account Deletion UI");
            Selection.activeGameObject = go;
            Debug.Log("[CloudSave] Account Deletion UI created in scene/prefab. Customize it in the Inspector!");
        }

        public static void GenerateAll()
        {
            GenerateCloudSaveUI();
            GenerateSyncStatusUI();
            GenerateCloudAuthUI();
            GenerateAccountDeletionUI();
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
