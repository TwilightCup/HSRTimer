using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// Holds the set of invalid reasons currently active on a run. There are
    /// three groups:
    ///   - unforgivable (permanent until game restart),
    ///   - forgivable (clearable on retry),
    ///   - soft (counted, shown in normal HUD text, only flashing red on the
    ///     triggering frame; cleared on one-key retry, but not by pause-menu
    ///     restart; removed by a full timer reset).
    /// Reasons only accumulate; they never auto-clear except via
    /// <see cref="ClearForgivable"/> / <see cref="ClearAll"/>.
    /// </summary>
    public sealed class ValidityFlags
    {
        private sealed class SoftFlagState
        {
            public int Count;
            public float LastTriggerTime;
        }

        private readonly HashSet<InvalidReason> _unforgivable = new HashSet<InvalidReason>();
        private readonly HashSet<InvalidReason> _forgivable = new HashSet<InvalidReason>();
        private readonly Dictionary<InvalidReason, SoftFlagState> _soft = new Dictionary<InvalidReason, SoftFlagState>();

        /// <summary>
        /// True when a hard invalid flag is active (unforgivable or forgivable).
        /// Soft flags do NOT make the run invalid for subsegment/marker PB
        /// recording; they are informational and shown separately on the HUD.
        /// </summary>
        public bool IsInvalid => _unforgivable.Count + _forgivable.Count > 0;

        /// <summary>Alias for the HUD banner condition: hard flags only, soft flags excluded.</summary>
        public bool HasHardInvalid => IsInvalid;

        public bool HasUnforgivable => _unforgivable.Count > 0;

        public bool HasSoft => _soft.Count > 0;

        /// <summary>All currently-active hard (banner) reasons, unforgivable first.</summary>
        public IEnumerable<InvalidReason> All
        {
            get
            {
                foreach (var r in _unforgivable) yield return r;
                foreach (var r in _forgivable) yield return r;
            }
        }

        /// <summary>All currently-active soft flags with their trigger counts and last-trigger timestamps.</summary>
        public IEnumerable<SoftFlagInfo> SoftFlags
        {
            get
            {
                foreach (var kv in _soft)
                    yield return new SoftFlagInfo(kv.Key, kv.Value.Count, kv.Value.LastTriggerTime);
            }
        }

        /// <summary>
        /// Record a reason. Hard reasons are idempotent; soft reasons instead
        /// increment their trigger count and stamp the flash time so the HUD can
        /// flash the line once per new trigger.
        /// </summary>
        public void Raise(InvalidReason reason)
        {
            if (InvalidReasons.SeverityOf(reason) == Severity.Soft)
            {
                SoftFlagState state;
                if (!_soft.TryGetValue(reason, out state))
                {
                    state = new SoftFlagState();
                    _soft[reason] = state;
                }
                state.Count++;
                state.LastTriggerTime = Time.realtimeSinceStartup;
                return;
            }

            if (InvalidReasons.SeverityOf(reason) == Severity.Unforgivable)
                _unforgivable.Add(reason);
            else
                _forgivable.Add(reason);
        }

        /// <summary>R5.4.2: clear only forgivable flags (pause-menu restart / one-key retry). Soft flags are kept for pause-menu restart, but one-key retry clears them explicitly.</summary>
        public void ClearForgivable() => _forgivable.Clear();

        /// <summary>Clear only soft flags (one-key retry). Hard flags and unforgivable flags are kept.</summary>
        public void ClearSoftFlags() => _soft.Clear();

        /// <summary>Clear everything (full-run reset / game restart), including soft flags.</summary>
        public void ClearAll()
        {
            _unforgivable.Clear();
            _forgivable.Clear();
            _soft.Clear();
        }

        /// <summary>Comma-joined localized hard-reason names for the HUD banner (soft flags are rendered separately).</summary>
        public string FormatReasons(LocalizationService loc)
        {
            var sb = new StringBuilder();
            foreach (var r in All)
            {
                if (sb.Length > 0)
                    sb.Append(", ");
                sb.Append(loc != null ? loc.Get(InvalidReasons.LocalKey(r)) : InvalidReasons.LocalKey(r));
            }
            return sb.ToString();
        }

        /// <summary>Immutable snapshot of one soft flag for HUD rendering.</summary>
        public readonly struct SoftFlagInfo
        {
            public readonly InvalidReason Reason;
            public readonly int Count;
            public readonly float LastTriggerTime;

            public SoftFlagInfo(InvalidReason reason, int count, float lastTriggerTime)
            {
                Reason = reason;
                Count = count;
                LastTriggerTime = lastTriggerTime;
            }
        }
    }
}
