using System;
using System.Collections.Generic;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// The shared IMGUI leaderboard HUD (left side of the screen). It shows
    /// either the subsegment reference leaderboard (R8.5) or the markers
    /// trigger feed (R10.7), chosen by <c>LayoutModel.LeaderboardMode</c>.
    /// The top edge is fixed at the screen center (plus the configured Y
    /// offset) and rows extend downward as content grows. Appearance (font
    /// size, offsets, entry colors) comes from the layout model and applies
    /// to both modes; the mode-cycle key (<c>SubsegmentToggleKey</c>) lives
    /// here and cycles: hidden → Subsegment → Markers → hidden.
    ///
    /// A content mode whose module is currently unavailable — user-disabled in
    /// settings or auto-disabled by another mechanism (e.g. the co-op client
    /// gate, R8.9) — is left out of the rotation: e.g. with subsegment
    /// disabled the cycle is only hidden ↔ Markers (R8.5.1.2).
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

        /// <summary>
        /// Content modes currently available for the cycle, in canonical order
        /// (Subsegment first, then Markers), filtered to those that are
        /// user-enabled and not auto-disabled (R8.5.1.2).
        /// </summary>
        public static List<string> AvailableModes()
        {
            var modes = new List<string>(2);
            var sub = SubsegmentManager.Instance;
            if (sub != null && sub.IsLeaderboardAvailable)
                modes.Add("Subsegment");
            var markers = MarkersManager.Instance;
            if (markers != null && markers.IsLeaderboardAvailable)
                modes.Add("Markers");
            return modes;
        }

        private static int IndexOfMode(List<string> modes, string mode)
        {
            for (int i = 0; i < modes.Count; i++)
                if (string.Equals(modes[i], mode, StringComparison.OrdinalIgnoreCase))
                    return i;
            return -1;
        }

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

        /// <summary>
        /// Cycle the leaderboard through hidden → (available content modes) →
        /// hidden. A mode whose module is disabled — by the user's settings or
        /// by an auto-disable mechanism — is skipped: the rotation only
        /// contains modes that are currently available (R8.5.1.2). When
        /// turning it back on from hidden, it starts in the first available
        /// mode (Subsegment preferred); while visible it switches between the
        /// available content modes and hides after the last one. If no content
        /// mode is available at all, the board stays hidden.
        /// </summary>
        public void CycleMode()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;

            var modes = AvailableModes();

            if (!_visible)
            {
                if (modes.Count == 0)
                {
                    Plugin.Logger.LogInfo("HSRTimer: leaderboard cycle: no content mode available (subsegment and markers both disabled); staying hidden.");
                    return;
                }
                _visible = true;
                cfg.Layout.LeaderboardMode = modes[0];
                Plugin.Logger.LogInfo($"HSRTimer: leaderboard shown in {modes[0]} mode.");
                return;
            }

            string current = cfg.Layout.LeaderboardMode;
            int idx = IndexOfMode(modes, current);
            if (idx < 0 || idx == modes.Count - 1)
            {
                // Either the current mode is no longer available (disabled or
                // unknown, so it sits outside the rotation) or it is the last
                // available mode: the next press hides the board.
                _visible = false;
                Plugin.Logger.LogInfo("HSRTimer: leaderboard hidden.");
            }
            else
            {
                cfg.Layout.LeaderboardMode = modes[idx + 1];
                Plugin.Logger.LogInfo($"HSRTimer: leaderboard switched to {modes[idx + 1]} mode.");
            }
        }

        /// <summary>
        /// Show or hide the leaderboard without toggling if it is already in the
        /// requested state. Used by the in-game dev console.
        /// </summary>
        public void SetVisible(bool visible)
        {
            if (_visible == visible)
                return;
            _visible = visible;
            if (_visible && ConfigService.Instance != null
                && string.IsNullOrEmpty(ConfigService.Instance.Layout.LeaderboardMode))
            {
                var modes = AvailableModes();
                if (modes.Count > 0)
                    ConfigService.Instance.Layout.LeaderboardMode = modes[0];
            }
        }

        /// <summary>
        /// Switch the leaderboard to a content mode ("Subsegment" or "Markers")
        /// and make sure it is visible. Used by the in-game dev console.
        /// </summary>
        public void SetMode(string mode)
        {
            if (string.IsNullOrEmpty(mode))
                return;
            var cfg = ConfigService.Instance;
            if (cfg != null)
            {
                if (string.Equals(mode, "Markers", StringComparison.OrdinalIgnoreCase))
                    cfg.Layout.LeaderboardMode = "Markers";
                else if (string.Equals(mode, "Subsegment", StringComparison.OrdinalIgnoreCase))
                    cfg.Layout.LeaderboardMode = "Subsegment";
            }
            _visible = true;
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

            bool markersMode = string.Equals(cfg.Layout.LeaderboardMode, "Markers", System.StringComparison.OrdinalIgnoreCase);
            if (markersMode)
                DrawMarkers(cfg);
            else
                DrawSubsegment(cfg);
        }

        // ── Subsegment mode (R8.5) ─────────────────────────────────────────

        private void DrawSubsegment(ConfigService cfg)
        {
            var mgr = SubsegmentManager.Instance;
            // Gated on full availability (user-enabled AND not auto-disabled):
            // when subsegment is off — by settings or by a mechanism like the
            // co-op client gate — the mode draws nothing (R8.5.1.2).
            if (mgr == null || !mgr.IsLeaderboardAvailable) return;
            var state = TimerCore.State;
            if (state == null) return;
            // During a level transition (LoadingLevel between levels) keep the
            // previous leaderboard on screen until the next level's first
            // settled diff refreshes it (R8.5.6.4).
            bool inPlayableSegment = state.InSegment;
            bool inMultiTransition = mgr.InMultiRunActive && state.PlayableTicks > 0UL;
            bool inPreservedTransition = mgr.InPreservedTransition && state.PlayableTicks > 0UL;
            if (!inPlayableSegment && !inMultiTransition && !inPreservedTransition) return;

            var entries = mgr.Entries;
            if (entries.Count == 0) return;

            var layout = cfg.Layout;
            int hudSize = layout.LeaderboardFontSize;
            EnsureFont(hudSize);
            ApplyFont();
            _rowStyle.fontSize = hudSize;

            float lineHeight = _rowStyle.CalcSize(new GUIContent("Wg")).y + 2f;
            float x = layout.LeaderboardOffsetX;
            // Fixed top anchor: the title/rows always start here and extend
            // downward, so the top edge does not move as entries change.
            float y = Screen.height * 0.5f + layout.LeaderboardOffsetY;

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
                    color = layout.LeaderboardColorTie;
                else if (entry.DiffMs.Value < 0)
                    color = layout.LeaderboardColorFaster;
                else
                    color = layout.LeaderboardColorSlower;
                DrawLine(line, color, x, y);
                y += lineHeight;
            }
        }

        // ── Markers mode (R10.7) ───────────────────────────────────────────

        private void DrawMarkers(ConfigService cfg)
        {
            var mgr = MarkersManager.Instance;
            if (mgr == null || !mgr.HasFeedData || !mgr.IsLeaderboardAvailable) return;
            var layout = cfg.Layout;
            var state = TimerCore.State;
            if (state == null) return;

            // R10.7.6: keep the previous feed on screen through a level
            // transition; hide it once a run has reset (game clock back to 0).
            if (!state.InSegment && state.PlayableTicks == 0UL) return;

            var feed = mgr.Feed;
            if (feed == null || feed.Count == 0) return;

            bool relative = string.Equals(layout.LeaderboardMarkersTimeMode, "Relative", System.StringComparison.OrdinalIgnoreCase);

            int hudSize = layout.LeaderboardFontSize;
            EnsureFont(hudSize);
            ApplyFont();
            _rowStyle.fontSize = hudSize;

            string title = mgr.LeaderboardTitle;
            bool hasTitle = !string.IsNullOrEmpty(title);
            float lineHeight = _rowStyle.CalcSize(new GUIContent("Wg")).y + 2f;
            float x = layout.LeaderboardOffsetX;
            // Fixed top anchor: the title/rows always start here and extend
            // downward, so adding a new feed row does not move the top edge.
            float y = Screen.height * 0.5f + layout.LeaderboardOffsetY;

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
                        color = ToneColor(layout, diff);
                    }
                    else
                    {
                        value = "--";
                        color = layout.LeaderboardColorTie;
                    }
                }
                else
                {
                    // Absolute segment time (R10.7.3); the color still reflects
                    // ahead/behind vs PB (R10.7.5).
                    value = TimeFormatter.Format(row.TMs / 1000.0);
                    color = pb.HasValue ? ToneColor(layout, diff) : layout.LeaderboardColorTie;
                }

                DrawLine(row.Name + ": " + value, color, x, y);
                y += lineHeight;
            }
        }

        private static Color ToneColor(LayoutModel layout, long diff)
        {
            if (diff < 0) return layout.LeaderboardColorFaster;
            if (diff > 0) return layout.LeaderboardColorSlower;
            return layout.LeaderboardColorTie;
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
