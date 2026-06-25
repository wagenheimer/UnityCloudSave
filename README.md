# Unity Cloud Save

Automatic cloud save for Unity games. Drop-in `byte[]` API — you handle serialization, this package handles the cloud transport.

| Platform | Backend | Conflict Resolution |
|---|---|---|
| Android | Google Play Games Services — Saved Games | Most Recently Saved |
| iOS | iCloud Key-Value Store | Automatic (OS-managed) |

**Includes** an iOS `PostProcessBuild` script that adds the iCloud capability to Xcode automatically — no manual Xcode steps needed.

---

## Requirements

- Unity 2021.3+
- **Android:** [Google Play Games Plugin for Unity](https://github.com/playgameservices/play-games-plugin-for-unity) installed separately
- **iOS:** An Apple Developer account with iCloud enabled for your App ID

---

## Installation

### Via Package Manager (Git URL)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL…**
3. Enter:
   ```
   https://github.com/wagenheimer/unity-cloud-save.git
   ```

### Via `manifest.json`

Add to `Packages/manifest.json`:
```json
{
  "dependencies": {
    "com.wagenheimer.cloudsave": "https://github.com/wagenheimer/unity-cloud-save.git"
  }
}
```

### Pin to a specific version

```json
"com.wagenheimer.cloudsave": "https://github.com/wagenheimer/unity-cloud-save.git#1.0.0"
```

---

## Quick Start

### 1 — Configure the save key (once, at startup)

```csharp
// Use the same key on Android and iOS so saves roam between platforms.
GooglePlayCloudSaveService.SlotName = "my_game_save";
iCloudSaveService.SaveKey           = "my_game_save";
```

### 2 — Trigger an early iCloud sync (iOS)

Call this as early as possible so the OS has time to pull fresh data from
iCloud before you load.

```csharp
iCloudSaveService.RequestSync();
```

### 3 — Save to the cloud

Call this every time you write a local save file.

```csharp
byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(saveData));
File.WriteAllBytes(localPath, bytes);

// Cloud — fire and forget, both are no-ops on other platforms
GooglePlayCloudSaveService.Save(bytes);
iCloudSaveService.Save(bytes);
```

### 4 — Load from the cloud

#### Android (async callback)

```csharp
// Call after successful GPGS sign-in
GooglePlayCloudSaveService.Enabled = true;

GooglePlayCloudSaveService.Load(cloudBytes =>
{
    if (cloudBytes == null) return;

    var cloudSave = JsonUtility.FromJson<SaveData>(Encoding.UTF8.GetString(cloudBytes));

    // Apply only if the cloud save is newer than the local one
    if (cloudSave.LastSaved > localSave.LastSaved)
    {
        localSave = cloudSave;
        ApplySaveData();
    }
});
```

#### iOS (synchronous)

```csharp
// Call after Game Center auth (or independently — iCloud doesn't need GC)
byte[] cloudBytes = iCloudSaveService.Load();

if (cloudBytes != null)
{
    var cloudSave = JsonUtility.FromJson<SaveData>(Encoding.UTF8.GetString(cloudBytes));

    if (cloudSave.LastSaved > localSave.LastSaved)
    {
        localSave = cloudSave;
        ApplySaveData();
    }
}
```

> **Tip:** Add a `long LastSaved` field to your save data class and set it to
> `DateTime.UtcNow.Ticks` on every save. Use that to decide which version to keep.

---

## Android Setup

### 1 — Install Google Play Games Plugin

Download and import from:  
https://github.com/playgameservices/play-games-plugin-for-unity/releases

### 2 — Enable Saved Games in Play Console

1. Google Play Console → your app → **Play Games Services** → **Configuration**
2. Scroll to **Saved Games** → enable it
3. Save changes

### 3 — Sign in and set Enabled

```csharp
PlayGamesPlatform.Activate();
PlayGamesPlatform.Instance.Authenticate(status =>
{
    if (status == SignInStatus.Success)
        GooglePlayCloudSaveService.Enabled = true;
});
```

---

## iOS Setup

### Automatic (recommended)

The included `iOSPostBuildProcessor` script runs automatically after every iOS
build and adds the iCloud Key-Value Storage capability to the Xcode project.
No Xcode changes needed.

### Manual (if you skip PostBuild)

1. Xcode → your target → **Signing & Capabilities**
2. **+ Capability** → **iCloud**
3. Check **Key-value storage**

### Apple Developer Portal (one-time)

1. [developer.apple.com](https://developer.apple.com) → **Certificates, Identifiers & Profiles**
2. **Identifiers** → your App ID → **Edit**
3. Enable **iCloud** → Save

---

## API Reference

### `GooglePlayCloudSaveService` (Android)

| Member | Type | Description |
|---|---|---|
| `SlotName` | `string` | GPGS save slot name. Default: `"game_save"` |
| `Enabled` | `bool` | Set `true` after successful sign-in |
| `Save(byte[])` | `void` | Async upload. No-op if not enabled |
| `Load(Action<byte[]>)` | `void` | Async download via callback |

### `iCloudSaveService` (iOS)

| Member | Type | Description |
|---|---|---|
| `SaveKey` | `string` | iCloud KV store key. Default: `"game_save"` |
| `RequestSync()` | `void` | Triggers background iCloud pull. Call at startup |
| `Save(byte[])` | `void` | Writes to local KV cache + requests sync |
| `Load()` | `byte[]` | Reads from local KV cache (synchronous) |

All methods are **no-ops** on platforms they don't support, so you can call
them unconditionally without `#if` guards.

---

## How Conflict Resolution Works

| Platform | Strategy |
|---|---|
| Android | `UseMostRecentlySaved` — GPGS picks the slot written most recently |
| iOS | OS-managed — iCloud merges automatically; last-write-wins per key |

For your own logic, the recommended pattern is a `LastSaved` timestamp in your
save data (see Quick Start above).

---

## Limitations

| Limit | Android | iOS |
|---|---|---|
| Max save size | 3 MB per slot | 1 MB per key (1 MB total per app) |
| Requires internet | Yes (for first sync) | No (reads from local cache) |
| Requires auth | Yes (GPGS sign-in) | No (iCloud account on device) |

---

## License

MIT — see [LICENSE](LICENSE).
