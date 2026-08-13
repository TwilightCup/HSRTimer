using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using BepInEx;

namespace HSRTimer
{
    /// <summary>
    /// A single parsed line of a config file, with its source location for
    /// error reporting (spec N6: report the offending line's number and file).
    /// </summary>
    public readonly struct ParsedLine
    {
        public readonly string File;
        public readonly int LineNo;
        public readonly string Section; // current [section], empty if none
        public readonly string Key;     // null for comments / blanks / bad lines
        public readonly string Value;

        public ParsedLine(string file, int lineNo, string section, string key, string value)
        {
            File = file;
            LineNo = lineNo;
            Section = section;
            Key = key;
            Value = value;
        }
    }

    /// <summary>
    /// Line-oriented, sectioned key=value reader/writer for all HSRTimer config
    /// files. Format:
    /// <code>
    /// # line comments
    /// [section]
    /// key = value
    /// </code>
    /// Malformed lines are skipped with a logged warning naming the file and
    /// line number; the rest of the file still loads (N6 robustness). Values are
    /// trimmed; blank lines and comment lines are ignored.
    /// </summary>
    public static class PersistenceService
    {
        /// <summary>The plugin's runtime directory under the BepInEx config root.</summary>
        public static string PluginDir => System.IO.Path.Combine(Paths.ConfigPath, PluginInfo.PLUGIN_GUID);

        public static string LangDir => System.IO.Path.Combine(PluginDir, "lang");

        public static string PathFor(string fileName) => System.IO.Path.Combine(PluginDir, fileName);

        /// <summary>Read all lines of a file as parsed entries; returns empty on missing file.</summary>
        public static IEnumerable<ParsedLine> Read(string filePath)
        {
            var result = new List<ParsedLine>();
            if (!File.Exists(filePath))
                return result;

            string shortName = System.IO.Path.GetFileName(filePath);
            string[] lines;
            try
            {
                lines = File.ReadAllLines(filePath, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to read {shortName}: {ex.Message}");
                return result;
            }

            string section = "";
            for (int i = 0; i < lines.Length; i++)
            {
                int lineNo = i + 1;
                string raw = lines[i];
                string line = raw.Trim();

                if (line.Length == 0)
                    continue;
                if (line[0] == '#' || line[0] == ';')
                    continue;

                if (line[0] == '[' && line[line.Length - 1] == ']')
                {
                    section = line.Substring(1, line.Length - 2).Trim();
                    continue;
                }

                int eq = line.IndexOf('=');
                if (eq <= 0)
                {
                    Plugin.Logger.LogWarning($"HSRTimer: {shortName}({lineNo}): expected 'key = value', skipping line.");
                    continue;
                }

                string key = line.Substring(0, eq).Trim();
                string value = line.Substring(eq + 1).Trim();
                if (key.Length == 0)
                {
                    Plugin.Logger.LogWarning($"HSRTimer: {shortName}({lineNo}): empty key, skipping line.");
                    continue;
                }

                result.Add(new ParsedLine(shortName, lineNo, section, key, value));
            }

            return result;
        }

        /// <summary>Write a sectioned file from a list of (section,key,value) tuples.</summary>
        public static void Write(string filePath, IEnumerable<KeyValuePair<string, IDictionary<string, string>>> sections, string headerComment = null)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath));
                var sb = new StringBuilder();
                if (!string.IsNullOrEmpty(headerComment))
                {
                    foreach (var c in headerComment.Split('\n'))
                        sb.Append("# ").Append(c.TrimEnd('\r')).Append('\n');
                    sb.Append('\n');
                }
                foreach (var section in sections)
                {
                    if (!string.IsNullOrEmpty(section.Key))
                        sb.Append('[').Append(section.Key).Append("]\n");
                    foreach (var kv in section.Value)
                        sb.Append(kv.Key).Append(" = ").Append(kv.Value).Append('\n');
                    sb.Append('\n');
                }
                File.WriteAllText(filePath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to write {System.IO.Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        /// <summary>Ensure the plugin runtime dir (and lang subdir) exist.</summary>
        public static void EnsureDirs()
        {
            try
            {
                Directory.CreateDirectory(PluginDir);
                Directory.CreateDirectory(LangDir);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to create config dirs: {ex.Message}");
            }
        }
    }
}
