using HumanAPI;
using UnityEngine;

namespace HSRTimer
{
    /// <summary>
    /// Footsie (water stepping spot) detection. In the built-in Water (River)
    /// level, touching the level's pass point while the local player is inside
    /// the known "Footsie Spot" box is the glitch; no other prerequisite or
    /// death-state check is needed.
    ///
    /// The pass point can set <c>Game.passedLevel</c> without ending the level
    /// immediately (the player may leave the spot and finish by drowning later),
    /// so the detection watches the false→true edge of <c>passedLevel</c> while
    /// the player is inside the spot.
    /// </summary>
    internal sealed class FootsieGlitchCheck : IGlitchCheck
    {
        private const int WaterLevelNumber = 6;

        // Footsie Spot marker "m1" from the user's marker set (Range box).
        private static readonly Vector3 FootsieCenter = new Vector3(-23.823f, 2.5f, 0.988f);
        private static readonly Vector3 FootsieSize = new Vector3(3f, 2f, 3f);

        private bool _raised;
        private bool _wasPassed;

        public void OnLevelEnter(ValidationContext ctx)
        {
            _raised = false;
            _wasPassed = false;
        }

        public void OnTick(ValidationContext ctx)
        {
            var game = ctx.Game;
            if (_raised || !IsWaterLevel(game))
                return;

            var human = Human.Localplayer;
            if (human == null || human.transform == null)
                return;

            bool passed = game.passedLevel;
            if (passed && !_wasPassed && IsInsideFootsieSpot(human.transform.position))
            {
                ctx.Flags.Raise(InvalidReason.Footsie);
                _raised = true;
            }

            _wasPassed = passed;
        }

        public void OnLevelExit(ValidationContext ctx)
        {
            // Detection happens at the passedLevel edge while inside the spot,
            // not at the eventual level-end position.
        }

        private static bool IsWaterLevel(Game game)
            => game != null
                && game.currentLevelType == WorkshopItemSource.BuiltIn
                && game.currentLevelNumber == WaterLevelNumber;

        private static bool IsInsideFootsieSpot(Vector3 pos)
        {
            return Mathf.Abs(pos.x - FootsieCenter.x) <= FootsieSize.x * 0.5f
                && Mathf.Abs(pos.y - FootsieCenter.y) <= FootsieSize.y * 0.5f
                && Mathf.Abs(pos.z - FootsieCenter.z) <= FootsieSize.z * 0.5f;
        }
    }
}
