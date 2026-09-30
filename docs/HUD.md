# HUD

> **中文版**: [zh/HUD.md](zh/HUD.md)

The timer is an IMGUI text overlay drawn **directly on screen** — no window,
no background, not draggable. Its rows can be freely arranged, and text uses a
per-character two-color gradient.

## Row types

Each row renders one value:

| Row type | Shows |
|----------|-------|
| `GameTime` | Total game time accumulated this run |
| `RealTime` | Wall-clock time for the current run (shown by default in the second column; see below) |
| `PrevRt` | **Prev RT**: the Real Time clock value frozen at the moment the previous level completed — the real-time counterpart of `TotalAtLastSegment` (a cumulative snapshot of the run, not that level's own real duration). Defaults directly below `RealTime` in the second column |
| `CurrentSegment` | Time since entering the current level |
| `TotalAtLastSegment` | Run total frozen at the moment the last segment completed |
| `LastSegment` | Duration of the last completed level |
| `LastRun` | Total time of the last complete run — a regular row like any other (defaults into the third column), shown only while idle (hidden once a new run starts timing) |
| `WakeUpTime` | **Wake Up Time** (defaults into the third column below `LastRun`): time from the most recent wake-up-relevant moment to the first time the local player leaves the soft/spawn state, formatted `SS:mmm` |
| `CurrentState` | The engine's detected game state (debug) |

## Columns

Rows are grouped into **columns**. Each `[column.<n>]` section in `layout.ini`
holds one column's rows, where **each key is the row's 1-based position** in
that column (positions must be unique; `0` is never stored). Columns
are drawn **left-to-right by their number** — `[column.1]` is the leftmost
timer column, `[column.2]` the second, and so on — with the whole block anchored
at `offset_x` / `offset_y`. A column with **no rows is not displayed** and takes
no space. The default layout (written once on a fresh install) is:

```ini
[column.1]
1 = GameTime
2 = CurrentSegment
3 = TotalAtLastSegment
4 = LastSegment

[column.2]
1 = RealTime
2 = PrevRt

[column.3]
1 = LastRun
2 = WakeUpTime
```

`RealTime` and `PrevRt` default into `[column.2]` (`PrevRt` directly below
`RealTime`); `LastRun` and `WakeUpTime` default into `[column.3]` (the former
"right-hand column"). Every row type — including `LastRun` and `WakeUpTime` —
is a regular row you can move between columns freely; the panel's
**Interface → Timer HUD** column editor edits the same positions (`0` hides a
row), and empty columns are simply hidden.

> **Legacy `[rows]` configs**: old `layout.ini` files store the rows in a flat
> `[rows]` section. They still load (mapped to `[column.1]` in memory) and are
> rewritten in the `[column.N]` format on the next save; nothing is migrated
> or auto-modified at boot anymore.

## Wake Up Time

**Wake Up Time** is a regular HUD row (defaults below `LastRun` in the third
column). By default it is the time from the most recent
wake-up-relevant moment to the first time the local player leaves the
soft/spawn state (`Spawning` / `Unconscious` / `Dead`). The measurement
restarts whenever the player respawns (e.g. after a fall), loads the current
checkpoint from the pause menu, or restarts the level from the pause menu —
the value then reflects how long it took to get up after that particular
respawn. Once recorded within one measurement, later manual play-dead does not
reset it; a new respawn/restart clears it and starts a fresh measurement.

When the **Only record first wake-up time** option is enabled (in
**Interface → Timer HUD**), the original behavior is restored: only the
first wake-up after a level starts is measured, and later respawns / checkpoint
loads / level restarts do not reset the value. The value is cleared when the
level is passed or exited.

The value is formatted as `SS:mmm` (seconds and milliseconds, no minute/hour
breakdown) and shows `--:--` before the first measurement of a level. Show or
hide the row by toggling it in a column via the panel's **Interface → Timer
HUD** column editor (or editing `layout.ini` directly).

## Real Time clock

Alongside the game-time rows, the HUD has a **Real Time** clock that measures
wall-clock time for the current run. It starts when the game clock starts,
continues through level-loading screens and pauses, and stops when the run is
completed (the final level is passed) or when you leave the run for the menu.

The Real Time row is shown by default in the second column (`[column.2]`,
directly to the right of **Game Time**) in the default layout, and the clock is
always active. `RealTime` is a regular row type: move it in `[column.N]` and it
appears in your chosen position, or remove it entirely — the clock keeps
running in the background either way. Show or hide the row by toggling it in a
column via the panel's **Interface → Timer HUD** column editor.

**Prev RT (`PrevRt`)** shows the Real Time clock value frozen at the moment the
previous level completed (the same moment `TotalAtLastSegment` freezes the game
time, R1.4.1). Like that row it is a cumulative snapshot of the whole run at
that instant — not the previous level's own real duration — so it ticks up
across the run and updates once per completed level. It is `--:--` before the
first level is completed and is cleared by the same resets that clear
`TotalAtLastSegment` (auto-reset, a new run from the menu, and the manual reset;
a one-key retry keeps it).

## Timing-standard indicator

While the **"Use plcc timing standard"** option (R1.4.2) is enabled, the HUD
shows one line **`plcc timing mode`** (Chinese: `plcc计时模式`) directly under
the time rows, so the active timing mode is visible on screen. The line is
drawn in the same two-color gradient as the timer rows and is omitted entirely
when the option is off (the default).

## Colors & gradient

The default text gradient is a two-color left→-right blend. Specify each color
as hex, either `RRGGBB` (opaque) or `RRGGBBAA` (with alpha). Set both in
`layout.ini` `[panel]`:

```ini
color_a = FF5272FF   # start color (pink-red, fully opaque)
color_b = FF9A72FF   # end color
```

If both colors are equal, the text is a flat single color. Per-character alpha
comes from the alpha byte, so you can fade text across the line.

Custom texts (`[custom.<n>]`) each have their own `color_a` / `color_b`.

## Custom texts

Place any number of arbitrary texts at fixed screen coordinates:

```ini
[custom.0]
x = 400
y = 50
text = {date} {time}
font_size = 16
color_a = FFFFFFFF
color_b = CCCCCCCF
```

**Template variables** (auto-replaced):

| Variable | Replaced with |
|----------|---------------|
| `{date}` | Current date (`YYYY-MM-DD`) |
| `{time}` | Current time (`HH:MM:SS`) |
| `{version}` | Plugin version |
| `{collection}` | Active Level Collections collection name (or fallback) |
| `{category}` | Active category display name |
| `{gametime}` | Current game time |
| `{realtime}` | Current Real Time clock value |

Unknown `{tokens}` are left intact. Use the literal `\n` in the text for a
newline. Custom texts can also be added, edited, and deleted live from the
settings panel's **Interface → Custom Text** sub-page (see
[PANEL.md](PANEL.md)).

