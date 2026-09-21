# Configuration

> **中文版**: [zh/CONFIG.md](zh/CONFIG.md)

All config lives under `<BepInEx config dir>/HSRTimer/` (on a typical Linux
Steam install,
`~/.local/share/Steam/steamapps/common/Human Fall Flat/BepInEx/config/HSRTimer/`).
Files are human-readable, sectioned `key = value` text. `#` lines are comments.

Every file is parsed **line by line, tolerantly**: a malformed line is skipped
with a logged warning that names the file and line number. The plugin never
fails to start because of one bad line (spec N6). Missing keys fall back to
defaults.

## settings.ini

```ini
[settings]
auto_reset = true
restart_clears_forgivable = false
retry_min_dwell = 0.5
retry_level_override_enabled = false
retry_level_override =
show_hud = true
show_real_time = true
show_wake_up_time = true
only_record_first_wake_up_time = false
center_loading_saving = false
language = en
category = any
reset_key = Backspace
retry_key = R
menu_key = Home
```

| Key | Values | Default | Notes |
|-----|--------|---------|-------|
| `auto_reset` | true/false | true | R1.7.2 — reset the live timers and last-segment snapshots when leaving to the menu/lobby; keeps the last completed run total |
| `restart_clears_forgivable` | true/false | false | R5.4.3 — clear forgivable validity flags when the level is restarted from the in-level **pause menu** (the run's timers keep running). The one-key retry clears them unconditionally (fixed behavior), and a full-run reset clears all flags. |
| `retry_min_dwell` | seconds (≥0) | 0.5 | R6 — minimum time held in the empty scene on retry, measured from the key press. If the level reloads faster, the empty scene is held until this elapses; `0` disables the hold. |
| `retry_level_override_enabled` | true/false | false | R6.5 — use a fixed retry target instead of the current/campaign-start level. Disabled keeps the normal one-key retry behavior. |
| `retry_level_override` | string (English level name or Workshop numeric id) | (empty) | R6.5 — the one-key retry target. Case-insensitive name for BuiltIn/EditorPick levels, or a loaded Steam Workshop id. Invalid values show a red HUD hint when Retry is pressed and do not start a retry. When enabled and no level is active (e.g. the main menu), pressing Retry directly enters the specified level. |
| `show_hud` | true/false | true | R2.5.1 |
| `show_real_time` | true/false | true | R2.5.3 — show the always-active Real Time clock in the HUD (default shown below Game Time; can still be hidden) |
| `show_wake_up_time` | true/false | true | Show Wake Up Time in the right-hand HUD column, below Last Run when both are visible. By default it measures from the latest wake-up-relevant moment (level start, respawn, pause-menu checkpoint load, or pause-menu level restart) to the next wake-up |
| `only_record_first_wake_up_time` | true/false | false | R2.5.5 — restore the original Wake Up Time behavior: measure only the first wake-up after a level starts and ignore later respawns/checkpoint loads/level restarts. Visible in the settings panel only while `show_wake_up_time` is enabled |
| `center_loading_saving` | true/false | false | Move the game's own top-right "Loading"/"Saving" progress indicator to the top-center of the screen |
| `language` | BCP-47 code | en | matches a `lang/<code>.txt` |
| `category` | category id | any | R3.1 |
| `reset_key` | KeyCode | Backspace | reset-run keybind |
| `retry_key` | KeyCode | R | retry-level keybind |
| `menu_key` | KeyCode | Home | open/close the settings panel |

> **Pause/menu behavior is fixed**: time while paused is always counted, and
> menu/lobby time is never counted. There are no `count_in_pause` or
> `count_in_menu` settings.

> **Cheat/speed/drift detection (R5.1) is always on with hardcoded thresholds
> and is intentionally not configurable** — there is no `drift_tolerance` or any
> other anti-cheat option in `settings.ini`.

> **Tip:** instead of editing `settings.ini` by hand, press the **settings panel
> key** (default `Home`) in-game. Every option is editable there, changes apply
> live, and they are saved on panel close / game exit.

Key codes are Unity's `KeyCode` enum names, e.g. `Backspace`, `Home`, `R`,
`Keypad0`, `Alpha1`, `LeftControl`. Mouse side buttons (`Mouse3`–`Mouse6`) can
also be used as keybinds; mouse left/right buttons are intentionally not
bindable from the settings panel.

## tags.ini

HSRTimer has **no category presets**. The active rule set is just the set of
tags the user has enabled (toggled in the settings panel's Category page).

```ini
[tags]
enabled = Checkpoint, Jumpless
```

- `enabled` — comma-separated tag ids. Built-in ids: `Checkpoint`,
  `NoCheckpoint`, `Jumpless`, `Voiceline`, `Glitchless`, `NoEC`. Custom tags from
  third-party plugins use their own ids (see [EXTENDING.md](EXTENDING.md)).
  Leave empty for a plain run (generic validity checks only).

See [CATEGORIES.md](CATEGORIES.md).

## layout.ini

```ini
[text]
offset_x = 16
offset_y = 16
font_size = 18
color_a = FF5272FF
color_b = FF9A72FF

[rows]
0 = GameTime
1 = CurrentSegment
2 = LastSegment

[leaderboard]
font_size = 16
offset_x = 16
offset_y = 0
color_faster = 59FF66FF
color_slower = FF5959FF
color_tie = FFFFFFFF
mode = Subsegment
markers_time_mode = Relative

[custom.0]
x = 400
y = 50
text = {date} {time}
color_a = FFFFFFFF
color_b = CCCCCCCF

[custom.1]
x = 400
y = 80
text = Collection: {collection}
```

- `[text]` — the main text block is drawn directly on screen (no window, not
  draggable). `offset_x`/`offset_y` are the pixel offset from the top-left;
  `font_size` is the font size; `color_a`/`color_b` are the default two-color
  gradient (hex, see [HUD.md](HUD.md)).
- `[rows]` — ordered rows; keys are 0-based indices. Row types: `GameTime`,
  `RealTime`, `CurrentSegment`, `LastSegment`, `LastRun`, `CurrentState`.
  `RealTime` is also gated by the `show_real_time` setting (default on).
  Wake Up Time is not a row type — it renders in the right-hand column next to
  Last Run and is gated by `show_wake_up_time`.
- `[leaderboard]` — the shared leaderboard HUD (Subsegment / Markers modes).
  `font_size`, `offset_x`, `offset_y`, `color_faster`, `color_slower`,
  `color_tie`, `mode`, and `markers_time_mode` control its appearance and
  display mode. `offset_y` is relative to the fixed top anchor at the screen
  center; content extends downward.
- `[custom.<n>]` — arbitrary on-screen texts at `(x, y)` with their own gradient.
  Template variables: `{date}`, `{time}`, `{version}`, `{collection}`,
  `{category}`, `{gametime}`, `{realtime}`.

Show/hide of the whole timer is controlled by `show_hud` in `settings.ini`
(and the Toggle HUD keybind), not in `layout.ini`.

## settings.ini — [Subsegment]

Starting with R8, the `[Subsegment]` section is written into `settings.ini`
alongside the regular `[settings]` section (or may be added by hand). It is
managed by the same tolerant reader/writer.

```ini
[Subsegment]
Enable = true
PBPath = subsegment/pb
LoadPath = subsegment/load
ToggleKey = Tab
MultiProject = Any%
PlaneRadius = 50.0
MinMove = 0.5
SampleInterval = 1.0
QuietSettleSeconds = 0.5
PlaneDebounceSeconds = 0.2
RespawnJumpMeters = 100.0
MaxSamplesPerLevel = 480
MaxLeaderboardEntries = 8
DebugLogging = false
DisabledLeaderboardSources =
```

| Key | Default | Notes |
|-----|---------|-------|
| `Enable` | true | Master switch; disables sampling, loading, and the leaderboard. |
| `PBPath` | `subsegment/pb` | Relative paths resolve under `<config>/HSRTimer/`; absolute paths are accepted. Created automatically when a PB is written. |
| `LoadPath` | `subsegment/load` | Manually-placed reference samples. The directory is created automatically when the plugin loads, so it is ready for dropping reference samples into it. |
| `ToggleKey` | `Tab` | Cycle the shared leaderboard: hidden → Subsegment → Markers → hidden. |
| `MultiProject` | `Any%` | Initial multi-run project used for live ML comparisons (`Aztec%`/`Dark%`/`Steam%`/`Any%`). Within a session it can auto-upgrade along the containment chain (Aztec% → Dark% → Steam% → Any%) without writing back to config; if the chosen project has no data at all, it falls back to the smallest project that has data (session-only). PB writes still use the actual last-completed endpoint. |
| `PlaneRadius` | `50.0` | Virtual detection-plane radius in meters. |
| `MinMove` | `0.5` | Minimum sampled move distance; smaller moves become zero-displacement samples and do not build planes. |
| `SampleInterval` | `1.0` | Game-time seconds between subsegment samples. |
| `QuietSettleSeconds` | `0.5` | Quiet settle window for crossing candidates. |
| `PlaneDebounceSeconds` | `0.2` | Same-plane candidate debounce window. |
| `RespawnJumpMeters` | `100.0` | Continuity threshold; larger frame-to-frame sample jumps mean a failed/rewound segment is not treated as a stale loop. |
| `MaxSamplesPerLevel` | `480` | Cap on the cumulative number of samples recorded in one level. When the next sample would exceed it, sampling stops for the rest of the level and the buffered samples are discarded, so that level never contributes a PB. |
| `MaxLeaderboardEntries` | `8` | Maximum displayed leaderboard rows. |
| `DebugLogging` | false | Detailed subsegment logging (sample/load/plane/settle/PB writes). |
| `DisabledLeaderboardSources` | *(empty)* | Comma-separated display ids hidden from the leaderboard (`PB` = the PB entry; otherwise each top-level folder name under `LoadPath`). Empty shows everything. |

The shared leaderboard HUD's appearance and display mode (`font_size`,
`offset_x`, `offset_y`, colors, `mode`, `markers_time_mode`) live in
`layout.ini` `[leaderboard]`, not here. Old `Hud*`, `LeaderboardMode`, and
`LeaderboardTimeMode` keys in `settings.ini` are migrated to `layout.ini`
automatically and removed from `settings.ini` on the next write.

## settings.ini — [Markers]

Starting with R10, marker data and the marker module are configured by the
`[Markers]` section in `settings.ini` (same tolerant reader/writer):

```ini
[Markers]
Enable = true
EditMode = false
Path = markers
DebugLogging = false
OverlayFillColor = 3F7FFF66
OverlayLabelColor = FFFFFFFF
```

| Key | Default | Notes |
|-----|---------|-------|
| `Enable` | true | Master switch; disables trigger recording, PB writes, and marker leaderboard/overlay display. |
| `EditMode` | false | Marker edit mode: shows the in-game overlay (R10.6), the conspicuous HUD hint at the bottom of the timer HUD, and the XYZ axis indicator below it; it also unlocks the panel's edit controls. Persisted, so it survives restarts. |
| `Path` | `markers` | Directory for marker definition + PB files (`<config>/HSRTimer/markers`). Relative paths resolve under `<config>/HSRTimer/`; absolute paths are accepted. |
| `DebugLogging` | false | Detailed marker logging (level key, marker count, triggers, object resolution, PB writes). |
| `OverlayFillColor` | `3F7FFF66` | Fill color (with alpha) of the translucent range cubes and grab-object highlights. |
| `OverlayLabelColor` | `FFFFFFFF` | Color of the marker name labels in the edit-mode overlay. |

The leaderboard time-display setting (`markers_time_mode`) is part of the
shared leaderboard HUD config in `layout.ini` `[leaderboard]`, not here.

Marker data files live under `<config>/HSRTimer/markers/{level}/{category}.json`
(the level key uses the same scheme as subsegment IL ids: English localized
name for BuiltIn/EditorPick levels, the numeric Workshop id, or the local level
folder name when it has no id; the category key follows R8.2.4). There is no
load/import directory — markers only store your own PB.

## settings.ini — [Presets]

Starting with R11, the currently selected preset is stored as a normal config
item in `settings.ini`:

```ini
[Presets]
Current = default
```

| Key | Default | Notes |
|-----|---------|-------|
| `Current` | `default` | The currently selected preset name. Saved/loaded with the rest of the config. |

## presets/

Starting with R11, layout + markers presets live under
`<config>/HSRTimer/presets/`:

```
presets/
  default/
    layout.ini
    markers/
  <name>/
    layout.ini
    markers/
```

A preset is a snapshot of `layout.ini` plus the whole `markers/` directory
(marker definitions and PB records). On first load or when upgrading from an
older version, the plugin creates the `default` preset from the current config
and selects it; `default` cannot be deleted. Manage presets from the
**General** tab of the settings panel (see [PANEL.md](PANEL.md)).

## lang/*.txt

See [LOCALIZATION.md](LOCALIZATION.md).
