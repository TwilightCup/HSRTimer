using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// Prop Fly detection. The glitch is jumping while standing on a movable
    /// object that the player is also holding. The game itself recognizes this
    /// setup in <c>LegMuscles.ApplyJumpImpulses</c> (it reduces the jump impulse
    /// when a ground contact is already grabbed); we detect the same condition
    /// at the <c>Human.jump</c> false→true edge.
    ///
    /// Only the local player is checked, and only real grabs are considered via
    /// <see cref="GrabManager.grabbedObjects"/> so a phantom SSG grab does not
    /// satisfy this rule.
    /// </summary>
    internal sealed class PropFlyGlitchCheck : IGlitchCheck
    {
        private bool _raised;
        private bool _wasJumping;

        public void OnLevelEnter(ValidationContext ctx)
        {
            _raised = false;

            var human = Human.Localplayer;
            _wasJumping = human != null && human.jump;
        }

        public void OnTick(ValidationContext ctx) => Check(ctx);

        public void OnLevelExit(ValidationContext ctx) => Check(ctx);

        private void Check(ValidationContext ctx)
        {
            if (_raised)
                return;

            var human = Human.Localplayer;
            if (human == null)
            {
                _wasJumping = false;
                return;
            }

            bool jumping = human.jump;
            if (jumping && !_wasJumping && IsStandingOnGrabbedMovableProp(human))
            {
                ctx.Flags.Raise(InvalidReason.PropFly);
                _raised = true;
            }

            _wasJumping = jumping;
        }

        private static bool IsStandingOnGrabbedMovableProp(Human human)
        {
            if (!human.onGround)
                return false;

            var grabManager = human.GetComponent<GrabManager>();
            var groundManager = human.GetComponent<GroundManager>();
            if (grabManager == null || groundManager == null || grabManager.grabbedObjects == null)
                return false;

            foreach (var grabbed in grabManager.grabbedObjects)
            {
                if (grabbed == null)
                    continue;

                var grabbedRoot = grabbed.GetComponentInParent<Rigidbody>();
                if (grabbedRoot == null || grabbedRoot.isKinematic)
                    continue;

                if (groundManager.IsStanding(grabbedRoot.gameObject) || IsFootOnRigidbody(human, grabbedRoot))
                    return true;
            }

            return false;
        }

        private static bool IsFootOnRigidbody(Human human, Rigidbody rb)
        {
            if (human == null || human.ragdoll == null)
                return false;

            return IsFootOn(human.ragdoll.partLeftFoot, rb)
                || IsFootOn(human.ragdoll.partRightFoot, rb);
        }

        private static bool IsFootOn(HumanSegment foot, Rigidbody rb)
        {
            if (foot == null || foot.sensor == null)
                return false;

            var contact = foot.sensor.groundObject;
            if (contact == null)
                return false;

            return contact.GetComponentInParent<Rigidbody>() == rb;
        }
    }
}
