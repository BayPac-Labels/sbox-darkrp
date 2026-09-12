---
name: sbox-player-toolbar-hud
description: >-
  Builds and fixes DarkRP2 / S&Box player HUD chrome — vitals panels, inventory
  toolbar (hotbar), pocket slots, white L-corner frames, slot grid borders,
  HudSway movement bob, selection rings, weapon DisplayIcon/DisplayName, and
  theme tokens. Use when creating or editing any player HUD panel, Inventory,
  InventorySlot, PocketSlot, Vitals, HudSway, hotbar corners/borders, weapon
  icons, or when HUD corners are missing, bleed while walking, sway stops,
  selection desyncs, or styling fails after hotload.
---

# DarkRP2 Player HUD

Conventions for building and fixing the in-game player HUD (vitals + toolbar).
Read this **before** changing framed panels, corners, sway, or hotbar chrome.

## Key files

| Area | Path |
|------|------|
| Hotbar host | `DarkRP2/Code/UI/Inventory/Inventory.razor` (+ `.scss`) |
| Hotbar slot | `DarkRP2/Code/UI/Inventory/InventorySlot.razor` (+ `.scss`) |
| Pocket slot | `DarkRP2/Code/UI/Inventory/PocketSlot.razor` (+ `.scss`) |
| Presets button | `DarkRP2/Code/UI/Inventory/HotbarPresetsButton.razor` (+ `.scss`) |
| Player vitals | `DarkRP2/Code/UI/Vitals/Vitals.razor` (+ `.scss`) |
| Shared sway | `DarkRP2/Code/UI/HudSway.cs` |
| Theme tokens | `DarkRP2/Code/UI/Hud.scss` (via `Theme.scss`) |
| Weapon icons | `DarkRP2/Assets/ui/weapons/*` |
| Scene hosts | `DarkRP2/Assets/scenes/system.scene` (`Inventory`, `Vitals` components) |

Related: [sbox-cmenu-pocket-inventory](../sbox-cmenu-pocket-inventory/SKILL.md) · [icon-assets.md](icon-assets.md)

## Visual language

| Token / element | Value / rule |
|-----------------|--------------|
| Panel fill | `$hud-panel-bg` → `rgba(20,20,20,0.85)` |
| Panel perimeter | **omit** or neutral `$hud-panel-border` — **never cyan** on swayed panels |
| L-corners | White `#ffffff` filled `.arm-h` / `.arm-v` (2px) |
| Slot grid lines | `1px solid rgba(94, 177, 255, 0.25)` (top/bottom/right on slots) |
| Selection / accent | `$hud-outline` → `#5eb1ff` |
| Fonts | Tahoma / Segoe UI — sharp, GMod-like |

## Recipe: framed HUD panel (required)

Every DarkRP-style box (vitals card, ammo, arrest, hotbar, pocket) uses this structure.

### Markup

```html
<!-- Screen anchor (absolute) — NO corners here -->
<div class="player-card">
  <!-- Relative frame — corners + fill live here -->
  <div class="hud-box">
    <div class="corner tl"><div class="arm-h"></div><div class="arm-v"></div></div>
    <div class="corner tr"><div class="arm-h"></div><div class="arm-v"></div></div>
    <div class="corner bl"><div class="arm-h"></div><div class="arm-v"></div></div>
    <div class="corner br"><div class="arm-h"></div><div class="arm-v"></div></div>
    <!-- content -->
  </div>
</div>
```

Toolbar / pocket skip the outer wrapper: `.hotbar` / `.pocket` **are** the relative frame (same corner block nested inside).

### SCSS rules

