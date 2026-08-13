using System.Collections.Generic;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// All user-tunable settings (R1.8/R1.9 toggles, R5.4 retry-clear, drift
    /// tolerance, HUD visibility, language, current category). Persisted to
    /// settings.ini as human-readable text; missing keys fall back to defaults.
    /// </summary>
    public sealed class SettingsModel
    {
        // ── Timing toggles ──
        public bool CountInPause = true;          // R1.8 (default on)
        public bool CountInMenu = false;          // R1.9 (default off)
        public bool AutoReset = true;             // R1.7.2 (default on)
        public bool RestartClearsForgivable = false; // R5.4.3 — pause-menu Restart clears forgivable flags (default off); the one-key retry always clears them (R5.4.2, fixed)
        public float RetryMinDwell = 0.5f;        // R6 minimum empty-scene dwell (seconds)

        // Note: there are no user-facing validity/anti-cheat options. The
        // cheat/speed/drift detectors (R5.1) are always on with hardcoded
        // thresholds — none may be tuned or disabled by the player.

        // ── HUD ──
        public bool ShowHud = true;               // R2.5.1

        // ── Identity / selection ──
        // Note: there are no category presets; the enabled tag set is stored in
        // tags.ini (see EnabledTagsModel). Only the language choice lives here.
        public string CurrentLang = "en";

        // ── Keybinds (R1.7.1 reset, R6.1 retry, settings panel) ──
        public KeyCode ResetKey = KeyCode.Backspace;
        public KeyCode RetryKey = KeyCode.R;
        public KeyCode MenuKey = KeyCode.Home;

        private const string Section = "settings";

        public void Load()
        {
            foreach (var p in PersistenceService.Read(PersistenceService.PathFor("settings.ini")))
            {
                if (p.Section != Section)
                    continue;
                Apply(p.Key, p.Value);
            }
        }

        private void Apply(string key, string value)
        {
            try
            {
                switch (key)
                {
                    case "count_in_pause": CountInPause = ParseBool(value, CountInPause); break;
                    case "count_in_menu": CountInMenu = ParseBool(value, CountInMenu); break;
                    case "auto_reset": AutoReset = ParseBool(value, AutoReset); break;
                    case "restart_clears_forgivable": RestartClearsForgivable = ParseBool(value, RestartClearsForgivable); break;
                    case "retry_min_dwell": RetryMinDwell = ParseFloat(value, RetryMinDwell); break;
                    case "show_hud": ShowHud = ParseBool(value, ShowHud); break;
                    case "language": CurrentLang = value; break;
                    case "reset_key": ResetKey = ParseKeyCode(value, ResetKey); break;
                    case "retry_key": RetryKey = ParseKeyCode(value, RetryKey); break;
                    case "menu_key": MenuKey = ParseKeyCode(value, MenuKey); break;
                    default:
                        Plugin.Logger.LogWarning($"HSRTimer: settings.ini: unknown key '{key}', ignored.");
                        break;
                }
            }
            catch
            {
                Plugin.Logger.LogWarning($"HSRTimer: settings.ini: bad value for '{key}' = '{value}', kept default.");
            }
        }

        public void Save()
        {
            var kv = new Dictionary<string, string>
            {
                ["count_in_pause"] = CountInPause ? "true" : "false",
                ["count_in_menu"] = CountInMenu ? "true" : "false",
                ["auto_reset"] = AutoReset ? "true" : "false",
                ["restart_clears_forgivable"] = RestartClearsForgivable ? "true" : "false",
                ["retry_min_dwell"] = RetryMinDwell.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["show_hud"] = ShowHud ? "true" : "false",
                ["language"] = CurrentLang,
                ["reset_key"] = ResetKey.ToString(),
                ["retry_key"] = RetryKey.ToString(),
                ["menu_key"] = MenuKey.ToString(),
            };
            PersistenceService.Write(
                PersistenceService.PathFor("settings.ini"),
                new[] { new KeyValuePair<string, IDictionary<string, string>>(Section, kv) },
                "HSRTimer settings. Lines of the form 'key = value'. Bad lines are ignored.");
        }

        // ── parsing helpers (tolerant) ──
        public static bool ParseBool(string s, bool fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            s = s.Trim().ToLowerInvariant();
            if (s == "true" || s == "1" || s == "yes" || s == "on") return true;
            if (s == "false" || s == "0" || s == "no" || s == "off") return false;
            return fallback;
        }

        public static KeyCode ParseKeyCode(string s, KeyCode fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            return System.Enum.TryParse(s, true, out KeyCode kc) ? kc : fallback;
        }

        public static float ParseFloat(string s, float fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            return float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out float f) ? f : fallback;
        }
    }
}
