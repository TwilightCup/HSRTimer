# Changelog

This file contains user-facing release notes for HSRTimer. Only changes that plugin users can observe belong here.

## 0.0.0

- **Release Date:** Unreleased
- **Highlights:** _To be filled during version branch preparation._
- **Details:**
  - The settings panel can now bind mouse side buttons (Mouse3–Mouse6); mouse left/right buttons remain un-bindable.
  - Other plugins can now register one IMGUI configuration tab each in the settings panel.
  - Added a Leaderboard settings tab (below Subsegment) with the subsegment HUD appearance options and three configurable entry-state colors (faster/ahead, slower/behind, tie/no-data).
- **Contributors:** _To be filled from PRs merged into dev._

## 1.1.0

- **Release Date:** *06 Sep 2026*
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