```scss
.hud-box   // or .hotbar / .pocket
{
  position: relative;      // REQUIRED — absolute anchors do not host corners in S&Box
  overflow: visible;
  background-color: $hud-panel-bg;
  // no cyan border

  .corner
  {
    position: absolute;
    width: 14px;
    height: 14px;
    pointer-events: none;
    z-index: 10;

    .arm-h, .arm-v { position: absolute; background-color: #ffffff; }
    .arm-h { width: 14px; height: 2px; }
    .arm-v { width: 2px; height: 14px; }
  }

  .corner.tl { top: -1px; left: -1px;  .arm-h { top: 0; left: 0; } .arm-v { top: 0; left: 0; } }
  .corner.tr { top: -1px; right: -1px; .arm-h { top: 0; left: 0; } .arm-v { top: 0; right: 0; } }
  .corner.bl { bottom: -1px; left: -1px;  .arm-h { bottom: 0; left: 0; } .arm-v { top: 0; left: 0; } }
  .corner.br { bottom: -1px; right: -1px; .arm-h { bottom: 0; left: 0; } .arm-v { top: 0; right: 0; } }
}
```

- `-1px` offset → 2px stroke centerline sits on the panel edge (1px out + 1px in)
- Use **filled arms**, not CSS `border` L-brackets — empty-panel borders often don’t paint; never use separate `border-style: solid` (S&Box rejects it)
- Outer screen placement stays `position: absolute` + `$deadzone-x` / `$deadzone-y`

## Movement sway (required)

Vitals + Inventory share **`HudSway`** so they bob together.

```csharp
// Vitals
HudSway.Apply( Panel );

// Inventory — bake spawn-menu dip; Style.Transform overrides CSS transform
HudSway.Apply( Panel, extraOffset: spawnMenuOpen ? new Vector2( 0f, 24f ) : Vector2.Zero );
```

| Rule | Detail |
|------|--------|
| API | `HudSway.Apply(Panel, scale = 1f, extraOffset = default)` from each host `OnUpdate` |
| Motion | Walk/run bob, jump/fall, landing punch, strafe lean; optional light roll |
| Pref | Honors `GamePreferences.ViewBobbing` (`sb.viewbob`) |
| Translate | Pixel-snapped in `Apply` so thin strokes stay crisp |
| CSS | Prefer `transition: opacity` only on swayed roots — **never** transition `transform` |
| Scale | **Never** `transform: scale()` on HUD — blurs chrome and opens corner gaps. Size with real px/fonts |
| Sync | Tweak amplitudes only in `HudSway` constants |

### Hotload warning

Changing/removing static members on `HudSway` can yield:

`Unable to find matching substitution for a static method`

That leaves Vitals unstyled (plain text) and kills sway. **Fully restart the S&Box editor** — hotload will not recover.

Keep public surface stable (`Offset`, `Roll`, `Tick`, `Apply`). Prefer additive changes over deleting static API.

## Toolbar (hotbar)

### Host layout

```
Inventory root (HudSway)
└── .hotbar-row          row; align flex-end; gap 6px
    ├── .slot-stack      column; stretch; gap 6px
    │   ├── .pocket      optional (C-menu open)
    │   └── .hotbar      relative frame + 9 InventorySlots
    └── HotbarPresetsButton
```

- Corners on **`.hotbar` / `.pocket` only** — not on `.hotbar-row` / `.slot-stack`
- Pocket must share `.slot-stack` with hotbar so widths align when presets appear

### Slot chrome (`InventorySlot`)

```
┌─────────────┐
│ 1           │  .index
│   [ ICON ]  │  .icon
│ ─────────── │  .divider  bottom: 16px
│   Physgun   │  .name     under divider
└─────────────┘
```

- Size `64×72`; absolute layout inside slot (flex clips small labels)
- Grid lines on the slot: `border-top` / `border-bottom` / `border-right` = `1px solid rgba(94,177,255,0.25)`; last child clears `border-right`
- Name: `font-size` ≥ **11px** (9px often fails); set `NamePanel.Text` in `Tick` when dynamic
- Spawner → entity `DisplayName`; include name in `BuildHash()`

### Selection (sway-safe)

Do **not** use `box-shadow` — it desyncs from parent transform sway.

