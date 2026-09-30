using System.Collections.Generic;

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
    /// appended to <see cref="Rules"/>.</item>
    /// </list>
    /// The HUD layout columns are deliberately <b>not</b> repaired here: the
    /// default layout is written once at config initialization (when
    /// <c>layout.ini</c> does not exist, see <see cref="ConfigService.Load"/>),
    /// and existing files are never auto-modified — the settings panel's
    /// Timer HUD column editor is the way users manage columns.
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

    }
}
