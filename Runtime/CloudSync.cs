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

        /// <summary>Fires when save progress has been reset locally and on the cloud.</summary>
        public static event Action OnSaveReset;

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
        /// One-call bidirectional sync: authenticates, compares the timestamps and
        ///  - UPLOADS <paramref name="localData"/> when the cloud has no save, is older, or the
        ///    player chose to keep the local save;
        ///  - APPLIES the cloud save (via <paramref name="onCloudNewer"/>) when the cloud is newer.
        /// This is the recommended entry point: it leaves local and cloud consistent in one call,
        /// so the game never has to inspect <see cref="LastResult"/> and upload by hand.
        /// </summary>
        /// <param name="localTimestamp">UTC ticks of the local save (0 when there is none yet).</param>
        /// <param name="localData">Local save bytes to upload when local wins. Null/empty skips the upload.</param>
        /// <param name="onCloudNewer">Invoked with the cloud bytes when the cloud save wins.</param>
        /// <param name="conflictReason">Reason passed to <see cref="ConflictResolver"/>.</param>
        public static async Task<CloudSyncResult> SyncAsync(long localTimestamp, byte[] localData,
            Action<byte[]> onCloudNewer, CloudConflictReason conflictReason = CloudConflictReason.CloudIsNewer)
        {
            OnSyncStarted?.Invoke();
            var result = CloudSyncResult.Error;
            try
            {
                await CloudAuth.EnsureSignedInAsync();
                if (!IsAvailable)
                {
                    result = CloudSyncResult.Offline;
                }
                else
                {
                    var (cloudBytes, cloudTs) = await LoadCloudDataAsync();
                    bool canUpload = localData != null && localData.Length > 0;

                    if (cloudBytes == null || cloudTs <= localTimestamp)
                    {
                        // Nothing in the cloud yet, or the local save is the most recent one.
                        if (canUpload)
                        {
                            await SaveAsync(localData, localTimestamp);
                            result = CloudSyncResult.UploadedLocal;
                        }
                        else
                        {
                            result = cloudBytes == null ? CloudSyncResult.NoCloudSave : CloudSyncResult.LocalNewer;
                        }
                    }
                    else
                    {
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
                        else if (canUpload)
                        {
                            await SaveAsync(localData, localTimestamp);
                            result = CloudSyncResult.UploadedLocal;
                        }
                        else
                        {
                            result = CloudSyncResult.UserChoseLocal;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSync] Sync error: {e.Message}");
                result = CloudSyncResult.Error;
            }
            finally
            {
                LastResult = result;
                OnSyncCompleted?.Invoke(result);
            }
            return result;
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

        /// <summary>
        /// Deletes the cloud save slot keys (_dataKey and _tsKey) from UGS Cloud Save.
        /// </summary>
        public static async Task<bool> DeleteCloudSaveAsync()
        {
            if (!IsAvailable) return false;
            try
            {
                await CloudSaveService.Instance.Data.Player.DeleteAsync(_dataKey);
                await CloudSaveService.Instance.Data.Player.DeleteAsync(_tsKey);
                Debug.Log("[CloudSync] Cloud save deleted.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSync] DeleteCloudSave failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Resets game progress locally via <paramref name="onClearLocalSave"/>, and updates
        /// or wipes the cloud save accordingly.
        ///
        /// If <paramref name="getCleanSaveBytes"/> is provided, the clean save is uploaded with
        /// a fresh UTC timestamp (DateTime.UtcNow.Ticks), ensuring any other devices sync to this reset.
        /// If null, the cloud save keys are deleted.
        /// Fires <see cref="OnSaveReset"/>.
        /// </summary>
        public static async Task<bool> ResetProgressAsync(Action onClearLocalSave, Func<byte[]> getCleanSaveBytes = null)
        {
            try
            {
                onClearLocalSave?.Invoke();

                if (getCleanSaveBytes != null)
                {
                    byte[] cleanBytes = getCleanSaveBytes();
                    long newTs = DateTime.UtcNow.Ticks;
                    await SaveAsync(cleanBytes, newTs);
                }
                else
                {
                    await DeleteCloudSaveAsync();
                }

                OnSaveReset?.Invoke();
                Debug.Log("[CloudSync] ResetProgress completed successfully.");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSync] ResetProgress failed: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// Loads raw bytes and timestamp directly from UGS without triggering conflict resolution.
        /// Used internally by CloudSync and CloudMigration.
        /// </summary>
        public static async Task<(byte[] bytes, long timestamp)> LoadRawCloudDataAsync()
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

        private static Task<(byte[] bytes, long timestamp)> LoadCloudDataAsync() => LoadRawCloudDataAsync();

#if UNITY_EDITOR
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        internal static void TestFireSyncStarted() => OnSyncStarted?.Invoke();

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        internal static void TestFireSyncCompleted(CloudSyncResult result) => OnSyncCompleted?.Invoke(result);

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        internal static void TestFireSaveReset() => OnSaveReset?.Invoke();
#endif
    }
}