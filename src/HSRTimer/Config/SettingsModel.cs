using System.Collections.Generic;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// All user-tunable settings (auto-reset, retry-clear, HUD visibility,
    /// language, current category). Persisted to settings.ini as human-readable
    /// text; missing keys fall back to defaults.
    /// </summary>
    public sealed class SettingsModel
    {
        // ── Timing toggles ──
        // Pause time is always counted and menu/lobby time is never counted;
        // those are no longer user settings.
        public bool AutoReset = true;             // R1.7.2 (default on)
        public bool RestartClearsForgivable = false; // R5.4.3 — pause-menu Restart clears forgivable flags (default off); the one-key retry always clears them (R5.4.2, fixed)
        public float RetryMinDwell = 0.5f;        // R6 minimum empty-scene dwell (seconds)

        // Note: there are no user-facing validity/anti-cheat options. The
        // cheat/speed/drift detectors (R5.1) are always on with hardcoded
        // thresholds — none may be tuned or disabled by the player.

        // ── HUD ──
        public bool ShowHud = true;               // R2.5.1

        // Real-time clock is always active while a run is in progress. It is
        // shown by default (below Game Time in the default layout); this setting
        // controls its HUD visibility.
        public bool ShowRealTime = true;

        // Move the game's own top-right Loading/Saving progress indicator to
        // the top-center of the screen (default off).
        public bool CenterLoadingSaving = false;

        // ── Identity / selection ──
        // Note: there are no category presets; the enabled tag set is stored in
        // tags.ini (see EnabledTagsModel). Only the language choice lives here.
        public string CurrentLang = "en";

        // ── Keybinds (R1.7.1 reset, R6.1 retry, settings panel) ──
        public KeyCode ResetKey = KeyCode.Backspace;
        public KeyCode RetryKey = KeyCode.R;
        public KeyCode MenuKey = KeyCode.Home;

        // ── Subsegment (R8) ─────────────────────────────────────────────
        public bool SubsegmentEnable = true;
        public string SubsegmentPBPath = "subsegment/pb";
        public string SubsegmentLoadPath = "subsegment/load";
        public KeyCode SubsegmentToggleKey = KeyCode.Tab;
        public string SubsegmentMultiProject = "Any%";
        public float SubsegmentPlaneRadius = 50f;
        public float SubsegmentMinMove = 0.5f;
        public float SubsegmentSampleInterval = 1f;
        public float SubsegmentQuietSettleSeconds = 0.5f;
        public float SubsegmentPlaneDebounceSeconds = 0.2f;
        public float SubsegmentRespawnJumpMeters = 100f;
        public int SubsegmentMaxLeaderboardEntries = 8;
        public bool SubsegmentDebugLogging = false;

        // Subsegment leaderboard HUD appearance (R8.5); independent of the main
        // timer panel's layout. OffsetY is an offset from the automatic
        // left-middle vertical centering.
        public int SubsegmentHudFontSize = 16;
        public float SubsegmentHudOffsetX = 16f;
        public float SubsegmentHudOffsetY = 0f;

        private const string Section = "settings";
        private const string SubsegmentSection = "Subsegment";

        public void Load()
        {
            foreach (var p in PersistenceService.Read(PersistenceService.PathFor("settings.ini")))
            {
                if (p.Section == Section)
                {
                    Apply(p.Key, p.Value);
                }
                else if (p.Section == SubsegmentSection)
                {
                    ApplySubsegment(p.Key, p.Value);
                }
            }
        }

        private void Apply(string key, string value)
        {
            try
            {
                switch (key)
                {
                    case "auto_reset": AutoReset = ParseBool(value, AutoReset); break;
                    case "restart_clears_forgivable": RestartClearsForgivable = ParseBool(value, RestartClearsForgivable); break;
                    case "retry_min_dwell": RetryMinDwell = ParseFloat(value, RetryMinDwell); break;
                    case "show_hud": ShowHud = ParseBool(value, ShowHud); break;
                    case "show_real_time": ShowRealTime = ParseBool(value, ShowRealTime); break;
                    case "center_loading_saving": CenterLoadingSaving = ParseBool(value, CenterLoadingSaving); break;
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

        private void ApplySubsegment(string key, string value)
        {
            try
            {
                switch (key)
                {
                    case "Enable": SubsegmentEnable = ParseBool(value, SubsegmentEnable); break;
                    case "PBPath": SubsegmentPBPath = value; break;
                    case "LoadPath": SubsegmentLoadPath = value; break;
                    case "ToggleKey": SubsegmentToggleKey = ParseKeyCode(value, SubsegmentToggleKey); break;
                    case "MultiProject": SubsegmentMultiProject = value; break;
                    case "PlaneRadius": SubsegmentPlaneRadius = ParseFloat(value, SubsegmentPlaneRadius); break;
                    case "MinMove": SubsegmentMinMove = ParseFloat(value, SubsegmentMinMove); break;
                    case "SampleInterval": SubsegmentSampleInterval = ParseFloat(value, SubsegmentSampleInterval); break;
                    case "QuietSettleSeconds": SubsegmentQuietSettleSeconds = ParseFloat(value, SubsegmentQuietSettleSeconds); break;
                    case "PlaneDebounceSeconds": SubsegmentPlaneDebounceSeconds = ParseFloat(value, SubsegmentPlaneDebounceSeconds); break;
                    case "RespawnJumpMeters": SubsegmentRespawnJumpMeters = ParseFloat(value, SubsegmentRespawnJumpMeters); break;
                    case "MaxLeaderboardEntries": SubsegmentMaxLeaderboardEntries = ParseInt(value, SubsegmentMaxLeaderboardEntries); break;
                    case "DebugLogging": SubsegmentDebugLogging = ParseBool(value, SubsegmentDebugLogging); break;
                    case "HudFontSize": SubsegmentHudFontSize = ParseInt(value, SubsegmentHudFontSize); break;
                    case "HudOffsetX": SubsegmentHudOffsetX = ParseFloat(value, SubsegmentHudOffsetX); break;
                    case "HudOffsetY": SubsegmentHudOffsetY = ParseFloat(value, SubsegmentHudOffsetY); break;
                    default:
                        Plugin.Logger.LogWarning($"HSRTimer: settings.ini: unknown Subsegment key '{key}', ignored.");
                        break;
                }
            }
            catch
            {
                Plugin.Logger.LogWarning($"HSRTimer: settings.ini: bad Subsegment value for '{key}' = '{value}', kept default.");
            }
        }

        public void Save()
        {
            var kv = new Dictionary<string, string>
            {
                ["auto_reset"] = AutoReset ? "true" : "false",
                ["restart_clears_forgivable"] = RestartClearsForgivable ? "true" : "false",
                ["retry_min_dwell"] = RetryMinDwell.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["show_hud"] = ShowHud ? "true" : "false",
                ["show_real_time"] = ShowRealTime ? "true" : "false",
                ["center_loading_saving"] = CenterLoadingSaving ? "true" : "false",
                ["language"] = CurrentLang,
                ["reset_key"] = ResetKey.ToString(),
                ["retry_key"] = RetryKey.ToString(),
                ["menu_key"] = MenuKey.ToString(),
            };
            var sub = new Dictionary<string, string>
            {
                ["Enable"] = SubsegmentEnable ? "true" : "false",
                ["PBPath"] = SubsegmentPBPath,
                ["LoadPath"] = SubsegmentLoadPath,
                ["ToggleKey"] = SubsegmentToggleKey.ToString(),
                ["MultiProject"] = SubsegmentMultiProject,
                ["PlaneRadius"] = SubsegmentPlaneRadius.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["MinMove"] = SubsegmentMinMove.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["SampleInterval"] = SubsegmentSampleInterval.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["QuietSettleSeconds"] = SubsegmentQuietSettleSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["PlaneDebounceSeconds"] = SubsegmentPlaneDebounceSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["RespawnJumpMeters"] = SubsegmentRespawnJumpMeters.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["MaxLeaderboardEntries"] = SubsegmentMaxLeaderboardEntries.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["DebugLogging"] = SubsegmentDebugLogging ? "true" : "false",
                ["HudFontSize"] = SubsegmentHudFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["HudOffsetX"] = SubsegmentHudOffsetX.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
                ["HudOffsetY"] = SubsegmentHudOffsetY.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture),
            };
            PersistenceService.Write(
                PersistenceService.PathFor("settings.ini"),
                new[]
                {
                    new KeyValuePair<string, IDictionary<string, string>>(Section, kv),
                    new KeyValuePair<string, IDictionary<string, string>>(SubsegmentSection, sub),
                },
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

        public static int ParseInt(string s, int fallback)
        {
            if (string.IsNullOrEmpty(s)) return fallback;
            return int.TryParse(s.Trim(), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out int i) ? i : fallback;
        }
    }
}
