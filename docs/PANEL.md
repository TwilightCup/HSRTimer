# Settings Panel

> **中文版**: [zh/PANEL.md](zh/PANEL.md)

The settings panel (open/close with the **settings key**, default `Home`) edits
every user-tunable option live — changes take effect immediately and are saved
to disk when the panel is closed or the game exits. It is organized into the
built-in tabs below, plus any **extra tabs registered by other plugins** via
`ISettingsPanelTab` (see [EXTENDING.md](EXTENDING.md)).

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
  "Reload language files" re-scans `lang/*.txt`.
- **Keybinds** — reset / retry / settings / subsegment leaderboard toggle keys.
  To rebind: click the field, then press the desired key. Pure modifier presses
  are ignored. Mouse side buttons (`Mouse3`–`Mouse6`) can also be bound; mouse
  left/right buttons remain reserved for normal UI use.

## Interface

- **HUD** — `show_hud`; `show_real_time` (show the always-active Real Time
  clock); `show_wake_up_time` (show Wake Up Time in the right-hand HUD column);
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
  only (Any% ⊃ Steam% ⊃ Dark% ⊃ Aztec%) without changing the saved setting. The
  leaderboard toggle key is in **General → Keybinds**; the leaderboard
  appearance settings live on the **Leaderboard** tab below.

## Leaderboard

- **HUD** — the subsegment leaderboard font size, X offset, and Y offset
  (relative to automatic vertical centering). These were moved here from the
  Subsegment tab.
- **Entry colors** — three user-configurable colors for the three leaderboard
  entry states: faster/ahead (default green), slower/behind (default red), and
  tie/no-data (default white, shown as `--`). All colors include RGBA sliders
  and a hex input.
- **Displayed sources** — at the bottom, a toggle for every subsegment source:
  **PB** and each top-level folder under the load directory. Only checked
  sources appear on the leaderboard. The list is still truncated to
  `MaxLeaderboardEntries` after filtering and sorting.

See [CATEGORIES.md](CATEGORIES.md) for what each tag does and
[CHECKPOINTS.md](CHECKPOINTS.md) for the Checkpoint tag's rules.
