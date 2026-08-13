namespace HSRTimer
{
    /// <summary>
    /// R3.6 Voiceline tag: the player must trigger every voiceline in the level.
    /// Delegates scene scanning and per-block tracking to <see cref="VoicelineTracker"/>.
    /// On level exit, raises an invalid reason if any block was missed or the
    /// special Easter voiceline was skipped.
    /// </summary>
    public sealed class VoicelineTagRule : ITagRule
    {
        public string Id => TagIds.Voiceline;
        public string DisplayNameKey => "TAG_VOICELINE";

        private VoicelineTracker _tracker;

        public void OnLevelEnter(ValidationContext ctx)
        {
            _tracker = new VoicelineTracker();
            _tracker.InitOnLevelEnter();
            VoicelineTracker.Current = _tracker;
        }

        public void OnTick(ValidationContext ctx)
        {
            // Nothing to do per tick; completion is checked on exit.
        }

        public void OnLevelExit(ValidationContext ctx)
        {
            if (_tracker == null) return;
            if (!_tracker.IsSatisfied())
                ctx.Flags.Raise(InvalidReason.Voiceline);

            // Clear the static current-pointer so a stray postfix doesn't
            // resurrect a finished level's tracker.
            if (VoicelineTracker.Current == _tracker)
                VoicelineTracker.Current = null;
        }

        /// <summary>Access the active tracker (for the HUD green hint, R3.6.3).</summary>
        public VoicelineTracker Tracker => _tracker;
    }
}
