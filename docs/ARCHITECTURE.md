# Architecture

> **中文版**: [zh/ARCHITECTURE.md](zh/ARCHITECTURE.md)

This document explains how HSRTimer is structured and the design decisions
behind it. For the requirements, see [../REQUIREMENTS.md](../REQUIREMENTS.md).

## Guiding principle: poll, don't patch

Almost every signal HSRTimer needs is a **public field or property** on the
game's classes:

| Signal | Source |
|--------|--------|
| Game state | `Game.state` (`GameState`: Inactive/Paused/LoadingLevel/PlayingLevel) |
| App/network state | `App.state` (`AppSate` — lobby/load transitions) |
| Local/server/client | `NetGame.isLocal`, `NetGame.isServer`, `NetGame.isClient` |
| Current checkpoint | `Game.currentCheckpointNumber` |
| Cheats | `CheatCodes.climbCheat`, `CheatCodes.throwCheat` |
| Jump | `Human.Localplayer.jump` |

Because the timing/segment/reset/checkpoint/validity rules are defined on
*transitions* of these fields, a single polling loop (`TimerCore.FixedUpdate`)
computes everything by comparing the current tick to a cached previous tick —
cheaply, and resilient to game updates that rename or inline private methods.

**Harmony is used only where no field exposes the event**: the two voiceline
hooks (`NarrativeBlock.Play` and `SubtitleManager.PlayNarrative`, see
[VOICELINE.md](VOICELINE.md)), the pause-menu restart hook
(`PauseMenu.RestartClick`, which fires the `restart_clears_forgivable` option),
and the Jumpless jump-key suppression (`HumanControls.HandleInput`, R3.5.3).
The last one is a deliberate exception: a pollable field *does* exist
(`HumanControls.jump`), but enforcement is a *write* into a chain the game both
writes and consumes within one physics frame (`NetPlayer.PreFixedUpdate` →
`Human.FixedUpdate`); writing it from this plugin's own `FixedUpdate` would race
on undefined script execution order. Polling covers *observation* — suppression
of an input the game consumes same-frame must hook inside the chain.

## Module layout

```
Plugin.cs                 entry: wires config + rules + patches + LC, spawns singletons
PluginInfo.cs             GUID/NAME/VERSION
Core/
  TimerCore.cs            engine MonoBehaviour (FixedUpdate=timing, Update=keys/validators)
  RunState.cs             single source of truth (time, segments, flags, caches)
  SegmentLogic.cs         pure Appendix-B truth table
  RetryAction.cs          one-key retry (R6)
Validation/
  InvalidReason.cs        enum + severity map
  ValidityFlags.cs        unforgivable/forgivable flag sets
  GenericValidators.cs    cheat-code detector
Tags/
  ITagRule.cs             tag rule interface + ValidationContext
  TagRuleRegistry.cs      extension registry (R3.7)
  CheckpointRules.cs      R4 skip-exception + final-checkpoint tables
  VoicelineTracker.cs     scene scan + Easter detection
  Rules/                  Checkpoint / NoCheckpoint / Jumpless / Voiceline
Patches/
  PatchModule.cs          Harmony.CreateAndPatchAll
  NarrativeBlockPatches.cs    postfix on NarrativeBlock.Play
  SubtitleManagerPatches.cs   postfix on SubtitleManager.PlayNarrative
  PauseMenuPatches.cs         postfix on PauseMenu.RestartClick
  HumanControlsPatches.cs     postfix on HumanControls.HandleInput (Jumpless enforcement)
Hud/
  TimerHud.cs             IMGUI panel (R2)
  GradientText.cs         color hex/alpha + gradient helper
  TemplateVars.cs         {date}/{time}/{version}/{collection}/{category}
Config/
  ConfigService.cs        facade
  PersistenceService.cs   tolerant INI reader/writer
  SettingsModel.cs, EnabledTagsModel.cs, LayoutModel.cs
Localization/
  LocalizationService.cs, LanguageFile.cs
LcIntegration.cs          optional LevelCollections soft integration
```

