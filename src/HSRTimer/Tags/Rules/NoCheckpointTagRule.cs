namespace HSRTimer
{
    /// <summary>
    /// R3.4 NoCheckpoint tag: the player must not trigger any checkpoint.
    /// Any currentCheckpointNumber greater than 0 invalidates the run.
    /// </summary>
    public sealed class NoCheckpointTagRule : ITagRule
    {
        public string Id => TagIds.NoCheckpoint;
        public string DisplayNameKey => "TAG_NO_CHECKPOINT";

        public void OnLevelEnter(ValidationContext ctx) { }
        public void OnLevelExit(ValidationContext ctx) { }

        public void OnTick(ValidationContext ctx)
        {
            if (ctx.CurrentCheckpoint > 0)
                ctx.Flags.Raise(InvalidReason.NoCheckpointHit);
        }
    }
}
