using HarmonyLib;
using Multiplayer;

namespace HSRTimer
{
    /// <summary>
    /// Harmony postfix on <c>Game.AfterLoad(int, int)</c>: the authoritative
    /// moment the game assigns <c>state = PlayingLevel</c> for a freshly loaded
    /// level (R1.2.2). It latches the exact segment-start tick so the boundary
    /// no longer depends on the frame the polling loop first observes the new
    /// state (TB-3). The record is consumed by <see cref="TimerCore"/> on the
    /// next physics step.
    /// </summary>
    [HarmonyPatch(typeof(Game), nameof(Game.AfterLoad))]
    internal static class GameAfterLoadBoundaryPatch
    {
        private static void Postfix()
        {
            try
            {
                TimerCore.RecordSegmentStart();
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: Game.AfterLoad postfix failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Harmony postfix on <c>Game.EnterPassZone()</c>: reaching the exit pass
    /// zone sets <c>passedLevel</c> (R1.4.2). It latches the pass flag
    /// immediately so later checks see the completion even before the polling
    /// loop next reads <c>game.passedLevel</c>. The segment's end tick is
    /// recorded by <see cref="GameFallBoundaryPatch"/>, where the game actually
    /// detects the pass and starts the load.
    /// </summary>
    [HarmonyPatch(typeof(Game), nameof(Game.EnterPassZone))]
    internal static class GameEnterPassZoneBoundaryPatch
    {
        private static void Postfix(Game __instance)
        {
            try
            {
                if (__instance.state == GameState.PlayingLevel && __instance.passedLevel)
                    TimerCore.LatchLevelPassed();
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: Game.EnterPassZone postfix failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Harmony prefix/postfix on <c>Game.Fall(HumanBase, bool, bool)</c>: the
    /// point where the game detects a genuine level completion and enters the
    /// loading/leave path (R1.4.2). The prefix snapshots whether this call took
    /// the pass branch (the method itself may clear <c>passedLevel</c> for
    /// Workshop/EditorPick before returning); the postfix then latches the exact
    /// end tick via <see cref="TimerCore.RecordLevelPass"/>.
    /// <para>
    /// This is the deliberate exception to "poll, don't patch" documented in
    /// ARCHITECTURE.md: the pass is evaluated inside the game's physics step, so
    /// a later poll can only observe it one or more ticks late.
    /// </para>
    /// </summary>
    [HarmonyPatch(typeof(Game), nameof(Game.Fall))]
    internal static class GameFallBoundaryPatch
    {
        private static void Prefix(Game __instance, out bool __state)
        {
            __state = IsPassingFall(__instance);
        }

        private static void Postfix(bool __state)
        {
            if (!__state)
                return;
            try
            {
                TimerCore.RecordLevelPass();
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: Game.Fall postfix failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Mirror of the condition the game itself uses to take the pass branch
        /// in <c>Game.Fall</c>: the level is marked passed and it is a level that
        /// can be completed (campaign, Workshop, or EditorPick).
        /// </summary>
        private static bool IsPassingFall(Game game)
        {
            // Mirror the early returns at the top of Game.Fall so a replay
            // playback or a client-side fall never latches a boundary.
            if (ReplayRecorder.isPlaying || NetGame.isClient)
                return false;
            if (game == null || !game.passedLevel)
                return false;
            if (game.workshopLevel != null)
                return true;
            if (game.currentLevelType == WorkshopItemSource.EditorPick)
                return true;
            return game.levels != null
                && game.currentLevelNumber >= 0
                && game.currentLevelNumber < game.levels.Length;
        }
    }
}
