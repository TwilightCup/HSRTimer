namespace HSRTimer
{
    /// <summary>
    /// SSG (half-body) detection. The game's reverse-wall-climb branch in
    /// <c>CollisionSensor.GrabCheck</c> writes <c>grabObject</c> then returns
    /// before creating <c>grabJoint</c>; because <c>ReleaseGrab</c> only clears
    /// the pair when a joint exists, that hand keeps a permanent phantom grab.
    /// This check watches for exactly that broken invariant:
    /// <c>grabObject != null &amp;&amp; grabJoint == null</c>.
    ///
    /// It deliberately does NOT test <c>Human.state == Climb</c> or
    /// <c>!Human.hasGrabbed</c> on their own: a normal climb release can keep
    /// the game in Climb until landing (the allowed "pseudo half-body" state)
    /// while both grab fields are already null.
    ///
    /// The phantom-grab state persists for as long as it exists, so the check
    /// is edge-tracked: it raises on the false→true edge and counts each new
    /// occurrence (e.g. again after a respawn rebuilds the ragdoll) exactly
    /// once, instead of counting every tick while the glitch is active.
    /// </summary>
    internal sealed class SsgGlitchCheck : IGlitchCheck
    {
        // Edge-tracked instead of one-shot-per-level: the phantom grab persists
        // for as long as it exists, so counting every tick would inflate the
        // soft count. Raising on the false→true edge counts each new occurrence
        // (e.g. again after a respawn rebuilds the ragdoll) exactly once.
        private bool _wasBad;

        public void OnLevelEnter(ValidationContext ctx)
        {
            _wasBad = false;
        }

        public void OnTick(ValidationContext ctx) => Check(ctx);

        public void OnLevelExit(ValidationContext ctx) => Check(ctx);

        private void Check(ValidationContext ctx)
        {
            var human = Human.Localplayer;
            if (human == null || human.ragdoll == null)
            {
                // Ragdoll rebuilt / level unloading: any ongoing phantom grab
                // is gone, so the next occurrence is a fresh false→true edge.
                _wasBad = false;
                return;
            }

            bool bad = HasPhantomGrab(human.ragdoll.partLeftHand)
                || HasPhantomGrab(human.ragdoll.partRightHand);
            if (bad && !_wasBad)
                ctx.Flags.Raise(InvalidReason.Ssg);
            _wasBad = bad;
        }

        private static bool HasPhantomGrab(HumanSegment hand)
        {
            if (hand == null)
                return false;

            var sensor = hand.sensor;
            if (sensor == null)
                return false;

            // Unity overloads == / != for UnityEngine.Object, so a destroyed
            // grabJoint or grabObject correctly reads as null here.
            return sensor.grabObject != null && sensor.grabJoint == null;
        }
    }
}
