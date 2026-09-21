using System;
using System.IO;

namespace HSRTimer
{
    /// <summary>
    /// Layout + marker presets (R11). A preset is a folder under
    /// <c>&lt;config&gt;/HSRTimer/presets/</c> containing a <c>layout.ini</c> snapshot and a
    /// <c>markers/</c> snapshot (marker definitions + PB records).
    ///
    /// The currently selected preset is persisted as a normal config item,
    /// <c>[Presets] Current</c> in <c>settings.ini</c>. First-load / old-version upgrade
    /// detection is intentionally directory-based: when the <c>presets/</c> root or the
    /// <c>default</c> preset does not exist, the plugin creates <c>default</c> from the
    /// current config and selects it. There is no separate "presets initialized" flag.
    /// </summary>
    public static class PresetStore
    {
        public const string DefaultPresetName = "default";

        /// <summary>The presets root directory under the plugin config dir.</summary>
        public static string RootDir => Path.Combine(PersistenceService.PluginDir, "presets");

        /// <summary>Directory for a named preset (folder name is sanitized).</summary>
        public static string DirFor(string name)
            => Path.Combine(RootDir, SanitizeName(name ?? ""));

        /// <summary>
        /// Make a name safe for use as a directory name. The sanitized form is also
        /// the user-visible preset name, which keeps display/storage 1:1.
        /// </summary>
        public static string SanitizeName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "_";
            return SubsegmentFileStore.SanitizeId(name.Trim());
        }

        /// <summary>
        /// All preset folder names, with <c>default</c> always pinned to the top;
        /// the rest are in ordinal order.
        /// </summary>
        public static string[] ListPresets()
        {
            try
            {
                if (!Directory.Exists(RootDir))
                    return new string[0];
                var dirs = Directory.GetDirectories(RootDir);
                var names = new string[dirs.Length];
                for (int i = 0; i < dirs.Length; i++)
                    names[i] = Path.GetFileName(dirs[i]);
                Array.Sort(names, StringComparer.Ordinal);

                int defaultIndex = Array.IndexOf(names, DefaultPresetName);
                if (defaultIndex <= 0)
                    return names;

                var reordered = new string[names.Length];
                reordered[0] = names[defaultIndex];
                int j = 1;
                for (int i = 0; i < names.Length; i++)
                {
                    if (i == defaultIndex) continue;
                    reordered[j++] = names[i];
                }
                return reordered;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer[presets]: failed to list presets: {ex.Message}");
                return new string[0];
            }
        }

        public static bool Exists(string name)
            => !string.IsNullOrEmpty(name) && Directory.Exists(DirFor(name));

        /// <summary>
        /// Create <c>default</c> on first load / upgrade and repair the selected
        /// preset to a valid existing one. Called once from Plugin.Awake after config
        /// load/repair.
        /// </summary>
        public static void EnsureInitialized(ConfigService cfg)
        {
            if (cfg == null)
                return;

            try
            {
                Directory.CreateDirectory(RootDir);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer[presets]: failed to create presets directory: {ex.Message}");
                return;
            }

            if (!Exists(DefaultPresetName))
            {
                CreateSnapshot(DefaultPresetName, cfg);
                cfg.Settings.CurrentPreset = DefaultPresetName;
                cfg.SaveSettings();
                Plugin.Logger.LogInfo("HSRTimer[presets]: initialized default preset from current config.");
                return;
            }

            if (!Exists(cfg.Settings.CurrentPreset))
            {
                cfg.Settings.CurrentPreset = DefaultPresetName;
                cfg.SaveSettings();
                Plugin.Logger.LogInfo($"HSRTimer[presets]: selected preset '{cfg.Settings.CurrentPreset}' was missing; switched to default.");
            }
        }

        /// <summary>
        /// Create a new preset from the current config and select it. Returns false
        /// (with a localization key) when the name is empty or already exists.
        /// </summary>
        public static bool TryCreate(string name, ConfigService cfg, out string errorKey)
        {
            errorKey = null;
            if (cfg == null)
            {
                errorKey = "SETTINGS_PRESET_FAILED";
                return false;
            }

            string safe = SanitizeName(name ?? "");
            if (safe.Length == 0 || safe == "_")
            {
                errorKey = "SETTINGS_PRESET_EMPTY";
                return false;
            }
            if (Exists(safe))
            {
                errorKey = "SETTINGS_PRESET_DUPLICATE";
                return false;
            }

            CreateSnapshot(safe, cfg);
            cfg.Settings.CurrentPreset = safe;
            cfg.SaveSettings();
            return true;
        }

        /// <summary>Write the current live config into the currently selected preset.</summary>
        public static bool SaveToCurrent(ConfigService cfg)
        {
            if (cfg == null || !Exists(cfg.Settings.CurrentPreset))
                return false;
            CreateSnapshot(cfg.Settings.CurrentPreset, cfg);
            return true;
        }

