namespace HSRTimer
{
    /// <summary>
    /// Static plugin identity constants used by the BepInEx plugin attribute
    /// and surfaced to the UI (e.g. the {version} template variable and the
    /// settings panel's About page).
    /// </summary>
    internal static class PluginInfo
    {
        public const string PLUGIN_GUID = "HSRTimer";
        public const string PLUGIN_NAME = "HSRTimer";
        public const string PLUGIN_VERSION = "1.7.1";

        /// <summary>Project repository, opened by the About page's link button (R12.4).</summary>
        public const string PLUGIN_REPOSITORY_URL = "https://github.com/TwilightCup/HSRTimer";

        // R12.3: first two non-empty lines of the repository LICENSE file.
        // License notices are legal text and intentionally not localized; keep
        // these in sync with LICENSE when the copyright line changes.
        public const string LICENSE_LINE1 = "MIT License";
        public const string LICENSE_LINE2 = "Copyright (c) 2026 TwilightCup";
    }
}
