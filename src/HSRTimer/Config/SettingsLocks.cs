using System;
using System.Collections.Generic;

namespace HSRTimer
{
    /// <summary>
    /// Reusable registry of settings that must not be changed while a condition
    /// holds (R1.12.3). An option that selects the time unit / pause behavior
    /// (or any future option with the same "must not change mid-run" property)
    /// registers its <see cref="SettingsModel"/> field name here with a lock
    /// predicate and a localization key; the settings panel then disables the
    /// toggle and shows the hint while the predicate is true, and the dev
    /// console refuses <c>hsr set</c> for it — without either surface knowing
    /// anything option-specific.
    ///
    /// The registry is deliberately dependency-free (it never references
    /// <see cref="TimerCore"/>); the wiring layer (<see cref="Plugin"/>) supplies
    /// the predicate. External plugins may register their own locked settings
    /// through the same public API (their tabs draw their own UI but can query
    /// <see cref="IsLocked"/>).
    ///
    /// All lookups are case-insensitive on the field name; a throwing predicate
    /// is treated as "not locked" so a bad extension can never break the panel
    /// or the console.
    /// </summary>
    public static class SettingsLocks
    {
        /// <summary>One locked setting.</summary>
        public sealed class Entry
        {
            /// <summary>Public <see cref="SettingsModel"/> field name (e.g. <c>nameof(SettingsModel.ForceFrameTiming)</c>).</summary>
            public string FieldName;

            /// <summary>Live lock predicate; true = the setting cannot be changed right now.</summary>
            public Func<bool> IsLocked;

            /// <summary>Localization key for the hint shown while locked.</summary>
            public string HintKey;
        }

        private static readonly List<Entry> EntriesList = new List<Entry>();

        /// <summary>All registered entries, in registration order.</summary>
        public static IReadOnlyList<Entry> All => EntriesList;

        /// <summary>
        /// Register (or replace) a locked setting. Idempotent per field name, so
        /// re-running the wiring replaces the previous predicate/hint.
        /// </summary>
        public static void Register(string fieldName, Func<bool> isLocked, string hintKey)
        {
            if (string.IsNullOrEmpty(fieldName))
                return;
            for (int i = 0; i < EntriesList.Count; i++)
            {
                if (string.Equals(EntriesList[i].FieldName, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    EntriesList[i] = new Entry { FieldName = fieldName, IsLocked = isLocked, HintKey = hintKey };
                    return;
                }
            }
            EntriesList.Add(new Entry { FieldName = fieldName, IsLocked = isLocked, HintKey = hintKey });
        }

        /// <summary>Find a registered setting by its field name (case-insensitive).</summary>
        public static bool TryGet(string fieldName, out Entry entry)
        {
            entry = null;
            if (string.IsNullOrEmpty(fieldName))
                return false;
            foreach (var e in EntriesList)
            {
                if (string.Equals(e.FieldName, fieldName, StringComparison.OrdinalIgnoreCase))
                {
                    entry = e;
                    return true;
                }
            }
            return false;
        }

        /// <summary>True when the setting is registered and its predicate currently holds.</summary>
        public static bool IsLocked(string fieldName)
        {
            if (!TryGet(fieldName, out Entry entry))
                return false;
            try
            {
                return entry.IsLocked != null && entry.IsLocked();
            }
            catch (Exception ex)
            {
                if (Plugin.Logger != null)
                    Plugin.Logger.LogWarning($"HSRTimer: settings lock '{fieldName}' predicate threw: {ex.Message}");
                return false;
            }
        }
    }
}
