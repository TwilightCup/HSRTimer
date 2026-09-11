using System;
using System.IO;
using HumanAPI;

namespace HSRTimer
{
    /// <summary>
    /// Shared, stateless helpers that derive stable per-level identifiers and
    /// display names from the game's runtime state (<c>Game</c>) or from the
    /// workshop metadata used by the settings panel.
    ///
    /// Two identification schemes coexist, matching R8.2.3:
    /// <list type="bullet">
    /// <item><b>IL key</b> (<see cref="CurrentLevelKey"/>) — BuiltIn / EditorPick
    /// use the English localized level name (e.g. <c>Intro</c>, <c>Power Plant</c>,
    /// <c>Aztec</c>), Workshop uses the raw numeric workshop id, LocalWorkshop
    /// falls back to the level folder name when the workshop id is 0. This is
    /// the stable identity used by marker data files and by the subsegment IL
    /// directories.</item>
    /// <item><b>ML machine id</b> (<see cref="MachineLevelId"/>) — the synthetic
    /// <c>B{n}</c>/<c>E{n}</c>/<c>W{...}</c> timeline ids used by the subsegment
    /// multi-run per-level files.</item>
    /// </list>
    ///
    /// <see cref="SubsegmentManager"/> delegates to these so its on-disk layout
    /// never drifts from the ids the markers module computes. The LocalWorkshop
    /// folder-name fallback is opt-in per caller: subsegment keeps the legacy
    /// <c>W{levelNumber}</c> rule (its PB directories predate this helper),
    /// while markers use the folder name so a marker created in the panel
    /// resolves to the same level key while playing.
    /// </summary>
    public static class LevelIdentity
    {
        /// <summary>Sanitize an id for use in file/directory names (R8.2.3).</summary>
        public static string SanitizeId(string id) => SubsegmentFileStore.SanitizeId(id);

        /// <summary>
        /// The English (not current-language) translation for a game localization
        /// term such as <c>LEVEL/Aztec</c>. Falls back to the game's current
        /// language translation, then to <paramref name="fallback"/>.
        /// </summary>
        public static string EnglishLevelName(string term, string fallback)
        {
            try
            {
                var codes = I2.Loc.LocalizationManager.GetLanguageCodes();
                var translations = I2.Loc.LocalizationManager.GetAllTranslationsForKey(term);
                if (codes != null && translations != null)
                {
                    for (int i = 0; i < codes.Count && i < translations.Count; i++)
                    {
                        string code = codes[i];
                        if (string.IsNullOrEmpty(code)) continue;
                        bool isEnglish = string.Equals(code, "English", StringComparison.OrdinalIgnoreCase)
                            || string.Equals(code, "en", StringComparison.OrdinalIgnoreCase)
                            || code.StartsWith("en-", StringComparison.OrdinalIgnoreCase)
                            || code.StartsWith("en_", StringComparison.OrdinalIgnoreCase);
                        if (isEnglish && !string.IsNullOrEmpty(translations[i])
                            && !translations[i].StartsWith("Missing:", StringComparison.OrdinalIgnoreCase))
                        {
                            return translations[i];
                        }
                    }
                }
                // Fallback to the game's current-language translation, or the
                // internal scene name if localization is not ready.
                string current = I2.Loc.ScriptLocalization.Get(term);
                return !string.IsNullOrEmpty(current) && !current.StartsWith("Missing:", StringComparison.OrdinalIgnoreCase)
                    ? current
                    : fallback;
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to resolve English level name for '{term}': {ex.Message}");
                return fallback;
            }
        }

        /// <summary>
        /// The IL identity of the level currently loaded in <paramref name="game"/>:
        /// English localized name for BuiltIn / EditorPick, numeric workshop id for
        /// Workshop, folder name (when enabled) for a LocalWorkshop level with no id.
        /// Always sanitized for use as a directory/file name.
        /// </summary>
        public static string CurrentLevelKey(Game game, bool folderFallbackForLocalWorkshop)
        {
            if (game == null) return null;
            switch (game.currentLevelType)
            {
                case WorkshopItemSource.BuiltIn:
                case WorkshopItemSource.EditorPick:
                    string fallback = game.currentLevelType == WorkshopItemSource.BuiltIn ? "B" : "E";
                    return OfficialLocalizedLevelKey(game, game.currentLevelNumber, fallback);
                case WorkshopItemSource.Subscription:
                case WorkshopItemSource.LocalWorkshop:
                    return WorkshopLevelKey(game, folderFallbackForLocalWorkshop);
                default:
                    return SanitizeId("L" + game.currentLevelNumber);
            }
        }

        /// <summary>
        /// The multi-run per-level (ML) machine id (R8.2.3): BuiltIn keeps
        /// <c>B{number}</c> timeline numbering, EditorPick <c>E{number}</c>,
        /// Workshop uses the raw numeric workshop id (falling back to
        /// <c>W{number}</c> — never the folder name, so the ML timeline layout is
        /// stable regardless of folder naming).
        /// </summary>
        public static string MachineLevelId(Game game)
        {
            if (game == null) return null;
            switch (game.currentLevelType)
            {
                case WorkshopItemSource.BuiltIn:
                    return SanitizeId("B" + game.currentLevelNumber);
                case WorkshopItemSource.EditorPick:
                    return SanitizeId("E" + game.currentLevelNumber);
                case WorkshopItemSource.Subscription:
                case WorkshopItemSource.LocalWorkshop:
                    return WorkshopLevelKey(game, folderFallbackForLocalWorkshop: false);
                default:
                    return SanitizeId("L" + game.currentLevelNumber);
            }
        }

