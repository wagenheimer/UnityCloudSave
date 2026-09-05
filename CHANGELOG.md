# Changelog

## [4.16.1] - 2026-09-05

### Fixed
- exclude verification steps from Integration meter; surface real UGS init error

## [4.16.0] - 2026-09-05

### Added
- guided setup & verification hub foundation (Phase 0)

## [4.15.3] - 2026-09-05

### Fixed
- update service accounts URL to /settings/service-accounts

## [4.15.2] - 2026-09-05

### Fixed
- resolve numeric genesis org id 22730, correct service accounts URL without project path, show breadcrumbs and URL text fields

## [4.15.1] - 2026-09-05

### Changed
- docs/ui: clarify Service Accounts vs Secrets tab with direct links and instructions

## [4.15.0] - 2026-09-05

### Added
- update UGS URLs to cloud.unity.com player-authentication/identity-providers

## [4.14.0] - 2026-09-05

### Added
- add UGS DashboardCredentialsHelper to detect IDs and auto-configure UGS

## [4.13.2] - 2026-09-05

### Fixed
- make RunAuditMenuItem return void per Unity MenuItem convention

## [4.13.1] - 2026-09-05

### Changed
- perf(audit): cache cs files in memory for 70x faster audit execution

## [4.13.0] - 2026-09-05

### Added
- add RunAuditFromCli menu item, automated CLI report and AI prompt

## [4.12.1] - 2026-09-05

### Added
- add CloudMigration, save reset, delete save, and store compliance audit checks (v4.11.0)

### Fixed
- replace manual YAML stubs with valid 64-bit serialized prefabs (v4.11.1)

## [4.12.0] - 2026-09-05

### Fixed
- Replaced hand-crafted UI prefabs (`CloudAuthUI.prefab`, `SyncStatusUI.prefab`, `CloudSaveUI.prefab`) in `Runtime/Resources/` with properly serialized Unity prefabs using 64-bit file IDs, resolving "unexpected file IDs and is likely to be corrupt" warnings upon project import.

### Added
- `CloudMigration` universal class (`TryMigrateAsync`) for seamless import from legacy backends (PlayFab, Firebase, custom servers) into UGS Cloud Save.
- `CloudSync.DeleteCloudSaveAsync()` to delete cloud save keys from UGS.
- `CloudSync.ResetProgressAsync(onClearLocalSave, getCleanSaveBytes)` for progress reset preserving linked account identity.
- `CloudSync.OnSaveReset` event fired on progress reset.
- `CloudSync.LoadRawCloudDataAsync()` exposed for raw data queries without conflict flow.
- `CloudAuth.OnAccountDeleted` event fired on account deletion.
- `CloudAuth.CopyPlayerIdToClipboard()` utility method.
- Complete Store Compliance audit checks in `CloudSaveAudit`: Apple Guideline 5.1.1(v) In-App Deletion, Google Play Data Safety URL check, Meta Data Deletion URL, Save Game Reset check, and Legacy Migration detector.
- Integration Guide cards for Account Deletion compliance, Save Reset, and Legacy Migration.
- Localized strings for Account Deletion, Reset Progress, and Store Compliance.

### Fixed
- Replaced hand-crafted UI prefabs (`CloudAuthUI.prefab`, `SyncStatusUI.prefab`, `CloudSaveUI.prefab`) in `Runtime/Resources/` with properly serialized Unity prefabs using 64-bit file IDs, resolving "unexpected file IDs and is likely to be corrupt" warnings upon project import.

## [4.11.0] - 2026-09-02

### Added
- Facebook and Google sign-in providers, account deletion, sign-out and unlink.

## [4.10.0] - 2026-09-01

### Added
- `CloudAuth.LinkFacebookAsync(accessToken)` and `SignInWithFacebookAsync` fallback for Facebook login.
- `CloudAuth.LinkGoogleAsync(idToken)` and `SignInWithGoogleAsync` fallback for Google ID token login.
- `CloudAuth.DeleteAccountAsync()` for GDPR / Store compliance account deletion in Unity Gaming Services.
- `CloudAuth.SignOut(clearCredentials)` and `CloudAuth.UnlinkAsync(provider)`.
- `CloudAuthProvider.Facebook` and `CloudAuthProvider.Google` enum entries.
- Localized strings and convenience methods for Facebook and Google sign-in.

## [4.9.1] - 2026-07-15

### Changed
- chore: translate UI, docs and comments to English

## [4.9.0] - 2026-07-09

### Added
- show dialog on manual check (up to date / errors), redesign update popup

## [4.8.1] - 2026-07-08

### Fixed
- null guard for _detailText in UpdateDetail (old prefabs without the new field)

## [4.8.0] - 2026-07-08

### Added
- add detail tooltip to SyncStatusUI showing PlayerId, Provider, and last sync result

## [4.7.5] - 2026-07-08

### Fixed
- SyncStatusUI stuck on Offline if created after sync completed — check LastResult

## [4.7.4] - 2026-07-08

