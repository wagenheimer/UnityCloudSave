# Changelog

## [4.0.0] - 2026-06-25

### Added
- `CloudSaveUI` component (in-package) — replaces game-project version
  - Serialized `[SerializeField]` fields for all UI elements — assign references in the Inspector
  - `CloudSaveUI.Create()` — static factory, creates a fully working UI instance
  - `BuildDefaultUI()` — builds UI hierarchy programmatically when no prefab references assigned
  - Context menu "Setup References from Children" in Editor
- `CloudSaveUI.prefab` — minimal default prefab (procedural fallback builds the full UI)
- `CloudSaveUIPrefabGenerator` — Editor tool via `Tools > Cloud Save > Generate UI Prefab` to create a fully-assigned prefab at `Assets/Resources/CloudSaveUI.prefab`
- Dependency on `com.unity.textmeshpro` (3.0.6+) — all text uses `TextMeshProUGUI`

### Changed
- `CloudSaveUI` migrated from legacy `Text` to `TextMeshProUGUI`
- Package assembly (`Wagenheimer.CloudSave`) now references `Unity.TextMeshPro`
- Added `Wagenheimer.CloudSave.Editor` assembly for Editor scripts

### Removed
- All `using UnityEngine.UI.Text` references — text is 100% TextMeshPro now

---

## [3.1.0] - 2026-06-25

### Added
- `CloudSyncEvent.cs` — event types for UI integration (`CloudSyncResult`, `CloudConflictData`, `CloudConflictChoice`, `CloudConflictReason`)
- `CloudSync.OnSyncStarted` — fires when sync begins
- `CloudSync.OnSyncCompleted` — fires with `CloudSyncResult` when sync ends
- `CloudSync.ConflictResolver` — `Func<CloudConflictData, Task<CloudConflictChoice>>` delegate for custom conflict UI (cloud wins by default when null)
- `CloudAuth.OnAccountSwitched` — fires when `SignedInExisting` (player recovered a previous account); PlayerId has changed

### Changed
- `CloudSync.InitAndSyncAsync` now fires events and invokes `ConflictResolver` when cloud is newer
- `CloudAuth.FinalizeLink` now fires `OnAccountSwitched` when status is `SignedInExisting`

---

## [3.0.0] - 2026-06-25

### Added
- `CloudAuth` — full authentication manager:
  - Anonymous sign-in at startup (automatic, via `EnsureSignedInAsync`)
  - `LinkGooglePlayGamesAsync(serverAuthCode)` — upgrade to GPGS on Android
  - `LinkAppleAsync(identityToken)` — upgrade via Sign in with Apple on iOS
  - `LinkAppleGameCenterAsync(...)` — upgrade via Game Center on iOS (requires native bridge)
  - Automatic fallback: if credential already linked to another account, signs in to that account (`SignedInExisting`) and re-syncs cloud save
  - `IsAnonymous`, `IsLinked`, `Provider`, `PlayerId` state properties
  - `OnLinked` event (fires after first link or existing-account sign-in)
- `CloudAuthProvider` enum: `Anonymous`, `GooglePlayGames`, `Apple`, `AppleGameCenter`
- `CloudLinkResult` / `CloudLinkStatus` — typed result for all link operations
- `.meta` files for all package assets (required for Unity to recognise the package)

### Changed
- `CloudSync` now delegates authentication to `CloudAuth` (no breaking changes to `CloudSync` public API)
- `CloudSync.IsAvailable` now reflects `CloudAuth.IsReady`
- `package.json` version bumped to 3.0.0

### Removed
- `CloudSync`'s private `InitAsync` method (logic moved to `CloudAuth.EnsureSignedInAsync`)

---

## [2.0.0] - 2026-06-25

### Added
- Full rewrite using Unity Cloud Save (UGS) — replaces GPGS Saved Games and iCloud KV Store
- `CloudSync` static class: `Configure`, `InitAndSyncAsync`, `SaveAsync`, `IsAvailable`
- Anonymous authentication via Unity Authentication SDK
- Timestamp-based conflict resolution (last-write-wins using `long` UTC ticks)
- Package declares UGS dependencies so consumers get them automatically

### Removed
- `GooglePlayCloudSaveService`, `iCloudSaveService`, native iOS plugin, `iOSPostBuildProcessor`

---

## [1.0.0] - 2026-06-25

### Added
- `GooglePlayCloudSaveService` — Android cloud save via GPGS Saved Games API
- `iCloudSaveService` — iOS cloud save via NSUbiquitousKeyValueStore
- `iCloudSavePlugin.mm` — native Objective-C bridge for iCloud KV Store
- `iOSPostBuildProcessor` — PostProcessBuild script that adds iCloud capability to Xcode automatically
