# Cloud Save — Quick Start (`CloudSaveController`)

`CloudSaveController` is the **recommended** way to integrate this package. It runs the entire
Cloud Save lifecycle so your game supplies only the two things that are genuinely game‑specific:
**how to serialize your save** and **how to apply it back**.

Everything else — Unity Services init, anonymous sign‑in, pulling the cloud save on launch, conflict
resolution, the conflict *timestamp*, re‑syncing when the player switches account, debounced
auto‑upload, and one‑time legacy migration — lives inside the controller.

> Prefer the low‑level API (`CloudSync` / `CloudAuth` directly)? That still works and is documented in
> [`INTEGRATION.md`](INTEGRATION.md). The controller is a thin layer on top of it.

---

## 1. The whole integration

```csharp
using Wagenheimer.CloudSave;

public class SaveSystem : MonoBehaviour
{
    CloudSaveController _cloud;

    async void Start()
    {
        _cloud = CloudSaveController.Create(new CloudSaveOptions
        {
            // ── required ──────────────────────────────────────────────
            SaveKey     = "my_game_save",                       // stable for the life of the game
            Serialize   = ()    => Encode(GameSave.Current),    // your save -> byte[]
            Deserialize = bytes =>                              // byte[] -> your save (+ write to disk!)
            {
                GameSave.Current = Decode(bytes);
                GameSave.WriteToDisk();
            },

            // ── optional ─────────────────────────────────────────────
            OnCloudApplied = () => Hud.Refresh(),               // cloud just overwrote local — refresh UI
        });

        await _cloud.StartAsync();   // sign in + pull cloud (+ migrate, if configured)
    }

    // call this right after every local save
    public void OnGameSaved() => _cloud.MarkDirty();

    void OnApplicationPause(bool paused) { if (paused) _ = _cloud.FlushAsync(); }
    void OnApplicationQuit()             { _ = _cloud.FlushAsync(); }
}
```

That is the complete integration. There is **no timestamp field to add**, no `CloudSync.Configure`,
no `InitAndSyncAsync`, no manual conflict wiring.

---

## 2. What each required field does

| Field | Type | Responsibility |
|---|---|---|
| `SaveKey` | `string` | The Cloud Save slot name. Pick once, never change it. |
| `Serialize` | `Func<byte[]>` | Return the current save as bytes. Bring your own JSON / compression. |
| `Deserialize` | `Action<byte[]>` | Apply cloud bytes to the running game **and persist them to disk** (so a crash right after doesn't lose the pulled save). Called on first sync, on account switch, and after migration. |

---

## 3. Optional fields

| Field | Default | Use it when |
|---|---|---|
| `OnCloudApplied` | — | You need to refresh UI after the cloud overwrites local. |
| `OnSyncCompleted` | — | You want the sync outcome (`CloudSyncResult`) for a status badge or analytics. |
| `ConflictResolver` | cloud wins if newer | You want to show the player a "keep local / use cloud" dialog. |
| `GetTimestamp` / `SetTimestamp` | controller owns it | You want the conflict timestamp mirrored into your own save data. |
| `FetchLegacySave` | — | Migrating players from PlayFab / Firebase / a custom backend (see below). |
| `ApplyLegacySave` | `Deserialize` | The migrated bytes need different handling than a normal cloud pull. |
| `AutoSave` | `true` | Set `false` to upload immediately on `MarkDirty()` instead of debouncing. |
| `AutoSaveDebounceSeconds` | `2` | Tune how long repeated `MarkDirty()` calls coalesce. |
| `OnClearLocalSave` | — | Required only if you call `ResetProgressAsync()`. |
| `SerializeCleanSave` | — | On reset, upload a clean save (so other devices reset too) instead of deleting the slot. |

---

## 4. Timestamps — you don't manage them

The controller stores the conflict timestamp in `PlayerPrefs` under `ucs_ts_<SaveKey>` and sets it to
`DateTime.UtcNow.Ticks` on every upload. After a cloud pull it anchors the local value to the cloud's,
so the next comparison is correct.

Provide `GetTimestamp` **only** if your save already has an authoritative `long` timestamp you'd
rather use (common when migrating a legacy game). Provide `SetTimestamp` if you also want the value
written back into your save object.

---

## 5. Account linking (cross‑device saves)

Anonymous saves are device‑local. To follow the player across devices, link a provider after its SDK
returns a token:

```csharp
// Android, after GPGS returns a server auth code
await _cloud.LinkGooglePlayGamesAsync(serverAuthCode);

// iOS, after Sign in with Apple
await _cloud.LinkAppleAsync(identityToken);

// Facebook
await _cloud.LinkFacebookAsync(accessToken);
```

If that credential is already linked to another account (the player reinstalled), the controller
signs into the existing account and **re‑syncs automatically** — your `Deserialize` + `OnCloudApplied`
run again with the recovered save.

Provider console setup (Google Play, Apple Developer, Meta, Unity Dashboard) is walked step‑by‑step in
**Tools → Wagenheimer → Cloud Save → Setup & Verification**.

---

## 6. Legacy migration (PlayFab / Firebase / custom)

Set `FetchLegacySave` and the controller runs a one‑time migration right after the first sync:

```csharp
FetchLegacySave = async () =>
{
    var (raw, ticks) = await MyPlayFabBridge.DownloadSaveAsync();
    return (raw, ticks);          // bytes + UTC-ticks timestamp; return (null, 0) if there is nothing
},
```

It only imports when the legacy save is newer than (or UGS is empty vs) the cloud. It is idempotent —
safe to leave in the build. A ready‑made PlayFab bridge and a resumable wizard ship in
`Samples~/PlayFabMigration` (arriving in a later release).

---

## 7. Reset progress & delete account (store compliance)

```csharp
// wipe progress, keep the account/identity
await _cloud.ResetProgressAsync();      // needs OnClearLocalSave (+ optional SerializeCleanSave)

// delete the account entirely (Apple 5.1.1(v) / Google Play / GDPR)
await _cloud.DeleteAccountAsync();
```

---

## 8. Teardown

`CloudSaveController` subscribes to a couple of static events. If your game fully tears down its
session (not just a scene change), call `_cloud.Dispose()`.

---

## 9. Verify it works

Open **Tools → Wagenheimer → Cloud Save → Setup & Verification**, enter Play Mode, and run the
checks. Each step has a **Copy AI prompt** button if you'd rather have an assistant wire it in.
