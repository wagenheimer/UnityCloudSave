using UnityEditor;
using UnityEngine;
using Wagenheimer.CloudSave;

namespace Wagenheimer.CloudSave.Editor
{
    public class CloudSaveTester : EditorWindow
    {
        [MenuItem("Tools/Cloud Save/Test Window", priority = 200)]
        static void Open() => GetWindow<CloudSaveTester>("Cloud Save Test");

        static System.Reflection.MethodInfo _setLoading;
        static System.Reflection.MethodInfo _handleSyncCompleted;
        static System.Reflection.MethodInfo _showConflict;

        void OnEnable()
        {
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
            _setLoading          = typeof(CloudSaveUI).GetMethod("SetLoading", flags);
            _handleSyncCompleted = typeof(CloudSaveUI).GetMethod("HandleSyncCompleted", flags);
            _showConflict        = typeof(CloudSaveUI).GetMethod("ShowConflictDialogAsync", flags);
        }

        void OnGUI()
        {
            GUILayout.Label("Scene Setup", EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create All UIs", GUILayout.Height(30)))
                {
                    CloudSaveUI.Create();
                    SyncStatusUI.Create();
                }
                if (GUILayout.Button("Destroy All UIs", GUILayout.Height(30)))
                {
                    if (CloudSaveUI.Instance != null) DestroyImmediate(CloudSaveUI.Instance.gameObject);
                    if (SyncStatusUI.Instance != null) DestroyImmediate(SyncStatusUI.Instance.gameObject);
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("CloudSaveUI", EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Show Loading")) InvokeOnUI(_setLoading, new object[] { true });
                if (GUILayout.Button("Hide Loading")) InvokeOnUI(_setLoading, new object[] { false });
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Toast: Synced"))    FireToast(CloudSyncResult.CloudApplied);
                if (GUILayout.Button("Toast: LocalNewer")) FireToast(CloudSyncResult.LocalNewer);
                if (GUILayout.Button("Toast: Offline"))    FireToast(CloudSyncResult.Offline);
                if (GUILayout.Button("Toast: Error"))      FireToast(CloudSyncResult.Error);
            }
            if (GUILayout.Button("Show Conflict Dialog"))
            {
                var ui = CloudSaveUI.Instance;
                if (ui == null) { EditorUtility.DisplayDialog("CloudSaveUI", "Create CloudSaveUI first.", "OK"); return; }
                var data = new CloudConflictData(
                    System.DateTime.UtcNow.Ticks - 10000,
                    System.DateTime.UtcNow.Ticks,
                    new byte[] { 1, 2, 3 },
                    CloudConflictReason.CloudIsNewer);
                _ = (System.Threading.Tasks.Task<CloudConflictChoice>)_showConflict.Invoke(ui, new object[] { data });
            }

            GUILayout.Space(10);
            GUILayout.Label("SyncStatusUI", EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Synced"))  SetSyncStatus(SyncStatus.Synced);
                if (GUILayout.Button("Syncing")) SetSyncStatus(SyncStatus.Syncing);
                if (GUILayout.Button("Offline")) SetSyncStatus(SyncStatus.Offline);
                if (GUILayout.Button("Error"))   SetSyncStatus(SyncStatus.Error);
            }

            GUILayout.Space(10);
            GUILayout.Label("CloudAuthUI", EditorStyles.boldLabel);
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Create + Show"))
                {
                    var auth = CloudAuthUI.Create();
                    auth.Show();
                }
                if (GUILayout.Button("Hide"))
                {
                    var auth = FindObjectOfType<CloudAuthUI>();
                    if (auth != null) auth.Hide();
                    else EditorUtility.DisplayDialog("CloudAuthUI", "Create CloudAuthUI first.", "OK");
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Event Simulation", EditorStyles.boldLabel);
            if (GUILayout.Button("Fire OnSyncStarted")) CloudSync.TestFireSyncStarted();
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Completed: CloudApplied")) CloudSync.TestFireSyncCompleted(CloudSyncResult.CloudApplied);
                if (GUILayout.Button("Completed: LocalNewer"))   CloudSync.TestFireSyncCompleted(CloudSyncResult.LocalNewer);
                if (GUILayout.Button("Completed: Offline"))      CloudSync.TestFireSyncCompleted(CloudSyncResult.Offline);
                if (GUILayout.Button("Completed: Error"))        CloudSync.TestFireSyncCompleted(CloudSyncResult.Error);
            }
            using (new GUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Fire OnLinked"))          CloudAuth.TestFireLinked(CloudAuthProvider.GooglePlayGames);
                if (GUILayout.Button("Fire AccountSwitched"))   CloudAuth.TestFireAccountSwitched(CloudAuthProvider.GooglePlayGames);
            }

            GUILayout.Space(10);
            DrawState();
        }

        static void DrawState()
        {
            GUILayout.Label("State", EditorStyles.boldLabel);
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.Toggle("CloudSaveUI.Instance",  CloudSaveUI.Instance  != null);
            EditorGUILayout.Toggle("SyncStatusUI.Instance", SyncStatusUI.Instance != null);
            if (SyncStatusUI.Instance != null)
                EditorGUILayout.EnumPopup("SyncStatusUI.Status", SyncStatusUI.Instance.Status);
            EditorGUILayout.Toggle("CloudAuth.IsReady", CloudAuth.IsReady);
            EditorGUILayout.Toggle("CloudAuth.IsLinked", CloudAuth.IsLinked);
            EditorGUILayout.EnumPopup("CloudAuth.Provider", CloudAuth.Provider);
            EditorGUILayout.TextField("CloudAuth.PlayerId", CloudAuth.PlayerId ?? "(null)");
            if (CloudSync.LastResult.HasValue)
                EditorGUILayout.EnumPopup("CloudSync.LastResult", CloudSync.LastResult.Value);
            else
                EditorGUILayout.LabelField("CloudSync.LastResult", "(null)");
            EditorGUI.EndDisabledGroup();
        }

        static void InvokeOnUI(System.Reflection.MethodInfo method, object[] args)
        {
            var ui = CloudSaveUI.Instance;
            if (ui == null) { EditorUtility.DisplayDialog("CloudSaveUI", "Create CloudSaveUI first.", "OK"); return; }
            method?.Invoke(ui, args);
        }

        static void FireToast(CloudSyncResult result)
        {
            var ui = CloudSaveUI.Instance;
            if (ui == null) { EditorUtility.DisplayDialog("CloudSaveUI", "Create CloudSaveUI first.", "OK"); return; }
            _handleSyncCompleted?.Invoke(ui, new object[] { result });
        }

        static void SetSyncStatus(SyncStatus status)
        {
            if (SyncStatusUI.Instance != null)
                SyncStatusUI.Instance.SetStatus(status);
            else
                EditorUtility.DisplayDialog("SyncStatusUI", "Create SyncStatusUI first.", "OK");
        }
    }
}
