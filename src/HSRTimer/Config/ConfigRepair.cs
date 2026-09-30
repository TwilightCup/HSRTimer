using System.Collections.Generic;
using System.Text;

namespace HSRTimer
{
    /// <summary>
    /// Boot-time config check &amp; repair. Runs once right after
    /// <see cref="ConfigService.Load"/> (from <c>Plugin.Awake</c>) to detect and
    /// fill in missing or incorrect config items before any subsystem reads them.
    ///
    /// Design:
    /// <list type="bullet">
    /// <item><b>Idempotent structural checks</b> every boot, <b>write only when
    /// something changed</b> (dirty-gated <see cref="ConfigService.SaveSettings"/>).
    /// No config-version key — a stored version lies when a user hand-edits the
    /// file, whereas cheap structural checks self-heal hand-edited corruption
    /// and leave clean files untouched.</item>
    /// <item><b>Safe</b>: repairs are additive/conservative — never destroy user
    /// data (custom row order, custom texts, tag choices).</item>
    /// <item><b>Observable</b>: one <c>LogInfo</c> summary listing what changed,
    /// silent on a clean boot.</item>
    /// <item><b>Extensible</b>: each concern is a <see cref="RepairRule"/>
    /// appended to <see cref="Rules"/>. Adding a new default row is one line in
    /// <see cref="LayoutModel.DefaultColumns"/>; adding a new repair concern is one
    /// method + one array entry.</item>
    /// </list>
    /// </summary>
    public static class ConfigRepair
    {
        /// <summary>
        /// One repair concern. Returns true if it mutated the config (so the
        /// runner persists afterwards); <paramref name="summary"/> is a short,
        /// human-readable note on what changed or was skipped (empty if nothing
        /// of note). Must be safe to run every boot and idempotent.
        /// </summary>
        private delegate bool RepairRule(ConfigService cfg, out string summary);

        private static readonly RepairRule[] Rules =
        {
            MigrateRowsToColumns,
            RepairLayoutColumns,
            RepairLayoutColumn2,
            MigrateLeaderboardFromSettings,
        };

        /// <summary>Run every repair rule; persist once if any changed something.</summary>
        public static void Run(ConfigService cfg)
        {
            if (cfg == null) return;

            var changed = new List<string>();
            var hints = new List<string>();

            foreach (var rule in Rules)
            {
                try
                {
                    if (rule(cfg, out string summary))
                        changed.Add(summary);
                    else if (!string.IsNullOrEmpty(summary))
                        hints.Add(summary);
                }
                catch (System.Exception ex)
                {
                    Plugin.Logger.LogWarning($"HSRTimer: config repair rule '{rule.Method.Name}' threw: {ex.Message}");
                }
            }

            if (changed.Count > 0)
            {
                cfg.SaveSettings();
                Plugin.Logger.LogInfo("HSRTimer: config repaired — " + string.Join("; ", changed) + ".");
            }

            // Advisory-only notes (e.g. a default row is missing from a
            // hand-customized layout). Rare and actionable; safe to repeat.
            foreach (var hint in hints)
                Plugin.Logger.LogInfo("HSRTimer: " + hint);
        }