        /// <summary>
        /// The workshop id for the level currently loaded in <paramref name="game"/>,
        /// or null when no usable numeric id exists.
        /// </summary>
        public static string WorkshopIdOrNull(Game game)
        {
            if (game == null || game.workshopLevel == null || game.workshopLevel.workshopId == 0UL)
                return null;
            return SanitizeId(game.workshopLevel.workshopId.ToString());
        }

        /// <summary>
        /// Compute the IL identity for a BuiltIn / EditorPick level from the
        /// workshop metadata, used by the settings-panel level list. Returns null
        /// when the metadata (and thus a reliable English name) is not available
        /// yet — the caller must not fabricate a key in that case.
        /// </summary>
        public static string MetadataLevelKey(WorkshopItemSource source, int index, WorkshopLevelMetadata meta)
        {
            switch (source)
            {
                case WorkshopItemSource.BuiltIn:
                case WorkshopItemSource.EditorPick:
                    var builtin = meta as BuiltinLevelMetadata;
                    string internalName = builtin != null && !string.IsNullOrEmpty(builtin.internalName)
                        ? builtin.internalName
                        : null;
                    if (string.IsNullOrEmpty(internalName)) return null;
                    string english = EnglishLevelName("LEVEL/" + internalName, internalName);
                    return SanitizeId(english);
                case WorkshopItemSource.Subscription:
                case WorkshopItemSource.LocalWorkshop:
                    if (meta != null && meta.workshopId != 0UL)
                        return SanitizeId(meta.workshopId.ToString());
                    if (meta != null && !string.IsNullOrEmpty(meta.folder))
                    {
                        string folderName = Path.GetFileName(meta.folder.TrimEnd('/', '\\'));
                        if (!string.IsNullOrEmpty(folderName))
                            return SanitizeId(folderName);
                    }
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// IL identity for a BuiltIn / EditorPick level at runtime: the English
        /// localized name, falling back to the synthetic numbered id.
        /// </summary>
        private static string OfficialLocalizedLevelKey(Game game, int levelNumber, string fallbackPrefix)
        {
            string internalName = OfficialInternalName(game, levelNumber);
            if (string.IsNullOrEmpty(internalName))
                return SanitizeId(fallbackPrefix + levelNumber);
            string english = EnglishLevelName("LEVEL/" + internalName, internalName);
            return SanitizeId(english);
        }

        /// <summary>
        /// Canonical internal name for a BuiltIn / EditorPick level. Prefers the
        /// level metadata's <c>internalName</c> (the key the <c>LEVEL/</c>
        /// localization terms are stored under) over the raw scene id from
        /// <c>Game.levels</c>/<c>Game.editorPickLevels</c> (which often has no
        /// localization term of its own, e.g. the Train scene id <c>Push</c>).
        /// </summary>
        public static string OfficialInternalName(Game game, int levelNumber)
        {
            var repo = WorkshopRepository.instance != null ? WorkshopRepository.instance.levelRepo : null;
            if (repo != null)
            {
                var items = repo.BySource(game.currentLevelType);
                if (items != null)
                {
                    foreach (var item in items)
                    {
                        if (item == null || item.workshopId != (ulong)levelNumber)
                            continue;
                        var builtin = item as BuiltinLevelMetadata;
                        if (builtin != null && !string.IsNullOrEmpty(builtin.internalName))
                            return builtin.internalName;
                    }
                }
            }

            if (game.currentLevelType == WorkshopItemSource.BuiltIn
                && game.levels != null
                && levelNumber >= 0
                && levelNumber < game.levels.Length)
            {
                return game.levels[levelNumber];
            }
            if (game.currentLevelType == WorkshopItemSource.EditorPick
                && game.editorPickLevels != null
                && levelNumber >= 0
                && levelNumber < game.editorPickLevels.Length)
            {
                return game.editorPickLevels[levelNumber];
            }
            return null;
        }

        /// <summary>
        /// Workshop level key: the numeric workshop id when present; for a
        /// LocalWorkshop level with no id, the level folder name when
        /// <paramref name="folderFallbackForLocalWorkshop"/> is true; otherwise
        /// the synthetic <c>W{levelNumber}</c>.
        /// </summary>
        private static string WorkshopLevelKey(Game game, bool folderFallbackForLocalWorkshop)
        {
            string id = WorkshopIdOrNull(game);
            if (!string.IsNullOrEmpty(id))
                return id;
            if (folderFallbackForLocalWorkshop && game.workshopLevel != null && !string.IsNullOrEmpty(game.workshopLevel.folder))
            {
                string folderName = Path.GetFileName(game.workshopLevel.folder.TrimEnd('/', '\\'));
                if (!string.IsNullOrEmpty(folderName))
                    return SanitizeId(folderName);
            }
            return SanitizeId("W" + game.currentLevelNumber);
        }
    }
}
