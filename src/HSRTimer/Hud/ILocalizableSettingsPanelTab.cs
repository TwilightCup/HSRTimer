using System.Collections.Generic;

namespace HSRTimer
{
    /// <summary>
    /// Optional extension of <see cref="ISettingsPanelTab"/> for third-party
    /// settings tabs that want their configuration UI to follow HSRTimer's
    /// language selection (R9.2). Implement this interface instead of
    /// <see cref="ISettingsPanelTab"/> when the tab should switch language
    /// together with HSRTimer's settings panel.
    ///
    /// Contract:
    /// <list type="bullet">
    /// <item><description><see cref="SupportedLanguages"/> must list every
    /// BCP 47 language code the tab ships and must contain English
    /// (<c>"en"</c>). English is the mandatory base/fallback.</description></item>
    /// <item><description>HSRTimer calls <see cref="SetLanguage"/> once when the
    /// tab is registered and again whenever the active language changes. It
    /// passes the active HSRTimer code when the tab supports it; otherwise it
    /// passes <c>"en"</c>, so the tab always has a usable language.</description></item>
    /// <item><description>The tab must resolve its own UI keys with the same
    /// fallback order as HSRTimer (R7.5.3): active language → English base →
    /// key itself. <see cref="LanguageFile"/> can be used to parse files that
    /// follow HSRTimer's localization format.</description></item>
    /// </list>
    /// </summary>
    public interface ILocalizableSettingsPanelTab : ISettingsPanelTab
    {
        /// <summary>
        /// All language codes this tab can display, in BCP 47 form (for example
        /// <c>"en"</c>, <c>"zh-Hans"</c>, <c>"pt-BR"</c>). Must include
        /// <c>"en"</c> as the mandatory English base. HSRTimer logs a warning
        /// when it is missing and still uses <c>"en"</c> as the fallback
        /// language for languages the tab does not support.
        /// </summary>
        IEnumerable<string> SupportedLanguages { get; }

        /// <summary>
        /// Apply the language HSRTimer has selected. HSRTimer passes a code
        /// from <see cref="SupportedLanguages"/>, or <c>"en"</c> when the
        /// active language is not supported by this tab. All strings returned
        /// by <see cref="ISettingsPanelTab.Title"/> and drawn by
        /// <see cref="ISettingsPanelTab.Draw"/> must reflect the new language
        /// immediately (no restart or panel reopen required).
        /// </summary>
        void SetLanguage(string languageCode);
    }
}
