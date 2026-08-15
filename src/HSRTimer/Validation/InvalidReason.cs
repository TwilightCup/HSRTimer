namespace HSRTimer
{
    /// <summary>
    /// Concrete reasons a run can be flagged invalid (R5). Grouped implicitly
    /// by <see cref="Severity"/>: unforgivable flags never clear until the game
    /// restarts; forgivable flags may be cleared on a manual retry (R5.4).
    /// </summary>
    public enum InvalidReason
    {
        // ── Unforgivable (R5.3.1) ──
        /// <summary>A built-in cheat code is active (CheatCodes.climbCheat/throwCheat).</summary>
        CheatCode,

        // ── Forgivable ──
        /// <summary>A checkpoint was skipped (R4.1).</summary>
        CheckpointSkip,

        /// <summary>Final checkpoint did not match the required value on level exit (R4.2).</summary>
        CheckpointFinal,

        /// <summary>A checkpoint was triggered under a NoCheckpoint category (R3.4).</summary>
        NoCheckpointHit,

        /// <summary>The player jumped under a Jumpless category (R3.5).</summary>
        Jumpless,

        /// <summary>A voiceline was skipped under a Voiceline category (R3.6).</summary>
        Voiceline,
    }

    /// <summary>Whether a reason can be cleared on retry.</summary>
    public enum Severity
    {
        /// <summary>Never cleared until the game process restarts.</summary>
        Unforgivable,

        /// <summary>Cleared on manual retry when "restart clears forgivable" is on.</summary>
        Forgivable,
    }

    /// <summary>Static classification of each invalid reason.</summary>
    public static class InvalidReasons
    {
        public static Severity SeverityOf(InvalidReason r)
            => r == InvalidReason.CheatCode
                ? Severity.Unforgivable
                : Severity.Forgivable;

        /// <summary>The localization key for a reason's human-readable text.</summary>
        public static string LocalKey(InvalidReason r)
        {
            switch (r)
            {
                case InvalidReason.CheatCode: return "INVALID_CHEAT_CODE";
                case InvalidReason.CheckpointSkip: return "INVALID_CHECKPOINT_SKIP";
                case InvalidReason.CheckpointFinal: return "INVALID_CHECKPOINT_FINAL";
                case InvalidReason.NoCheckpointHit: return "INVALID_NO_CHECKPOINT";
                case InvalidReason.Jumpless: return "INVALID_JUMPLESS";
                case InvalidReason.Voiceline: return "INVALID_VOICELINE";
                default: return r.ToString();
            }
        }
    }
}
