using System;
using System.Threading.Tasks;

namespace Wagenheimer.CloudSave
{
    /// <summary>
    /// Everything <see cref="CloudSaveController"/> needs to run the whole Cloud Save lifecycle for
    /// your game. Only three fields are required — <see cref="SaveKey"/>, <see cref="Serialize"/>
    /// and <see cref="Deserialize"/> — because only your game knows the shape of its save. Every
    /// other concern (sign-in, conflict resolution, the conflict timestamp, account switching,
    /// auto-save timing, legacy migration) has a working default and lives inside the controller.
    ///
    /// <code>
    /// var cloud = CloudSaveController.Create(new CloudSaveOptions
    /// {
    ///     SaveKey     = "my_game_save",
    ///     Serialize   = ()    =&gt; MySave.ToBytes(),
    ///     Deserialize = bytes =&gt; MySave.FromBytes(bytes),      // also persist locally here
    ///     OnCloudApplied = () =&gt; RefreshUi(),                  // optional
    /// });
    /// await cloud.StartAsync();          // sign in + pull cloud + (optional) migrate
    /// // ... after any local save:
    /// cloud.MarkDirty();                 // debounced upload
    /// // ... in OnApplicationPause(true) / OnApplicationQuit:
    /// await cloud.FlushAsync();
    /// </code>
    /// </summary>
    public sealed class CloudSaveOptions
    {
        // ── Required ────────────────────────────────────────────────────────

        /// <summary>Cloud Save slot name. Passed to <c>CloudSync.Configure</c>. Keep it stable for the life of the game.</summary>
        public string SaveKey;

        /// <summary>Returns the current game save serialized to bytes. Bring your own serializer / compression.</summary>
        public Func<byte[]> Serialize;

        /// <summary>
        /// Applies cloud bytes to the running game AND persists them to disk (so a crash right after
        /// doesn't lose the pulled save). Called on first sync, on account switch, and after migration.
        /// </summary>
        public Action<byte[]> Deserialize;

        // ── Optional: conflict timestamp mirroring ─────────────────────────
        // The controller owns the conflict timestamp (stored in PlayerPrefs, key "ucs_ts_<SaveKey>").
        // Provide these only if you also want the value in your own save data.

        /// <summary>If set, this is used as the local timestamp source instead of the controller's PlayerPrefs value.</summary>
        public Func<long> GetTimestamp;

        /// <summary>If set, the controller mirrors every timestamp it writes into your save via this callback.</summary>
        public Action<long> SetTimestamp;

        // ── Optional: hooks ───────────────────────────────────────────────

        /// <summary>Called after cloud data has been applied (first sync, account switch, migration). Refresh your UI here.</summary>
        public Action OnCloudApplied;

        /// <summary>Called after every sync attempt with its outcome. Handy for a status indicator / analytics.</summary>
        public Action<CloudSyncResult> OnSyncCompleted;

        /// <summary>
        /// Custom conflict resolution. Return <see cref="CloudConflictChoice.UseCloud"/> or
        /// <see cref="CloudConflictChoice.UseLocal"/>. When null (default) the cloud save wins if it is newer.
        /// </summary>
        public Func<CloudConflictData, Task<CloudConflictChoice>> ConflictResolver;

        // ── Optional: legacy migration (PlayFab / Firebase / custom) ───────

        /// <summary>
        /// Fetches the player's save from a previous backend (bytes + UTC-ticks timestamp). When set,
        /// <see cref="CloudSaveController.StartAsync"/> runs a one-time migration after the first sync.
        /// See <c>Samples~/PlayFabMigration</c>.
        /// </summary>
        public Func<Task<(byte[] data, long timestamp)>> FetchLegacySave;

        /// <summary>How to apply migrated legacy bytes locally. Defaults to <see cref="Deserialize"/>.</summary>
        public Action<byte[]> ApplyLegacySave;

        // ── Optional: conflict dialog summary ─────────────────────────────

        /// <summary>
        /// A short human-readable summary of a save blob ("Level 42 · 1200 coins · 3 days played").
        /// When set, the built-in conflict dialog shows this on each side instead of a bare timestamp.
        /// </summary>
        public Func<byte[], string> DescribeSave;

        // ── Optional: auto-save ──────────────────────────────────────────

        /// <summary>When true (default), <see cref="CloudSaveController.MarkDirty"/> debounces an upload. When false it uploads immediately.</summary>
        public bool AutoSave = true;

        /// <summary>Debounce window for <see cref="CloudSaveController.MarkDirty"/>. Default 2 seconds.</summary>
        public float AutoSaveDebounceSeconds = 2f;

        // ── Optional: reset-progress support ─────────────────────────────

        /// <summary>Clears the player's local progress (keeps identity). Required only if you call <see cref="CloudSaveController.ResetProgressAsync"/>.</summary>
        public Action OnClearLocalSave;

        /// <summary>Serializes a fresh/clean save to upload on reset, so other devices reset too. If null, the cloud slot is deleted instead.</summary>
        public Func<byte[]> SerializeCleanSave;

        internal void Validate()
        {
            if (string.IsNullOrEmpty(SaveKey))
                throw new ArgumentException("CloudSaveOptions.SaveKey is required.");
            if (Serialize == null)
                throw new ArgumentException("CloudSaveOptions.Serialize is required.");
            if (Deserialize == null)
                throw new ArgumentException("CloudSaveOptions.Deserialize is required.");
        }
    }
}
