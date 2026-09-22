# TESTS

This document explains how to test every HSRTimer feature from inside the game
using the built-in dev console (default keys **`~`** or **F1**).

## What this gives you

HSRTimer registers an `hsr ...` command family with the game's `Shell` console
at plugin load. You can inspect live state, toggle settings, mutate config,
switch tags, edit the HUD layout, manage presets, exercise subsegments and
markers, and simulate validity flags — all without restarting the game or
editing files by hand.

All `hsr` commands are safe to run from the console. Most mutations call the
same `ConfigService.SaveSettings()` path used by the settings panel, so they
persist to the normal `settings.ini` / `tags.ini` / `layout.ini` files.

## Opening the console

1. Launch Human: Fall Flat with HSRTimer installed.
2. Press **`~`** (backquote) or **F1** to open the game's dev console.
3. Type `hsr` and press Enter to see the command list.
4. Type `hsr help <topic>` for detailed help on one command.

> If the console does not appear, the game version may have moved the key
> binding. The plugin still logs "HSRTimer: dev-console commands registered" to
> the BepInEx log at startup.

> **Note on case:** the game's console lowercases the whole line before a
> command runs. HSRTimer therefore treats identifiers (tag ids, language codes,
> preset names, leaderboard modes) case-insensitively and canonicalizes them.
> Free-text values (e.g. a custom text or a marker name) are stored as typed by
> the console, i.e. lowercased.

## Command reference

| Command | Description |
|---|---|
| `hsr` | Print the full command summary |
| `hsr help [topic]` | Print help for one topic |
| `hsr status` | Dump live timer/config/HUD/subsegment/marker/LC state |
| `hsr keys` | List every settable settings/layout key |
| `hsr get <key>` / `hsr get all` | Read one config value, or all values |
| `hsr set <key> <value>` | Set and save a config value |
| `hsr reload` | Re-read all config + language files from disk |
| `hsr save` | Save current in-memory config |
| `hsr reset` | Full-run reset (same as the reset key) |
| `hsr retry` | One-key retry (same as the retry key, R6) |
| `hsr hud [on\|off\|toggle\|status]` | Control timer HUD visibility |
| `hsr panel [open\|close\|toggle\|status]` | Control the settings panel |
| `hsr leaderboard [cycle\|show\|hide\|mode <Subsegment\|Markers>\|status]` | Control the leaderboard HUD |
| `hsr layout [status\|row ...\|text ...\|get <key>\|set <key> <value>]` | Inspect/edit the HUD layout |
| `hsr tag [list\|enable <id>\|disable <id>\|set <id> <on\|off>]` | Toggle enabled tag rules |
| `hsr lang [list\|set <code>\|reload\|current]` | Manage localization |
| `hsr preset [list\|current\|create <name>\|apply [name]\|save\|delete <name>]` | Manage presets (R11) |
| `hsr sub [status\|entries\|clear]` | Inspect/clear the subsegment module |
| `hsr marker [list\|feed\|add ...\|remove <id>\|toggle <id>\|pb <ms>\|pbclear\|clear\|save\|reload]` | Inspect/edit markers (R10) |
| `hsr flags [list\|raise <Reason>\|clear [forgivable\|soft\|all]]` | Inspect/mutate validity flags (R5) |
| `hsr lc [status\|restart]` | Inspect LevelCollections integration or dispatch `lc restart` |
| `hsr config [path\|files]` | Print HSRTimer config paths |

## Keys accepted by `hsr get` / `hsr set`

The command uses snake_case versions of the public fields in `SettingsModel`
and `LayoutModel`. Common examples:

- `auto_reset`, `restart_clears_forgivable`, `retry_min_dwell`
- `retry_level_override_enabled`, `retry_level_override`
- `show_hud`, `show_real_time`, `show_wake_up_time`
- `only_record_first_wake_up_time`, `center_loading_saving`, `language`
- `reset_key`, `retry_key`, `menu_key`
- `subsegment_enable`, `subsegment_pb_path`, `subsegment_load_path`,
  `subsegment_toggle_key`, `subsegment_multi_project`,
  `subsegment_debug_logging`
- `markers_enable`, `markers_edit_mode`, `markers_path`,
  `markers_debug_logging`, `markers_overlay_fill_color`,
  `markers_overlay_label_color`
- Layout: `offset_x`, `offset_y`, `font_size`, `color_a`, `color_b`,
  `leaderboard_font_size`, `leaderboard_offset_x`, `leaderboard_offset_y`,
  `leaderboard_mode`, `leaderboard_markers_time_mode`

Boolean values accept `true/false`, `1/0`, `on/off`, `yes/no`. KeyCodes accept
Unity `KeyCode` names (e.g. `Backspace`, `R`, `Home`, `Tab`). Colors accept
`RRGGBB` or `RRGGBBAA` hex.

