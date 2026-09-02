using System.Globalization;
using System.Text;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// Independent IMGUI leaderboard HUD for the Subsegment module (R8.5). It
    /// renders in the left-middle of the screen, separately from the main
    /// timer panel, and only while a subsegment reference set is loaded.
    /// </summary>
    public class SubsegmentHud : MonoBehaviour
    {
        private GUIStyle _rowStyle;
        private Font _font;
        private int _appliedFontSize = -1;

        private void Awake()
        {
            _rowStyle = new GUIStyle
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft,
                normal = { textColor = Color.white },
            };
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
                Plugin.Logger.LogWarning($"HSRTimer: subsegment dynamic font creation failed: {ex.Message}");
                _font = null;
            }
        }

        private void OnGUI()
        {
            var mgr = SubsegmentManager.Instance;
            if (mgr == null || !mgr.Visible) return;
            if (ConfigService.Instance == null || !ConfigService.Instance.Settings.SubsegmentEnable) return;
            var state = TimerCore.State;
            if (state == null) return;
            // During a multi-run level transition (LoadingLevel between levels)
            // keep the previous leaderboard on screen until the next level's
            // first settled diff refreshes it.
            bool inPlayableSegment = state.InSegment;
            bool inMultiTransition = mgr.InMultiRunActive && state.GameTime > 0d;
            if (!inPlayableSegment && !inMultiTransition) return;

            var entries = mgr.Entries;
            if (entries.Count == 0) return;

            int hudSize = mgr.Options.HudFontSize;
            EnsureFont(hudSize);
            ApplyFont();
            _rowStyle.fontSize = hudSize;

            int size = entries.Count;
            float lineHeight = _rowStyle.CalcSize(new GUIContent("Wg")).y + 2f;
            float x = mgr.Options.HudOffsetX;
            // Default behavior: vertically centered; OffsetY lets the user nudge
            // the whole block independently of the main timer HUD.
            float y = Screen.height * 0.5f - size * lineHeight * 0.5f + mgr.Options.HudOffsetY;

            foreach (var entry in entries)
            {
                string line = entry.DisplayId + "  " + FormatDiff(entry.DiffMs);
                Color color;
                if (!entry.DiffMs.HasValue)
                    color = Color.white;
                else if (entry.DiffMs.Value < 0)
                    color = new Color(0.35f, 1f, 0.4f, 1f);
                else if (entry.DiffMs.Value > 0)
                    color = new Color(1f, 0.35f, 0.35f, 1f);
                else
                    color = Color.white;
                DrawLine(line, color, x, y);
                y += lineHeight;
            }
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
            GUI.Label(new Rect(x, y, 400f, 24f), text, _rowStyle);
        }

        /// <summary>
        /// Format a settled diff_ms as <c>+MM:SS.mmm</c> / <c>-MM:SS.mmm</c>.
        /// Null renders as <c>--</c>. The sign prefix is the conventional HSRTimer
        /// delta sign and the magnitude is an absolute duration.
        /// </summary>
        public static string FormatDiff(long? diffMs)
        {
            if (!diffMs.HasValue || diffMs.Value == 0)
                return "--";
            long d = diffMs.Value;
            char sign = d < 0 ? '-' : d > 0 ? '+' : ' ';
            long abs = d < 0 ? -d : d;
            long totalSeconds = abs / 1000L;
            long ms = abs % 1000L;
            long minutes = totalSeconds / 60L;
            long seconds = totalSeconds % 60L;
            var sb = new StringBuilder();
            if (sign != ' ')
                sb.Append(sign);
            else
                sb.Append(' ');
            sb.Append(minutes.ToString("D2", CultureInfo.InvariantCulture));
            sb.Append(':');
            sb.Append(seconds.ToString("D2", CultureInfo.InvariantCulture));
            sb.Append('.');
            sb.Append(ms.ToString("D3", CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
