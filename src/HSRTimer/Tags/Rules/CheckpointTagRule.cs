using HumanAPI;

namespace HSRTimer
{
    /// <summary>
    /// R3.3 / R4 Checkpoint tag: the player must pass every checkpoint in
    /// order. Two checks:
    ///   - Skip detection each tick (R4.1): a jump of more than +1 is a
    ///     violation unless it is a known legal span (R4.1.2 exception table).
    ///   - Final-checkpoint validation on level exit (R4.2): the checkpoint on
    ///     exit must equal the level's required final value.
    /// Also exposes the current checkpoint number for the HUD (R3.3.3).
    /// </summary>
    public sealed class CheckpointTagRule : ITagRule
    {
        public string Id => TagIds.Checkpoint;
        public string DisplayNameKey => "TAG_CHECKPOINT";

        private WorkshopItemSource _source;
        private int _level;
        private bool _finalChecked;

        public void OnLevelEnter(ValidationContext ctx)
        {
            _source = ctx.Game != null ? ctx.Game.currentLevelType : WorkshopItemSource.BuiltIn;
            _level = ctx.Game != null ? ctx.Game.currentLevelNumber : -1;
            _finalChecked = false;
        }

        public void OnTick(ValidationContext ctx)
        {
            // Skip detection (R4.1). Only flag when the checkpoint advanced.
            if (ctx.CurrentCheckpoint > ctx.PrevCheckpoint
                && CheckpointRules.IsSkipViolation(_source, _level, ctx.PrevCheckpoint, ctx.CurrentCheckpoint))
            {
                ctx.Flags.Raise(InvalidReason.CheckpointSkip);
            }
        }

        public void OnLevelExit(ValidationContext ctx)
        {
            if (_finalChecked) return;
            _finalChecked = true;

            if (ctx.Game == null) return;

            // Non-linear levels legitimately reach the exit without hitting the
            // numerically-highest checkpoint; skip the strict final check there.
            var level = Game.currentLevel;
            if (level != null && level.nonLinearCheckpoints)
                return;

            int? expected = CheckpointRules.ExpectedFinalCheckpoint(_source, _level, ctx.State.MaxCheckpointThisLevel);
            if (expected.HasValue && ctx.CurrentCheckpoint != expected.Value)
                ctx.Flags.Raise(InvalidReason.CheckpointFinal);
        }
    }
}
