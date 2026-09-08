using System;
using System.Collections.Generic;
using BepInEx;

namespace HSRTimer
{
    /// <summary>
    /// Registry of settings-panel tabs contributed by other BepInEx plugins
    /// (R9). Each plugin (identified by its BepInEx plugin GUID) may register at
    /// most one tab; a second registration for the same GUID is rejected and
    /// logged. Registered tabs appear as extra pages at the bottom of HSRTimer's
    /// settings panel navigation, after the built-in pages.
    ///
    /// Tabs that implement <see cref="ILocalizableSettingsPanelTab"/> also
    /// participate in HSRTimer's language switching (R9.2): the registry pushes
    /// the active language (or English when the tab does not support it) to the
    /// tab at registration time and on every language change.
    /// </summary>
    public sealed class SettingsPanelTabRegistry
    {
        private const string EnglishCode = "en";

        private static SettingsPanelTabRegistry _instance;

        /// <summary>
        /// The shared registry. Created lazily so plugins can register a tab
        /// even before HSRTimer's own Awake has run (e.g. without declaring a
        /// hard BepInEx dependency on HSRTimer).
        /// </summary>
        public static SettingsPanelTabRegistry Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new SettingsPanelTabRegistry();
                return _instance;
            }
            private set => _instance = value;
        }

        /// <summary>
        /// Install the registry instance used by the settings panel. No-op when
        /// a registry already exists (a lazily-created one may already hold tabs
        /// registered by other plugins before HSRTimer's Awake).
        /// </summary>
        public static void Init(SettingsPanelTabRegistry instance)
        {
            if (_instance == null && instance != null)
                Instance = instance;
        }

        private readonly Dictionary<string, ISettingsPanelTab> _tabsByPluginGuid =
            new Dictionary<string, ISettingsPanelTab>();
        private readonly List<ISettingsPanelTab> _tabsInOrder = new List<ISettingsPanelTab>();

        /// <summary>
        /// Last language code pushed to localizable tabs. Used to avoid calling
        /// <see cref="ILocalizableSettingsPanelTab.SetLanguage"/> every OnGUI
        /// frame while the language is unchanged.
        /// </summary>
        private string _lastLanguageCode;

        /// <summary>
        /// Raised after HSRTimer persists its configuration (R9.3). External
        /// plugins subscribe to persist their own config at the same moments:
        /// settings-panel Save/Close, game exit, and any internal HSRTimer
        /// auto-save. Handlers are called one by one; an exception in one
        /// handler is logged and does not prevent HSRTimer from saving.
        /// </summary>
        public event Action SettingsSaved;

        /// <summary>All registered external tabs, in registration order.</summary>
        public IEnumerable<ISettingsPanelTab> Tabs => _tabsInOrder;

        /// <summary>Number of registered external tabs.</summary>
        public int Count => _tabsInOrder.Count;

        /// <summary>
        /// Register the given tab for the calling plugin (identified by its
        /// BepInEx plugin GUID). Returns false (and logs) when the plugin GUID is
        /// missing or a tab is already registered for that plugin. A tab that
        /// implements <see cref="ILocalizableSettingsPanelTab"/> is immediately
        /// sent the active language (or English when unsupported) via
        /// <see cref="ILocalizableSettingsPanelTab.SetLanguage"/>.
        /// </summary>
        public bool Register(string pluginGuid, ISettingsPanelTab tab)
        {
            if (tab == null)
            {
                LogWarning("HSRTimer: ignored null settings panel tab registration.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(pluginGuid))
            {
                LogWarning("HSRTimer: ignored settings panel tab registration without a plugin GUID.");
                return false;
            }
            if (_tabsByPluginGuid.ContainsKey(pluginGuid))
            {
                LogWarning($"HSRTimer: settings panel tab already registered for plugin '{pluginGuid}'; ignoring duplicate.");
                return false;
            }

            _tabsByPluginGuid[pluginGuid] = tab;
            _tabsInOrder.Add(tab);

            // A localizable tab needs to know the active language immediately,
            // even before the settings panel is opened for the first time.
            var localizable = tab as ILocalizableSettingsPanelTab;
            if (localizable != null)
            {
                WarnIfEnglishMissing(pluginGuid, localizable);
                NotifyTabLanguage(pluginGuid, localizable, CurrentLanguageCode());
            }

            LogInfo($"HSRTimer: registered settings panel tab '{TabTitle(tab)}' for plugin '{pluginGuid}'.");
            return true;
        }

        /// <summary>
        /// Convenience overload that derives the plugin GUID from a
        /// <see cref="BaseUnityPlugin"/> instance. In <c>Awake</c> pass
        /// <c>this</c>.
        /// </summary>
        public bool Register(BaseUnityPlugin owner, ISettingsPanelTab tab)
        {
            string guid = owner != null && owner.Info != null && owner.Info.Metadata != null
                ? owner.Info.Metadata.GUID
                : null;
            return Register(guid, tab);
        }

        /// <summary>
        /// Push the given HSRTimer language code to every registered tab that
        /// implements <see cref="ILocalizableSettingsPanelTab"/>. Tabs receive
        /// the code itself when they support it, or English (<c>"en"</c>)
        /// otherwise (R9.2). Calling this repeatedly with the same code is
        /// cheap: the callback is skipped until the code actually changes.
        /// </summary>
        public void NotifyLanguageChanged(string languageCode)
        {
            if (string.IsNullOrEmpty(languageCode))
                languageCode = EnglishCode;
            if (string.Equals(_lastLanguageCode, languageCode, StringComparison.OrdinalIgnoreCase))
                return;

            _lastLanguageCode = languageCode;
            foreach (var tab in _tabsInOrder)
            {
                var localizable = tab as ILocalizableSettingsPanelTab;
                if (localizable != null)
                    NotifyTabLanguage(null, localizable, languageCode);
            }
        }

        /// <summary>
        /// Raise <see cref="SettingsSaved"/> for every subscriber. HSRTimer's
        /// <see cref="ConfigService"/> calls this after it writes its config
        /// files (R9.3). Each handler is isolated: one plugin's exception is
        /// logged and the remaining handlers still run.
        /// </summary>
        public void NotifySettingsSaved()
        {
            var handlers = SettingsSaved;
            if (handlers == null)
                return;

            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception ex)
                {
                    string owner = handler.Method != null && handler.Method.DeclaringType != null
                        ? handler.Method.DeclaringType.FullName
                        : "unknown";
                    LogWarning($"HSRTimer: SettingsSaved handler '{owner}' threw: {ex.Message}");
                }
            }
        }

        /// <summary>Look up a tab by its owning plugin GUID (null if unknown).</summary>
        public ISettingsPanelTab Find(string pluginGuid)
        {
            ISettingsPanelTab tab;
            return pluginGuid != null && _tabsByPluginGuid.TryGetValue(pluginGuid, out tab) ? tab : null;
        }

        /// <summary>Active HSRTimer language, or English before config is ready.</summary>
        private static string CurrentLanguageCode()
        {
            var cfg = ConfigService.Instance;
            if (cfg == null || cfg.Localization == null)
                return EnglishCode;
            string code = cfg.Localization.CurrentCode;
            return string.IsNullOrEmpty(code) ? EnglishCode : code;
        }

        /// <summary>Send the best supported language code to one localizable tab.</summary>
        private static void NotifyTabLanguage(string pluginGuid, ILocalizableSettingsPanelTab tab, string requestedCode)
        {
            string effectiveCode = ResolveLanguage(tab, requestedCode);
            try
            {
                tab.SetLanguage(effectiveCode);
            }
            catch (Exception ex)
            {
                string owner = string.IsNullOrEmpty(pluginGuid) ? tab.GetType().Name : pluginGuid;
                LogWarning($"HSRTimer: settings panel tab '{owner}' threw in SetLanguage('{effectiveCode}'): {ex.Message}");
            }
        }

        /// <summary>
        /// Resolve the language code a tab should receive: the requested code
        /// when supported, otherwise the tab's English code, otherwise
        /// <c>"en"</c>.
        /// </summary>
        private static string ResolveLanguage(ILocalizableSettingsPanelTab tab, string requestedCode)
        {
            if (string.IsNullOrEmpty(requestedCode))
                requestedCode = EnglishCode;

            string[] supported = GetSupportedLanguages(tab);
            if (supported == null || supported.Length == 0)
                return EnglishCode;

            string requestedMatch = null;
            string englishMatch = null;
            for (int i = 0; i < supported.Length; i++)
            {
                string code = supported[i];
                if (string.IsNullOrEmpty(code))
                    continue;
                if (requestedMatch == null && string.Equals(code, requestedCode, StringComparison.OrdinalIgnoreCase))
                    requestedMatch = code;
                if (englishMatch == null && string.Equals(code, EnglishCode, StringComparison.OrdinalIgnoreCase))
                    englishMatch = code;
            }

            if (requestedMatch != null)
                return requestedMatch;
            return englishMatch ?? EnglishCode;
        }

        /// <summary>Copy the tab's supported languages into an array, logging on failure.</summary>
        private static string[] GetSupportedLanguages(ILocalizableSettingsPanelTab tab)
        {
            try
            {
                var languages = tab.SupportedLanguages;
                if (languages == null)
                    return null;

                var list = new List<string>();
                foreach (var code in languages)
                {
                    if (!string.IsNullOrEmpty(code))
                        list.Add(code);
                }
                return list.ToArray();
            }
            catch (Exception ex)
            {
                LogWarning($"HSRTimer: settings panel tab '{tab.GetType().Name}' threw in SupportedLanguages: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Contract check (R9.2.2): a localizable tab must ship English as its
        /// base language. HSRTimer still uses <c>"en"</c> as the fallback for
        /// languages the tab does not support when it is missing.
        /// </summary>
        private static void WarnIfEnglishMissing(string pluginGuid, ILocalizableSettingsPanelTab tab)
        {
            string[] supported = GetSupportedLanguages(tab);
            if (supported != null)
            {
                for (int i = 0; i < supported.Length; i++)
                {
                    if (string.Equals(supported[i], EnglishCode, StringComparison.OrdinalIgnoreCase))
                        return;
                }
            }

            LogWarning($"HSRTimer: settings panel tab for plugin '{pluginGuid}' must declare English (\"en\") in SupportedLanguages; falling back to \"en\".");
        }

        private static string TabTitle(ISettingsPanelTab tab)
        {
            string title = tab.Title;
            return string.IsNullOrEmpty(title) ? tab.GetType().Name : title;
        }

        private static void LogWarning(string message)
        {
            if (Plugin.Logger != null)
                Plugin.Logger.LogWarning(message);
        }

        private static void LogInfo(string message)
        {
            if (Plugin.Logger != null)
                Plugin.Logger.LogInfo(message);
        }
    }
}
