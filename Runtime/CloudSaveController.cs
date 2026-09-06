using System;
using System.Threading.Tasks;
using UnityEngine;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// The drop-in high-level layer over <see cref="CloudSync"/> / <see cref="CloudAuth"/> /
    /// <see cref="CloudMigration"/>. It runs the entire Cloud Save lifecycle so your game only
    /// supplies what is genuinely game-specific (serialize / deserialize your save).
    ///
    /// What the controller does for you:
    /// <list type="bullet">
    ///   <item>Configure the slot, initialise Unity Services, sign in anonymously.</item>
    ///   <item>Pull the cloud save on start and resolve conflicts (cloud-wins by default).</item>
    ///   <item>Own the conflict timestamp (PlayerPrefs, key <c>ucs_ts_&lt;SaveKey&gt;</c>) — no field needed in your save.</item>
    ///   <item>Debounced auto-upload on <see cref="MarkDirty"/>; immediate <see cref="FlushAsync"/> for pause/quit.</item>
    ///   <item>Re-sync automatically when the player signs into a different linked account.</item>
    ///   <item>Run a one-time legacy migration (PlayFab / Firebase / custom) if you provide a fetch delegate.</item>
    ///   <item>Pass-throughs for account linking, reset-progress and account deletion.</item>
    /// </list>
    ///
    /// It is a plain object — create it, hold the reference, and <see cref="Dispose"/> it if your
    /// game tears the session down. See <see cref="CloudSaveOptions"/> for the full quick-start.
    /// </summary>
    public sealed class CloudSaveController : IDisposable
    {
        readonly CloudSaveOptions _o;
        int _saveGeneration;
        bool _started;

        string TimestampPrefKey => "ucs_ts_" + _o.SaveKey;

        CloudSaveController(CloudSaveOptions options) => _o = options;

        /// <summary>Validates the options and returns a controller. Does not touch the network yet — call <see cref="StartAsync"/>.</summary>
        public static CloudSaveController Create(CloudSaveOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            options.Validate();
            return new CloudSaveController(options);
        }

        // ── State ──────────────────────────────────────────────────────────

        /// <summary>True once <see cref="StartAsync"/> has completed.</summary>
        public bool IsStarted => _started;

        /// <summary>The Unity player id (stable across anonymous sign-ins and after linking).</summary>
        public string PlayerId => CloudAuth.PlayerId;

        /// <summary>The active auth provider.</summary>
        public CloudAuthProvider Provider => CloudAuth.Provider;

        /// <summary>True when the account has been linked to a provider (cross-device).</summary>
        public bool IsLinked => CloudAuth.IsLinked;

        /// <summary>Outcome of the most recent sync, or null before the first one.</summary>
        public CloudSyncResult? LastSync => CloudSync.LastResult;

        // ── Lifecycle ──────────────────────────────────────────────────────

        /// <summary>
        /// Configures the slot, signs in, pulls the cloud save, and (if <see cref="CloudSaveOptions.FetchLegacySave"/>
        /// is set) runs a one-time migration. Idempotent. Safe to <c>await</c> or fire-and-forget.
        /// </summary>
        public async Task StartAsync()
        {
            if (_started) return;

            CloudSync.Configure(_o.SaveKey);
            CloudSync.ConflictResolver = _o.ConflictResolver;
            if (_o.OnSyncCompleted != null) CloudSync.OnSyncCompleted += _o.OnSyncCompleted;
            CloudAuth.OnAccountSwitched += HandleAccountSwitched;

            await CloudAuth.EnsureSignedInAsync();
            await SyncAsync(CloudConflictReason.CloudIsNewer);

            if (_o.FetchLegacySave != null)
                await TryMigrateAsync();

            _started = true;
        }

        async Task SyncAsync(CloudConflictReason reason)
        {
            await CloudSync.InitAndSyncAsync(ReadLocalTimestamp(), ApplyCloudBytes, reason);

            // Anchor our local timestamp to the cloud's so the next comparison is correct.
            try
            {
                var (cloudBytes, cloudTs) = await CloudSync.LoadRawCloudDataAsync();
                if (cloudBytes != null && cloudTs > ReadLocalTimestamp())
                    WriteLocalTimestamp(cloudTs);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSaveController] timestamp anchor skipped: {e.Message}");
            }
        }

        void ApplyCloudBytes(byte[] bytes)
        {
            try { _o.Deserialize(bytes); }
            catch (Exception e) { Debug.LogWarning($"[CloudSaveController] Deserialize threw: {e.Message}"); return; }
            _o.OnCloudApplied?.Invoke();
        }

        async void HandleAccountSwitched(CloudAuthProvider provider)
        {
            Debug.Log($"[CloudSaveController] account switched to {provider} — re-syncing.");
            await SyncAsync(CloudConflictReason.AccountSwitched);
        }

        // ── Saving ─────────────────────────────────────────────────────────

        /// <summary>
        /// Call after every local save. With <see cref="CloudSaveOptions.AutoSave"/> on (default) the
        /// upload is debounced by <see cref="CloudSaveOptions.AutoSaveDebounceSeconds"/>; repeated
        /// calls collapse into one upload.
        /// </summary>
        public void MarkDirty()
        {
            if (!_o.AutoSave) { _ = SaveNowAsync(); return; }
            int generation = ++_saveGeneration;
            _ = DebouncedSaveAsync(generation);
        }

        async Task DebouncedSaveAsync(int generation)
        {
            int ms = Mathf.Max(0, Mathf.RoundToInt(_o.AutoSaveDebounceSeconds * 1000f));
            if (ms > 0) await Task.Delay(ms);
            if (generation != _saveGeneration) return; // superseded by a newer MarkDirty
            await SaveNowAsync();
        }

        /// <summary>Serializes and uploads the current save immediately, bypassing the debounce.</summary>
        public async Task SaveNowAsync()
        {
            long ts = _o.GetTimestamp?.Invoke() ?? DateTime.UtcNow.Ticks;
            WriteLocalTimestamp(ts);

            byte[] bytes;
            try { bytes = _o.Serialize(); }
            catch (Exception e) { Debug.LogWarning($"[CloudSaveController] Serialize threw: {e.Message}"); return; }
            if (bytes == null || bytes.Length == 0) { Debug.LogWarning("[CloudSaveController] Serialize returned no bytes."); return; }

            await CloudSync.SaveAsync(bytes, ts);
        }

        /// <summary>
        /// Cancels any pending debounced save and uploads now. Call from
        /// <c>OnApplicationPause(true)</c> and <c>OnApplicationQuit</c>.
        /// </summary>
        public Task FlushAsync()
        {
            _saveGeneration++;
            return SaveNowAsync();
        }

        // ── Account operations (pass-throughs) ────────────────────────────

        /// <summary>Wipes progress locally + on the cloud, keeping the account/identity. Requires <see cref="CloudSaveOptions.OnClearLocalSave"/>.</summary>
        public Task<bool> ResetProgressAsync()
        {
            if (_o.OnClearLocalSave == null)
                throw new InvalidOperationException("Set CloudSaveOptions.OnClearLocalSave to use ResetProgressAsync().");
            return CloudSync.ResetProgressAsync(_o.OnClearLocalSave, _o.SerializeCleanSave);
        }

        /// <summary>Deletes the account from UGS Authentication (GDPR / Apple 5.1.1(v) / Google Play).</summary>
        public Task<bool> DeleteAccountAsync() => CloudAuth.DeleteAccountAsync();

        public Task<CloudLinkResult> LinkGooglePlayGamesAsync(string serverAuthCode) => CloudAuth.LinkGooglePlayGamesAsync(serverAuthCode);
        public Task<CloudLinkResult> LinkGoogleAsync(string idToken) => CloudAuth.LinkGoogleAsync(idToken);
        public Task<CloudLinkResult> LinkAppleAsync(string identityToken) => CloudAuth.LinkAppleAsync(identityToken);
        public Task<CloudLinkResult> LinkAppleGameCenterAsync(string publicKeyUrl, string signature, string salt, ulong timestamp, string teamPlayerId)
            => CloudAuth.LinkAppleGameCenterAsync(publicKeyUrl, signature, salt, timestamp, teamPlayerId);
        public Task<CloudLinkResult> LinkFacebookAsync(string accessToken) => CloudAuth.LinkFacebookAsync(accessToken);

        // ── Migration ─────────────────────────────────────────────────────

        async Task TryMigrateAsync()
        {
            try
            {
                var result = await CloudMigration.TryMigrateAsync(_o.FetchLegacySave, _o.ApplyLegacySave ?? _o.Deserialize);
                if (result.Status == CloudMigrationStatus.Migrated)
                {
                    WriteLocalTimestamp(result.Timestamp > 0 ? result.Timestamp : DateTime.UtcNow.Ticks);
                    _o.OnCloudApplied?.Invoke();
                    Debug.Log("[CloudSaveController] legacy save migrated.");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CloudSaveController] migration failed: {e.Message}");
            }
        }

        // ── Timestamp ─────────────────────────────────────────────────────

        long ReadLocalTimestamp()
        {
            if (_o.GetTimestamp != null) return _o.GetTimestamp();
            return long.TryParse(PlayerPrefs.GetString(TimestampPrefKey, "0"), out var t) ? t : 0L;
        }

        void WriteLocalTimestamp(long ts)
        {
            PlayerPrefs.SetString(TimestampPrefKey, ts.ToString());
            PlayerPrefs.Save();
            _o.SetTimestamp?.Invoke(ts);
        }

        // ── Teardown ──────────────────────────────────────────────────────

        public void Dispose()
        {
            if (_o.OnSyncCompleted != null) CloudSync.OnSyncCompleted -= _o.OnSyncCompleted;
            CloudAuth.OnAccountSwitched -= HandleAccountSwitched;
        }
    }
}
