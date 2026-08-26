# Settings Panel

> **中文版**: [zh/PANEL.md](zh/PANEL.md)

The settings panel (open/close with the **settings key**, default `Home`) edits
every user-tunable option live — changes take effect immediately and are saved
to disk when the panel is closed or the game exits. It is organized into three
tabs.

> Cheat/speed/drift detection (R5.1) is always on with hardcoded thresholds and
> is intentionally **not** exposed anywhere in the panel.

## General

- **Timing** — `auto_reset`,
  `restart_clears_forgivable` (clears forgivable flags on a pause-menu
  restart; see [CONFIG.md](CONFIG.md)). Pause time is always counted and
  menu/lobby time is never counted; there are no toggles for them.
- **Language** — pick the active language from the loaded set (single-select).
  "Reload language files" re-scans `lang/*.txt`.
- **Keybinds** — reset / retry / settings keys. To rebind: click the field, then
  press the desired key. Pure modifier presses are ignored.

## Interface

- **HUD** — `show_hud`; `center_loading_saving` (moves the game's own top-right
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

See [CATEGORIES.md](CATEGORIES.md) for what each tag does and
[CHECKPOINTS.md](CHECKPOINTS.md) for the Checkpoint tag's rules.
