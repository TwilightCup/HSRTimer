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
    /// <see cref="LayoutModel.DefaultRows"/>; adding a new repair concern is one
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
            RepairLayoutRows,
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
        /// Ensure every default HUD row is present. When a new default
        /// <see cref="RowType"/> ships (e.g. <c>TotalAtLastSegment</c>), existing
        /// users whose <c>layout.ini</c> predates it never see it, because
        /// <see cref="LayoutModel.Load"/> rebuilds <see cref="LayoutModel.Rows"/>
        /// purely from the on-disk <c>[rows]</c> section. This inserts any
        /// missing default row — but only when the user's row set looks like a
        /// default-derived configuration (so a deliberately hand-customized order
        /// is left untouched).
        /// </summary>
        /// <remarks>
        /// Algorithm:
        /// <list type="number">
        /// <item><c>missing = DefaultRows \ Rows</c>. Empty → clean, nothing to do.</item>
        /// <item>Build <c>expected</c> = the default rows the user still has, in
        /// default order. If <c>expected</c> equals <c>Rows</c> element-wise, the
        /// set is default-derived (just missing some defaults) → safe to repair.</item>
        /// <item>Default-derived: insert each missing row at its canonical index,
        /// reconstructing exactly <see cref="LayoutModel.DefaultRows"/>.</item>
        /// <item>Otherwise (reordered, extra, or duplicate rows): leave it alone
        /// and emit a hint naming the missing default(s).</item>
        /// </list>
        /// Idempotent: after a repair <c>Rows == DefaultRows</c>, so the next boot
        /// finds nothing missing and writes nothing.
        /// </remarks>
        private static bool RepairLayoutRows(ConfigService cfg, out string summary)
        {
            summary = null;
            var rows = cfg.Layout.Rows;
            var defaults = LayoutModel.DefaultRows;

            var present = new HashSet<RowType>(rows);
            var missing = new List<RowType>();
            foreach (var r in defaults)
                if (!present.Contains(r))
                    missing.Add(r);

            if (missing.Count == 0)
                return false; // clean

            // Default-derived test: does the user's set equal "the defaults they
            // still have, in default order"? If so it's a default config that's
            // simply missing some newer defaults — safe to fill in. Any reorder,
            // extra, or duplicate makes it hand-customized.
            bool defaultDerived = IsDefaultDerived(rows, defaults);

            if (!defaultDerived)
            {
                summary = "layout rows: a default row is missing (" + Join(missing) +
                          ") but the row order looks custom; left unchanged. Add it manually in layout.ini [rows] if wanted.";
                return false;
            }

            // Reconstruct the canonical default list, preserving whatever subset
            // the user has and slotting the missing rows in at their default
            // positions. (For a default-derived set this reproduces DefaultRows
            // exactly; building it row-by-row keeps the result robust if the
            // "default-derived" test is ever loosened.)
            rows.Clear();
            foreach (var r in defaults)
                rows.Add(r);

            summary = "layout rows: added missing default row(s) " + Join(missing);
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
