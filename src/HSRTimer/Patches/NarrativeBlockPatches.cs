using HarmonyLib;

namespace HSRTimer
{
    /// <summary>
    /// Harmony postfix on <c>NarrativeBlock.Play()</c>: whenever a voiceline
    /// trigger fires, mark that NarrativeBlock instance as triggered in the
    /// active <see cref="VoicelineTracker"/> (R3.6 / R5.5). NarrativeBlock.Play
    /// early-returns when already played, so the postfix is idempotent; the
    /// tracker dedupes by instance id regardless. Everything is guarded so a
    /// thrown exception logs instead of crashing the game (N7).
    /// </summary>
    [HarmonyPatch(typeof(NarrativeBlock), nameof(NarrativeBlock.Play))]
    internal static class NarrativeBlockPlayPatch
    {
        private static void Postfix(NarrativeBlock __instance)
        {
            try
            {
                var tracker = VoicelineTracker.Current;
                if (tracker != null)
                    tracker.MarkPlayed(__instance);
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: NarrativeBlock.Play postfix failed: {ex.Message}");
            }
        }
    }
}
