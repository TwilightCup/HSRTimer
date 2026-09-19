namespace HSRTimer
{
    /// <summary>
    /// One independent Glitchless rule. Glitchless stacks several glitch
    /// detectors under a single tag; each detector raises its own
    /// <see cref="InvalidReason"/> so the HUD can name the exact glitch.
    /// </summary>
    internal interface IGlitchCheck
    {
        void OnLevelEnter(ValidationContext ctx);
        void OnTick(ValidationContext ctx);
        void OnLevelExit(ValidationContext ctx);
    }
}
