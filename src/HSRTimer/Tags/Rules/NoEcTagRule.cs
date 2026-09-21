using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// NoEC tag: the player must not wall-climb ("无爬墙"). Detection:
    /// when the local player leaves the ground (onGround true→false edge), the
    /// first grab height of that airborne period is recorded as the baseline.
    /// If the player is already holding something at takeoff, the higher of the
    /// two hands' grab heights is used. While airborne, any new grab whose
    /// height rises more than 0.2 m above that baseline is an EC violation.
    /// A 0.2 s debounce keeps a burst of rapid grabs from raising the same EC
    /// flag more than once per interval.
    /// </summary>
    public sealed class NoEcTagRule : ITagRule
    {
        public string Id => TagIds.NoEC;
        public string DisplayNameKey => "TAG_NO_EC";

        /// <summary>Maximum allowed rise above the airborne baseline before a grab is EC.</summary>
        private const float HeightTolerance = 0.2f;

        /// <summary>Minimum interval between EC triggers (debounce).</summary>
        private const float EcDebounceSeconds = 0.2f;

        private bool _armed;
        private bool _baselineSet;
        private float _baselineHeight;
        private GameObject _leftPrevObject;
        private GameObject _rightPrevObject;
        private float _lastEcTime;

        private bool _prevOnGround;
        private bool _prevOnGroundInit;

        public void OnLevelEnter(ValidationContext ctx)
        {
            Reset();
        }

        public void OnTick(ValidationContext ctx)
        {
            var human = Human.Localplayer;
            if (human == null || human.ragdoll == null)
                return;

            var left = human.ragdoll.partLeftHand != null ? human.ragdoll.partLeftHand.sensor : null;
            var right = human.ragdoll.partRightHand != null ? human.ragdoll.partRightHand.sensor : null;
            bool leftGrabbing = left != null && left.grabObject != null;
            bool rightGrabbing = right != null && right.grabObject != null;

            // Track the onGround edge so the baseline starts exactly when the
            // player leaves the ground (not mid-air spawns).
            if (!_prevOnGroundInit)
            {
                _prevOnGround = human.onGround;
                _prevOnGroundInit = true;
            }
            bool leftGroundEdge = _prevOnGround && !human.onGround;
            _prevOnGround = human.onGround;

            if (!_armed)
            {
                if (leftGroundEdge)
                {
                    _armed = true;
                    _baselineSet = false;
                    if (leftGrabbing || rightGrabbing)
                    {
                        _baselineSet = true;
                        _baselineHeight = MaxGrabHeight(left, right);
                    }
                    _leftPrevObject = leftGrabbing ? left.grabObject : null;
                    _rightPrevObject = rightGrabbing ? right.grabObject : null;
                }
                return;
            }

            // The airborne period ends when the player touches the ground again.
            // The EC debounce timestamp is deliberately kept across airborne
            // periods so "at most one EC per 0.2 s" is enforced globally within
            // the level, not just inside a single jump.
            if (human.onGround)
            {
                EndAirborne();
                return;
            }

            // If nothing was held at takeoff, the first grab during this
            // airborne period becomes the baseline.
            if (!_baselineSet)
            {
                if (leftGrabbing || rightGrabbing)
                {
                    _baselineSet = true;
                    _baselineHeight = MaxGrabHeight(left, right);
                    _leftPrevObject = leftGrabbing ? left.grabObject : null;
                    _rightPrevObject = rightGrabbing ? right.grabObject : null;
                }
                else
                {
                    _leftPrevObject = null;
                    _rightPrevObject = null;
                }
                return;
            }

            // Any NEW grab (a hand that was not holding this object before)
            // above the baseline + tolerance is EC.
            if (leftGrabbing && _leftPrevObject != left.grabObject
                && left.grabPosition.y > _baselineHeight + HeightTolerance)
                RaiseDebounced(ctx);
            if (rightGrabbing && _rightPrevObject != right.grabObject
                && right.grabPosition.y > _baselineHeight + HeightTolerance)
                RaiseDebounced(ctx);

            _leftPrevObject = leftGrabbing ? left.grabObject : null;
            _rightPrevObject = rightGrabbing ? right.grabObject : null;
        }

        public void OnLevelExit(ValidationContext ctx)
        {
            Reset();
        }

        private void RaiseDebounced(ValidationContext ctx)
        {
            float now = Time.realtimeSinceStartup;
            if (now - _lastEcTime < EcDebounceSeconds)
                return;
            _lastEcTime = now;
            ctx.Flags.Raise(InvalidReason.Ec);
        }

        private static float MaxGrabHeight(CollisionSensor left, CollisionSensor right)
        {
            float max = float.MinValue;
            if (left != null && left.grabObject != null)
                max = Mathf.Max(max, left.grabPosition.y);
            if (right != null && right.grabObject != null)
                max = Mathf.Max(max, right.grabPosition.y);
            return max;
        }

        /// <summary>Clear all per-airborne-period state (landing). The debounce clock is preserved.</summary>
        private void EndAirborne()
        {
            _armed = false;
            _baselineSet = false;
            _baselineHeight = 0f;
            _leftPrevObject = null;
            _rightPrevObject = null;
        }

        /// <summary>Full reset at level enter/exit: also clears the debounce clock.</summary>
        private void Reset()
        {
            EndAirborne();
            _lastEcTime = 0f;
            _prevOnGround = false;
            _prevOnGroundInit = false;
        }
    }
}