## The timing truth table (Appendix B)

Each physics frame (`FixedUpdate`), in order:

1. **Transitions** — compare `state`/`appState` to cached prev:
   - **Segment end (R1.4)**: `PlayingLevel → LoadingLevel`, or `PlayingLevel → Inactive` while local. (Recorded *before* the auto-reset clear.)
   - **Auto-reset (R1.7)**: `Paused→Inactive`, `ServerLoadLobby→ServerLobby`, `ClientLoadLobby→ClientLobby`, or `PlayingLevel→Inactive` (local) — only when `AutoReset` is on.
   - **Segment start (R1.2)**: `LoadingLevel/Inactive → PlayingLevel`, not in a lobby.
   - **Resume from pause (R1.3)**: `Paused → PlayingLevel` while timing was stopped.
2. **Accumulate** — `GameTime += Time.fixedDeltaTime` when `PlayingLevel` and not in a lobby / not waiting on the server.
3. **Menu supplement** — additionally `fixedDeltaTime` while `Inactive` or in a lobby, when `CountInMenu` is on.
4. **Rules** — run each active category's tag rules' `OnTick`.

`Update` (per render frame) handles: the cheat-code check, the **pause
supplement** (`unscaledDeltaTime` while `Paused`, since `timeScale=0` halts
`FixedUpdate`), and keybinds.

`LoadingLevel` and the client-wait gap (`ClientWaitServerLoad`) **never** count,
and no toggle restores them.

## Why retry unloads then re-launches the level

R6.2 requires a **full async level reload**, including the empty transition
scene (R6.2.1.3). HSRTimer drives it as a coroutine on the engine MonoBehaviour:

1. `Game.instance.UnloadLevel()` tears the running level down — `AfterUnload`
   sets `currentLevelNumber = -1`, `state = Inactive`, clears `currentLevel`
   and `workshopLevel`.
2. `SceneManager.LoadScene("Empty")` loads the empty transition scene.
3. **Dwell** — hold the empty scene until `retry_min_dwell` seconds (default
   0.5) have elapsed since the key press, measured with `Time.unscaledTime`.
   This lets a fast reload breathe; `0` disables the hold. The empty-scene time
   is never counted (the `Retrying` flag suppresses accumulation).
4. `App.instance.LaunchSinglePlayer(level, type, 0, 0)` re-launches the
   **retry-target** level (see R6.4 below — during a menu-entered campaign run
   this is the run's start level, not the current one). Its `LoadLevel`
   coroutine reloads the scene **only when `currentLevelNumber != levelNumber`**
   — so the unload in step 1 is what makes step 4 actually reload. The coroutine
   runs `SignalManager.BeginReset` → reload the scene → `AfterLoad`
   (`state = PlayingLevel`, `RespawnAllPlayers`, `Level.Reset(0, 0)`). No menu is
   shown (the App state machine is the hidden springboard R6.2.1.4 describes).
   Works for BuiltIn, EditorPick, and Workshop levels.

Skip step 1 and re-launching the current level silently degrades into a
checkpoint respawn — exactly the pause-menu "Restart" button behavior, which is
what R6 forbids. This is intentionally **not** `Game.RestartLevel(true)`
(checkpoint respawn, no scene reload) and **not** `Game.ReloadBundle()`
(Workshop-only: dereferences `workshopLevel.dataPath` and crashes on BuiltIn
levels after setting `timeScale = 0`, freezing the game in the "Empty" scene).

This is a level-level restart. A retry re-attempts the current level, so both
live timers (total game time and the current segment) reset to zero and the
level is timed from scratch. It is independent of the R1.7 run reset in the
sense that it does **not** clear the run's records (completed
segments, `LastRun`) or validity flags. The reload drives the level through
`PlayingLevel → Inactive → LoadingLevel → PlayingLevel`; to keep that from
looking like a run exit, `RetryAction` zeroes `GameTime`/`SegmentStart` and sets
`RunState.Retrying`, and the engine honors it for the duration of the reload:

