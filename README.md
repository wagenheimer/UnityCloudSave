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

The package automatically pulls in its UGS dependencies:
- `com.unity.services.core`
- `com.unity.services.authentication`
- `com.unity.services.cloudsave`

### Pin to a specific version

```json
"com.wagenheimer.cloudsave": "https://github.com/wagenheimer/UnityCloudSave.git#3.0.0"
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

## How Conflict Resolution Works

Every save stores a `long LastSaved` timestamp (UTC ticks). On sync:

1. Cloud save timestamp is compared to local timestamp
2. If cloud is newer → `onCloudNewer` callback is invoked with the cloud bytes
3. If local is newer or equal → no change (local save is authoritative)

This is last-write-wins across devices using wall-clock time.

---

## Limits

| Limit | Value |
|---|---|
| Max key size | 255 characters |
| Max value size | 5 MB per key |
| Max keys per player | 300 |
| Requires internet | For first sync; fails silently offline |

---

## License

MIT — see [LICENSE](LICENSE).
