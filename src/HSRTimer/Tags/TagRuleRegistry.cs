using System.Collections.Generic;

namespace HSRTimer
{
    /// <summary>
    /// Built-in tag ids (R3.3-R3.6). Custom third-party tags use arbitrary
    /// string ids via <see cref="TagRuleRegistry.Register(ITagRule)"/>.
    /// </summary>
    public static class TagIds
    {
        public const string Checkpoint = "Checkpoint";
        public const string NoCheckpoint = "NoCheckpoint";
        public const string Jumpless = "Jumpless";
        public const string Voiceline = "Voiceline";
    }

    /// <summary>
    /// Registry of all known tag rules (R3.7). The built-in rules are registered
    /// at boot; third-party plugins call <see cref="Register"/> to add custom
    /// rules. Users toggle rules on/off in the settings panel by their string
    /// id. Duplicate ids are rejected (logged + ignored) to avoid
    /// double-penalizing a run.
    /// </summary>
    public sealed class TagRuleRegistry
    {
        public static TagRuleRegistry Instance { get; private set; }

        private readonly Dictionary<string, ITagRule> _byId = new Dictionary<string, ITagRule>();

        public IEnumerable<ITagRule> All => _byId.Values;

        public static void Init(TagRuleRegistry instance) => Instance = instance;

        /// <summary>Register a rule; returns false (and logs) on a duplicate id.</summary>
        public bool Register(ITagRule rule)
        {
            if (rule == null) return false;
            if (_byId.ContainsKey(rule.Id))
            {
                Plugin.Logger.LogWarning($"HSRTimer: tag rule '{rule.Id}' already registered; ignoring duplicate.");
                return false;
            }
            _byId[rule.Id] = rule;
            return true;
        }

        /// <summary>Look up a rule by id (null if unknown).</summary>
        public ITagRule Find(string id)
        {
            ITagRule r;
            return id != null && _byId.TryGetValue(id, out r) ? r : null;
        }
    }
}
