using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Wagenheimer.CloudSave.Editor
{
    [InitializeOnLoad]
    internal static class UpdateChecker
    {
        const string PackageJsonUrl = "https://raw.githubusercontent.com/wagenheimer/UnityCloudSave/main/package.json";
        const string RepoUrl = "https://github.com/wagenheimer/UnityCloudSave";
        const string PrefLastCheckTicks = "Wagenheimer.CloudSave.UpdateChecker.LastCheckTicks";
        const string PrefSkipVersion = "Wagenheimer.CloudSave.UpdateChecker.SkipVersion";
        const double CheckIntervalHours = 24;

        static UpdateChecker()
        {
            EditorApplication.delayCall += () => CheckForUpdate(force: false);
        }

        [MenuItem("Tools/Wagenheimer/Cloud Save/Check for Updates...", priority = 41)]
        static void CheckForUpdateMenuItem() => CheckForUpdate(force: true);

        static void CheckForUpdate(bool force)
        {
            if (!force && !IntervalElapsed())
                return;

            var request = UnityWebRequest.Get(PackageJsonUrl);
            request.timeout = 5;
            var op = request.SendWebRequest();
            op.completed += _ => OnRequestComplete(request, force);
        }

        static bool IntervalElapsed()
        {
            var stored = EditorPrefs.GetString(PrefLastCheckTicks, "0");
            if (!long.TryParse(stored, out var ticks))
                return true;

            return (DateTime.UtcNow - new DateTime(ticks, DateTimeKind.Utc)).TotalHours >= CheckIntervalHours;
        }

        static void OnRequestComplete(UnityWebRequest request, bool force)
        {
            EditorPrefs.SetString(PrefLastCheckTicks, DateTime.UtcNow.Ticks.ToString());

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.Log($"[CloudSave] Update check failed: {request.error}");
                request.Dispose();
                return;
            }

            string remoteVersion = null;
            try
            {
                remoteVersion = JsonUtility.FromJson<PackageJsonVersionOnly>(request.downloadHandler.text)?.version;
            }
            catch (Exception e)
            {
                Debug.Log($"[CloudSave] Update check failed: could not parse remote package.json ({e.Message})");
            }

            request.Dispose();

            var localVersion = GetLocalVersion();
            if (string.IsNullOrEmpty(remoteVersion) || string.IsNullOrEmpty(localVersion))
                return;

            if (!IsNewer(remoteVersion, localVersion))
            {
                Debug.Log($"[CloudSave] Up to date (installed: {localVersion}).");
                return;
            }

            if (!force && EditorPrefs.GetString(PrefSkipVersion, "") == remoteVersion)
            {
                Debug.Log($"[CloudSave] Version {remoteVersion} available (installed: {localVersion}) but ignored by user preference.");
                return;
            }

            Debug.Log($"[CloudSave] New version available: {remoteVersion} (installed: {localVersion}). See {RepoUrl}/releases/latest");
            UpdateAvailableWindow.Show("Cloud Save", localVersion, remoteVersion, RepoUrl, PrefSkipVersion);
        }

        static string GetLocalVersion()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(UpdateChecker).Assembly);
            return packageInfo?.version;
        }

        static bool IsNewer(string remote, string local)
        {
            if (Version.TryParse(remote, out var remoteVer) && Version.TryParse(local, out var localVer))
                return remoteVer > localVer;

            return string.CompareOrdinal(remote, local) > 0;
        }

        [Serializable]
        class PackageJsonVersionOnly
        {
            public string version;
        }
    }
}
