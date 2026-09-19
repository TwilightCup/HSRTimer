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
    /// </summary>
    internal sealed class SsgGlitchCheck : IGlitchCheck
    {
        private bool _raised;

        public void OnLevelEnter(ValidationContext ctx)
        {
            _raised = false;
        }

        public void OnTick(ValidationContext ctx) => Check(ctx);

        public void OnLevelExit(ValidationContext ctx) => Check(ctx);

        private void Check(ValidationContext ctx)
        {
            if (_raised)
                return;

            var human = Human.Localplayer;
            if (human == null || human.ragdoll == null)
                return;

            bool leftBad = HasPhantomGrab(human.ragdoll.partLeftHand);
            bool rightBad = HasPhantomGrab(human.ragdoll.partRightHand);
            if (!leftBad && !rightBad)
                return;

            ctx.Flags.Raise(InvalidReason.Ssg);
            _raised = true;
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
