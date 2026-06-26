# Unity Cloud Save

Cross-platform cloud save for Unity games, backed by **Unity Cloud Save (UGS)**.  
Drop-in `byte[]` API — you handle serialization, this package handles the cloud transport and auth.

| Platform | Auth (default) | Auth (upgraded) | Cloud Backend |
|---|---|---|---|
| Android | Anonymous | Google Play Games | Unity Cloud Save |
| iOS | Anonymous | Apple Game Center | Unity Cloud Save |
| Any | Anonymous | — | Unity Cloud Save |

**Conflict resolution:** last-write-wins via a `long` UTC-ticks timestamp you store in your save data.

---

## Requirements

- Unity 2021.3+
- A [Unity Gaming Services](https://dashboard.unity3d.com/) project with **Cloud Save** enabled
- **Android upgrade (optional):** Google Play Games Plugin for Unity (OpenUPM `com.google.play.games`)
- **iOS upgrade (optional):** A native bridge for `GKLocalPlayer.generateIdentityVerificationSignature`  
  (e.g. [Apple.GameKit](https://github.com/Apple/unityplugins) or a custom `.mm` plugin)

---

## Installation

### Via Package Manager (Git URL)

1. Open **Window → Package Manager**
2. Click **+** → **Add package from git URL…**
3. Enter:
   ```
   https://github.com/wagenheimer/UnityCloudSave.git
   ```

### Via `manifest.json`

```json
{
  "dependencies": {
    "com.wagenheimer.cloudsave": "https://github.com/wagenheimer/UnityCloudSave.git"
  }
}
```

The package automatically pulls in its dependencies:
- `com.unity.services.core`
- `com.unity.services.authentication`
- `com.unity.services.cloudsave`
- `com.unity.textmeshpro`

### Pin to a specific version

```json
"com.wagenheimer.cloudsave": "https://github.com/wagenheimer/UnityCloudSave.git#4.1.0"
```

---

## Unity Dashboard Setup

1. Go to [dashboard.unity3d.com](https://dashboard.unity3d.com/) → your project
2. **Cloud Save** → Enable
3. In Unity: **Edit → Project Settings → Services** → link your project

---

## Quick Start

### 1 — Configure the save key (once, at startup)

```csharp
CloudSync.Configure("my_game_save");
```

### 2 — Init and sync on load

```csharp
// Fire-and-forget. Calls ApplyCloudSave only if the cloud save is newer.
_ = CloudSync.InitAndSyncAsync(SaveData.LastSaved, ApplyCloudSave);
```

### 3 — Save to the cloud

```csharp
// Call this every time you write a local save.
SaveData.LastSaved = DateTime.UtcNow.Ticks;
byte[] bytes = Encoding.UTF8.GetBytes(JsonUtility.ToJson(saveData));
File.WriteAllBytes(localPath, bytes);
_ = CloudSync.SaveAsync(bytes, SaveData.LastSaved);
```

### 4 — Apply cloud save callback

```csharp
private void ApplyCloudSave(byte[] cloudBytes)
{
    var cloudSave = JsonUtility.FromJson<SaveData>(Encoding.UTF8.GetString(cloudBytes));
    if (cloudSave == null) return;
    SaveData = cloudSave;
    // apply to game state...
}
```

> **Tip:** Add a `long LastSaved` field to your save data class and set it to
> `DateTime.UtcNow.Ticks` on every save. The package uses this to decide which version to keep.

---

## Auth: Anonymous (default)

On first run, the SDK creates an anonymous account tied to a device UUID stored locally.  
The player never sees a login screen. Saves roam between app reinstalls **on the same device**.

```
Same device, reinstalled  → same UUID → same cloud save ✓
Different device          → different UUID → no access to old save ✗  (until upgraded)
```

---

## Auth: Upgrade to Google Play Games (Android)

After GPGS sign-in, request a server auth code and call `LinkGooglePlayGamesAsync`.  
The anonymous account is **permanently upgraded** — the `PlayerId` stays the same.

```csharp
// Call after successful GPGS Authenticate()
PlayGamesPlatform.Instance.RequestServerSideAccess(
    forceRefreshToken: false,
    code =>
    {
        _ = UpgradeCloudAuthAsync(code);
    });

async Task UpgradeCloudAuthAsync(string serverAuthCode)
{
    var result = await CloudAuth.LinkGooglePlayGamesAsync(serverAuthCode);

    if (result.Status == CloudLinkStatus.Linked)
    {
        Debug.Log("Conta vinculada ao Google Play Games.");
    }
    else if (result.Status == CloudLinkStatus.SignedInExisting)
    {
        // Player had an existing account (e.g. reinstalled on new device).
        // The PlayerId has switched — re-sync to pull the existing cloud save.
        Debug.Log("Conta existente detectada — sincronizando save da nuvem.");
        await CloudSync.InitAndSyncAsync(SaveData.LastSaved, ApplyCloudSave);
    }
    else
    {
        Debug.LogWarning($"Link falhou: {result.Message}");
    }
}
```

After upgrading, the save is accessible from **any device** where the player signs in to GPGS.

---

## Auth: Upgrade to Apple Game Center (iOS)

Game Center auth uses Apple's identity verification signature — you need a native bridge to
call `GKLocalPlayer.generateIdentityVerificationSignature` from C#.

### Option A — Apple.GameKit Unity Plugin (recommended)

Install [Apple.GameKit](https://github.com/Apple/unityplugins) and use:

```csharp
var player = GKLocalPlayer.Local;
var (publicKeyUrl, signature, salt, timestamp) =
    await player.FetchItemsForIdentityVerificationSignatureAsync();

var result = await CloudAuth.LinkAppleGameCenterAsync(
    publicKeyUrl : publicKeyUrl,
    signature    : Convert.ToBase64String(signature),
    salt         : Convert.ToBase64String(salt),
    timestamp    : timestamp,
    teamPlayerId : player.TeamPlayerId);
```

### Option B — Custom native plugin (`.mm`)

Create `Assets/Plugins/iOS/GameCenterBridge.mm`:

```objc
extern "C" void GC_GetIdentitySignature(
    void (*callback)(const char* pubKeyUrl, const char* sig, const char* salt,
                     uint64_t ts, const char* teamId))
{
    GKLocalPlayer* lp = [GKLocalPlayer localPlayer];
    [lp generateIdentityVerificationSignatureWithCompletionHandler:
        ^(NSURL* pubKeyURL, NSData* signature, NSData* salt, uint64_t timestamp, NSError* error)
    {
        if (error) { callback("", "", "", 0, ""); return; }
        NSString* sig64  = [signature base64EncodedStringWithOptions:0];
        NSString* salt64 = [salt base64EncodedStringWithOptions:0];
        callback(pubKeyURL.absoluteString.UTF8String,
                 sig64.UTF8String, salt64.UTF8String,
                 timestamp, lp.teamPlayerID.UTF8String);
    }];
}
```

Then from C# (inside `#if UNITY_IOS`):

```csharp
[DllImport("__Internal")]
static extern void GC_GetIdentitySignature(
    Action<string, string, string, ulong, string> callback);

// Call after Social.localUser.Authenticate succeeds:
GC_GetIdentitySignature(async (pubKeyUrl, sig, salt, ts, teamId) =>
{
    var result = await CloudAuth.LinkAppleGameCenterAsync(pubKeyUrl, sig, salt, ts, teamId);
    if (result.Status == CloudLinkStatus.SignedInExisting)
        await CloudSync.InitAndSyncAsync(SaveData.LastSaved, ApplyCloudSave);
});
```

---

## Auth: Upgrade to Sign in with Apple (iOS)

For apps using [Sign in with Apple](https://github.com/lupidan/apple-signin-unity):

```csharp
var credential = await AppleAuthManager.LoginWithAppleId(...);
var result = await CloudAuth.LinkAppleAsync(credential.IdentityToken);

if (result.Status == CloudLinkStatus.SignedInExisting)
    await CloudSync.InitAndSyncAsync(SaveData.LastSaved, ApplyCloudSave);
```

---

## Auth State

```csharp
CloudAuth.IsReady          // true once UGS is initialized and signed in
CloudAuth.IsAnonymous      // true = not yet linked to a provider
CloudAuth.IsLinked         // true = linked to GPGS, Apple, or Game Center
CloudAuth.Provider         // CloudAuthProvider enum value
CloudAuth.PlayerId         // stable Unity player ID

CloudAuth.OnLinked += provider => Debug.Log($"Linked to {provider}");
```

---

## API Reference

### `CloudSync`

| Method / Property | Description |
|---|---|
| `Configure(slotName)` | Sets the cloud key prefix. Call once at startup. |
| `InitAndSyncAsync(localTs, onCloudNewer)` | Init auth + pulls cloud save if newer. Fire-and-forget safe. |
| `SaveAsync(bytes, timestamp)` | Uploads save. No-op when offline. Fire-and-forget safe. |
| `IsAvailable` | True once auth + init are complete. |
| `DataKey` | Current data key prefix used for cloud storage. |
| `LastResult` | `CloudSyncResult?` — result of the most recent sync, or null before first sync. |
| `OnSyncStarted` | `event Action` — fires when sync begins. |
| `OnSyncCompleted` | `event Action<CloudSyncResult>` — fires with the result. |
| `ConflictResolver` | `Func<CloudConflictData, Task<CloudConflictChoice>>` — override conflict UI. |

### `CloudSaveUI`

| Member | Description |
|---|---|
| `Create()` | Static factory — uses `Resources/CloudSaveUI.prefab` if it exists, otherwise auto-generates it (Editor) or builds procedurally (builds). Idempotent — returns the singleton instance on subsequent calls. |
| `Instance` | `CloudSaveUI` singleton instance (null before first `Create()`). |
| `_sortOrder` | Serialized `int` — canvas sorting order (default 200). Configurable in the Inspector. |

The conflict dialog auto-dismisses after **30 seconds** (chooses cloud), preventing a stuck sync if no button is clicked.

### `SyncStatusUI`

| Member | Description |
|---|---|
| `Create()` | Static factory — same pattern as CloudSaveUI. Idempotent. |
| `Instance` | `SyncStatusUI` singleton instance. |
| `Status` | `SyncStatus` — current status (readonly). Starts as `Offline`. |
| `SetStatus(SyncStatus)` | Set the current sync status manually. |
| `SetLastSync(DateTime)` | Set the displayed last-sync timestamp. |
| `SetVisible(bool)` | Show/hide the indicator. |
| `_sortOrder` | Serialized `int` — canvas sorting order (default 150). |

### `CloudAuthUI`

| Member | Description |
|---|---|
| `Create()` | Static factory — same pattern as CloudSaveUI. |
| `Show()` | Show the account linking dialog. |
| `Hide()` | Hide the dialog. |
| `OnLinkRequested` | `event Action` — fires when the player clicks the link button. Wire your platform auth here. |
| `OnDismissed` | `event Action` — fires when the dialog is hidden (close button or overlay click). |
| `SetLinkResult(bool)` | Call after auth completes to re-enable the button on failure. |
| `_sortOrder` | Serialized `int` — canvas sorting order (default 250). |

The overlay also responds to clicks — tapping outside the card dismisses the dialog.

### `CloudSaveLocale`

| Member | Description |
|---|---|
| `Translate` | `Func<string, string>` delegate. Assign to integrate a localization system. Null = English fallback. |
| All convenience methods | e.g. `Synced()`, `Error()`, `BtnKeepLocal()` — return localized strings. |

### `CloudAuth`

| Method / Property | Description |
|---|---|
| `EnsureSignedInAsync()` | Init UGS + anonymous sign-in. Idempotent. Called by `CloudSync` automatically. |
| `LinkGooglePlayGamesAsync(code)` | Android: link/sign-in via GPGS server auth code. |
| `LinkAppleAsync(idToken)` | iOS: link/sign-in via Sign in with Apple identity token. |
| `LinkAppleGameCenterAsync(...)` | iOS: link/sign-in via Game Center signature. |
| `IsReady` | True once initialized and signed in. |
| `IsAnonymous` | Signed in but no linked provider. |
| `IsLinked` | Linked to an external provider. |
| `Provider` | `CloudAuthProvider` enum. |
| `PlayerId` | Unity player ID (stable, preserved after linking). |
| `OnLinked` | `event Action<CloudAuthProvider>` fired on link success. |

### `CloudLinkResult`

| Status | Meaning |
|---|---|
| `Linked` | Account linked for the first time. PlayerId unchanged. |
| `SignedInExisting` | Credential was already linked to another account. PlayerId switched. Re-sync cloud save. |
| `AlreadyLinked` | This account is already linked to this provider. |
| `Failed` | Error. Check `result.Message`. |

---

## UI: CloudSaveUI (v4.0.0+)

The package ships a ready-to-use UI component that shows a loading overlay, toast notifications, and a conflict-resolution dialog.

### Quick start

```csharp
CloudSaveUI.Create();
```

That's it. The first time you call this in the Editor, it auto-generates
`Assets/Resources/CloudSaveUI.prefab` — open it in the Inspector to customize
colors, fonts, and layout. On subsequent calls, the prefab is used directly.

In standalone builds, the UI is built procedurally (no prefab required).

### Custom prefab

The auto-generated prefab at `Assets/Resources/CloudSaveUI.prefab` is yours to
modify. After customising, right-click the prefab root →
**Setup References from Children** to refresh the serialised field bindings.

To regenerate from scratch (e.g. after breaking the layout):

**Tools → Cloud Save → Setup UI Prefabs → CloudSaveUI**

All three UI prefabs can be regenerated at once via **Tools → Cloud Save → Setup UI Prefabs → All**.

### Serialized fields

| Field | Type | Purpose |
|---|---|---|
| `_sortOrder` | `int` | Canvas sorting order (default 200) |
| `_loadingRoot` | `GameObject` | Loading overlay panel |
| `_loadingText` | `TextMeshProUGUI` | Animated loading text |
| `_toastRoot` | `GameObject` | Toast notification bar |
| `_toastBg` | `Image` | Toast background color |
| `_toastText` | `TextMeshProUGUI` | Toast message text |
| `_conflictRoot` | `GameObject` | Conflict dialog panel |
| `_conflictTitle` | `TextMeshProUGUI` | Conflict dialog title |
| `_localInfoText` | `TextMeshProUGUI` | Local save timestamp |
| `_cloudInfoText` | `TextMeshProUGUI` | Cloud save timestamp |

If no prefab references are assigned, `BuildUI()` creates the UI hierarchy
procedurally at startup — zero setup required.

---

## UI: SyncStatusUI (v4.1.0+)

Persistent sync status indicator. Shows a small colored panel (bottom-right) with the current sync state and optional last-sync time.

### Quick start

```csharp
SyncStatusUI.Create();
```

### States

| State | Color | Description |
|---|---|---|
| `Synced` | Green | Last sync succeeded |
| `Syncing` | Blue | Sync in progress |
| `Offline` | Yellow | No connection |
| `Error` | Red | Sync failed |

The indicator automatically listens to `CloudSync.OnSyncStarted` and `CloudSync.OnSyncCompleted`. Call `SetStatus(SyncStatus)` for manual control.

Custom prefab at `Assets/Resources/SyncStatusUI.prefab` — regenerate via **Tools → Cloud Save → Setup UI Prefabs → SyncStatusUI**.

### Serialized fields

| Field | Type | Purpose |
|---|---|---|
| `_sortOrder` | `int` | Canvas sorting order (default 150) |
| `_root` | `GameObject` | Container (anchored bottom-right) |
| `_icon` | `Image` | Status color indicator |
| `_statusText` | `TextMeshProUGUI` | Status label (Synced/Syncing/Offline/Error) |
| `_lastSyncText` | `TextMeshProUGUI` | Last sync timestamp (hidden until first sync) |
| `_colorSynced` | `Color` | Green |
| `_colorSyncing` | `Color` | Blue |
| `_colorOffline` | `Color` | Yellow |
| `_colorError` | `Color` | Red |

---

## UI: CloudAuthUI (v4.1.0+)

Modal dialog for linking an anonymous account to a platform provider (Google Play Games on Android, Apple Game Center on iOS).

### Quick start

```csharp
var auth = CloudAuthUI.Create();
auth.OnLinkRequested += async () =>
{
    var result = await CloudAuth.LinkGooglePlayGamesAsync(serverAuthCode);
    auth.SetLinkResult(result.Status == CloudLinkStatus.Linked);
};
auth.Show();
```

### Serialized fields

| Field | Type | Purpose |
|---|---|---|
| `_sortOrder` | `int` | Canvas sorting order (default 250) |
| `_overlay` | `Image` | Semi-transparent background (click to dismiss) |
| `_cardRoot` | `GameObject` | Center card container |
| `_titleText` | `TextMeshProUGUI` | "Cloud Login" |
| `_descriptionText` | `TextMeshProUGUI` | Explanatory text |
| `_statusText` | `TextMeshProUGUI` | "Account: Anonymous" / "Account: {provider}" |
| `_providerIcon` | `Image` | Platform logo placeholder |
| `_linkButton` | `Button` | Sign in button |
| `_linkButtonText` | `TextMeshProUGUI` | Button label |
| `_closeButton` | `Button` | Close / "Not now" |
| `_closeButtonText` | `TextMeshProUGUI` | Close label |

Custom prefab at `Assets/Resources/CloudAuthUI.prefab` — regenerate via **Tools → Cloud Save → Setup UI Prefabs → CloudAuthUI**.

---

## Localization (v4.1.0+)

All UI text uses string keys with built-in English fallback. To integrate a localization system (e.g. I2 Localization), assign `CloudSaveLocale.Translate`:

```csharp
// I2 Localization example
CloudSaveLocale.Translate = key => LocalizationManager.GetTermTranslation(key);
```

### String Keys

| Key | Fallback (EN) |
|---|---|
| `cloudsave.loading` | Syncing save |
| `cloudsave.synced` | Cloud save applied |
| `cloudsave.local_newer` | Local save is up to date |
| `cloudsave.local_kept` | Local save kept |
| `cloudsave.offline` | No connection — local save |
| `cloudsave.error` | Failed to sync save |
| `cloudsave.conflict_title_cloud` | Cloud save is newer |
| `cloudsave.conflict_title_account` | Save from another account found |
| `cloudsave.conflict_choose` | Choose which save to use: |
| `cloudsave.conflict_local` | Local Save |
| `cloudsave.conflict_cloud` | Cloud Save |
| `cloudsave.conflict_none` | No save |
| `cloudsave.btn_keep_local` | Keep Local |
| `cloudsave.btn_use_cloud` | Use Cloud |
| `cloudsave.sync_status_synced` | Synced |
| `cloudsave.sync_status_syncing` | Syncing... |
| `cloudsave.sync_status_offline` | Offline |
| `cloudsave.sync_status_error` | Sync error |
| `cloudsave.sync_last` | Last sync: {0} |
| `cloudsave.auth_title` | Cloud Login |
| `cloudsave.auth_description` | Link your account to access your save on other devices. |
| `cloudsave.auth_status_anonymous` | Account: Anonymous |
| `cloudsave.auth_status_linked` | Account: {0} |
| `cloudsave.auth_btn_google` | Sign in with Google Play Games |
| `cloudsave.auth_btn_apple` | Sign in with Apple Game Center |
| `cloudsave.auth_btn_close` | Not now |

When `CloudSaveLocale.Translate` is `null` (default), all strings fall back to the built-in English translations.

---

## How Conflict Resolution Works

Every save stores a `long LastSaved` timestamp (UTC ticks). On sync:

1. Cloud save timestamp is compared to local timestamp
2. If cloud is newer → `onCloudNewer` callback is invoked with the cloud bytes
3. If local is newer or equal → no change (local save is authoritative)

This is last-write-wins across devices using wall-clock time.

### Custom conflict resolver

```csharp
CloudSync.ConflictResolver = async data =>
{
    // Show your own UI, then return the player's choice
    return await ShowMyConflictDialog(data.LocalTimestamp, data.CloudTimestamp);
};
```

When `ConflictResolver` is `null` (default), cloud always wins.

---

---

## Testing in the Editor (v4.1.1+)

The package ships a **Test Window** that lets you simulate sync operations, fire events, and visualise all UI components without needing actual Unity Gaming Services credentials or a device build.

### Open the Test Window

**Tools → Cloud Save → Test Window**

### What you can test

| Section | Actions |
|---|---|
| **Scene Setup** | Create all UIs at once / Destroy all UIs |
| **CloudSaveUI** | Show/hide loading overlay, trigger toast notifications (Synced / LocalNewer / Offline / Error), show the conflict resolution dialog |
| **SyncStatusUI** | Set status to Synced / Syncing / Offline / Error manually |
| **CloudAuthUI** | Create and show the account-linking modal, hide it |
| **Event Simulation** | Fire `CloudSync.OnSyncStarted` and `OnSyncCompleted` (any result), fire `CloudAuth.OnLinked` and `OnAccountSwitched` |
| **State panel** | Read-only view of all package state: singleton instances, sync status, auth readiness, last result |

### Typical test flow

```text
1. Click [Create All UIs]     → CloudSaveUI + SyncStatusUI appear on screen
2. Click [Fire OnSyncStarted]  → loading overlay + SyncStatusUI(Syncing)
3. Click [Completed: CloudApplied] → toast + SyncStatusUI(Synced)
4. Click [Fire OnLinked]      → toast "Account linked"
5. Click [Show Conflict Dialog] → conflict UI with mock timestamps
6. Click [Keep Local] or [Use Cloud] → dialog closes
```

The test window calls the same internal methods and events that the runtime uses, so the behaviour is identical to a real sync flow. No UGS project or internet connection required.

---

| Limit | Value |
|---|---|
| Max key size | 255 characters |
| Max value size | 5 MB per key |
| Max keys per player | 300 |
| Requires internet | For first sync; fails silently offline |

---

## License

MIT — see [LICENSE](LICENSE).
