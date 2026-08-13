using HarmonyLib;

namespace HSRTimer
{
    /// <summary>
    /// Harmony postfix on <c>PauseMenu.RestartClick()</c>: when the player
    /// restarts the level from the in-level pause menu, clear the run's
    /// forgivable validity flags (<c>restart_clears_forgivable</c>, R5.4.3).
    ///
    /// Unlike the one-key retry (R6, <see cref="RetryAction"/>), the pause
    /// menu's Restart is a checkpoint respawn — <c>Game.RestartLevel(true)</c>
    /// resets the level in place without reloading the scene — so the run's
    /// timers keep running and are NOT reset; only the forgivable flags are
    /// cleared, and only when the option is on. The one-key retry clears them
    /// unconditionally. Unforgivable flags are never cleared here.
    ///
    /// This fires before the game's own restart runs (the menu button's click
    /// handler), so the clear lands before any post-restart tick can re-raise
    /// a reason. Idempotent, and guarded so a thrown exception logs instead of
    /// crashing the game (N7). The multiplayer pause menu is intentionally not
    /// patched: the timer only supports single-player local runs.
    /// </summary>
    [HarmonyPatch(typeof(PauseMenu), nameof(PauseMenu.RestartClick))]
    internal static class PauseMenuRestartPatch
    {
        private static void Postfix()
        {
            try
            {
                var cfg = ConfigService.Instance;
                var state = TimerCore.State;
                if (cfg == null || state == null)
                    return;
                if (cfg.Settings.RestartClearsForgivable)
                    state.Flags.ClearForgivable();
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: PauseMenu.RestartClick postfix failed: {ex.Message}");
            }
        }
    }
}
