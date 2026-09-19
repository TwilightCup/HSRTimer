namespace HSRTimer
{
    /// <summary>
    /// Glitchless tag: the player must not perform any glitches. The concrete
    /// judgment rules are still being specified; this is the registered shell so
    /// the tag appears in the settings panel and can be enabled before its rules
    /// are implemented incrementally.
    /// </summary>
    public sealed class GlitchlessTagRule : ITagRule
    {
        public string Id => TagIds.Glitchless;
        public string DisplayNameKey => "TAG_GLITCHLESS";

        public void OnLevelEnter(ValidationContext ctx) { }

        public void OnTick(ValidationContext ctx) { }

        public void OnLevelExit(ValidationContext ctx) { }
    }
}
