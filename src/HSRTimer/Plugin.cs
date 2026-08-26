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

            // 1. Config + localization. Seed the runtime lang dir first (so any
            //    on-disk defaults are picked up by the scan), then load. Note:
            //    the English base + example translation are also embedded in the
            //    DLL, so localization works even if the lang/ folder is absent.
            var config = new ConfigService();
            ConfigService.Init(config);
            EnsureDefaultLangFiles();
            config.Load();
            // Detect & fill in missing/incorrect config items before any
            // subsystem reads them (e.g. insert a newly-defaulted HUD row into
            // an existing layout.ini). Idempotent; writes only on change.
            ConfigRepair.Run(config);

            // 2. Register built-in tag rules (R3.7 extension point).
            var registry = new TagRuleRegistry();
            TagRuleRegistry.Init(registry);
            registry.Register(new CheckpointTagRule());
            registry.Register(new NoCheckpointTagRule());
            registry.Register(new JumplessTagRule());
            registry.Register(new VoicelineTagRule());

            // 3. Harmony patches (voiceline hooks only).
            PatchModule.Apply();

            // 4. Optional LevelCollections integration (reflection; no-op if absent).
            LcIntegration.Init();

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
