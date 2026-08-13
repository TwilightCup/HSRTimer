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
