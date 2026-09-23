# Changelog

This file contains user-facing release notes for HSRTimer. Only changes that plugin users can observe belong here.

## 0.0.0

- **Release Date:** Unreleased
- **Highlights:** _To be filled during version branch preparation._
- **Details:**
  - Added an in-game dev console command set (`hsr ...`, opened with `~` / `F1`) so every feature — timer, reset/retry, settings, tags, HUD/layout, localization, presets, subsegments, markers, validity flags, and LevelCollections integration — can be inspected and tested from inside the game. See `TESTS.md`.
  - Fixed the manual reset and one-key retry hotkeys still firing while a text input was open (for example the in-game chat), which could silently reset or restart a run while typing.
  - Fixed the settings panel showing a horizontal scrollbar when the "Specify retry level" option is enabled — the input row was wider than the panel's scroll area.
  - Removed the Save and Close buttons and the hint line from the bottom of the settings panel. Edits still apply live and are written to disk when the panel is closed (settings key) or the game exits.
  - Added an **About** tab as the first entry in the settings panel navigation, showing the plugin name, version, the first two lines of the MIT license, and a **GitHub Repository** button that opens the project page. The `hsr about` console command prints the same information.
  - Added a **Check Update** button on the About tab that checks GitHub Releases (via the project's releases feed, so it works without GitHub API rate limits): it shows the latest release's title and a short summary (release date + Highlights), an **Open Release Page** button for the full notes, and can download and install the new plugin DLL (replacing older copies), prompting you to restart the game after a successful update. The `hsr update ...` console commands expose the same flow for testing.
- **Contributors:** _To be filled from PRs merged into dev._

## 1.5.0

- **Release Date:** _21 Sep 2026_
- **Highlights:**
  - Added **Presets** for HUD layout and marker snapshots.
  - Added the `NoEC` tag with extended climbing detection. Note that EC is `soft invalid flag`, which means that it wont mark the run as invalid since it might misjudge, final judgement should be made by human.
- **Details:**
  - Moved the shared leaderboard HUD appearance and display-mode settings from `settings.ini` into `layout.ini` under a new `[leaderboard]` section; existing settings are migrated automatically.
  - Added **Presets**: save and switch whole HUD layout + marker snapshots from the General tab. The built-in `default` preset is created automatically on first load or when upgrading from an older version; a preset can be created, selected, loaded, saved, and (except `default`) deleted. The current selection is stored in `settings.ini` under `[Presets] Current`.
  - Added **soft invalid flags**: soft flags render on their own HUD line in normal text color with a trigger count (e.g. `EC x3`), flash red on each new trigger, are cleared by one-key retry and a full timer reset, and are not cleared by pause-menu restart.
  - Added the `NoEC` tag to the Category page with extended climbing detection: when the player leaves the ground, the first grab height of that airborne period is the baseline; any new grab more than 0.2 m above it is EC (debounced to at most once per 0.2 s). The `Ec` violation is a soft flag.

## 1.4.0

- **Release Date:** _19 Sep 2026_
- **Highlights:**
  - Added the `Glitchless` category tag, including SSG, Prop Fly, and Footsie detection.
- **Details:**
  - Pressing the manual reset key while a level is already playing now clears and stops the timer instead of immediately starting a new segment; the Wake Up Time display is also cleared, forgivable invalid flags stay cleared, and leaving the level no longer re-triggers those invalid checks.
  - Added a configurable cap (`MaxSamplesPerLevel`, default 480) on the number of subsegment samples recorded in one level; once a level exceeds the cap, sampling stops for that level and its buffered samples are discarded so the level does not count toward a PB.
  - Added the `Glitchless` tag to the Category page, with SSG, Prop Fly, and Footsie detection.

## 1.3.1

- **Release Date:** _12 Sep 2026_
- **Highlights:**
  - Fixed subsegment and marker persistence.
  - Fixed the Voiceline tag.
- **Details:**
  - Subsegment and marker PBs are no longer recorded when a run has any invalid flag (e.g. cheat codes, skipped checkpoints, missed voicelines), including invalid flags that are only detected at level exit.
  - The Voiceline tag now shows a live `Voice: triggered/total` progress line in the HUD, similar to the Checkpoint tag display.
  - The Voiceline tag's old "all voicelines triggered" green hint was removed; the progress line is now the only Voiceline display.
  - Fixed hidden easter-egg voicelines being included in the Voiceline total but not incrementing the triggered count when played.

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
