using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Multiplayer;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// Runtime options for the R8 Subsegment module. Read from
    /// <c>ConfigService.Settings</c> each frame, so settings panel changes apply
    /// live without reloading the plugin.
    /// </summary>
    public struct SubsegmentOptions
    {
        public bool Enable;
        public string PBPath;
        public string LoadPath;
        public KeyCode ToggleKey;
        public string MultiProject;
        public float PlaneRadius;
        public float MinMove;
        public float SampleInterval;
        public float QuietSettleSeconds;
        public float PlaneDebounceSeconds;
        public float RespawnJumpMeters;
        public int MaxLeaderboardEntries;
        public bool DebugLogging;
        public int HudFontSize;
        public float HudOffsetX;
        public float HudOffsetY;

        public static SubsegmentOptions FromSettings(SettingsModel s)
        {
            return new SubsegmentOptions
            {
                Enable = s.SubsegmentEnable,
                PBPath = SubsegmentFileStore.ResolvePath(string.IsNullOrEmpty(s.SubsegmentPBPath) ? "subsegment/pb" : s.SubsegmentPBPath),
                LoadPath = SubsegmentFileStore.ResolvePath(string.IsNullOrEmpty(s.SubsegmentLoadPath) ? "subsegment/load" : s.SubsegmentLoadPath),
                ToggleKey = s.SubsegmentToggleKey,
                MultiProject = IsValidMultiProject(s.SubsegmentMultiProject) ? s.SubsegmentMultiProject : "Any%",
                PlaneRadius = s.SubsegmentPlaneRadius,
                MinMove = s.SubsegmentMinMove,
                SampleInterval = Mathf.Max(0.01f, s.SubsegmentSampleInterval),
                QuietSettleSeconds = s.SubsegmentQuietSettleSeconds,
                PlaneDebounceSeconds = s.SubsegmentPlaneDebounceSeconds,
                RespawnJumpMeters = s.SubsegmentRespawnJumpMeters,
                MaxLeaderboardEntries = Mathf.Max(1, s.SubsegmentMaxLeaderboardEntries),
                DebugLogging = s.SubsegmentDebugLogging,
                HudFontSize = Mathf.Max(8, s.SubsegmentHudFontSize),
                HudOffsetX = s.SubsegmentHudOffsetX,
                HudOffsetY = s.SubsegmentHudOffsetY,
            };
        }

        private static bool IsValidMultiProject(string value)
        {
            return value == "Aztec%" || value == "Dark%" || value == "Steam%" || value == "Any%";
        }
    }

    /// <summary>
    /// Local subsegment processor: Recorder + Loader + Comparator + HUD data.
    /// This is a plain polled MonoBehaviour owned by <see cref="TimerCore"/>;
    /// TimerCore calls the lifecycle/tick hooks at the same points it processes
    /// timing, so subsegment sampling shares the authoritative game-time clock.
    /// </summary>
    public sealed class SubsegmentManager : MonoBehaviour
    {
        public static SubsegmentManager Instance { get; private set; }

        private SubsegmentOptions _options;

        // Recorder state
        private readonly List<SubsegmentSample> _currentSamples = new List<SubsegmentSample>();
        private Vector3? _lastSamplePosition;
        private double _lastSampleGameTime = -1d;
        private int _nextSeq;
        private bool _firstAwake;

        // Loader/comparator state
        private readonly List<SubsegmentReference> _references = new List<SubsegmentReference>();
        private bool _visible = true;

        // Multi-run (ML) tracking
        private bool _multiRunCandidate;
        private bool _multiRunActive;
        private readonly List<string> _multiRunLevelIds = new List<string>();
        private readonly Dictionary<string, List<SubsegmentSample>> _multiRunSamples = new Dictionary<string, List<SubsegmentSample>>();
        private int _lastCompletedLevelNumber = -1;
        private long _multiRunTotalMs;
        private bool _multiRunPbWritten;

        private void Awake()
        {
            Instance = this;
            _options = SubsegmentOptions.FromSettings(SettingsFromConfig());
        }

        private static SettingsModel SettingsFromConfig()
            => ConfigService.Instance != null ? ConfigService.Instance.Settings : new SettingsModel();

        /// <summary>Whether the leaderboard is currently shown (R8.5.1.2).</summary>
        public bool Visible => _visible;

        /// <summary>True during an official-campaign multi-run attempt (candidate or active).</summary>
        public bool InMultiRunActive => _multiRunCandidate || _multiRunActive;

        /// <summary>Current sorted leaderboard data (already truncated to MaxLeaderboardEntries).</summary>
        public List<SubsegmentReference> Entries
        {
            get
            {
                if (!_options.Enable || _references.Count == 0) return new List<SubsegmentReference>();
                var with = _references.Where(r => r.DiffMs.HasValue)
                    .OrderBy(r => r.DiffMs.Value)
                    .ThenBy(r => r.DisplayId, StringComparer.Ordinal);
                var without = _references.Where(r => !r.DiffMs.HasValue)
                    .OrderBy(r => r.DisplayId, StringComparer.Ordinal);
                return with.Concat(without).Take(_options.MaxLeaderboardEntries).ToList();
            }
        }

        public SubsegmentOptions Options => _options;

        /// <summary>Called by the engine when a new segment (level) starts.</summary>
        public void OnLevelStart(Game game, RunState state)
        {
            _options = SubsegmentOptions.FromSettings(SettingsFromConfig());
            if (!_options.Enable)
            {
                ClearRuntime();
                return;
            }

            // Multi-run display continuity: when advancing from one campaign
            // level to the next, keep the previous leaderboard values on screen
            // until the new level produces its first settled diff (R8.5.6 /
            // follow-up UX). Snapshot by display id before the new load wipes it.
            bool wasMultiRun = _multiRunCandidate || _multiRunActive;
            var carryDisplayDiffs = new Dictionary<string, long?>();
            if (wasMultiRun)
            {
                foreach (var r in _references)
                    carryDisplayDiffs[r.DisplayId] = r.DiffMs;
            }

            // Multi-run tracking (R8.3.2): a menu-entered B0 begins a candidate;
            // reaching B1 upgrades it to an active multi-run display. Any other
            // new non-retry level starts a standalone/IL segment. An LC collection
            // run is not HSRTimer's official-campaign multi-run, so it does not
            // enter ML tracking here.
            bool inCollectionRun = LcIntegration.Instance != null && LcIntegration.Instance.IsInCollectionRun;
            if (!state.Retrying && !inCollectionRun
                && game.currentLevelType == WorkshopItemSource.BuiltIn && game.currentLevelNumber == 0)
            {
                _multiRunCandidate = true;
                _multiRunActive = false;
                _multiRunLevelIds.Clear();
                _multiRunSamples.Clear();
                _lastCompletedLevelNumber = -1;
                _multiRunTotalMs = 0L;
                _multiRunPbWritten = false;
            }
            else if (_multiRunCandidate && game.currentLevelNumber == 1)
            {
                _multiRunActive = true;
            }
            else if (_multiRunCandidate && game.currentLevelNumber != 0 && !_multiRunActive)
            {
                // A page-advance to B1 should have set active above; this branch
                // is defensive for levels skipped from a B0-only run.
                _multiRunActive = true;
            }

            ClearRecorder();
            LoadReferences(game);

            if (wasMultiRun && !state.Retrying)
            {
                foreach (var reference in _references)
                {
                    if (carryDisplayDiffs.TryGetValue(reference.DisplayId, out long? carried))
                        reference.DiffMs = carried;
                }
            }

            if (_options.DebugLogging)
                Plugin.Logger.LogInfo($"HSRTimer: subsegment loaded {_references.Count} reference(s) for {GetLevelId(game)} (multi={_multiRunActive}).");
        }

        /// <summary>Called by the engine when the current segment ends, before the auto-reset clear.</summary>
        public void OnLevelEnd(Game game, RunState state, double endTime, bool completed, bool retrying, GameState nowGameState, AppSate nowAppState)
        {
            UpdateOptions();
            if (!_options.Enable)
            {
                ClearRuntime();
                return;
            }

            if (retrying)
            {
                ClearRecorder();
                return;
            }

            if (completed && _firstAwake)
                AddFinalSample(endTime);

            bool valid = !state.Flags.IsInvalid;
            string levelId = GetLevelId(game);

            if (completed)
            {
                if (valid)
                    WriteIlPb(levelId, state, (long)Math.Round(endTime * 1000.0));

                // Track ML run contents/endpoint.
                if (_multiRunCandidate)
                {
                    var copy = new List<SubsegmentSample>(_currentSamples);
                    if (copy.Count > 0)
                    {
                        if (!_multiRunSamples.ContainsKey(levelId))
                            _multiRunLevelIds.Add(levelId);
                        _multiRunSamples[levelId] = copy;
                    }
                    _lastCompletedLevelNumber = game.currentLevelNumber;
                    _multiRunTotalMs = (long)Math.Round(endTime * 1000.0);
                }

                // R8.3.2.2.3: Any% ends with Intro_Reprise (B12). The run is
                // complete here even though the game immediately loads Credits,
                // so clear the multi-run tracking after writing (no later exit
                // should re-write it).
                if (_multiRunCandidate && game.currentLevelType == WorkshopItemSource.BuiltIn && game.currentLevelNumber == 12)
                {
                    if (valid && !_multiRunPbWritten)
                        WriteMultiPb(state);
                    ClearMultiRun();
                }
            }

            // Leaving through Inactive ends the segment and, for a multi-run,
            // may be the moment the run's PB is finalized (after completing an
            // endpoint). Clear all level-local runtime so the HUD/state are
            // reset and the next segment loads fresh references.
            if (!retrying && nowGameState == GameState.Inactive)
            {
                if (_multiRunCandidate && valid && !_multiRunPbWritten && IsMultiEndLevel(_lastCompletedLevelNumber))
                    WriteMultiPb(state);
                if (_options.DebugLogging)
                    Plugin.Logger.LogInfo($"HSRTimer: subsegment level end (completed={completed}, valid={valid}, samples={_currentSamples.Count}).");
                ClearRuntime();
                return;
            }

            // During an official-campaign multi-run, a completed level advances
            // through LoadingLevel into the next level. Keep the current
            // leaderboard display intact across that transition; OnLevelStart
            // snapshots it and refreshes on the first new-level diff.
            if (!retrying && (_multiRunCandidate || _multiRunActive)
                && nowGameState == GameState.LoadingLevel)
            {
                if (_options.DebugLogging)
                    Plugin.Logger.LogInfo($"HSRTimer: subsegment level end (completed={completed}, valid={valid}, samples={_currentSamples.Count}); preserving multi-run leaderboard.");
                return;
            }

            if (_options.DebugLogging)
                Plugin.Logger.LogInfo($"HSRTimer: subsegment level end (completed={completed}, valid={valid}, samples={_currentSamples.Count}).");
            ClearRecorder();
        }

        /// <summary>Called before an auto-reset / menu-entry full reset, i.e. a run exits without using the manual reset key.</summary>
        public void OnRunExit()
        {
            UpdateOptions();
            if (!_options.Enable)
            {
                ClearRuntime();
                return;
            }
            var state = TimerCore.State;
            if (state != null && !state.Flags.IsInvalid && !_multiRunPbWritten && _multiRunCandidate && IsMultiEndLevel(_lastCompletedLevelNumber))
                WriteMultiPb(state);
            ClearRuntime();
        }

        /// <summary>Called from full-run reset/manual reset: discard without writing PB.</summary>
        public void OnRunReset()
        {
            ClearRuntime();
        }

        /// <summary>Called when a one-key retry starts (R8.1.5.1 / R8.5.6.1).</summary>
        public void OnRetryStart()
        {
            ClearRuntime();
        }

        /// <summary>Per-physics-frame hook: sample recording + crossing detection.</summary>
        public void OnPhysicsTick(Game game, GameState gState, RunState state)
        {
            UpdateOptions();
            if (!_options.Enable || !state.InSegment || gState != GameState.PlayingLevel)
                return;

            var pos = GetCurrentPosition();
            if (pos == null) return;

            EnsureAwakeSample(state.GameTime, pos.Value);
            if (!_firstAwake)
                return;

            if (state.GameTime - _lastSampleGameTime >= _options.SampleInterval)
                AddRegularSample(state.GameTime, pos.Value);

            RunCrossingDetection(pos.Value, state);
        }

        /// <summary>Per-render-frame hook: quiet-settle timers (runs even while paused, R8.4.3.4).</summary>
        public void OnUpdate()
        {
            UpdateOptions();
            if (!_options.Enable)
                return;

            float now = Time.unscaledTime;
            foreach (var reference in _references)
            {
                foreach (var plane in reference.Planes)
                {
                    if (!plane.HasQuiet) continue;
                    if (now - plane.QuietStartUnscaledTime < _options.QuietSettleSeconds) continue;
                    long? hit = plane.CandidateHitMs;
                    if (hit.HasValue)
                        reference.DiffMs = hit.Value - plane.TMs;
                    plane.HasQuiet = false;
                    plane.CandidateHitMs = null;
                    if (_options.DebugLogging)
                        Plugin.Logger.LogInfo($"HSRTimer: subsegment settled '{reference.DisplayId}' plane seq {plane.Seq} diff_ms={reference.DiffMs}");
                }
            }
        }

        /// <summary>Handle the configurable subsegment leaderboard toggle key.</summary>
        public void HandleKeybind(KeyCode key)
        {
            UpdateOptions();
            if (!_options.Enable || key != _options.ToggleKey) return;
            if (!Input.GetKeyDown(key)) return;
            _visible = !_visible;
            Plugin.Logger.LogInfo(_visible ? "HSRTimer: subsegment leaderboard shown." : "HSRTimer: subsegment leaderboard hidden.");
        }

        private void UpdateOptions()
            => _options = SubsegmentOptions.FromSettings(SettingsFromConfig());

        // ── Recorder ──────────────────────────────────────────────────────

        private void ClearRecorder()
        {
            _currentSamples.Clear();
            _lastSamplePosition = null;
            _lastSampleGameTime = -1d;
            _nextSeq = 0;
            _firstAwake = false;
            foreach (var reference in _references)
            {
                reference.DiffMs = null;
                foreach (var plane in reference.Planes)
                {
                    plane.HasPrevD = false;
                    plane.HasQuiet = false;
                    plane.CandidateHitMs = null;
                    plane.LastDebounceUnscaledTime = float.NegativeInfinity;
                }
            }
        }

        private void ClearRuntime()
        {
            _references.Clear();
            ClearRecorder();
            _multiRunCandidate = false;
            _multiRunActive = false;
            _multiRunLevelIds.Clear();
            _multiRunSamples.Clear();
            _lastCompletedLevelNumber = -1;
            _multiRunTotalMs = 0L;
            _multiRunPbWritten = false;
        }

        private void ClearMultiRun()
        {
            _multiRunCandidate = false;
            _multiRunActive = false;
            _multiRunLevelIds.Clear();
            _multiRunSamples.Clear();
            _lastCompletedLevelNumber = -1;
            _multiRunTotalMs = 0L;
            _multiRunPbWritten = false;
        }

        private void EnsureAwakeSample(double gameTime, Vector3 pos)
        {
            if (_firstAwake) return;
            var human = Human.Localplayer;
            if (human == null) return;
            if (human.state == HumanState.Spawning || human.state == HumanState.Unconscious || human.state == HumanState.Dead)
                return;

            _firstAwake = true;
            _lastSampleGameTime = gameTime;
            _lastSamplePosition = pos;
            _currentSamples.Add(new SubsegmentSample
            {
                seq = _nextSeq++,
                level_index = GetLevelIndex(),
                t_ms = (long)Math.Round(gameTime * 1000.0),
                px = pos.x,
                py = pos.y,
                pz = pos.z,
                dx = 0f,
                dy = 0f,
                dz = 0f,
                plane_radius = _options.PlaneRadius,
            });
        }

        private void AddRegularSample(double gameTime, Vector3 pos)
        {
            float dx = 0f, dy = 0f, dz = 0f;
            if (_lastSamplePosition.HasValue)
            {
                Vector3 d = pos - _lastSamplePosition.Value;
                if (d.magnitude >= _options.MinMove)
                {
                    dx = d.x;
                    dy = d.y;
                    dz = d.z;
                }
            }
            _currentSamples.Add(new SubsegmentSample
            {
                seq = _nextSeq++,
                level_index = GetLevelIndex(),
                t_ms = (long)Math.Round(gameTime * 1000.0),
                px = pos.x,
                py = pos.y,
                pz = pos.z,
                dx = dx,
                dy = dy,
                dz = dz,
                plane_radius = _options.PlaneRadius,
            });
            _lastSamplePosition = pos;
            _lastSampleGameTime = gameTime;
        }

        private void AddFinalSample(double endTime)
        {
            var pos = GetCurrentPosition();
            if (pos == null) return;
            float dx = 0f, dy = 0f, dz = 0f;
            if (_lastSamplePosition.HasValue)
            {
                Vector3 d = pos.Value - _lastSamplePosition.Value;
                if (d.magnitude >= _options.MinMove)
                {
                    dx = d.x;
                    dy = d.y;
                    dz = d.z;
                }
            }
            _currentSamples.Add(new SubsegmentSample
            {
                seq = _nextSeq++,
                level_index = GetLevelIndex(),
                t_ms = (long)Math.Round(endTime * 1000.0),
                px = pos.Value.x,
                py = pos.Value.y,
                pz = pos.Value.z,
                dx = dx,
                dy = dy,
                dz = dz,
                plane_radius = _options.PlaneRadius,
            });
            _lastSamplePosition = pos;
            _lastSampleGameTime = endTime;
        }

        private Vector3? GetCurrentPosition()
        {
            var human = Human.Localplayer;
            if (human == null || human.transform == null) return null;
            return human.transform.position;
        }

        private int GetLevelIndex()
        {
            var state = TimerCore.State;
            if (state == null) return 0;
            // IL samples always use level_index 0 (R8.3.1.3). ML samples use the
            // actual BuiltIn index from the moment the run is in multi mode.
            return _multiRunActive ? state.CurrentLevelNumber : 0;
        }

        // ── Loader ────────────────────────────────────────────────────────

        private string GetCategoryKey()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null || cfg.EnabledTags == null || cfg.EnabledTags.Tags.Count == 0)
                return "Any";
            var sorted = new List<string>(cfg.EnabledTags.Tags);
            sorted.Sort(StringComparer.Ordinal);
            return string.Join("+", sorted);
        }

        private string GetLevelId(Game game)
        {
            switch (game.currentLevelType)
            {
                case WorkshopItemSource.BuiltIn:
                    return SubsegmentFileStore.SanitizeId("B" + game.currentLevelNumber);
                case WorkshopItemSource.EditorPick:
                    return SubsegmentFileStore.SanitizeId("E" + game.currentLevelNumber);
                case WorkshopItemSource.Subscription:
                case WorkshopItemSource.LocalWorkshop:
                    if (game.workshopLevel != null && game.workshopLevel.workshopId != 0UL)
                        return SubsegmentFileStore.SanitizeId("W" + game.workshopLevel.workshopId);
                    return SubsegmentFileStore.SanitizeId("W" + game.currentLevelNumber);
                default:
                    return SubsegmentFileStore.SanitizeId("L" + game.currentLevelNumber);
            }
        }

        private void LoadReferences(Game game)
        {
            _references.Clear();
            try
            {
                string levelId = GetLevelId(game);
                string category = GetCategoryKey();

                if (_multiRunActive && game.currentLevelNumber != 0)
                {
                    LoadPbMl(levelId, category);
                    LoadLoadMl(levelId, category);
                }
                else
                {
                    LoadPbIl(levelId, category);
                    LoadLoadIl(levelId, category);
                }
            }
            catch (Exception ex)
            {
                _references.Clear();
                Plugin.Logger.LogWarning($"HSRTimer: subsegment load failed gracefully: {ex.Message}");
            }
        }

        private void LoadPbIl(string levelId, string category)
        {
            if (string.IsNullOrEmpty(_options.PBPath)) return;
            TryAddIlReference(Path.Combine(_options.PBPath, "IL", levelId, category), "PB");
        }

        private void LoadLoadIl(string levelId, string category)
        {
            if (string.IsNullOrEmpty(_options.LoadPath) || !Directory.Exists(_options.LoadPath)) return;
            foreach (var dir in Directory.GetDirectories(_options.LoadPath))
            {
                string display = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(display)) continue;
                TryAddIlReference(Path.Combine(dir, "IL", levelId, category), display);
            }
        }

        private void LoadPbMl(string levelId, string category)
        {
            if (string.IsNullOrEmpty(_options.PBPath)) return;
            TryAddMlReference(Path.Combine(_options.PBPath, "ML", _options.MultiProject, category), "PB", levelId);
        }

        private void LoadLoadMl(string levelId, string category)
        {
            if (string.IsNullOrEmpty(_options.LoadPath) || !Directory.Exists(_options.LoadPath)) return;
            foreach (var dir in Directory.GetDirectories(_options.LoadPath))
            {
                string display = Path.GetFileName(dir);
                if (string.IsNullOrEmpty(display)) continue;
                TryAddMlReference(Path.Combine(dir, "ML", _options.MultiProject, category), display, levelId);
            }
        }

        private void TryAddIlReference(string refDir, string displayId)
        {
            if (string.IsNullOrEmpty(refDir) || !Directory.Exists(refDir)) return;
            string metaPath = Path.Combine(refDir, "meta.json");
            string samplePath = Path.Combine(refDir, "sample.jsonl");
            AddReference(displayId, refDir, metaPath, samplePath);
        }

        private void TryAddMlReference(string refDir, string displayId, string levelId)
        {
            if (string.IsNullOrEmpty(refDir) || !Directory.Exists(refDir)) return;
            string metaPath = Path.Combine(refDir, "meta.json");
            string levelPath = Path.Combine(refDir, "levels", levelId + ".jsonl");
            AddReference(displayId, refDir, metaPath, levelPath);
        }

        private void AddReference(string displayId, string refDir, string metaPath, string samplePath)
        {
            if (!SubsegmentFileStore.TryLoadMeta(metaPath, out var meta))
                return;
            if (!SubsegmentFileStore.TryLoadSamples(samplePath, out var samples) || samples.Count == 0)
                return;

            var reference = new SubsegmentReference
            {
                DisplayId = displayId,
                SourcePath = refDir,
                Samples = samples,
            };
            BuildPlanes(reference, samples);
            _references.Add(reference);
        }

        private void BuildPlanes(SubsegmentReference reference, List<SubsegmentSample> samples)
        {
            foreach (var sample in samples)
            {
                Vector3 d = sample.Displacement;
                if (d.magnitude < _options.MinMove) continue;
                Vector3 normal = d.normalized;
                float radius = sample.plane_radius > 0f ? sample.plane_radius : _options.PlaneRadius;
                reference.Planes.Add(new SubsegmentPlane
                {
                    Seq = sample.seq,
                    TMs = sample.t_ms,
                    Position = sample.Position,
                    Normal = normal,
                    Radius = radius,
                });
            }
        }

        // ── Comparator ────────────────────────────────────────────────────

        private void RunCrossingDetection(Vector3 pos, RunState state)
        {
            foreach (var reference in _references)
            {
                foreach (var plane in reference.Planes)
                {
                    Vector3 offset = pos - plane.Position;
                    float d = Vector3.Dot(offset, plane.Normal);
                    float lateralSq = (offset - plane.Normal * d).sqrMagnitude;
                    if (!plane.HasPrevD)
                    {
                        plane.PrevD = d;
                        plane.HasPrevD = true;
                        continue;
                    }

                    if (plane.PrevD < 0f && d >= 0f && lateralSq <= plane.Radius * plane.Radius)
                    {
                        float now = Time.unscaledTime;
                        if (now - plane.LastDebounceUnscaledTime < _options.PlaneDebounceSeconds)
                        {
                            plane.PrevD = d;
                            continue;
                        }

                        if (IsStaleLoop(plane, pos, out bool uncertain))
                        {
                            plane.PrevD = d;
                            if (!uncertain && _options.DebugLogging)
                                Plugin.Logger.LogInfo($"HSRTimer: subsegment suppressed stale loop '{reference.DisplayId}' plane seq {plane.Seq}.");
                            continue;
                        }

                        long hitMs = (long)Math.Round(state.GameTime * 1000.0);
                        plane.CandidateHitMs = hitMs;
                        plane.QuietStartUnscaledTime = Time.unscaledTime;
                        plane.HasQuiet = true;
                        plane.LastDebounceUnscaledTime = Time.unscaledTime;
                        if (_options.DebugLogging)
                            Plugin.Logger.LogInfo($"HSRTimer: subsegment crossing candidate '{reference.DisplayId}' plane seq {plane.Seq} at {hitMs} ms.");
                    }
                    plane.PrevD = d;
                }
            }
        }

        private bool IsStaleLoop(SubsegmentPlane plane, Vector3 currentPos, out bool uncertain)
        {
            uncertain = false;
            if (_currentSamples.Count == 0) return false;

            // Find the player's earliest sample whose position is already within
            // this plane and whose time is before the reference sample. We also
            // require the earlier sample to be on the *positive/after* side of
            // the plane (same side as a completed crossing). Otherwise a normal
            // approach through a wide 50 m plane would be misclassified as a
            // stale loop just because the player was already near the plane.
            int startIndex = -1;
            for (int i = 0; i < _currentSamples.Count; i++)
            {
                var s = _currentSamples[i];
                if (s.t_ms >= plane.TMs) continue;
                if ((s.Position - plane.Position).sqrMagnitude > plane.Radius * plane.Radius) continue;
                if (Vector3.Dot(s.Position - plane.Position, plane.Normal) < 0f) continue;
                startIndex = i;
                break;
            }
            if (startIndex < 0) return false;

            // Check sample continuity from that point to now. A large jump means
            // the player failed/rewound and this crossing is legitimate; a gap in
            // seq (e.g. mid-run save/load) is treated conservatively as NOT a
            // stale loop (R8.4.4.4).
            for (int i = startIndex + 1; i < _currentSamples.Count; i++)
            {
                var prev = _currentSamples[i - 1];
                var cur = _currentSamples[i];
                if (cur.seq != prev.seq + 1)
                {
                    uncertain = true;
                    return false;
                }
                if ((cur.Position - prev.Position).magnitude > _options.RespawnJumpMeters)
                    return false;
            }

            // Also ensure continuity from the last sample to the current frame.
            if (_currentSamples.Count > 0)
            {
                var last = _currentSamples[_currentSamples.Count - 1];
                if ((currentPos - last.Position).magnitude > _options.RespawnJumpMeters)
                    return false;
            }

            return true;
        }

        // ── PB writing ────────────────────────────────────────────────────

        private void WriteIlPb(string levelId, RunState state, long endTimeMs)
        {
            if (_currentSamples.Count == 0)
                return;
            string category = GetCategoryKey();
            string dir = Path.Combine(_options.PBPath, "IL", levelId, category);
            string metaPath = Path.Combine(dir, "meta.json");

            // IL PB files are per-level: level_index is always 0 and t_ms is
            // relative to the segment start. In a multi-run the recorder may be
            // holding cumulative game-time samples (which are correct for the ML
            // files), so normalize a copy before writing the IL record.
            long levelStartMs = (long)Math.Round(state.SegmentStart * 1000.0);
            long totalMs = Math.Max(0L, endTimeMs - levelStartMs);
            if (SubsegmentFileStore.TryReadTotalMs(metaPath, out long existing) && existing <= totalMs)
                return;

            var ilSamples = new List<SubsegmentSample>(_currentSamples.Count);
            foreach (var s in _currentSamples)
            {
                ilSamples.Add(new SubsegmentSample
                {
                    seq = s.seq,
                    level_index = 0,
                    t_ms = Math.Max(0L, s.t_ms - levelStartMs),
                    px = s.px,
                    py = s.py,
                    pz = s.pz,
                    dx = s.dx,
                    dy = s.dy,
                    dz = s.dz,
                    plane_radius = s.plane_radius,
                });
            }

            string samplePath = Path.Combine(dir, "sample.jsonl");
            if (!SubsegmentFileStore.WriteAtomic(samplePath, SubsegmentFileStore.MakeSampleJson(ilSamples)))
                return;
            string metaJson = SubsegmentFileStore.MakeMetaJson(
                "IL", levelId, null, category, null, totalMs, ilSamples.Count);
            if (!SubsegmentFileStore.WriteAtomic(metaPath, metaJson))
                return;
            Plugin.Logger.LogInfo($"HSRTimer: subsegment PB written IL/{levelId}/{category} ({totalMs} ms, {ilSamples.Count} samples).");
        }

        private void WriteMultiPb(RunState state)
        {
            if (_multiRunSamples.Count == 0)
                return;
            string subproject = MultiSubprojectForLevel(_lastCompletedLevelNumber);
            if (subproject == null)
                return;
            string category = GetCategoryKey();
            string dir = Path.Combine(_options.PBPath, "ML", subproject, category);
            string metaPath = Path.Combine(dir, "meta.json");
            if (SubsegmentFileStore.TryReadTotalMs(metaPath, out long existing) && existing <= _multiRunTotalMs)
                return;

            int sampleCount = _multiRunSamples.Values.Sum(s => s.Count);
            foreach (var kv in _multiRunSamples)
            {
                string samplePath = Path.Combine(dir, "levels", kv.Key + ".jsonl");
                if (!SubsegmentFileStore.WriteAtomic(samplePath, SubsegmentFileStore.MakeSampleJson(kv.Value)))
                    return;
            }

            string metaJson = SubsegmentFileStore.MakeMetaJson(
                "ML", null, subproject, category, _multiRunLevelIds.ToArray(), _multiRunTotalMs, sampleCount);
            if (!SubsegmentFileStore.WriteAtomic(metaPath, metaJson))
                return;

            _multiRunPbWritten = true;
            Plugin.Logger.LogInfo($"HSRTimer: subsegment PB written ML/{subproject}/{category} ({_multiRunTotalMs} ms, {sampleCount} samples).");
        }

        private static bool IsMultiEndLevel(int levelNumber)
        {
            return levelNumber == 8 || levelNumber == 9 || levelNumber == 10 || levelNumber == 12;
        }

        private static string MultiSubprojectForLevel(int levelNumber)
        {
            switch (levelNumber)
            {
                case 8: return "Aztec%";
                case 9: return "Dark%";
                case 10: return "Steam%";
                case 12: return "Any%";
                default: return null;
            }
        }
    }
}
