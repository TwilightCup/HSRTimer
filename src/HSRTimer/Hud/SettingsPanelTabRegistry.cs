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
    /// </summary>
    public sealed class SettingsPanelTabRegistry
    {
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

        /// <summary>All registered external tabs, in registration order.</summary>
        public IEnumerable<ISettingsPanelTab> Tabs => _tabsInOrder;

        /// <summary>Number of registered external tabs.</summary>
        public int Count => _tabsInOrder.Count;

        /// <summary>
        /// Register the given tab for the calling plugin (identified by its
        /// BepInEx plugin GUID). Returns false (and logs) when the plugin GUID is
        /// missing or a tab is already registered for that plugin.
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

        /// <summary>Look up a tab by its owning plugin GUID (null if unknown).</summary>
        public ISettingsPanelTab Find(string pluginGuid)
        {
            ISettingsPanelTab tab;
            return pluginGuid != null && _tabsByPluginGuid.TryGetValue(pluginGuid, out tab) ? tab : null;
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
