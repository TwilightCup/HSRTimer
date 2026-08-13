using System.Collections.Generic;
using System.Text;

namespace HSRTimer
{
    /// <summary>
    /// Holds the set of invalid reasons currently active on a run, split into
    /// unforgivable (permanent until game restart) and forgivable (clearable on
    /// retry). Raising an unforgivable reason while already-forgivable flags
    /// exist keeps everything; reasons only accumulate, they never auto-clear
    /// except via <see cref="ClearForgivable"/> / <see cref="ClearAll"/>.
    /// </summary>
    public sealed class ValidityFlags
    {
        private readonly HashSet<InvalidReason> _unforgivable = new HashSet<InvalidReason>();
        private readonly HashSet<InvalidReason> _forgivable = new HashSet<InvalidReason>();

        public bool IsInvalid => _unforgivable.Count + _forgivable.Count > 0;

        public bool HasUnforgivable => _unforgivable.Count > 0;

        /// <summary>All currently-active reasons, unforgivable first.</summary>
        public IEnumerable<InvalidReason> All
        {
            get
            {
                foreach (var r in _unforgivable) yield return r;
                foreach (var r in _forgivable) yield return r;
            }
        }

        /// <summary>Record a reason. Idempotent; ignores severity duplicates.</summary>
        public void Raise(InvalidReason reason)
        {
            if (InvalidReasons.SeverityOf(reason) == Severity.Unforgivable)
                _unforgivable.Add(reason);
            else
                _forgivable.Add(reason);
        }

        /// <summary>R5.4.2: clear only forgivable flags (manual retry).</summary>
        public void ClearForgivable() => _forgivable.Clear();

        /// <summary>Clear everything (full-run reset / game restart).</summary>
        public void ClearAll()
        {
            _unforgivable.Clear();
            _forgivable.Clear();
        }

        /// <summary>Comma-joined localized reason names for the HUD banner.</summary>
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
    }
}
