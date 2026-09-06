# Polished Skin — ready-made art for the built-in UIs

The built-in `CloudSaveUI` / `SyncStatusUI` / `CloudAuthUI` draw themselves from procedurally
generated shapes so the package ships with **zero image assets**. If you want a richer look, assign
a **`CloudSaveUISkin`** and the UIs use your sprites instead — any slot left empty falls back to the
generated shape, so a skin can be partial.

## Setup (3 minutes)

1. **Create the skin** — `Assets → Create → Cloud Save → UI Skin`.
2. **Create the theme** (if you don't have one) — `Assets → Create → Cloud Save → UI Theme`,
   put it at `Assets/Resources/CloudSaveUITheme.asset` so it's picked up automatically, and drag
   your skin into its **Skin** slot.
3. **Fill the slots you care about:**

   | Slot | What it is | Import as |
   |---|---|---|
   | `Panel` | Card / pill background | Sprite (2D and UI), 9-slice border set in the Sprite Editor |
   | `Button` | Button background | Sprite, 9-slice |
   | `Shadow` | Soft drop shadow behind cards | Sprite, 9-slice |
   | `Ring` | Loading spinner arc | Sprite (Filled/Radial360 is applied for you) |
   | `Backdrop` | Full-screen image behind the dim overlay | Sprite, no border; set `BackdropAlpha` (~0.5) |
   | `CloudIcon` / `DeviceIcon` / `CheckIcon` / `WarnIcon` / `CrossIcon` / `SyncIcon` | Replace the vector icons | Sprite with alpha, square, `Preserve Aspect` handled for you |

That's it — no code. `EnableAnimations`, `CornerRadius`, colours and `Font` still come from the theme.

## A starter backdrop

A dark, minimal backdrop image was generated for this skin (deep navy, soft blue glow, empty
centre for text). Drop your own `backdrop.png` in this folder, import it as a **Sprite**, and assign
it to the skin's `Backdrop` slot.

Good sources for the rest:
- Icons: a crisp mono icon set (e.g. Google Material Symbols, exported as white PNGs) reads best.
  AI image generators are poor at clean transparent UI icons — prefer vector/icon-font art there.
- Panels / buttons: any 9-slice rounded-rectangle sprite; a subtle 1px inner highlight sells it.

## Notes

- The skin is resolved through `CloudSaveUITheme.Current.Skin`, so it applies everywhere the built-in
  UIs are used, including a regenerated `Resources/*.prefab`.
- Removing the skin (or the theme) instantly reverts to the procedural look.
