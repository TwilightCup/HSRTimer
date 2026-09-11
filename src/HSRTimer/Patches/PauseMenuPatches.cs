using HarmonyLib;

namespace HSRTimer
{
    /// <summary>
    /// Harmony postfix on <c>PauseMenu.RestartClick()</c>: when the player
    /// restarts the level from the in-level pause menu, clear the run's
    /// forgivable validity flags (<c>restart_clears_forgivable</c>, R5.4.3),
    /// and restart the Wake Up Time measurement (default wake-up behavior).
    ///
    /// Unlike the one-key retry (R6, <see cref="RetryAction"/>), the pause
    /// menu's Restart is a checkpoint respawn — <c>Game.RestartLevel(true)</c>
    /// resets the level in place without reloading the scene — so the run's
    /// timers keep running and are NOT reset; only the forgivable flags are
    /// cleared, and only when the option is on. The one-key retry clears them
    /// unconditionally. Unforgivable flags are never cleared here.
    ///
    /// This fires after the game's own restart has run (the menu button's click
    /// handler). That is also after <c>RespawnAllPlayers</c> has put the local
    /// player back into <c>Spawning</c>, so the patch marks that state as
    /// already seen to avoid a second restart from the FixedUpdate polling
    /// respawn detector. Idempotent, and guarded so a thrown exception logs
    /// instead of crashing the game (N7). The multiplayer pause menu is
    /// intentionally not patched: the timer only supports single-player local
    /// runs.
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
                if (!cfg.Settings.OnlyRecordFirstWakeUpTime)
                    TimerCore.RestartWakeUpMeasurement();
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: PauseMenu.RestartClick postfix failed: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Harmony postfix on <c>PauseMenu.LoadClick()</c>: when the player loads
    /// the current checkpoint from the in-level pause menu, restart the Wake Up
    /// Time measurement so the displayed value reflects the time from that
    /// checkpoint respawn to the next wake-up. This is the default wake-up
    /// behavior; it is skipped when "only record first wake-up time" is enabled.
    /// Like the Restart patch, it runs after the game has already respawned the
    /// player into <c>Spawning</c>, so it marks that state as already seen by
    /// the polling respawn detector.
    /// </summary>
    [HarmonyPatch(typeof(PauseMenu), nameof(PauseMenu.LoadClick))]
    internal static class PauseMenuLoadPatch
    {
        private static void Postfix()
        {
            try
            {
                var cfg = ConfigService.Instance;
                var state = TimerCore.State;
                if (cfg == null || state == null)
                    return;
                if (!cfg.Settings.OnlyRecordFirstWakeUpTime)
                    TimerCore.RestartWakeUpMeasurement();
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: PauseMenu.LoadClick postfix failed: {ex.Message}");
            }
        }
    }
}