- `SegmentLogic.IsAutoReset` suppresses its `PlayingLevel/Paused → Inactive`
  branches while `Retrying` (and the R1.7.3 branch additionally requires
  `App.state == Menu`, which a genuine run exit reaches via
  `PauseLeave → EnterMenu` but a retry never does). So retrying does not clear
  the run even with `AutoReset` on.
- `SegmentLogic.ShouldAccumulateMenu` returns false while `Retrying`, so the
  time spent in `Inactive` during the reload is never counted (even with
  `CountInMenu` on).
- `EndSegment` / `StartSegment` skip the LC
  full-run capture, the `LastRun` snapshot, **and the tag `OnLevelExit`
  callbacks** while `Retrying` — the level was abandoned mid-attempt, not
  completed. `OnLevelExit` runs the R4.2 final-checkpoint and the voiceline
  completion checks, so firing them against the abandoned state would spuriously
  raise `INVALID_CHECKPOINT_FINAL` / `Voiceline`. The current level's segment
  still restarts cleanly when the reloaded level reaches `PlayingLevel`.

`Retrying` is cleared on the next segment start and on a full reset.

## Why retry delegates to Level Collections during a collection run

When the Level Collections (LC) plugin is loaded **and** the player is mid-collection-run
(`CollectionManager.IsInCollectionRun`), the one-key retry does **not** reload the current
level in place — it restarts the *whole collection* from level 1 by dispatching LC's own
`lc restart` command through the game's dev-console registry (`Shell.RawInvoke("lc restart")`).
This is `R6.3`.

Restarting a collection re-times the entire run from level 1, so it must take precedence over
the single-level reload. Delegating to `lc restart` (rather than reflecting on LC internals) means
HSRTimer reuses LC's scene-reload forcing (`ResetCurrentLevelIfSame`), level validation, and
launching — and it works for both config collections and transient (`lc random`) runs. `Shell.RawInvoke`
is the same code path the console uses, so the dispatched command behaves exactly like typing
`lc restart`.

The timer treats this exactly like the single-level retry: it zeroes `GameTime`/`SegmentStart` and
sets `RunState.Retrying` first, so the abandoned level's segment end is not recorded as a `LastRun`
and the reload into level 1 is not mistaken for a run exit — while keeping the run's records and
non-forgivable flags (R6.2.2 independence from R1.7 applies here too). Implementation lives in
`RetryAction.TryExecute` (the LC branch) and `LcIntegration.RestartCollection`.

Two guard cases, both surfaced as `NOTIFY_RETRY_BLOCKED_STATE` without mutating any timer state:
LC refuses a new `lc restart` while one of its delayed commands (`lc restart/skip/random <seconds>`)
is counting down (`IsDelayedCommandPending`), so HSRTimer refuses too; and if `RestartCollection`
itself returns false (LC absent at call time, run ended), the speculatively-zeroed timers are
restored and HSRTimer falls back to the single-level reload.

### R6.4 — the campaign "entered from menu" retry target

During a run through the official campaign list (Intro–Reprise) that was **entered
from the menu**, the one-key retry returns to the level the player *started the run
on* (`RunState.CampaignRetryLevel`), not the level they happen to be playing. The
campaign auto-advances on every pass (`PassLevel → StartNextLevel →
LaunchSinglePlayer`), so without this the retry target would drift forward each
level — useless for practicing one chosen level repeatedly.

**Detection — the `Menu → LoadLevel` App-state edge.** The game's own state machine
makes this unambiguous (verified in the decompiled `App.cs`):

