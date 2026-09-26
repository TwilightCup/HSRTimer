namespace HSRTimer
{
    /// <summary>
    /// Label-only category tags (R3.10). Unlike rule tags (R3.3-R3.9), a label
    /// tag carries no judgment logic and no validity flag: it is never
    /// registered as an <see cref="ITagRule"/>, so the engine's rule loop
    /// (<see cref="TimerCore.ForEachEnabledRule"/> via
    /// <see cref="TagRuleRegistry"/>) skips it and the settings panel's
    /// Category page (which lists only registered rules) never shows it.
    ///
    /// Labels are auto-managed by <see cref="TimerCore.SyncAutoLabels"/>: the
    /// engine adds/removes them from the enabled set at runtime to follow the
    /// live game mode, and <see cref="Config.EnabledTagsModel"/> never persists
    /// them to tags.ini. When enabled they flow through the normal enabled-tag
    /// surfaces (HUD tags line, {category} template variable, category keys).
    /// </summary>
    public static class TagLabels
    {
        /// <summary>
        /// Co-op label (R3.10): enabled only while a multiplayer session is
        /// active (<c>NetGame.isServer</c> or <c>NetGame.isClient</c>).
        /// </summary>
        public const string Coop = "Co-op";

        /// <summary>All label tag ids, in display order.</summary>
        public static readonly string[] All = { Coop };

        /// <summary>True when <paramref name="tagId"/> is a label tag (not a rule tag).</summary>
        public static bool IsLabel(string tagId) => tagId == Coop;

        /// <summary>Localization key for a label tag's display name, or null.</summary>
        public static string DisplayNameKey(string tagId)
        {
            if (tagId == Coop) return "TAG_COOP";
            return null;
        }
    }
}
