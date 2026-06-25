// iCloud Key-Value Store cloud save for iOS.
//
// REQUIREMENTS:
//   - iCloud capability with "Key-value storage" enabled in Xcode
//     (the included iOSPostBuildProcessor does this automatically on build)
//   - App ID must have iCloud enabled in the Apple Developer portal
//
// USAGE:
//   1. Optionally set iCloudSaveService.SaveKey (default: "game_save")
//   2. Call RequestSync() early at app startup (triggers background iCloud pull)
//   3. Call Save(bytes) whenever you save locally
//   4. Call Load() after a moment to get the (possibly fresher) cloud bytes

#if UNITY_IOS
using System.Runtime.InteropServices;
#endif
using System;
using System.Text;
using UnityEngine;

public static class iCloudSaveService
{
    /// <summary>iCloud KV key used to store the save. Set once before first Save/Load.</summary>
    public static string SaveKey { get; set; } = "game_save";

#if UNITY_IOS
    [DllImport("__Internal")] private static extern void   _iCloudKVSave(string key, string value);
    [DllImport("__Internal")] private static extern string _iCloudKVLoad(string key);
    [DllImport("__Internal")] private static extern void   _iCloudKVSync();
#endif

    /// <summary>
    /// Requests a background sync from iCloud. Call early at app startup.
    /// No-op outside iOS.
    /// </summary>
    public static void RequestSync()
    {
#if UNITY_IOS
        _iCloudKVSync();
#endif
    }

    /// <summary>
    /// Persists <paramref name="data"/> to iCloud KV store. No-op outside iOS.
    /// </summary>
    public static void Save(byte[] data)
    {
#if UNITY_IOS
        if (data == null || data.Length == 0) return;
        try
        {
            _iCloudKVSave(SaveKey, Encoding.UTF8.GetString(data));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CloudSave/iCloud] Save error: " + e.Message);
        }
#endif
    }

    /// <summary>
    /// Returns cached cloud bytes, or <c>null</c> if absent or outside iOS.
    /// Synchronous — reads from the local iCloud cache.
    /// </summary>
    public static byte[] Load()
    {
#if UNITY_IOS
        try
        {
            string value = _iCloudKVLoad(SaveKey);
            if (string.IsNullOrEmpty(value)) return null;
            return Encoding.UTF8.GetBytes(value);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[CloudSave/iCloud] Load error: " + e.Message);
            return null;
        }
#else
        return null;
#endif
    }
}
