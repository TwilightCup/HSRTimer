using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>A timer-panel row type (R2.1.1).</summary>
    public enum RowType
    {
        GameTime,
        RealTime,
        CurrentSegment,
        TotalAtLastSegment,
        LastSegment,
        LastRun,
        CurrentState,
    }

    /// <summary>An arbitrary custom text the user can place anywhere (R2.4).</summary>
    public sealed class CustomText
    {
        public float X;
        public float Y;
        public string Text = "";
        public Color ColorA = Color.white;
        public Color ColorB = Color.white;
    }

    /// <summary>
    /// The editable HUD layout: ordered rows of text drawn directly on screen
    /// (no window/chrome), the anchor offset and font size for the main block,
    /// and the default two-color gradient. Persisted to layout.ini. Colors are
    /// stored as hex (with optional alpha) so the file is human-editable; parsed
    /// by <see cref="GradientText"/>.
    /// </summary>
    public sealed class LayoutModel
    {
        /// <summary>
        /// The canonical default row order. This is the single source of truth
        /// shared by the <see cref="Rows"/> field initializer and
        /// <see cref="ConfigRepair"/>: adding a new default row (or reordering
        /// the defaults) is a one-line edit here, and both the fresh-install
        /// layout and the config-repair target stay in sync automatically.
        /// </summary>
        public static readonly RowType[] DefaultRows =
        {
            RowType.GameTime,
            RowType.RealTime,
            RowType.CurrentSegment,
            RowType.TotalAtLastSegment,
            RowType.LastSegment,
        };

        public readonly List<RowType> Rows = new List<RowType>(DefaultRows);

        public readonly List<CustomText> CustomTexts = new List<CustomText>();

        /// <summary>Screen offset (pixels) of the main text block from the top-left.</summary>
        public float OffsetX = 16f;

        /// <summary>Screen offset (pixels) of the main text block from the top.</summary>
        public float OffsetY = 16f;

        /// <summary>Font size of the main text block (also drives the dynamic font).</summary>
        public int FontSize = 18;

        /// <summary>Default gradient colors applied to rows without their own colors.</summary>
        public Color ColorA = GradientText.ParseColor("FF5272FF", Color.white);

        public Color ColorB = GradientText.ParseColor("FF9A72FF", Color.white);

        // ── Leaderboard HUD (shared by Subsegment and Markers modes) ──
        // These live in layout.ini [leaderboard]; settings.ini no longer stores
        // them. OffsetY is relative to the fixed top anchor at the screen center.
        public int LeaderboardFontSize = 16;
        public float LeaderboardOffsetX = 16f;
        public float LeaderboardOffsetY = 0f;
        public Color LeaderboardColorFaster = GradientText.ParseColor("59FF66FF", new Color(0.35f, 1f, 0.4f, 1f));
        public Color LeaderboardColorSlower = GradientText.ParseColor("FF5959FF", new Color(1f, 0.35f, 0.35f, 1f));
        public Color LeaderboardColorTie = Color.white;
        public string LeaderboardMode = "Subsegment";
        public string LeaderboardMarkersTimeMode = "Relative";

        public void Load()
        {
            CustomTexts.Clear();
            var rowsByKey = new Dictionary<int, RowType>();
            var tmpTexts = new Dictionary<int, CustomText>();
            foreach (var p in PersistenceService.Read(PersistenceService.PathFor("layout.ini")))
            {
                if (p.Section == "text")
                {
                    switch (p.Key)
                    {
                        case "offset_x": OffsetX = ParseFloat(p.Value, OffsetX); break;
                        case "offset_y": OffsetY = ParseFloat(p.Value, OffsetY); break;
                        case "font_size": FontSize = ParseInt(p.Value, FontSize); break;
                        case "color_a": ColorA = GradientText.ParseColor(p.Value, ColorA); break;
                        case "color_b": ColorB = GradientText.ParseColor(p.Value, ColorB); break;
                    }
                }
                else if (p.Section == "rows")
                {
                    int idx;
                    if (int.TryParse(p.Key, out idx) && System.Enum.TryParse(p.Value, true, out RowType rt))
                        rowsByKey[idx] = rt;
                }
                else if (p.Section == "leaderboard")
                {
                    switch (p.Key)
                    {
                        case "font_size": LeaderboardFontSize = ParseInt(p.Value, LeaderboardFontSize); break;
                        case "offset_x": LeaderboardOffsetX = ParseFloat(p.Value, LeaderboardOffsetX); break;
                        case "offset_y": LeaderboardOffsetY = ParseFloat(p.Value, LeaderboardOffsetY); break;
                        case "color_faster": LeaderboardColorFaster = GradientText.ParseColor(p.Value, LeaderboardColorFaster); break;
                        case "color_slower": LeaderboardColorSlower = GradientText.ParseColor(p.Value, LeaderboardColorSlower); break;
                        case "color_tie": LeaderboardColorTie = GradientText.ParseColor(p.Value, LeaderboardColorTie); break;
                        case "mode": LeaderboardMode = p.Value; break;
                        case "markers_time_mode": LeaderboardMarkersTimeMode = p.Value; break;
                    }
                }
                else if (p.Section.StartsWith("custom."))
                {
                    int idx;
                    if (!int.TryParse(p.Section.Substring(7), out idx)) continue;
                    CustomText ct;
                    if (!tmpTexts.TryGetValue(idx, out ct))
                    {
                        ct = new CustomText();
                        tmpTexts[idx] = ct;
                    }
                    switch (p.Key)
                    {
                        case "x": ct.X = ParseFloat(p.Value, ct.X); break;
                        case "y": ct.Y = ParseFloat(p.Value, ct.Y); break;
                        case "text": ct.Text = UnescapeBackslashN(p.Value); break;
                        case "color_a": ct.ColorA = GradientText.ParseColor(p.Value, ct.ColorA); break;
                        case "color_b": ct.ColorB = GradientText.ParseColor(p.Value, ct.ColorB); break;
                    }
                }
            }

            if (rowsByKey.Count > 0)
            {
                Rows.Clear();
                var ordered = new List<int>(rowsByKey.Keys);
                ordered.Sort();
                foreach (var idx in ordered) Rows.Add(rowsByKey[idx]);
            }

            if (tmpTexts.Count > 0)
            {
                var ordered = new List<int>(tmpTexts.Keys);
                ordered.Sort();
                foreach (var idx in ordered) CustomTexts.Add(tmpTexts[idx]);
            }
        }

        public void Save() => SaveTo(PersistenceService.PathFor("layout.ini"));

        /// <summary>Serialize this layout to a specific file (live layout.ini or a preset snapshot).</summary>
        public void SaveTo(string path)
        {
            var sections = new List<KeyValuePair<string, IDictionary<string, string>>>();

            var text = new Dictionary<string, string>
            {
                ["offset_x"] = OffsetX.ToString("F0", CultureInfo.InvariantCulture),
                ["offset_y"] = OffsetY.ToString("F0", CultureInfo.InvariantCulture),
                ["font_size"] = FontSize.ToString(CultureInfo.InvariantCulture),
                ["color_a"] = GradientText.ToHex(ColorA),
                ["color_b"] = GradientText.ToHex(ColorB),
            };
            sections.Add(new KeyValuePair<string, IDictionary<string, string>>("text", text));

            var rows = new Dictionary<string, string>();
            for (int i = 0; i < Rows.Count; i++)
                rows[i.ToString()] = Rows[i].ToString();
            sections.Add(new KeyValuePair<string, IDictionary<string, string>>("rows", rows));

            var leaderboard = new Dictionary<string, string>
            {
                ["font_size"] = LeaderboardFontSize.ToString(CultureInfo.InvariantCulture),
                ["offset_x"] = LeaderboardOffsetX.ToString("0.###", CultureInfo.InvariantCulture),
                ["offset_y"] = LeaderboardOffsetY.ToString("0.###", CultureInfo.InvariantCulture),
                ["color_faster"] = GradientText.ToHex(LeaderboardColorFaster),
                ["color_slower"] = GradientText.ToHex(LeaderboardColorSlower),
                ["color_tie"] = GradientText.ToHex(LeaderboardColorTie),
                ["mode"] = LeaderboardMode,
                ["markers_time_mode"] = LeaderboardMarkersTimeMode,
            };
            sections.Add(new KeyValuePair<string, IDictionary<string, string>>("leaderboard", leaderboard));

            for (int i = 0; i < CustomTexts.Count; i++)
            {
                var ct = CustomTexts[i];
                sections.Add(new KeyValuePair<string, IDictionary<string, string>>("custom." + i, new Dictionary<string, string>
                {
                    ["x"] = ct.X.ToString("F0", CultureInfo.InvariantCulture),
                    ["y"] = ct.Y.ToString("F0", CultureInfo.InvariantCulture),
                    ["text"] = EscapeBackslashN(ct.Text),
                    ["color_a"] = GradientText.ToHex(ct.ColorA),
                    ["color_b"] = GradientText.ToHex(ct.ColorB),
                }));
            }

            PersistenceService.Write(
                path,
                sections,
                "HSRTimer HUD layout. Text is drawn directly on screen (no window).\n# [text] offset_x/offset_y (top-left px), font_size, color_a/color_b;\n# [rows] ordered row types; [leaderboard] shared leaderboard HUD appearance;\n# [custom.<n>] arbitrary on-screen texts (template vars).");
        }

        // ── helpers ──
        private static float ParseFloat(string s, float fallback)
        {
            float v;
            return float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        private static int ParseInt(string s, int fallback)
        {
            int v;
            return int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out v) ? v : fallback;
        }

        internal static string EscapeBackslashN(string s)
            => (s ?? "").Replace("\\", "\\\\").Replace("\n", "\\n");

        internal static string UnescapeBackslashN(string s)
            => (s ?? "").Replace("\\n", "\n").Replace("\\\\", "\\");
    }
}
