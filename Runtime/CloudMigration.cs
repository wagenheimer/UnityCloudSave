using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// Outcome of a legacy cloud save migration attempt.
    /// </summary>
    public enum CloudMigrationStatus
    {
        /// <summary>Legacy save was successfully imported, applied locally, and uploaded to UGS.</summary>
        Migrated,

        /// <summary>UGS already has a save that is newer or equal to the legacy save. No action taken.</summary>
        UgsAlreadyNewer,

        /// <summary>No legacy save data was found from the legacy provider.</summary>
        NoLegacyData,

        /// <summary>Migration failed due to an error (network, auth, or deserialization).</summary>
        Failed
    }

    /// <summary>
    /// Result details for a legacy migration operation.
    /// </summary>
    public class CloudMigrationResult
    {
        public CloudMigrationStatus Status { get; }
        public string Message { get; }
        public byte[] MigratedData { get; }
        public long Timestamp { get; }

        public CloudMigrationResult(CloudMigrationStatus status, string message = null, byte[] data = null, long timestamp = 0)
        {
            Status = status;
            Message = message ?? string.Empty;
            MigratedData = data;
            Timestamp = timestamp;
        }

        public static CloudMigrationResult Success(byte[] data, long timestamp) =>
            new(CloudMigrationStatus.Migrated, "Legacy save successfully migrated to UGS.", data, timestamp);

        public static CloudMigrationResult UgsNewer() =>
            new(CloudMigrationStatus.UgsAlreadyNewer, "UGS save is already newer or equal to legacy save.");

        public static CloudMigrationResult NoData(string reason = null) =>
            new(CloudMigrationStatus.NoLegacyData, reason ?? "No legacy save data was found.");

        public static CloudMigrationResult Fail(string error) =>
            new(CloudMigrationStatus.Failed, error);
    }

    /// <summary>
    /// Universal migration helper to import saves from legacy backends (PlayFab, Firebase,
    /// custom servers) into Unity Gaming Services (UGS) Cloud Save.
    ///
    /// Completely decoupled from third-party SDKs — the consumer game provides an async
    /// delegate that fetches the legacy bytes and timestamp.
    /// </summary>
    public static class CloudMigration
    {
        /// <summary>
        /// Attempts to migrate a save from a legacy provider into UGS Cloud Save.
        /// If the legacy save exists and is newer than current UGS data (or if UGS is empty),
        /// it invokes <paramref name="onApplyLegacyLocally"/>, uploads the save to UGS,
        /// and returns <see cref="CloudMigrationStatus.Migrated"/>.
        /// </summary>
        /// <param name="fetchLegacySaveAsync">Async callback that connects to legacy backend and returns raw bytes and UTC ticks timestamp.</param>
        /// <param name="onApplyLegacyLocally">Callback to apply the legacy bytes to the local game state before UGS upload.</param>
        public static async Task<CloudMigrationResult> TryMigrateAsync(
            Func<Task<(byte[] data, long timestamp)>> fetchLegacySaveAsync,
            Action<byte[]> onApplyLegacyLocally)
        {
            if (fetchLegacySaveAsync == null)
                return CloudMigrationResult.Fail("fetchLegacySaveAsync callback is null.");

            try
            {
                await CloudAuth.EnsureSignedInAsync();
                if (!CloudAuth.IsReady)
                    return CloudMigrationResult.Fail("CloudAuth not ready.");

                var (legacyData, legacyTimestamp) = await fetchLegacySaveAsync();
                if (legacyData == null || legacyData.Length == 0)
                    return CloudMigrationResult.NoData();

                // Check existing UGS cloud save
                var (ugsBytes, ugsTimestamp) = await CloudSync.LoadRawCloudDataAsync();
                if (ugsBytes != null && ugsTimestamp >= legacyTimestamp)
                {
                    Debug.Log("[CloudMigration] UGS cloud save is already newer or equal to legacy save. Migration skipped.");
                    return CloudMigrationResult.UgsNewer();
                }

                // Legacy save is newer or UGS has no save yet
                onApplyLegacyLocally?.Invoke(legacyData);

                long finalTs = legacyTimestamp > 0 ? legacyTimestamp : DateTime.UtcNow.Ticks;
                await CloudSync.SaveAsync(legacyData, finalTs);

                Debug.Log("[CloudMigration] Legacy save migrated to UGS successfully.");
                return CloudMigrationResult.Success(legacyData, finalTs);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CloudMigration] Migration failed: {ex.Message}");
                return CloudMigrationResult.Fail(ex.Message);
            }
        }
    }
}
