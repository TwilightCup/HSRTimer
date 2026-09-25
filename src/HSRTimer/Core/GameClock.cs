using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// The single conversion facade between the engine's integer physics-tick
    /// clock and wall-clock seconds (TB-1/TB-2). This is the ONLY place in the
    /// plugin that reads <c>Time.fixedDeltaTime</c>: the timer core
    /// (<see cref="TimerCore"/>), <see cref="RunState"/> and
    /// <see cref="SegmentLogic"/> all work in integer ticks and never touch the
    /// engineering constant; display/persistence convert through here.
    ///
    /// <see cref="CurrentTick"/> is a monotonic counter of physics steps. It is
    /// advanced lazily the first time it is observed for a given
    /// <c>Time.fixedTime</c>, so every script running inside the same physics
    /// step — this plugin's <c>TimerCore.FixedUpdate</c> and the game's own
    /// boundary methods that the precise hooks run on — reads the SAME tick no
    /// matter how Unity orders the scripts. That order independence is what
    /// makes the boundary hooks deterministic (TB-3/TB-5): a boundary recorded
    /// from inside the game's physics step is the same integer the poll would
    /// have seen, without depending on which FixedUpdate ran first.
    /// </summary>
    public static class GameClock
    {
        private static float _lastFixedTime = float.NaN;
        private static long _tick;

        /// <summary>
        /// Advance the counter if this is the first observation of a new physics
        /// step. Safe (and a no-op) to call repeatedly within one step.
        /// </summary>
        public static void Advance()
        {
            float fixedTime = Time.fixedTime;
            if (fixedTime != _lastFixedTime)
            {
                _lastFixedTime = fixedTime;
                _tick++;
            }
        }

        /// <summary>
        /// Monotonic index of the current physics step. All callers within one
        /// physics step (regardless of script execution order) get the same
        /// value; a new step advances it exactly once.
        /// </summary>
        public static long CurrentTick
        {
            get
            {
                Advance();
                return _tick;
            }
        }

        /// <summary>The fixed physics step in seconds (uses fixedTime).</summary>
        public static double TickSeconds => Time.fixedDeltaTime;

        /// <summary>
        /// Convert an integer tick count plus separately accumulated pause wall
        /// time into seconds. One multiply, no per-frame float accumulation —
        /// the same tick count always converts to the same seconds value (4.3).
        /// </summary>
        public static double Seconds(ulong ticks, double pauseAccum)
            => (double)ticks * Time.fixedDeltaTime + pauseAccum;

        public static long ToMs(double seconds) => (long)System.Math.Round(seconds * 1000.0);

        // ── RunState snapshots, converted on demand (display/persistence) ──

        /// <summary>Total at the start of the current segment, in seconds.</summary>
        public static double SegmentStartSeconds(RunState s)
            => Seconds(s.SegmentStartTicks, s.SegmentStartPause);

        /// <summary>Current segment time (<c>PlayableTicks - SegmentStartTicks</c>), in seconds.</summary>
        public static double SegmentSeconds(RunState s)
            => Seconds(
                s.PlayableTicks >= s.SegmentStartTicks ? s.PlayableTicks - s.SegmentStartTicks : 0UL,
                s.PauseAccum >= s.SegmentStartPause ? s.PauseAccum - s.SegmentStartPause : 0d);

        public static long SegmentMs(RunState s) => ToMs(SegmentSeconds(s));

        public static double? LastSegmentSeconds(RunState s)
            => s.LastSegmentTicks.HasValue
                ? Seconds(s.LastSegmentTicks.Value, s.LastSegmentPause)
                : (double?)null;

        public static double? TotalAtLastSegmentSeconds(RunState s)
            => s.TotalAtLastSegmentTicks.HasValue
                ? Seconds(s.TotalAtLastSegmentTicks.Value, s.TotalAtLastSegmentPause)
                : (double?)null;

        public static double? LastRunSeconds(RunState s)
            => s.LastRunTicks.HasValue
                ? Seconds(s.LastRunTicks.Value, s.LastRunPause)
                : (double?)null;

        public static double? WakeUpSeconds(RunState s)
            => s.WakeUpTicks.HasValue
                ? Seconds(s.WakeUpTicks.Value, s.WakeUpPause)
                : (double?)null;
    }
}
