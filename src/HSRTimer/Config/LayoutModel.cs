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
        PrevRt,
        CurrentSegment,
        TotalAtLastSegment,
        LastSegment,
        LastRun,
        CurrentState,
        WakeUpTime,
    }

    /// <summary>An arbitrary custom text the user can place anywhere (R2.4).</summary>
    public sealed class CustomText
    {
        public float X;
        public float Y;
        public string Text = "";
        public Color ColorA = Color.white;
        public Color ColorB = Color.white;
        public int FontSize = 16;
    }

    /// <summary>
    /// The editable HUD layout: ordered columns of text drawn directly on screen
    /// (no window/chrome), the anchor offset and font size for the main block,
    /// and the default two-color gradient. Persisted to layout.ini. Colors are
    /// stored as hex (with optional alpha) so the file is human-editable; parsed
    /// by <see cref="GradientText"/>.
    /// </summary>
    public sealed class LayoutModel
    {
        /// <summary>
        /// The canonical default layout: one ordered row list per column
        /// (1-based column index). Written to disk exactly once, when the
        /// config is initialized on a fresh install (see
        /// <see cref="ConfigService.Load"/>); existing files are never
        /// auto-modified. Column 1 is the leftmost timer stack; RealTime
        /// defaults into column 2 with PrevRt (the previous level's Real Time
        /// snapshot) directly below it; column 3 holds the LastRun /
        /// WakeUpTime rows (the former "right-hand column").
        /// </summary>
        public static readonly Dictionary<int, RowType[]> DefaultColumns = new Dictionary<int, RowType[]>
        {
            { 1, new[] { RowType.GameTime, RowType.CurrentSegment, RowType.TotalAtLastSegment, RowType.LastSegment } },
            { 2, new[] { RowType.RealTime, RowType.PrevRt } },
            { 3, new[] { RowType.LastRun, RowType.WakeUpTime } },
        };

        /// <summary>
        /// The HUD layout: per column (1-based column number) a map from
        /// **row position** (1-based) to row type. Positions set the
        /// top-to-bottom draw order within the column; columns are drawn
        /// left-to-right by number. A column with no entries is not displayed,
        /// and position 0 is never valid (0 means "not in this column").
        /// </summary>
        public readonly Dictionary<int, Dictionary<int, RowType>> Columns = CreateDefaultColumns();

        public readonly List<CustomText> CustomTexts = new List<CustomText>();

        private static Dictionary<int, Dictionary<int, RowType>> CreateDefaultColumns()
        {
            var d = new Dictionary<int, Dictionary<int, RowType>>();
            foreach (var kv in DefaultColumns)
            {
                var rows = new Dictionary<int, RowType>();
                for (int i = 0; i < kv.Value.Length; i++)
                    rows[i + 1] = kv.Value[i];
                d[kv.Key] = rows;
            }
            return d;
        }

        /// <summary>True if <paramref name="row"/> appears at any position in any column.</summary>
        public bool HasRow(RowType row)
        {
            foreach (var col in Columns)
                foreach (var pos in col.Value)
                    if (pos.Value == row)
                        return true;
            return false;
        }

        /// <summary>The position of <paramref name="row"/> in a column, or 0 when absent.</summary>
        public static int PositionOf(Dictionary<int, RowType> rows, RowType row)
        {
            if (rows == null) return 0;
            foreach (var kv in rows)
                if (kv.Value == row)
                    return kv.Key;
            return 0;
        }

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
            var path = PersistenceService.PathFor("layout.ini");
            bool fileExists = System.IO.File.Exists(path);
            var columnsByKey = new Dictionary<int, Dictionary<int, RowType>>(); // column → (row index → row type)
            var legacyRowsByKey = new Dictionary<int, RowType>();
            var tmpTexts = new Dictionary<int, CustomText>();
            foreach (var p in PersistenceService.Read(path))
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
                    // Legacy v1 [rows] section, read for compatibility only:
                    // old configs keep loading; the list is mapped to
                    // [column.1] in-memory below and rewritten in the new
                    // [column.N] format on the next save. No boot-time repair.
                    int idx;
                    if (int.TryParse(p.Key, out idx) && System.Enum.TryParse(p.Value, true, out RowType rt))
                        legacyRowsByKey[idx] = rt;
                }
                else if (p.Section.StartsWith("column."))
                {
                    int col;
                    if (!int.TryParse(p.Section.Substring(7), out col) || col < 1) continue;
                    int pos;
                    if (!int.TryParse(p.Key, out pos) || pos < 1 || !System.Enum.TryParse(p.Value, true, out RowType rt)) continue;
                    Dictionary<int, RowType> rows;
                    if (!columnsByKey.TryGetValue(col, out rows))
                    {
                        rows = new Dictionary<int, RowType>();
                        columnsByKey[col] = rows;
                    }
                    rows[pos] = rt;
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
                        case "font_size": ct.FontSize = ParseInt(p.Value, ct.FontSize); break;
                        case "color_a": ct.ColorA = GradientText.ParseColor(p.Value, ct.ColorA); break;
                        case "color_b": ct.ColorB = GradientText.ParseColor(p.Value, ct.ColorB); break;
                    }
                }
            }

            if (columnsByKey.Count > 0)
            {
                Columns.Clear();
                foreach (var kv in columnsByKey)
                    Columns[kv.Key] = kv.Value;
            }
            else if (legacyRowsByKey.Count > 0)
            {
                // Migration (in-memory only): the old single [rows] list
                // becomes [column.1], renumbered to 1-based positions. The
                // screen keeps showing exactly what it showed before; nothing
                // is moved and no empty column is added.
                Columns.Clear();
                var rows = new Dictionary<int, RowType>();
                var ordered = new List<int>(legacyRowsByKey.Keys);
                ordered.Sort();
                for (int i = 0; i < ordered.Count; i++)
                    rows[i + 1] = legacyRowsByKey[ordered[i]];
                Columns[1] = rows;
            }
            else if (fileExists)
            {
                // The file exists but contains no row sections — the user has
                // deliberately emptied the layout (e.g. deleted every column in
                // the panel). Keep it empty; do not resurrect the defaults.
                Columns.Clear();
            }
            // else: no layout file yet — keep the default columns; the first
            // run seeds them to disk via ConfigService.Load.

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

            // Write exactly the configured columns; no implicit empty columns.
            // Section keys are 1-based row positions (0 is never written).
            var colIndices = new List<int>(Columns.Keys);
            colIndices.Sort();
            foreach (var col in colIndices)
            {
                var positions = new List<int>(Columns[col].Keys);
                positions.Sort();
                var rows = new Dictionary<string, string>();
                foreach (var pos in positions)
                    rows[pos.ToString(CultureInfo.InvariantCulture)] = Columns[col][pos].ToString();
                sections.Add(new KeyValuePair<string, IDictionary<string, string>>("column." + col, rows));
            }

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
                    ["font_size"] = ct.FontSize.ToString(CultureInfo.InvariantCulture),
                    ["color_a"] = GradientText.ToHex(ct.ColorA),
                    ["color_b"] = GradientText.ToHex(ct.ColorB),
                }));
            }

            PersistenceService.Write(
                path,
                sections,
                "HSRTimer HUD layout. Text is drawn directly on screen (no window).\n# [text] offset_x/offset_y (top-left px), font_size, color_a/color_b;\n# [column.<n>] row types per column: each key is a 1-based row position (drawn top-to-bottom; columns left-to-right by number; empty columns are hidden);\n# [leaderboard] shared leaderboard HUD appearance;\n# [custom.<n>] arbitrary on-screen texts (template vars, own font_size and gradient).");
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