        /// <summary>
        /// One-time migration for the shared leaderboard HUD. The appearance
        /// (font size, offsets, colors) and display-mode keys used to live in
        /// settings.ini under [Subsegment]/[Markers]; they now belong in
        /// layout.ini [leaderboard]. Old keys are copied over only when the new
        /// layout key is absent (if both exist, the layout value wins), and the
        /// subsequent settings.ini rewrite drops the obsolete keys because
        /// <see cref="SettingsModel.Save"/> no longer writes them.
        /// Idempotent: after the first save the old keys are gone, so a clean
        /// boot finds nothing and writes nothing.
        /// </summary>
        private static bool MigrateLeaderboardFromSettings(ConfigService cfg, out string summary)
        {
            summary = null;

            var layoutKeys = new HashSet<string>();
            foreach (var p in PersistenceService.Read(PersistenceService.PathFor("layout.ini")))
            {
                if (p.Key != null && p.Section == "leaderboard")
                    layoutKeys.Add(p.Key);
            }

            var obsolete = new List<KeyValuePair<string, string>>();
            foreach (var p in PersistenceService.Read(PersistenceService.PathFor("settings.ini")))
            {
                if (p.Key == null) continue;
                if (p.Section == "Subsegment" && IsObsoleteSubsegmentKey(p.Key))
                    obsolete.Add(new KeyValuePair<string, string>(p.Key, p.Value));
                else if (p.Section == "Markers" && p.Key == "LeaderboardTimeMode")
                    obsolete.Add(new KeyValuePair<string, string>(p.Key, p.Value));
            }

            if (obsolete.Count == 0)
                return false; // clean — nothing to migrate or drop

            var layout = cfg.Layout;
            var migrated = new List<string>();
            var dropped = new List<string>();
            foreach (var kv in obsolete)
            {
                string newKey = ToLeaderboardKey(kv.Key);
                if (newKey == null) continue; // defensive; the mapping covers every obsolete key
                if (layoutKeys.Contains(newKey))
                {
                    dropped.Add(kv.Key);
                    continue;
                }
                ApplyLeaderboardValue(layout, newKey, kv.Value);
                migrated.Add(kv.Key + " -> " + newKey);
            }

            summary = "leaderboard config: ";
            if (migrated.Count > 0)
                summary += "migrated " + string.Join(", ", migrated) + " from settings.ini to layout.ini";
            if (dropped.Count > 0)
            {
                if (migrated.Count > 0) summary += "; ";
                summary += "dropped obsolete settings.ini key(s) " + string.Join(", ", dropped);
            }

            // True even when only old keys were dropped: SettingsModel.Save no
            // longer writes them, so the persisted settings.ini rewrite is what
            // actually removes them.
            return true;
        }

        private static bool IsObsoleteSubsegmentKey(string key)
        {
            switch (key)
            {
                case "HudFontSize":
                case "HudOffsetX":
                case "HudOffsetY":
                case "HudColorFaster":
                case "HudColorSlower":
                case "HudColorTie":
                case "LeaderboardMode":
                    return true;
                default:
                    return false;
            }
        }

        private static string ToLeaderboardKey(string oldKey)
        {
            switch (oldKey)
            {
                case "HudFontSize": return "font_size";
                case "HudOffsetX": return "offset_x";
                case "HudOffsetY": return "offset_y";
                case "HudColorFaster": return "color_faster";
                case "HudColorSlower": return "color_slower";
                case "HudColorTie": return "color_tie";
                case "LeaderboardMode": return "mode";
                case "LeaderboardTimeMode": return "markers_time_mode";
                default: return null;
            }
        }

        private static void ApplyLeaderboardValue(LayoutModel layout, string newKey, string value)
        {
            switch (newKey)
            {
                case "font_size": layout.LeaderboardFontSize = SettingsModel.ParseInt(value, layout.LeaderboardFontSize); break;
                case "offset_x": layout.LeaderboardOffsetX = SettingsModel.ParseFloat(value, layout.LeaderboardOffsetX); break;
                case "offset_y": layout.LeaderboardOffsetY = SettingsModel.ParseFloat(value, layout.LeaderboardOffsetY); break;
                case "color_faster": layout.LeaderboardColorFaster = GradientText.ParseColor(value, layout.LeaderboardColorFaster); break;
                case "color_slower": layout.LeaderboardColorSlower = GradientText.ParseColor(value, layout.LeaderboardColorSlower); break;
                case "color_tie": layout.LeaderboardColorTie = GradientText.ParseColor(value, layout.LeaderboardColorTie); break;
                case "mode": layout.LeaderboardMode = value; break;
                case "markers_time_mode": layout.LeaderboardMarkersTimeMode = value; break;
            }
        }

