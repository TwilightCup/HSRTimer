using System;
using System.Collections.Generic;
using HumanAPI;

namespace HSRTimer
{
    /// <summary>One level entry shown in the Markers panel's level lists (R10.5.1).</summary>
    public sealed class MarkerLevelEntry
    {
        public WorkshopItemSource Source;
        public ulong WorkshopId;
        public string LevelKey;      // sanitized storage key; null when the metadata cannot yield one yet
        public string DisplayName;
        public string InternalName;
        public bool Available => !string.IsNullOrEmpty(LevelKey);
    }

    /// <summary>
    /// Builds the three level lists of the Markers panel from the game's
    /// workshop repository (R10.5.1): Main Dreams = BuiltIn, Extra Dreams =
    /// EditorPick, Workshop = Subscription + LocalWorkshop. Level keys come from
    /// <see cref="LevelIdentity.MetadataLevelKey"/> so a marker created in the
    /// panel resolves to the same level while playing. When the repository (or a
    /// source's metadata) is not loaded yet, the list is empty — the panel shows
    /// a hint instead of fabricating possibly-wrong keys (R10.5.7).
    /// </summary>
    public static class MarkerCatalog
    {
        public static List<MarkerLevelEntry> MainDreams() => Build(WorkshopItemSource.BuiltIn);
        public static List<MarkerLevelEntry> ExtraDreams() => Build(WorkshopItemSource.EditorPick);

        public static List<MarkerLevelEntry> Workshop()
        {
            var list = Build(WorkshopItemSource.Subscription);
            list.AddRange(Build(WorkshopItemSource.LocalWorkshop));
            return list;
        }

        private static List<MarkerLevelEntry> Build(WorkshopItemSource source)
        {
            var result = new List<MarkerLevelEntry>();
            var repo = WorkshopRepository.instance != null ? WorkshopRepository.instance.levelRepo : null;
            if (repo == null)
                return result;
            try
            {
                var items = repo.BySource(source);
                if (items == null)
                    return result;
                foreach (var item in items)
                {
                    if (item == null)
                        continue;
                    string key = LevelIdentity.MetadataLevelKey(source, (int)item.workshopId, item);
                    var builtin = item as BuiltinLevelMetadata;
                    string display = string.IsNullOrEmpty(item.title)
                        ? (item.workshopId != 0UL ? item.workshopId.ToString() : (builtin != null ? builtin.internalName : ""))
                        : item.title;
                    if (string.IsNullOrEmpty(display))
                        display = item.workshopId != 0UL ? item.workshopId.ToString() : "";
                    result.Add(new MarkerLevelEntry
                    {
                        Source = source,
                        WorkshopId = item.workshopId,
                        LevelKey = key,
                        DisplayName = display,
                        InternalName = builtin != null ? builtin.internalName : null,
                    });
                }
                if (source != WorkshopItemSource.LocalWorkshop)
                    result.Sort((a, b) => a.WorkshopId.CompareTo(b.WorkshopId));
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to enumerate {source} levels for markers: {ex.Message}");
                result.Clear();
            }
            return result;
        }
    }
}
