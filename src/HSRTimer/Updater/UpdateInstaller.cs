using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace HSRTimer
{
    /// <summary>
    /// Filesystem half of the updater (R13.6): installs a freshly downloaded
    /// plugin DLL into the plugins folder, replaces/retires older versioned
    /// copies, and cleans up leftovers on startup. Pure file logic with no
    /// Unity dependencies so the failure modes are easy to reason about and
    /// exercise from the console.
    ///
    /// Locked-file strategy: the currently loaded DLL cannot always be deleted
    /// on Windows. On delete failure the file is renamed to BepInEx's
    /// ".dis" convention (which disables loading at next start); if even the
    /// rename is refused the path is recorded in the cleanup marker file
    /// <c>HSRTimer.update-cleanup.txt</c> and retried on the next launch. Only
    /// files matching <c>HSRTimer-v*.dll</c> are ever touched — other plugins'
    /// DLLs are left alone.
    /// </summary>
    internal static class UpdateInstaller
    {
        private const string CleanupMarkerName = "HSRTimer.update-cleanup.txt";

        /// <summary>Does this filename look like one of our versioned plugin DLLs?</summary>
        public static bool IsOurVersionedDll(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return false;
            if (!fileName.StartsWith("HSRTimer-v", StringComparison.OrdinalIgnoreCase))
                return false;
            if (!fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                return false;
            // Guard against e.g. "HSRTimer-victim.dll" — require a version segment.
            string middle = fileName.Substring("HSRTimer-v".Length, fileName.Length - "HSRTimer-v".Length - ".dll".Length);
            return VersionUtil.TryParse(middle, out _);
        }

        /// <summary>
        /// Move a validated download into place and retire older copies.
        /// </summary>
        /// <param name="pluginDir">Directory that holds the versioned plugin DLLs.</param>
        /// <param name="tempPath">Validated downloaded file (will be moved/removed).</param>
        /// <param name="assetName">Final filename, e.g. "HSRTimer-v1.6.0.dll".</param>
        /// <param name="error">Human-readable failure reason (raw text, wrapped in a localized
        /// template by the caller), or null on success.</param>
        /// <returns>True when the new DLL is in place (old copies may be pending cleanup).</returns>
        public static bool Install(string pluginDir, string tempPath, string assetName, out string error)
        {
            error = null;
            try
            {
                if (string.IsNullOrEmpty(pluginDir) || !Directory.Exists(pluginDir))
                {
                    error = "plugin folder does not exist";
                    return false;
                }

                // Security: only ever install a filename that is exactly one of our
                // own versioned names — this rejects path traversal ("..\...") and
                // any non-HSRTimer asset from a compromised release.
                string safeName = Path.GetFileName(assetName);
                if (!IsOurVersionedDll(safeName))
                {
                    error = "unsupported release asset name: " + assetName;
                    return false;
                }
                string finalPath = Path.Combine(pluginDir, safeName);

                // Validate the download before touching anything: non-empty and a
                // loadable .NET assembly. Catches truncated/corrupt downloads and
                // a redirect that served HTML instead of the DLL.
                if (!File.Exists(tempPath))
                {
                    error = "downloaded file is missing";
                    return false;
                }
                long size = new FileInfo(tempPath).Length;
                if (size <= 0)
                {
                    error = "downloaded file is empty (0 bytes)";
                    return false;
                }
                try
                {
                    AssemblyName.GetAssemblyName(tempPath);
                }
                catch (Exception ex)
                {
                    error = "downloaded file is not a valid plugin DLL (" + ex.GetType().Name + ")";
                    return false;
                }

                // Place the new file (netstandard2.0 has no File.Move overwrite
                // overload; the target is our new name and never the loaded file).
                if (File.Exists(finalPath))
                {
                    try { File.Delete(finalPath); }
                    catch (Exception ex) { error = "cannot overwrite existing file: " + ex.Message; return false; }
                }
                File.Move(tempPath, finalPath);

                // Retire every other versioned copy. Delete first; on failure try
                // the BepInEx ".dis" rename (never loaded at next start); on failure
                // defer to next-launch cleanup.
                var deferred = new List<string>();
                foreach (string old in Directory.GetFiles(pluginDir, "HSRTimer-v*.dll"))
                {
                    if (string.Equals(old, finalPath, StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (TryRemove(old))
                        continue;
                    deferred.Add(old);
                }
                if (deferred.Count > 0)
                    WriteCleanupMarker(pluginDir, deferred);

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>Try to get rid of a stale versioned DLL: delete, else rename to .dis.</summary>
        private static bool TryRemove(string path)
        {
            try { File.Delete(path); return true; }
            catch { /* locked / antivirus / permissions */ }
            try
            {
                string disabled = path + ".dis";
                if (File.Exists(disabled))
                    File.Delete(disabled);
                File.Move(path, disabled);
                return true;
            }
            catch { }
            return false;
        }

        private static void WriteCleanupMarker(string pluginDir, List<string> paths)
        {
            try
            {
                File.WriteAllLines(Path.Combine(pluginDir, CleanupMarkerName), paths);
                Plugin.Logger.LogWarning($"HSRTimer: {paths.Count} stale plugin DLL(s) could not be removed now; will retry at next launch (marker '{CleanupMarkerName}').");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to write update cleanup marker: {ex.Message}");
            }
        }

        /// <summary>
        /// Startup housekeeping (R13.6): delete files listed in the cleanup marker,
        /// leftover interrupted-download temps, and retired ".dis" backups. Idempotent;
        /// every step tolerates failure independently and logs a warning.
        /// </summary>
        public static void CleanupStartup(string pluginDir)
        {
            if (string.IsNullOrEmpty(pluginDir) || !Directory.Exists(pluginDir))
                return;

            int removed = 0;
            var remaining = new List<string>();

            // 1. Files explicitly deferred from a previous session's install.
            string markerPath = Path.Combine(pluginDir, CleanupMarkerName);
            if (File.Exists(markerPath))
            {
                string[] lines;
                try { lines = File.ReadAllLines(markerPath); }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"HSRTimer: cleanup marker unreadable, ignoring: {ex.Message}");
                    lines = new string[0];
                }
                foreach (string raw in lines)
                {
                    string path = raw == null ? null : raw.Trim();
                    if (string.IsNullOrEmpty(path))
                        continue;
                    // Safety: the marker only ever lists our own versioned DLLs, and
                    // only inside the plugin folder — never follow arbitrary paths
                    // a (possibly stale/tampered) marker might name.
                    string full;
                    try { full = Path.GetFullPath(path); }
                    catch { continue; }
                    if (!string.Equals(Path.GetDirectoryName(full), Path.GetFullPath(pluginDir), StringComparison.OrdinalIgnoreCase))
                    {
                        Plugin.Logger.LogWarning($"HSRTimer: skipping cleanup of out-of-folder file: {path}");
                        continue;
                    }
                    if (IsOurVersionedDll(Path.GetFileName(full)))
                    {
                        if (TryRemove(full))
                            removed++;
                        else
                            remaining.Add(full);
                    }
                }
                // 2. Retired ".dis" copies and interrupted ".tmp" downloads.
            }

            foreach (string tmp in Directory.GetFiles(pluginDir, "HSRTimer-v*.dll.tmp"))
                if (TryRemove(tmp)) removed++;

            foreach (string dis in Directory.GetFiles(pluginDir, "HSRTimer-v*.dll.dis"))
                if (TryRemove(dis)) removed++;

            // Drop the marker only when nothing is left pending.
            if (File.Exists(markerPath))
            {
                try
                {
                    if (remaining.Count == 0)
                        File.Delete(markerPath);
                    else
                        WriteCleanupMarker(pluginDir, remaining);
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning($"HSRTimer: failed to finalize cleanup marker: {ex.Message}");
                }
            }

            if (removed > 0 || remaining.Count > 0)
                Plugin.Logger.LogInfo($"HSRTimer: startup plugin cleanup: removed {removed} file(s), {remaining.Count} still pending.");
        }

        /// <summary>
        /// Name (basename) of the newest "HSRTimer-v*.dll" in the folder by version,
        /// or null when there is none / none parses. Used for the startup diagnostic.
        /// </summary>
        public static string FindNewestVersionedDll(string pluginDir)
        {
            if (string.IsNullOrEmpty(pluginDir) || !Directory.Exists(pluginDir))
                return null;
            string newest = null;
            VersionUtil.SemVer newestVer = default;
            bool have = false;
            foreach (string path in Directory.GetFiles(pluginDir, "HSRTimer-v*.dll"))
            {
                string name = Path.GetFileName(path);
                if (VersionUtil.TryParse(name.Substring("HSRTimer-v".Length, name.Length - "HSRTimer-v".Length - ".dll".Length), out VersionUtil.SemVer v))
                {
                    if (!have || VersionUtil.Compare(v, newestVer) > 0)
                    {
                        have = true;
                        newest = name;
                        newestVer = v;
                    }
                }
            }
            return newest;
        }
    }
}
