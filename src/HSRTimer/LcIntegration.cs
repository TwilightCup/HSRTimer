using System;
using System.Reflection;

namespace HSRTimer
{
    /// <summary>
    /// Optional integration with the "Level Collections" (LevelCollections)
    /// plugin, accessed purely by reflection so HSRTimer loads and runs fine
    /// when LC is absent (declared as a BepInEx soft dependency). Exposes the
    /// current collection name (for the {collection} template var), detects
    /// when a collection's final level completes (so the HUD can surface the
    /// run total), and lets the one-key retry delegate to LC's
    /// <c>lc restart</c> command while a collection run is active. Every call
    /// is wrapped to no-op on any reflection failure.
    /// </summary>
    public sealed class LcIntegration
    {
        public static LcIntegration Instance { get; private set; }

        private readonly bool _enabled;
        private readonly Type _managerType;
        private readonly PropertyInfo _instanceProp;
        private readonly PropertyInfo _isInRunProp;
        private readonly PropertyInfo _isLastLevelProp;
        private readonly PropertyInfo _isDelayedCommandPendingProp;
        private readonly PropertyInfo _currentCollectionProp;
        private readonly PropertyInfo _collectionNameProp;

        private LcIntegration()
        {
            try
            {
                _managerType = Type.GetType("LevelCollections.CollectionManager, LevelCollections");
                if (_managerType == null)
                {
                    _enabled = false;
                    Plugin.Logger.LogInfo("HSRTimer: LevelCollections not present; integration disabled.");
                    return;
                }
                _instanceProp = _managerType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
                _isInRunProp = _managerType.GetProperty("IsInCollectionRun", BindingFlags.Public | BindingFlags.Instance);
                _isLastLevelProp = _managerType.GetProperty("IsLastLevel", BindingFlags.Public | BindingFlags.Instance);
                _isDelayedCommandPendingProp = _managerType.GetProperty("IsDelayedCommandPending", BindingFlags.Public | BindingFlags.Instance);
                _currentCollectionProp = _managerType.GetProperty("CurrentCollection", BindingFlags.Public | BindingFlags.Instance);
                var colType = _currentCollectionProp != null ? _currentCollectionProp.PropertyType : null;
                _collectionNameProp = colType != null ? colType.GetProperty("Name", BindingFlags.Public | BindingFlags.Instance) : null;
                _enabled = _instanceProp != null && _isInRunProp != null;
                if (_enabled)
                    Plugin.Logger.LogInfo("HSRTimer: LevelCollections integration enabled.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: LC integration init failed: {ex.Message}");
                _enabled = false;
            }
        }

        public static void Init() => Instance = new LcIntegration();

        public bool Enabled => _enabled;

        /// <summary>
        /// True when the player is in the middle of an LC collection run
        /// (a config collection or a transient <c>lc random</c> run). When true,
        /// the one-key retry should delegate to LC's <c>lc restart</c> command so
        /// the whole collection restarts from level 1 instead of just reloading
        /// the current level.
        /// </summary>
        public bool IsInCollectionRun
        {
            get
            {
                if (!_enabled) return false;
                try
                {
                    var mgr = _instanceProp != null ? _instanceProp.GetValue(null, null) : null;
                    return mgr != null
                        && _isInRunProp != null
                        && Convert.ToBoolean(_isInRunProp.GetValue(mgr, null));
                }
                catch { return false; }
            }
        }

        /// <summary>
        /// True while LC has a delayed console command
        /// (<c>lc restart/skip/random &lt;seconds&gt;</c>) counting down. LC refuses
        /// a new <c>lc restart</c> in that window, so HSRTimer should also refuse
        /// (rather than zero the timer and then stall with no reload).
        /// </summary>
        public bool IsDelayedCommandPending
        {
            get
            {
                if (!_enabled || _isDelayedCommandPendingProp == null) return false;
                try
                {
                    var mgr = _instanceProp != null ? _instanceProp.GetValue(null, null) : null;
                    return mgr != null && Convert.ToBoolean(_isDelayedCommandPendingProp.GetValue(mgr, null));
                }
                catch { return false; }
            }
        }

        /// <summary>
        /// Restart the current collection from its first level by dispatching
        /// LC's own <c>lc restart</c> command through the game's dev-console
        /// registry (<c>Shell.RawInvoke</c>). This is the public entry LC
        /// registers on load, so it works for both config collections and
        /// transient (<c>lc random</c>) runs, and reuses LC's scene-reload
        /// forcing, validation, and level launching. Returns true if dispatched.
        /// No-op (returns false) when LC is absent, not in a run, or a delayed
        /// command is pending.
        /// </summary>
        public bool RestartCollection()
        {
            if (!_enabled || !IsInCollectionRun || IsDelayedCommandPending)
                return false;

            try
            {
                // Shell.RawInvoke runs the command through the same registry
                // Shell.Update uses, so it behaves exactly like typing
                // "lc restart" into the console. It is a public static on the
                // game's Shell type; resolve it by reflection to stay decoupled
                // from the game assembly at compile time.
                var shellType = Type.GetType("Shell, Assembly-CSharp");
                var raw = shellType?.GetMethod("RawInvoke", BindingFlags.Public | BindingFlags.Static);
                if (shellType == null || raw == null)
                {
                    Plugin.Logger.LogWarning("HSRTimer: Shell.RawInvoke not found; cannot delegate retry to LC.");
                    return false;
                }
                raw.Invoke(null, new object[] { "lc restart" });
                return true;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: LC collection restart failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>True when the player is in an LC collection run and on its last level.</summary>
        public bool IsLastLevelOfCollection
        {
            get
            {
                if (!_enabled) return false;
                try
                {
                    var mgr = _instanceProp != null ? _instanceProp.GetValue(null, null) : null;
                    if (mgr == null) return false;
                    if (!Convert.ToBoolean(_isInRunProp.GetValue(mgr, null))) return false;
                    return _isLastLevelProp != null && Convert.ToBoolean(_isLastLevelProp.GetValue(mgr, null));
                }
                catch { return false; }
            }
        }

        /// <summary>The active collection's display name, or null if not in a run.</summary>
        public string CollectionName
        {
            get
            {
                if (!_enabled) return null;
                try
                {
                    var mgr = _instanceProp != null ? _instanceProp.GetValue(null, null) : null;
                    if (mgr == null) return null;
                    if (_isInRunProp != null && !Convert.ToBoolean(_isInRunProp.GetValue(mgr, null))) return null;
                    if (_currentCollectionProp == null || _collectionNameProp == null) return null;
                    var col = _currentCollectionProp.GetValue(mgr, null);
                    return col != null ? _collectionNameProp.GetValue(col, null) as string : null;
                }
                catch { return null; }
            }
        }
    }
}
