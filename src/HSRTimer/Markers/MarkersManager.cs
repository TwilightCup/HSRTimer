using System;
using System.Collections.Generic;
using HumanAPI;
using Multiplayer;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>One marker trigger record shown in the markers leaderboard feed (R10.7).</summary>
    public sealed class MarkerFeedRow
    {
        public string MarkerId;
        public string Name;
        public long TMs;
    }

    /// <summary>
    /// Runtime engine for the R10 Markers module: loads the current level's
    /// marker set, evaluates triggers each physics frame (plus the pause-menu
    /// checkpoint-load event), maintains the newest-first trigger feed, and
    /// writes the whole-level PB record at level end. All evaluation reads
    /// public game fields (poll, don't patch); the only event source outside the
    /// polling loop is the existing <c>PauseMenu.LoadClick</c> postfix, because
    /// FixedUpdate is halted while paused (R10.2.4).
    /// </summary>
    public sealed class MarkersManager : MonoBehaviour
    {
        public static MarkersManager Instance { get; private set; }

        // ── level context (current level being played) ──
        private string _currentLevelKey;
        private string _currentCategory;
        private MarkerSet _currentSet;
        private string _currentTitle;

        // ── session cache of editable sets, keyed by "levelKey|categoryKey" ──
        // The settings panel and the runtime engine must edit the SAME MarkerSet
        // instance for a given (level, category); otherwise markers created in
        // the panel never reach the trigger evaluation / overlay / PB writer and
        // the level-end PB write overwrites the file with an empty runtime set.
        private readonly Dictionary<string, MarkerSet> _setCache = new Dictionary<string, MarkerSet>();

        // ── evaluation state: cleared whenever a new level/attempt starts ──
        private readonly Dictionary<string, long> _records = new Dictionary<string, long>();

        // ── display feed: survives the level-end transition (R10.7.6) ──
        private readonly List<MarkerFeedRow> _feed = new List<MarkerFeedRow>();
        private string _feedTitle;
        private string _feedLevelKey;
        private MarkerSet _feedSet;

        // ── dirty marker sets awaiting flush (R10.5.8) ──
        private readonly HashSet<MarkerSet> _dirtySets = new HashSet<MarkerSet>();

        // ── object resolution cache (per level) ──
        private bool _objectIndexReady;
        private List<GameObject> _sceneRoots = new List<GameObject>();
        private readonly Dictionary<uint, GameObject> _sceneIdMap = new Dictionary<uint, GameObject>();

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            SaveAllDirty();
            if (Instance == this) Instance = null;
        }

        private SettingsModel Settings => ConfigService.Instance != null ? ConfigService.Instance.Settings : null;

        private bool Enabled => Settings != null && Settings.MarkersEnable;

        private void Log(string message)
        {
            if (Settings != null && Settings.MarkersDebugLogging)
                Plugin.Logger.LogInfo("HSRTimer[markers]: " + message);
        }

        // ── lifecycle: level start ─────────────────────────────────────────

        public void OnLevelStart(Game game, RunState state)
        {
            ClearEvaluation();
            _currentSet = null;
            _currentLevelKey = null;
            _currentCategory = MarkerStore.CategoryKey();
            if (game == null) return;
            _currentLevelKey = LevelIdentity.CurrentLevelKey(game, folderFallbackForLocalWorkshop: true);
            _currentTitle = CurrentLevelDisplayName(game);
            if (!Enabled)
                return;
            _currentSet = GetOrCreateSet(_currentLevelKey, game.currentLevelType.ToString(), game.currentLevelNumber, _currentCategory);
            Log($"level start: key='{_currentLevelKey}' category='{_currentCategory}' markers={CountEnabled(_currentSet)}");
            // One concise info line whenever the level actually has markers, so a
            // user can confirm the runtime sees their marker set (and that the
            // panel/runtime are sharing one instance).
            int markerCount = CountEnabled(_currentSet);
            if (markerCount > 0)
            {
                Plugin.Logger.LogInfo(
                    $"HSRTimer[markers]: level '{_currentLevelKey}' [{_currentCategory}] loaded with {markerCount} marker(s).");
            }

            // R10.7.6: a new level with no markers clears the previous feed
            // immediately; otherwise the old feed stays until the first trigger.
            if (CountEnabled(_currentSet) == 0)
            {
                _feed.Clear();
                _feedTitle = null;
                _feedLevelKey = null;
                _feedSet = null;
            }
        }

        /// <summary>
        /// Per-frame trigger evaluation (R10.2). Called from
        /// <c>TimerCore.FixedUpdate</c> right after the subsegment tick.
        /// </summary>
        public void OnPhysicsTick(Game game, GameState gState, RunState state)
        {
            if (!Enabled || state == null || _currentSet == null || _currentSet.markers == null)
                return;
            if (!state.InSegment || state.Retrying || gState != GameState.PlayingLevel)
                return;
            if (CountEnabled(_currentSet) == 0)
                return;

            var human = Human.Localplayer;
            Vector3? pos = human != null && human.transform != null
                ? (Vector3?)human.transform.position
                : null;
            bool humanUsable = human != null && human.transform != null
                && human.state != HumanState.Spawning
                && human.state != HumanState.Unconscious
                && human.state != HumanState.Dead;
            var grab = human != null ? human.GetComponent<GrabManager>() : null;
            bool anyGrabbed = grab != null && grab.grabbedObjects != null && grab.grabbedObjects.Count > 0;
            bool jumping = human != null && human.jump;
            int cp = game != null ? game.currentCheckpointNumber : -1;
            long nowMs = SegmentTimeMs(state);

            foreach (var def in _currentSet.markers)
            {
                if (def == null || !def.enabled)
                    continue;
                if (_records.ContainsKey(def.id))
                    continue;
                if (!MarkerKindUtil.IsKnown(def.type))
                    continue;
                switch (def.Kind)
                {
                    case MarkerKind.Range:
                        if (pos.HasValue && humanUsable
                            && IsInsideBox(pos.Value, def)
                            && (!def.requireGrab || anyGrabbed)
                            && (!def.requireJump || jumping))
                        {
                            Record(def, nowMs);
                        }
                        break;
                    case MarkerKind.GrabObject:
                        if (humanUsable && IsObjectGrabbed(def, grab))
                            Record(def, nowMs);
                        break;
                    case MarkerKind.Checkpoint:
                        // R10.2.3: touch/reach uses >= (non-linear checkpoint levels).
                        if (!def.triggerOnLoad && cp >= def.checkpointIndex)
                            Record(def, nowMs);
                        break;
                }
            }
        }

        /// <summary>
        /// Pause-menu checkpoint load (R10.2.4). Called from the existing
        /// <c>PauseMenu.LoadClick</c> postfix while the game is paused.
        /// </summary>
        public void OnCheckpointLoaded(int checkpointNumber)
        {
            if (!Enabled || _currentSet == null || _currentSet.markers == null)
                return;
            var state = TimerCore.State;
            if (state == null)
                return;
            long nowMs = SegmentTimeMs(state);
            foreach (var def in _currentSet.markers)
            {
                if (def == null || !def.enabled)
                    continue;
                if (def.Kind != MarkerKind.Checkpoint || !def.triggerOnLoad)
                    continue;
                if (checkpointNumber == def.checkpointIndex && !_records.ContainsKey(def.id))
                    Record(def, nowMs);
            }
        }

        /// <summary>Pause-menu level restart: the level restarts from checkpoint 0 (R10.1.6).</summary>
        public void OnRestartLevel()
        {
            ClearEvaluation();
            _feed.Clear();
            _feedTitle = null;
            _feedLevelKey = null;
            _feedSet = null;
        }

        /// <summary>
        /// Level end (R10.3): write the whole-level PB before the run's reset
        /// clears the segment state, then drop evaluation state (the display feed
        /// survives for the transition, R10.7.6). Called from
        /// <c>TimerCore.EndSegment</c> before <c>State.EndSegment</c>.
        /// </summary>
        public void OnLevelEnd(Game game, RunState state, double endTime, bool completed, bool retrying, GameState nowGameState, AppSate nowAppState)
        {
            // TimerCore.EndSegment runs tag OnLevelExit before this hook, so
            // final-validity checks (R4.2 checkpoint-final / voiceline) have
            // already raised any invalid flag; an invalid run never gets a PB.
            if (Enabled && completed && !retrying && state != null && !state.Flags.IsInvalid && _currentSet != null && state.InSegment)
            {
                long levelMs = (long)Math.Round((endTime - state.SegmentStart) * 1000.0);
                TryWritePb(levelMs);
            }
            ClearEvaluation();
        }

        /// <summary>Run exit / auto-reset / manual reset: drop all per-level state.</summary>
        public void OnRunExit() => ClearAllLevelState();

        public void OnRunReset() => ClearAllLevelState();

        /// <summary>One-key retry (R6): the level is reloaded from scratch (R10.1.6).</summary>
        public void OnRetryStart()
        {
            ClearEvaluation();
            _feed.Clear();
            _feedTitle = null;
            _feedLevelKey = null;
            _feedSet = null;
        }

        private void ClearEvaluation()
        {
            _records.Clear();
            _objectIndexReady = false;
            _sceneRoots.Clear();
            _sceneIdMap.Clear();
        }

        private void ClearAllLevelState()
        {
            ClearEvaluation();
            _currentSet = null;
            _currentLevelKey = null;
            _currentTitle = null;
            _feed.Clear();
            _feedTitle = null;
            _feedLevelKey = null;
            _feedSet = null;
        }

        // ── PB (R10.3) ─────────────────────────────────────────────────────

        private void TryWritePb(long levelMs)
        {
            var set = _currentSet;
            if (set == null)
                return;
            if (set.pb != null && set.pb.total_ms != 0 && levelMs >= set.pb.total_ms)
            {
                Log($"PB not beaten for '{set.level_id}': {levelMs}ms >= {set.pb.total_ms}ms");
                return;
            }
            set.pb = new MarkerPb
            {
                total_ms = levelMs,
                created_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            };
            set.pbTimes = new List<MarkerPbEntry>();
            foreach (var kv in _records)
                set.pbTimes.Add(new MarkerPbEntry { id = kv.Key, t_ms = kv.Value });
            set.pbTimes.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            MarkerStore.Save(set); // synchronous: must land before the run reset (R10.3.3)
            Log($"PB written for '{set.level_id}' at {levelMs}ms ({set.pbTimes.Count} marker(s))");
        }

        // ── trigger helpers ────────────────────────────────────────────────

        private static long SegmentTimeMs(RunState state)
            => (long)Math.Round((state.GameTime - state.SegmentStart) * 1000.0);

        private static bool IsInsideBox(Vector3 pos, MarkerDef def)
        {
            if (def.sx <= 0f || def.sy <= 0f || def.sz <= 0f)
                return false; // invalid size: never triggers (R10.2.1)
            return Mathf.Abs(pos.x - def.cx) <= def.sx * 0.5f
                && Mathf.Abs(pos.y - def.cy) <= def.sy * 0.5f
                && Mathf.Abs(pos.z - def.cz) <= def.sz * 0.5f;
        }

        private bool IsObjectGrabbed(MarkerDef def, GrabManager grab)
        {
            if (grab == null || grab.grabbedObjects == null || grab.grabbedObjects.Count == 0)
                return false;
            var target = ResolveObject(def);
            if (target == null || target.transform == null)
                return false;
            foreach (var g in grab.grabbedObjects)
            {
                if (g == null || g.transform == null)
                    continue;
                if (g == target)
                    return true;
                // GrabManager.IsGrabbed semantics: parent/child containment either way.
                if (g.transform.IsChildOf(target.transform) || target.transform.IsChildOf(g.transform))
                    return true;
            }
            return false;
        }

        private void Record(MarkerDef def, long tMs)
        {
            // First trigger of the current level switches the feed to this
            // level (R10.7.6); later triggers just append.
            if (_feedLevelKey != _currentLevelKey)
            {
                _feed.Clear();
                _feedTitle = _currentTitle;
                _feedLevelKey = _currentLevelKey;
                _feedSet = _currentSet;
            }
            _records[def.id] = tMs;
            _feed.Add(new MarkerFeedRow { MarkerId = def.id, Name = def.name ?? "", TMs = tMs });
            Log($"marker '{def.name}' ({def.id}) triggered at {tMs}ms");
        }

        private static int CountEnabled(MarkerSet set)
        {
            if (set == null || set.markers == null)
                return 0;
            int n = 0;
            foreach (var m in set.markers)
                if (m != null && m.enabled && MarkerKindUtil.IsKnown(m.type))
                    n++;
            return n;
        }

        // ── dirty flush (R10.5.8) ──────────────────────────────────────────

        public void MarkDirty(MarkerSet set)
        {
            if (set != null)
                _dirtySets.Add(set);
        }

        public void SaveAllDirty()
        {
            if (_dirtySets.Count == 0)
                return;
            var list = new List<MarkerSet>(_dirtySets);
            _dirtySets.Clear();
            foreach (var set in list)
                MarkerStore.Save(set);
        }

        public void OnUpdate()
        {
            SaveAllDirty();
        }

        // ── leaderboard feed (R10.7) ───────────────────────────────────────

        /// <summary>Whether the markers feed should be drawn (R10.7.2).</summary>
        public bool HasFeedData => Enabled && _feed.Count > 0;

        /// <summary>Feed in trigger order (oldest first); the HUD renders it reversed.</summary>
        public IReadOnlyList<MarkerFeedRow> Feed => _feed;

        /// <summary>Title row for markers mode: the level the feed belongs to.</summary>
        public string LeaderboardTitle => _feedTitle;

        /// <summary>PB time for a feed row's marker, from the set the feed belongs to.</summary>
        public long? PbTimeOf(string markerId)
            => _feedSet != null ? _feedSet.PbTimeOf(markerId) : null;

        // ── overlay (R10.6) ────────────────────────────────────────────────

        /// <summary>The current level's marker set, or null when not in a level / disabled.</summary>
        public MarkerSet CurrentSet => Enabled ? _currentSet : null;

        /// <summary>The current level's storage key (used by the overlay to reset per-level warnings).</summary>
        public string CurrentLevelKey => _currentLevelKey;

        // ── settings-panel API ─────────────────────────────────────────────

        /// <summary>
        /// Load (or create) the editable marker set for a level/category and
        /// return the SHARED session instance: the settings panel and the
        /// runtime engine both edit this same object, so panel edits are
        /// immediately visible to trigger evaluation, the overlay, and the PB
        /// writer (and vice versa). Disk is only read on the first request for
        /// a (level, category) within a session.
        /// </summary>
        public MarkerSet GetOrCreateSet(string levelKey, string levelSource, int levelNumber, string categoryKey)
        {
            if (string.IsNullOrEmpty(levelKey))
                return null;
            string key = CacheKey(levelKey, categoryKey);
            MarkerSet set;
            if (_setCache.TryGetValue(key, out set))
                return set;
            if (MarkerStore.TryLoad(levelKey, categoryKey, out set))
            {
                _setCache[key] = set;
                return set;
            }
            set = MarkerStore.CreateEmpty(levelKey, levelSource, levelNumber, categoryKey);
            _setCache[key] = set;
            return set;
        }

        private static string CacheKey(string levelKey, string categoryKey)
            => levelKey + "|" + (categoryKey ?? "Any");

        public static string NextMarkerId(MarkerSet set)
        {
            int max = 0;
            if (set != null && set.markers != null)
            {
                foreach (var m in set.markers)
                {
                    if (m == null || string.IsNullOrEmpty(m.id) || !m.id.StartsWith("m", StringComparison.Ordinal))
                        continue;
                    int n;
                    if (int.TryParse(m.id.Substring(1), out n) && n > max)
                        max = n;
                }
            }
            return "m" + (max + 1);
        }

        public bool TryCapturePlayerPosition(out Vector3 pos, out string errorKey)
        {
            pos = Vector3.zero;
            errorKey = null;
            var human = Human.Localplayer;
            if (human == null || human.transform == null)
            {
                errorKey = "MARKER_NEED_LEVEL";
                return false;
            }
            pos = human.transform.position;
            return true;
        }

        /// <summary>
        /// Capture the single object the local player is currently grabbing into
        /// <paramref name="def"/> (R10.5.4). Fails with a localized error key when
        /// not in a level, nothing is grabbed, or more than one object is grabbed.
        /// </summary>
        public bool TryCaptureGrabbedObject(MarkerDef def, out string errorKey)
        {
            errorKey = null;
            var human = Human.Localplayer;
            if (human == null || human.transform == null)
            {
                errorKey = "MARKER_NEED_LEVEL";
                return false;
            }
            var grab = human.GetComponent<GrabManager>();
            if (grab == null || grab.grabbedObjects == null || grab.grabbedObjects.Count == 0)
            {
                errorKey = "MARKER_GRAB_NONE";
                return false;
            }
            if (grab.grabbedObjects.Count > 1)
            {
                errorKey = "MARKER_GRAB_MULTIPLE";
                return false;
            }
            var go = grab.grabbedObjects[0];
            if (go == null)
            {
                errorKey = "MARKER_GRAB_NONE";
                return false;
            }
            def.objectSceneId = SceneIdOf(go);
            def.objectPath = BuildHierarchyPath(go.transform);
            def.objectName = go.name ?? "";
            var p = go.transform != null ? go.transform.position : Vector3.zero;
            def.ox = p.x;
            def.oy = p.y;
            def.oz = p.z;
            return true;
        }

        // ── object resolution (R10.6.4) ────────────────────────────────────

        /// <summary>
        /// Resolve a captured object reference: sceneId scan, then hierarchy
        /// path, then name + position fallback (last resort, logged). Returns
        /// null when unresolvable.
        /// </summary>
        public GameObject ResolveObject(MarkerDef def)
        {
            if (def == null)
                return null;
            if (!_objectIndexReady)
                BuildObjectIndex();
            if (def.objectSceneId != 0u && _sceneIdMap.TryGetValue(def.objectSceneId, out var byId) && byId != null)
                return byId;
            if (!string.IsNullOrEmpty(def.objectPath))
            {
                var byPath = ResolveByPath(def.objectPath);
                if (byPath != null)
                    return byPath;
            }
            if (!string.IsNullOrEmpty(def.objectName))
            {
                var byName = FindByNameAndPosition(def.objectName, def.ox, def.oy, def.oz);
                if (byName != null)
                {
                    Log($"object '{def.objectName}' resolved by name/position fallback (sceneId/path mismatch).");
                    return byName;
                }
            }
            return null;
        }

        private void BuildObjectIndex()
        {
            _sceneRoots = new List<GameObject>();
            _sceneIdMap.Clear();
            try
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                if (scene.IsValid())
                {
                    var roots = scene.GetRootGameObjects();
                    if (roots != null)
                    {
                        _sceneRoots.AddRange(roots);
                        foreach (var root in roots)
                        {
                            if (root == null) continue;
                            var identities = root.GetComponentsInChildren<NetIdentity>(true);
                            foreach (var ni in identities)
                            {
                                if (ni == null || ni.sceneId == 0u || _sceneIdMap.ContainsKey(ni.sceneId))
                                    continue;
                                _sceneIdMap[ni.sceneId] = ni.gameObject;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer[markers]: object index build failed: {ex.Message}");
            }
            _objectIndexReady = true;
        }

        private GameObject ResolveByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;
            var parts = path.Split('/');
            if (parts.Length == 0)
                return null;
            foreach (var root in _sceneRoots)
            {
                if (root == null || root.transform == null)
                    continue;
                var t = MatchInChildren(root.transform, parts, 0);
                if (t != null)
                    return t.gameObject;
            }
            return null;
        }

        private static Transform MatchInChildren(Transform node, string[] parts, int idx)
        {
            if (node == null || idx >= parts.Length)
                return null;
            for (int c = 0; c < node.childCount; c++)
            {
                var child = node.GetChild(c);
                if (child == null || child.name != parts[idx])
                    continue;
                if (idx == parts.Length - 1)
                    return child;
                var r = MatchInChildren(child, parts, idx + 1);
                if (r != null)
                    return r;
            }
            return null;
        }

        private GameObject FindByNameAndPosition(string name, float x, float y, float z)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            var wanted = new Vector3(x, y, z);
            foreach (var root in _sceneRoots)
            {
                if (root == null) continue;
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || t.name != name)
                        continue;
                    if (Vector3.Distance(t.position, wanted) <= 5f)
                        return t.gameObject;
                }
            }
            return null;
        }

        private static uint SceneIdOf(GameObject go)
        {
            var cur = go != null ? go.transform : null;
            while (cur != null)
            {
                var ni = cur.GetComponent<NetIdentity>();
                if (ni != null && ni.sceneId != 0u)
                    return ni.sceneId;
                cur = cur.parent;
            }
            return 0u;
        }

        private static string BuildHierarchyPath(Transform t)
        {
            if (t == null)
                return "";
            var names = new List<string>();
            var cur = t;
            while (cur != null)
            {
                if (cur.parent != null)
                    names.Add(cur.name);
                cur = cur.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        private string CurrentLevelDisplayName(Game game)
        {
            if (game.currentLevelType == WorkshopItemSource.BuiltIn
                || game.currentLevelType == WorkshopItemSource.EditorPick)
            {
                string internalName = LevelIdentity.OfficialInternalName(game, game.currentLevelNumber);
                if (!string.IsNullOrEmpty(internalName))
                    return LevelIdentity.EnglishLevelName("LEVEL/" + internalName, internalName);
            }
            if (game.workshopLevel != null && !string.IsNullOrEmpty(game.workshopLevel.title))
                return game.workshopLevel.title;
            return LevelIdentity.CurrentLevelKey(game, folderFallbackForLocalWorkshop: true);
        }
    }
}