        /// <summary>
        /// One-time layout migration to the multi-column format (R2.2). Old
        /// configs store the HUD rows in a flat <c>[rows]</c> section; the new
        /// format uses <c>[column.N]</c> sections — the old list becomes
        /// <c>[column.1]</c> and an empty <c>[column.2]</c> is added.
        /// <see cref="LayoutModel.Load"/> already parsed the legacy section into
        /// <see cref="LayoutModel.Columns"/>, so this rule only needs to notice
        /// the legacy section on disk and report a change — the runner's final
        /// <c>SaveSettings</c> rewrites the file in the new format. RealTime is
        /// intentionally NOT moved into <c>[column.2]</c>: the migration
        /// preserves exactly what the user had (RealTime stays in
        /// <c>[column.1]</c> if that's where it was) and the new column is
        /// created empty. Idempotent: once the file is rewritten the
        /// <c>[rows]</c> section is gone, so a clean boot finds nothing.
        /// </summary>
        private static bool MigrateRowsToColumns(ConfigService cfg, out string summary)
        {
            summary = null;

            var path = PersistenceService.PathFor("layout.ini");
            bool hasRows = false;
            bool hasColumns = false;
            foreach (var p in PersistenceService.Read(path))
            {
                if (p.Section == "rows" && p.Key != null)
                    hasRows = true;
                else if (p.Section.StartsWith("column."))
                    hasColumns = true;
            }
            if (!hasRows || hasColumns)
                return false; // clean — nothing to migrate (or already migrated)

            summary = "layout: migrated [rows] to [column.1] and added an empty [column.2]";
            return true;
        }

        /// <summary>
        /// Ensure every default row of the first (timer-stack) column is
        /// present. When a new default <see cref="RowType"/> ships, existing
        /// users whose <c>layout.ini</c> predates it never see it, because
        /// <see cref="LayoutModel.Load"/> rebuilds
        /// <see cref="LayoutModel.Columns"/> purely from the on-disk
        /// <c>[column.N]</c> sections. This inserts any missing default row into
        /// <c>[column.1]</c> — but only when that column's row set looks like a
        /// default-derived configuration (so a deliberately hand-customized
        /// order is left untouched).
        /// </summary>
        /// <remarks>
        /// Only <c>[column.1]</c> is repaired by this rule. Column 2 is handled
        /// separately by <see cref="RepairLayoutColumn2"/> (the real-time rows'
        /// home), and later columns are never auto-filled: an empty column is a
        /// valid configuration (it is simply not displayed), and the legacy
        /// <c>[rows]</c> migration deliberately creates an empty
        /// <c>[column.2]</c> without moving RealTime into it.
        /// The default-derived check runs against
        /// <see cref="LayoutModel.LegacyDefaultRows"/> (the pre-columns default
        /// order, RealTime at index 1) so both fresh default column-1 layouts
        /// and legacy <c>[rows]</c>-migrated configs keep receiving repair for
        /// newer default rows.
        /// Algorithm:
        /// <list type="number">
        /// <item><c>missing = DefaultColumns[1] \ Column[1]</c>. Empty → clean, nothing to do.</item>
        /// <item>Build <c>expected</c> = the default rows the user still has, in
        /// default order. If <c>expected</c> equals <c>Column[1]</c>
        /// element-wise, the set is default-derived (just missing some defaults)
        /// → safe to repair.</item>
        /// <item>Default-derived: rebuild the canonical default list. A legacy
        /// column 1 (RealTime still in it) is restored to the full
        /// <see cref="LayoutModel.LegacyDefaultRows"/> so RealTime keeps its old
        /// position; a fresh-style column 1 is rebuilt to
        /// <see cref="LayoutModel.DefaultColumns"/>[1].</item>
        /// <item>Otherwise (reordered, extra, or duplicate rows): leave it alone
        /// and emit a hint naming the missing default(s).</item>
        /// </list>
        /// Idempotent: after a repair <c>Column[1]</c> is a canonical default
        /// list, so the next boot finds nothing missing and writes nothing.
        /// </remarks>
        private static bool RepairLayoutColumns(ConfigService cfg, out string summary)
        {
            summary = null;
            var layout = cfg.Layout;
            List<RowType> col;
            if (!layout.Columns.TryGetValue(1, out col) || col == null)
                return false;
            var defaults = LayoutModel.DefaultColumns[1];

            var present = new HashSet<RowType>(col);
            var missing = new List<RowType>();
            foreach (var r in defaults)
                if (!present.Contains(r))
                    missing.Add(r);

            if (missing.Count == 0)
                return false; // clean

            // Default-derived test: does the column equal "the defaults they
            // still have, in default order"? If so it's a default config that's
            // simply missing some newer defaults — safe to fill in. Any reorder,
            // extra, or duplicate makes it hand-customized. The check runs
            // against the legacy flat default order so migrated [rows] configs
            // (RealTime still in column 1) are accepted too.
            bool defaultDerived = IsDefaultDerived(col, LayoutModel.LegacyDefaultRows);

            if (!defaultDerived)
            {
                summary = "layout column 1: a default row is missing (" + Join(missing) +
                          ") but the row order looks custom; left unchanged. Add it manually in layout.ini [column.1] if wanted.";
                return false;
            }

            // Reconstruct the canonical default list, preserving whatever subset
            // the user has and slotting the missing rows in at their default
            // positions. For a *legacy* column 1 (RealTime still sitting at its
            // old index 1, as migrated [rows] configs have) restore the full
            // legacy default list so RealTime stays exactly where it was — the
            // migration must not move or drop it. For a fresh-style column 1
            // (no RealTime) rebuild the new default column 1.
            bool hasRealTime = col.Contains(RowType.RealTime);
            col.Clear();
            if (hasRealTime)
            {
                foreach (var r in LayoutModel.LegacyDefaultRows)
                    col.Add(r);
            }
            else
            {
                foreach (var r in defaults)
                    col.Add(r);
            }

            summary = "layout column 1: added missing default row(s) " + Join(missing);
            return true;
        }

