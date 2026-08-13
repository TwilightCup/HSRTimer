using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// The always-on validity checks (R5.1): cheat codes, game-speed change,
    /// and wall-clock drift. These run every frame from the engine and feed a
    /// <see cref="ValidityFlags"/> instance.
    /// </summary>
    public static class GenericValidators
    {
        // timeScale within this absolute distance of 1.0 is treated as unchanged
        // (floating-point tolerance; the game sets exactly 1.0 / 0.0).
        private const float TimeScaleEpsilon = 1e-4f;

        /// <summary>
        /// R5.1.1 + R5.1.2. Cheats are always checked. timeScale is only treated
        /// as a violation while actually playing: during Paused the game sets
        /// timeScale=0 on purpose, which is not a cheat.
        /// </summary>
        public static void CheckCheatAndSpeed(ValidityFlags flags, GameState state)
        {
            if (CheatCodes.climbCheat || CheatCodes.throwCheat)
                flags.Raise(InvalidReason.CheatCode);

            if (state == GameState.PlayingLevel
                && Mathf.Abs(Time.timeScale - 1f) > TimeScaleEpsilon)
            {
                flags.Raise(InvalidReason.TimeScale);
            }
        }
    }

    /// <summary>
    /// R5.1.3 wall-clock drift detector. Compares the physics-stepped time that
    /// *should* have elapsed over a sampling window against the real time that
    /// did elapse. A mismatch beyond the tolerance ratio indicates an external
    /// attempt to slow the physics clock. Sampled periodically, not every frame,
    /// to smooth over normal frame stutter.
    ///
    /// Real elapsed time is measured from the monotonic wall clock
    /// (<see cref="Time.realtimeSinceStartup"/>) sampled once at each window
    /// boundary — NOT by summing per-frame deltas. Per-frame sums are wrong here
    /// because <see cref="Time.unscaledDeltaTime"/> is a per-render-frame value,
    /// yet this is called from <c>FixedUpdate</c>, which Unity runs several
    /// catch-up times per hitched frame; each catch-up tick would re-add the same
    /// large frame delta and inflate the "real" total, tripping a false positive
    /// on ordinary pauses / load hitches. A boundary-to-boundary wall-clock
    /// difference is immune to that.
    ///
    /// The tolerance is a hardcoded anti-cheap constant (R5.1.3 default ±10%);
    /// it is intentionally NOT user-configurable — none of the cheat/speed/drift
    /// detectors may be tuned by the player.
    /// </summary>
    public sealed class DriftDetector
    {
        // Sampling window (seconds) and allowed physics/real ratio deviation.
        private const double WindowSeconds = 2.0;
        private const double Tolerance = 0.10; // ±10%

        private double _physicsAccumulated;
        private double _windowStartReal; // realtimeSinceStartup captured at window open
        private bool _windowOpen;

        /// <summary>
        /// Advance the window by one physics tick (call only while playing; the
        /// engine gates this). Raises <see cref="InvalidReason.Drift"/> when the
        /// accumulated ratio exceeds the hardcoded tolerance at a window boundary.
        /// </summary>
        public void Sample(ValidityFlags flags, bool playing)
        {
            if (!_windowOpen)
            {
                _windowOpen = true;
                _physicsAccumulated = 0d;
                _windowStartReal = Time.realtimeSinceStartup;
            }

            // One physics step == one fixedDeltaTime of "should-have-elapsed" time.
            _physicsAccumulated += Time.fixedDeltaTime;

            double realElapsed = Time.realtimeSinceStartup - _windowStartReal;
            if (realElapsed < WindowSeconds)
                return;

            // Compare the physics time that should have elapsed against the real
            // time that did. Only test once enough real time has passed.
            if (realElapsed > 1e-3)
            {
                double ratio = _physicsAccumulated / realElapsed;
                double deviation = System.Math.Abs(ratio - 1d);
                if (deviation > Tolerance)
                    flags.Raise(InvalidReason.Drift);
            }

            // Open a fresh window from now.
            _physicsAccumulated = 0d;
            _windowStartReal = Time.realtimeSinceStartup;
        }

        /// <summary>Reset accumulators (e.g. on a state transition mid-window).</summary>
        public void Reset()
        {
            _physicsAccumulated = 0d;
            _windowOpen = false;
            _windowStartReal = 0d;
        }
    }
}
