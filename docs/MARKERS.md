# Markers

> **中文版**: [zh/MARKERS.md](zh/MARKERS.md)

Markers are user-defined, per-level trigger points (R10). When a marker's
condition is met while playing, its **first** trigger time in that segment is
recorded and compared against the level's own PB record. Marker data is local
only — there is no import of other players' data, unlike the subsegment
module's PB + LoadPath model.

## Concept

- One marker set per **(level, category key)**. The category key follows the
  subsegment rule (R8.2.4): `Any` with no enabled tags, otherwise the sorted
  enabled tag ids joined with `+`.
- A marker's time is the **segment time** at the trigger frame:
  `t_ms = round((GameTime - SegmentStart) * 1000)` — the same time base as the
  HUD's *Segment Time* row and the subsegment IL comparisons.
- Each marker records only its **first** trigger per level attempt. Records and
  the leaderboard feed reset on level start, one-key retry, pause-menu level
  restart, manual reset, and level exit. PBs are kept.

## Marker types (R10.1.4 / R10.2)

| Type | Trigger condition |
|------|-------------------|
| **Range** | The local player's position is inside the axis-aligned box (`|p - center| <= size/2` on every axis) **and** any enabled conditions hold: *require grabbing any object* (`GrabManager` has ≥ 1 object) and/or *require jumping* (`Human.jump`). The player must be in an operable state (not Spawning/Unconscious/Dead). A box with any side length ≤ 0 never triggers and shows a warning in the editor. |
| **Checkpoint** | **Touch/reach** (default): the first frame in `PlayingLevel` where `Game.currentCheckpointNumber >= N` (the `>=` keeps non-linear checkpoint levels like Dark working). **Load** (with the "trigger when loaded from the pause menu" option): fired from the pause-menu Load click when `currentCheckpointNumber == N` exactly — the pause menu always loads the *current* checkpoint, so this is precise. |
| **Grab object** | The captured object is currently grabbed by the local player (parent/child containment either way, matching `GrabManager.IsGrabbed` semantics). |

Multiple markers satisfied on the same frame all record (feed order follows
definition order).

## PB (R10.3)

- A **whole-level PB set**: when a level is **passed**, is **not** a one-key
  retry, and the run is **valid** (no R5 flags), the level's segment time is
  compared against the stored `pb.total_ms`. Only when it is **strictly
  better** is the entire set of that run's marker times written as the new PB.
- Markers not triggered in that run are not written (they show `--`).
- Deleting a marker removes its PB entry.
- PB writes happen before the auto-reset clears the segment (same moment as the
  subsegment PB write, R8.3.3.1), atomically (temp file + rename).

## Data files

`<config>/HSRTimer/markers/{level}/{category}.json` (UTF-8, pretty-printed JSON;
written by an explicit serializer so every field — including an empty
`markers` array — is always present, and parsed tolerantly):

```json
{
  "format_version": 1,
  "level_id": "Aztec",
  "level_source": "BuiltIn",
  "level_number": 8,
  "category_key": "Any",
  "pb": { "total_ms": 184320, "created_at": "…" },
  "markers": [
    { "id": "m1", "name": "Tower", "type": "Range", "enabled": true,
      "cx": 1.0, "cy": 2.0, "cz": 3.0, "sx": 2.0, "sy": 4.0, "sz": 2.0,
      "requireGrab": false, "requireJump": true,
      "checkpointIndex": 0, "triggerOnLoad": false,
      "objectSceneId": 0, "objectPath": "", "objectName": "", "ox": 0, "oy": 0, "oz": 0 }
  ],
  "pbTimes": [ { "id": "m1", "t_ms": 12345 } ]
}
```

- The level key uses the same scheme as subsegment IL ids (R8.2.3): English
  localized name for BuiltIn / EditorPick levels, the numeric Workshop id, or
  the local level's folder name when it has no id (so a marker created in the
  panel resolves to the same key while playing).
- A missing, malformed, or unsupported-version file is treated as an empty set
  with a logged warning — it never affects timing.

## Editing (R10.4 / R10.5)

Settings panel → **Markers** tab:

