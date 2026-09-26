using System.Collections.Generic;
using Multiplayer;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// The timer engine. A single polling MonoBehaviour drives all timing,
    /// segment, reset, validity, and tag logic each frame by reading public
    /// game fields. The only exceptions are the precise start/end boundary
    /// hooks (see <see cref="RecordSegmentStart"/>/<see cref="RecordLevelPass"/>
    /// and docs/ARCHITECTURE.md): they record the exact tick where the game
    /// flips state inside its own physics step, which a poll can only observe
    /// a tick late.
    ///
    /// <see cref="FixedUpdate"/>: accumulation + state-transition detection
    /// (Appendix B) + per-tick tag rules on segment end.
    /// <see cref="Update"/>: keybinds (reset/retry/toggle/cycle/reload), the
    /// cheat-code validator, and the pause-supplement step (which runs in
    /// Update because timeScale=0 halts FixedUpdate while paused).
    /// </summary>
    public class TimerCore : MonoBehaviour
    {
        public static TimerCore Instance { get; private set; }

        /// <summary>The single source of truth for displayed values (HUD reads this).</summary>
        public static RunState State { get; private set; }

        private ConfigService _cfg;
        private TimingOptions _opt;

        private HumanState _prevHumanState;
        private bool _prevHumanStateInit;

        // Mark the physics step the poll last processed. The boundary hooks
        // compare it against Time.fixedTime to tell whether their step has
        // already been counted, so the segment-end tick is exact no matter how
        // Unity orders this plugin's FixedUpdate against the game's.
        private float _lastFixedTime = float.NaN;

        // Authoritative segment-start snapshot latched by the Game.AfterLoad
        // postfix (TB-3). Consumed by StartSegment.
        private bool _segmentStartLatched;
        private ulong _segmentStartTicks;
        private double _segmentStartPause;

        // Ring buffer of recent segment-start/end boundaries, exposed through
        // 'hsr clock history' so repeated loads/passes can be checked for zero
        // tick jitter (TB-5).
        private const int BoundaryLogCapacityConst = 200;
        private static readonly List<string> BoundaryEntries = new List<string>();

        public static int BoundaryLogCapacity => BoundaryLogCapacityConst;

        public static IReadOnlyList<string> BoundaryLog => BoundaryEntries;

        public static void ClearBoundaryLog() => BoundaryEntries.Clear();

        private static void LogBoundary(string entry)
        {
            BoundaryEntries.Add(entry);
            if (BoundaryEntries.Count > BoundaryLogCapacityConst)
                BoundaryEntries.RemoveAt(0);
        }

        /// <summary>
        /// True when this plugin's <see cref="FixedUpdate"/> has already run for
        /// the current physics step (used by the boundary hooks to decide
        /// whether the current step is already in <see cref="RunState.PlayableTicks"/>).
        /// </summary>
        public static bool HasProcessedCurrentPhysicsStep
            => Instance != null && Instance._lastFixedTime == Time.fixedTime;

        private void Awake()
        {
            Instance = this;
            State = new RunState();
            _cfg = ConfigService.Instance;
            UpdateOptions();
        }

        private void UpdateOptions() => _opt = TimingOptions.FromSettings(_cfg.Settings);

        // ── Physics step: accumulation + transitions + per-tick rules ───────
        private void FixedUpdate()
        {
            // Record the step first so boundary hooks firing later in the same
            // step can see that it has been processed.
            _lastFixedTime = Time.fixedTime;
            GameClock.Advance();

            // Always advance the transition cache so a transient null Game.instance
            // (scene teardown) doesn't leave a stale prev that manufactures a
            // spurious transition on resume. Treat a null game as Inactive.
            if (State == null)
                return;

            Game game = Game.instance;
            GameState gState = game != null ? game.state : GameState.Inactive;
            AppSate aState = App.state;
            bool isLocal = NetGame.isLocal;

            if (game != null)
            {
                // Latch the pass-zone flag BEFORE transition handling: a segment
                // can end on this very frame (the completion/leave flow clears
                // Game.passedLevel itself before the state flips), so EndSegment
                // must see that the level was passed while it still could be
                // observed.
                if (State.InSegment && game.passedLevel)
                    State.LevelPassed = true;

                // Latch the LC collection-run context for the same reason: LC
                // ends/advances the run synchronously inside Game.Fall, before
                // this FixedUpdate observes the state flip — EndSegment must see
                // where this segment sat in the run while that was still
                // observable.
                if (State.InSegment && LcIntegration.Instance != null)
                {
                    if (LcIntegration.Instance.IsInCollectionRun)
                        State.InCollectionRunSegment = true;
                    if (LcIntegration.Instance.IsLastLevelOfCollection)
                        State.OnCollectionLastLevel = true;
                }

                // Detect transitions first (uses cached prev), then respawns,
                // then accumulate, then run per-tick rules.
                HandleTransitions(game, gState, aState, isLocal);
                TrackRespawn(game, gState);
                Accumulate(game, gState, aState);
                State.GameTimeSeconds = GameClock.Seconds(State.PlayableTicks, State.PauseAccum);
                TrackWakeUp(game, gState);
                SubsegmentManager.Instance?.OnPhysicsTick(game, gState, State);
                MarkersManager.Instance?.OnPhysicsTick(game, gState, State);
                RunRules(game, gState);
            }

            State.PrevGameState = gState;
            State.PrevAppState = aState;
            State.JustReset = false;
            State.SegmentJustEnded = false;
        }

        // ── Per-frame step: validators + pause accumulation + keybinds ──────
        private void Update()
        {
            if (State == null || _cfg == null) return;

            // Auto label tags (R3.10): the Co-op label follows the live game
            // mode every frame — on during a multiplayer session
            // (NetGame.isServer/isClient), off in single-player. Label tags are
            // never persisted (EnabledTagsModel.Save filters them out) and carry
            // no rule, so this only affects the enabled-tag display surfaces.
            SyncAutoLabels();

            GameState gState = Game.instance != null ? Game.instance.state : GameState.Inactive;

            // Generic always-on validity check (R5.1): cheat codes.
            GenericValidators.CheckCheat(State.Flags);

            // B.2 pause supplement (always on; runs here because FixedUpdate is
            // paused when timeScale=0). Pause time is wall-clock, the one
            // non-tick game-time component (R1.8.3/TB-6). Once a pass boundary
            // has been latched the run's game clock is already frozen, so a
            // pause in the short post-pass window must not extend it either.
            if (!State.PendingEndTicks.HasValue
                && SegmentLogic.ShouldAccumulatePause(gState, State.TimingActive))
            {
                State.PauseAccum += Time.unscaledDeltaTime;
                State.GameTimeSeconds = GameClock.Seconds(State.PlayableTicks, State.PauseAccum);
            }

            // Subsegment quiet-settle windows run in unscaled time so they
            // continue through pauses (R8.4.3.4).
            SubsegmentManager.Instance?.OnUpdate();

            // Markers: flush dirty marker-set edits (R10.5.8).
            MarkersManager.Instance?.OnUpdate();

            // Real-time clock: unlike game time this is not tied to a playable
            // state, so it keeps advancing through level loading screens and
            // while paused. It is still reset/stopped by the same run lifecycle
            // (start on the first segment, stop when a run completes).
            if (State.RealTimeActive)
                State.RealTime += Time.unscaledDeltaTime;

            HandleKeybinds();
        }

        // ── Transition handling (Appendix B + R1.7 resets) ──────────────────
        private void HandleTransitions(Game game, GameState gState, AppSate aState, bool isLocal)
        {
            // Refresh timing options from the live settings model so toggles made
            // in the settings panel take effect without requiring a reset/retry.
            UpdateOptions();

            GameState prevG = State.PrevGameState;
            AppSate prevA = State.PrevAppState;

            // R1.4 segment end must be recorded BEFORE any R1.7 full-run clear:
            // the PlayingLevel→Inactive (local) edge is both a segment end (R1.4.1)
            // and an auto-reset trigger (R1.7.3). Recording is unconditional; only
            // the clear is gated by the AutoReset option. EndSegment is a no-op when
            // no segment is active, so this is safe on every other transition.
            if (SegmentLogic.IsSegmentEnd(prevG, gState, isLocal))
                EndSegment(game, completed: State.LevelPassed);

            // The real-time clock is deliberately not gated on LoadingLevel:
            // it must keep counting through cross-level loading. But when the
            // run exits to menu/lobby (not a retry) outside an LC collection
            // run, it is no longer actively progressing, so stop the real clock
            // too. This covers PlayingLevel→Inactive, Paused→Inactive, and
            // lobby transitions, preventing it from silently counting while
            // sitting in a menu/lobby even if AutoReset is disabled, while
            // still letting a collection run's internal transition continue
            // timing.
            bool leftRunOutsideCollection = gState == GameState.Inactive
                && !State.InCollectionRunSegment;
            bool genuinelyInMenu = gState == GameState.Inactive
                && aState == AppSate.Menu;
            bool enteredLobby = aState == AppSate.ServerLobby || aState == AppSate.ClientLobby
                || aState == AppSate.ServerLoadLobby || aState == AppSate.ClientLoadLobby;
            if (!State.Retrying && (leftRunOutsideCollection || genuinelyInMenu || enteredLobby))
                State.RealTimeActive = false;

            // R1.7 auto-reset (honored only when the option is on). Suppressed
            // during a retry (R6) so reloading the level does not clear the run.
            // It clears the live timers and the last-segment HUD snapshots too,
            // so leaving to the menu gives a fresh baseline while keeping the
            // previous completed run's total as a reference.
            if (_opt.AutoReset && SegmentLogic.IsAutoReset(prevG, gState, prevA, aState, isLocal, State.Retrying))
            {
                SubsegmentManager.Instance?.OnRunExit();
                MarkersManager.Instance?.OnRunExit();
                DoFullReset(keepLastValues: false, keepLastRun: true);
                return;
            }

            // R6.4: latch the Menu→LoadLevel edge for the next segment start.
            // This fires only on a genuine menu entry (campaign advance and a
            // retry's reload never pass through Menu — see SegmentLogic.IsMenuEntry),
            // so the soon-to-start level is the one to remember as the campaign
            // retry target. The edge is consumed at segment start below.
            //
            // A level entered from the menu always begins a brand-new run,
            // regardless of the AutoReset option, so reset the timer here
            // (keeping the previous completed run's total as a reference).
            if (SegmentLogic.IsMenuEntry(prevA, aState))
            {
                SubsegmentManager.Instance?.OnRunExit();
                MarkersManager.Instance?.OnRunExit();
                DoFullReset(keepLastValues: false, keepLastRun: true);
                State.MenuEntryPending = true;
            }

            // R1.2 segment start (level entered / became playable).
            if (SegmentLogic.IsSegmentStart(prevG, gState, aState))
            {
                StartSegment(game);
            }
            else if (SegmentLogic.IsResumeFromPause(prevG, gState, State.TimingActive))
            {
                // R1.3: resume without resetting the segment start or game time.
                State.TimingActive = true;
            }
        }

        private void StartSegment(Game game)
        {
            // A retry is a level-level restart: clear the flag now that the
            // reloaded level has begun its new segment.
            State.Retrying = false;

            int cp = game.currentCheckpointNumber;
            // TB-3: prefer the exact tick latched by the Game.AfterLoad hook.
            // It is consumed here (the poll's IsSegmentStart fires on the first
            // playable FixedUpdate after AfterLoad, so the values normally
            // coincide; the latch keeps the authoritative hook as the source).
            ulong startTicks = _segmentStartLatched && _segmentStartTicks <= State.PlayableTicks
                ? _segmentStartTicks
                : State.PlayableTicks;
            double startPause = _segmentStartLatched && _segmentStartTicks <= State.PlayableTicks
                ? _segmentStartPause
                : State.PauseAccum;
            _segmentStartLatched = false;
            State.BeginSegment(startTicks, startPause, game.currentLevelNumber, game.currentLevelType, cp);
            _prevHumanStateInit = false;
            // The Credits level (BuiltIn index == levelCount) is the epilogue of
            // the campaign run that just finished — not a new run. Mark it so the
            // HUD keeps showing the recorded LastRun during it and recording
            // skips it (it has no exit/pass zone). Credits can also appear as a
            // level INSIDE a collection run (listed in the collection) — that is
            // an ordinary level of an ongoing run, not an epilogue, so the
            // collection check (queried live here — no race: a collection Credits
            // means the run is still active) disqualifies it.
            bool inCollectionRunNow = LcIntegration.Instance != null
                && LcIntegration.Instance.IsInCollectionRun;
            State.InEpilogueSegment = game.currentLevelType == WorkshopItemSource.BuiltIn
                && game.levelCount > 0
                && game.currentLevelNumber == game.levelCount
                && !inCollectionRunNow;

            // Real-time clock starts with the first playable segment of a run and
            // stays active across level-loading transitions. The Credits epilogue
            // is not a new run, so it must not restart the real-time clock after
            // the final level has already stopped it.
            if (!State.InEpilogueSegment)
                State.RealTimeActive = true;

            // R6.4: if this segment was entered from the menu and is a playable
            // campaign level (BuiltIn, within the Intro–Reprise range, not the
            // Credits epilogue, not part of an LC collection run), remember it
            // as the campaign retry target. A retry during this run then returns
            // here even after advancing to later campaign levels. The Credits
            // level is excluded because it has no gameplay; a collection-run
            // level is excluded because R6.3 (LC delegation) owns those retries.
            //
            // If a fresh menu entry instead starts an EditorPick, Workshop, LC
            // collection, or Credits level, clear any previously remembered
            // campaign target. Otherwise a stale BuiltIn target from an earlier
            // menu-entered campaign run would make RetryAction reload that old
            // level instead of falling back to the current EditorPick/Workshop
            // level (R6.4.5).
            // The MenuEntryPending latch is cleared regardless — it only ever
            // describes the level that just started.
            if (State.MenuEntryPending)
            {
                bool playableCampaign = game.currentLevelType == WorkshopItemSource.BuiltIn
                    && game.levelCount > 0
                    && game.currentLevelNumber >= 0
                    && game.currentLevelNumber < game.levelCount
                    && !State.InEpilogueSegment
                    && !inCollectionRunNow;
                if (playableCampaign)
                    State.CampaignRetryLevel = game.currentLevelNumber;
                else
                    State.CampaignRetryLevel = -1;
            }
            State.MenuEntryPending = false;

            State.Game = game; // cache for the HUD / rules

            // Start/reload the local subsegment module (R8).
            SubsegmentManager.Instance?.OnLevelStart(game, State);

            // Start/reload the markers module (R10).
            MarkersManager.Instance?.OnLevelStart(game, State);

            // Fire tag OnLevelEnter for every enabled tag.
            ForEachEnabledRule(rule => Safe(rule, r => r.OnLevelEnter(MakeContext(game))));

            LogBoundary($"start level={State.CurrentLevelNumber} type={State.CurrentLevelType} tick={startTicks} pause={startPause:0.###} step={GameClock.CurrentTick}");
        }

        private void EndSegment(Game game, bool completed)
        {
            // A manual reset clears the active segment (R1.7.1). If the level
            // later leaves PlayingLevel without a new segment having started,
            // there is no attempt to finalize: tag OnLevelExit / PB recording
            // must not run, otherwise stale per-level tag state can re-raise
            // forgivable invalid flags on the way out.
            if (!State.InSegment)
                return;

            // TB-3: use the pass-zone hook's exact end tick when one was
            // latched; otherwise (a mid-level quit, which records nothing) fall
            // back to the polled tick.
            ulong endTicks = State.PendingEndTicks ?? State.PlayableTicks;
            double endPause = State.PendingEndTicks.HasValue ? State.PendingEndPause : State.PauseAccum;
            double end = GameClock.Seconds(endTicks, endPause);
            bool retrying = State.Retrying;

            ulong durationTicks = endTicks >= State.SegmentStartTicks ? endTicks - State.SegmentStartTicks : 0UL;
            bool fromHook = State.PendingEndTicks.HasValue;
            LogBoundary($"end   level={State.CurrentLevelNumber} tick={endTicks} dur={durationTicks} completed={completed} retrying={retrying} src={(fromHook ? "hook" : "poll")} step={GameClock.CurrentTick}");

            // Fire tag OnLevelExit first — but only for a genuine level
            // completion. A retry or a mid-level quit abandons the level (its
            // reload/leave drives it through PlayingLevel → Inactive/LoadingLevel,
            // a segment end); running OnLevelExit then would let the checkpoint
            // final-check (R4.2) and voiceline-completion check fire against the
            // abandoned state and spuriously raise INVALID_CHECKPOINT_FINAL /
            // Voiceline.
            //
            // Running it before the subsegment/marker PB writes matters: those
            // final checks are what raise INVALID_CHECKPOINT_FINAL / Voiceline,
            // and a run with any invalid flag (cheat, skipped checkpoint, missed
            // voiceline, etc.) must not record subsegment or marker PBs.
            if (!retrying && completed)
                ForEachEnabledRule(rule => Safe(rule, r => r.OnLevelExit(MakeContext(game))));

            SubsegmentManager.Instance?.OnLevelEnd(
                game, State, end, completed, retrying,
                game != null ? game.state : GameState.Inactive, App.state);
            MarkersManager.Instance?.OnLevelEnd(
                game, State, end, completed, retrying,
                game != null ? game.state : GameState.Inactive, App.state);
            State.EndSegment(endTicks, endPause, completed);

            // R1.6: record the completed run's total time. Three cases count as
            // "the run is over": (a) the campaign's final level was passed (the
            // game then loads Credits — levelCount-1 is the last playable level,
            // levelCount itself is Credits); (b) a standalone EditorPick level
            // was passed (outside a collection run); (c) the last level of an LC
            // collection run was passed. All use segment-start snapshots /
            // latches (CurrentLevelType, OnCollectionLastLevel) because by now
            // the game/LC state has already moved on to whatever follows.
            if (!retrying && completed)
            {
                bool campaignDone = State.CurrentLevelType == WorkshopItemSource.BuiltIn
                    && game.levelCount > 0
                    && State.CurrentLevelNumber == game.levelCount - 1;
                bool standaloneEditorPick = State.CurrentLevelType == WorkshopItemSource.EditorPick
                    && !State.InCollectionRunSegment;
                bool collectionDone = State.OnCollectionLastLevel;
                if (campaignDone || standaloneEditorPick || collectionDone)
                {
                    State.LastRunTicks = endTicks;
                    State.LastRunPause = endPause;
                    // The real-time clock freezes at the same moment the game
                    // clock records the completed run.
                    State.RealTimeActive = false;
                }
            }

            if (!retrying)
                _cfg.SaveSettings();
        }

        // ── Wake Up time (per level / per respawn) ────────────────────────
        /// <summary>
        /// In the default (respawn-aware) mode, detect a local-player respawn by
        /// the transition into <c>Spawning</c> and restart the Wake Up Time
        /// measurement from that instant. Pause-menu Load/Restart are handled by
        /// Harmony postfixes while FixedUpdate is halted; they call
        /// <see cref="RestartWakeUpMeasurement"/> and mark this transition cache
        /// as already seen so this polling path does not double-reset.
        /// </summary>
        private void TrackRespawn(Game game, GameState gState)
        {
            if (_cfg.Settings.OnlyRecordFirstWakeUpTime)
                return;
            if (!State.InSegment || gState != GameState.PlayingLevel)
                return;

            var human = Human.Localplayer;
            if (human == null)
            {
                _prevHumanStateInit = false;
                return;
            }

            if (!_prevHumanStateInit)
            {
                _prevHumanState = human.state;
                _prevHumanStateInit = true;
                return;
            }

            var prev = _prevHumanState;
            if (human.state == HumanState.Spawning && prev != HumanState.Spawning)
                State.RestartWakeUpMeasurement(State.PlayableTicks, State.PauseAccum);
            _prevHumanState = human.state;
        }

        /// <summary>
        /// Record the current Wake Up Time when the local player leaves the
        /// soft/spawn state. The duration is measured from
        /// <see cref="RunState.WakeUpMeasureStartTicks"/>, which is the segment
        /// start in "only first wake-up" mode and the latest respawn/restart
        /// moment in the default mode. Once recorded it is not reset by later
        /// manual play-dead within the same measurement; a new respawn clears it
        /// so the next wake-up can be measured.
        /// </summary>
        private void TrackWakeUp(Game game, GameState gState)
        {
            if (State.WakeUpTicks.HasValue || !State.InSegment || gState != GameState.PlayingLevel)
                return;
            var human = Human.Localplayer;
            if (human == null)
                return;
            if (human.state == HumanState.Spawning || human.state == HumanState.Unconscious || human.state == HumanState.Dead)
                return;

            State.WakeUpTicks = State.PlayableTicks >= State.WakeUpMeasureStartTicks
                ? State.PlayableTicks - State.WakeUpMeasureStartTicks
                : 0UL;
            State.WakeUpPause = State.PauseAccum >= State.WakeUpMeasureStartPause
                ? State.PauseAccum - State.WakeUpMeasureStartPause
                : 0d;
        }

        // ── Accumulation (B.1) ─────────────────────────────────────────────
        private void Accumulate(Game game, GameState gState, AppSate aState)
        {
            // Once the pass hook has latched the segment's exact end tick the
            // game clock is frozen at it, even though the state may stay
            // PlayingLevel for a few more physics steps while the game starts
            // the load (TB-3).
            if (State.PendingEndTicks.HasValue)
                return;
            if (SegmentLogic.ShouldAccumulateFixed(gState, aState, State.TimingActive))
                State.PlayableTicks++;
        }

        // ── Per-tick tag rules (skip/jump/nocheckpoint/voiceline-tick) ──────
        private void RunRules(Game game, GameState gState)
        {
            // Tag rules only apply to an active segment. After a manual reset
            // clears the segment while the level is still PlayingLevel, running
            // OnTick with stale per-level tag state would immediately re-raise
            // forgivable invalid flags (e.g. NoCheckpointHit / Jumpless).
            if (!State.InSegment)
                return;

            // Track max checkpoint seen for EditorPick final validation.
            int cp = game.currentCheckpointNumber;
            if (cp > State.MaxCheckpointThisLevel) State.MaxCheckpointThisLevel = cp;
            State.PrevCheckpoint = UpdateCheckpointEdge(cp);

            if (gState != GameState.PlayingLevel) return;

            ForEachEnabledRule(rule => Safe(rule, r => r.OnTick(MakeContext(game))));
        }

        /// <summary>Run an action for each registered rule whose tag is enabled.</summary>
        private void ForEachEnabledRule(System.Action<ITagRule> action)
        {
            if (TagRuleRegistry.Instance == null || _cfg.EnabledTags == null) return;
            foreach (var tagId in _cfg.EnabledTags.Tags)
            {
                var rule = TagRuleRegistry.Instance.Find(tagId);
                if (rule != null)
                    action(rule);
            }
        }

        // Edge-cache for checkpoint transitions, returning the previous value.
        private int _cpEdgeCache;
        private bool _cpEdgeInit;
        private int UpdateCheckpointEdge(int cp)
        {
            int prev = _cpEdgeInit ? _cpEdgeCache : cp;
            _cpEdgeCache = cp;
            _cpEdgeInit = true;
            return prev;
        }

        private ValidationContext MakeContext(Game game)
        {
            return new ValidationContext(
                State, game,
                game.currentCheckpointNumber, State.PrevCheckpoint,
                State.Flags, _cfg.Localization);
        }

        // ── Keybinds (R1.7.1 reset, R6 retry, settings panel) ──
        private void HandleKeybinds()
        {
            var s = _cfg.Settings;

            // The settings panel key always works (so the user can open/close it).
            if (InputUtil.GetKeyDown(s.MenuKey))
            {
                if (SettingsPanel.Instance != null)
                    SettingsPanel.Instance.Toggle();
                return;
            }

            // While the panel is open, suppress gameplay keybinds so typing /
            // rebinding inside the panel doesn't reset the run or retry.
            if (SettingsPanel.Instance != null && SettingsPanel.Instance.IsVisible)
                return;

            // While any other keyboard-capturing UI is open (chat, text input,
            // dialog, and the in-game dev console), suppress gameplay keybinds
            // too. Without this, typing an 'r' inside 'hsr status' can trigger a
            // retry, and Backspace can silently full-reset a run while fixing a
            // typo (same guard as RetryAction R6.1.2a).
            if (MenuSystem.keyboardState != KeyboardState.None)
                return;

            if (LeaderboardHud.Instance != null && InputUtil.GetKeyDown(s.SubsegmentToggleKey))
                LeaderboardHud.Instance.CycleMode();

            if (InputUtil.GetKeyDown(s.ResetKey))
            {
                DoFullReset(keepLastValues: false);

                // R1.7.1: a manual reset must clear AND stop the timer.
                // RunState.Reset zeroes the transition caches, which would make
                // the still-active PlayingLevel look like a fresh
                // LoadingLevel/Inactive -> PlayingLevel segment start on the next
                // FixedUpdate, immediately restarting the timer and re-arming the
                // Wake Up Time display. Restore the caches to the actual game
                // state so the timer stays stopped until a real level entry.
                var game = Game.instance;
                if (game != null && game.state == GameState.PlayingLevel)
                {
                    State.PrevGameState = game.state;
                    State.PrevAppState = App.state;
                }

                _cfg.SaveSettings();
                Notify("NOTIFY_RUN_RESET");
            }
            if (InputUtil.GetKeyDown(s.RetryKey))
            {
                // A successful retry is silent: logging its outcome on every
                // press would spam the BepInEx log during normal play. Only log
                // when the retry is refused, so the block reason stays
                // diagnosable without the per-press noise.
                if (RetryAction.TryExecute(this, State, s, out string key))
                    UpdateOptions(); // restart may change timing context
                else
                    Notify(key);
            }
        }

        /// <summary>
        /// Keep label-only tags (<see cref="TagLabels"/>, R3.10) in sync with the
        /// live game mode. The Co-op label is enabled only while a multiplayer
        /// session is active (<c>NetGame.isServer</c> or <c>NetGame.isClient</c>)
        /// and removed as soon as the player is back in single-player. It carries
        /// no rule and no validity flag (nothing in the rule pipeline references
        /// it), and it is never persisted — this only turns the label on/off on
        /// the enabled-tag display surfaces (HUD tags line, {category}).
        /// Called every frame from <see cref="Update"/>.
        /// </summary>
        private void SyncAutoLabels()
        {
            var tags = _cfg != null ? _cfg.EnabledTags : null;
            if (tags == null) return;
            bool coop = CoopLabelOverride ?? (NetGame.isServer || NetGame.isClient);
            if (coop)
                tags.Enable(TagLabels.Coop);
            else
                tags.Disable(TagLabels.Coop);
        }

        /// <summary>
        /// Session-only override for the Co-op auto label (R3.10), set by the
        /// dev console ('hsr tag label on|off|auto') so the label's display can
        /// be tested without an actual multiplayer session (mirrors the 'hsr
        /// flags raise' test tool). Null = follow the live game mode (default).
        /// </summary>
        public static bool? CoopLabelOverride;

        /// <summary>
        /// Public entry point used by the in-game dev console to perform the
        /// same full-run reset as the reset key.
        /// </summary>
        public static void ResetRun()
        {
            var core = Instance;
            if (core == null || State == null)
                return;

            core.DoFullReset(keepLastValues: false);

            // A manual reset must clear AND stop the timer; restore the
            // transition caches to the actual game state so the still-active
            // PlayingLevel does not look like a fresh segment start (mirrors the
            // reset-key path in HandleKeybinds).
            var game = Game.instance;
            if (game != null && game.state == GameState.PlayingLevel)
            {
                State.PrevGameState = game.state;
                State.PrevAppState = App.state;
            }

            core._cfg?.SaveSettings();
            core.Notify("NOTIFY_RUN_RESET");
        }

        /// <summary>Re-read timing options from the live settings model.</summary>
        public void RefreshTimingOptions() => UpdateOptions();

        /// <summary>
        /// Public entry point used by pause-menu patches to clear the current
        /// Wake Up Time and start a fresh measurement from the current game time.
        /// Also records the current human state as already seen so the normal
        /// FixedUpdate respawn detection does not immediately restart again.
        /// </summary>
        public static void RestartWakeUpMeasurement()
        {
            var core = Instance;
            if (core == null || State == null)
                return;
            State.RestartWakeUpMeasurement(State.PlayableTicks, State.PauseAccum);
            var human = Human.Localplayer;
            if (human != null)
            {
                core._prevHumanState = human.state;
                core._prevHumanStateInit = true;
            }
            else
            {
                core._prevHumanStateInit = false;
            }
        }

        // ── Precise boundary hooks (TB-3) ─────────────────────────────────
        /// <summary>
        /// Latch the authoritative segment-start tick. Called from the
        /// <c>Game.AfterLoad</c> postfix — the moment the game assigns
        /// <c>state = PlayingLevel</c> (R1.2.2). The value is consumed by
        /// <see cref="StartSegment"/> on the next physics step. Recording the
        /// tick here makes the boundary independent of the script execution
        /// order that used to add a random ±1 tick.
        /// </summary>
        public static void RecordSegmentStart()
        {
            var core = Instance;
            var st = State;
            if (core == null || st == null)
                return;
            core._segmentStartLatched = true;
            core._segmentStartTicks = st.PlayableTicks;
            core._segmentStartPause = st.PauseAccum;
            long loadLevel = Game.instance != null ? Game.instance.currentLevelNumber : -1;
            LogBoundary($"load  level={loadLevel} tick={st.PlayableTicks} step={GameClock.CurrentTick} processed={HasProcessedCurrentPhysicsStep}");
        }

        /// <summary>
        /// Latch the authoritative segment-end tick after the game has detected
        /// a genuine level pass (<c>Game.Fall</c> taking the <c>passedLevel</c>
        /// branch, R1.4.2). The tick is the step's exact index, including the
        /// pass step itself ("终点含最后一帧", TB-4); the poll then freezes
        /// accumulation at it and records the segment on the observed state
        /// flip. Suppressed under <c>Retrying</c> (R6) and outside a segment, so
        /// an abandoned attempt never ends a segment (TB-7).
        /// </summary>
        public static void RecordLevelPass()
        {
            var core = Instance;
            var st = State;
            if (core == null || st == null)
                return;
            if (!st.InSegment || st.Retrying)
                return;
            if (st.PendingEndTicks.HasValue)
                return;

            st.LevelPassed = true;
            // The hook can run before or after this plugin's FixedUpdate within
            // the same physics step. When it runs first, the pass step has not
            // been counted yet — include it explicitly (but only if this step is
            // actually a playable one, so a boundary in a non-accumulating
            // context cannot over-count). Accumulation is frozen from here so it
            // is never double-counted.
            ulong endTicks = st.PlayableTicks;
            if (!HasProcessedCurrentPhysicsStep
                && Game.instance != null
                && SegmentLogic.ShouldAccumulateFixed(Game.instance.state, App.state, st.TimingActive))
            {
                endTicks++;
            }
            st.PlayableTicks = endTicks;
            st.PendingEndTicks = endTicks;
            st.PendingEndPause = st.PauseAccum;
            st.GameTimeSeconds = GameClock.Seconds(st.PlayableTicks, st.PauseAccum);
            LogBoundary($"pass  level={st.CurrentLevelNumber} tick={endTicks} step={GameClock.CurrentTick}");
        }

        /// <summary>
        /// Latch only the completion flag (used by the <c>Game.EnterPassZone</c>
        /// postfix, R1.4.2). The segment's end tick is recorded later, at the
        /// authoritative <c>Game.Fall</c> pass detection.
        /// </summary>
        public static void LatchLevelPassed()
        {
            var st = State;
            if (st != null && st.InSegment)
                st.LevelPassed = true;
        }

        private void DoFullReset(bool keepLastValues, bool keepLastRun = false)
        {
            SubsegmentManager.Instance?.OnRunReset();
            MarkersManager.Instance?.OnRunReset();
            State.Reset(keepLastValues, keepLastRun);
            State.Flags.ClearAll();
            _cpEdgeInit = false;
            _prevHumanStateInit = false;
            _segmentStartLatched = false;
            UpdateOptions();
        }

        // ── helpers ──
        private void Notify(string key, string arg = null)
        {
            if (string.IsNullOrEmpty(key)) return;
            string msg = arg != null ? _cfg.Localization.Get(key, arg) : _cfg.Localization.Get(key);
            Plugin.Logger.LogInfo($"HSRTimer: {msg}");
        }

        private static void Safe(ITagRule rule, System.Action<ITagRule> action)
        {
            try { action(rule); }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: tag rule '{rule.Id}' threw: {ex.Message}");
            }
        }
    }
}