```scss
.selection { position: absolute; inset: 0; border: 1px solid transparent; z-index: 1; }
.selected .selection { border-color: $hud-outline; background-color: rgba(94,177,255,0.08); }
```

Keep ring at **1px**; `.name` / `.index` at `z-index: 2`.

## Vitals

| Panel | Placement | Inner frame |
|-------|-----------|-------------|
| Player card | `bottom/left: $deadzone-*` | `.hud-box` column, ~10% larger fonts than base |
| Ammo | `bottom/right: $deadzone-*` | `.hud-box` row |
| Arrest | top center | `.hud-box` |

- Content (labels) + corners inside `.hud-box`; outer div only positions
- Spawn-menu: fade/slide **outer** anchors via opacity (+ optional child `transform`); do not fight root `HudSway` transform
- Hide with `Player.WantsHideHud`

## Weapon icons (physgun standard)

Canonical: `DarkRP2/Assets/ui/weapons/physgun_hud.png`

- Solid white filled silhouette, transparent BG, horizontal side profile
- Remakes: `ui/weapons/<id>_wide.png`; change filename when replacing (texture cache)
- Prefab: `DisplayIcon` via `imagefile` — avoid SVG for hotbar
- Pipeline detail: [icon-assets.md](icon-assets.md)

| Weapon | Path |
|--------|------|
| Physgun / Toolgun / Hands / Keys | `*_hud.png` |
| Crowbar, Arrest, Lockpick, Medkit, RPG, Ragebait, Spawner, guns | `*_wide.png` |
| Camera | `ui/weapons/camera_front.png` |
| Grenade | `ui/weapons/grenades/he_grenade_wide.png` |

## Creating a new HUD panel (checklist)

1. Add Razor host under `Code/UI/...` as `PanelComponent`; register on `system.scene` if needed
2. Outer absolute anchor for screen position; inner **`position: relative`** frame with `$hud-panel-bg`
3. Copy white `.corner` + `.arm-h` / `.arm-v` block (offset `-1px`)
4. No cyan perimeter border; use white corners (± faint cyan grid lines if slot-like)
5. Call `HudSway.Apply` from `OnUpdate` if it should bob with player/toolbar
6. `transition: opacity` only — never CSS `scale()` or transform transitions on swayed roots
7. Respect `Player.WantsHideHud`
8. After `HudSway` static edits → **restart editor**

## Pitfalls (do not repeat)

| Symptom | Cause | Fix |
|---------|-------|-----|
| Corners missing on vitals; toolbar OK | Corners on `position: absolute` panel | Put corners on `.hud-box` / `.hotbar` (`relative`) |
| Plain unstyled vitals text + pink Code Error | `HudSway` hotload broke statics | Restart S&Box editor |
| Blue lines bleed while walking | Cyan perimeter border + transform AA | Remove cyan border; white corners only; pixel-snap translate |
| Blurry HUD / corner gaps | CSS `scale()` | Real px/font sizes |
| Selection square stuck while swaying | `box-shadow` selection | In-tree `.selection` border |
| `solid is not valid with border-style` | Separate `border-style: solid` | Use `border: Npx solid color` shorthand or filled arms |
| SCSS file lock / stale chrome | Editor holding file during write | Retry save; restart if stuck |

## Change checklist

- [ ] Framed panels use relative box + white filled L-arms at `-1px`
- [ ] No cyan perimeter on swayed panels
- [ ] Hotbar slots: top/bottom/right grid borders; absolute icon/divider/name
- [ ] Selection uses `.selection` border (1px), not `box-shadow`
- [ ] Vitals + Inventory call `HudSway.Apply`; spawn offsets baked in
- [ ] Swayed roots do not transition `transform`; no CSS `scale()`
- [ ] Icons solid white; prefab paths updated; pocket/hotbar hashes include display names
- [ ] Restarted editor after any `HudSway` static API change
