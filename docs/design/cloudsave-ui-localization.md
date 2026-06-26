# CloudSave UI - Localization & New UIs

## Status

- CloudSaveUI (loading + toast + conflict) — exists, needs localization
- SyncStatusUI — new, persistent sync status indicator
- AuthLinkUI — new, account linking dialog

## Localization Architecture

```csharp
public static class CloudSaveLocale
{
    public static Func<string, string> Translate { get; set; }
}
```

- `Translate` is a delegate assigned by the host project (e.g., to I2 Localization)
- Package uses string keys like `"cloudsave.synced"` instead of hardcoded text
- When `Translate` is null: fallback to built-in English strings
- No dependency on any localization package

### String Keys

| Key | Fallback (EN) |
|---|---|
| `cloudsave.loading` | Syncing save... |
| `cloudsave.synced` | Cloud save applied |
| `cloudsave.local_newer` | Local save is up to date |
| `cloudsave.local_kept` | Local save kept |
| `cloudsave.offline` | No connection — local save |
| `cloudsave.error` | Failed to sync save |
| `cloudsave.account_linked` | Account linked: {0} |
| `cloudsave.account_switched` | Account recovered — syncing... |
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
| `cloudsave.auth_btn_signin_apple` | Sign in with Apple |
| `cloudsave.auth_btn_close` | Not now |

## SyncStatusUI

Persistent small indicator in screen corner.

### States

| Enum | Icon | Text key |
|---|---|---|
| `Synced` | Green check | cloudsave.sync_status_synced |
| `Syncing` | Spinning | cloudsave.sync_status_syncing |
| `Offline` | Yellow warning | cloudsave.sync_status_offline |
| `Error` | Red cross | cloudsave.sync_status_error |

### Serialized Fields

| Field | Type | Purpose |
|---|---|---|
| `_root` | GameObject | Container (ancored bottom-right) |
| `_icon` | Image | Status icon |
| `_statusText` | TextMeshProUGUI | Status label |
| `_lastSyncText` | TextMeshProUGUI | "Last sync: HH:mm" (optional, hidden) |
| `_colorSynced` | Color | Green |
| `_colorSyncing` | Color | Blue |
| `_colorOffline` | Color | Yellow |
| `_colorError` | Color | Red |

### Lifecycle

- `SyncStatusUI.Create()` — factory (prefab or procedural)
- Listens to `CloudSync.OnSyncStarted`, `OnSyncCompleted`
- Shows `Syncing` during sync, then resolves to Synced/Offline/Error
- `SetStatus(SyncStatus)` public method for manual control

### Creation Flow

Same as CloudSaveUI:
1. `Resources.Load("SyncStatusUI")` — uses existing prefab if found
2. Editor fallback: auto-generates `Assets/Resources/SyncStatusUI.prefab`
3. Build fallback: procedural construction

## AuthLinkUI

Modal dialog for linking anonymous account to a platform provider.

### Serialized Fields

| Field | Type | Purpose |
|---|---|---|
| `_overlay` | Image | Semi-transparent background |
| `_cardRoot` | GameObject | Center card container |
| `_titleText` | TextMeshProUGUI | "Cloud Login" |
| `_descriptionText` | TextMeshProUGUI | Explanatory text |
| `_statusText` | TextMeshProUGUI | "Account: Anonymous" |
| `_providerIcon` | Image | Platform logo |
| `_linkButton` | Button | Sign in button |
| `_linkButtonText` | TextMeshProUGUI | Button label |
| `_closeButton` | Button | Close / "Not now" |
| `_closeButtonText` | TextMeshProUGUI | Close label |

### Lifecycle

- `CloudAuthUI.Create()` — factory
- Shows modal dialog
- On link button click: calls platform-specific auth (GPGS / Game Center)
- On success: updates status text, disables button
- On close: hides dialog

### Platform Detection

```csharp
#if UNITY_ANDROID
    // Google Play Games button
#elif UNITY_IOS
    // Apple Game Center button
#endif
```

Shows only the relevant button for the current platform.

## File Structure in Package

```
Runtime/
  CloudSaveUI.cs           (refactored with localization)
  SyncStatusUI.cs          (new)
  CloudAuthUI.cs           (new)
  CloudSaveLocale.cs       (new — localization delegate + string table)
  Resources/
    CloudSaveUI.prefab     (auto-generated)
    SyncStatusUI.prefab    (auto-generated)
    CloudAuthUI.prefab     (auto-generated)
Editor/
  CloudSaveUIPrefabGenerator.cs  (updated — handles all 3 UIs)
  com.wagenheimer.cloudsave.editor.asmdef
```

## Prefab Auto-Generation

Each factory (`Create()`) follows the same pattern:

1. `Resources.Load<GameObject>("{name}")` — try to load existing prefab
2. If found: `Instantiate`, return component
3. If not found in Editor: build procedural hierarchy, save as prefab at `Assets/Resources/{name}.prefab`, return component
4. If not found in build: build procedural hierarchy, return component

All serialized fields are populated during auto-generation so the prefab works out of the box.
