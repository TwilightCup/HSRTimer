using System.Globalization;
using System.Text;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// Resolves template variables in custom-text content (R2.4.2): {date},
    /// {time}, {version}, {collection}, {category}. Unknown tokens are left
    /// intact. Date/time use the current culture; collection/category come from
    /// the live config and (for collection) the optional LC integration.
    /// </summary>
    public static class TemplateVars
    {
        public static string Resolve(string text, double gameTime)
        {
            if (string.IsNullOrEmpty(text) || text.IndexOf('{') < 0)
                return text;

            var sb = new StringBuilder(text.Length + 16);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c != '{')
                {
                    sb.Append(c);
                    continue;
                }
                int close = text.IndexOf('}', i + 1);
                if (close < 0)
                {
                    sb.Append(c);
                    continue;
                }
                string token = text.Substring(i + 1, close - i - 1);
                sb.Append(ResolveToken(token, gameTime));
                i = close;
            }
            return sb.ToString();
        }

        private static string ResolveToken(string token, double gameTime)
        {
            var cfg = ConfigService.Instance;
            switch (token)
            {
                case "date":
                    return System.DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                case "time":
                    return System.DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
                case "version":
                    return PluginInfo.PLUGIN_VERSION;
                case "gametime":
                    return TimeFormatter.Format(gameTime);
                case "collection":
                    return CollectionName(cfg);
                case "category":
                    return CategoryName(cfg);
                default:
                    return "{" + token + "}";
            }
        }

        private static string CollectionName(ConfigService cfg)
        {
            string name = LcIntegration.Instance != null ? LcIntegration.Instance.CollectionName : null;
            if (!string.IsNullOrEmpty(name)) return name;
            if (cfg != null) return cfg.Localization.Get("TEMPLATE_NO_COLLECTION");
            return "(no collection)";
        }

        /// <summary>
        /// There are no category presets; {category} renders the enabled tags
        /// (localized), or a fallback when none are enabled. Public so the HUD
        /// can reuse it for its "current rule tags" line.
        /// </summary>
        public static string CategoryName(ConfigService cfg)
        {
            if (cfg == null || cfg.EnabledTags == null || cfg.EnabledTags.Tags.Count == 0)
                return cfg != null ? cfg.Localization.Get("TEMPLATE_NO_TAGS") : "(no tags)";
            var sb = new StringBuilder();
            foreach (var tagId in cfg.EnabledTags.Tags)
            {
                var rule = TagRuleRegistry.Instance != null ? TagRuleRegistry.Instance.Find(tagId) : null;
                string label = rule != null && !string.IsNullOrEmpty(rule.DisplayNameKey)
                    ? cfg.Localization.Get(rule.DisplayNameKey)
                    : tagId;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(label);
            }
            return sb.ToString();
        }
    }
}
