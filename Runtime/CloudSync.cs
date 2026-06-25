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
    ///   3. _ = CloudSync.SaveAsync(bytes, timestamp);  // after every local save
    ///   4. _ = CloudAuth.LinkGooglePlayGamesAsync(code) // Android, after GPGS auth
    ///   5. _ = CloudAuth.LinkAppleGameCenterAsync(...)  // iOS, after Game Center auth
    /// </summary>
    public static class CloudSync
    {
        private const string DefaultSlot = "save";
        private static string _dataKey = DefaultSlot;
        private static string _tsKey   = DefaultSlot + "_ts";

        /// <summary>True once UGS init + anonymous sign-in are complete.</summary>
        public static bool IsAvailable => CloudAuth.IsReady;

        /// <summary>Sets the cloud storage key prefix. Call once before InitAndSyncAsync.</summary>
        public static void Configure(string slotName)
        {
            _dataKey = slotName;
            _tsKey   = slotName + "_ts";
        }

        /// <summary>
        /// Initializes Unity Services with anonymous auth, then invokes
        /// <paramref name="onCloudNewer"/> if the cloud save is newer than
        /// <paramref name="localTimestamp"/>. Safe to fire-and-forget.
        /// </summary>
        public static async Task InitAndSyncAsync(long localTimestamp, Action<byte[]> onCloudNewer)
        {
            try
            {
                await CloudAuth.EnsureSignedInAsync();
                if (!IsAvailable) return;

                var cloudBytes = await LoadIfNewerAsync(localTimestamp);
                if (cloudBytes != null)
                    onCloudNewer?.Invoke(cloudBytes);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSync] InitAndSync error: {e.Message}");
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

        // ── private ──────────────────────────────────────────────────────────

        private static async Task<byte[]> LoadIfNewerAsync(long localTimestamp)
        {
            try
            {
                var keys    = new HashSet<string> { _dataKey, _tsKey };
                var results = await CloudSaveService.Instance.Data.Player.LoadAsync(keys);

                if (!results.TryGetValue(_dataKey, out var dataItem) ||
                    !results.TryGetValue(_tsKey,   out var tsItem))
                    return null;

                long cloudTs = tsItem.Value.GetAs<long>();
                if (cloudTs <= localTimestamp)
                {
                    Debug.Log("[CloudSync] Local save is up-to-date.");
                    return null;
                }

                Debug.Log($"[CloudSync] Cloud save is newer (cloud={cloudTs} > local={localTimestamp}).");
                return Convert.FromBase64String(dataItem.Value.GetAs<string>());
            }
            catch (CloudSaveException e) when (e.Reason == CloudSaveExceptionReason.NotFound)
            {
                Debug.Log("[CloudSync] No cloud save found yet.");
                return null;
            }
        }
    }
}
