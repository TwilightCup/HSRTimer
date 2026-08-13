using System.Collections.Generic;

namespace HSRTimer
{
    /// <summary>
    /// The currently enabled rule tags. HSRTimer has no concept of named category
    /// presets — the active rule set is simply the set of tags the user has turned
    /// on (the four built-in tags plus any custom tags registered by other
    /// plugins). Persisted as a flat list in tags.ini; loaded/owned by
    /// <see cref="ConfigService"/> and iterated by the engine each tick.
    /// </summary>
    public sealed class EnabledTagsModel
    {
        /// <summary>The tag ids currently enabled.</summary>
        public readonly List<string> Tags = new List<string>();

        public bool HasTag(string tagId) => Tags != null && Tags.Contains(tagId);

        public void Enable(string tagId)
        {
            if (!string.IsNullOrEmpty(tagId) && !Tags.Contains(tagId))
                Tags.Add(tagId);
        }

        public void Disable(string tagId)
        {
            Tags.Remove(tagId);
        }

        public void Load()
        {
            Tags.Clear();
            foreach (var p in PersistenceService.Read(PersistenceService.PathFor("tags.ini")))
            {
                if (p.Section != "tags") continue;
                if (p.Key == "enabled")
                {
                    foreach (var t in p.Value.Split(','))
                    {
                        var tt = t.Trim();
                        if (tt.Length > 0) Tags.Add(tt);
                    }
                }
            }
        }

        public void Save()
        {
            var kv = new Dictionary<string, string>
            {
                ["enabled"] = string.Join(", ", Tags),
            };
            PersistenceService.Write(
                PersistenceService.PathFor("tags.ini"),
                new[] { new KeyValuePair<string, IDictionary<string, string>>("tags", kv) },
                "HSRTimer enabled rule tags. No category presets — just the tag set.\n# enabled = comma-separated tag ids (built-in: Checkpoint, NoCheckpoint, Jumpless, Voiceline).");
        }
    }
}
