using BepInEx;
using BepInEx.Logging;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// BepInEx plugin entry point. Wires every subsystem: loads config, registers
    /// the built-in tag rules, applies the voiceline Harmony patches, initializes
    /// the optional LevelCollections integration, and spawns the engine + HUD
    /// singletons. Declares LevelCollections as a soft dependency so the plugin
    /// loads and runs fine without it.
    /// </summary>
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInDependency("LevelCollections", BepInEx.BepInDependency.DependencyFlags.SoftDependency)]
    public class Plugin : BaseUnityPlugin
    {
        internal static new ManualLogSource Logger;

        private void Awake()
        {
            Logger = base.Logger;
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} v{PluginInfo.PLUGIN_VERSION} is loaded!");

            // 0. Updater housekeeping (R13.6): clear leftovers from a previous
            //    session's plugin update (deferred deletions, interrupted-download
            //    temps, retired ".dis" copies) and warn if the loaded copy is not
            //    the newest one on disk (a stale plugin may have won the load).
            string pluginDir = System.IO.Path.GetDirectoryName(Info.Location) ?? "";
            try
            {
                UpdateInstaller.CleanupStartup(pluginDir);
                string newest = UpdateInstaller.FindNewestVersionedDll(pluginDir);
                string currentName = System.IO.Path.GetFileName(Info.Location);
                if (newest != null
                    && !string.Equals(newest, currentName, System.StringComparison.OrdinalIgnoreCase))
                {
                    Logger.LogWarning($"HSRTimer: newest plugin on disk is '{newest}' but the loaded copy is '{currentName}'. An update may not have taken effect — remove the stale HSRTimer-v*.dll file(s) and restart if needed.");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"HSRTimer: startup plugin cleanup failed: {ex.Message}");
            }

            // 1. Config + localization. Seed the runtime lang dir first (so any
            //    on-disk defaults are picked up by the scan), then load. Note:
            //    the English base + example translation are also embedded in the
            //    DLL, so localization works even if the lang/ folder is absent.
            var config = new ConfigService();
            ConfigService.Init(config);

            // 1.5 Settings-panel tab extension point (R9). Initialize it before
            //     config repair below, which may call SaveSettings and must reach
            //     external SettingsSaved handlers. Init preserves a registry that
            //     another plugin lazily created before this Awake.
            SettingsPanelTabRegistry.Init(new SettingsPanelTabRegistry());
            config.SettingsSaved += SettingsPanelTabRegistry.Instance.NotifySettingsSaved;

            EnsureDefaultLangFiles();
            config.Load();
            // Detect & fill in missing/incorrect config items before any
            // subsystem reads them (e.g. insert a newly-defaulted HUD row into
            // an existing layout.ini). Idempotent; writes only on change.
            ConfigRepair.Run(config);

            // 1.6 Presets (R11): create the default preset on first load / upgrade
            //     and repair the selected preset to a valid one. Runs after config
            //     load/repair so the default snapshot reflects the effective config.
            PresetStore.EnsureInitialized(config);

            // 2. Register built-in tag rules (R3.7 extension point).
            var registry = new TagRuleRegistry();
            TagRuleRegistry.Init(registry);
            registry.Register(new CheckpointTagRule());
            registry.Register(new NoCheckpointTagRule());
            registry.Register(new JumplessTagRule());
            registry.Register(new VoicelineTagRule());
            registry.Register(new GlitchlessTagRule());
            registry.Register(new NoEcTagRule());

            // 3. Harmony patches (voiceline hooks only).
            PatchModule.Apply();

            // 4. Optional LevelCollections integration (reflection; no-op if absent).
            LcIntegration.Init();

            // 4.5 Language-aware settings tabs: tell them the active language now
            //     that config is loaded. Tabs registered later receive it from
            //     SettingsPanelTabRegistry.Register.
            SettingsPanelTabRegistry.Instance.NotifyLanguageChanged(config.Localization.CurrentCode);

            // 5. Engine + HUD + settings-panel singletons, persistent across scene loads.
            var engineGo = new GameObject("HSRTimer.Core");
            Object.DontDestroyOnLoad(engineGo);
            engineGo.AddComponent<TimerCore>();

            var hudGo = new GameObject("HSRTimer.Hud");
            Object.DontDestroyOnLoad(hudGo);
            hudGo.AddComponent<TimerHud>();
            hudGo.AddComponent<ProgressIndicatorMover>();

            var panelGo = new GameObject("HSRTimer.Panel");
            Object.DontDestroyOnLoad(panelGo);
            panelGo.AddComponent<SettingsPanel>();

            // 5.5 Updater service (R13): the About page's Check Update button and
            //     the 'hsr update ...' console commands share this one state machine.
            var updaterGo = new GameObject("HSRTimer.Updater");
            Object.DontDestroyOnLoad(updaterGo);
            var updater = updaterGo.AddComponent<UpdaterService>();
            updater.Init(pluginDir, PluginInfo.PLUGIN_VERSION);

            // 6. Subsegment module (R8): recorder/loader/comparator + leaderboard HUD.
            var subGo = new GameObject("HSRTimer.Subsegment");
            Object.DontDestroyOnLoad(subGo);
            subGo.AddComponent<SubsegmentManager>();
            subGo.AddComponent<LeaderboardHud>();

            // 7. Markers module (R10): trigger engine + 3D edit-mode overlay.
            var markersGo = new GameObject("HSRTimer.Markers");
            Object.DontDestroyOnLoad(markersGo);
            markersGo.AddComponent<MarkersManager>();
            markersGo.AddComponent<MarkerOverlay>();

            // 8. In-game dev console: register the "hsr ..." commands with the
            //     game's Shell console (~ / F1) so every feature can be inspected
            //     and tested from inside the game. Safe before Shell.instance exists.
            ConsoleCommands.Register();
        }

        /// <summary>
        /// Copy the shipped default language files (en.txt, zh-Hans.txt) into the
        /// runtime lang dir on first run so the plugin is usable out of the box
        /// and English is always present as the fallback base.
        /// </summary>
        private void EnsureDefaultLangFiles()
        {
            string dir = PersistenceService.LangDir;
            try
            {
                System.IO.Directory.CreateDirectory(dir);
                CopyIfMissing("en.txt");
                CopyIfMissing("zh-Hans.txt");
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"HSRTimer: failed to seed default lang files: {ex.Message}");
            }
        }

        private void CopyIfMissing(string name)
        {
            string dst = System.IO.Path.Combine(PersistenceService.LangDir, name);
            if (System.IO.File.Exists(dst)) return;
            // Defaults live next to the built DLL (csproj CopyToOutputDirectory).
            string src = System.IO.Path.Combine(BepInEx.Paths.PluginPath, PluginInfo.PLUGIN_GUID, "lang", name);
            if (!System.IO.File.Exists(src))
                src = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Info.Location) ?? "", "lang", name);
            if (!System.IO.File.Exists(src)) return;
            try { System.IO.File.Copy(src, dst, overwrite: false); }
            catch (System.Exception ex) { Logger.LogWarning($"HSRTimer: copy {name} failed: {ex.Message}"); }
        }
    }
}