- a menu entry goes `App.state: Menu → LoadLevel` (via `LaunchGame`);
- a campaign auto-advance goes `PlayLevel → LoadLevel` — it never passes `Menu`;
- a retry's own reload stays in `LoadLevel` under the `Retrying` flag;
- a multiplayer lobby launch hits `ServerLoadLevel`/`ClientLoadLevel` instead.

So `SegmentLogic.IsMenuEntry(prevApp, nowApp)` fires only on a genuine menu pick.
`TimerCore.HandleTransitions` latches it into `RunState.MenuEntryPending`, and the
next `StartSegment` records the level into `CampaignRetryLevel` — but only when it
is a playable campaign level (`BuiltIn`, `0 <= number < levelCount`, not the
Credits epilogue, not inside an LC collection run, whose retries R6.3 owns).
`MenuEntryPending` is then cleared; the edge only ever describes the level that
just started.

**Persistence.** `CampaignRetryLevel` survives campaign advances (they don't
re-trip the menu edge), full-run resets, and retries themselves — it means "the
level the player last entered from the menu", which stays meaningful until the
next menu entry overwrites it (a new run at a different level). `MenuEntryPending`
is cleared by `RunState.Reset`.

**Retry behavior.** In `RetryAction.TryExecute`, when `CampaignRetryLevel >= 0`
the reload re-launches that level as `BuiltIn` (`NOTIFY_CAMPAIGN_RESTARTED`);
otherwise — EditorPick, Workshop, or any run not tagged as menu-entered — the
current-level reload runs unchanged (`NOTIFY_LEVEL_RESTARTED`). Timing semantics
are identical either way (see R6.2.2 above).

## Why segment end is recorded before the auto-reset clear

The `PlayingLevel → Inactive` (local) edge is *both* a segment end (R1.4.1) and
an auto-reset trigger (R1.7.3). Recording the final segment and running the
R4.2 final-checkpoint validation must happen before the run is cleared. The
*clock deactivation* on segment end is unconditional; the segment's **values**
(duration / total / completion checks) are recorded only on a genuine
completion (see below). The auto-reset clear is gated by `AutoReset`. See
`SegmentLogic` + `TimerCore.HandleTransitions`.

## Why a segment's values are recorded only on level completion

A level leaving `PlayingLevel` is not always a completion — it can be a mid-level
quit (`Esc → Exit → PauseLeave`), and crucially a **Workshop/EditorPick
completion leaves via the same `PauseLeave` path as a quit**. So the state
transition alone can't tell them apart. `RunState.LevelPassed` is the signal:
each physics tick the engine latches `Game.passedLevel` into `LevelPassed`
(OR'd, so it sticks once the pass zone is reached — the game clears
`passedLevel` itself during the completion/leave flow, before the state flips,
so the latch must read it beforehand). `LevelPassed` is reset on segment start.

