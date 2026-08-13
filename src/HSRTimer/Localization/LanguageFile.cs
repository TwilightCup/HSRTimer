using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace HSRTimer
{
    /// <summary>
    /// One parsed localization entry: key, value (with \n expanded), source line.
    /// </summary>
    public readonly struct LangEntry
    {
        public readonly string Key;
        public readonly string Value;
        public readonly int LineNo;

        public LangEntry(string key, string value, int lineNo)
        {
            Key = key;
            Value = value;
            LineNo = lineNo;
        }
    }

    /// <summary>
    /// Parser for one localization file (R7.3). Format per line:
    /// <code>KEY:Translation</code>
    /// Rules: '#' lines are comments; blank lines ignored; the value is
    /// everything after the first ':'; a literal '\n' escape becomes a newline;
    /// keys must match <c>^[A-Z][A-Z0-9_]*$</c>. UTF-8 (no BOM) assumed;
    /// '\n' and '\r\n' line endings both tolerated. Malformed lines are skipped
    /// with a logged warning naming the file and line number (N6 / R7.5.2).
    /// </summary>
    public static class LanguageFile
    {
        private static readonly Regex KeyPattern = new Regex(@"^[A-Z][A-Z0-9_]*$");

        /// <summary>Parse a file into entries. Returns empty if the file is missing.</summary>
        public static IEnumerable<LangEntry> Parse(string filePath, out string displayName)
        {
            displayName = null;
            var result = new List<LangEntry>();
            if (!File.Exists(filePath))
                return result;

            string shortName = Path.GetFileName(filePath);
            List<string> lines;
            try
            {
                string[] raw = File.ReadAllLines(filePath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                lines = new List<string>(raw);
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to read {shortName}: {ex.Message}");
                return result;
            }

            return ParseLines(lines, shortName, out displayName);
        }

        /// <summary>Parse an in-memory list of lines (file or embedded resource).</summary>
        public static IEnumerable<LangEntry> ParseLines(IList<string> lines, string sourceName, out string displayName)
        {
            displayName = null;
            var result = new List<LangEntry>();
            if (lines == null) return result;

            for (int i = 0; i < lines.Count; i++)
            {
                int lineNo = i + 1;
                string raw = lines[i];
                string line = raw.Trim();

                if (line.Length == 0)
                    continue;
                if (line[0] == '#')
                    continue;

                int colon = line.IndexOf(':');
                if (colon <= 0)
                {
                    Plugin.Logger.LogWarning($"HSRTimer: {sourceName}({lineNo}): expected 'KEY:Translation', skipping.");
                    continue;
                }

                string key = line.Substring(0, colon);
                string value = Unescape(line.Substring(colon + 1));

                if (!KeyPattern.IsMatch(key))
                {
                    Plugin.Logger.LogWarning($"HSRTimer: {sourceName}({lineNo}): invalid key '{key}' (must match [A-Z][A-Z0-9_]*), skipping.");
                    continue;
                }

                if (key == "__LANG_NAME__")
                {
                    displayName = value;
                    continue;
                }

                result.Add(new LangEntry(key, value, lineNo));
            }

            return result;
        }

        /// <summary>Expand literal '\n' / '\t' / '\\' escapes in a translation value.</summary>
        public static string Unescape(string s)
        {
            if (string.IsNullOrEmpty(s) || s.IndexOf('\\') < 0)
                return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c == '\\' && i + 1 < s.Length)
                {
                    char n = s[i + 1];
                    if (n == 'n') { sb.Append('\n'); i++; continue; }
                    if (n == 't') { sb.Append('\t'); i++; continue; }
                    if (n == '\\') { sb.Append('\\'); i++; continue; }
                    if (n == ':') { sb.Append(':'); i++; continue; }
                }
                sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
