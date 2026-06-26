using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Services.CloudSave;
using Unity.Services.CloudSave.Models;
using UnityEngine;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// Cross-platform cloud save backed by Unity Cloud Save (UGS).
    /// Auth is handled by <see cref="CloudAuth"/> (anonymous by default; upgradeable to
    /// Google Play Games or Apple). Timestamp-based conflict resolution, offline fallback.
    ///
    /// Usage:
    ///   1. CloudSync.Configure("my_game_save");
    ///   2. _ = CloudSync.InitAndSyncAsync(localTimestamp, ApplyCloudSave);
    ///   3. _ = CloudSync.SaveAsync(bytes, timestamp);         // after every local save
    ///   4. _ = CloudAuth.LinkGooglePlayGamesAsync(code);      // Android, after GPGS auth
    ///   5. _ = CloudAuth.LinkAppleGameCenterAsync(...);       // iOS, after Game Center auth
    ///
    /// UI hooks:
    ///   CloudSync.OnSyncStarted   += () => ShowLoadingOverlay();
    ///   CloudSync.OnSyncCompleted += result => HideLoadingOverlay();
    ///   CloudSync.ConflictResolver = async data => await ShowConflictDialog(data);
    /// </summary>
    public static class CloudSync
    {
        private const string DefaultSlot = "save";
        private static string _dataKey = DefaultSlot;
        private static string _tsKey   = DefaultSlot + "_ts";

        /// <summary>True once UGS init + anonymous sign-in are complete.</summary>
        public static bool IsAvailable => CloudAuth.IsReady;

        /// <summary>The data key prefix used for cloud storage.</summary>
        public static string DataKey => _dataKey;

        /// <summary>The result of the most recent sync operation, or null before the first sync.</summary>
        public static CloudSyncResult? LastResult { get; private set; }

        /// <summary>Fires when a sync operation begins (before network calls).</summary>
        public static event Action OnSyncStarted;

        /// <summary>Fires when a sync operation completes, with the outcome.</summary>
        public static event Action<CloudSyncResult> OnSyncCompleted;

        /// <summary>
        /// Optional async delegate called when the cloud save is newer than the local one.
        /// Return <see cref="CloudConflictChoice.UseCloud"/> to apply the cloud save,
        /// or <see cref="CloudConflictChoice.UseLocal"/> to keep the local save.
        /// When null, cloud always wins (default behaviour).
        /// </summary>
        public static Func<CloudConflictData, Task<CloudConflictChoice>> ConflictResolver { get; set; }

        /// <summary>Sets the cloud storage key prefix. Call once before InitAndSyncAsync.</summary>
        public static void Configure(string slotName)
        {
            _dataKey = slotName;
            _tsKey   = slotName + "_ts";
        }

        /// <summary>
        /// Initializes Unity Services with anonymous auth, then optionally invokes
        /// <paramref name="onCloudNewer"/> if the cloud save wins conflict resolution.
        /// Fires <see cref="OnSyncStarted"/> and <see cref="OnSyncCompleted"/>.
        /// Safe to fire-and-forget.
        /// </summary>
        public static async Task InitAndSyncAsync(long localTimestamp, Action<byte[]> onCloudNewer,
            CloudConflictReason conflictReason = CloudConflictReason.CloudIsNewer)
        {
            OnSyncStarted?.Invoke();
            var result = CloudSyncResult.Error;
            try
            {
                await CloudAuth.EnsureSignedInAsync();
                if (!IsAvailable)
                {
                    result = CloudSyncResult.Offline;
                    return;
                }

                var (cloudBytes, cloudTs) = await LoadCloudDataAsync();

                if (cloudBytes == null)
                {
                    result = CloudSyncResult.NoCloudSave;
                    return;
                }

                if (cloudTs <= localTimestamp)
                {
                    result = CloudSyncResult.LocalNewer;
                    return;
                }

                var choice = CloudConflictChoice.UseCloud;
                if (ConflictResolver != null)
                {
                    var data = new CloudConflictData(localTimestamp, cloudTs, cloudBytes, conflictReason);
                    choice = await ConflictResolver(data);
                }

                if (choice == CloudConflictChoice.UseCloud)
                {
                    onCloudNewer?.Invoke(cloudBytes);
                    result = CloudSyncResult.CloudApplied;
                }
                else
                {
                    result = CloudSyncResult.UserChoseLocal;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSync] InitAndSync error: {e.Message}");
                result = CloudSyncResult.Error;
            }
            finally
            {
                LastResult = result;
                OnSyncCompleted?.Invoke(result);
            }
        }

        /// <summary>
        /// Persists <paramref name="data"/> to the cloud with the given timestamp.
        /// No-op when offline. Safe to fire-and-forget.
        /// </summary>
        public static async Task SaveAsync(byte[] data, long timestamp)
        {
            if (!IsAvailable) return;
            try
            {
                var payload = new Dictionary<string, object>
                {
                    { _dataKey, Convert.ToBase64String(data) },
                    { _tsKey,   timestamp                    }
                };
                await CloudSaveService.Instance.Data.Player.SaveAsync(payload);
                Debug.Log("[CloudSync] Saved to cloud.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSync] Save failed: {e.Message}");
            }
        }

        private static async Task<(byte[] bytes, long timestamp)> LoadCloudDataAsync()
        {
            try
            {
                var keys    = new HashSet<string> { _dataKey, _tsKey };
                var results = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (!results.TryGetValue(_dataKey, out var dataItem) ||
                    !results.TryGetValue(_tsKey,   out var tsItem))
                    return (null, 0);

                long cloudTs  = tsItem.Value.GetAs<long>();
                byte[] bytes  = Convert.FromBase64String(dataItem.Value.GetAs<string>());
                return (bytes, cloudTs);
            }
            catch (CloudSaveException e) when (e.Reason == CloudSaveExceptionReason.NotFound)
            {
                Debug.Log("[CloudSync] No cloud save found yet.");
                return (null, 0);
            }
        }

#if UNITY_EDITOR
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        internal static void TestFireSyncStarted() => OnSyncStarted?.Invoke();

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        internal static void TestFireSyncCompleted(CloudSyncResult result) => OnSyncCompleted?.Invoke(result);
#endif
    }
}