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
        private GUIStyle _rowStyle;
        private GUIStyle _bannerStyle;
        private GUIStyle _customStyle;
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

            // Global show/hide toggle.
            if (!cfg.Settings.ShowHud)
            {
                DrawCustomTexts(cfg);
                return;
            }

            int size = cfg.Layout.FontSize;
            EnsureFont(size);
            ApplyFontToStyles(size);

            float mainBlockWidth = DrawMainBlock(cfg);

            DrawRightColumn(cfg, mainBlockWidth);

            DrawCustomTexts(cfg);
        }

        /// <summary>
        /// Draw the right-hand column next to the main timer stack. The first row
        /// is the finished-run total ("LastRun") when visible, and the second row
        /// (or the only row when no LastRun is available) is the current level's
        /// Wake Up time, gated by the "Show Wake Up Time" setting.
        /// </summary>
        private void DrawRightColumn(ConfigService cfg, float mainBlockWidth)
        {
            var state = TimerCore.State;
            if (state == null) return;

            // LastRun hides once a *new* run starts timing — but not during the
            // epilogue (the Credits level the game loads after the campaign
            // finishes), which belongs to the run that just ended.
            bool showLastRun = state.LastRun.HasValue
                && (state.InEpilogueSegment || (!state.InSegment && state.GameTime <= 0d));
            bool showWakeUp = cfg.Settings.ShowWakeUpTime && state.WakeUpTime.HasValue;
            if (!showLastRun && !showWakeUp)
                return;

            var layout = cfg.Layout;
            var loc = cfg.Localization;

            // Column gap next to the main block's widest line; top-aligned with it.
            const float gap = 24f;
            float x = layout.OffsetX + mainBlockWidth + gap;
            float y = layout.OffsetY;

            if (showLastRun)
            {
                string line = loc.Get("TIMER_LAST_RUN") + ":  " + TimeFormatter.Format(state.LastRun);
                DrawGradientLine(line, layout.ColorA, layout.ColorB, x, y, _rowStyle);
                y += _rowStyle.CalcSize(new GUIContent(line)).y + 2f;
            }

            if (showWakeUp)
            {
                string line = loc.Get("TIMER_WAKE_UP_TIME") + ":  " + TimeFormatter.FormatWakeUp(state.WakeUpTime);
                DrawGradientLine(line, layout.ColorA, layout.ColorB, x, y, _rowStyle);
            }
        }

        /// <summary>
        /// Draw the ordered rows + category extras + invalid banner at the
        /// anchor. Returns the widest row's width — used to place the LastRun
        /// column immediately to the right of the timer stack.
        /// </summary>
        private float DrawMainBlock(ConfigService cfg)
        {
            var state = TimerCore.State;
            if (state == null) return 0f;

            var layout = cfg.Layout;
            var loc = cfg.Localization;
            float x = layout.OffsetX;
            float y = layout.OffsetY;
            float widest = 0f;

            // Each configured row. LastRun is NOT drawn here — it renders in its
            // own right-hand column (see DrawRightColumn) so the finished-run
            // total doesn't sit in the same column as the live timers. RealTime
            // is a regular row type but is additionally gated by the ShowRealTime
            // settings toggle.
            bool hasRealTimeRow = layout.Rows.Contains(RowType.RealTime);
            foreach (var row in layout.Rows)
            {
                if (row == RowType.LastRun)
                    continue;
                if (row == RowType.RealTime && !cfg.Settings.ShowRealTime)
                    continue;
                string label, value;
                GetRow(row, cfg, state, loc, out label, out value);
                string line = label.Length > 0 ? (label + ":  " + value) : value;
                DrawGradientLine(line, layout.ColorA, layout.ColorB, x, y, _rowStyle);
                float w = _rowStyle.CalcSize(new GUIContent(line)).x;
                if (w > widest) widest = w;
                y += _rowStyle.CalcSize(new GUIContent(line)).y + 2f;
            }

            // Real-time clock fallback: when the setting is enabled but the user
            // hasn't placed the row in their layout, show it below the configured
            // rows so enabling the setting always has an immediate effect.
            if (cfg.Settings.ShowRealTime && !hasRealTimeRow)
            {
                string line = loc.Get("TIMER_REAL_TIME") + ":  " + TimeFormatter.Format(state.RealTime);
                DrawGradientLine(line, layout.ColorA, layout.ColorB, x, y, _rowStyle);
                float w = _rowStyle.CalcSize(new GUIContent(line)).x;
                if (w > widest) widest = w;
                y += _rowStyle.CalcSize(new GUIContent(line)).y + 2f;
            }

            // Current rule tags: one line right under the timer rows listing the
            // enabled tags (localized). Skipped when none are enabled. Rendered
            // before the tag extras and the invalid banner so the banner — the
            // most important line — always stays last.
            y += DrawTagsLine(cfg, loc, layout, x, y);

            // Voiceline / checkpoint extras (R3.3.3, R3.6.3) for the active category.
            y += DrawTagExtras(cfg, state, loc, x, y);

            // Invalid banner (R5.3.2).
            if (state.Flags.IsInvalid)
            {
                string reasons = state.Flags.FormatReasons(loc);
                string banner = loc.Get("INVALID_RUN") + ": " + reasons;
                var content = new GUIContent(banner);
                _bannerStyle.normal.textColor = Color.red;
                DrawGradientLine(banner, Color.red, new Color(1f, 0.4f, 0.4f, 1f), x, y, _bannerStyle);
                y += _bannerStyle.CalcSize(content).y + 2f;
            }

            return widest;
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

            // Voiceline tag: green hint when all triggered (R3.6.3).
            if (tags.HasTag(TagIds.Voiceline))
            {
                var rule = TagRuleRegistry.Instance != null ? TagRuleRegistry.Instance.Find(TagIds.Voiceline) as VoicelineTagRule : null;
                if (rule != null && rule.Tracker != null && rule.Tracker.AllBlocksTriggered && rule.Tracker.EasterSatisfied)
                {
                    string line = loc.Get("VOICELINE_ALL_DONE");
                    DrawGradientLine(line, new Color(0.3f, 1f, 0.4f, 1f), new Color(0.5f, 1f, 0.6f, 1f), x, y + added, _rowStyle);
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
                    value = TimeFormatter.Format(state.GameTime);
                    break;
                case RowType.RealTime:
                    label = loc.Get("TIMER_REAL_TIME");
                    value = TimeFormatter.Format(state.RealTime);
                    break;
                case RowType.CurrentSegment:
                    label = loc.Get("TIMER_SEGMENT_TIME");
                    value = TimeFormatter.Format(state.GameTime - state.SegmentStart);
                    break;
                case RowType.TotalAtLastSegment:
                    label = loc.Get("TIMER_LAST_TOTAL");
                    value = TimeFormatter.Format(state.TotalAtLastSegment);
                    break;
                case RowType.LastSegment:
                    label = loc.Get("TIMER_LAST_SEGMENT");
                    value = TimeFormatter.Format(state.LastSegment);
                    break;
                case RowType.LastRun:
                    label = loc.Get("TIMER_LAST_RUN");
                    value = TimeFormatter.Format(state.LastRun);
                    break;
                case RowType.CurrentState:
                    label = loc.Get("TIMER_CURRENT_STATE");
                    value = loc.Get(StateKey(state.Game != null ? state.Game.state : GameState.Inactive));
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

        private void DrawCustomTexts(ConfigService cfg)
        {
            if (cfg == null || cfg.Layout.CustomTexts.Count == 0) return;
            var state = TimerCore.State;
            double gt = state != null ? state.GameTime : 0d;
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
            }
            if (_rowStyle.fontSize != size) _rowStyle.fontSize = size;
            // The invalid banner matches the timer rows' size (the fixed smaller
            // size made the warning look like a footnote next to the timer).
            if (_bannerStyle.fontSize != size) _bannerStyle.fontSize = size;
        }
    }
}
