# UnityCloudSave — AGENTS.md

This is a **Unity UPM package** (`com.wagenheimer.cloudsave`), not a Unity project. The root IS the package root.

## Structure

```
Runtime/       → package code (asmdef: Wagenheimer.CloudSave)
Editor/        → Editor-only code (asmdef: Wagenheimer.CloudSave.Editor)
docs/design/   → design docs (already implemented, not aspirational)
.github/workflows/ → CI (auto version bump on push to main)
```

## Core Architecture

- **`CloudSync`** (static) — `Configure()`, `InitAndSyncAsync()`, `SaveAsync()`. Fires `OnSyncStarted`/`OnSyncCompleted`. Delegates auth to `CloudAuth`.
- **`CloudAuth`** (static) — `EnsureSignedInAsync()`, `LinkGooglePlayGamesAsync()`, `LinkAppleAsync()`, `LinkAppleGameCenterAsync()`. Anonymous first, upgrade to platform provider.
- **3 UI components** (`CloudSaveUI`, `SyncStatusUI`, `CloudAuthUI`) — each a `MonoBehaviour` with a static `Create()` factory. Prefab-backed via `Resources.Load()`, procedural fallback in builds. Singletons with `DontDestroyOnLoad`.
- **`CloudSaveLocale`** — localization delegate (`Func<string, string>`). Null = English fallback. All UI text goes through this.
- **`CloudSyncEvent.cs`** — enums and data classes (`CloudSyncResult`, `CloudConflictChoice`, `CloudConflictReason`, `CloudConflictData`).

All runtime classes live in namespace `Wagenheimer.CloudSave`.

## Editor-Only Test Hooks

- `CloudSync.TestFireSyncStarted()` / `CloudSync.TestFireSyncCompleted(CloudSyncResult result)`
- `CloudAuth.TestFireLinked(CloudAuthProvider)` / `CloudAuth.TestFireAccountSwitched(CloudAuthProvider)`
- Conditionally compiled via `[System.Diagnostics.Conditional("UNITY_EDITOR")]` — calls are stripped in non-Editor builds.
- **Test Window:** `Tools → Wagenheimer → Cloud Save → Open Test Window` (`CloudSaveTester`)

## UI Prefab Auto-Generation

Each factory (`CloudSaveUI.Create()`, `SyncStatusUI.Create()`, `CloudAuthUI.Create()`) follows:
1. `Resources.Load<GameObject>("{name}")` — use existing prefab if found
2. Editor fallback: build procedural UI + save as `Assets/Resources/{name}.prefab`
3. Build fallback: build procedural UI only

Regenerate all prefabs via: **Tools → Wagenheimer → Cloud Save → Setup UI Prefabs → All**

## CI

`bump-version.yml` runs on push to `main` (ignores `package.json`/`CHANGELOG.md` changes, skips commits containing `chore: bump version`):
- Determines bump type from commit message (conventional commits: `feat!:` → major, `feat:` → minor, else patch)
- Bumps `package.json`, updates `CHANGELOG.md` from git log since last tag
- Creates tag `v{version}` and GitHub release

## Conventions & Gotchas

- All text is `TextMeshProUGUI` — no legacy `Text` anywhere.
- Timestamps are `long` (DateTime.UtcNow.Ticks), not Unity time.
- Conflict resolution: last-write-wins. Default (when `ConflictResolver` is null): cloud always wins.
- `CloudSaveUI` auto-installs as `CloudSync.ConflictResolver` in `Awake`.
- Canvas scaler: `ScaleWithScreenSize`, ref 1080×1920, match 0.5.
- All APIs are `async Task` — fire-and-forget with `_ = MethodAsync()` convention.
- UI `_sortOrder` defaults: CloudSaveUI=200, SyncStatusUI=150, CloudAuthUI=250.
- `UpdateChecker` (`[InitializeOnLoad]`) polls GitHub every 24h for newer versions.
- `InternalsVisibleTo` from Runtime → Editor is declared in `CloudSaveLocale.cs`.
- Editor menu items unified under `Tools/Wagenheimer/Cloud Save/` since v4.3.3.
- Package dependencies (auto-resolved for consumers): `com.unity.services.core` 1.12+, `com.unity.services.authentication` 2.7+, `com.unity.services.cloudsave` 3.0+, `com.unity.textmeshpro` 3.0.6+.

## Critical: .meta Files

Every file created in this repo **MUST** have a corresponding `.meta` file with a unique GUID. Without it, Unity logs `"has no meta file, but it's in an immutable folder"` and ignores the asset when the package is consumed.

- `.cs` files → use `MonoImporter` template (see any existing `.cs.meta`)
- `.md` files → use `TextScriptImporter` template (see `docs/INTEGRATION.md.meta`)
- Generate GUIDs via `[guid]::NewGuid().ToString("N")` (PowerShell)

**Always create `.meta` immediately after creating the asset file.**

## Commands

There are no CLI build/test commands — this is a Unity package. Test via the Editor Test Window or manual playmode testing in a consumer project.

## What NOT to Do

- Do not add test projects or test scripts to this repo — testing is done via the Editor Test Window.
- Do not convert static classes to instance classes — static API is intentional for the byte[] drop-in pattern.
- Do not remove or modify `Runtime/Resources/` prefabs — they are the shipped defaults and also the output target for Editor auto-generation.
- Do not add new package dependencies unless absolutely necessary — current deps are minimal.
