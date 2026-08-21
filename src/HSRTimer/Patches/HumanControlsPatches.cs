using HarmonyLib;

namespace HSRTimer
{
    /// <summary>
    /// Harmony postfix on <c>HumanControls.HandleInput()</c>: while the Jumpless
    /// tag (R3.5.3) is enabled, force <c>controls.jump = false</c> so the local
    /// player physically cannot jump — enforcement on top of the R3.5.2 detection
    /// in <see cref="JumplessTagRule"/>, which stays as the fallback should this
    /// patch ever fail to apply.
    ///
    /// Why a patch when the field is pollable (the "poll, don't patch" exception,
    /// see docs/ARCHITECTURE.md): suppression is a WRITE into the game's input
    /// chain, not an observation. The game writes <c>controls.jump</c> inside
    /// NetPlayer.PreFixedUpdate → HandleInput and consumes it in
    /// Human.FixedUpdate the same physics frame; writing the field from this
    /// plugin's own FixedUpdate would race on Unity script execution order (our
    /// GameObject is created at runtime, default order group) and could be
    /// overwritten same-frame or land a frame too late. A postfix inside
    /// HandleInput runs synchronously in the game's own call chain, strictly
    /// before any reader — order-safe by construction.
    ///
    /// Scope: jump only. Grab / arm-extend / playDead / fireworks flow through
    /// the same method untouched, and the jump key's secondary role as
    /// pause-menu confirm (HumanControls.ControllerJumpPressed reads the
    /// binding directly) is unaffected. HandleInput is called for every
    /// NetPlayer (local and remote replicas) — the local-player gate ensures
    /// remote players' input is never touched. The tag set is read live on
    /// every jump press (never cached), so panel toggles apply instantly and
    /// there is no state to restore when the tag is turned off. Guarded so a
    /// thrown exception logs instead of crashing the game (N7).
    /// </summary>
    [HarmonyPatch(typeof(HumanControls), nameof(HumanControls.HandleInput))]
    internal static class HumanControlsHandleInputPatch
    {
        private static void Postfix(HumanControls __instance)
        {
            try
            {
                // Hot path: runs every physics frame for every NetPlayer —
                // cheap early-out before touching any plugin state.
                if (!__instance.jump)
                    return;
                // Local player only — never police remote replicas.
                var human = Human.Localplayer;
                if (human == null || human.controls != __instance)
                    return;
                var cfg = ConfigService.Instance;
                if (cfg == null || !cfg.EnabledTags.HasTag(TagIds.Jumpless))
                    return;
                __instance.jump = false;
            }
            catch (System.Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: HumanControls.HandleInput postfix failed: {ex.Message}");
            }
        }
    }
}
