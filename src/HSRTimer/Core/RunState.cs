using Multiplayer;

namespace HSRTimer
{
    /// <summary>
    /// Mutable runtime state of the timer engine. This is the single source of
    /// truth for all displayed values. It is plain data (no Unity callbacks) so
    /// it can be reset cheaply on a full-run reset (R1.7) without reallocation.
    /// </summary>
    public sealed class RunState
    {
        // ── Time ──────────────────────────────────────────────────────
        /// <summary>Accumulated game time since the run started, in seconds.</summary>
        public double GameTime;

        /// <summary>
        /// Snapshot of <see cref="GameTime"/> at the start of the current
        /// segment (level). The current segment time is GameTime - SegmentStart.
        /// </summary>
        public double SegmentStart;

        /// <summary>Duration of the most recently completed segment, or null.</summary>
        public double? LastSegment;

        /// <summary>
        /// Total game time frozen at the instant the most recently completed
        /// segment ended (i.e. the cumulative time "as of last segment
        /// completion"), or null. Unlike <see cref="LastSegment"/> (the segment's
        /// duration), this is the run total at that moment. Snapshotted in
        /// <see cref="EndSegment"/>.
        /// </summary>
        public double? TotalAtLastSegment;

        /// <summary>Total game time of the most recently completed run, or null.</summary>
        public double? LastRun;

        /// <summary>
        /// Time from the start of the current level to
        /// the first time the local player leaves the soft/spawn state
        /// (<c>Spawning</c> / <c>Unconscious</c> / <c>Dead</c>), or null before
        /// that first wake-up has been observed. Cleared when a new playable
        /// level starts, when the level ends/exits, and on any reset — it is
        /// purely a live per-level display value.
        /// </summary>
        public double? WakeUpTime;

        /// <summary>
        /// Accumulated wall-clock time since the current run began. Unlike
        /// <see cref="GameTime"/>, this keeps advancing through level loading
        /// screens and pauses, so it represents the real time spent on the run.
        /// </summary>
        public double RealTime;

        /// <summary>
        /// True while the real-time clock is actively accumulating. Starts when
        /// the game clock starts a run, keeps running through loading screens,
        /// and stops when the run completes (the same moment the game clock
        /// stops after the final level).
        /// </summary>
        public bool RealTimeActive;

        /// <summary>True while the clock is actively accumulating game time.</summary>
        public bool TimingActive;

        /// <summary>True once the current segment (level) has been started.</summary>
        public bool InSegment;

        // ── Transition caches (previous-tick values for edge detection) ──
        public GameState PrevGameState = GameState.Inactive;
        public AppSate PrevAppState = AppSate.Startup;

        /// <summary>Previous checkpoint number, for skip detection (R4.1).</summary>
        public int PrevCheckpoint;

        /// <summary>Highest checkpoint number observed in the current level.</summary>
        public int MaxCheckpointThisLevel;

        // ── Context bookkeeping ───────────────────────────────────────
        /// <summary>Level number active for the current segment (-1 if none).</summary>
        public int CurrentLevelNumber = -1;

        /// <summary>
        /// Level source (BuiltIn / EditorPick / Workshop) snapshot at the start
        /// of the current segment. Captured then (not read at segment end)
        /// because by the time a completion drives the level out of
        /// PlayingLevel the game may already be mid-transition into the next
        /// level, with <c>currentLevelType</c> updated. Used by the LastRun rule
        /// to tell apart a campaign completion, a standalone EditorPick, etc.
        /// </summary>
        public WorkshopItemSource CurrentLevelType = WorkshopItemSource.BuiltIn;

        /// <summary>
        /// True when the run was started fresh from the menu (used by auto-reset
        /// bookkeeping; mirrors the spec's "started from menu" flag).
        /// </summary>
        public bool StartedFromMenu = true;

        /// <summary>True for one FixedUpdate tick after a full reset fires.</summary>
        public bool JustReset;

        /// <summary>
        /// True for one tick after a segment ends; lets the HUD / LC integration
        /// capture the total time at the instant a run/segment completes.
        /// </summary>
        public bool SegmentJustEnded;

