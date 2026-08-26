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
color_a = FFD950FF
color_b = FFF299FF

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
  `CurrentSegment`, `LastSegment`, `LastRun`, `CurrentState`.
- `[custom.<n>]` — arbitrary on-screen texts at `(x, y)` with their own gradient.
  Template variables: `{date}`, `{time}`, `{version}`, `{collection}`,
  `{category}`, `{gametime}`.

Show/hide of the whole timer is controlled by `show_hud` in `settings.ini`
(and the Toggle HUD keybind), not in `layout.ini`.

## lang/*.txt

See [LOCALIZATION.md](LOCALIZATION.md).
