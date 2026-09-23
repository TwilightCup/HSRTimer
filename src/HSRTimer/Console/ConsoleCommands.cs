using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using HumanAPI;
using Multiplayer;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// Registers an <c>hsr</c> command family with the game's built-in dev
    /// console (<c>Shell</c>, toggled with <c>~</c> or <c>F1</c>) so every
    /// HSRTimer feature can be inspected and exercised from inside the game.
    ///
    /// The commands intentionally mirror the settings-panel / keybind surfaces:
    /// status dumps, run controls, HUD/panel/leaderboard toggles, tag toggles,
    /// config get/set, layout editing, localization, presets, subsegments,
    /// markers, validity flags, and LC integration. They are intended for
    /// testing and debugging; most mutations call
    /// <see cref="ConfigService.SaveSettings"/> so they persist exactly like
    /// panel edits.
    /// </summary>
    public static class ConsoleCommands
    {
        private const string Prefix = "hsr";

        private static bool _registered;

        private static readonly FieldInfo[] SettingsFields =
            typeof(SettingsModel).GetFields(BindingFlags.Public | BindingFlags.Instance);

        private static readonly FieldInfo[] LayoutFields =
            typeof(LayoutModel).GetFields(BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// Common settings.ini-style aliases whose public field names differ
        /// from the on-disk key (e.g. "language" vs the <c>CurrentLang</c> field).
        /// </summary>
        private static readonly Dictionary<string, string> KeyAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["language"] = "CurrentLang",
                ["current_lang"] = "CurrentLang",
                ["currentlang"] = "CurrentLang",
                ["current"] = "CurrentPreset",
                ["current_preset"] = "CurrentPreset",
                ["currentpreset"] = "CurrentPreset",
                ["preset"] = "CurrentPreset",
                ["retry_level_override_enabled"] = "RetryLevelOverrideEnable",
                ["retry_level_override_enable"] = "RetryLevelOverrideEnable",
            };

        /// <summary>Register all HSRTimer console commands. Safe to call once; idempotent.</summary>
        public static void Register()
        {
            if (_registered)
                return;
            _registered = true;
            try
            {
                // The game's Shell uses a static CommandRegistry, so commands can
                // be registered before Shell.instance exists (Shell.Awake only
                // adds the built-in help commands; it never clears the registry).
                Shell.RegisterCommand(Prefix, new Action<string>(OnCommand), HelpFor(Prefix));
                Shell.RegisterCommand("hsrtimer", new Action<string>(OnCommand), HelpFor("hsrtimer"));
                Plugin.Logger.LogInfo("HSRTimer: dev-console commands registered (type 'hsr help' with ~/F1).");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to register console commands: {ex.Message}");
                _registered = false;
            }
        }

        // ── entry point ────────────────────────────────────────────────────

        private static void OnCommand(string args)
        {
            try
            {
                // Mirror the incoming command to the BepInEx log so a headless /
                // CI pass can see exactly what was invoked, not just its result.
                // The game's Shell passes null for a zero-argument invocation
                // (bare 'hsr'), so args must not be dereferenced directly.
                Plugin.Logger.LogInfo("HSRTimer[console]: > hsr" + (string.IsNullOrEmpty(args) ? "" : " " + args));

                var parts = SplitArgs(args);
                if (parts.Count == 0)
                {
                    PrintSummary();
                    return;
                }

                string cmd = parts[0].ToLowerInvariant();
                var rest = parts.GetRange(1, parts.Count - 1);

                switch (cmd)
                {
                    case "help": OnHelp(rest); break;
                    case "status": CmdStatus(); break;
                    case "keys": CmdKeys(); break;
                    case "get": CmdGet(rest); break;
                    case "set": CmdSet(rest); break;
                    case "reload": CmdReload(); break;
                    case "save": CmdSave(); break;
                    case "reset": CmdReset(); break;
                    case "retry": CmdRetry(); break;
                    case "hud": CmdHud(rest); break;
                    case "panel": CmdPanel(rest); break;
                    case "leaderboard": CmdLeaderboard(rest); break;
                    case "layout": CmdLayout(rest); break;
                    case "tag": CmdTag(rest); break;
                    case "lang": CmdLang(rest); break;
                    case "preset": CmdPreset(rest); break;
                    case "sub": CmdSub(rest); break;
                    case "marker": CmdMarker(rest); break;
                    case "flags": CmdFlags(rest); break;
                    case "lc": CmdLc(rest); break;
                    case "config": CmdConfig(rest); break;
                    case "update": CmdUpdate(rest); break;
                    case "about": CmdAbout(); break;
                    default:
                        Print($"Unknown HSRTimer command: {cmd}. Type 'hsr help' for usage.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Print($"HSRTimer command error: {ex.Message}");
                Plugin.Logger.LogWarning($"HSRTimer console command error: {ex}");
            }
        }

        private static void OnHelp(List<string> args)
        {
            if (args.Count > 0)
            {
                string topic = args[0].ToLowerInvariant();
                Print(HelpFor(topic));
                return;
            }
            PrintSummary();
        }

        // ── status / keys / get / set / reload / save ─────────────────────

        private static void CmdStatus()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) { Print("HSRTimer config is not ready."); return; }
            var state = TimerCore.State;
            var game = Game.instance;
            var s = cfg.Settings;
            var loc = cfg.Localization;

            var sb = new StringBuilder();
            sb.AppendLine($"HSRTimer v{PluginInfo.PLUGIN_VERSION} | lang={loc.CurrentCode} | preset={s.CurrentPreset}");
            sb.AppendLine($"game={(game != null ? game.state.ToString() : "null")} app={App.state} local={NetGame.isLocal} server={NetGame.isServer} client={NetGame.isClient}");

            if (state != null)
            {
                sb.AppendLine($"segment={state.InSegment} timing={state.TimingActive} retrying={state.Retrying} realTimeActive={state.RealTimeActive}");
                sb.AppendLine($"gameTime={FormatNumber(state.GameTime)} segmentTime={FormatNumber(state.GameTime - state.SegmentStart)} realTime={FormatNumber(state.RealTime)}");
                sb.AppendLine($"lastSegment={FormatNullable(state.LastSegment)} totalAtLastSegment={FormatNullable(state.TotalAtLastSegment)} lastRun={FormatNullable(state.LastRun)} wakeUp={FormatNullable(state.WakeUpTime)}");
                sb.AppendLine($"level={state.CurrentLevelNumber} type={state.CurrentLevelType} cp={(game != null ? game.currentCheckpointNumber : -1)} prevCp={state.PrevCheckpoint} maxCp={state.MaxCheckpointThisLevel} campaignRetryLevel={state.CampaignRetryLevel}");
                sb.Append("flags:");
                string hard = state.Flags.FormatReasons(loc);
                if (hard.Length == 0)
                    sb.Append(" none");
                else
                    sb.Append(' ').Append(hard);
                bool firstSoft = true;
                foreach (var soft in state.Flags.SoftFlags)
                {
                    sb.Append(firstSoft ? " | soft:" : ",");
                    firstSoft = false;
                    sb.Append(' ').Append(InvalidReasons.LocalKey(soft.Reason)).Append(" x").Append(soft.Count);
                }
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("state=null");
            }

            sb.Append("tags:");
            var tags = cfg.EnabledTags != null ? cfg.EnabledTags.Tags : null;
            if (tags == null || tags.Count == 0)
                sb.Append(" Any");
            else
                foreach (var t in tags) sb.Append(' ').Append(t);
            sb.AppendLine();

            var sub = SubsegmentManager.Instance;
            sb.AppendLine($"subsegment={(sub != null && s.SubsegmentEnable ? "on" : "off")} entries={(sub != null ? sub.Entries.Count : 0)} title=\"{sub?.LeaderboardTitle}\" multiRun={sub != null && sub.InMultiRunActive} preserved={sub != null && sub.InPreservedTransition}");

            var markers = MarkersManager.Instance;
            int markerCount = markers?.CurrentSet != null ? markers.CurrentSet.markers.Count : 0;
            sb.AppendLine($"markers={(s.MarkersEnable ? "on" : "off")} currentLevel=\"{markers?.CurrentLevelKey}\" markers={markerCount} feed={markers?.Feed.Count ?? 0}");

            var lb = LeaderboardHud.Instance;
            sb.AppendLine($"leaderboard={(lb != null && lb.Visible ? "visible" : "hidden")} mode={cfg.Layout.LeaderboardMode}");

            var lc = LcIntegration.Instance;
            sb.AppendLine($"lc={(lc != null && lc.Enabled ? "enabled" : "absent")} inRun={lc != null && lc.IsInCollectionRun} lastLevel={lc != null && lc.IsLastLevelOfCollection} name=\"{lc?.CollectionName}\"");

            sb.Append("configDir=").Append(PersistenceService.PluginDir);
            Print(sb.ToString());
        }

        private static void CmdKeys()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("Settings keys:");
            foreach (var f in SettingsFields)
                sb.Append("  ").AppendLine(FieldName(f.Name));
            sb.AppendLine("Layout keys:");
            foreach (var f in LayoutFields)
            {
                if (f.FieldType == typeof(List<RowType>) || f.FieldType == typeof(List<CustomText>))
                    continue; // managed by 'hsr layout row/text'
                sb.Append("  ").AppendLine(FieldName(f.Name));
            }
            Print(sb.ToString());
        }

        private static void CmdGet(List<string> args)
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) { Print("HSRTimer config is not ready."); return; }
            if (args.Count == 0) { Print("Usage: hsr get <key> | hsr get all"); return; }

            string key = args[0];
            if (Normalize(key) == "all")
            {
                var sb = new StringBuilder();
                sb.AppendLine("--- settings ---");
                foreach (var f in SettingsFields)
                    sb.AppendLine($"{FieldName(f.Name)} = {FormatFieldValue(f, cfg.Settings)}");
                sb.AppendLine("--- layout ---");
                foreach (var f in LayoutFields)
                {
                    if (f.FieldType == typeof(List<RowType>) || f.FieldType == typeof(List<CustomText>))
                        continue;
                    sb.AppendLine($"{FieldName(f.Name)} = {FormatFieldValue(f, cfg.Layout)}");
                }
                sb.AppendLine("--- tags ---");
                sb.AppendLine("enabled = " + (cfg.EnabledTags != null ? string.Join(", ", cfg.EnabledTags.Tags) : ""));
                Print(sb.ToString());
                return;
            }

            if (TryFindField(key, out FieldInfo field, out object owner))
            {
                Print($"{FieldName(field.Name)} = {FormatFieldValue(field, owner)}");
            }
            else
            {
                Print($"Unknown HSRTimer key: {key}. Try 'hsr keys' or 'hsr get all'.");
            }
        }

        private static void CmdSet(List<string> args)
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) { Print("HSRTimer config is not ready."); return; }
            if (args.Count < 2) { Print("Usage: hsr set <key> <value>"); return; }

            string key = args[0];
            string value = string.Join(" ", args.GetRange(1, args.Count - 1));

            if (!TryFindField(key, out FieldInfo field, out object owner))
            {
                Print($"Unknown HSRTimer key: {key}. Try 'hsr keys' or 'hsr get all'.");
                return;
            }

            if (!TrySetField(field, owner, value, out string error))
            {
                Print($"Cannot set {FieldName(field.Name)}: {error}");
                return;
            }

            // Apply side effects for keys that need live re-wiring.
            if (ReferenceEquals(owner, cfg.Settings) && Normalize(field.Name) == "currentlang")
            {
                string code = CanonicalLanguageCode(cfg, cfg.Settings.CurrentLang);
                if (code != null)
                    cfg.Settings.CurrentLang = code;
                cfg.Localization.SetLanguage(cfg.Settings.CurrentLang);
                SettingsPanelTabRegistry.Instance?.NotifyLanguageChanged(cfg.Localization.CurrentCode);
            }
            if (ReferenceEquals(owner, cfg.Settings) && Normalize(field.Name) == "currentpreset")
            {
                string actualPreset = FindPresetName(cfg.Settings.CurrentPreset);
                if (actualPreset != null)
                    cfg.Settings.CurrentPreset = actualPreset;
                else if (!PresetStore.Exists(cfg.Settings.CurrentPreset))
                    cfg.Settings.CurrentPreset = PresetStore.DefaultPresetName;
            }
            if (ReferenceEquals(owner, cfg.Settings) && Normalize(field.Name) == "subsegmentmultiproject")
            {
                cfg.Settings.SubsegmentMultiProject = CanonicalMultiProject(cfg.Settings.SubsegmentMultiProject);
            }
            if (ReferenceEquals(owner, cfg.Layout) && Normalize(field.Name) == "leaderboardmode")
            {
                cfg.Layout.LeaderboardMode = CanonicalLeaderboardMode(cfg.Layout.LeaderboardMode);
            }
            if (ReferenceEquals(owner, cfg.Layout) && Normalize(field.Name) == "leadermarkerstimemode")
            {
                cfg.Layout.LeaderboardMarkersTimeMode = CanonicalMarkersTimeMode(cfg.Layout.LeaderboardMarkersTimeMode);
            }

            cfg.SaveSettings();
            Print($"set {FieldName(field.Name)} = {FormatFieldValue(field, owner)}");
        }

        private static string CanonicalLanguageCode(ConfigService cfg, string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;
            foreach (var lang in cfg.Localization.Languages)
            {
                if (string.Equals(lang.Code, value, StringComparison.OrdinalIgnoreCase))
                    return lang.Code;
            }
            return null;
        }

        private static string CanonicalMultiProject(string value)
        {
            if (string.Equals(value, "Aztec%", StringComparison.OrdinalIgnoreCase)) return "Aztec%";
            if (string.Equals(value, "Dark%", StringComparison.OrdinalIgnoreCase)) return "Dark%";
            if (string.Equals(value, "Steam%", StringComparison.OrdinalIgnoreCase)) return "Steam%";
            if (string.Equals(value, "Any%", StringComparison.OrdinalIgnoreCase)) return "Any%";
            return value;
        }

        private static string CanonicalLeaderboardMode(string value)
        {
            if (string.Equals(value, "Markers", StringComparison.OrdinalIgnoreCase)) return "Markers";
            return "Subsegment";
        }

        private static string CanonicalMarkersTimeMode(string value)
        {
            if (string.Equals(value, "Absolute", StringComparison.OrdinalIgnoreCase)) return "Absolute";
            return "Relative";
        }

        private static void CmdReload()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            cfg.ReloadAll();
            SettingsPanelTabRegistry.Instance?.NotifyLanguageChanged(cfg.Localization.CurrentCode);
            Print("HSRTimer config + localization reloaded from disk.");
        }

        private static void CmdSave()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            cfg.SaveSettings();
            Print("HSRTimer settings saved.");
        }

        // ── run controls ───────────────────────────────────────────────────

        private static void CmdReset()
        {
            if (TimerCore.Instance == null)
            {
                Print("TimerCore is not ready.");
                return;
            }
            TimerCore.ResetRun();
            Print("HSRTimer run reset.");
        }

        private static void CmdRetry()
        {
            var cfg = ConfigService.Instance;
            var core = TimerCore.Instance;
            if (core == null || cfg == null || TimerCore.State == null)
            {
                Print("TimerCore is not ready.");
                return;
            }
            // The console is itself a keyboard-capturing UI; opt out of the
            // R6.1.2a input guard so 'hsr retry' can exercise the retry flow.
            // Physical keybinds keep the guard (see TimerCore.HandleKeybinds).
            if (RetryAction.TryExecute(core, TimerCore.State, cfg.Settings, out string notifyKey, allowWhileKeyboardCaptured: true))
                core.RefreshTimingOptions(); // restart may change timing context
            string msg = notifyKey != null ? cfg.Localization.Get(notifyKey) : "retry blocked";
            Print("HSRTimer retry: " + msg);
        }

        // ── hud / panel / leaderboard ──────────────────────────────────────

        /// <summary>
        /// R12.6: print the same identity/license/repository content the
        /// settings panel's About page shows, so the page's static text can be
        /// verified from the console (the link button itself opens the system
        /// browser).
        /// </summary>
        private static void CmdAbout()
        {
            var sb = new StringBuilder();
            sb.AppendLine(PluginInfo.PLUGIN_NAME + " " + PluginInfo.PLUGIN_VERSION);
            sb.AppendLine(PluginInfo.LICENSE_LINE1);
            sb.AppendLine(PluginInfo.LICENSE_LINE2);
            sb.AppendLine(PluginInfo.PLUGIN_REPOSITORY_URL);
            Print(sb.ToString());
        }

        private static void CmdHud(List<string> args)
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            string arg = args.Count > 0 ? args[0].ToLowerInvariant() : "toggle";
            switch (arg)
            {
                case "on": case "show": cfg.Settings.ShowHud = true; break;
                case "off": case "hide": cfg.Settings.ShowHud = false; break;
                case "toggle": cfg.Settings.ShowHud = !cfg.Settings.ShowHud; break;
                case "status":
                    Print("show_hud = " + (cfg.Settings.ShowHud ? "true" : "false"));
                    return;
                default:
                    Print("Usage: hsr hud [on|off|toggle|status]");
                    return;
            }
            cfg.SaveSettings();
            Print("show_hud = " + (cfg.Settings.ShowHud ? "true" : "false"));
        }

        private static void CmdPanel(List<string> args)
        {
            var panel = SettingsPanel.Instance;
            if (panel == null) { Print("SettingsPanel is not ready."); return; }
            string arg = args.Count > 0 ? args[0].ToLowerInvariant() : "toggle";
            switch (arg)
            {
                case "open": case "show": panel.SetVisible(true); break;
                case "close": case "hide": panel.SetVisible(false); break;
                case "toggle": panel.Toggle(); break;
                case "status":
                    Print("panel = " + (panel.IsVisible ? "visible" : "hidden"));
                    return;
                default:
                    Print("Usage: hsr panel [open|close|toggle|status]");
                    return;
            }
            Print("panel = " + (panel.IsVisible ? "visible" : "hidden"));
        }

        private static void CmdLeaderboard(List<string> args)
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            var lb = LeaderboardHud.Instance;
            if (lb == null) { Print("LeaderboardHud is not ready."); return; }

            string arg = args.Count > 0 ? args[0].ToLowerInvariant() : "status";
            switch (arg)
            {
                case "cycle":
                    lb.CycleMode();
                    break;
                case "show":
                    lb.SetVisible(true);
                    break;
                case "hide":
                    lb.SetVisible(false);
                    break;
                case "mode":
                    if (args.Count < 2)
                    {
                        Print("Usage: hsr leaderboard mode <Subsegment|Markers>");
                        return;
                    }
                    lb.SetMode(args[1]);
                    break;
                case "status":
                    Print($"leaderboard = {(lb.Visible ? "visible" : "hidden")}, mode = {cfg.Layout.LeaderboardMode}");
                    return;
                default:
                    Print("Usage: hsr leaderboard [cycle|show|hide|mode <Subsegment|Markers>|status]");
                    return;
            }
            Print($"leaderboard = {(lb.Visible ? "visible" : "hidden")}, mode = {cfg.Layout.LeaderboardMode}");
        }

        // ── layout ─────────────────────────────────────────────────────────

        private static void CmdLayout(List<string> args)
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            if (args.Count == 0) { CmdLayoutStatus(cfg); return; }

            string sub = args[0].ToLowerInvariant();
            var rest = args.GetRange(1, args.Count - 1);
            switch (sub)
            {
                case "status": CmdLayoutStatus(cfg); break;
                case "row": CmdLayoutRow(cfg, rest); break;
                case "text": CmdLayoutText(cfg, rest); break;
                case "set":
                    if (rest.Count < 2) { Print("Usage: hsr layout set <key> <value>"); return; }
                    CmdSet(new List<string> { rest[0], string.Join(" ", rest.GetRange(1, rest.Count - 1)) });
                    break;
                case "get":
                    if (rest.Count < 1) { Print("Usage: hsr layout get <key>"); return; }
                    CmdGet(new List<string> { rest[0] });
                    break;
                default:
                    Print("Usage: hsr layout [status|row ...|text ...|get <key>|set <key> <value>]");
                    break;
            }
        }

        private static void CmdLayoutStatus(ConfigService cfg)
        {
            var l = cfg.Layout;
            var sb = new StringBuilder();
            sb.AppendLine($"offsetX={l.OffsetX} offsetY={l.OffsetY} fontSize={l.FontSize} colorA={GradientText.ToHex(l.ColorA)} colorB={GradientText.ToHex(l.ColorB)}");
            sb.Append("rows:");
            for (int i = 0; i < l.Rows.Count; i++)
                sb.Append(' ').Append(i).Append('=').Append(l.Rows[i]);
            sb.AppendLine();
            sb.Append("customTexts:");
            if (l.CustomTexts.Count == 0)
                sb.Append(" none");
            else
                for (int i = 0; i < l.CustomTexts.Count; i++)
                    sb.Append(' ').Append(i).Append("=\"").Append(l.CustomTexts[i].Text).Append("\"@(").Append(l.CustomTexts[i].X).Append(',').Append(l.CustomTexts[i].Y).Append(')');
            sb.AppendLine();
            sb.AppendLine($"leaderboard: fontSize={l.LeaderboardFontSize} offsetX={l.LeaderboardOffsetX} offsetY={l.LeaderboardOffsetY} mode={l.LeaderboardMode} markersTimeMode={l.LeaderboardMarkersTimeMode}");
            Print(sb.ToString());
        }

        private static void CmdLayoutRow(ConfigService cfg, List<string> args)
        {
            var l = cfg.Layout;
            if (args.Count == 0) { Print("Usage: hsr layout row <list|add <type>|remove <index>|clear>"); return; }
            string sub = args[0].ToLowerInvariant();
            switch (sub)
            {
                case "list":
                    var sb = new StringBuilder();
                    for (int i = 0; i < l.Rows.Count; i++)
                        sb.AppendLine($"{i}: {l.Rows[i]}");
                    Print(sb.Length == 0 ? "No rows configured." : sb.ToString());
                    return;
                case "add":
                    if (args.Count < 2) { Print("Usage: hsr layout row add <RowType>"); return; }
                    if (Enum.TryParse(args[1], true, out RowType rt))
                    {
                        l.Rows.Add(rt);
                        cfg.SaveSettings();
                        Print($"Added row {rt} at index {l.Rows.Count - 1}.");
                    }
                    else
                    {
                        Print($"Unknown RowType: {args[1]}. Valid: {string.Join(", ", Enum.GetNames(typeof(RowType)))}");
                    }
                    return;
                case "remove":
                    if (args.Count < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx)
                        || idx < 0 || idx >= l.Rows.Count)
                    {
                        Print("Usage: hsr layout row remove <index>");
                        return;
                    }
                    var removed = l.Rows[idx];
                    l.Rows.RemoveAt(idx);
                    cfg.SaveSettings();
                    Print($"Removed row {idx} ({removed}).");
                    return;
                case "clear":
                    l.Rows.Clear();
                    cfg.SaveSettings();
                    Print("Cleared all layout rows.");
                    return;
                default:
                    Print("Usage: hsr layout row <list|add <type>|remove <index>|clear>");
                    return;
            }
        }

        private static void CmdLayoutText(ConfigService cfg, List<string> args)
        {
            var l = cfg.Layout;
            if (args.Count == 0) { Print("Usage: hsr layout text <list|add <x> <y> <text...>|remove <index>|clear>"); return; }
            string sub = args[0].ToLowerInvariant();
            switch (sub)
            {
                case "list":
                    var sb = new StringBuilder();
                    for (int i = 0; i < l.CustomTexts.Count; i++)
                    {
                        var t = l.CustomTexts[i];
                        sb.AppendLine($"{i}: \"{t.Text}\" @({t.X},{t.Y}) {GradientText.ToHex(t.ColorA)}/{GradientText.ToHex(t.ColorB)}");
                    }
                    Print(sb.Length == 0 ? "No custom texts configured." : sb.ToString());
                    return;
                case "add":
                    if (args.Count < 4)
                    {
                        Print("Usage: hsr layout text add <x> <y> <text...>");
                        return;
                    }
                    if (!float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)
                        || !float.TryParse(args[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y))
                    {
                        Print("x and y must be numbers.");
                        return;
                    }
                    string text = string.Join(" ", args.GetRange(3, args.Count - 3));
                    l.CustomTexts.Add(new CustomText { X = x, Y = y, Text = text, ColorA = Color.white, ColorB = Color.white });
                    cfg.SaveSettings();
                    Print($"Added custom text {l.CustomTexts.Count - 1}: \"{text}\".");
                    return;
                case "remove":
                    if (args.Count < 2 || !int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int idx)
                        || idx < 0 || idx >= l.CustomTexts.Count)
                    {
                        Print("Usage: hsr layout text remove <index>");
                        return;
                    }
                    l.CustomTexts.RemoveAt(idx);
                    cfg.SaveSettings();
                    Print($"Removed custom text {idx}.");
                    return;
                case "clear":
                    l.CustomTexts.Clear();
                    cfg.SaveSettings();
                    Print("Cleared all custom texts.");
                    return;
                default:
                    Print("Usage: hsr layout text <list|add <x> <y> <text...>|remove <index>|clear>");
                    return;
            }
        }

        // ── tags ───────────────────────────────────────────────────────────

        private static void CmdTag(List<string> args)
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            if (args.Count == 0) { CmdTagList(cfg); return; }
            string sub = args[0].ToLowerInvariant();
            switch (sub)
            {
                case "list": CmdTagList(cfg); break;
                case "enable":
                    if (args.Count < 2) { Print("Usage: hsr tag enable <id>"); return; }
                    string enableId = CanonicalTagId(args[1]);
                    if (enableId == null)
                    {
                        Print($"Unknown tag: {args[1]}. Use 'hsr tag list'.");
                        return;
                    }
                    cfg.EnabledTags.Enable(enableId);
                    cfg.SaveSettings();
                    Print($"Tag {enableId} enabled.");
                    break;
                case "disable":
                    if (args.Count < 2) { Print("Usage: hsr tag disable <id>"); return; }
                    string disableId = CanonicalTagId(args[1]) ?? args[1];
                    cfg.EnabledTags.Disable(disableId);
                    cfg.SaveSettings();
                    Print($"Tag {disableId} disabled.");
                    break;
                case "set":
                    if (args.Count < 3) { Print("Usage: hsr tag set <id> <on|off>"); return; }
                    string setId = CanonicalTagId(args[1]);
                    if (setId == null)
                    {
                        Print($"Unknown tag: {args[1]}. Use 'hsr tag list'.");
                        return;
                    }
                    if (SettingsModel.ParseBool(args[2], false))
                        cfg.EnabledTags.Enable(setId);
                    else
                        cfg.EnabledTags.Disable(setId);
                    cfg.SaveSettings();
                    Print($"Tag {setId} = {(cfg.EnabledTags.HasTag(setId) ? "on" : "off")}.");
                    break;
                default:
                    Print("Usage: hsr tag [list|enable <id>|disable <id>|set <id> <on|off>]");
                    break;
            }
        }

        /// <summary>
        /// Resolve a tag id case-insensitively (the game console lowercases all
        /// input before our handler runs) to the canonical registered id, or
        /// null when no registered tag matches.
        /// </summary>
        private static string CanonicalTagId(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            var registry = TagRuleRegistry.Instance;
            if (registry == null)
                return null;
            foreach (var rule in registry.All)
            {
                if (rule != null && string.Equals(rule.Id, id, StringComparison.OrdinalIgnoreCase))
                    return rule.Id;
            }
            return null;
        }

        private static void CmdTagList(ConfigService cfg)
        {
            var registry = TagRuleRegistry.Instance;
            var sb = new StringBuilder();
            sb.Append("enabled: ");
            if (cfg.EnabledTags == null || cfg.EnabledTags.Tags.Count == 0)
                sb.Append("Any");
            else
                sb.Append(string.Join(", ", cfg.EnabledTags.Tags));
            sb.AppendLine();
            if (registry == null)
            {
                sb.Append("registry: not ready");
            }
            else
            {
                sb.Append("available:");
                foreach (var rule in registry.All)
                    sb.Append(' ').Append(rule.Id).Append(cfg.EnabledTags.HasTag(rule.Id) ? " [on]" : " [off]");
            }
            Print(sb.ToString());
        }

        // ── language ───────────────────────────────────────────────────────

        private static void CmdLang(List<string> args)
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            if (args.Count == 0) { CmdLangList(cfg); return; }
            string sub = args[0].ToLowerInvariant();
            switch (sub)
            {
                case "list": CmdLangList(cfg); break;
                case "set":
                    if (args.Count < 2) { Print("Usage: hsr lang set <code>"); return; }
                    string canonicalCode = null;
                    foreach (var lang in cfg.Localization.Languages)
                    {
                        if (string.Equals(lang.Code, args[1], StringComparison.OrdinalIgnoreCase))
                        {
                            canonicalCode = lang.Code;
                            break;
                        }
                    }
                    if (canonicalCode == null)
                    {
                        Print($"Language '{args[1]}' not found. Use 'hsr lang list'.");
                        return;
                    }
                    cfg.Localization.SetLanguage(canonicalCode);
                    cfg.Settings.CurrentLang = canonicalCode;
                    cfg.SaveSettings();
                    SettingsPanelTabRegistry.Instance?.NotifyLanguageChanged(canonicalCode);
                    Print($"Language set to {canonicalCode} ({cfg.Localization.DisplayNameOf(canonicalCode)}).");
                    break;
                case "reload":
                    cfg.ReloadLanguage();
                    SettingsPanelTabRegistry.Instance?.NotifyLanguageChanged(cfg.Localization.CurrentCode);
                    Print($"Language reloaded; current = {cfg.Localization.CurrentCode}.");
                    break;
                case "current":
                    Print($"current language = {cfg.Localization.CurrentCode} ({cfg.Localization.DisplayNameOf(cfg.Localization.CurrentCode)})");
                    break;
                default:
                    Print("Usage: hsr lang [list|set <code>|reload|current]");
                    break;
            }
        }

        private static void CmdLangList(ConfigService cfg)
        {
            var sb = new StringBuilder();
            foreach (var lang in cfg.Localization.Languages)
            {
                sb.Append(lang.Code).Append(" = ").Append(lang.DisplayName);
                if (lang.Code == cfg.Localization.CurrentCode)
                    sb.Append(" [current]");
                sb.AppendLine();
            }
            Print(sb.ToString());
        }

        // ── presets ────────────────────────────────────────────────────────

        private static void CmdPreset(List<string> args)
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            if (args.Count == 0) { CmdPresetList(cfg); return; }
            string sub = args[0].ToLowerInvariant();
            switch (sub)
            {
                case "list": CmdPresetList(cfg); break;
                case "current":
                    Print($"current preset = {cfg.Settings.CurrentPreset}");
                    break;
                case "create":
                case "new":
                    if (args.Count < 2) { Print("Usage: hsr preset create <name>"); return; }
                    if (FindPresetName(args[1]) != null)
                    {
                        Print($"Preset '{args[1]}' already exists. Use 'hsr preset list'.");
                        return;
                    }
                    if (PresetStore.TryCreate(args[1], cfg, out string errorKey))
                        Print($"Preset '{PresetStore.SanitizeName(args[1])}' created and selected.");
                    else
                        Print("Preset creation failed: " + (errorKey ?? "unknown error"));
                    break;
                case "apply":
                case "load":
                    if (args.Count >= 2)
                    {
                        string actualName = FindPresetName(args[1]);
                        if (actualName == null)
                        {
                            Print($"Preset '{args[1]}' not found. Use 'hsr preset list'.");
                            return;
                        }
                        cfg.Settings.CurrentPreset = actualName;
                        cfg.SaveSettings();
                    }
                    if (PresetStore.LoadCurrent(cfg))
                        Print($"Applied preset '{cfg.Settings.CurrentPreset}'.");
                    else
                        Print($"Failed to apply preset '{cfg.Settings.CurrentPreset}'.");
                    break;
                case "save":
                    if (PresetStore.SaveToCurrent(cfg))
                        Print($"Saved current config to preset '{cfg.Settings.CurrentPreset}'.");
                    else
                        Print("Failed to save preset.");
                    break;
                case "delete":
                case "remove":
                    if (args.Count < 2) { Print("Usage: hsr preset delete <name>"); return; }
                    string target = FindPresetName(args[1]);
                    if (target == null)
                    {
                        Print($"Preset '{args[1]}' not found. Use 'hsr preset list'.");
                        return;
                    }
                    if (!string.Equals(target, cfg.Settings.CurrentPreset, StringComparison.OrdinalIgnoreCase))
                    {
                        cfg.Settings.CurrentPreset = target;
                        cfg.SaveSettings();
                    }
                    if (PresetStore.DeleteCurrent(cfg))
                        Print($"Deleted preset '{target}'; switched to '{cfg.Settings.CurrentPreset}'.");
                    else
                        Print("Preset deletion failed (default cannot be deleted).");
                    break;
                default:
                    Print("Usage: hsr preset [list|current|create <name>|apply [name]|save|delete <name>]");
                    break;
            }
        }

        private static void CmdPresetList(ConfigService cfg)
        {
            var names = PresetStore.ListPresets();
            var sb = new StringBuilder();
            foreach (var n in names)
            {
                sb.Append(n);
                if (string.Equals(n, cfg.Settings.CurrentPreset, StringComparison.OrdinalIgnoreCase))
                    sb.Append(" [current]");
                sb.AppendLine();
            }
            Print(sb.Length == 0 ? "No presets." : sb.ToString());
        }

        /// <summary>
        /// Resolve a preset name case-insensitively (the game console lowercases
        /// input before our handler runs) to the actual on-disk preset folder.
        /// </summary>
        private static string FindPresetName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            foreach (var n in PresetStore.ListPresets())
            {
                if (string.Equals(n, name, StringComparison.OrdinalIgnoreCase))
                    return n;
            }
            return null;
        }

        // ── subsegment ─────────────────────────────────────────────────────

        private static void CmdSub(List<string> args)
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            var sub = SubsegmentManager.Instance;
            if (sub == null) { Print("SubsegmentManager is not ready."); return; }
            string action = args.Count > 0 ? args[0].ToLowerInvariant() : "status";
            switch (action)
            {
                case "status":
                    var opts = sub.Options;
                    var sb = new StringBuilder();
                    sb.AppendLine($"enable={opts.Enable} multiRun={sub.InMultiRunActive} preserved={sub.InPreservedTransition}");
                    sb.AppendLine($"title=\"{sub.LeaderboardTitle}\" entries={sub.Entries.Count}");
                    sb.AppendLine($"pbPath={opts.PBPath} loadPath={opts.LoadPath} multiProject={opts.MultiProject}");
                    sb.AppendLine($"samplesPerLevelCap={opts.MaxSamplesPerLevel} quietSettle={opts.QuietSettleSeconds} debug={opts.DebugLogging}");
                    Print(sb.ToString());
                    break;
                case "entries":
                    var entries = sub.Entries;
                    if (entries.Count == 0)
                    {
                        Print("No subsegment leaderboard entries.");
                    }
                    else
                    {
                        var sb2 = new StringBuilder();
                        foreach (var e in entries)
                            sb2.AppendLine($"{e.DisplayId}: {(e.DiffMs.HasValue ? e.DiffMs.Value + " ms" : "no diff")}");
                        Print(sb2.ToString());
                    }
                    break;
                case "clear":
                    sub.OnRunReset();
                    Print("Cleared subsegment runtime state (no PB written).");
                    break;
                default:
                    Print("Usage: hsr sub [status|entries|clear]");
                    break;
            }
        }

        // ── markers ────────────────────────────────────────────────────────

        private static void CmdMarker(List<string> args)
        {
            var cfg = ConfigService.Instance;
            if (cfg == null) return;
            var mgr = MarkersManager.Instance;
            if (mgr == null) { Print("MarkersManager is not ready."); return; }
            if (args.Count == 0) { CmdMarkerList(); return; }

            string action = args[0].ToLowerInvariant();
            var rest = args.GetRange(1, args.Count - 1);
            switch (action)
            {
                case "list": CmdMarkerList(); break;
                case "feed": CmdMarkerFeed(); break;
                case "add": CmdMarkerAdd(mgr, rest); break;
                case "remove":
                    if (rest.Count < 1) { Print("Usage: hsr marker remove <id>"); return; }
                    CmdMarkerRemove(mgr, rest[0]);
                    break;
                case "toggle":
                    if (rest.Count < 1) { Print("Usage: hsr marker toggle <id>"); return; }
                    CmdMarkerToggle(mgr, rest[0]);
                    break;
                case "pb":
                    if (rest.Count < 1) { Print("Usage: hsr marker pb <total_ms>"); return; }
                    CmdMarkerSetPb(mgr, rest[0]);
                    break;
                case "pbclear":
                    CmdMarkerClearPb(mgr);
                    break;
                case "clear":
                    CmdMarkerClearAll(mgr);
                    break;
                case "save":
                    mgr.SaveAllDirty();
                    Print("Marker dirty sets saved.");
                    break;
                case "reload":
                    mgr.InvalidateAll();
                    Print("Marker cache reloaded from disk.");
                    break;
                default:
                    Print("Usage: hsr marker [list|feed|add ...|remove <id>|toggle <id>|pb <total_ms>|pbclear|clear|save|reload]");
                    break;
            }
        }

        private static void CmdMarkerList()
        {
            var set = CurrentMarkerSet();
            if (set == null)
            {
                Print("No active marker set (enter a level or enable markers).");
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine($"level=\"{set.level_id}\" category=\"{set.category_key}\" source={set.level_source} number={set.level_number}");
            sb.Append("PB: ");
            if (set.pb != null)
            {
                sb.Append(set.pb.total_ms).Append(" ms");
                if (set.pbTimes != null && set.pbTimes.Count > 0)
                    sb.Append(" (").Append(set.pbTimes.Count).Append(" marker times)");
                sb.AppendLine();
            }
            else
            {
                sb.AppendLine("none");
            }
            if (set.markers == null || set.markers.Count == 0)
            {
                sb.Append("markers: none");
            }
            else
            {
                sb.AppendLine("markers:");
                foreach (var m in set.markers)
                {
                    if (m == null) continue;
                    sb.Append("  ").Append(m.id).Append(" [").Append(m.enabled ? "on" : "off").Append("] ").Append(m.type);
                    sb.Append(" \"").Append(m.name).Append("\"");
                    switch (m.Kind)
                    {
                        case MarkerKind.Range:
                            sb.Append($" center=({m.cx},{m.cy},{m.cz}) size=({m.sx},{m.sy},{m.sz}) grab={m.requireGrab} jump={m.requireJump}");
                            break;
                        case MarkerKind.Checkpoint:
                            sb.Append($" checkpoint={m.checkpointIndex} load={m.triggerOnLoad}");
                            break;
                        case MarkerKind.GrabObject:
                            sb.Append($" object=\"{m.objectName}\" path=\"{m.objectPath}\" sceneId={m.objectSceneId}");
                            break;
                    }
                    sb.AppendLine();
                }
            }
            Print(sb.ToString());
        }

        private static void CmdMarkerFeed()
        {
            var mgr = MarkersManager.Instance;
            if (mgr == null || !mgr.HasFeedData)
            {
                Print("No marker feed data.");
                return;
            }
            var sb = new StringBuilder();
            sb.AppendLine("title=\"" + mgr.LeaderboardTitle + "\"");
            foreach (var row in mgr.Feed)
                sb.AppendLine($"{row.MarkerId}: \"{row.Name}\" at {row.TMs} ms");
            Print(sb.ToString());
        }

        private static void CmdMarkerAdd(MarkersManager mgr, List<string> args)
        {
            if (args.Count < 1) { Print("Usage: hsr marker add <range|checkpoint|grab> ..."); return; }
            string type = args[0].ToLowerInvariant();
            var set = CurrentMarkerSet();
            if (set == null)
            {
                Print("No active marker set (enter a level first).");
                return;
            }

            switch (type)
            {
                case "range":
                    CmdMarkerAddRange(set, args.GetRange(1, args.Count - 1));
                    break;
                case "checkpoint":
                    CmdMarkerAddCheckpoint(set, args.GetRange(1, args.Count - 1));
                    break;
                case "grab":
                case "grabobject":
                    CmdMarkerAddGrab(mgr, set, args.GetRange(1, args.Count - 1));
                    break;
                default:
                    Print("Unknown marker type: " + args[0]);
                    break;
            }
        }

        private static void CmdMarkerAddRange(MarkerSet set, List<string> args)
        {
            // Syntax: range <name> [cx cy cz sx sy sz] [grab] [jump]
            if (args.Count < 1)
            {
                Print("Usage: hsr marker add range <name> [cx cy cz sx sy sz] [grab] [jump]");
                return;
            }
            string name = args[0];
            var pos = Human.Localplayer != null && Human.Localplayer.transform != null
                ? Human.Localplayer.transform.position
                : Vector3.zero;
            float cx = pos.x, cy = pos.y, cz = pos.z;
            float sx = 2f, sy = 2f, sz = 2f;
            bool requireGrab = false, requireJump = false;
            int idx = 1;
            if (args.Count - idx >= 6)
            {
                if (!TryParseFloats(args, idx, 6, out float[] vals))
                {
                    Print("Range center/size must be numbers.");
                    return;
                }
                cx = vals[0]; cy = vals[1]; cz = vals[2]; sx = vals[3]; sy = vals[4]; sz = vals[5];
                idx += 6;
            }
            for (; idx < args.Count; idx++)
            {
                if (string.Equals(args[idx], "grab", StringComparison.OrdinalIgnoreCase)) requireGrab = true;
                else if (string.Equals(args[idx], "jump", StringComparison.OrdinalIgnoreCase)) requireJump = true;
            }

            var def = new MarkerDef
            {
                id = MarkersManager.NextMarkerId(set),
                name = name,
                type = "Range",
                enabled = true,
                cx = cx, cy = cy, cz = cz,
                sx = sx, sy = sy, sz = sz,
                requireGrab = requireGrab,
                requireJump = requireJump,
            };
            set.markers.Add(def);
            MarkersManager.Instance.MarkDirty(set);
            Print($"Added range marker {def.id} \"{def.name}\".");
        }

        private static void CmdMarkerAddCheckpoint(MarkerSet set, List<string> args)
        {
            // Syntax: checkpoint <name> <index> [load]
            if (args.Count < 2)
            {
                Print("Usage: hsr marker add checkpoint <name> <index> [load]");
                return;
            }
            if (!int.TryParse(args[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int cp))
            {
                Print("Checkpoint index must be an integer.");
                return;
            }
            bool load = args.Count > 2 && string.Equals(args[2], "load", StringComparison.OrdinalIgnoreCase);
            var def = new MarkerDef
            {
                id = MarkersManager.NextMarkerId(set),
                name = args[0],
                type = "Checkpoint",
                enabled = true,
                checkpointIndex = cp,
                triggerOnLoad = load,
            };
            set.markers.Add(def);
            MarkersManager.Instance.MarkDirty(set);
            Print($"Added checkpoint marker {def.id} \"{def.name}\" for cp={cp} load={load}.");
        }

        private static void CmdMarkerAddGrab(MarkersManager mgr, MarkerSet set, List<string> args)
        {
            if (args.Count < 1)
            {
                Print("Usage: hsr marker add grab <name>");
                return;
            }
            var def = new MarkerDef
            {
                id = MarkersManager.NextMarkerId(set),
                name = args[0],
                type = "GrabObject",
                enabled = true,
            };
            if (!mgr.TryCaptureGrabbedObject(def, out string errorKey))
            {
                Print("Grab capture failed: " + (errorKey ?? "unknown"));
                return;
            }
            set.markers.Add(def);
            mgr.MarkDirty(set);
            Print($"Added grab-object marker {def.id} \"{def.name}\" object=\"{def.objectName}\".");
        }

        private static void CmdMarkerRemove(MarkersManager mgr, string id)
        {
            var set = CurrentMarkerSet();
            if (set == null) { Print("No active marker set."); return; }
            var def = set.Find(id);
            if (def == null) { Print($"Marker {id} not found."); return; }
            set.markers.Remove(def);
            mgr.MarkDirty(set);
            Print($"Removed marker {id}.");
        }

        private static void CmdMarkerToggle(MarkersManager mgr, string id)
        {
            var set = CurrentMarkerSet();
            if (set == null) { Print("No active marker set."); return; }
            var def = set.Find(id);
            if (def == null) { Print($"Marker {id} not found."); return; }
            def.enabled = !def.enabled;
            mgr.MarkDirty(set);
            Print($"Marker {id} enabled = {def.enabled}.");
        }

        private static void CmdMarkerSetPb(MarkersManager mgr, string msText)
        {
            if (!long.TryParse(msText, NumberStyles.Integer, CultureInfo.InvariantCulture, out long ms))
            {
                Print("PB total must be an integer number of milliseconds.");
                return;
            }
            var set = CurrentMarkerSet();
            if (set == null) { Print("No active marker set."); return; }
            set.pb = new MarkerPb { total_ms = ms, created_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture) };
            if (set.pbTimes == null) set.pbTimes = new List<MarkerPbEntry>();
            set.pbTimes.Clear();
            mgr.MarkDirty(set);
            Print($"Set marker PB to {ms} ms.");
        }

        private static void CmdMarkerClearPb(MarkersManager mgr)
        {
            var set = CurrentMarkerSet();
            if (set == null) { Print("No active marker set."); return; }
            set.pb = null;
            if (set.pbTimes != null) set.pbTimes.Clear();
            mgr.MarkDirty(set);
            Print("Cleared marker PB.");
        }

        private static void CmdMarkerClearAll(MarkersManager mgr)
        {
            var set = CurrentMarkerSet();
            if (set == null) { Print("No active marker set."); return; }
            set.markers.Clear();
            mgr.MarkDirty(set);
            Print("Cleared all markers in current set.");
        }

        private static MarkerSet CurrentMarkerSet()
        {
            var mgr = MarkersManager.Instance;
            if (mgr == null) return null;
            var set = mgr.CurrentSet;
            if (set != null) return set;

            var game = Game.instance;
            if (game == null) return null;
            string key = LevelIdentity.CurrentLevelKey(game, folderFallbackForLocalWorkshop: true);
            if (string.IsNullOrEmpty(key)) return null;
            return mgr.GetOrCreateSet(key, game.currentLevelType.ToString(), game.currentLevelNumber, MarkerStore.CategoryKey());
        }

        // ── validity flags ─────────────────────────────────────────────────

        private static void CmdFlags(List<string> args)
        {
            var state = TimerCore.State;
            if (state == null) { Print("TimerCore state is not ready."); return; }
            if (args.Count == 0) { CmdFlagsList(state); return; }
            string action = args[0].ToLowerInvariant();
            switch (action)
            {
                case "list": CmdFlagsList(state); break;
                case "raise":
                    if (args.Count < 2) { Print("Usage: hsr flags raise <Reason>"); return; }
                    if (Enum.TryParse(args[1], true, out InvalidReason reason))
                    {
                        state.Flags.Raise(reason);
                        Print($"Raised {reason}.");
                    }
                    else
                    {
                        Print($"Unknown reason: {args[1]}. Valid: {string.Join(", ", Enum.GetNames(typeof(InvalidReason)))}");
                    }
                    break;
                case "clear":
                    string target = args.Count > 1 ? args[1].ToLowerInvariant() : "all";
                    switch (target)
                    {
                        case "forgivable": state.Flags.ClearForgivable(); Print("Cleared forgivable flags."); break;
                        case "soft": state.Flags.ClearSoftFlags(); Print("Cleared soft flags."); break;
                        case "all": state.Flags.ClearAll(); Print("Cleared all validity flags."); break;
                        default: Print("Usage: hsr flags clear [forgivable|soft|all]"); break;
                    }
                    break;
                default:
                    Print("Usage: hsr flags [list|raise <Reason>|clear [forgivable|soft|all]]");
                    break;
            }
        }

        private static void CmdFlagsList(RunState state)
        {
            var cfg = ConfigService.Instance;
            var loc = cfg != null ? cfg.Localization : null;
            var sb = new StringBuilder();
            string hard = state.Flags.FormatReasons(loc);
            sb.Append("hard: ").Append(hard.Length > 0 ? hard : "none").AppendLine();
            sb.Append("soft:");
            int n = 0;
            foreach (var soft in state.Flags.SoftFlags)
            {
                n++;
                sb.Append(' ').Append(InvalidReasons.LocalKey(soft.Reason)).Append(" x").Append(soft.Count);
            }
            if (n == 0) sb.Append(" none");
            Print(sb.ToString());
        }

        // ── LC / config paths ──────────────────────────────────────────────

        private static void CmdLc(List<string> args)
        {
            var lc = LcIntegration.Instance;
            if (lc == null || !lc.Enabled)
            {
                Print("LevelCollections integration is absent or disabled.");
                return;
            }
            string action = args.Count > 0 ? args[0].ToLowerInvariant() : "status";
            switch (action)
            {
                case "status":
                    Print($"enabled=true inRun={lc.IsInCollectionRun} lastLevel={lc.IsLastLevelOfCollection} delayed={lc.IsDelayedCommandPending} name=\"{lc.CollectionName}\"");
                    break;
                case "restart":
                    if (lc.RestartCollection())
                        Print("Dispatched LC restart.");
                    else
                        Print("LC restart refused (not in a run or delayed command pending).");
                    break;
                default:
                    Print("Usage: hsr lc [status|restart]");
                    break;
            }
        }

        private static void CmdConfig(List<string> args)
        {
            string action = args.Count > 0 ? args[0].ToLowerInvariant() : "path";
            switch (action)
            {
                case "path":
                case "dir":
                    Print("HSRTimer config dir: " + PersistenceService.PluginDir);
                    break;
                case "files":
                    Print("settings.ini: " + PersistenceService.PathFor("settings.ini"));
                    Print("tags.ini: " + PersistenceService.PathFor("tags.ini"));
                    Print("layout.ini: " + PersistenceService.PathFor("layout.ini"));
                    Print("lang dir: " + PersistenceService.LangDir);
                    Print("presets dir: " + PresetStore.RootDir);
                    break;
                default:
                    Print("Usage: hsr config [path|files]");
                    break;
            }
        }

        // ── update checker (R13) ─────────────────────────────────────────────

        /// <summary>
        /// R13.9: exercise the About page's update flow from the console. The
        /// check/download run asynchronously and log their result to the BepInEx
        /// log (headless read path); this command only kicks them off / prints state.
        /// </summary>
        private static void CmdUpdate(List<string> args)
        {
            var updater = UpdaterService.Instance;
            if (updater == null) { Print("UpdaterService is not ready."); return; }
            if (args.Count == 0) { Print("Usage: hsr update [status|check|apply|cancel|base [url]]"); return; }

            string action = args[0].ToLowerInvariant();
            switch (action)
            {
                case "status":
                    var sb = new StringBuilder();
                    sb.AppendLine($"phase={updater.Phase} busy={updater.IsBusy}");
                    sb.AppendLine($"base={updater.RepoBase} feed={updater.FeedUrl}");
                    sb.AppendLine($"upToDate={updater.UpToDate} checked={updater.CheckedVersion ?? "-"} installed={updater.InstalledVersion ?? "-"}");
                    if (updater.PendingRelease != null)
                    {
                        sb.AppendLine($"pending={updater.PendingRelease.Tag} \"{updater.PendingRelease.Title}\"");
                        sb.AppendLine($"releaseUrl={updater.PendingRelease.Url ?? "-"}");
                    }
                    if (!string.IsNullOrEmpty(updater.ErrorText))
                        sb.AppendLine($"error={updater.ErrorText}");
                    Print(sb.ToString());
                    break;
                case "check":
                    updater.CheckForUpdate();
                    Print("Update check started; result is logged to the BepInEx log.");
                    break;
                case "apply":
                    updater.ApplyUpdate();
                    Print("Update apply started; result is logged to the BepInEx log.");
                    break;
                case "cancel":
                    updater.Cancel();
                    Print("Update operation cancelled.");
                    break;
                case "base":
                case "endpoint": // legacy alias
                    if (args.Count >= 2)
                    {
                        string url = string.Join(" ", args.GetRange(1, args.Count - 1));
                        if (string.Equals(url, "clear", StringComparison.OrdinalIgnoreCase))
                            url = "";
                        updater.SetRepoBaseOverride(url);
                    }
                    Print("update repo base = " + updater.RepoBase + (updater.RepoBaseOverride != null ? " (session override)" : ""));
                    break;
                default:
                    Print("Usage: hsr update [status|check|apply|cancel|base [url]]");
                    break;
            }
        }

        // ── reflection helpers ─────────────────────────────────────────────

        private static bool TryFindField(string key, out FieldInfo field, out object owner)
        {
            field = null;
            owner = null;
            var cfg = ConfigService.Instance;
            if (cfg == null) return false;
            string norm = Normalize(key);
            foreach (var kv in KeyAliases)
            {
                if (Normalize(kv.Key) == norm)
                {
                    norm = Normalize(kv.Value);
                    break;
                }
            }
            foreach (var f in SettingsFields)
            {
                if (Normalize(f.Name) == norm)
                {
                    field = f;
                    owner = cfg.Settings;
                    return true;
                }
            }
            foreach (var f in LayoutFields)
            {
                if (f.FieldType == typeof(List<RowType>) || f.FieldType == typeof(List<CustomText>))
                    continue;
                if (Normalize(f.Name) == norm)
                {
                    field = f;
                    owner = cfg.Layout;
                    return true;
                }
            }
            return false;
        }

        private static bool TrySetField(FieldInfo field, object owner, string value, out string error)
        {
            error = null;
            try
            {
                Type t = field.FieldType;
                if (t == typeof(bool))
                {
                    field.SetValue(owner, SettingsModel.ParseBool(value, (bool)field.GetValue(owner)));
                    return true;
                }
                if (t == typeof(int))
                {
                    field.SetValue(owner, SettingsModel.ParseInt(value, (int)field.GetValue(owner)));
                    return true;
                }
                if (t == typeof(float))
                {
                    field.SetValue(owner, SettingsModel.ParseFloat(value, (float)field.GetValue(owner)));
                    return true;
                }
                if (t == typeof(KeyCode))
                {
                    field.SetValue(owner, SettingsModel.ParseKeyCode(value, (KeyCode)field.GetValue(owner)));
                    return true;
                }
                if (t == typeof(Color))
                {
                    field.SetValue(owner, GradientText.ParseColor(value, (Color)field.GetValue(owner)));
                    return true;
                }
                if (t == typeof(string))
                {
                    field.SetValue(owner, value);
                    return true;
                }
                error = "unsupported field type " + t.Name;
                return false;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private static string FormatFieldValue(FieldInfo field, object owner)
        {
            try
            {
                object value = field.GetValue(owner);
                if (value == null) return "";
                if (value is Color c) return GradientText.ToHex(c);
                if (value is bool b) return b ? "true" : "false";
                return value.ToString();
            }
            catch
            {
                return "?";
            }
        }

        private static string FieldName(string fieldName)
        {
            // Preserve the familiar settings.ini style for common fields: just
            // snake_case; reflection field names are used as the canonical key.
            var sb = new StringBuilder();
            for (int i = 0; i < fieldName.Length; i++)
            {
                char ch = fieldName[i];
                if (i > 0 && char.IsUpper(ch) && !char.IsUpper(fieldName[i - 1]))
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(ch));
            }
            return sb.ToString();
        }

        private static string Normalize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c))
                    sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        // ── parsing / printing helpers ─────────────────────────────────────

        private static List<string> SplitArgs(string args)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(args)) return result;
            var current = new StringBuilder();
            bool inQuote = false;
            for (int i = 0; i < args.Length; i++)
            {
                char c = args[i];
                if (c == '"')
                {
                    inQuote = !inQuote;
                }
                else if (c == ' ' && !inQuote)
                {
                    if (current.Length > 0)
                    {
                        result.Add(current.ToString());
                        current.Length = 0;
                    }
                }
                else
                {
                    current.Append(c);
                }
            }
            if (current.Length > 0)
                result.Add(current.ToString());
            return result;
        }

        private static bool TryParseFloats(List<string> args, int start, int count, out float[] values)
        {
            values = new float[count];
            if (start + count > args.Count) return false;
            for (int i = 0; i < count; i++)
            {
                if (!float.TryParse(args[start + i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]))
                    return false;
            }
            return true;
        }

        private static string FormatNumber(double value)
            => value.ToString("0.###", CultureInfo.InvariantCulture);

        private static string FormatNullable(double? value)
            => value.HasValue ? FormatNumber(value.Value) : "-";

        private static void Print(string message)
        {
            if (string.IsNullOrEmpty(message))
                return;
            try
            {
                // Always mirror command output to the BepInEx log; the in-game
                // Shell may be absent or not visible, and headless/CI runs read
                // LogOutput.log instead of the game console.
                Plugin.Logger.LogInfo("HSRTimer[console]: " + message);
                if (Shell.instance != null)
                    Shell.Print(message);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: console print failed: {ex.Message}");
            }
        }

        // ── help text ──────────────────────────────────────────────────────

        private static void PrintSummary()
        {
            var sb = new StringBuilder();
            sb.AppendLine("HSRTimer console commands. Use 'hsr help <topic>' for details.");
            sb.AppendLine("  hsr status | keys | get <key> | set <key> <value> | reload | save");
            sb.AppendLine("  hsr reset | retry");
            sb.AppendLine("  hsr hud [on|off|toggle|status] | panel [open|close|toggle|status]");
            sb.AppendLine("  hsr leaderboard [cycle|show|hide|mode <Subsegment|Markers>|status]");
            sb.AppendLine("  hsr layout [status|row ...|text ...]");
            sb.AppendLine("  hsr tag [list|enable <id>|disable <id>|set <id> <on|off>]");
            sb.AppendLine("  hsr lang [list|set <code>|reload|current]");
            sb.AppendLine("  hsr preset [list|current|create <name>|apply [name]|save|delete <name>]");
            sb.AppendLine("  hsr sub [status|entries|clear]");
            sb.AppendLine("  hsr marker [list|feed|add ...|remove <id>|toggle <id>|pb <ms>|pbclear|clear|save|reload]");
            sb.AppendLine("  hsr flags [list|raise <Reason>|clear [forgivable|soft|all]]");
            sb.AppendLine("  hsr lc [status|restart] | hsr config [path|files]");
            sb.AppendLine("  hsr update [status|check|apply|cancel|base [url]] | hsr about");
            Print(sb.ToString());
        }

        private static string HelpFor(string topic)
        {
            switch (Normalize(topic))
            {
                case "hsr":
                case "hsrhelp":
                case "hsrtimer":
                    return "hsr <command> [args]\r\nHSRTimer in-game test/debug console.\r\nType 'hsr' for the command list or 'hsr help <topic>' for one command.";
                case "status":
                    return "hsr status\r\nPrint the full live state of the timer engine, config, tags, HUD, subsegment, markers, leaderboard, LC and config paths.";
                case "keys":
                    return "hsr keys\r\nList all settable settings.ini / layout.ini keys accepted by 'hsr get/set'.";
                case "get":
                    return "hsr get <key>\r\nPrint one config value. 'hsr get all' prints every config value.";
                case "set":
                    return "hsr set <key> <value>\r\nSet a config value and save. Boolean keys accept true/false/1/0/on/off/yes/no.";
                case "reload":
                    return "hsr reload\r\nRe-read all config files and language files from disk.";
                case "save":
                    return "hsr save\r\nSave the current in-memory config to disk.";
                case "reset":
                    return "hsr reset\r\nPerform the same full-run reset as the reset key (clears timers and all validity flags).";
                case "retry":
                    return "hsr retry\r\nPerform the same one-key retry as the retry key (R6).";
                case "hud":
                    return "hsr hud [on|off|toggle|status]\r\nShow/hide/toggle the timer HUD (show_hud).";
                case "panel":
                    return "hsr panel [open|close|toggle|status]\r\nOpen/close/toggle the IMGUI settings panel.";
                case "leaderboard":
                    return "hsr leaderboard [cycle|show|hide|mode <Subsegment|Markers>|status]\r\nControl the shared leaderboard HUD.";
                case "layout":
                    return "hsr layout [status|row <list|add <type>|remove <index>|clear>|text <list|add <x> <y> <text...>|remove <index>|clear>|get <key>|set <key> <value>]\r\nInspect/edit the HUD layout.";
                case "tag":
                    return "hsr tag [list|enable <id>|disable <id>|set <id> <on|off>]\r\nList available tag rules and toggle which tags are enabled (tags.ini).";
                case "lang":
                    return "hsr lang [list|set <code>|reload|current]\r\nList/change/reload the active language.";
                case "preset":
                    return "hsr preset [list|current|create <name>|apply [name]|save|delete <name>]\r\nManage layout/marker presets (R11).";
                case "sub":
                    return "hsr sub [status|entries|clear]\r\nInspect the subsegment module: options, leaderboard entries, or clear runtime state.";
                case "marker":
                    return "hsr marker [list|feed|add <range|checkpoint|grab> ...|remove <id>|toggle <id>|pb <total_ms>|pbclear|clear|save|reload]\r\nInspect/edit the current level's marker set.";
                case "flags":
                    return "hsr flags [list|raise <Reason>|clear [forgivable|soft|all]]\r\nInspect or mutate validity flags for testing (R5).";
                case "lc":
                    return "hsr lc [status|restart]\r\nInspect LevelCollections integration or dispatch 'lc restart'.";
                case "config":
                    return "hsr config [path|files]\r\nPrint HSRTimer config paths.";
                case "update":
                    return "hsr update [status|check|apply|cancel|base [url]]\r\nCheck the GitHub releases feed for a newer HSRTimer version and optionally download + install it (R13). 'base' sets a session-only repo-base URL override (testing; 'base clear' resets) — the feed and download URLs derive from it. Results are logged to the BepInEx log.";
                case "about":
                    return "hsr about\r\nPrint plugin name, version, license notice, and repository URL (the About page content).";
                default:
                    return "Unknown HSRTimer command topic: " + topic + ". Type 'hsr' for the command list.";
            }
        }
    }
}
