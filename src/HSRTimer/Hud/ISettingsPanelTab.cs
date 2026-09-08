namespace HSRTimer
{
    /// <summary>
    /// A settings-panel tab contributed by another BepInEx plugin that uses
    /// Unity IMGUI for its configuration UI (R9). HSRTimer shows the tab's
    /// <see cref="Title"/> in the settings panel's left navigation and calls
    /// <see cref="Draw"/> while the tab is active, inside HSRTimer's own IMGUI
    /// window and scroll view. Each plugin may register at most one tab (see
    /// <see cref="SettingsPanelTabRegistry"/>). To persist your own config when
    /// HSRTimer saves, subscribe to
    /// <see cref="SettingsPanelTabRegistry.SettingsSaved"/> (R9.3); to follow
    /// HSRTimer's language selection, implement
    /// <see cref="ILocalizableSettingsPanelTab"/> (R9.2).
    /// </summary>
    public interface ISettingsPanelTab
    {
        /// <summary>
        /// Tab title shown in HSRTimer's settings panel. May return a localized
        /// string that changes with the plugin's own language settings. When
        /// implementing <see cref="ILocalizableSettingsPanelTab"/>, use
        /// <see cref="ILocalizableSettingsPanelTab.SetLanguage"/> to update it.
        /// </summary>
        string Title { get; }

        /// <summary>
        /// Draw the tab's IMGUI content. Use <c>GUILayout.*</c>/<c>GUI.*</c>
        /// exactly as you would inside any other Unity IMGUI panel.
        /// </summary>
        void Draw();
    }
}
