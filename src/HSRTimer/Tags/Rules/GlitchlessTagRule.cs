using System.Collections.Generic;

namespace HSRTimer
{
    /// <summary>
    /// Glitchless tag: the player must not perform any glitch. Individual
    /// glitches are implemented as independent <see cref="IGlitchCheck"/>
    /// detectors so each one raises its own invalid reason.
    /// </summary>
    public sealed class GlitchlessTagRule : ITagRule
    {
        public string Id => TagIds.Glitchless;
        public string DisplayNameKey => "TAG_GLITCHLESS";

        private readonly List<IGlitchCheck> _checks = new List<IGlitchCheck>
        {
            new SsgGlitchCheck(),
            new PropFlyGlitchCheck(),
            new FootsieGlitchCheck(),
        };

        public void OnLevelEnter(ValidationContext ctx)
        {
            for (int i = 0; i < _checks.Count; i++)
                _checks[i].OnLevelEnter(ctx);
        }

        public void OnTick(ValidationContext ctx)
        {
            for (int i = 0; i < _checks.Count; i++)
                _checks[i].OnTick(ctx);
        }

        public void OnLevelExit(ValidationContext ctx)
        {
            for (int i = 0; i < _checks.Count; i++)
                _checks[i].OnLevelExit(ctx);
        }
    }
}