## Position & size

The main text block is drawn **directly on screen** — there is no window or
background, and it cannot be dragged. Adjust its position and font size in
`layout.ini` under `[text]`:

```ini
[text]
offset_x = 16   # pixels from the left edge
offset_y = 16   # pixels from the top edge
font_size = 18  # font size
```

Custom texts (`[custom.<n>]`) each have their own absolute `(x, y)` and
`font_size`, so they can appear anywhere on screen at any size regardless of the
main block's offset.

## Show / hide

Toggle the whole timer via the settings panel (default key `Home`), or set
`show_hud = false` in `settings.ini`.

## Game Loading/Saving indicator

The game shows its own "Loading"/"Saving" progress indicator in the top-right
corner. If you prefer it at the top-center, enable `center_loading_saving` in
`settings.ini` or toggle **Center Loading/Saving prompts** on the settings
panel's Interface tab.

## Invalid banner

When a run is flagged invalid (R5), a red banner appears inside the panel
listing the reason(s). See [CONFIG.md](CONFIG.md) for the validity options.

The same red style is also used for an invalid **specified retry level**
(R6.5): when the retry-target override is enabled and the configured level name
or Workshop id cannot be resolved, pressing the retry key shows a red
"Specified retry level is invalid" line in the timer HUD. It stays visible
until a resolvable value is pressed with Retry or the override is turned off.

## Fonts

The panel uses a dynamic OS font with a CJK-capable fallback chain (PingFang /
Microsoft YaHei / Noto Sans CJK …), so localized text in Chinese/Japanese
renders correctly. Verify rendering on your platform.