        /// <summary>
        /// Whether the current level has been passed (reached its exit / pass
        /// zone) this segment. Latched on every tick from
        /// <c>Game.passedLevel</c> (OR'd, so it stays true once the pass zone is
        /// hit even though the game clears <c>passedLevel</c> during the
        /// completion/leave flow). Cleared on segment start and reset. Used to
        /// tell a genuine level <b>completion</b> (which should record the
        /// segment) apart from a mid-level <b>quit</b> (which must not) — both
        /// drive the level out of PlayingLevel, and a Workshop/EditorPick
        /// completion even leaves via the same PauseLeave path as a quit.
        /// </summary>
        public bool LevelPassed;

        /// <summary>
        /// Whether the current segment is on the LAST level of an LC collection
        /// run, latched per tick from <c>LcIntegration.IsLastLevelOfCollection</c>.
        /// Latching is required because LC's Harmony patch ends the run
        /// synchronously inside <c>Game.Fall</c> (before the engine's next
        /// FixedUpdate observes the state flip) — by the time EndSegment runs,
        /// <c>IsInCollectionRun</c> is already false. Cleared on segment start
        /// and reset.
        /// </summary>
        public bool OnCollectionLastLevel;

        /// <summary>
        /// Whether the current segment is inside an LC collection run (on ANY
        /// of its levels), latched per tick from
        /// <c>LcIntegration.IsInCollectionRun</c>. Companion to
        /// <see cref="OnCollectionLastLevel"/>: used to tell a *standalone*
        /// EditorPick completion (which ends its run) apart from a mid-collection
        /// EditorPick level (which just advances the collection).
        /// </summary>
        public bool InCollectionRunSegment;

        /// <summary>
        /// True while the current segment is the campaign epilogue — the Credits
        /// level (BuiltIn, index == levelCount) the game loads right after the
        /// final playable level is passed. The Credits segment belongs to the
        /// run that just finished (it is that run's closing cinematic), so it
        /// must NOT be treated as "a new run started timing": the HUD keeps
        /// showing the just-recorded LastRun during it, and it is exempt from
        /// run-completion recording itself.
        /// </summary>
        public bool InEpilogueSegment;

        /// <summary>
        /// Set while a one-key level retry (R6) is reloading the level. The level
        /// goes through PlayingLevel → Inactive → LoadingLevel → PlayingLevel; the
        /// engine uses this flag to treat that as a *segment* restart (the current
        /// level's timer resets) while keeping it independent of the run reset
        /// (R6.2.2): game time and LastRun are preserved across a retry. Cleared
        /// once the reloaded level begins a new segment.
        /// </summary>
        public bool Retrying;

        // ── R6.4: campaign "entered from menu" retry target ──────────
        /// <summary>
        /// The campaign (BuiltIn, Intro–Reprise) level the run was entered
        /// from the menu — the level a one-key retry should return to during a
        /// campaign run (R6.4), instead of reloading whatever level happens to
        /// be playing (which on a mid-run advance would be a later level the
        /// player never chose to start from). -1 while the current run was not
        /// entered from the menu (EditorPick/Workshop/collection/advance), in
        /// which case retry falls back to the current-level reload.
        /// <para>
        /// Latched at segment start the moment a <c>Menu → LoadLevel</c> edge
        /// was seen this run (see <see cref="MenuEntryPending"/>), for a BuiltIn
        /// level that is not the Credits epilogue and is not inside an LC
        /// collection run. Once set it persists for the whole run: a campaign
        /// advance (<c>PlayLevel → LoadLevel</c>, no <c>Menu</c>) does not
        /// re-trip the menu edge, so the remembered entry level survives until
        /// the run ends or a reset clears it. Only a fresh menu entry
        /// overwrites it (starting a new campaign run from a different level).
        /// </para>
        /// </summary>
        public int CampaignRetryLevel = -1;

        /// <summary>
        /// Set when a <c>Menu → LoadLevel</c> App-state edge fires during the
        /// current run (the player entered a level from the menu). Latched into
        /// <see cref="CampaignRetryLevel"/> at the next segment start, then
        /// cleared. Keeps the edge (a frame-scoped transition) alive until the
        /// segment-start logic that consumes it runs.
        /// </summary>
        public bool MenuEntryPending;
        /// <summary>The active validity flags for this run (R5).</summary>
        public readonly ValidityFlags Flags = new ValidityFlags();

