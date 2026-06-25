// Google Play Games Services — Saved Games cloud save.
//
// REQUIREMENTS (Android only):
//   - Google Play Games plugin for Unity installed (com.google.play.games)
//   - Play Games Services configured in the Google Play Console
//   - "Saved Games" API enabled in Play Games Services settings
//
// USAGE:
//   1. Set GooglePlayCloudSaveService.SlotName once at startup (default: "game_save")
//   2. After GPGS sign-in, set Enabled = true
//   3. Call Save(bytes) whenever you save locally
//   4. Call Load(callback) after sign-in to get cloud bytes; apply if newer

#if UNITY_ANDROID
using GooglePlayGames;
using GooglePlayGames.BasicApi;
using GooglePlayGames.BasicApi.SavedGame;
#endif
using System;
using UnityEngine;

public static class GooglePlayCloudSaveService
{
    /// <summary>Name of the save slot in GPGS. Set once before first Save/Load.</summary>
    public static string SlotName { get; set; } = "game_save";

    /// <summary>Set to true after successful GPGS sign-in.</summary>
    public static bool Enabled { get; set; }

    /// <summary>
    /// Persists <paramref name="data"/> to the cloud asynchronously.
    /// No-op if <see cref="Enabled"/> is false or outside Android.
    /// </summary>
    public static void Save(byte[] data)
    {
#if UNITY_ANDROID
        if (!Enabled || data == null) return;

        OpenSlot((status, metadata) =>
        {
            if (status != SavedGameRequestStatus.Success)
            {
                Debug.LogWarning("[CloudSave/GPGS] Failed to open slot for write: " + status);
                return;
            }

            var update = new SavedGameMetadataUpdate.Builder()
                .WithUpdatedDescription("Saved " + DateTime.UtcNow.ToString("s"))
                .Build();

            PlayGamesPlatform.Instance.SavedGame.CommitUpdate(
                metadata, update, data,
                (commitStatus, _) =>
                {
                    if (commitStatus != SavedGameRequestStatus.Success)
                        Debug.LogWarning("[CloudSave/GPGS] Commit failed: " + commitStatus);
                });
        });
#endif
    }

    /// <summary>
    /// Loads cloud save bytes and delivers them via <paramref name="onLoaded"/>.
    /// Callback receives <c>null</c> on failure or when outside Android.
    /// </summary>
    public static void Load(Action<byte[]> onLoaded)
    {
#if UNITY_ANDROID
        if (!Enabled) { onLoaded?.Invoke(null); return; }

        OpenSlot((status, metadata) =>
        {
            if (status != SavedGameRequestStatus.Success)
            {
                Debug.LogWarning("[CloudSave/GPGS] Failed to open slot for read: " + status);
                onLoaded?.Invoke(null);
                return;
            }

            PlayGamesPlatform.Instance.SavedGame.ReadBinaryData(metadata, (readStatus, data) =>
            {
                if (readStatus == SavedGameRequestStatus.Success)
                    onLoaded?.Invoke(data);
                else
                {
                    Debug.LogWarning("[CloudSave/GPGS] Read failed: " + readStatus);
                    onLoaded?.Invoke(null);
                }
            });
        });
#else
        onLoaded?.Invoke(null);
#endif
    }

#if UNITY_ANDROID
    private static void OpenSlot(Action<SavedGameRequestStatus, ISavedGameMetadata> onOpened)
    {
        PlayGamesPlatform.Instance.SavedGame.OpenWithAutomaticConflictResolution(
            SlotName,
            DataSource.ReadCacheOrNetwork,
            ConflictResolutionStrategy.UseMostRecentlySaved,
            onOpened);
    }
#endif
}
