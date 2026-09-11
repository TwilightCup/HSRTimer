using System.Collections.Generic;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// The "Markers" settings tab (R10.5): an in-tab navigation with a root page
    /// (edit-mode toggle + Main/Extra/Workshop dropdowns) and per-level pages
    /// (marker list + expandable editors). All edits go through
    /// <see cref="MarkersManager.MarkDirty"/> and are flushed by the manager
    /// (R10.5.8). IMGUI has no Popup in this Unity version, so the dropdowns are
    /// buttons with collapsible button lists, mirroring the language selector.
    /// </summary>
    public static class MarkersPanel
    {
        // ── navigation state ──
        private static string _sourceExpanded;          // "main" | "extra" | "workshop" | null
        private static MarkerLevelEntry _levelPage;     // null = root page
        private static string _levelPageSource;         // source group to restore on Back
        private static MarkerSet _currentSet;
        private static string _currentCategory;
        private static string _expandedMarkerId;
        private static string _confirmDeleteId;

        // Per-editor text buffers (IMGUI text fields are stateless).
        private static readonly Dictionary<string, string> _floatBuf = new Dictionary<string, string>();
        private static bool _managerMissingLogged;

        private static readonly string[] SourceKeys = { "MARKER_SOURCE_MAIN_DREAMS", "MARKER_SOURCE_EXTRA_DREAMS", "MARKER_SOURCE_WORKSHOP" };

        public static void Draw(ConfigService cfg, LocalizationService loc,
            GUIStyle section, GUIStyle label, GUIStyle value, GUIStyle small,
            GUIStyle toggle, GUIStyle button, GUIStyle textField)
        {
            var s = cfg.Settings;

            GUILayout.Space(6);
            GUILayout.Label(loc.Get("PANEL_MARKERS"), section);

            bool editMode = Toggle(loc.Get("SETTINGS_MARKERS_EDIT_MODE"), s.MarkersEditMode, toggle);
            if (editMode != s.MarkersEditMode)
                s.MarkersEditMode = editMode;
            bool enable = Toggle(loc.Get("SETTINGS_MARKERS_ENABLE"), s.MarkersEnable, toggle);
            if (enable != s.MarkersEnable)
                s.MarkersEnable = enable;

            if (!s.MarkersEnable)
            {
                GUILayout.Label(loc.Get("SETTINGS_MARKERS_DISABLED_HINT"), small);
                return;
            }

            if (_levelPage != null)
            {
                DrawLevelPage(cfg, loc, section, label, value, small, toggle, button, textField);
                return;
            }

            DrawRootPage(cfg, loc, small, button);
        }

        // ── root page: three source dropdowns ──────────────────────────────

        private static void DrawRootPage(ConfigService cfg, LocalizationService loc, GUIStyle small, GUIStyle button)
        {
            DrawSourceDropdown("main", SourceKeys[0], MarkerCatalog.MainDreams(), loc, small, button);
            DrawSourceDropdown("extra", SourceKeys[1], MarkerCatalog.ExtraDreams(), loc, small, button);
            DrawSourceDropdown("workshop", SourceKeys[2], MarkerCatalog.Workshop(), loc, small, button);
        }

        private static void DrawSourceDropdown(string id, string titleKey, List<MarkerLevelEntry> levels,
            LocalizationService loc, GUIStyle small, GUIStyle button)
        {
            string title = (_sourceExpanded == id ? "▾ " : "▸ ") + loc.Get(titleKey);
            if (GUILayout.Button(title, button))
                _sourceExpanded = _sourceExpanded == id ? null : id;

            if (_sourceExpanded != id)
                return;

            if (levels == null || levels.Count == 0)
            {
                GUILayout.Label(loc.Get("MARKER_LEVEL_META_PENDING"), small);
                return;
            }

            foreach (var entry in levels)
            {
                if (entry == null) continue;
                string display = string.IsNullOrEmpty(entry.DisplayName) ? entry.LevelKey ?? "?" : entry.DisplayName;
                if (!entry.Available)
                {
                    GUILayout.Label(display + "  (" + loc.Get("MARKER_LEVEL_UNAVAILABLE") + ")", small);
                    continue;
                }
                if (GUILayout.Button(display + "  [" + entry.LevelKey + "]", button))
                {
                    EnterLevelPage(entry, id);
                }
            }
        }

        private static void EnterLevelPage(MarkerLevelEntry entry, string sourceId)
        {
            _levelPage = entry;
            _levelPageSource = sourceId;
            _sourceExpanded = null;
            _currentCategory = MarkerStore.CategoryKey();
            var mgr = MarkersManager.Instance;
            _currentSet = mgr != null
                ? mgr.GetOrCreateSet(entry.LevelKey, entry.Source.ToString(), (int)entry.WorkshopId, _currentCategory)
                : null;
            _expandedMarkerId = null;
            _confirmDeleteId = null;
            _hint = null;
        }

        // ── level page ─────────────────────────────────────────────────────

        private static void DrawLevelPage(ConfigService cfg, LocalizationService loc,
            GUIStyle section, GUIStyle label, GUIStyle value, GUIStyle small,
            GUIStyle toggle, GUIStyle button, GUIStyle textField)
        {
            var s = cfg.Settings;
            GUILayout.Space(6);

            if (GUILayout.Button(loc.Get("MARKER_BACK"), button))
            {
                _levelPage = null;
                _currentSet = null;
                _currentCategory = null;
                _expandedMarkerId = null;
                _confirmDeleteId = null;
                _hint = null;
                _sourceExpanded = _levelPageSource; // restore the group that was open (R10.5.2)
                return;
            }

            GUILayout.Label((_levelPage.DisplayName ?? _levelPage.LevelKey ?? "?") + "  [" + _currentCategory + "]", label);

            // If the active tag set changed, the marker data lives under the new
            // category key — reload so the page always edits the visible set.
            string categoryNow = MarkerStore.CategoryKey();
            if (categoryNow != _currentCategory)
            {
                _currentCategory = categoryNow;
                var mgr = MarkersManager.Instance;
                _currentSet = mgr != null
                    ? mgr.GetOrCreateSet(_levelPage.LevelKey, _levelPage.Source.ToString(), (int)_levelPage.WorkshopId, _currentCategory)
                    : null;
                _expandedMarkerId = null;
                _confirmDeleteId = null;
            }

            if (_currentSet == null)
            {
                GUILayout.Label(loc.Get("MARKER_LEVEL_UNAVAILABLE"), small);
                return;
            }
            if (_currentSet.markers == null)
                _currentSet.markers = new List<MarkerDef>();

            foreach (var def in _currentSet.markers)
            {
                if (def == null) continue;
                string row = MarkerRowText(def, loc);
                bool expanded = _expandedMarkerId == def.id;
                string prefix = expanded ? "▾ " : "▸ ";
                if (GUILayout.Button(prefix + row, button))
                    _expandedMarkerId = expanded ? null : def.id;

                if (expanded)
                {
                    if (s.MarkersEditMode)
                        DrawMarkerEditor(cfg, loc, def, label, value, small, toggle, button, textField);
                    else
                        DrawMarkerReadOnly(cfg, loc, def, label, value);
                }
            }

            // R10.5.2: new marker button only in edit mode.
            if (s.MarkersEditMode)
            {
                if (GUILayout.Button(loc.Get("MARKER_NEW"), button))
                    CreateMarker(loc);
            }
        }

        private static string MarkerRowText(MarkerDef def, LocalizationService loc)
        {
            string name = string.IsNullOrEmpty(def.name) ? "(" + (string.IsNullOrEmpty(def.id) ? "?" : def.id) + ")" : def.name;
            string type = loc.Get(TypeKey(def.Kind));
            string pb = "PB: " + (PbDisplay(def, loc));
            return name + "  [" + type + "]  " + pb;
        }

        private static string PbDisplay(MarkerDef def, LocalizationService loc)
        {
            if (_currentSet == null)
                return "--";
            long? pbMs = _currentSet.PbTimeOf(def.id);
            return pbMs.HasValue ? TimeFormatter.Format(pbMs.Value / 1000.0) : loc.Get("MARKER_NO_PB");
        }

        private static void CreateMarker(LocalizationService loc)
        {
            if (_currentSet == null) return;
            string id = MarkersManager.NextMarkerId(_currentSet);
            var def = new MarkerDef
            {
                id = id,
                name = loc.Get("MARKER_DEFAULT_NAME") + " " + id,
                type = MarkerKindUtil.ToString(MarkerKind.Range),
                enabled = true,
                // A small valid box by default so a freshly created marker is
                // immediately triggerable and visible in the edit-mode overlay.
                sx = 2f,
                sy = 3f,
                sz = 2f,
            };
            _currentSet.markers.Add(def);
            _expandedMarkerId = def.id;
            _confirmDeleteId = null;
            MarkDirty();
        }

        // ── marker editor (edit mode on) ───────────────────────────────────

        private static void DrawMarkerEditor(ConfigService cfg, LocalizationService loc, MarkerDef def,
            GUIStyle label, GUIStyle value, GUIStyle small, GUIStyle toggle, GUIStyle button, GUIStyle textField)
        {
            // Name input (R10.5.3).
            def.name = TextField(loc.Get("MARKER_NAME"), def.name, textField, label);

            // Type dropdown (button + collapsible list).
            GUILayout.BeginHorizontal();
            GUILayout.Label(loc.Get("MARKER_TYPE"), label, GUILayout.Width(120));
            string current = (_typeOpen ? "▾ " : "▸ ") + loc.Get(TypeKey(def.Kind));
            if (GUILayout.Button(current, button, GUILayout.Width(200)))
                _typeOpen = !_typeOpen;
            GUILayout.EndHorizontal();
            if (_typeOpen)
            {
                foreach (var kind in new[] { MarkerKind.Range, MarkerKind.Checkpoint, MarkerKind.GrabObject })
                {
                    if (GUILayout.Button(loc.Get(TypeKey(kind)), button))
                    {
                        def.type = MarkerKindUtil.ToString(kind);
                        _typeOpen = false;
                        MarkDirty();
                    }
                }
            }

            switch (def.Kind)
            {
                case MarkerKind.Range: DrawRangeEditor(cfg, loc, def, label, value, small, toggle, button, textField); break;
                case MarkerKind.Checkpoint: DrawCheckpointEditor(cfg, loc, def, label, value, toggle, button, textField); break;
                case MarkerKind.GrabObject: DrawGrabObjectEditor(cfg, loc, def, label, value, small, button); break;
            }

            // Per-marker enable (supplemental).
            bool en = Toggle(loc.Get("MARKER_ENABLED"), def.enabled, toggle);
            if (en != def.enabled) { def.enabled = en; MarkDirty(); }

            // PB display.
            GUILayout.Label(loc.Get("MARKER_PB_LABEL") + ": " + PbDisplay(def, loc), value);

            // Delete with confirmation (R10.5.5).
            GUILayout.BeginHorizontal();
            bool confirming = _confirmDeleteId == def.id;
            if (GUILayout.Button(confirming ? loc.Get("MARKER_DELETE_CONFIRM") : loc.Get("MARKER_DELETE"), button, GUILayout.Width(180)))
            {
                if (!confirming)
                    _confirmDeleteId = def.id;
                else
                    DeleteMarker(def);
            }
            if (confirming && GUILayout.Button(loc.Get("MARKER_DELETE_CANCEL"), button, GUILayout.Width(120)))
                _confirmDeleteId = null;
            GUILayout.EndHorizontal();
        }

        private static void DrawRangeEditor(ConfigService cfg, LocalizationService loc, MarkerDef def,
            GUIStyle label, GUIStyle value, GUIStyle small, GUIStyle toggle, GUIStyle button, GUIStyle textField)
        {
            // Set-to-player-position (R10.5.3).
            if (GUILayout.Button(loc.Get("MARKER_SET_TO_PLAYER_POS"), button))
            {
                var mgr = MarkersManager.Instance;
                Vector3 pos;
                string err = null;
                if (mgr == null)
                {
                    WarnManagerMissing();
                }
                else if (mgr.TryCapturePlayerPosition(out pos, out err))
                {
                    def.cx = Round3(pos.x);
                    def.cy = Round3(pos.y);
                    def.cz = Round3(pos.z);
                    // Keep the text-field buffers in sync so the inputs show the
                    // captured position and do not overwrite it on the next draw.
                    _floatBuf[FieldKey(def, "cx")] = F3(def.cx);
                    _floatBuf[FieldKey(def, "cy")] = F3(def.cy);
                    _floatBuf[FieldKey(def, "cz")] = F3(def.cz);
                    MarkDirty();
                    _hint = null;
                }
                else if (!string.IsNullOrEmpty(err))
                {
                    _hint = err;
                }
            }
            if (_hint != null)
                GUILayout.Label(loc.Get(_hint), small);

            def.cx = FloatField(loc.Get("MARKER_POS_X"), def.cx, textField, label, FieldKey(def, "cx"));
            def.cy = FloatField(loc.Get("MARKER_POS_Y"), def.cy, textField, label, FieldKey(def, "cy"));
            def.cz = FloatField(loc.Get("MARKER_POS_Z"), def.cz, textField, label, FieldKey(def, "cz"));
            def.sx = FloatField(loc.Get("MARKER_SIZE_X"), def.sx, textField, label, FieldKey(def, "sx"));
            def.sy = FloatField(loc.Get("MARKER_SIZE_Y"), def.sy, textField, label, FieldKey(def, "sy"));
            def.sz = FloatField(loc.Get("MARKER_SIZE_Z"), def.sz, textField, label, FieldKey(def, "sz"));

            bool grab = Toggle(loc.Get("MARKER_GRAB_ANY"), def.requireGrab, toggle);
            if (grab != def.requireGrab) { def.requireGrab = grab; MarkDirty(); }
            bool jump = Toggle(loc.Get("MARKER_JUMP"), def.requireJump, toggle);
            if (jump != def.requireJump) { def.requireJump = jump; MarkDirty(); }

            if (def.sx <= 0f || def.sy <= 0f || def.sz <= 0f)
                GUILayout.Label(loc.Get("MARKER_INVALID_RANGE"), small);
        }

        private static void DrawCheckpointEditor(ConfigService cfg, LocalizationService loc, MarkerDef def,
            GUIStyle label, GUIStyle value, GUIStyle toggle, GUIStyle button, GUIStyle textField)
        {
            def.checkpointIndex = IntField(loc.Get("MARKER_CHECKPOINT_INDEX"), def.checkpointIndex, textField, label, FieldKey(def, "cp"));
            bool load = Toggle(loc.Get("MARKER_TRIGGER_ON_LOAD"), def.triggerOnLoad, toggle);
            if (load != def.triggerOnLoad) { def.triggerOnLoad = load; MarkDirty(); }
        }

        private static void DrawGrabObjectEditor(ConfigService cfg, LocalizationService loc, MarkerDef def,
            GUIStyle label, GUIStyle value, GUIStyle small, GUIStyle button)
        {
            if (GUILayout.Button(loc.Get("MARKER_SET_CURRENT_GRAB"), button))
            {
                var mgr = MarkersManager.Instance;
                string err = null;
                if (mgr == null)
                {
                    WarnManagerMissing();
                }
                else if (mgr.TryCaptureGrabbedObject(def, out err))
                {
                    MarkDirty();
                    _hint = null;
                }
                else if (!string.IsNullOrEmpty(err))
                {
                    _hint = err;
                }
            }
            if (_hint != null)
                GUILayout.Label(loc.Get(_hint), small);

            string idText = string.IsNullOrEmpty(def.objectName) ? loc.Get("MARKER_GRABBED_ID_EMPTY") : def.objectName;
            if (def.objectSceneId != 0u)
                idText += "  #" + def.objectSceneId;
            else if (!string.IsNullOrEmpty(def.objectPath))
                idText += "  " + def.objectPath;
            GUILayout.Label(loc.Get("MARKER_GRABBED_ID") + ": " + idText, value);
        }

        /// <summary>Read-only summary when edit mode is off (R10.5.6).</summary>
        private static void DrawMarkerReadOnly(ConfigService cfg, LocalizationService loc, MarkerDef def,
            GUIStyle label, GUIStyle value)
        {
            GUILayout.Label(loc.Get("MARKER_NAME") + ": " + (string.IsNullOrEmpty(def.name) ? "-" : def.name), value);
            GUILayout.Label(loc.Get("MARKER_TYPE") + ": " + loc.Get(TypeKey(def.Kind)), value);
            switch (def.Kind)
            {
                case MarkerKind.Range:
                    GUILayout.Label(string.Format("{0}: {1} / {2} / {3}", loc.Get("MARKER_POS_X"), F3(def.cx), F3(def.cy), F3(def.cz)), value);
                    GUILayout.Label(string.Format("{0}: {1} / {2} / {3}", loc.Get("MARKER_SIZE_X"), F3(def.sx), F3(def.sy), F3(def.sz)), value);
                    break;
                case MarkerKind.Checkpoint:
                    GUILayout.Label(loc.Get("MARKER_CHECKPOINT_INDEX") + ": " + def.checkpointIndex, value);
                    break;
                case MarkerKind.GrabObject:
                    GUILayout.Label(loc.Get("MARKER_GRABBED_ID") + ": " + (string.IsNullOrEmpty(def.objectName) ? "-" : def.objectName), value);
                    break;
            }
            GUILayout.Label(loc.Get("MARKER_PB_LABEL") + ": " + PbDisplay(def, loc), value);
        }

        private static void DeleteMarker(MarkerDef def)
        {
            if (_currentSet == null) return;
            _currentSet.markers.Remove(def);
            if (_currentSet.pbTimes != null)
                _currentSet.pbTimes.RemoveAll(e => e != null && e.id == def.id);
            // Drop this marker's text buffers: NextMarkerId can reuse a freed id,
            // and a stale buffer would then leak old values into the new marker.
            foreach (var field in new[] { "cx", "cy", "cz", "sx", "sy", "sz", "cp" })
                _floatBuf.Remove(FieldKey(def, field));
            _expandedMarkerId = null;
            _confirmDeleteId = null;
            MarkDirty();
        }

        // ── helpers ────────────────────────────────────────────────────────

        private static bool _typeOpen;
        private static string _hint;

        private static string TypeKey(MarkerKind kind)
        {
            switch (kind)
            {
                case MarkerKind.Checkpoint: return "MARKER_TYPE_CHECKPOINT";
                case MarkerKind.GrabObject: return "MARKER_TYPE_GRAB";
                default: return "MARKER_TYPE_RANGE";
            }
        }

        /// <summary>
        /// Per-marker text-buffer key so different markers never share a field
        /// buffer, and the buffer can be updated when a value is set by code
        /// (e.g. "Set to player position") instead of by typing.
        /// </summary>
        private static string FieldKey(MarkerDef def, string field)
            => (def != null && !string.IsNullOrEmpty(def.id) ? def.id : "?") + "_" + field;

        private static void MarkDirty()
        {
            if (MarkersManager.Instance == null || _currentSet == null)
            {
                WarnManagerMissing();
                return;
            }
            MarkersManager.Instance.MarkDirty(_currentSet);
        }

        /// <summary>
        /// The panel is useless without the runtime manager (it owns the shared
        /// marker sets, capture helpers, and the dirty-flush). Log once so a
        /// wiring failure is visible in the log instead of failing silently.
        /// </summary>
        private static void WarnManagerMissing()
        {
            if (_managerMissingLogged) return;
            _managerMissingLogged = true;
            Plugin.Logger.LogWarning("HSRTimer[markers]: MarkersManager is not available; marker edits cannot be saved. Check for an earlier plugin-Awake error in the log.");
        }

        private static bool Toggle(string text, bool value, GUIStyle style)
            => GUILayout.Toggle(value, text, style);

        private static float FloatField(string labelText, float value, GUIStyle textField, GUIStyle label, string bufKey)
        {
            GUILayout.BeginHorizontal();
            string buf;
            if (!_floatBuf.TryGetValue(bufKey, out buf))
            {
                buf = value.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
                _floatBuf[bufKey] = buf;
            }
            string next = GUILayout.TextField(buf, textField, GUILayout.Width(80));
            _floatBuf[bufKey] = next;
            GUILayout.Label(labelText, label);
            GUILayout.EndHorizontal();
            float parsed;
            if (float.TryParse(next, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out parsed))
            {
                if (parsed != value) MarkDirty();
                return parsed;
            }
            return value;
        }

        private static int IntField(string labelText, int value, GUIStyle textField, GUIStyle label, string bufKey)
        {
            GUILayout.BeginHorizontal();
            string buf;
            if (!_floatBuf.TryGetValue(bufKey, out buf))
            {
                buf = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
                _floatBuf[bufKey] = buf;
            }
            string next = GUILayout.TextField(buf, textField, GUILayout.Width(80));
            _floatBuf[bufKey] = next;
            GUILayout.Label(labelText, label);
            GUILayout.EndHorizontal();
            int parsed;
            if (int.TryParse(next, System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out parsed))
            {
                if (parsed != value) MarkDirty();
                return parsed;
            }
            return value;
        }

        private static string TextField(string labelText, string value, GUIStyle textField, GUIStyle label)
        {
            GUILayout.BeginHorizontal();
            string next = GUILayout.TextField(value ?? "", textField, GUILayout.Width(220));
            GUILayout.Label(labelText, label);
            GUILayout.EndHorizontal();
            if (next != (value ?? "")) MarkDirty();
            return next;
        }

        private static float Round3(float v) => Mathf.Round(v * 1000f) / 1000f;

        private static string F3(float v) => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
    }
}
