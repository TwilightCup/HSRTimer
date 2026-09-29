using System.Collections.Generic;

namespace HSRTimer
{
    /// <summary>
    /// Concrete reasons a run can be flagged invalid (R5). Grouped implicitly
    /// by <see cref="Severity"/>: unforgivable flags never clear until the game
    /// restarts; forgivable flags may be cleared on a manual retry (R5.4); soft
    /// flags are counted and rendered as normal HUD text (they only flash red on
    /// the triggering frame), and are cleared by one-key retry and a full timer
    /// reset, but not by pause-menu restart.
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

        /// <summary>
        /// The SSG (half-body) glitch under Glitchless: a hand kept a phantom
        /// grab (<c>grabObject != null</c>) after the reverse-wall-climb branch
        /// returned without creating <c>grabJoint</c>. A soft flag (R3.8.3).
        /// </summary>
        Ssg,

        /// <summary>The Prop Fly glitch under Glitchless: jumping while standing on a grabbed movable object. A soft flag (R3.8.3).</summary>
        PropFly,

        /// <summary>The Footsie glitch under Glitchless: touching the Water (River) pass point inside the Footsie Spot range. A soft flag (R3.8.3).</summary>
        Footsie,

        /// <summary>The EC (wall-climb) violation under NoEC: while airborne (onGround false), a new grab point rose more than 0.2 m above the first grab height recorded for that airborne period.</summary>
        Ec,
    }

    /// <summary>How a reason behaves after it has been raised.</summary>
    public enum Severity
    {
        /// <summary>Never cleared until the game process restarts.</summary>
        Unforgivable,

        /// <summary>Cleared on manual retry when "restart clears forgivable" is on.</summary>
        Forgivable,

        /// <summary>
        /// A soft flag: shown on its own HUD line in normal text color with a
        /// trigger count. It flashes red when a new trigger occurs, is cleared
        /// by one-key retry and a full timer reset, but not by pause-menu
        /// restart (R3.8.3, R3.9.3).
        /// </summary>
        Soft,
    }

    /// <summary>Static classification of each invalid reason.</summary>
    public static class InvalidReasons
    {
        // Soft flags: EC (NoEC, R3.9.3) and all Glitchless glitches
        // SSG / Prop Fly / Footsie (R3.8.3). They never turn the run's red
        // "invalid" banner on; the HUD shows each on its own line with a
        // trigger count instead.
        private static readonly HashSet<InvalidReason> SoftReasons = new HashSet<InvalidReason>
        {
            InvalidReason.Ec,
            InvalidReason.Ssg,
            InvalidReason.PropFly,
            InvalidReason.Footsie,
        };

        public static bool IsSoft(InvalidReason r) => SoftReasons.Contains(r);

        public static Severity SeverityOf(InvalidReason r)
        {
            if (IsSoft(r))
                return Severity.Soft;
            return r == InvalidReason.CheatCode
                ? Severity.Unforgivable
                : Severity.Forgivable;
        }

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
                case InvalidReason.Ssg: return "INVALID_SSG";
                case InvalidReason.PropFly: return "INVALID_PROP_FLY";
                case InvalidReason.Footsie: return "INVALID_FOOTSIE";
                case InvalidReason.Ec: return "INVALID_EC";
                default: return r.ToString();
            }
        }
    }
}
