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
        // ── Game clock: integer physics ticks (TB-1) ──────────────────
        /// <summary>
        /// Playable <c>FixedUpdate</c> frames since the run started: one
        /// increment per physics frame in which the engine may accumulate
        /// (B.1). This is the core's game-time representation — no floating
        /// point, no dependency on the physics step constant.
        /// </summary>
        public ulong PlayableTicks;

        /// <summary>
        /// Paused wall-clock seconds since the run started, accumulated with
        /// <c>Time.unscaledDeltaTime</c> (R1.8.3/TB-6). Pausing halts
        /// <c>FixedUpdate</c>, so there are no ticks to count; this is the one
        /// documented non-tick component and is merged back in by
        /// <see cref="GameClock.Seconds"/> only at the display boundary.
        /// </summary>
        public double PauseAccum;

        /// <summary>
        /// Read-only cache of <c>GameClock.Seconds(PlayableTicks, PauseAccum)</c>,
        /// refreshed by the engine each tick (and each pause frame). Consumers
        /// that only display the total may keep reading a single double without
        /// converting themselves (4.1).
        /// </summary>
        public double GameTimeSeconds;

        /// <summary>
        /// Snapshot of <see cref="PlayableTicks"/> at the start of the current
        /// segment (level). The current segment time is the integer difference
        /// <c>PlayableTicks - SegmentStartTicks</c>.
        /// </summary>
        public ulong SegmentStartTicks;

        /// <summary>Snapshot of <see cref="PauseAccum"/> at the start of the current segment.</summary>
        public double SegmentStartPause;

        /// <summary>
        /// Exact end tick of the current segment, latched by the authoritative
        /// pass-zone boundary hook (TB-3), or null when no pass has been
        /// recorded. Once set, the poll freezes accumulation so the segment
        /// ends on the hook's tick, not on the frame the state flip happens to
        /// be observed.
        /// </summary>
        public ulong? PendingEndTicks;

        /// <summary>Snapshot of <see cref="PauseAccum"/> at the instant <see cref="PendingEndTicks"/> was recorded.</summary>
        public double PendingEndPause;

        /// <summary>Duration of the most recently completed segment, in ticks, or null.</summary>
        public ulong? LastSegmentTicks;

        /// <summary>Pause wall time accrued during the most recently completed segment, or 0.</summary>
        public double LastSegmentPause;

        /// <summary>
        /// Run total (in ticks) frozen at the instant the most recently
        /// completed segment ended, or null. Unlike <see cref="LastSegmentTicks"/>
        /// (the segment's duration), this is the run total at that moment.
        /// Snapshotted in <see cref="EndSegment"/>.
        /// </summary>
        public ulong? TotalAtLastSegmentTicks;

        /// <summary>Pause wall time at the instant the last segment ended.</summary>
        public double TotalAtLastSegmentPause;

        /// <summary>Total game ticks of the most recently completed run, or null.</summary>
        public ulong? LastRunTicks;

        /// <summary>Pause wall time accrued during the most recently completed run.</summary>
        public double LastRunPause;

        /// <summary>
        /// Ticks from the current wake-up measurement start to
        /// the local player leaving the soft/spawn state
        /// (<c>Spawning</c> / <c>Unconscious</c> / <c>Dead</c>), or null before
        /// that wake-up has been observed. By default the measurement restarts
        /// on each player respawn / pause-menu checkpoint load / level restart;
        /// with <c>OnlyRecordFirstWakeUpTime</c> it stays fixed to the first
        /// wake-up after a level starts. Cleared when a new playable level
        /// starts, when the level ends/exits, and on any reset — it is purely a
        /// live per-level display value.
        /// </summary>
        public ulong? WakeUpTicks;

        /// <summary>Pause wall time accrued during the current wake-up measurement.</summary>
        public double WakeUpPause;

        /// <summary>
        /// Authoritative tick at which the current Wake Up Time measurement
        /// began. For the original "first wake-up only" mode this is the segment
        /// start; for the default mode it is updated to each respawn/restart
        /// moment so Wake Up Time shows how long the player took to get up after
        /// that particular respawn.
        /// </summary>
        public ulong WakeUpMeasureStartTicks;

        /// <summary>Pause wall time at the start of the current wake-up measurement.</summary>
        public double WakeUpMeasureStartPause;

        /// <summary>
        /// Accumulated wall-clock time since the current run began. Unlike the
        /// game clock, this keeps advancing through level loading
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
        /// When set, the current completion flow must NOT persist subsegment /
        /// marker PBs. Set by the <c>hsr pass</c> test command (a simulated
        /// level pass that should not pollute real PB data); the segment is
        /// still recorded normally (LastSegment / LastRun), only the PB write
        /// paths are skipped. Cleared on segment start and on any full reset.
        /// </summary>
        public bool SuppressPbRecording;

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
        /// overwrites it (starting a new campaign run from a different level);
        /// a fresh menu entry that starts an EditorPick/Workshop/collection
        /// level clears it back to -1 so retry falls back to the current level.
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
        /// segment snapshots (<see cref="LastSegmentTicks"/> and
        /// <see cref="TotalAtLastSegmentTicks"/>) are preserved. When
        /// <paramref name="keepLastRun"/> is true, the previous run's total
        /// (<see cref="LastRunTicks"/>) is preserved separately — auto-reset and
        /// menu-entry reset keep it as the "previous completed run" reference,
        /// while the manual reset clears everything. Live timers and
        /// segment/transition caches are zeroed regardless.
        /// </summary>
        public void Reset(bool keepLastValues, bool keepLastRun)
        {
            PlayableTicks = 0UL;
            PauseAccum = 0d;
            GameTimeSeconds = 0d;
            SegmentStartTicks = 0UL;
            SegmentStartPause = 0d;
            PendingEndTicks = null;
            PendingEndPause = 0d;
            if (!keepLastValues)
            {
                LastSegmentTicks = null;
                LastSegmentPause = 0d;
                TotalAtLastSegmentTicks = null;
                TotalAtLastSegmentPause = 0d;
            }
            if (!keepLastRun)
            {
                LastRunTicks = null;
                LastRunPause = 0d;
            }
            // Wake Up time is a per-level live stat only; it is cleared whenever
            // the level ends/exits and on any reset.
            WakeUpTicks = null;
            WakeUpPause = 0d;
            WakeUpMeasureStartTicks = 0UL;
            WakeUpMeasureStartPause = 0d;
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
            SuppressPbRecording = false;
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
        /// Begin a new segment (level): snapshot the start tick/pause and mark
        /// active. Does not touch accumulated run time.
        /// </summary>
        public void BeginSegment(ulong playableTicks, double pauseAccum, int levelNumber, WorkshopItemSource levelType, int startCheckpoint)
        {
            SegmentStartTicks = playableTicks;
            SegmentStartPause = pauseAccum;
            PendingEndTicks = null;
            PendingEndPause = 0d;
            TimingActive = true;
            InSegment = true;
            CurrentLevelNumber = levelNumber;
            CurrentLevelType = levelType;
            PrevCheckpoint = startCheckpoint;
            MaxCheckpointThisLevel = startCheckpoint;
            WakeUpTicks = null;
            WakeUpPause = 0d;
            WakeUpMeasureStartTicks = SegmentStartTicks;
            WakeUpMeasureStartPause = SegmentStartPause;
            SegmentJustEnded = false;
            LevelPassed = false;
            SuppressPbRecording = false;
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
        /// <para><paramref name="endTicks"/> is the segment's exact end tick:
        /// the pass-zone boundary hook's value when one was latched (TB-3), else
        /// the polled <see cref="PlayableTicks"/>. The run total is normalized
        /// to it so a pass that the poll had not yet counted is still included.</para>
        /// </summary>
        public void EndSegment(ulong endTicks, double endPause, bool completed)
        {
            if (!InSegment) return;
            if (completed)
            {
                LastSegmentTicks = endTicks >= SegmentStartTicks ? endTicks - SegmentStartTicks : 0UL;
                LastSegmentPause = endPause >= SegmentStartPause ? endPause - SegmentStartPause : 0d;
                TotalAtLastSegmentTicks = endTicks;
                TotalAtLastSegmentPause = endPause;
            }
            PlayableTicks = endTicks;
            PauseAccum = endPause;
            PendingEndTicks = null;
            PendingEndPause = 0d;
            TimingActive = false;
            InSegment = false;
            SegmentJustEnded = true;
            WakeUpTicks = null; // the level is over; do not keep showing it
            WakeUpPause = 0d;
        }

        /// <summary>
        /// Clear the current Wake Up Time display and start a fresh measurement
        /// from the given tick/pause. Used by the default respawn-aware
        /// wake-up behavior; not called when "only record first wake-up time" is
        /// enabled.
        /// </summary>
        public void RestartWakeUpMeasurement(ulong playableTicks, double pauseAccum)
        {
            WakeUpTicks = null;
            WakeUpPause = 0d;
            WakeUpMeasureStartTicks = playableTicks;
            WakeUpMeasureStartPause = pauseAccum;
        }
    }
}
