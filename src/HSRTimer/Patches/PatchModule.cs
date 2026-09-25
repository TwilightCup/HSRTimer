using HarmonyLib;

namespace HSRTimer
{
    /// <summary>
    /// Applies all HSRTimer Harmony patches with a stable instance id. Only the
    /// voiceline hooks (NarrativeBlock.Play, SubtitleManager.PlayNarrative),
    /// the pause-menu load/restart hooks (PauseMenu.LoadClick,
    /// PauseMenu.RestartClick), the Jumpless jump-key suppression
    /// (HumanControls.HandleInput), and the precise timing-boundary hooks
    /// (Game.AfterLoad, Game.EnterPassZone, Game.Fall — TB-3) are patched —
    /// everything else is polled from public game fields for resilience.
    /// </summary>
    internal static class PatchModule
    {
        private const string InstanceId = PluginInfo.PLUGIN_GUID;

        public static void Apply()
        {
            try
            {
                var harmony = new Harmony(InstanceId);
                harmony.PatchAll(typeof(PatchModule).Assembly);
                Plugin.Logger.LogInfo("HSRTimer: Harmony patches applied.");
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogError($"HSRTimer: failed to apply Harmony patches: {ex}");
            }
        }
    }
}
