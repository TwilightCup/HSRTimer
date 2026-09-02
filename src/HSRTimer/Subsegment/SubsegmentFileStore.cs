using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// File I/O for subsegment samples (R8.2 / R8.3.3). The loader is strict
    /// about rejecting malformed/missing files, but the failures are contained
    /// so the timer and any other loaded references are never affected.
    /// </summary>
    public static class SubsegmentFileStore
    {
        public const int FormatVersion = 1;

        public static string SanitizeId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "_";
            var sb = new StringBuilder(id.Length);
            foreach (var c in id)
                sb.Append((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.' || c == '_' || c == '-' ? c : '_');
            return sb.ToString();
        }

        public static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (Path.IsPathRooted(path)) return path;
            return Path.Combine(PersistenceService.PluginDir, path);
        }

        /// <summary>Write a text file atomically: temp file + rename (R8.2.7.3).</summary>
        public static bool WriteAtomic(string path, string content)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                if (File.Exists(path))
                {
                    try
                    {
                        File.Replace(tmp, path, null);
                    }
                    catch
                    {
                        // Some Mono/Unity setups do not implement File.Replace on
                        // the target filesystem. Fall back to the closest safe
                        // replacement (delete + move).
                        File.Delete(path);
                        File.Move(tmp, path);
                    }
                }
                else
                {
                    File.Move(tmp, path);
                }
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: subsegment atomic write failed for {path}: {ex.Message}");
                try { if (File.Exists(path + ".tmp")) File.Delete(path + ".tmp"); } catch { }
                return false;
            }
        }

        public static bool TryLoadMeta(string path, out SubsegmentMeta meta)
        {
            meta = null;
            if (!File.Exists(path))
                return false;
            try
            {
                meta = JsonUtility.FromJson<SubsegmentMeta>(File.ReadAllText(path, Encoding.UTF8));
                if (meta == null || meta.format_version != FormatVersion)
                {
                    Plugin.Logger.LogWarning($"HSRTimer: subsegment meta '{path}' missing or unsupported format_version; skipped.");
                    meta = null;
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to parse meta '{path}': {ex.Message}");
                meta = null;
                return false;
            }
        }

        public static string MakeMetaJson(
            string project,
            string levelId,
            string subproject,
            string categoryKey,
            string[] levelIds,
            long totalMs,
            int sampleCount)
        {
            var meta = new SubsegmentMeta
            {
                format_version = FormatVersion,
                project = project,
                level_id = levelId,
                subproject = subproject,
                category_key = categoryKey,
                level_ids = levelIds,
                total_ms = totalMs,
                sample_count = sampleCount,
                hsrtimer_version = PluginInfo.PLUGIN_VERSION,
                created_at = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            };
            return JsonUtility.ToJson(meta, true);
        }

        public static string MakeSampleJson(IEnumerable<SubsegmentSample> samples)
        {
            var sb = new StringBuilder();
            foreach (var s in samples)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append(JsonUtility.ToJson(s));
            }
            sb.Append('\n');
            return sb.ToString();
        }

        /// <summary>
        /// Load one JSONL sample file into an ordered list. A single malformed
        /// line is skipped with a warning naming the file and line number; the
        /// rest of the file is still used (R8.2.7.1).
        /// </summary>
        public static bool TryLoadSamples(string path, out List<SubsegmentSample> samples)
        {
            samples = new List<SubsegmentSample>();
            if (!File.Exists(path)) return false;
            try
            {
                string[] lines = File.ReadAllLines(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                for (int i = 0; i < lines.Length; i++)
                {
                    string line = lines[i].Trim();
                    if (line.Length == 0) continue;
                    try
                    {
                        var sample = JsonUtility.FromJson<SubsegmentSample>(line);
                        if (sample != null)
                            samples.Add(sample);
                    }
                    catch (Exception ex)
                    {
                        Plugin.Logger.LogWarning($"HSRTimer: subsegment {path}({i + 1}): bad sample line skipped: {ex.Message}");
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to read subsegment samples '{path}': {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Try to read an existing PB's total_ms so a new PB is only written when
        /// it actually beats it (strictly less than, per R8.3.1.2 / R8.3.2.5).
        /// </summary>
        public static bool TryReadTotalMs(string metaPath, out long totalMs)
        {
            totalMs = 0;
            if (!TryLoadMeta(metaPath, out var meta) || meta == null)
                return false;
            totalMs = meta.total_ms;
            return true;
        }
    }
}
