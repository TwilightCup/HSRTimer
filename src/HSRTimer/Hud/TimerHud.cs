using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// The standard-style timer display, rendered as plain text directly on
    /// screen (no window, no chrome, not draggable). Shows the configured
    /// ordered rows with a per-character two-color gradient, an invalid-reason
    /// line when the run is flagged, and any user custom texts at arbitrary
    /// screen positions. The main block's offset and font size come from
    /// <see cref="LayoutModel"/>; visibility from <see cref="SettingsModel"/>.
    /// All strings come from <see cref="LocalizationService"/>.
    /// </summary>
    public class TimerHud : MonoBehaviour
    {
        private const float MarkerAxisIndicatorHeight = 38f;

        /// <summary>How long a soft flag stays red after a new trigger.</summary>
        private const float SoftFlagFlashSeconds = 0.6f;

        /// <summary>Separator between two soft flags sharing the single HUD line.</summary>
        private const string SoftFlagSeparator = "  ";

        private GUIStyle _rowStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _customStyle;
        private GUIStyle _axisLabelStyle;
        private Font _font;
        private int _appliedFontSize = -1;

        private void Awake()
        {
            _rowStyle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                richText = false,
                // White base so GUI.color carries the per-character gradient
                // unchanged (final text color = GUI.color * textColor).
                normal = { textColor = Color.white },
            };
            _bannerStyle = new GUIStyle
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.red },
            };
            _customStyle = new GUIStyle
            {
                fontSize = 16,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white },
            };
            _axisLabelStyle = new GUIStyle
            {
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white },
            };
        }

        /// <summary>(Re)create the dynamic OS font when the configured size changes.</summary>
        private void EnsureFont(int size)
        {
            if (size <= 0) size = 18;
            if (_font != null && _appliedFontSize == size) return;
            try
            {
                // A dynamic OS font with a broad fallback list renders Latin + CJK.
                _font = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "PingFang SC", "Microsoft YaHei", "Noto Sans CJK SC",
                    "Noto Sans CJK", "Heiti SC", "Arial Unicode MS", "Arial",
                }, size);
                _appliedFontSize = size;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: dynamic font creation failed: {ex.Message}");
                _font = null;
            }
        }

        private void OnGUI()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;

            int size = cfg.Layout.FontSize;
            EnsureFont(size);
            ApplyFontToStyles(size);

            // Global show/hide toggle.
            if (!cfg.Settings.ShowHud)
            {
                // R10.4.2: the marker-edit-mode hint is independent of show_hud.
                // With the timer HUD hidden there is no block to anchor to, so
                // the hint (and its XYZ axis indicator) is drawn at the
                // bottom-left of the screen instead.
                float x = 12f;
                float y = Screen.height - MarkerEditModeBlockHeight(cfg) - 12f;
                y += DrawMarkerEditModeLine(cfg, cfg.Localization, x, y);
                y += DrawMarkerAxisIndicator(cfg, x, y);
                DrawCustomTexts(cfg);
                return;
            }

            DrawMainBlock(cfg);

            DrawCustomTexts(cfg);
        }

        /// <summary>
        /// Draw the configured columns left-to-right, then the shared extra
        /// status lines below the tallest column. Each non-empty column is a
        /// stack of rows drawn at its own x; an empty column (or one whose rows
        /// are all hidden) is skipped and takes no space.
        /// </summary>
        private void DrawMainBlock(ConfigService cfg)
        {
            var state = TimerCore.State;
            if (state == null) return;

            var layout = cfg.Layout;
            var loc = cfg.Localization;
            float x = layout.OffsetX;
            float y = layout.OffsetY;
            const float colGap = 24f;

            // LastRun hides once a *new* run starts timing — but not during the
            // epilogue (the Credits level the game loads after the campaign
            // finishes), which belongs to the run that just ended.
            bool showLastRun = state.LastRunTicks.HasValue
                && (state.InEpilogueSegment || (!state.InSegment && state.PlayableTicks == 0UL));

            // Each configured column, left-to-right by index. Every row type is
            // a regular column row now (including LastRun and WakeUpTime); the
            // RealTime clock always runs in the background and its row shows
            // wherever the user placed it.
            float rightX = x;
            float bottomY = y;
            var colIndices = new List<int>(layout.Columns.Keys);
            colIndices.Sort();
            foreach (var col in colIndices)
            {
                var rows = layout.Columns[col];
                // Rows are drawn top-to-bottom by their 1-based position.
                var positions = new List<int>(rows.Keys);
                positions.Sort();
                float colX = rightX;
                float colY = y;
                float colWidth = 0f;
                bool anyDrawn = false;
                foreach (var pos in positions)
                {
                    var row = rows[pos];
                    // LastRun only renders while the previous run is on show.
                    if (row == RowType.LastRun && !showLastRun)
                        continue;
                    string label, value;
                    GetRow(row, cfg, state, loc, out label, out value);
                    string line = label.Length > 0 ? (label + ":  " + value) : value;
                    DrawGradientLine(line, layout.ColorA, layout.ColorB, colX, colY, _rowStyle);
                    var size = _rowStyle.CalcSize(new GUIContent(line));
                    if (size.x > colWidth) colWidth = size.x;
                    colY += size.y + 2f;
                    anyDrawn = true;
                }
                if (!anyDrawn)
                    continue; // empty column (or every row hidden): not displayed

                rightX = colX + colWidth + colGap;
                if (colY > bottomY) bottomY = colY;
            }

            // Extras sit below the tallest column, starting at the left edge.
            float ey = bottomY;

            // Timing-standard indicator (R1.4.2): one line directly under the
            // time rows while the plcc timing standard is enabled, so the active
            // timing mode is visible on the HUD.
            ey += DrawPlccModeLine(cfg, loc, layout, x, ey);

            // Current rule tags: one line right under the timer rows listing the
            // enabled tags (localized). Skipped when none are enabled. Rendered
            // before the tag extras and the invalid banner; the marker-edit-mode
            // block is drawn last, at the very bottom of the HUD.
            ey += DrawTagsLine(cfg, loc, layout, x, ey);

            // Voiceline / checkpoint extras (R3.3.3, R3.6.3) for the active category.
            ey += DrawTagExtras(cfg, state, loc, x, ey);

            // Invalid banner (R5.3.2). Soft flags are NOT part of this banner;
            // they render on their own lines in normal HUD text color below.
            if (state.Flags.HasHardInvalid)
            {
                string reasons = state.Flags.FormatReasons(loc);
                string banner = loc.Get("INVALID_RUN") + ": " + reasons;
                var content = new GUIContent(banner);
                _bannerStyle.normal.textColor = Color.red;
                DrawGradientLine(banner, Color.red, new Color(1f, 0.4f, 0.4f, 1f), x, ey, _bannerStyle);
                ey += _bannerStyle.CalcSize(content).y + 2f;
            }

            // Soft flags: normal HUD text with a trigger count, all on one line.
            // Only the individual flag that was newly triggered flashes red.
            ey += DrawSoftFlags(cfg, state, loc, x, ey);

            // R6.5: show the retry-target-resolution failure in the same red
            // banner style as a run invalid hint. It stays visible until the
            // override is turned off or a retry is pressed with a resolvable
            // value.
            if (cfg.Settings.RetryTargetInvalidHint)
            {
                string banner = loc.Get("HUD_INVALID_RETRY_TARGET");
                _bannerStyle.normal.textColor = Color.red;
                DrawGradientLine(banner, Color.red, new Color(1f, 0.4f, 0.4f, 1f), x, ey, _bannerStyle);
                ey += _bannerStyle.CalcSize(new GUIContent(banner)).y + 2f;
            }

            // R10.4.2: the marker-edit-mode hint is the last line of the timer
            // HUD, and the edit-mode XYZ axis indicator sits directly beneath it.
            ey += DrawMarkerEditModeLine(cfg, loc, x, ey);
            ey += DrawMarkerAxisIndicator(cfg, x, ey);
        }

        /// <summary>One line listing the currently enabled rule tags (localized), directly
        /// under the timer rows. Returns the vertical space consumed (0 when no tags
        /// are enabled, so the line is omitted entirely).</summary>
        private float DrawTagsLine(ConfigService cfg, LocalizationService loc, LayoutModel layout, float x, float y)
        {
            if (cfg == null || cfg.EnabledTags == null || cfg.EnabledTags.Tags.Count == 0)
                return 0f;
            string line = loc.Get("HUD_TAGS_LABEL") + ":  " + TemplateVars.CategoryName(cfg);
            DrawGradientLine(line, layout.ColorA, layout.ColorB, x, y, _rowStyle);
            return _rowStyle.CalcSize(new GUIContent(line)).y + 2f;
        }

        /// <summary>
        /// One line directly under the time rows shown only while the plcc timing
        /// standard is enabled ("Use plcc timing standard", R1.4.2), so the HUD
        /// reflects the active timing mode. Returns the vertical space consumed
        /// (0 when the option is off, so the line is omitted entirely).
        /// </summary>
        private float DrawPlccModeLine(ConfigService cfg, LocalizationService loc, LayoutModel layout, float x, float y)
        {
            if (cfg.Settings == null || !cfg.Settings.UsePlccTimingStandard)
                return 0f;
            string line = loc.Get("HUD_PLCC_TIMING_MODE");
            DrawGradientLine(line, layout.ColorA, layout.ColorB, x, y, _rowStyle);
            return _rowStyle.CalcSize(new GUIContent(line)).y + 2f;
        }

        /// <summary>
        /// R10.4.2: one conspicuous amber line while the marker edit mode is on.
        /// Returns the vertical space consumed (0 when not in edit mode).
        /// </summary>
        private float DrawMarkerEditModeLine(ConfigService cfg, LocalizationService loc, float x, float y)
        {
            if (cfg.Settings == null || !cfg.Settings.MarkersEnable || !cfg.Settings.MarkersEditMode)
                return 0f;
            string line = loc.Get("MARKER_HUD_EDIT_MODE");
            DrawGradientLine(line, new Color(1f, 0.8f, 0.2f, 1f), new Color(1f, 0.55f, 0.1f, 1f), x, y, _rowStyle);
            return _rowStyle.CalcSize(new GUIContent(line)).y + 2f;
        }

        /// <summary>
        /// R10.4.2: a small XYZ axis indicator like a 3D editor's orientation
        /// gizmo. It projects the world axes through the active camera, so it
        /// follows the player's view angle in normal gameplay and in F8 free
        /// roam. Returns the vertical space consumed (0 when not in edit mode).
        /// </summary>
        private float DrawMarkerAxisIndicator(ConfigService cfg, float x, float y)
        {
            if (cfg.Settings == null || !cfg.Settings.MarkersEnable || !cfg.Settings.MarkersEditMode)
                return 0f;
            var cam = MarkerOverlay.GetCamera();
            if (cam == null || cam.transform == null)
                return 0f;

            const float size = 30f;
            Vector2 origin = new Vector2(x + size * 0.5f, y + size * 0.5f);

            DrawAxisLine(origin, origin + ProjectAxis(cam, Vector3.right, size), new Color(1f, 0.3f, 0.3f, 1f), "X");
            DrawAxisLine(origin, origin + ProjectAxis(cam, Vector3.up, size), new Color(0.3f, 1f, 0.3f, 1f), "Y");
            DrawAxisLine(origin, origin + ProjectAxis(cam, Vector3.forward, size), new Color(0.3f, 0.6f, 1f, 1f), "Z");

            return MarkerAxisIndicatorHeight;
        }

        private float MarkerEditModeBlockHeight(ConfigService cfg)
        {
            if (cfg.Settings == null || !cfg.Settings.MarkersEnable || !cfg.Settings.MarkersEditMode)
                return 0f;
            string line = cfg.Localization.Get("MARKER_HUD_EDIT_MODE");
            float height = _rowStyle.CalcSize(new GUIContent(line)).y + 2f;
            var cam = MarkerOverlay.GetCamera();
            if (cam != null && cam.transform != null)
                height += MarkerAxisIndicatorHeight;
            return height;
        }

        private static Vector2 ProjectAxis(Camera cam, Vector3 worldAxis, float size)
        {
            Vector3 local = cam.transform.InverseTransformDirection(worldAxis);
            return new Vector2(local.x, -local.y) * size;
        }

        private void DrawAxisLine(Vector2 a, Vector2 b, Color color, string label)
        {
            var prevColor = GUI.color;
            GUI.color = color;

            Vector2 dir = b - a;
            float len = dir.magnitude;
            if (len > 0.5f)
            {
                float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                var prevMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(angle, a);
                GUI.DrawTexture(new Rect(a.x, a.y - 1.5f, len, 3f), Texture2D.whiteTexture);
                GUI.matrix = prevMatrix;

                GUI.Label(new Rect(b.x - 8f, b.y - 8f, 16f, 16f), label, _axisLabelStyle);
            }

            GUI.color = prevColor;
        }

        /// <summary>
        /// Draw every active soft flag on a single line in the normal HUD text
        /// gradient, each segment followed by its trigger count ("name x3").
        /// Only the individual flag whose trigger is within
        /// <see cref="SoftFlagFlashSeconds"/> flashes red, so co-occurring flags
        /// flash independently of each other. Returns the vertical space consumed.
        /// </summary>
        private float DrawSoftFlags(ConfigService cfg, RunState state, LocalizationService loc, float x, float y)
        {
            var flags = new List<ValidityFlags.SoftFlagInfo>(state.Flags.SoftFlags);
            if (flags.Count == 0) return 0f;

            // Stable left→right order independent of dictionary iteration order.
            flags.Sort((l, r) => l.Reason.CompareTo(r.Reason));

            float now = Time.realtimeSinceStartup;
            var sb = new StringBuilder();
            var flashMask = new List<bool>();
            for (int f = 0; f < flags.Count; f++)
            {
                if (f > 0)
                {
                    sb.Append(SoftFlagSeparator);
                    for (int i = 0; i < SoftFlagSeparator.Length; i++)
                        flashMask.Add(false);
                }
                string segment = loc.Get(InvalidReasons.LocalKey(flags[f].Reason)) + " x" + flags[f].Count;
                bool flash = now - flags[f].LastTriggerTime <= SoftFlagFlashSeconds;
                sb.Append(segment);
                for (int i = 0; i < segment.Length; i++)
                    flashMask.Add(flash);
            }

            string line = sb.ToString();
            DrawPerCharacterLine(line, flashMask, Color.red, new Color(1f, 0.4f, 0.4f, 1f),
                cfg.Layout.ColorA, cfg.Layout.ColorB, x, y, _rowStyle);
            return _rowStyle.CalcSize(new GUIContent(line)).y + 2f;
        }

        private float DrawTagExtras(ConfigService cfg, RunState state, LocalizationService loc, float x, float y)
        {
            var tags = cfg.EnabledTags;
            if (tags == null) return 0f;
            float added = 0f;

            // Checkpoint tag: show current checkpoint number (R3.3.3).
            if (tags.HasTag(TagIds.Checkpoint) && state.Game != null)
            {
                string line = loc.Get("CHECKPOINT_CURRENT") + ":  " + state.Game.currentCheckpointNumber;
                DrawGradientLine(line, cfg.Layout.ColorA, cfg.Layout.ColorB, x, y + added, _rowStyle);
                added += _rowStyle.CalcSize(new GUIContent(line)).y + 2f;
            }

            // Voiceline tag: show triggered/total progress (R3.6.3).
            if (tags.HasTag(TagIds.Voiceline))
            {
                var rule = TagRuleRegistry.Instance != null ? TagRuleRegistry.Instance.Find(TagIds.Voiceline) as VoicelineTagRule : null;
                if (rule != null && rule.Tracker != null)
                {
                    string line = loc.Get("VOICELINE_COUNT") + ":  " + rule.Tracker.TriggeredCount + "/" + rule.Tracker.TotalCount;
                    DrawGradientLine(line, cfg.Layout.ColorA, cfg.Layout.ColorB, x, y + added, _rowStyle);
                    added += _rowStyle.CalcSize(new GUIContent(line)).y + 2f;
                }
            }

            return added;
        }

        private void GetRow(RowType row, ConfigService cfg, RunState state, LocalizationService loc,
                            out string label, out string value)
        {
            switch (row)
            {
                case RowType.GameTime:
                    label = loc.Get("TIMER_GAME_TIME");
                    value = TimeFormatter.Format(state.GameTimeSeconds);
                    break;
                case RowType.RealTime:
                    label = loc.Get("TIMER_REAL_TIME");
                    value = TimeFormatter.Format(state.RealTime);
                    break;
                case RowType.PrevRt:
                    label = loc.Get("TIMER_PREV_RT");
                    value = TimeFormatter.Format(GameClock.RealTimeAtLastSegmentSeconds(state));
                    break;
                case RowType.CurrentSegment:
                    label = loc.Get("TIMER_SEGMENT_TIME");
                    value = TimeFormatter.Format(GameClock.SegmentSeconds(state));
                    break;
                case RowType.TotalAtLastSegment:
                    label = loc.Get("TIMER_LAST_TOTAL");
                    value = TimeFormatter.Format(GameClock.TotalAtLastSegmentSeconds(state));
                    break;
                case RowType.LastSegment:
                    label = loc.Get("TIMER_LAST_SEGMENT");
                    value = TimeFormatter.Format(GameClock.LastSegmentSeconds(state));
                    break;
                case RowType.LastRun:
                    label = loc.Get("TIMER_LAST_RUN");
                    value = TimeFormatter.Format(GameClock.LastRunSeconds(state));
                    break;
                case RowType.CurrentState:
                    label = loc.Get("TIMER_CURRENT_STATE");
                    value = loc.Get(StateKey(state.Game != null ? state.Game.state : GameState.Inactive));
                    break;
                case RowType.WakeUpTime:
                    label = loc.Get("TIMER_WAKE_UP_TIME");
                    value = TimeFormatter.FormatWakeUp(GameClock.WakeUpSeconds(state));
                    break;
                default:
                    label = "";
                    value = "";
                    break;
            }
        }

        private static string StateKey(GameState s)
        {
            switch (s)
            {
                case GameState.Inactive: return "STATE_INACTIVE";
                case GameState.Paused: return "STATE_PAUSED";
                case GameState.LoadingLevel: return "STATE_LOADING";
                case GameState.PlayingLevel: return "STATE_PLAYING";
                default: return "STATE_UNKNOWN";
            }
        }

        /// <summary>Draw one line with a left→right two-color gradient, per character.</summary>
        private static void DrawGradientLine(string text, Color a, Color b, float x, float y, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            style.alignment = TextAnchor.UpperLeft;

            // Measure the whole line so we can place characters with proper spacing.
            var fullSize = style.CalcSize(new GUIContent(text));
            int len = text.Length;

            // Per-character placement: advance by each glyph's width.
            float cx = x;
            for (int i = 0; i < len; i++)
            {
                char ch = text[i];
                Color c = GradientText.Gradient(a, b, i, len);
                var content = new GUIContent(ch.ToString());
                Vector2 size = style.CalcSize(content);
                Color prev = GUI.color;
                GUI.color = c;
                var rect = new Rect(cx, y, size.x, fullSize.y);
                GUI.Label(rect, content, style);
                GUI.color = prev;
                cx += size.x;
            }
        }

        /// <summary>
        /// Draw one line per character with a left→right two-color gradient,
        /// letting a per-character mask override individual characters with an
        /// alternate (flash) gradient. Used so the single soft-flag line can
        /// flash only the segments that were newly triggered.
        /// </summary>
        private static void DrawPerCharacterLine(string text, IList<bool> flashMask,
            Color flashA, Color flashB, Color a, Color b, float x, float y, GUIStyle style)
        {
            if (string.IsNullOrEmpty(text)) return;
            style.alignment = TextAnchor.UpperLeft;

            var fullSize = style.CalcSize(new GUIContent(text));
            int len = text.Length;

            float cx = x;
            for (int i = 0; i < len; i++)
            {
                char ch = text[i];
                bool flash = flashMask != null && i < flashMask.Count && flashMask[i];
                Color c = flash
                    ? GradientText.Gradient(flashA, flashB, i, len)
                    : GradientText.Gradient(a, b, i, len);
                var content = new GUIContent(ch.ToString());
                Vector2 size = style.CalcSize(content);
                Color prev = GUI.color;
                GUI.color = c;
                var rect = new Rect(cx, y, size.x, fullSize.y);
                GUI.Label(rect, content, style);
                GUI.color = prev;
                cx += size.x;
            }
        }

        private void DrawCustomTexts(ConfigService cfg)
        {
            if (cfg == null || cfg.Layout.CustomTexts.Count == 0) return;
            var state = TimerCore.State;
            double gt = state != null ? state.GameTimeSeconds : 0d;
            double rt = state != null ? state.RealTime : 0d;
            foreach (var ct in cfg.Layout.CustomTexts)
            {
                string resolved = TemplateVars.Resolve(ct.Text, gt, rt);
                DrawGradientLine(resolved, ct.ColorA, ct.ColorB, ct.X, ct.Y, _customStyle);
            }
        }

        private void ApplyFontToStyles(int size)
        {
            if (_font != null)
            {
                if (_rowStyle.font != _font) _rowStyle.font = _font;
                if (_bannerStyle.font != _font) _bannerStyle.font = _font;
                if (_customStyle.font != _font) _customStyle.font = _font;
                if (_axisLabelStyle.font != _font) _axisLabelStyle.font = _font;
            }
            if (_rowStyle.fontSize != size) _rowStyle.fontSize = size;
            // The invalid banner matches the timer rows' size (the fixed smaller
            // size made the warning look like a footnote next to the timer).
            if (_bannerStyle.fontSize != size) _bannerStyle.fontSize = size;
        }
    }
}