        /// <summary>
        /// Ensure the second column's default rows are present — currently the
        /// "Real Time + Prev RT" pair. Column 2 is the home of the real-time
        /// rows (R2.2.4), so a new default row there ships with the same
        /// auto-insert for existing users that column 1 gets. Unlike column 1
        /// this rule deliberately **never auto-fills an empty column 2**: an
        /// empty column is a valid configuration (hidden, and the legacy
        /// <c>[rows]</c> migration creates it empty on purpose), so only a
        /// non-empty, default-derived column 2 (a subset of the defaults in
        /// order — e.g. just <c>RealTime</c>) is rebuilt to the canonical list.
        /// A reordered/extra/duplicate row set is treated as hand-customized
        /// and left untouched with an advisory hint, mirroring column 1.
        /// </summary>
        private static bool RepairLayoutColumn2(ConfigService cfg, out string summary)
        {
            summary = null;
            var layout = cfg.Layout;
            List<RowType> col;
            if (!layout.Columns.TryGetValue(2, out col) || col == null || col.Count == 0)
                return false; // empty column 2 is a valid configuration — never auto-fill
            var defaults = LayoutModel.DefaultColumns[2];

            var present = new HashSet<RowType>(col);
            var missing = new List<RowType>();
            foreach (var r in defaults)
                if (!present.Contains(r))
                    missing.Add(r);

            if (missing.Count == 0)
                return false; // clean

            if (!IsDefaultDerived(col, defaults))
            {
                summary = "layout column 2: a default row is missing (" + Join(missing) +
                          ") but the row order looks custom; left unchanged. Add it manually in layout.ini [column.2] if wanted.";
                return false;
            }

            col.Clear();
            foreach (var r in defaults)
                col.Add(r);

            summary = "layout column 2: added missing default row(s) " + Join(missing);
            return true;
        }

        /// <summary>
        /// True if <paramref name="rows"/> is exactly the subsequence of
        /// <paramref name="defaults"/> containing the rows that appear in it (i.e.
        /// a default config that may be missing some entries, but is otherwise
        /// un-customized). Any reordering, extra non-default row, or duplicate
        /// makes this false.
        /// </summary>
        private static bool IsDefaultDerived(List<RowType> rows, RowType[] defaults)
        {
            int di = 0;
            foreach (var r in rows)
            {
                // Walk defaults forward to the next occurrence of r.
                while (di < defaults.Length && defaults[di] != r)
                    di++;
                if (di >= defaults.Length)
                    return false; // r is not a default row, or already consumed (duplicate)
                di++; // consume this default slot
            }
            return true;
        }

        private static string Join(List<RowType> rows)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < rows.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(rows[i]);
            }
            return sb.ToString();
        }
    }
}
