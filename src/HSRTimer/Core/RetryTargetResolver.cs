using System;
using HumanAPI;

namespace HSRTimer
{
    /// <summary>
    /// Resolves the user-specified one-key retry target (R6.5) to a level id +
    /// source pair suitable for <c>App.LaunchSinglePlayer</c>.
    ///
    /// Accepted inputs:
    /// - the game's English localized level name (case-insensitive) for a
    ///   BuiltIn or EditorPick level, e.g. "Intro", "Power Plant", "Aztec";
    /// - a Steam Workshop level numeric id (loaded in
    ///   <c>WorkshopRepository.levelRepo</c> as Subscription).
    ///
    /// The resolver intentionally keeps workshop targeting id-based: name
    /// collisions between built-in and workshop titles are avoided by only
    /// matching names against official BuiltIn/EditorPick levels.
    /// </summary>
    public static class RetryTargetResolver
    {
        public static bool TryResolve(string input, out ulong levelId, out WorkshopItemSource levelType)
        {
            levelId = 0UL;
            levelType = WorkshopItemSource.NotSpecified;

            if (string.IsNullOrWhiteSpace(input))
                return false;

            string trimmed = input.Trim();

            // Numeric input is always treated as a Workshop level id.
            if (ulong.TryParse(trimmed, out ulong workshopId))
                return TryFindWorkshop(workshopId, out levelId, out levelType);

            return TryFindLevelByName(trimmed, out levelId, out levelType);
        }

        private static bool TryFindWorkshop(ulong workshopId, out ulong levelId, out WorkshopItemSource levelType)
        {
            levelId = 0UL;
            levelType = WorkshopItemSource.NotSpecified;

            var repo = WorkshopRepository.instance != null ? WorkshopRepository.instance.levelRepo : null;
            if (repo == null)
                return false;

            foreach (var item in repo)
            {
                if (item == null)
                    continue;

                // Steam Workshop subscriptions launch through
                // MonoApp.LaunchSinglePlayer(workshopId, Subscription). Local
                // Workshop (lvl:...) levels use a folder path instead of a
                // numeric id, so they are intentionally not accepted here.
                if (item.levelType != WorkshopItemSource.Subscription
                    || item.workshopId != workshopId)
                    continue;

                levelId = item.workshopId;
                levelType = item.levelType;
                return true;
            }

            return false;
        }

        private static bool TryFindLevelByName(string name, out ulong levelId, out WorkshopItemSource levelType)
        {
            levelId = 0UL;
            levelType = WorkshopItemSource.NotSpecified;

            var repo = WorkshopRepository.instance != null ? WorkshopRepository.instance.levelRepo : null;
            if (repo != null)
            {
                foreach (var item in repo)
                {
                    if (item == null)
                        continue;

                    if (item.levelType != WorkshopItemSource.BuiltIn
                        && item.levelType != WorkshopItemSource.EditorPick)
                        continue;

                    if (!MatchesName(item, name))
                        continue;

                    levelId = item.workshopId;
                    levelType = item.levelType;
                    return true;
                }
            }

            // Fallback to the raw game arrays when the WorkshopRepository has
            // not loaded the built-in / editor-pick metadata yet.
            return TryFindByGameArrays(name, out levelId, out levelType);
        }

        private static bool TryFindByGameArrays(string name, out ulong levelId, out WorkshopItemSource levelType)
        {
            levelId = 0UL;
            levelType = WorkshopItemSource.NotSpecified;

            var game = Game.instance;
            if (game == null)
                return false;

            if (game.levels != null)
            {
                int playable = game.levelCount > 0 ? game.levelCount : game.levels.Length;
                for (int i = 0; i < game.levels.Length && i < playable; i++)
                {
                    string internalName = game.levels[i];
                    if (MatchesInternalName(internalName, name))
                    {
                        levelId = (ulong)i;
                        levelType = WorkshopItemSource.BuiltIn;
                        return true;
                    }
                }
            }

            if (game.editorPickLevels != null)
            {
                for (int i = 0; i < game.editorPickLevels.Length; i++)
                {
                    string internalName = game.editorPickLevels[i];
                    if (MatchesInternalName(internalName, name))
                    {
                        levelId = (ulong)i;
                        levelType = WorkshopItemSource.EditorPick;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool MatchesInternalName(string internalName, string input)
        {
            if (string.IsNullOrEmpty(internalName))
                return false;
            if (string.Equals(internalName, input, StringComparison.OrdinalIgnoreCase))
                return true;
            string english = GetEnglishLocalizedLevelName("LEVEL/" + internalName, internalName);
            return !string.IsNullOrEmpty(english)
                && string.Equals(english, input, StringComparison.OrdinalIgnoreCase);
        }

        private static bool MatchesName(WorkshopLevelMetadata metadata, string input)
        {
            var builtin = metadata as BuiltinLevelMetadata;
            string internalName = builtin != null ? builtin.internalName : null;

            // The internal scene id is a useful fallback even when the HFF
            // localization asset is not ready yet.
            if (!string.IsNullOrEmpty(internalName)
                && string.Equals(internalName, input, StringComparison.OrdinalIgnoreCase))
                return true;

            string english = GetEnglishLocalizedLevelName("LEVEL/" + internalName, internalName);
            if (!string.IsNullOrEmpty(english)
                && string.Equals(english, input, StringComparison.OrdinalIgnoreCase))
                return true;

            // Fall back to the metadata title as loaded by the game. This lets
            // the input also match the currently displayed localized title, and
            // covers editor picks whose English term differs from the scene id.
            return !string.IsNullOrEmpty(metadata.title)
                && string.Equals(metadata.title, input, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Return the English (not current-language) translation for a game
        /// localization term such as <c>LEVEL/Aztec</c>. Delegates to the shared
        /// <see cref="LevelIdentity"/> helper.
        /// </summary>
        private static string GetEnglishLocalizedLevelName(string term, string fallback)
            => LevelIdentity.EnglishLevelName(term, fallback);
    }
}