### Changed
- refactor: remove deprecated .mm bridge option, Apple.GameKit is the only supported iOS path

## [4.7.3] - 2026-07-07

### Changed
- docs: add full Dashboard sign-in method prerequisites and platform console setup steps to guide, audit, and editor window

## [4.7.2] - 2026-07-07

### Fixed
- proper UTF-8 encoding for INTEGRATION.md (was corrupted by Add-Content)

## [4.7.1] - 2026-07-07

### Changed
- docs: clarify auth 3-step flow, add logging reference table to guide, audit, and window

## [4.7.0] - 2026-07-07

### Added
- add Android GPGS and iOS native bridge detection to audit tool

## [4.6.1] - 2026-07-07

### Changed
- refactor: redesign audit with progress bar, file-level details, manual checks, and cards

## [4.6.0] - 2026-07-07

### Added
- redesign Integration Guide with modern card layout, copy buttons, and links

## [4.5.1] - 2026-07-07

### Fixed
- add missing AGENTS.md.meta and document .meta requirement in AGENTS.md

## [4.5.0] - 2026-07-07

### Added
- Add AGENTS.md, integration guide, audit tool, and instructions window

## [4.4.0] - 2026-07-07

### Added
- show changelog excerpt and one-click update in the popup

## [4.3.3] - 2026-07-07

### Fixed
- pass commit message via env to avoid shell breakage/injection

### Changed
- refactor: unify Editor menus under Tools/Wagenheimer/Cloud Save

## [4.3.2] - 2026-07-07

### Fixed
- add missing .meta files for CHANGELOG.md, LICENSE, README.md, docs/

## [4.3.1] - 2026-07-07

### Fixed
- resolve CS0104 ambiguous PackageInfo reference

## [4.3.0] - 2026-07-07

### Added
- auto-generate CHANGELOG.md entry on version bump

## [4.2.0] - 2026-07-07

### Added
- add self-update checker and CI version auto-bump workflow — Editor checker compara a versão instalada com a do branch `main` e avisa quando há uma nova; GitHub Actions agora faz bump automático de versão, tag e release a cada push

## [4.1.1] - 2026-06-25

### Fixed
- `TextAlignmentOptions.TopCenter` → `TextAlignmentOptions.Top` (compilation error no TMP)

### Added
- `CloudSaveUI.Instance` — singleton property; `Create()` é idempotente
- `SyncStatusUI.Instance` e `SyncStatusUI.Status` — singleton + status readonly
- `_sortOrder` serializado nas 3 UIs (CloudSaveUI=200, SyncStatusUI=150, CloudAuthUI=250)
- Conflict dialog com timeout de 30s (fallback para UseCloud)
- `CloudAuthUI.OnDismissed` — evento ao fechar o dialog
- Overlay click-to-close no CloudAuthUI
- `CloudSync.DataKey` e `CloudSync.LastResult` públicos

### Changed
- `SyncStatusUI` agora inicia como `Offline` (antes `Synced`)
- `SyncStatusUI` e `CloudSaveUI` usam `DontDestroyOnLoad` + singleton guard
- Editor generator cria diretório `Assets/Resources/` automaticamente se não existir

### Removed
- Duplicação de `BuildDefaultUI()` e `SetupReferencesFromChildren()` no CloudAuthUI

## [4.1.2] - 2026-06-26

### Fixed
- `#if UNITY_EDITOR` test helpers posicionados fora da classe causando CS8803/CS0106/CS1022

### Added
- `CloudSaveTester` — Editor window para testar todas as UIs e eventos sem UGS
  - Menu: **Tools → Cloud Save → Test Window**
  - Simula sync, toast, conflito, auth link, account switch
  - Painel de estado com todos os valores atuais
- `CloudSync.TestFireSyncStarted()` / `TestFireSyncCompleted()` — helpers para teste de eventos
- `CloudAuth.TestFireLinked()` / `TestFireAccountSwitched()` — helpers para teste de eventos

## [4.1.0] - 2026-06-25

### Added
- `CloudSaveLocale` — localization delegate + string table with English fallback
  - `CloudSaveLocale.Translate` — assign to integrate I2 Localization or any other system
  - Convenience accessors for all string keys (e.g. `CloudSaveLocale.Synced()`)
  - All CloudSaveUI strings now use `CloudSaveLocale` instead of hardcoded text
- `SyncStatusUI` — persistent sync status indicator
  - 4 states: Synced (green), Syncing (blue), Offline (yellow), Error (red)
  - Auto-listeners on `CloudSync.OnSyncStarted` / `OnSyncCompleted`
  - Last-sync time tooltip
  - Factory: `SyncStatusUI.Create()`
- `CloudAuthUI` — modal dialog for linking anonymous account to a platform provider
  - Shows correct button for current platform (`#if UNITY_ANDROID` / `#if UNITY_IOS`)
  - Factory: `CloudAuthUI.Create()`, call `.Show()` to display
- Editor generator now supports all 3 UIs via `Tools > Cloud Save > Setup UI Prefabs` menu

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
