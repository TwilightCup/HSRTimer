namespace HSRTimer
{
    /// <summary>
    /// The always-on validity check (R5.1): built-in cheat codes. Runs every
    /// frame from the engine and feeds a <see cref="ValidityFlags"/> instance.
    /// </summary>
    public static class GenericValidators
    {
        /// <summary>Cheat codes are always checked (R5.1.1).</summary>
        public static void CheckCheat(ValidityFlags flags)
        {
            if (CheatCodes.climbCheat || CheatCodes.throwCheat)
                flags.Raise(InvalidReason.CheatCode);
        }
    }
}
