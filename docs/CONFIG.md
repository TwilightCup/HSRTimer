# Configuration

> **中文版**: [zh/CONFIG.md](zh/CONFIG.md)

All config lives under `<BepInEx config dir>/HSRTimer/` (on a typical install,
`~/Library/Application Support/Steam/steamapps/common/Human Fall Flat/BepInEx/config/HSRTimer/`).
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
show_hud = true
show_real_time = true
show_wake_up_time = true
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
| `show_hud` | true/false | true | R2.5.1 |
| `show_real_time` | true/false | true | R2.5.3 — show the always-active Real Time clock in the HUD (default shown below Game Time; can still be hidden) |
| `show_wake_up_time` | true/false | true | Show the Wake Up time — from level start to the first time the local player leaves the soft/spawn state — in the right-hand HUD column, below Last Run when both are visible |
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
`Keypad0`, `Alpha1`, `LeftControl`.

## tags.ini

HSRTimer has **no category presets**. The active rule set is just the set of
tags the user has enabled (toggled in the settings panel's Category page).

```ini
[tags]
enabled = Checkpoint, Jumpless
```

- `enabled` — comma-separated tag ids. Built-in ids: `Checkpoint`,
  `NoCheckpoint`, `Jumpless`, `Voiceline`. Custom tags from third-party plugins
  use their own ids (see [EXTENDING.md](EXTENDING.md)). Leave empty for a plain
  run (generic validity checks only).

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
MaxLeaderboardEntries = 8
DebugLogging = false
HudFontSize = 16
HudOffsetX = 16
HudOffsetY = 0
```

| Key | Default | Notes |
|-----|---------|-------|
| `Enable` | true | Master switch; disables sampling, loading, and the leaderboard. |
| `PBPath` | `subsegment/pb` | Relative paths resolve under `<config>/HSRTimer/`; absolute paths are accepted. Created automatically when a PB is written. |
| `LoadPath` | `subsegment/load` | Manually-placed reference samples. Missing directory silently means no external references. |
| `ToggleKey` | `Tab` | Show/hide the subsegment leaderboard. |
| `MultiProject` | `Any%` | Multi-run project used for live ML comparisons (`Aztec%`/`Dark%`/`Steam%`/`Any%`). PB writes still use the actual last-completed endpoint. |
| `PlaneRadius` | `50.0` | Virtual detection-plane radius in meters. |
| `MinMove` | `0.5` | Minimum sampled move distance; smaller moves become zero-displacement samples and do not build planes. |
| `SampleInterval` | `1.0` | Game-time seconds between subsegment samples. |
| `QuietSettleSeconds` | `0.5` | Quiet settle window for crossing candidates. |
| `PlaneDebounceSeconds` | `0.2` | Same-plane candidate debounce window. |
| `RespawnJumpMeters` | `100.0` | Continuity threshold; larger frame-to-frame sample jumps mean a failed/rewound segment is not treated as a stale loop. |
| `MaxLeaderboardEntries` | `8` | Maximum displayed leaderboard rows. |
| `DebugLogging` | false | Detailed subsegment logging (sample/load/plane/settle/PB writes). |
| `HudFontSize` | 16 | Subsegment leaderboard font size, independent of the main timer HUD. |
| `HudOffsetX` | 16 | Left edge of the subsegment leaderboard. |
| `HudOffsetY` | 0 | Vertical offset from the automatic left-middle centering. |

## lang/*.txt

See [LOCALIZATION.md](LOCALIZATION.md).
