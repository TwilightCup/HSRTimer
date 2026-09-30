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
        /// (1-based column index). This is the single source of truth shared by
        /// the <see cref="Columns"/> field initializer and
        /// <see cref="ConfigRepair"/>: adding a new default row (or reordering
        /// the defaults) is a one-line edit here, and both the fresh-install
        /// layout and the config-repair target stay in sync automatically.
        /// Column 1 is the leftmost timer stack; RealTime defaults into
        /// column 2.
        /// </summary>
        public static readonly Dictionary<int, RowType[]> DefaultColumns = new Dictionary<int, RowType[]>
        {
            { 1, new[] { RowType.GameTime, RowType.CurrentSegment, RowType.TotalAtLastSegment, RowType.LastSegment } },
            { 2, new[] { RowType.RealTime } },
        };

        /// <summary>
        /// The pre-columns default row order (the v1 <c>[rows]</c> layout, with
        /// RealTime at index 1). Kept so <see cref="ConfigRepair"/> can recognize
        /// legacy default-derived configs after their <c>[rows]</c> section is
        /// migrated to <c>[column.1]</c> and still auto-insert newer default rows.
        /// </summary>
        public static readonly RowType[] LegacyDefaultRows =
        {
            RowType.GameTime,
            RowType.RealTime,
            RowType.CurrentSegment,
            RowType.TotalAtLastSegment,
            RowType.LastSegment,
        };

        /// <summary>
        /// The HUD layout: one ordered row list per column (1-based index).
        /// Rows in a column are drawn top-to-bottom; columns are drawn
        /// left-to-right by index. A column with no rows is not displayed.
        /// </summary>
        public readonly Dictionary<int, List<RowType>> Columns = CreateDefaultColumns();

        public readonly List<CustomText> CustomTexts = new List<CustomText>();

        private static Dictionary<int, List<RowType>> CreateDefaultColumns()
        {
            var d = new Dictionary<int, List<RowType>>();
            foreach (var kv in DefaultColumns)
                d[kv.Key] = new List<RowType>(kv.Value);
            return d;
        }

        /// <summary>
        /// Ensure every column index from 1 up to <c>max(2, largest configured
        /// column)</c> exists, creating an empty column for any missing one (an
        /// empty column is a valid configuration — it is simply not displayed).
        /// Guarantees the canonical <c>[column.1]</c> / <c>[column.2]</c> are
        /// always present, so migrated configs get an empty <c>[column.2]</c>
        /// without RealTime being moved into it.
        /// </summary>
        public void EnsureColumns()
        {
            int max = 2;
            foreach (var k in Columns.Keys)
                if (k > max) max = k;
            for (int i = 1; i <= max; i++)
                if (!Columns.ContainsKey(i))
                    Columns[i] = new List<RowType>();
        }

        /// <summary>True if <paramref name="row"/> appears in any column.</summary>
        public bool HasRow(RowType row)
        {
            foreach (var kv in Columns)
                if (kv.Value.Contains(row))
                    return true;
            return false;
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
            var columnsByKey = new Dictionary<int, Dictionary<int, RowType>>(); // column → (row index → row type)
            var legacyRowsByKey = new Dictionary<int, RowType>();
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
                    // Legacy v1 [rows] section. Parsed so old configs keep
                    // working; Load maps it to [column.1] below and
                    // ConfigRepair rewrites the file to the new [column.N]
                    // sections.
                    int idx;
                    if (int.TryParse(p.Key, out idx) && System.Enum.TryParse(p.Value, true, out RowType rt))
                        legacyRowsByKey[idx] = rt;
                }
                else if (p.Section.StartsWith("column."))
                {
                    int col;
                    if (!int.TryParse(p.Section.Substring(7), out col)) continue;
                    int idx;
                    if (!int.TryParse(p.Key, out idx) || !System.Enum.TryParse(p.Value, true, out RowType rt)) continue;
                    Dictionary<int, RowType> rows;
                    if (!columnsByKey.TryGetValue(col, out rows))
                    {
                        rows = new Dictionary<int, RowType>();
                        columnsByKey[col] = rows;
                    }
                    rows[idx] = rt;
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

            if (columnsByKey.Count > 0)
            {
                Columns.Clear();
                foreach (var kv in columnsByKey)
                {
                    var rows = new List<RowType>();
                    var ordered = new List<int>(kv.Value.Keys);
                    ordered.Sort();
                    foreach (var idx in ordered) rows.Add(kv.Value[idx]);
                    Columns[kv.Key] = rows;
                }
            }
            else if (legacyRowsByKey.Count > 0)
            {
                // Migration (in-memory): the old single [rows] list becomes
                // [column.1]. An empty [column.2] is added by EnsureColumns
                // below; RealTime is NOT moved into it — the migration must not
                // change what the user sees.
                Columns.Clear();
                var rows = new List<RowType>();
                var ordered = new List<int>(legacyRowsByKey.Keys);
                ordered.Sort();
                foreach (var idx in ordered) rows.Add(legacyRowsByKey[idx]);
                Columns[1] = rows;
            }
            // else: no layout rows in the file — keep the default columns.

            // Missing columns become empty ones (see EnsureColumns), so a
            // [column.1]-only or [rows]-migrated config always gets an empty
            // [column.2].
            EnsureColumns();

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

            EnsureColumns();
            var colIndices = new List<int>(Columns.Keys);
            colIndices.Sort();
            foreach (var col in colIndices)
            {
                var rows = new Dictionary<string, string>();
                for (int i = 0; i < Columns[col].Count; i++)
                    rows[i.ToString()] = Columns[col][i].ToString();
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
                    ["color_a"] = GradientText.ToHex(ct.ColorA),
                    ["color_b"] = GradientText.ToHex(ct.ColorB),
                }));
            }

            PersistenceService.Write(
                path,
                sections,
                "HSRTimer HUD layout. Text is drawn directly on screen (no window).\n# [text] offset_x/offset_y (top-left px), font_size, color_a/color_b;\n# [column.<n>] ordered row types per column (drawn left-to-right by index; empty columns are hidden);\n# [leaderboard] shared leaderboard HUD appearance;\n# [custom.<n>] arbitrary on-screen texts (template vars).");
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
