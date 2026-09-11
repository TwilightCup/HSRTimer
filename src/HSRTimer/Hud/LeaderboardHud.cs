using System.Collections.Generic;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// The shared IMGUI leaderboard HUD (left-middle of the screen). It shows
    /// either the subsegment reference leaderboard (R8.5) or the markers
    /// trigger feed (R10.7), chosen by <c>Settings.SubsegmentLeaderboardMode</c>.
    /// Appearance (font size, offsets, entry colors) comes from the settings
    /// model and applies to both modes; the display/hide toggle key
    /// (<c>SubsegmentToggleKey</c>) lives here.
    /// </summary>
    public sealed class LeaderboardHud : MonoBehaviour
    {
        public static LeaderboardHud Instance { get; private set; }

        private bool _visible = true;
        private GUIStyle _rowStyle;
        private Font _font;
        private int _appliedFontSize = -1;

        /// <summary>Whether the leaderboard is currently shown (R8.5.1.2).</summary>
        public bool Visible => _visible;

        private void Awake()
        {
            Instance = this;
            _rowStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white },
            };
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Flip the leaderboard show/hide flag (bound to the toggle key).</summary>
        public void ToggleVisible()
        {
            _visible = !_visible;
            Plugin.Logger.LogInfo(_visible ? "HSRTimer: leaderboard shown." : "HSRTimer: leaderboard hidden.");
        }

        private void EnsureFont(int size)
        {
            if (size <= 0) size = 16;
            if (_font != null && _appliedFontSize == size) return;
            try
            {
                _font = Font.CreateDynamicFontFromOSFont(new[]
                {
                    "PingFang SC", "Microsoft YaHei", "Noto Sans CJK SC",
                    "Noto Sans CJK", "Heiti SC", "Arial Unicode MS", "Arial",
                }, size);
                _appliedFontSize = size;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: leaderboard dynamic font creation failed: {ex.Message}");
                _font = null;
            }
        }

        private void OnGUI()
        {
            if (!_visible) return;
            var cfg = ConfigService.Instance;
            if (cfg == null) return;

            bool markersMode = string.Equals(cfg.Settings.SubsegmentLeaderboardMode, "Markers", System.StringComparison.OrdinalIgnoreCase);
            if (markersMode)
                DrawMarkers(cfg);
            else
                DrawSubsegment(cfg);
        }

        // ── Subsegment mode (R8.5) ─────────────────────────────────────────

        private void DrawSubsegment(ConfigService cfg)
        {
            var mgr = SubsegmentManager.Instance;
            if (mgr == null || !cfg.Settings.SubsegmentEnable) return;
            var state = TimerCore.State;
            if (state == null) return;
            // During a level transition (LoadingLevel between levels) keep the
            // previous leaderboard on screen until the next level's first
            // settled diff refreshes it (R8.5.6.4).
            bool inPlayableSegment = state.InSegment;
            bool inMultiTransition = mgr.InMultiRunActive && state.GameTime > 0d;
            bool inPreservedTransition = mgr.InPreservedTransition && state.GameTime > 0d;
            if (!inPlayableSegment && !inMultiTransition && !inPreservedTransition) return;

            var entries = mgr.Entries;
            if (entries.Count == 0) return;

            var settings = cfg.Settings;
            int hudSize = settings.SubsegmentHudFontSize;
            EnsureFont(hudSize);
            ApplyFont();
            _rowStyle.fontSize = hudSize;

            int size = entries.Count;
            float lineHeight = _rowStyle.CalcSize(new GUIContent("Wg")).y + 2f;
            float x = settings.SubsegmentHudOffsetX;
            float y = Screen.height * 0.5f - (size + 1) * lineHeight * 0.5f + settings.SubsegmentHudOffsetY;

            string title = mgr.LeaderboardTitle;
            if (!string.IsNullOrEmpty(title))
            {
                DrawLine(title, Color.white, x, y);
                y += lineHeight;
            }

            foreach (var entry in entries)
            {
                string line = entry.DisplayId + "  " + TimeFormatter.FormatSignedDiff(entry.DiffMs);
                Color color;
                if (!entry.DiffMs.HasValue || entry.DiffMs.Value == 0)
                    color = settings.SubsegmentHudColorTie;
                else if (entry.DiffMs.Value < 0)
                    color = settings.SubsegmentHudColorFaster;
                else
                    color = settings.SubsegmentHudColorSlower;
                DrawLine(line, color, x, y);
                y += lineHeight;
            }
        }

        // ── Markers mode (R10.7) ───────────────────────────────────────────

        private void DrawMarkers(ConfigService cfg)
        {
            var mgr = MarkersManager.Instance;
            if (mgr == null || !mgr.HasFeedData) return;
            var settings = cfg.Settings;
            if (settings == null || !settings.MarkersEnable) return;
            var state = TimerCore.State;
            if (state == null) return;

            // R10.7.6: keep the previous feed on screen through a level
            // transition; hide it once a run has reset (GameTime back to 0).
            if (!state.InSegment && state.GameTime <= 0d) return;

            var feed = mgr.Feed;
            if (feed == null || feed.Count == 0) return;

            bool relative = string.Equals(settings.MarkersLeaderboardTimeMode, "Relative", System.StringComparison.OrdinalIgnoreCase);

            int hudSize = settings.SubsegmentHudFontSize;
            EnsureFont(hudSize);
            ApplyFont();
            _rowStyle.fontSize = hudSize;

            string title = mgr.LeaderboardTitle;
            bool hasTitle = !string.IsNullOrEmpty(title);
            int size = feed.Count;
            float lineHeight = _rowStyle.CalcSize(new GUIContent("Wg")).y + 2f;
            float x = settings.SubsegmentHudOffsetX;
            float y = Screen.height * 0.5f - (size + (hasTitle ? 1 : 0)) * lineHeight * 0.5f + settings.SubsegmentHudOffsetY;

            if (hasTitle)
            {
                DrawLine(title, Color.white, x, y);
                y += lineHeight;
            }

            // Newest trigger on top (R10.7.4): the feed is stored oldest-first.
            for (int i = feed.Count - 1; i >= 0; i--)
            {
                var row = feed[i];
                if (row == null) continue;
                long? pb = mgr.PbTimeOf(row.MarkerId);
                long diff = pb.HasValue ? row.TMs - pb.Value : 0L;

                string value;
                Color color;
                if (relative)
                {
                    if (pb.HasValue)
                    {
                        value = TimeFormatter.FormatSignedDiff(diff);
                        color = ToneColor(settings, diff);
                    }
                    else
                    {
                        value = "--";
                        color = settings.SubsegmentHudColorTie;
                    }
                }
                else
                {
                    // Absolute segment time (R10.7.3); the color still reflects
                    // ahead/behind vs PB (R10.7.5).
                    value = TimeFormatter.Format(row.TMs / 1000.0);
                    color = pb.HasValue ? ToneColor(settings, diff) : settings.SubsegmentHudColorTie;
                }

                DrawLine(row.Name + ": " + value, color, x, y);
                y += lineHeight;
            }
        }

        private static Color ToneColor(SettingsModel s, long diff)
        {
            if (diff < 0) return s.SubsegmentHudColorFaster;
            if (diff > 0) return s.SubsegmentHudColorSlower;
            return s.SubsegmentHudColorTie;
        }

        private void ApplyFont()
        {
            if (_font == null) return;
            _rowStyle.font = _font;
            _rowStyle.fontSize = _appliedFontSize > 0 ? _appliedFontSize : 16;
        }

        private void DrawLine(string text, Color color, float x, float y)
        {
            _rowStyle.normal.textColor = color;
            GUI.Label(new Rect(x, y, 600f, 28f), text, _rowStyle);
        }
    }
}
