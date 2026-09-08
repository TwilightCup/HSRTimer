namespace HSRTimer
{
    /// <summary>
    /// Top-level service that owns all the user-editable models and wires
    /// hot-reload / save. Created once at plugin boot; subsystems read from the
    /// instance properties. Keeps concerns off the engine MonoBehaviour.
    /// </summary>
    public sealed class ConfigService
    {
        public readonly SettingsModel Settings = new SettingsModel();
        public readonly EnabledTagsModel EnabledTags = new EnabledTagsModel();
        public readonly LayoutModel Layout = new LayoutModel();
        public readonly LocalizationService Localization = new LocalizationService();

        public static ConfigService Instance { get; private set; }

        /// <summary>
        /// Raised after <see cref="SaveSettings"/> has written every config
        /// file. HSRTimer subscribes the settings-tab registry to this event so
        /// external tabs can persist their own config (R9.3); external plugins
        /// should subscribe to <c>SettingsPanelTabRegistry.SettingsSaved</c>
        /// instead of this internal event.
        /// </summary>
        internal event System.Action SettingsSaved;

        /// <summary>Load everything from disk; called once at boot.</summary>
        public void Load()
        {
            PersistenceService.EnsureDirs();
            Settings.Load();
            EnabledTags.Load();
            Layout.Load();
            Localization.Reload();
            Localization.SetLanguage(Settings.CurrentLang);
        }

        /// <summary>Re-read all files from disk without restarting (hot-reload).</summary>
        public void ReloadAll()
        {
            Settings.Load();
            EnabledTags.Load();
            Layout.Load();
            Localization.Reload();
            Localization.SetLanguage(Settings.CurrentLang);
        }

        /// <summary>Persist mutable settings back to disk.</summary>
        public void SaveSettings()
        {
            Settings.CurrentLang = Localization.CurrentCode;
            Settings.Save();
            EnabledTags.Save();
            Layout.Save();

            var saved = SettingsSaved;
            if (saved != null)
            {
                try { saved(); }
                catch (System.Exception ex)
                {
                    if (Plugin.Logger != null)
                        Plugin.Logger.LogWarning($"HSRTimer: config-saved handler threw: {ex.Message}");
                }
            }
        }

        /// <summary>Re-scan language files and re-apply the current language.</summary>
        public void ReloadLanguage()
        {
            Localization.Reload();
            Localization.SetLanguage(Settings.CurrentLang);
        }

        public static void Init(ConfigService instance) => Instance = instance;
    }
}