## Feature-by-feature test guide

### 1. Timer core / run controls

```text
hsr status                 # see game/app state, game time, segment, real time
hsr reset                  # verify timers zero and flags clear
hsr retry                  # verify one-key retry reloads the level
```

While playing a level, `hsr status` should show `segment=True`, `timing=True`
and an increasing `gameTime`. After `hsr reset`, `gameTime` should be `0` and
`inSegment` should be `False` (or the transition cache is preserved so the
level does not restart).

### 2. Validity flags (R5)

```text
hsr flags list
hsr flags raise CheatCode            # unforgivable
hsr flags raise CheckpointSkip       # forgivable
hsr flags raise Ec                   # soft, increments count
hsr flags clear forgivable
hsr flags clear soft
hsr flags clear all
```

`hsr status` should show the hard reasons in the `flags:` line and soft flags
as `Ec xN`.

### 3. Tags / category (R3)

```text
hsr tag list
hsr tag enable Checkpoint
hsr tag enable Jumpless
hsr tag set NoEC on
hsr tag disable Jumpless
hsr tag set NoCheckpoint off
hsr status          # enabled tags are listed
```

Tag changes persist to `tags.ini` immediately.

### 4. HUD / layout (R2)

```text
hsr hud off
hsr hud on
hsr layout status
hsr layout row list
hsr layout row add CurrentState
hsr layout row remove 5
hsr layout text add 20 400 "Hello {gametime}"
hsr layout text list
hsr layout set font_size 24
hsr layout set offset_x 30
hsr layout set color_a FF0000FF
```

The HUD should update on the next frame and the changes should persist to
`layout.ini`.

### 5. Settings panel / general settings

```text
hsr panel open
hsr panel close
hsr set auto_reset false
hsr set auto_reset true
hsr set language zh-Hans
hsr lang list
hsr lang set en
hsr reload
```

`hsr panel open` should show the same IMGUI settings panel as the Home key.

### 6. Presets (R11)

```text
hsr preset list
hsr preset create test-preset
hsr layout set font_size 30
hsr preset save
hsr preset apply default
hsr preset apply test-preset
hsr preset delete test-preset
```

After `apply`, the HUD should reflect the preset's saved layout/markers.

### 7. Subsegment module (R8)

```text
hsr sub status
hsr sub entries
hsr sub clear
```

`hsr sub status` prints the enabled flag, paths, multi-run state and current
leaderboard entry count. `hsr sub entries` lists each loaded reference and its
latest settled diff.

### 8. Markers (R10)

Enter a level, then:

```text
hsr marker list
hsr marker add range "Test Box"          # uses player position, 2m box
hsr marker add checkpoint "CP1" 1
hsr marker add grab "My Box"             # requires currently grabbing exactly one object
hsr marker toggle m1
hsr marker pb 12345
hsr marker list
hsr marker save
hsr marker reload
hsr marker clear
```

The marker overlay/feed should react to these changes when edit mode is enabled
(`hsr set markers_edit_mode true`).

### 9. Localization (R7)

```text
hsr lang list
hsr lang set zh-Hans
hsr lang set en
hsr lang reload
```

The settings panel and HUD labels should switch language immediately.

### 10. Leaderboard HUD

```text
hsr leaderboard status
hsr leaderboard show
hsr leaderboard mode Markers
hsr leaderboard mode Subsegment
hsr leaderboard hide
hsr leaderboard cycle
```

### 11. LevelCollections integration (optional)

```text
hsr lc status
hsr lc restart
```

`hsr lc status` reports whether the LC integration is enabled, whether a
collection run is active, and the current collection name. `hsr lc restart`
dispatches the same `lc restart` command used by the retry delegation.

### 12. Config file locations

```text
hsr config path
hsr config files
```

These print the exact paths used by the plugin so you can verify or edit files
on disk.

## Test checklist

- [ ] `hsr` prints the command summary.
- [ ] `hsr status` shows plausible live values while in a level.
- [ ] `hsr reset` zeroes timers and clears flags.
- [ ] `hsr retry` reloads the current level (or the configured override).
- [ ] `hsr hud off/on` hides/shows the timer HUD.
- [ ] `hsr panel open/close` opens/closes the settings panel.
- [ ] `hsr tag enable/disable` changes the enabled tags and persists them.
- [ ] `hsr set language zh-Hans` switches UI language.
- [ ] `hsr layout row add/remove` changes the HUD rows.
- [ ] `hsr preset create/save/apply` round-trips layout + markers.
- [ ] `hsr sub status/entries` works with subsegment data present.
- [ ] `hsr marker add/list/toggle/pb` works while in a level.
- [ ] `hsr flags raise/clear` shows the expected HUD banner / soft-flag line.
- [ ] `hsr lc status` reports correctly with LevelCollections installed or absent.