        /// <summary>Cached Game.instance for the HUD / rules (refreshed on segment start).</summary>
        public Game Game;

        /// <summary>
        /// Full-run reset: zero all time and segment state. Per-level tag state
        /// is reset separately by the engine on the next segment start. Clears
        /// the "last segment / last run" snapshots too — use this for a manual
        /// reset (the reset key).
        /// </summary>
        public void Reset() => Reset(keepLastValues: false, keepLastRun: false);

        /// <summary>
        /// Compatibility overload: clears everything, including the previous
        /// run's total.
        /// </summary>
        public void Reset(bool keepLastValues) => Reset(keepLastValues, keepLastRun: false);

        /// <summary>
        /// Full-run reset. When <paramref name="keepLastValues"/> is true the
        /// segment snapshots (<see cref="LastSegment"/> and
        /// <see cref="TotalAtLastSegment"/>) are preserved. When
        /// <paramref name="keepLastRun"/> is true, the previous run's total
        /// (<see cref="LastRun"/>) is preserved separately — auto-reset and
        /// menu-entry reset keep it as the "previous completed run" reference,
        /// while the manual reset clears everything. Live timers and
        /// segment/transition caches are zeroed regardless.
        /// </summary>
        public void Reset(bool keepLastValues, bool keepLastRun)
        {
            GameTime = 0d;
            SegmentStart = 0d;
            if (!keepLastValues)
            {
                LastSegment = null;
                TotalAtLastSegment = null;
            }
            if (!keepLastRun)
                LastRun = null;
            // Wake Up time is a per-level live stat only; it is cleared whenever
            // the level ends/exits and on any reset.
            WakeUpTime = null;
            RealTime = 0d;
            RealTimeActive = false;
            TimingActive = false;
            InSegment = false;
            PrevGameState = GameState.Inactive;
            PrevAppState = AppSate.Startup;
            PrevCheckpoint = 0;
            MaxCheckpointThisLevel = 0;
            CurrentLevelNumber = -1;
            CurrentLevelType = WorkshopItemSource.BuiltIn;
            StartedFromMenu = true;
            JustReset = true;
            SegmentJustEnded = false;
            Retrying = false;
            LevelPassed = false;
            OnCollectionLastLevel = false;
            InCollectionRunSegment = false;
            InEpilogueSegment = false;
            MenuEntryPending = false;
            // CampaignRetryLevel deliberately survives a full-run reset: it is
            // "the level the player last entered from the menu", which stays
            // meaningful across runs (the player usually retries the same
            // level). Only a fresh Menu→LoadLevel edge overwrites it.
        }

        /// <summary>
        /// Begin a new segment (level): snapshot the start time and mark active.
        /// Does not touch accumulated run time.
        /// </summary>
        public void BeginSegment(double gameTime, int levelNumber, WorkshopItemSource levelType, int startCheckpoint)
        {
            SegmentStart = gameTime;
            TimingActive = true;
            InSegment = true;
            CurrentLevelNumber = levelNumber;
            CurrentLevelType = levelType;
            PrevCheckpoint = startCheckpoint;
            MaxCheckpointThisLevel = startCheckpoint;
            WakeUpTime = null;
            SegmentJustEnded = false;
            LevelPassed = false;
            OnCollectionLastLevel = false;
            InCollectionRunSegment = false;
            InEpilogueSegment = false;
        }

        /// <summary>
        /// End the current segment and deactivate the clock. Only records the
        /// segment's duration and the run total at that instant when
        /// <paramref name="completed"/> is true — i.e. the level was actually
        /// <b>passed</b>, not abandoned by a mid-level quit. No-op if no segment
        /// is active (avoids recording a garbage segment from a stale transition
        /// cache, e.g. across a Game.instance null window).
        /// </summary>
        public void EndSegment(double gameTime, bool completed)
        {
            if (!InSegment) return;
            if (completed)
            {
                LastSegment = gameTime - SegmentStart;
                TotalAtLastSegment = gameTime;
            }
            TimingActive = false;
            InSegment = false;
            SegmentJustEnded = true;
            WakeUpTime = null; // the level is over; do not keep showing it
        }
    }
}
