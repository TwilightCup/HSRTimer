using HumanAPI;
using Multiplayer;

namespace HSRTimer
{
    /// <summary>
    /// The data handed to each tag rule every tick and at level enter/exit.
    /// A readonly-ish snapshot of what a rule needs to decide validity. Rules
    /// mutate only <see cref="Flags"/> (raising reasons) and their own state.
    /// </summary>
    public readonly struct ValidationContext
    {
        public readonly RunState State;
        public readonly Game Game;            // current Game.instance (may be null early)
        public readonly int CurrentCheckpoint;
        public readonly int PrevCheckpoint;
        public readonly ValidityFlags Flags;
        public readonly LocalizationService Loc;

        public ValidationContext(
            RunState state, Game game,
            int currentCheckpoint, int prevCheckpoint,
            ValidityFlags flags, LocalizationService loc)
        {
            State = state;
            Game = game;
            CurrentCheckpoint = currentCheckpoint;
            PrevCheckpoint = prevCheckpoint;
            Flags = flags;
            Loc = loc;
        }
    }

    /// <summary>
    /// Extension point for category tag rules (R3.7). Built-in tags
    /// (Checkpoint/NoCheckpoint/Jumpless/Voiceline) and any third-party rule
    /// implement this interface and are driven identically by the engine.
    /// Lifecycle: <see cref="OnLevelEnter"/> once when a segment starts,
    /// <see cref="OnTick"/> every physics frame while playing, <see cref="OnLevelExit"/>
    /// once when the segment ends. The engine only invokes a rule when the
    /// active category actually carries its tag.
    /// </summary>
    public interface ITagRule
    {
        /// <summary>Stable tag id, matching the tag id users toggle in the settings panel.</summary>
        string Id { get; }

        /// <summary>Optional localization key for the tag's display name (R3 tags).</summary>
        string DisplayNameKey { get; }

        /// <summary>Called once at segment (level) start.</summary>
        void OnLevelEnter(ValidationContext ctx);

        /// <summary>Called every physics frame while the level is active.</summary>
        void OnTick(ValidationContext ctx);

        /// <summary>Called once at segment (level) end.</summary>
        void OnLevelExit(ValidationContext ctx);
    }
}
