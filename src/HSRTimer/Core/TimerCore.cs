using Multiplayer;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// The timer engine. A single polling MonoBehaviour drives all timing,
    /// segment, reset, validity, and tag logic each frame by reading public
    /// game fields — no game-method patching (see docs/ARCHITECTURE.md).
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

                // Detect transitions first (uses cached prev), then accumulate,
                // then run per-tick rules.
                HandleTransitions(game, gState, aState, isLocal);
                Accumulate(game, gState, aState);
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

            GameState gState = Game.instance != null ? Game.instance.state : GameState.Inactive;

            // Generic always-on validity check (R5.1): cheat codes.
            GenericValidators.CheckCheat(State.Flags);

            // B.2 pause supplement (runs here because FixedUpdate is paused when timeScale=0).
            if (SegmentLogic.ShouldAccumulatePause(gState, State.TimingActive, _opt))
                State.GameTime += Time.unscaledDeltaTime;

            HandleKeybinds();
        }

        // ── Transition handling (Appendix B + R1.7 resets) ──────────────────
        private void HandleTransitions(Game game, GameState gState, AppSate aState, bool isLocal)
        {
            GameState prevG = State.PrevGameState;
            AppSate prevA = State.PrevAppState;

            // R1.4 segment end must be recorded BEFORE any R1.7 full-run clear:
            // the PlayingLevel→Inactive (local) edge is both a segment end (R1.4.1)
            // and an auto-reset trigger (R1.7.3). Recording is unconditional; only
            // the clear is gated by the AutoReset option. EndSegment is a no-op when
            // no segment is active, so this is safe on every other transition.
            if (SegmentLogic.IsSegmentEnd(prevG, gState, isLocal))
                EndSegment(game, completed: State.LevelPassed);

            // R1.7 auto-reset (honored only when the option is on). Suppressed
            // during a retry (R6) so reloading the level does not clear the run.
            // keepLastValues: an auto-reset clears the live run but preserves the
            // "last segment / last run" HUD snapshots (LastSegment /
            // TotalAtLastSegment / LastRun) — those are reference values that
            // update only when a new value is recorded or a manual reset clears
            // them, not on every auto-reset.
            if (_opt.AutoReset && SegmentLogic.IsAutoReset(prevG, gState, prevA, aState, isLocal, State.Retrying))
            {
                DoFullReset(keepLastValues: true);
                return;
            }

            // R6.4: latch the Menu→LoadLevel edge for the next segment start.
            // This fires only on a genuine menu entry (campaign advance and a
            // retry's reload never pass through Menu — see SegmentLogic.IsMenuEntry),
            // so the soon-to-start level is the one to remember as the campaign
            // retry target. The edge is consumed at segment start below.
            if (SegmentLogic.IsMenuEntry(prevA, aState))
                State.MenuEntryPending = true;

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
            State.BeginSegment(State.GameTime, game.currentLevelNumber, game.currentLevelType, cp);
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

            // R6.4: if this segment was entered from the menu and is a playable
            // campaign level (BuiltIn, within the Intro–Reprise range, not the
            // Credits epilogue, not part of an LC collection run), remember it
            // as the campaign retry target. A retry during this run then returns
            // here even after advancing to later campaign levels. The Credits
            // level is excluded because it has no gameplay; a collection-run
            // level is excluded because R6.3 (LC delegation) owns those retries.
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
            }
            State.MenuEntryPending = false;

            State.Game = game; // cache for the HUD / rules

            // Fire tag OnLevelEnter for every enabled tag.
            ForEachEnabledRule(rule => Safe(rule, r => r.OnLevelEnter(MakeContext(game))));
        }

        private void EndSegment(Game game, bool completed)
        {
            double end = State.GameTime;
            bool retrying = State.Retrying;
            State.EndSegment(end, completed);

            // Fire tag OnLevelExit — but only for a genuine level completion. A
            // retry or a mid-level quit abandons the level (its reload/leave
            // drives it through PlayingLevel → Inactive/LoadingLevel, a segment
            // end); running OnLevelExit then would let the checkpoint final-check
            // (R4.2) and voiceline-completion check fire against the abandoned
            // state and spuriously raise INVALID_CHECKPOINT_FINAL / Voiceline.
            if (!retrying && completed)
                ForEachEnabledRule(rule => Safe(rule, r => r.OnLevelExit(MakeContext(game))));

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
                    State.LastRun = end;
            }

            if (!retrying)
                _cfg.SaveSettings();
        }

        // ── Accumulation (B.1 + B.3) ────────────────────────────────────────
        private void Accumulate(Game game, GameState gState, AppSate aState)
        {
            if (SegmentLogic.ShouldAccumulateFixed(gState, aState, State.TimingActive))
                State.GameTime += Time.fixedDeltaTime;
            else if (SegmentLogic.ShouldAccumulateMenu(gState, aState, State.TimingActive, _opt, State.Retrying))
                State.GameTime += Time.fixedDeltaTime;
        }

        // ── Per-tick tag rules (skip/jump/nocheckpoint/voiceline-tick) ──────
        private void RunRules(Game game, GameState gState)
        {
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
            if (Input.GetKeyDown(s.MenuKey))
            {
                if (SettingsPanel.Instance != null)
                    SettingsPanel.Instance.Toggle();
                return;
            }

            // While the panel is open, suppress gameplay keybinds so typing /
            // rebinding inside the panel doesn't reset the run or retry.
            if (SettingsPanel.Instance != null && SettingsPanel.Instance.IsVisible)
                return;

            if (Input.GetKeyDown(s.ResetKey))
            {
                DoFullReset(keepLastValues: false);
                _cfg.SaveSettings();
                Notify("NOTIFY_RUN_RESET");
            }
            if (Input.GetKeyDown(s.RetryKey))
            {
                if (RetryAction.TryExecute(this, State, s, out string key))
                    UpdateOptions(); // restart may change timing context
                Notify(key);
            }
        }

        private void DoFullReset(bool keepLastValues)
        {
            State.Reset(keepLastValues);
            State.Flags.ClearAll();
            _cpEdgeInit = false;
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