1. Turn on **marker edit mode** (persisted). While it is on, the timer HUD
   shows a conspicuous hint line at the very bottom (independent of `show_hud`;
   when the HUD is hidden it appears at the bottom-left). A small XYZ axis
   indicator like a 3D editor's orientation gizmo is drawn directly below the
   hint; it follows the active view angle, including the F8 free camera.
2. Pick a level group — **Main Dreams** (built-in), **Extra Dreams**
   (editor-pick), **Workshop** — and open a level.
3. **New marker** (only in edit mode) creates a `Range` marker and expands its
   editor: name, type dropdown, and per-type controls.
   - Range: *Set to player position* (needs the local player in a level),
     center X/Y/Z, length X/Y/Z, and the grab/jump requirement toggles.
   - Checkpoint: checkpoint number, optional "trigger when loaded from the
     pause menu".
   - Grab object: *Set to currently grabbed object* — not grabbing anything
     shows a hint; grabbing more than one object is **invalid**; exactly one
     stores the object reference (scene id when available, else the hierarchy
     path, plus name and position for fallback resolution).
4. Changes are saved automatically (dirty flush at most once per frame, plus
   on panel save/close and game exit).

If the level metadata (`WorkshopRepository`) is not loaded yet (e.g. the level
select screen was never opened), the level lists show a hint instead of
inventing level keys; enter the main menu's level select once and press
**Refresh**.

## Visualization (R10.6)

While **edit mode is on** and a level is playing:

- every enabled **Range** marker draws a blue translucent cube (configured
  `OverlayFillColor`, default `3F7FFF66`) with its name as a white label at the
  center;
- every enabled **Grab object** marker highlights its resolved target's bounds
  with the same cube + label.

The overlay uses `Graphics.DrawMesh` with a transparent unlit shader — no
colliders are created and no game materials are modified, so gameplay and
netcode are unaffected. If no usable shader is found the overlay degrades once
to an IMGUI wireframe projection. Unresolvable grab-object targets are logged
once per level and skipped; re-capture the object in the editor.

## Leaderboard feed (R10.7)

The shared leaderboard HUD (left-middle, toggled by the leaderboard toggle key)
has a content switch on the **Leaderboard** settings tab: **Subsegment** or
**Markers**.

In Markers mode:

- A row appears **when a marker triggers** — untriggered markers take no
  placeholder rows.
- Row format: `{marker name}: {time}`. The time is either the marker's
  **absolute segment time** (`MM:SS.mmm`) or the **relative** signed difference
  to that marker's PB (`+MM:SS.mmm` / `-MM:SS.mmm`), chosen by
  `Markers.LeaderboardTimeMode` (default `Relative`). Without a PB entry, the
  relative row shows `--`.
- **Newest trigger is inserted directly under the title row**; older rows shift
  down (no re-sorting, no truncation).
- The faster/slower/tie colors always apply: faster than PB = green, slower =
  red, tie/no PB = white, in both time modes.
- The feed survives the level-end transition (the previous level's feed stays
  on screen until the next level's first trigger replaces it) unless the next
  level has no markers, in which case it clears immediately.
- Switching the mode does not affect subsegment recording or PB writes.

## Troubleshooting

- **Marker never triggers** — check: enabled? edit mode irrelevant here; Range
  box side lengths > 0; the player state (not dead/unconscious/spawning);
  required grab/jump conditions actually held at the same moment; for
  checkpoints, the number matches `Game.currentCheckpointNumber`.
- **Grab-object marker inactive** — the target could not be resolved in this
  level (object changed or is dynamically spawned). Re-capture it with "Set to
  currently grabbed object"; check the log for the once-per-level warning.
- **PB "missing" after switching tags** — the marker set is keyed by the
  category key; switching tags points to a different file. Switch back to see
  the old data.
- **Level list empty** — open the main menu's level select once so the game
  populates `WorkshopRepository`, then press **Refresh**.
- **Overlay missing** — confirm edit mode is on, you are in a level, and
  `Markers.Enable` is true; with `Markers.DebugLogging = true` the log shows
  the level key and marker count.

See [CONFIG.md](CONFIG.md) for the `[Markers]` settings and
[REQUIREMENTS.md](../REQUIREMENTS.md) R10 for the full specification.
