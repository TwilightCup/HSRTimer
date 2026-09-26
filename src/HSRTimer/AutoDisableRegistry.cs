using System;
using System.Collections.Generic;

namespace HSRTimer
{
    /// <summary>
    /// Tracks mechanisms other than the user's config toggle that can
    /// temporarily disable a module (e.g. the co-op client gate for
    /// subsegment). It keeps "user disabled in settings" separate from
    /// "auto-disabled by another mechanism" so the leaderboard mode-cycle can
    /// drop a mode only when it is truly unavailable, and status output can
    /// report which source disabled it.
    ///
    /// Sources are registered once (usually in <c>Awake</c>); each source is a
    /// named predicate evaluated lazily, so session-only test overrides (e.g.
    /// 'hsr sub clientmode on') keep working through the same predicate. A
    /// module is auto-disabled when at least one registered source currently
    /// returns true.
    /// </summary>
    public sealed class AutoDisableRegistry
    {
        private readonly Dictionary<string, Func<bool>> _sources =
            new Dictionary<string, Func<bool>>(StringComparer.Ordinal);

        /// <summary>
        /// Register (or replace) an auto-disable source. <paramref name="reason"/>
        /// is a stable short id used in status output; <paramref name="disabled"/>
        /// returns true while the source wants the module off.
        /// </summary>
        public void Register(string reason, Func<bool> disabled)
        {
            if (string.IsNullOrEmpty(reason))
                return;
            _sources[reason] = disabled ?? (() => false);
        }

        /// <summary>Remove a previously registered source.</summary>
        public void Unregister(string reason)
        {
            if (!string.IsNullOrEmpty(reason))
                _sources.Remove(reason);
        }

        /// <summary>True when at least one registered source currently disables the module.</summary>
        public bool IsDisabled
        {
            get
            {
                foreach (var src in _sources.Values)
                    if (src())
                        return true;
                return false;
            }
        }

        /// <summary>
        /// Snapshot of the registered source reasons that currently disable
        /// the module, in registration order. Empty when nothing is active.
        /// </summary>
        public List<string> ActiveReasons()
        {
            var active = new List<string>(_sources.Count);
            foreach (var kv in _sources)
                if (kv.Value())
                    active.Add(kv.Key);
            return active;
        }

        /// <summary>Snapshot of all registered source reasons (for status output).</summary>
        public List<string> RegisteredReasons()
        {
            return new List<string>(_sources.Keys);
        }
    }
}
