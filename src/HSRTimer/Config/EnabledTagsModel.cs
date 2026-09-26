using System.Collections.Generic;

namespace HSRTimer
{
    /// <summary>
    /// The currently enabled tags. HSRTimer has no concept of named category
    /// presets — the active rule set is simply the set of tags the user has
    /// turned on (the six built-in rule tags plus any custom tags registered by
    /// other plugins), plus any auto-managed label tags (see <see cref="TagLabels"/>,
    /// R3.10) the engine added for the live game mode. Persisted as a flat list
    /// in tags.ini (label tags excluded); loaded/owned by
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
                ["enabled"] = string.Join(", ", PersistedTags()),
            };
            PersistenceService.Write(
                PersistenceService.PathFor("tags.ini"),
                new[] { new KeyValuePair<string, IDictionary<string, string>>("tags", kv) },
                "HSRTimer enabled rule tags. No category presets — just the tag set.\n# enabled = comma-separated tag ids (built-in: Checkpoint, NoCheckpoint, Jumpless, Voiceline, Glitchless, NoEC).\n# Label tags (e.g. Co-op, R3.10) are auto-managed at runtime and are never saved here.");
        }

        /// <summary>
        /// The tag ids that are actually persisted to tags.ini. Label tags
        /// (<see cref="TagLabels"/>) are auto-managed by the engine at runtime
        /// and excluded, so a transient label (e.g. Co-op during a multiplayer
        /// session) can never leak into the file and stick on the next launch.
        /// </summary>
        private IEnumerable<string> PersistedTags()
        {
            foreach (var t in Tags)
                if (!TagLabels.IsLabel(t))
                    yield return t;
        }
    }
}
