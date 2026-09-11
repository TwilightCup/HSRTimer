# Settings Panel

> **中文版**: [zh/PANEL.md](zh/PANEL.md)

The settings panel (open/close with the **settings key**, default `Home`) edits
every user-tunable option live — changes take effect immediately and are saved
to disk when the panel is closed or the game exits. It is organized into the
built-in tabs below, plus any **extra tabs registered by other plugins** via
`ISettingsPanelTab` (see [EXTENDING.md](EXTENDING.md)). Extra tabs that
implement `ILocalizableSettingsPanelTab` switch language together with
HSRTimer's **General → Language** selection, and extra tabs can subscribe to
`SettingsPanelTabRegistry.SettingsSaved` to persist their own config when
HSRTimer saves.

> Cheat/speed/drift detection (R5.1) is always on with hardcoded thresholds and
> is intentionally **not** exposed anywhere in the panel.

## General

- **Timing** — `auto_reset`,
  `restart_clears_forgivable` (clears forgivable flags on a pause-menu
  restart; see [CONFIG.md](CONFIG.md)), and the retry target override
  (`retry_level_override_enabled` + `retry_level_override`). When the override
  is enabled, a text field appears for entering the target's English localized
  name (case-insensitive) or Workshop numeric id. Invalid values show a red
  HUD hint when Retry is pressed; from the main menu, a valid value lets Retry
  directly enter the specified level. Pause time is always counted and
  menu/lobby time is never counted; there are no toggles for them.
- **Language** — pick the active language from the loaded set (single-select).
  "Reload language files" re-scans `lang/*.txt`. External tabs that implement
  `ILocalizableSettingsPanelTab` follow this selection automatically.
- **Keybinds** — reset / retry / settings / leaderboard toggle keys. The
  leaderboard toggle key shows/hides the shared leaderboard HUD, whether it is
  showing the subsegment references or the marker feed. To rebind: click the
  field, then press the desired key. Pure modifier presses are ignored. Mouse
  side buttons (`Mouse3`–`Mouse6`) can also be bound; mouse left/right buttons
  remain reserved for normal UI use.

## Interface

- **HUD** — `show_hud`; `show_real_time` (show the always-active Real Time
  clock); `show_wake_up_time` (show Wake Up Time in the right-hand HUD column);
  `only_record_first_wake_up_time` (visible only while Wake Up Time display is
  on — restores the original first-wake-up-only behavior);
  `center_loading_saving` (moves the game's own top-right
  "Loading"/"Saving" prompts to the top-center); the main text block's offset
  (`offset_x`, `offset_y`), `font_size`, and the two-color gradient
  (`color_a`, `color_b`) with per-channel RGBA sliders.

## Category

- **Rule tags** — a multi-select of every registered rule tag (the four built-in
  tags: `Checkpoint`, `NoCheckpoint`, `Jumpless`, `Voiceline`, plus any custom
  tags registered by other plugins — see [EXTENDING.md](EXTENDING.md)).
  Checking a tag enables it; unchecking disables it. There are no category
  presets — this tag set *is* the active rule set. Changes are live and
  persisted to `tags.ini` on close/exit.

## Subsegment

- **Subsegment** — enable/disable subsegment time comparison, PB and manual-load
  sample paths, the initial multi-run project, and the detailed detection/settle
  parameters. During a multi-run the project can auto-upgrade for that session
  only (Any% ⊃ Steam% ⊃ Dark% ⊃ Aztec%) without changing the saved setting; if
  the selected project has no data at all, it falls back to the smallest
  project that has data (session-only). The leaderboard toggle key is in
  **General → Keybinds**; the leaderboard appearance settings live on the
  **Leaderboard** tab below.

## Leaderboard

- **Content** — choose what the shared leaderboard HUD shows: **Subsegment**
  (reference time comparison) or **Markers** (the current level's marker feed).
  The show/hide key and the appearance settings below apply to both modes.
- **HUD** — the leaderboard font size, X offset, and Y offset (relative to
  automatic vertical centering).
- **Entry colors** — three user-configurable colors for the three leaderboard
  entry states: faster/ahead (default green), slower/behind (default red), and
  tie/no-data (default white, shown as `--`). In Markers mode the colors still
  reflect ahead/behind vs the marker's PB even when absolute times are shown.
- **Marker time display** — (Markers mode only) whether each triggered marker
  row shows its absolute segment time or the signed difference to that marker's
  PB.
- **Displayed sources** — (Subsegment mode only) at the bottom, a toggle for
  every subsegment source: **PB** and each top-level folder under the load
  directory. Only checked sources appear on the leaderboard. The list is still
  truncated to `MaxLeaderboardEntries` after filtering and sorting.

## Markers

- **Markers** — enable/disable the marker module, toggle **marker edit mode**
  (which also turns on the in-game overlay, the conspicuous HUD hint at the
  bottom of the timer HUD, and the XYZ axis indicator below it), and browse
  levels: **Main Dreams** (built-in levels), **Extra Dreams**
  (editor-pick levels), and **Workshop**. Each level button opens that level's
  marker page.
- **Level page** — a **Back** button, the list of the level's markers (each
  row expands into its editor), and a **New marker** button at the bottom
  (visible only while edit mode is on).
- **Marker editor** — a name text field and a type dropdown (Range trigger /
  Checkpoint trigger / Grab object), with per-type controls: for Range, "Set to
  player position" plus center/length inputs and grab/jump requirement toggles;
  for Checkpoint, the checkpoint number and an optional "trigger when loaded
  from the pause menu" toggle; for Grab object, "Set to currently grabbed
  object" (invalid when more than one object is grabbed) and a read-only object
  id. Markers can be enabled/disabled and deleted (with confirmation), and the
  current PB times are shown read-only.
- When edit mode is off, expanded markers are read-only.

See [CATEGORIES.md](CATEGORIES.md) for what each tag does,
[CHECKPOINTS.md](CHECKPOINTS.md) for the Checkpoint tag's rules, and
[MARKERS.md](MARKERS.md) for the marker feature details.
