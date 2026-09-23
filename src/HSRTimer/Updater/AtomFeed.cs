using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace HSRTimer
{
    /// <summary>
    /// One release as parsed from the GitHub releases Atom feed (R13).
    /// </summary>
    public sealed class ReleaseInfo
    {
        /// <summary>Release tag, e.g. "v1.5.0" (used for version compare + download URL).</summary>
        public string Tag;

        /// <summary>Release title/name, e.g. "HSRTimer v1.5.0" (falls back to the tag).</summary>
        public string Title;

        /// <summary>Plain-text release summary: the release date + Highlights section,
        /// with the Details/Contributors sections trimmed off (R13.4).</summary>
        public string Body;

        /// <summary>GitHub release page URL, opened by the "Open Release Page" button.</summary>
        public string Url;
    }

    /// <summary>
    /// Parser for the GitHub releases Atom feed (<c>https://github.com/{owner}/{repo}/releases.atom</c>).
    /// The feed is served from github.com itself and — unlike the GitHub REST API — is not
    /// subject to the unauthenticated 60-requests/hour rate limit and keeps working in
    /// networks where api.github.com is blocked. Entries are ordered newest first; this
    /// parser returns the newest entry whose tag parses as a non-prerelease version.
    /// </summary>
    internal static class AtomFeed
    {
        private static readonly Regex HtmlTag = new Regex("<[^>]+>", RegexOptions.Compiled);
        // Block-level tags become line breaks; inline tags (strong/em/code/a/…) are
        // dropped entirely so words are not split across lines.
        private static readonly Regex HtmlBlockTag = new Regex(
            "<\\/?(?:p|div|li|ul|ol|tr|td|th|h[1-6]|br|hr|blockquote|pre)\\b[^>]*>",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// Parse the newest stable release from a feed document.
        /// </summary>
        /// <param name="xml">The raw Atom XML document.</param>
        /// <param name="release">The newest non-prerelease release, or null when none is found.</param>
        /// <param name="error">Human-readable reason when parsing fails entirely, else null.</param>
        public static bool TryParseLatest(string xml, out ReleaseInfo release, out string error)
        {
            release = null;
            error = null;
            if (string.IsNullOrWhiteSpace(xml))
            {
                error = "empty feed response";
                return false;
            }

            XmlDocument doc = new XmlDocument();
            try
            {
                doc.LoadXml(xml);
            }
            catch (Exception ex)
            {
                error = "feed is not valid XML (" + ex.GetType().Name + ")";
                return false;
            }

            // The Atom default namespace has no prefix, so qualified names are
            // just "entry"/"title"/"content"/"link" — GetElementsByTagName matches them.
            XmlNodeList entries = doc.GetElementsByTagName("entry");
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i] as XmlElement;
                if (entry == null)
                    continue;

                string link = ExtractReleaseLink(entry);
                if (string.IsNullOrEmpty(link))
                    continue;
                string tag = TagFromLink(link);
                if (string.IsNullOrEmpty(tag))
                    continue;
                if (!VersionUtil.TryParse(tag, out VersionUtil.SemVer version))
                    continue;
                if (!string.IsNullOrEmpty(version.Pre))
                    continue; // skip prereleases (mirrors /releases/latest semantics)

                release = new ReleaseInfo
                {
                    Tag = tag,
                    Title = FirstElementText(entry, "title") ?? tag,
                    Body = TrimToHighlights(StripHtml(FirstElementText(entry, "content"))),
                    Url = link,
                };
                return true;
            }

            error = "no stable release found in feed";
            return false;
        }

        /// <summary>Extract the release page URL from the entry's alternate link
        /// (…/releases/tag/{tag}), or null.</summary>
        private static string ExtractReleaseLink(XmlElement entry)
        {
            XmlNodeList links = entry.GetElementsByTagName("link");
            for (int i = 0; i < links.Count; i++)
            {
                var link = links[i] as XmlElement;
                if (link == null)
                    continue;
                string href = link.GetAttribute("href");
                if (string.IsNullOrEmpty(href))
                    continue;
                if (href.IndexOf("/releases/tag/", StringComparison.OrdinalIgnoreCase) >= 0)
                    return href;
            }
            return null;
        }

        /// <summary>Release tag parsed out of a release page link (…/releases/tag/{tag}).</summary>
        private static string TagFromLink(string href)
        {
            int idx = href.IndexOf("/releases/tag/", StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
                return null;
            string tag = href.Substring(idx + "/releases/tag/".Length).TrimEnd('/');
            return tag.Length > 0 ? tag : null;
        }

        private static string FirstElementText(XmlElement parent, string name)
        {
            XmlNodeList nodes = parent.GetElementsByTagName(name);
            if (nodes.Count == 0)
                return null;
            var el = nodes[0] as XmlElement;
            return el != null ? el.InnerText : null;
        }

        /// <summary>Rough HTML → plain text: block tags become line breaks, other
        /// tags are dropped, common entities decoded, blank runs collapsed.</summary>
        private static string StripHtml(string html)
        {
            if (string.IsNullOrEmpty(html))
                return null;
            string s = HtmlBlockTag.Replace(html, "\n");
            s = HtmlTag.Replace(s, "");
            s = s.Replace("&amp;", "&").Replace("&lt;", "<").Replace("&gt;", ">")
                 .Replace("&quot;", "\"").Replace("&#39;", "'").Replace("&nbsp;", " ");
            s = Regex.Replace(s, "[ \\t\\r]*\\n[ \\t\\r]*", "\n");
            s = Regex.Replace(s, "[ \\t]{2,}", " ");
            s = Regex.Replace(s, "\\n{2,}", "\n");
            return s.Trim();
        }

        /// <summary>
        /// Keep only the release date + Highlights part of the notes (R13.4): the
        /// project's release notes follow the CHANGELOG structure
        /// (Release Date → Highlights → Details → Contributors), and the panel only
        /// shows the short summary; everything from the Details/Contributors heading
        /// on is dropped. Bodies without those headings are returned unchanged.
        /// </summary>
        public static string TrimToHighlights(string body)
        {
            if (string.IsNullOrEmpty(body))
                return body;
            var sb = new StringBuilder();
            foreach (string raw in body.Split('\n'))
            {
                string trimmed = raw.Trim().TrimStart('*', '#').Trim();
                if (trimmed.StartsWith("Details:", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "Details", StringComparison.OrdinalIgnoreCase)
                    || trimmed.StartsWith("Contributors:", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(trimmed, "Contributors", StringComparison.OrdinalIgnoreCase))
                    break;
                sb.Append(raw).Append('\n');
            }
            return sb.ToString().Trim();
        }
    }
}