`EndSegment(completed: LevelPassed)` records `LastSegment`/`TotalAtLastSegment`,
the tag `OnLevelExit` completion checks (R4.2,
voiceline), and the LC last-level `LastRun` capture **only when `completed`**.
A mid-level quit (or a retry, where `LevelPassed` is false) leaves the previous
attempt's `LastSegment`/`TotalAtLastSegment` untouched — exactly the desired
behavior: the "last segment" reference reflects the last level you *finished*,
not one you walked out of. Note the `PlayingLevel → LoadingLevel` edge (a
built-in level's `StartNextLevel` reload) is inherently a completion, and there
`LevelPassed` is true from the earlier `EnterPassZone`.

## What counts as a completed run (LastRun)

`LastRun` (the "last run" total) is recorded at the instant a run is *completed*,
and it must distinguish three unrelated endings that the game state alone can't
tell apart. The rule runs in `EndSegment` when `completed` is true, using
segment-start snapshots / per-tick latches (because by then the game/LC state has
already moved on to whatever follows):

- **Campaign → Credits**: a BuiltIn level whose number is `levelCount - 1` (the
  last playable level; the game then loads Credits at index `levelCount`).
- **Standalone EditorPick**: an EditorPick level passed *outside* an LC
  collection run (`InCollectionRunSegment` latch false).
- **Collection completion**: the last level of an LC collection run
  (`OnCollectionLastLevel` latch). This one needs latching because LC ends the
  run synchronously inside `Game.Fall`, before the engine's FixedUpdate observes
  the state flip.

(Workshop level passes alone are not a "run completion" — they don't end a run
in the timer's sense.) A prior heuristic that recorded `LastRun` at any segment
start when the clock was running was removed: it fired mid-campaign at every
level boundary and never on EditorPick/Workshop ends, and now `LastRun` updates
only on these genuine completions.

`LastRun` renders in its own column immediately to the right of the timer stack
(not inside it, anchored at the main block's widest line), and only while idle
(`!InSegment && GameTime == 0`) — once a new run starts timing it hides until
the next completion. The one exception is the campaign epilogue: the game loads
Credits (BuiltIn index == `levelCount`) as an ordinary level right after the
final playable level is passed, and that segment is flagged
`InEpilogueSegment` — it belongs to the run that just finished, so the column
stays visible through Credits (and Credits itself never records anything: it
has no pass zone, so its segment never counts as `completed`). Credits listed as
a level inside a collection run does NOT count as the epilogue (the run is still
active — appearing in Credits mid-collection is just an ordinary level), so
`InEpilogueSegment` also requires "not currently in a collection run".

## Why auto-reset preserves the "last" snapshots

`LastSegment`, `TotalAtLastSegment`, and `LastRun` are HUD reference values
("how the previous segment/run compared"). They update only when a new value is
recorded (a segment end records `LastSegment`/`TotalAtLastSegment`; a new run
begun from a reset or a completed last level records `LastRun`) — so an
**auto-reset (R1.7) must not clear them**, even though it clears the live timers.
`RunState.Reset(bool keepLastValues)` expresses this: the auto-reset path
(`HandleTransitions` → `DoFullReset(keepLastValues: true)`) keeps the three
snapshots; the **manual reset key** (`DoFullReset(keepLastValues: false)`) clears
them, since pressing reset means "I want a fresh comparison baseline." (Retry
doesn't touch `Reset` at all — it goes through the `Retrying` flag.)

## Config check & repair

`ConfigRepair.Run(cfg)` runs once in `Plugin.Awake`, right after `ConfigService.Load`
and before any subsystem reads the models. It detects and fills in missing or
incorrect config items. Scalar settings/layout keys already self-heal to defaults
(the parse helpers fall back to the current value), so the rules target collection
fields that `Clear()`-then-rebuild from disk — where a newly-added default item is
silently lost for existing users (this is how the `TotalAtLastSegment` row failed
to appear until this system existed).

Design: **idempotent structural checks every boot, write only when something
changed** (a dirty-gated `ConfigService.SaveSettings`). No config-version key — a
stored version lies when a user hand-edits the file, whereas cheap structural
checks self-heal hand-edited corruption and leave clean files untouched. It logs
one summary line on a repair and is silent on a clean boot.

The first rule, `RepairLayoutRows`, inserts any missing default HUD row when the
user's row set looks default-derived (`IsDefaultDerived`: the rows equal the
defaults in order, minus any missing entries). A reordered or extra/duplicate row
set is treated as hand-customized and left untouched, with an advisory hint. The
canonical default order lives in one place — `LayoutModel.DefaultRows` — shared by
the `Rows` field initializer and the repair target, so the two can't drift; adding
a new default row is a single line there. Adding a new repair concern is one
method plus one entry in the `ConfigRepair.Rules` array.

## Building

```bash
dotnet build src/HSRTimer/HSRTimer.csproj
```

`Directory.Build.props` points at the default Steam install's managed DLLs and
BepInEx core. Override `GAME_MANAGED` / `BEPINEX_CORE` for other platforms.