        /// <summary>
        /// Apply the currently selected preset to the live layout + markers, then
        /// reload the in-memory models and marker runtime cache.
        /// </summary>
        public static bool LoadCurrent(ConfigService cfg)
        {
            if (cfg == null || !Exists(cfg.Settings.CurrentPreset))
                return false;

            string presetDir = DirFor(cfg.Settings.CurrentPreset);
            string layoutSrc = Path.Combine(presetDir, "layout.ini");
            string layoutDst = PersistenceService.PathFor("layout.ini");
            try
            {
                if (File.Exists(layoutSrc))
                    File.Copy(layoutSrc, layoutDst, true);
                else
                    Plugin.Logger.LogWarning($"HSRTimer[presets]: preset '{cfg.Settings.CurrentPreset}' has no layout.ini; keeping current layout.");
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer[presets]: failed to restore layout.ini from preset: {ex.Message}");
                return false;
            }

            if (!RestoreMarkersFrom(presetDir))
                Plugin.Logger.LogWarning($"HSRTimer[presets]: failed to restore markers from preset '{cfg.Settings.CurrentPreset}'.");

            // Refresh in-memory layout from the newly written live file.
            cfg.Layout.Load();

            var mgr = MarkersManager.Instance;
            if (mgr != null)
                mgr.InvalidateAll();

            // The Markers tab also caches a MarkerSet/level page; make it re-read
            // from the restored files instead of editing a stale in-memory set.
            MarkersPanel.InvalidateCache();

            return true;
        }

        /// <summary>
        /// Delete the currently selected preset (never <c>default</c>) and switch
        /// selection back to <c>default</c>.
        /// </summary>
        public static bool DeleteCurrent(ConfigService cfg)
        {
            if (cfg == null)
                return false;
            string name = cfg.Settings.CurrentPreset;
            if (string.Equals(name, DefaultPresetName, StringComparison.OrdinalIgnoreCase) || !Exists(name))
                return false;

            try
            {
                Directory.Delete(DirFor(name), true);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer[presets]: failed to delete preset '{name}': {ex.Message}");
                return false;
            }

            cfg.Settings.CurrentPreset = DefaultPresetName;
            cfg.SaveSettings();
            return true;
        }

        // ── snapshot internals ────────────────────────────────────────────

        private static void CreateSnapshot(string name, ConfigService cfg)
        {
            string dir = DirFor(name);
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer[presets]: failed to create preset directory '{dir}': {ex.Message}");
                return;
            }

            try
            {
                cfg.Layout.SaveTo(Path.Combine(dir, "layout.ini"));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer[presets]: failed to write layout.ini into preset '{name}': {ex.Message}");
            }

            // Flush any in-memory marker edits before snapshotting the markers dir.
            var mgr = MarkersManager.Instance;
            if (mgr != null)
                mgr.SaveAllDirty();
            CopyMarkersTo(Path.Combine(dir, "markers"));
        }

        private static void CopyMarkersTo(string destDir)
        {
            string src = MarkerStore.MarkersDir();
            CopyDirectoryOverwrite(src, destDir, clearDest: true);
        }

        private static bool RestoreMarkersFrom(string presetDir)
        {
            string src = Path.Combine(presetDir, "markers");
            string dest = MarkerStore.MarkersDir();
            if (string.IsNullOrEmpty(dest))
                return false;
            CopyDirectoryOverwrite(src, dest, clearDest: true);
            return true;
        }

        /// <summary>
        /// Recursively copy <paramref name="sourceDir"/> into <paramref name="destDir"/>,
        /// optionally clearing the destination first so snapshots are exact (no stale
        /// files survive a restore/save).
        /// </summary>
        private static void CopyDirectoryOverwrite(string sourceDir, string destDir, bool clearDest)
        {
            try
            {
                if (clearDest && Directory.Exists(destDir))
                    DeleteDirectoryContents(destDir);

                Directory.CreateDirectory(destDir);
                if (string.IsNullOrEmpty(sourceDir) || !Directory.Exists(sourceDir))
                    return;

                foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
                {
                    // Directory.GetFiles returns paths rooted at sourceDir, so a
                    // simple substring gives the relative path (avoids depending on
                    // Path.GetRelativePath, which is absent on this Unity/.NET build).
                    string relative = file.Substring(sourceDir.Length)
                        .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    string target = Path.Combine(destDir, relative);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(file, target, true);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer[presets]: directory copy failed ('{sourceDir}' -> '{destDir}'): {ex.Message}");
            }
        }

        private static void DeleteDirectoryContents(string dir)
        {
            foreach (var file in Directory.GetFiles(dir))
                File.Delete(file);
            foreach (var sub in Directory.GetDirectories(dir))
                Directory.Delete(sub, true);
        }
    }
}
