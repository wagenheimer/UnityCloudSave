# CloudSave Integration Guide

Complete guide to enable `com.wagenheimer.cloudsave` in your Unity project.

---

## Index

1. [Prerequisites](#1-prerequisites)
2. [Package Installation](#2-package-installation)
3. [Unity Dashboard — Cloud Save Enabled](#3-unity-dashboard--cloud-save-enabled)
4. [Project Linked to Unity Services](#4-project-linked-to-unity-services)
5. [Code — Minimal Setup](#5-code--minimal-setup)
6. [UIs (Recommended)](#6-uis-recommended)
7. [Auth Upgrade (Android/iOS)](#7-auth-upgrade-androidios)
8. [Localization](#8-localization)
9. [Testing Without UGS](#9-testing-without-ugs)
10. [Final Checklist](#10-final-checklist)
11. [Reusable AI Audit Prompt](#11-reusable-ai-audit-prompt)
12. [Logging — What to Expect in the Console](#12-logging--what-to-expect-in-the-console)

---

## 1. Prerequisites

- Unity **2021.3+**
- A project in the [Unity Dashboard](https://dashboard.unity3d.com/)
- **Optional (Android):** Google Play Games Plugin for Unity — `com.google.play.games`
- **Optional (iOS):** Apple.GameKit or a native bridge to GKLocalPlayer

---

## 2. Package Installation

### Via Package Manager
1. **Window → Package Manager**
2. **+ → Add package from git URL...**
3. Paste: `https://github.com/wagenheimer/UnityCloudSave.git`

### Via `manifest.json`
```json
{
  "dependencies": {
    "com.wagenheimer.cloudsave": "https://github.com/wagenheimer/UnityCloudSave.git"
  }
}
```

Dependencies resolve automatically:
- `com.unity.services.core` 1.12+
- `com.unity.services.authentication` 2.7+
- `com.unity.services.cloudsave` 3.0+
- `com.unity.textmeshpro` 3.0.6+

---

## 3. Unity Dashboard — Cloud Save Enabled

1. Access [dashboard.unity3d.com](https://dashboard.unity3d.com/)
2. Select your project
3. **Cloud Save** → **Enable**

---

## 4. Project Linked to Unity Services

1. In Unity: **Edit → Project Settings → Services**
2. Log in with your Unity account
3. Select the project (same as the Dashboard)
4. Verify that it appears as **Linked** with the correct project

> If it does not appear, go to **Window → Unity Gaming Services** and follow the link flow.

---

## 5. Code — Minimal Setup

Everything revolves around **two static classes**: `CloudSync` and `CloudAuth`.

### 5.1 — Your SaveData Needs a Timestamp

```csharp
[System.Serializable]
public class MySaveData
{
    public long LastSaved;  // ← REQUIRED — used to decide the newest version
    public int Coins;
    public int Stage;
    // ... your other fields
}
```

### 5.2 — Startup (Once, on First Loading Screen)

```csharp
using Wagenheimer.CloudSave;

public class GameManager : MonoBehaviour
{
    private MySaveData _saveData;

    void Start()
    {
        // 1. Configure the cloud key (once)
        CloudSync.Configure("my_game_save");

        // 2. Start sync (fire-and-forget)
        _ = CloudSync.InitAndSyncAsync(_saveData.LastSaved, ApplyCloudSave);

        // 3. (Optional) Create UIs
        CloudSaveUI.Create();
        SyncStatusUI.Create();
    }

    private void ApplyCloudSave(byte[] cloudBytes)
    {
        // Called when the cloud save is newer than the local one
        var json = System.Text.Encoding.UTF8.GetString(cloudBytes);
        _saveData = JsonUtility.FromJson<MySaveData>(json);
        // Apply to the game...
    }
}
```

### 5.3 — Save (Every Time You Save Locally)

```csharp
public void SaveGame()
{
    _saveData.LastSaved = System.DateTime.UtcNow.Ticks;
    string json = JsonUtility.ToJson(_saveData);
    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json);
    System.IO.File.WriteAllBytes(Application.persistentDataPath + "/save.json", bytes);

    // Send to cloud (fire-and-forget)
    _ = CloudSync.SaveAsync(bytes, _saveData.LastSaved);
}
```

### 5.4 — Load (Before Starting Sync)

```csharp
public void LoadGame()
{
    string path = Application.persistentDataPath + "/save.json";
    if (System.IO.File.Exists(path))
    {
        byte[] bytes = System.IO.File.ReadAllBytes(path);
        string json = System.Text.Encoding.UTF8.GetString(bytes);
        _saveData = JsonUtility.FromJson<MySaveData>(json);
    }
    else
    {
        _saveData = new MySaveData();
    }
}
```

### Complete Example (Startup)

```csharp
using UnityEngine;
using Wagenheimer.CloudSave;

public class GameManager : MonoBehaviour
{
    [SerializeField] private MySaveData _saveData = new();

    void Awake()
    {
        LoadLocal();
        CloudSync.Configure("my_game");
    }

    void Start()
    {
        CloudSaveUI.Create();
        SyncStatusUI.Create();
        _ = CloudSync.InitAndSyncAsync(_saveData.LastSaved, ApplyCloudSave);
    }

    void LoadLocal()
    {
        var path = Application.persistentDataPath + "/save.json";
        if (System.IO.File.Exists(path))
        {
            var json = System.IO.File.ReadAllText(path);
            _saveData = JsonUtility.FromJson<MySaveData>(json);
        }
    }

    void ApplyCloudSave(byte[] bytes)
    {
        var json = System.Text.Encoding.UTF8.GetString(bytes);
        _saveData = JsonUtility.FromJson<MySaveData>(json);
        Debug.Log("Cloud save applied!");
    }

    public void Save()
    {
        _saveData.LastSaved = System.DateTime.UtcNow.Ticks;
        var json = JsonUtility.ToJson(_saveData);
        System.IO.File.WriteAllText(Application.persistentDataPath + "/save.json", json);
        _ = CloudSync.SaveAsync(
            System.Text.Encoding.UTF8.GetBytes(json),
            _saveData.LastSaved);
    }
}
```

---

## 6. UIs (Recommended)

### CloudSaveUI

Shows loading overlay, toasts (synced/offline/error), and conflict dialogs.

```csharp
CloudSaveUI.Create();  // Idempotent — returns the singleton
```

### SyncStatusUI

Persistent indicator in the bottom right corner.

```csharp
SyncStatusUI.Create();  // Idempotent
```

States: `Synced` (green), `Syncing` (blue), `Offline` (yellow), `Error` (red).

### CloudAuthUI

Modal dialog for account linking (GPGS/Game Center).

```csharp
var auth = CloudAuthUI.Create();
auth.OnLinkRequested += async () =>
{
    var result = await CloudAuth.LinkGooglePlayGamesAsync(serverAuthCode);
    auth.SetLinkResult(result.IsSuccess);
};
auth.Show();
```

### Regenerating Prefabs (If Modifying Layout)

**Tools → Wagenheimer → Cloud Save → Setup UI Prefabs → All**

---

## 7. Auth Upgrade (Android/iOS)

> [!IMPORTANT]
> `CloudAuth.Link*` does NOT perform authentication with the platform.
> You must authenticate with GPGS / Game Center / Apple **first** and only then pass the tokens.

### 7.1 — Unity Dashboard: Configure Sign-In Methods

Before writing any auth code, you need to configure UGS Authentication to use Google Play Games / Apple as login providers.

**Step-by-step:**

1. Access [dashboard.unity3d.com](https://dashboard.unity3d.com/) → your project
2. **Authentication** (in the side menu) → **Sign-In Methods**
3. **Anonymous** — enabled by default ✔
4. **Google Play Games** — click to enable
   - **Web client ID** field: paste the OAuth 2.0 Web client ID from Google Play Console (explained in [Section 7.2](#72-google-cloud-console-creating-the-right-credential-type))
5. **Apple Game Center** — enable (iOS, no extra configuration needed on Dashboard)
6. **Apple** — enable (Sign in with Apple, iOS)
   - Fields: **Service ID** + **Redirect URL** — obtain from Apple Developer Portal

> [!WARNING]
> If you **do not enable** the provider in the Dashboard, the call `AuthenticationService.Instance.LinkWithGooglePlayGamesAsync(code)` will throw a "provider not configured" error. The package integration audit includes this verification.

### 7.2 — Google Cloud Console: Creating the Right Credential Type

> [!WARNING]
> The **Android** credential type does not work here. Android Client IDs do not have Client Secrets and cannot be used for server-side validation (UGS). You must create a **Web application** credential type.

Step-by-step in the Google Cloud Console:

1. Access [console.cloud.google.com](https://console.cloud.google.com/) → APIs & Services → **Credentials**
2. Click **Create Credentials** → **OAuth client ID**
3. Under **Application type**, select **Web application**
4. Provide a name (e.g., "Unity Play Games Auth Bridge")
5. **Authorized redirect URIs** — leave blank for now
6. Click **Create**
7. Copy the generated **Client ID** (and optionally the **Client Secret**)

This Web Client ID is what you will paste into the Unity Dashboard.

> **Why Web and not Android?** The `AuthenticationService.Instance.LinkWithGooglePlayGamesAsync()` method performs server-side validation (Unity backend → Google). The Android type is only for direct device-level authentication. The Web type contains the required keys for OAuth 2.0 server-to-server exchange.

### 7.3 — Unity Dashboard: Paste the Client ID

1. Access [dashboard.unity3d.com](https://dashboard.unity3d.com/) → your project
2. **Authentication** → **Sign-In Methods** → **Google Play Games**
3. Enable and paste the **Web client ID** (the one created in the previous step)

### 7.4 — Google Play Console (Optional, for Linked Apps)

If the app is already registered in Google Play:

1. Access [play.google.com/console](https://play.google.com/console/) → your app
2. **Play Games Services** → **Setup & Management** → **Configuration**
3. Link the Web credential created above
4. Add test accounts if the app is not published yet

### 7.5 — Unity: Install GPGS Plugin

```json
// Packages/manifest.json
{
  "dependencies": {
    "com.google.play.games": "https://github.com/playgameservices/play-games-plugin-for-unity.git"
  }
}
```

Or via **Window → Package Manager → + → Add package from git URL**.

### 7.6 — Apple Developer (iOS)

#### Game Center

1. [developer.apple.com](https://developer.apple.com/) → **Certificates, Identifiers & Profiles**
2. Select your app's **App ID**
3. Enable **Game Center** capability
4. **Save** and generate a new provisioning profile
5. On the **Unity Dashboard** → Authentication → Sign-In Methods → **Apple Game Center** — enable (no need to paste anything)

#### Sign in with Apple (Optional, if using `LinkAppleAsync`)

1. Under the same App ID, enable **Sign in with Apple**
2. Create a **Service ID** (identifier for login)
3. Configure **Redirect URL** (usually `https://{your-project}.unitygameservices.com`)
4. On the **Unity Dashboard** → Authentication → Sign-In Methods → **Apple**
   - Paste the **Service ID** and **Redirect URL**

### 7.7 — Android: Google Play Games (Code)

After configuring the steps above, the code is identical — but now it functions:

**3 mandatory steps:**

1. **Authenticate with GPGS** (`PlayGamesPlatform.Authenticate`)
2. **Request the server auth code** (`RequestServerSideAccess`)
3. **Pass the code to CloudAuth** (`LinkGooglePlayGamesAsync`)

```csharp
using GooglePlayGames;
using GooglePlayGames.BasicApi;

// STEP 1 — Authenticate with Google Play Games
PlayGamesPlatform.Instance.Authenticate(status =>
{
    if (status != SignInStatus.Success)
    {
        Debug.LogWarning("GPGS auth failed.");
        return;
    }

    // STEP 2 — Get the server auth code
    PlayGamesPlatform.Instance.RequestServerSideAccess(
        forceRefreshToken: false,
        serverAuthCode =>
        {
            if (string.IsNullOrEmpty(serverAuthCode))
            {
                Debug.LogWarning("Server auth code is null.");
                return;
            }

            // STEP 3 — Link to Unity Cloud Save (UGS)
            _ = CloudAuth.LinkGooglePlayGamesAsync(serverAuthCode);
        });
});
```

**Console logs to expect:**
```
[CloudAuth] Ready. PlayerId=xxx Provider=Anonymous       ← before linking
[CloudAuth] Linked: provider=GooglePlayGames PlayerId=xxx ← after linking
```

See README for a complete example utilizing `SignedInExisting`.

### 7.8 — iOS: Apple Game Center (Code)

**Prerequisite:** Install the official Apple.GameKit package (available in the Package Manager).

```csharp
using Apple.GameKit;

// STEP 1 — Authenticate with Game Center
var player = GKLocalPlayer.Local;

// STEP 2 — Retrieve signature items
var (pubKeyUrl, signature, salt, timestamp) =
    await player.FetchItemsForIdentityVerificationSignatureAsync();

// STEP 3 — Link to Unity Cloud Save (UGS)
var result = await CloudAuth.LinkAppleGameCenterAsync(
    publicKeyUrl : pubKeyUrl,
    signature    : Convert.ToBase64String(signature),
    salt         : Convert.ToBase64String(salt),
    timestamp    : timestamp,
    teamPlayerId : player.TeamPlayerId);

Debug.Log($"Link result: {result.Status}");
```

### 7.9 — iOS: Sign in with Apple (Code)

```csharp
// STEP 1 — Authenticate with Apple
var credential = await AppleAuthManager.LoginWithAppleId(...);

// STEP 2 — Link to UGS
var result = await CloudAuth.LinkAppleAsync(credential.IdentityToken);
```

### 7.10 — Auth Events

```csharp
CloudAuth.OnLinked += provider => Debug.Log($"Linked: {provider}");
CloudAuth.OnAccountSwitched += provider => {
    Debug.Log($"Account switched! New PlayerId: {CloudAuth.PlayerId}");
    _ = CloudSync.InitAndSyncAsync(_saveData.LastSaved, ApplyCloudSave);
};
```

---

## 8. Localization

```csharp
CloudSaveLocale.Translate = key => LocalizationManager.GetTermTranslation(key);
```

All strings use keys such as `"cloudsave.synced"`. If `Translate` is `null`, it defaults to English.

---

## 9. Testing Without UGS

**Tools → Wagenheimer → Cloud Save → Open Test Window**

Simulate cloud behaviors without internet or credentials:
- Create/destroy UIs
- Trigger toasts (Synced, Error, Offline...)
- Show conflict dialogs
- Simulate events: `OnSyncStarted`, `OnSyncCompleted`, `OnLinked`
- View all property states

---

## 10. Final Checklist

- [ ] Package `com.wagenheimer.cloudsave` installed
- [ ] Cloud Save enabled in Unity Dashboard
- [ ] Project linked in Services (Edit → Project Settings → Services)
- [ ] `CloudSync.Configure("key")` called at startup
- [ ] `CloudSync.InitAndSyncAsync(timestamp, callback)` called at startup
- [ ] `CloudSync.SaveAsync(bytes, timestamp)` called after each local save
- [ ] Save class has `long LastSaved` field
- [ ] `CloudSaveUI.Create()` called (if UI is desired)
- [ ] `SyncStatusUI.Create()` called (if indicator is desired)
- [ ] Sign-In Methods configured in the Dashboard (Authentication → Sign-In Methods)
- [ ] Google Play Console configured (OAuth 2.0 Web client ID) + GPGS plugin installed (if Android)
- [ ] Apple Developer: Game Center enabled in App ID + native bridge (if iOS)
- [ ] `CloudAuth.LinkGooglePlayGamesAsync()` / `LinkAppleGameCenterAsync()` called (if cross-device)
- [ ] Tested via **Test Window** (Tools → Wagenheimer → Cloud Save → Open Test Window)

---

## 11. Reusable AI Audit Prompt

Copy and paste this prompt into any AI to analyze the project and determine integration status:

```text
You are a Unity Cloud Save integration auditor.
Analyze the project files and output ONLY a markdown table.
Do NOT add explanations beyond the table.

## Checks

1. **Package installed**
   - Check if "com.wagenheimer.cloudsave" in Packages/manifest.json
   - OR package directory exists at Packages/com.wagenheimer.cloudsave/
   - OR any .cs file references "Wagenheimer.CloudSave"

2. **CloudSync.Configure()**
   - Search .cs files for "CloudSync.Configure"
   - If found, note the key used

3. **CloudSync.InitAndSyncAsync()**
   - Search .cs files for "CloudSync.InitAndSyncAsync"

4. **CloudSync.SaveAsync()**
   - Search .cs files for "CloudSync.SaveAsync"

5. **long LastSaved field**
   - Search .cs files for "long LastSaved" in [System.Serializable] classes

6. **[System.Serializable] class**
   - Search .cs files for a [System.Serializable] class for save data

7. **CloudSaveUI.Create()** — Search .cs for "CloudSaveUI.Create()"

8. **SyncStatusUI.Create()** — Search .cs for "SyncStatusUI.Create()"

9. **CloudAuthUI.Create()** — Search .cs for "CloudAuthUI.Create()"
   - Also check if "OnLinkRequested" is wired

10. **Auth upgrade (UGS link calls)**
    - Search .cs for "LinkGooglePlayGamesAsync", "LinkAppleGameCenterAsync", "LinkAppleAsync"
    - Note which platform(s) are configured

11. **Android GPGS plugin**
    - Check Packages/manifest.json for "com.google.play.games"
    - Search Assets/ for GooglePlayGames DLLs or .cs references
    - Search .cs for "PlayGamesPlatform" or "GooglePlayGames"

12. **iOS native bridge**
    - Search Assets/Plugins/iOS/ for .mm files containing "GameCenter" or "GKLocalPlayer"
    - Search .cs for "Apple.GameKit", "GKLocalPlayer", "FetchItemsForIdentityVerification"
    - Search for "LinkAppleGameCenterAsync" or "LinkAppleAsync"

13. **Unity Services**
    - Check ProjectSettings/ProjectSettings.asset for "CloudSave" or "Unity Gaming Services"

## Output format

| # | Item | Status | Files Found | Details/Action Needed |
|---|------|--------|-------------|----------------------|
| 1 | Package installed | ✅ / ❌ / ⚠️ | (paths) | (what to do) |
| ... | ... | ... | ... | ... |
```

---

## 12. Logging — What to Expect in the Console

The package logs information using `Debug.Log` / `Debug.LogWarning`. Look for console messages prefixed with `[CloudAuth]`, `[CloudSync]`, or `[CloudSave]`.

| Tag | Trigger Condition | Example |
|-----|-------------------|---------|
| `[CloudAuth] Ready` | UGS initialized + anonymous sign-in OK | `[CloudAuth] Ready. PlayerId=xxx Provider=Anonymous` |
| `[CloudAuth] Init failed` | No internet or project not linked | `[CloudAuth] Ready failed: ...` |
| `[CloudAuth] Linked` | Link with GPGS/GameCenter OK | `[CloudAuth] Linked: provider=GooglePlayGames PlayerId=xxx` |
| `[CloudAuth] SignedInExisting` | Credential already linked to another account | `[CloudAuth] SignedInExisting: provider=Apple` |
| `[CloudAuth] Link* failed` | Link rejected (invalid token, etc.) | `[CloudAuth] LinkGooglePlayGames failed: ...` |
| `[CloudSync] Saved to cloud` | SaveAsync uploaded data successfully | `[CloudSync] Saved to cloud.` |
| `[CloudSync] Save failed` | Upload failed | `[CloudSync] Save failed: ...` |
| `[CloudSync] No cloud save found` | InitAndSync — first save (no remote save exists) | `[CloudSync] No cloud save found yet.` |
| `[CloudSync] InitAndSync error` | Sync failed completely | `[CloudSync] InitAndSync error: ...` |
| (General Debug) | UIs, prefabs, etc. | `[CloudSave] Prefab generated at ...` |

If **no logs** containing `[CloudAuth]` or `[CloudSync]` appear, the package APIs are not being called. Run the **Audit** (Tools → Wagenheimer → Cloud Save → Audit Integration) to confirm.
