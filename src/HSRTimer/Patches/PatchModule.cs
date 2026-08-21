using HarmonyLib;

namespace HSRTimer
{
    /// <summary>
    /// Applies all HSRTimer Harmony patches with a stable instance id. Only the
    /// voiceline hooks (NarrativeBlock.Play, SubtitleManager.PlayNarrative), the
    /// pause-menu restart hook (PauseMenu.RestartClick), and the Jumpless
    /// jump-key suppression (HumanControls.HandleInput) are patched — everything
    /// else is polled from public game fields for resilience.
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
