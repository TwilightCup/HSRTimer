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

## About

- **Plugin identity** — the plugin name (`HSRTimer`) and the current version
  number, on the first two lines.
- **License** — the first two lines of the MIT license (`MIT License`,
  `Copyright (c) 2026 TwilightCup`). This is legal text and is **not**
  translated.
- **GitHub Repository** — opens
  <https://github.com/TwilightCup/HSRTimer> in the system browser.
- **Check Update** (R13) — reads the project's GitHub releases feed
  (`github.com/.../releases.atom`) for the newest stable release of the plugin
  (this avoids the GitHub API's unauthenticated rate limit and keeps working
  where `api.github.com` is blocked). A failed check shows a one-line error
  under the button; being up to date shows one confirmation line; a newer
  release shows its title, a short notes summary (release date + Highlights
  only — the Details/Contributors sections are trimmed), an **Open Release
  Page** button that opens the release's GitHub page, and an **Update** button.
  Clicking **Update** downloads the release's `HSRTimer-v{version}.dll` asset
  (the URL is derived from the release tag), verifies it is a valid assembly,
  places it in the plugins folder next to the running copy, and retires older
  `HSRTimer-v*.dll` files (deleting them, or renaming them
  to BepInEx's `.dis` disabled suffix when they are locked; anything still
  locked is cleaned up at the next launch). After a successful install the
  panel shows a plain-text prompt to restart the game (no auto-quit button).
  All download/install failures are caught and shown as an error line; the
  update flow never touches other plugins' DLLs. The notes summary is rendered
  as plain text (HTML stripped).

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
- **Presets** — save and switch whole `layout.ini` + markers snapshots. The
  dropdown below **Language** lists all presets (`default` is always pinned to
  the top); the **Load preset** button
  applies the selected snapshot to the live layout/markers, and **Save preset**
  writes the current layout/markers into the selected preset. **New preset**
  saves the current config under a new name and selects it. **Delete preset**
  (hidden for `default`) asks for confirmation: the button expands into a
  **Confirm delete** / **Cancel** pair, and the confirmation is dismissed when
  you switch tabs or close/reopen the panel. The current
  selection is persisted in `settings.ini` (`[Presets] Current`). The built-in
  `default` preset is created automatically on first load/upgrade and cannot be
  deleted. See [CONFIG.md](CONFIG.md).
- **Keybinds** — reset / retry / settings / leaderboard mode-cycle keys. The
  leaderboard mode-cycle key cycles the shared leaderboard HUD through hidden →
  the available content modes → hidden; a mode whose module is disabled (user
  setting or an auto-disable mechanism, e.g. the co-op client gate) is skipped,
  so with subsegment off the cycle is only hidden ↔ Markers. To rebind: click
  the field, then press the desired key. Pure modifier presses are ignored.
  Mouse side buttons (`Mouse3`–`Mouse6`) can also be bound; mouse left/right
  buttons remain reserved for normal UI use.

## Interface

- **Center Loading/Saving** — at the top of the page:
  `center_loading_saving` moves the game's own top-right "Loading"/"Saving"
  prompts to the top-center.
- **Timer HUD** — a button that opens the timer HUD sub-page (marker-style
  drill-down with a **Back** button at the top):
  - **HUD general settings** — `show_hud`; the main text block's offset
    (`offset_x`, `offset_y`), `font_size`, and the two-color gradient
    (`color_a`, `color_b`) with per-channel RGBA sliders; and
    `only_record_first_wake_up_time` (restores the original
    first-wake-up-only behavior).
  - **Columns** — one collapsible dropdown button per column (columns are
    drawn left-to-right by number; an empty column is not displayed). Each
    dropdown lists every timer HUD row type (**Game Time**, **Real Time**,
    **Prev RT**, **Segment Time**, **Total at Last Segment**, **Last
    Segment**, **Last Run**, **Wake Up Time**, **State**) with an integer
    **position** field: `0` hides the row, and a value `N > 0` shows it at
    the N-th line of that column (so the same field both enables and orders
    the row). Entering a position already used by another row replaces that
    row. Each dropdown has a **Delete** button (with confirmation, like the
    marker editor), and the page ends with a **New column** button that
    appends an empty column.
    Real Time and Wake Up Time are regular rows here — they show wherever you
    place them, so the old `show_real_time` / `show_wake_up_time` toggles are
    gone (the Real Time clock itself always runs in the background).
- **Leaderboard** — a button that opens the shared leaderboard HUD sub-page
  (same marker-style drill-down with a **Back** button at the top):
  - **Content** — choose what the shared leaderboard HUD shows: **Subsegment**
    (reference time comparison) or **Markers** (the current level's marker
    feed). The mode-cycle key (in **General → Keybinds**) and the appearance
    settings below apply to both modes.
  - **HUD** — the leaderboard font size, X offset, and Y offset (relative to
    the fixed top anchor at the screen center; content extends downward).
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
- **Custom Text** — a button that opens the custom-text sub-page (same
  marker-style drill-down with a **Back** button at the top). Each custom text
  (the `[custom.<n>]` entries in `layout.ini`) is listed as a collapsible
  dropdown; expanding one shows all of its configuration: the **Content** text
  input (template variables such as `{gametime}`, `{date}`, `{version}`,
  `{collection}`, `{category}` are substituted live), a **Font size** slider,
  the **Offset X** / **Offset Y** position fields, and the two-color gradient
  (**Color A** / **Color B**) with per-channel RGBA sliders. Each dropdown has a
  **Delete** button (with confirmation, like the column and marker editors), and
  the page ends with a **New text** button that appends an empty text set to
  `(0, 0)` and opens it.

## Category

- **Rule tags** — a multi-select of every registered rule tag (the six built-in
  tags: `Checkpoint`, `NoCheckpoint`, `Jumpless`, `Voiceline`, `Glitchless`, `NoEC`, plus
  any custom tags registered by other plugins — see [EXTENDING.md](EXTENDING.md)).
  Checking a tag enables it; unchecking disables it. There are no category
  presets — this tag set *is* the active rule set. Changes are live and
  persisted to `tags.ini` on close/exit.

## Subsegment

- **Subsegment** — enable/disable subsegment time comparison, PB and manual-load
  sample paths, the initial multi-run project, and the detailed detection/settle
  parameters. During a multi-run the project can auto-upgrade for that session
  only (Any% ⊃ Steam% ⊃ Dark% ⊃ Aztec%) without changing the saved setting; if
  the selected project has no data at all, it falls back to the smallest
  project that has data (session-only). The leaderboard mode-cycle key is in
  **General → Keybinds**; the leaderboard appearance settings live on the
  **Interface → Leaderboard** sub-page.

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
