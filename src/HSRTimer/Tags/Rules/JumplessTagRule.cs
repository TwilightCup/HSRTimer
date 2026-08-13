namespace HSRTimer
{
    /// <summary>
    /// R3.5 Jumpless tag: the player must not jump. Watches the local player's
    /// jump flag and raises an invalid reason on a false→true transition.
    /// </summary>
    public sealed class JumplessTagRule : ITagRule
    {
        public string Id => TagIds.Jumpless;
        public string DisplayNameKey => "TAG_JUMPLESS";

        // Edge detection across ticks.
        private bool _wasJumping;

        public void OnLevelEnter(ValidationContext ctx)
        {
            _wasJumping = CurrentJump();
        }

        public void OnLevelExit(ValidationContext ctx) { }

        public void OnTick(ValidationContext ctx)
        {
            bool jumping = CurrentJump();
            if (!_wasJumping && jumping)
                ctx.Flags.Raise(InvalidReason.Jumpless);
            _wasJumping = jumping;
        }

        private static bool CurrentJump()
        {
            // Human.Localplayer is null outside a level; treat as not-jumping.
            var human = Human.Localplayer;
            return human != null && human.jump;
        }
    }
}
