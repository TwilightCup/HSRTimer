# Changelog

This file contains user-facing release notes for HSRTimer. Only changes that plugin users can observe belong here.

## 0.0.0

- **Release Date:** Unreleased
- **Highlights:** _To be filled during version branch preparation._
- **Details:**
  - Subsegment and marker PBs are no longer recorded when a run has any invalid flag (e.g. cheat codes, skipped checkpoints, missed voicelines), including invalid flags that are only detected at level exit.
  - The Voiceline tag now shows a live `Voice: triggered/total` progress line in the HUD, similar to the Checkpoint tag display.
- **Contributors:** _To be filled from PRs merged into dev._

## 1.3.0

- **Release Date:** _11 Sep 2026_
- **Highlights:**
  - New **Markers** feature: create per-level trigger markers, record their trigger times, and compare them against your local PB.
  - Added a wake-up time display mode that restarts on respawns and pause-menu loads/restarts, with an optional "only record first wake-up time" setting.
  - Optimized the leaderboard HUD with a fixed top edge, a Subsegment/Markers content switch, and a one-key mode cycle.
- **Details:**
  - Wake Up Time now restarts its measurement whenever the player respawns, loads a checkpoint from the pause menu, or restarts the level from the pause menu, so it shows how long the latest respawn took to get up; a new "Only record first wake-up time" option (visible only while Wake Up Time display is on) restores the previous first-wake-up-only behavior.
  - New **Markers** feature: create per-level trigger markers (range / checkpoint / grab-object) from a new **Markers** settings tab (Main Dreams / Extra Dreams / Workshop), record each marker's first segment-time trigger, and compare it against the level's own local PB (no external data import). A marker edit mode shows a HUD hint plus in-game blue translucent cubes and grab-object highlights. The leaderboard HUD gains a content switch (Subsegment / Markers) and shows a newest-first marker feed with absolute or relative times; faster/slower colors apply in both modes.
  - The leaderboard top edge is now fixed at the screen center (plus the configured Y offset) so rows always extend downward instead of re-centering when the number of entries changes.
  - The leaderboard toggle key now cycles the shared HUD through hidden → Subsegment → Markers → hidden, so you can switch between the two content modes without opening the settings panel.
  - Settings-panel dropdown buttons now show expand/collapse arrows, making open/closed state clearer.

## 1.2.2

- **Release Date:** _10 Sep 2026_
- **Highlights:**
  - Fixed subsegment leaderboard ordering, IL comparisons, and EditorPick/Workshop PB persistence.
  - Fixed one-key retry so menu-entered EditorPick and Workshop levels retry the current level, with repeated Workshop retries working reliably.
- **Details:**
  - The subsegment leaderboard now lists settled entries from slowest to fastest (descending diff), keeping no-data entries at the bottom.
  - In IL mode the subsegment leaderboard compares against the current segment time instead of cumulative game time, so IL references stay correct even during a multi-level run.
  - Passing a level now keeps the subsegment leaderboard on screen until the next level settles its first subsegment, in both single-level (IL) and multi-run (ML) modes.
  - Fixed subsegment PB persistence for EditorPick and Workshop levels: they previously collapsed into a single `E-1` / `W-1` record that overwrote every level, and are now stored under their own per-level id.
  - Subsegment level IDs now follow the game's own identification scheme: single-level (IL) records use the English localized level name for BuiltIn and EditorPick levels and the raw numeric workshop id for Workshop levels (multi-run per-level files keep their `B{number}` timeline numbering).
  - Fixed one-key retry so EditorPick and Workshop levels entered from the menu retry the current level instead of jumping back to a previously played built-in level; Workshop retries now preserve the full Steam Workshop id and keep working on every press.

## 1.2.1

- **Release Date:** _09 Sep 2026_
- **Highlights:**
  - Improved integration for other plugins.
- **Details:**
  - Settings-panel tabs from other plugins can now follow HSRTimer's language selection, with English as the mandatory fallback language.
  - Other plugins can now subscribe to `SettingsPanelTabRegistry.SettingsSaved` to persist their own settings whenever HSRTimer saves its configuration.

## 1.2.0

- **Release Date:** _08 Sep 2026_
- **Highlights:**
  - Mouse side buttons can now be bound to hotkeys.
  - Improved the subsegment leaderboard experience.
- **Details:**
  - The settings panel can now bind mouse side buttons (Mouse3–Mouse6).
  - Other plugins can now register one IMGUI configuration tab each in the settings panel.
  - Added a Leaderboard settings tab with the subsegment HUD appearance options and three configurable entry-state colors (faster/ahead, slower/behind, tie/no-data).
  - Added per-source display toggles on the Leaderboard tab so PB and each load-directory reference can be individually hidden/shown on the subsegment leaderboard.
  - Moved the subsegment leaderboard toggle key into General → Keybinds.
  - The subsegment leaderboard now shows a title row at the top.
  - During a multi-run, the subsegment leaderboard starts from the configured category and can auto-upgrade for the current session when the run passes that category's endpoint (Aztec% → Dark% → Steam% → Any%); the switch occurs at the first settled subsegment of the new level.
  - When entering multi-run mode, if the selected category (other than Aztec%) has no data at all, the leaderboard falls back to the smallest category that has data for the current session (Aztec% → Dark% → Steam% → Any%).

## 1.1.0

- **Release Date:** _06 Sep 2026_
- **Highlights:**
  - Redesigned the settings panel with a language dropdown, left-side navigation, and clearer input fields.
  - Added a "Specify retry level" option so one-key retry can target a chosen level by English name (e.g., Mansion. Case-insensitive) or Workshop ID, and can be triggered directly from the menu.
- **Details:**
  - Settings panel input descriptions now appear next to the fields.
  - Added a language dropdown to the settings panel; the configured language name is honored.
  - Category tabs moved to the left sidebar and the settings panel was widened.
  - Updated the default HUD gradient colors.
  - Grouped subsegment detailed parameters under their own section.
  - Added an optional "Specify retry level" setting to retry (or directly enter from the menu) a chosen level by English name or Workshop ID.
  - The subsegment load directory is now created automatically when the plugin loads.
