using System.Collections;
using Multiplayer;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HSRTimer
{
    /// <summary>
    /// One-key level retry (R6). Guards the R6.1 preconditions, then performs a
    /// full async reload of the retry-target level as a coroutine on the engine:
    /// <c>Game.UnloadLevel()</c> tears the running level down (clearing
    /// <c>currentLevelNumber</c>), the empty transition scene is loaded and held
    /// for the configured minimum dwell, then <c>App.LaunchSinglePlayer</c>
    /// re-launches the target level. That final call's <c>LoadLevel</c> coroutine
    /// only reloads the scene when <c>currentLevelNumber != levelNumber</c>, so
    /// the unload is essential — without it, re-launching the current level
    /// silently degrades into a checkpoint respawn.
    ///
    /// <b>R6.4 retry target.</b> During a campaign run that was entered from the
    /// menu (Intro–Reprise), the target is the level the player STARTED the run
    /// on (<c>RunState.CampaignRetryLevel</c>), not the level they happen to be
    /// playing — so one key always returns the player to their chosen practice
    /// level even after the campaign auto-advances. For every other case
    /// (EditorPick, Workshop, a run not entered from the menu) the target is the
    /// current level, exactly as before R6.4.
    ///
    /// <b>R6.5 user-specified target.</b> The settings panel can additionally
    /// pin the retry target to a fixed level (by English localized name or a
    /// Steam Workshop numeric id). When that option is enabled, it takes
    /// precedence over both the R6.4 campaign-start target and the current-level
    /// fallback; invalid values only show a red HUD hint and never start a retry.
    /// If no level is active (e.g. the main menu), a valid override makes the
    /// retry key directly launch the specified level instead of requiring an
    /// active run.
    ///
    /// This drives the full R6.2.1 flow (load the empty transition scene → reset
    /// the player → reload the level scene → <c>AfterLoad</c>: <c>state =
    /// PlayingLevel</c>, <c>RespawnAllPlayers</c>, <c>Level.Reset(0, 0)</c>),
    /// works for BuiltIn, EditorPick, and Workshop levels alike, and shows no
    /// menu (the App state machine acts as the hidden flow springboard R6.2.1.4
    /// describes).
    ///
    /// The forced dwell in the empty scene lets a fast reload breathe (default
    /// 0.5 s, configurable via <c>retry_min_dwell</c>), measured from the key
    /// press — if the reload would finish sooner, the empty scene is held until
    /// the dwell elapses.
    ///
    /// This is intentionally NOT <c>Game.RestartLevel(true)</c> (a checkpoint
    /// respawn: zeroes <c>currentCheckpointNumber</c>, runs
    /// <c>RespawnAllPlayers</c> + <c>Level.Reset(0, 0)</c> in place, never
    /// reloading the scene) and NOT <c>Game.ReloadBundle()</c> (Workshop-only:
    /// dereferences <c>workshopLevel.dataPath</c> and crashes on BuiltIn levels,
    /// leaving <c>timeScale = 0</c> and the player stuck in the "Empty" scene).
    ///
    /// This is a *level*-level restart and is independent of the run reset
    /// (R1.7): it resets the live timers but keeps the run's records. Per
    /// R5.4.2, a manual retry always clears forgivable validity flags
    /// (unconditional since R5.4.3; formerly gated by
    /// <c>restart_clears_forgivable</c>, which now targets the pause menu's
    /// own Restart button — see <see cref="PauseMenuRestartPatch"/>).
    /// </summary>
    public static class RetryAction
    {
        /// <summary>
        /// Attempt a retry. Returns true if it executed; otherwise logs why not.
        /// <paramref name="host"/> runs the async reload coroutine (the engine
        /// MonoBehaviour, which is <c>DontDestroyOnLoad</c> so it survives the
        /// empty-scene transition).
        /// </summary>
        public static bool TryExecute(MonoBehaviour host, RunState state, SettingsModel settings, out string notifyKey)
        {
            notifyKey = null;

            // Snapshot the live timers up front so a refused LC collection
            // restart can restore them (it zeroes them speculatively before the
            // delegation, then restores if LC declines). Captured before any
            // guard so a blocked retry never mutates state.
            double savedGameTime = state.GameTime;
            double savedSegmentStart = state.SegmentStart;
            double savedRealTime = state.RealTime;
            bool savedRealTimeActive = state.RealTimeActive;

            var game = Game.instance;

            // R6.5: optional user-specified retry target. Resolve it before the
            // R6.1 guards so an invalid value always shows the HUD hint when the
            // retry key is pressed, even if another guard would otherwise block
            // the retry; and so a resolvable value always clears a pending hint.
            // The resolved target is applied below only for the single-level
            // retry path; R6.3 collection retries keep their own "restart the
            // whole collection" behavior.
            bool hasOverride = false;
            ulong overrideLevel = 0UL;
            WorkshopItemSource overrideType = WorkshopItemSource.NotSpecified;
            if (settings.RetryLevelOverrideEnable)
            {
                if (!RetryTargetResolver.TryResolve(settings.RetryLevelOverride,
                        out overrideLevel, out overrideType))
                {
                    settings.RetryTargetInvalidHint = true;
                    notifyKey = "NOTIFY_RETRY_TARGET_INVALID";
                    return false;
                }
                settings.RetryTargetInvalidHint = false;
                hasOverride = true;
            }
            else
            {
                settings.RetryTargetInvalidHint = false;
            }

            // R6.1.2a: no keyboard-capturing UI open (chat, text input, dialog).
            if (MenuSystem.keyboardState != KeyboardState.None)
            {
                notifyKey = "NOTIFY_RETRY_BLOCKED_INPUT";
                return false;
            }

            // R6.1.2b: single-player local only (not host, not client).
            if (!NetGame.isLocal || NetGame.isServer || NetGame.isClient)
            {
                notifyKey = "NOTIFY_RETRY_BLOCKED_STATE";
                return false;
            }

            // R6.1.2c: not while a level is loading; and a level must be active.
            // A resolved R6.5 override is the exception: when no level is active
            // (for example the main menu), the retry key directly launches the
            // specified level instead of reloading the current one.
            //
            // A Workshop level is active whenever Game.workshopLevel is set, even
            // if Game.currentLevelNumber was truncated to a negative int by a
            // Steam Workshop id larger than int.MaxValue (the game itself stores
            // it as an int). Without this, such a level could only be retried
            // once before the guard wrongly reported "no active level".
            bool hasActiveLevel = game != null
                && game.state != GameState.LoadingLevel
                && (game.currentLevelNumber >= 0 || game.workshopLevel != null);
            if (!hasActiveLevel && !hasOverride)
            {
                notifyKey = "NOTIFY_RETRY_BLOCKED_STATE";
                return false;
            }

            if (!hasActiveLevel)
            {
                if (App.instance == null || game == null)
                {
                    notifyKey = "NOTIFY_RETRY_BLOCKED_STATE";
                    return false;
                }
                if (game.state == GameState.LoadingLevel || App.state == AppSate.LoadLevel)
                {
                    notifyKey = "NOTIFY_RETRY_BLOCKED_STATE";
                    return false;
                }

                // Menu entry: no running level needs to be torn down, so launch
                // the specified level directly. The normal Menu→LoadLevel flow
                // starts a fresh run (R1.7.5).
                App.instance.LaunchSinglePlayer(overrideLevel, overrideType, 0, 0);
                notifyKey = "NOTIFY_RETRY_OVERRIDE_RESTARTED";
                return true;
            }

            // R5.4.2: always clear forgivable flags to give a clean retry
            // (fixed behavior — R5.4.3 moved the option's job elsewhere).
            state.Flags.ClearForgivable();

            // R6.3: if a Level Collections (LC) collection run is active, delegate
            // the retry to LC's own "lc restart" command instead of reloading the
            // current level in place. Restarting a collection re-times the WHOLE
            // run from level 1, so this takes precedence over the single-level
            // reload below. LC owns the scene-reload forcing, validation, and
            // level launching (including transient lc-random collections), so
            // dispatching through Shell.RawInvoke reuses all of it and stays in
            // lock-step with LC's console command.
            var lc = LcIntegration.Instance;
            if (lc != null && lc.IsInCollectionRun)
            {
                if (lc.IsDelayedCommandPending)
                {
                    // LC is mid-countdown (lc restart/skip/random <seconds>) and
                    // would refuse an immediate restart. Don't zero the timers.
                    notifyKey = "NOTIFY_RETRY_BLOCKED_STATE";
                    return false;
                }

                // A collection restart re-attempts the whole run: reset the live
                // timers (total + current segment + real time). Use the same
                // Retrying guard as the single-level retry so the abandoned
                // level's segment end is not recorded as a LastRun and the reload
                // (level 1) is not mistaken for a run exit — while keeping the
                // run's records and non-forgivable flags (R6.2.2 applies to a
                // collection restart too: it is independent of the R1.7 reset).
                state.Retrying = true;
                state.GameTime = 0d;
                state.SegmentStart = 0d;
                state.RealTime = 0d;
                state.RealTimeActive = false;

                if (!lc.RestartCollection())
                {
                    // LC refused (absent at call time, run ended, etc.) — restore
                    // the live timers we just zeroed so we don't corrupt the run,
                    // then fall through to the single-level reload as a fallback.
                    state.Retrying = false;
                    state.GameTime = savedGameTime;
                    state.SegmentStart = savedSegmentStart;
                    state.RealTime = savedRealTime;
                    state.RealTimeActive = savedRealTimeActive;
                }
                else
                {
                    SubsegmentManager.Instance?.OnRetryStart();
                    notifyKey = "NOTIFY_COLLECTION_RESTARTED";
                    return true;
                }
            }

            // Mark a retry in progress so the engine treats the level's
            // PlayingLevel → Inactive → PlayingLevel reload as a segment restart
            // without firing the R1.7 full-run clear. A retry re-attempts the
            // target level: reset the live timers (total + current segment +
            // real time) so the level is timed from scratch — but keep the run's
            // records (PBs, completed segments, LastRun) and validity flags
            // (R6.2.2: retry is a level-level restart, not the R1.7 reset).
            state.Retrying = true;
            state.GameTime = 0d;
            state.SegmentStart = 0d;
            state.RealTime = 0d;
            state.RealTimeActive = false;
            SubsegmentManager.Instance?.OnRetryStart();

            // R6.5: if the user specified a retry target, re-launch exactly that
            // level (BuiltIn/EditorPick by English name, Workshop by numeric id)
            // instead of the R6.4 campaign-start-level / current-level decision.
            if (hasOverride)
            {
                float overrideDwell = Mathf.Max(0f, settings.RetryMinDwell);
                host.StartCoroutine(ReloadCoroutine(game, overrideLevel, overrideType, overrideDwell));
                notifyKey = "NOTIFY_RETRY_OVERRIDE_RESTARTED";
                return true;
            }

            // R6.4: pick the retry target. During a campaign run that was
            // entered from the menu, the player wants to practice the level
            // they STARTED the run on, not whatever later level they have since
            // advanced to (the campaign auto-advances Intro→…→Reprise on each
            // pass). CampaignRetryLevel holds that menu-entered level for the
            // whole run; use it as a BuiltIn re-launch so one key always returns
            // to the run's start level. When unset (-1) — an EditorPick/Workshop
            // level, a run not entered from the menu, or an advance the engine
            // didn't tag — fall back to reloading the current level (the
            // pre-R6.4 behavior), preserving identical semantics for those cases.
            //
            // The campaign target only ever applies while the current level is a
            // BuiltIn campaign level. Even if a stale value is still set from an
            // earlier menu-entered campaign run (e.g. the player then entered an
            // EditorPick/Workshop level from the menu), never let it redirect an
            // EditorPick/Workshop retry to that old built-in level — always fall
            // back to the current level (R6.4.5). TimerCore also clears
            // CampaignRetryLevel on such menu entries; this check is a defensive
            // guard in case a segment-start edge was ever missed.
            bool useCampaignRetry = state.CampaignRetryLevel >= 0
                && game.currentLevelType == WorkshopItemSource.BuiltIn;
            ulong levelId = useCampaignRetry
                ? (ulong)state.CampaignRetryLevel
                : GetCurrentLevelId(game);
            WorkshopItemSource levelType = useCampaignRetry
                ? WorkshopItemSource.BuiltIn
                : game.currentLevelType;
            float dwell = Mathf.Max(0f, settings.RetryMinDwell);
            host.StartCoroutine(ReloadCoroutine(game, levelId, levelType, dwell));

            notifyKey = useCampaignRetry
                ? "NOTIFY_CAMPAIGN_RESTARTED"
                : "NOTIFY_LEVEL_RESTARTED";
            return true;
        }

        /// <summary>
        /// Resolve the current level's id for a retry. BuiltIn and EditorPick
        /// levels use <c>Game.currentLevelNumber</c> directly. Workshop levels
        /// must use the full <c>Game.workshopLevel.workshopId</c> instead: the
        /// game stores only a truncated <c>int</c> in
        /// <c>Game.currentLevelNumber</c>, and <c>App.LaunchSinglePlayer</c>
        /// needs the complete ulong Workshop id to look the level up again.
        /// </summary>
        private static ulong GetCurrentLevelId(Game game)
        {
            if (game.workshopLevel != null
                && game.workshopLevel.workshopId != 0UL
                && (game.currentLevelType == WorkshopItemSource.Subscription
                    || game.currentLevelType == WorkshopItemSource.LocalWorkshop))
            {
                return game.workshopLevel.workshopId;
            }

            return (ulong)game.currentLevelNumber;
        }

        /// <summary>
        /// The reload: tear the level down → load the empty transition scene →
        /// hold it until <paramref name="minDwell"/> seconds have elapsed since
        /// the retry → re-launch the level. The empty-scene dwell uses
        /// <c>Time.unscaledTime</c> (real time) so it is unaffected by timeScale.
        /// </summary>
        private static IEnumerator ReloadCoroutine(Game game, ulong levelNumber, WorkshopItemSource levelType, float minDwell)
        {
            // Measured from the key press (this coroutine starts the same frame).
            float start = Time.unscaledTime;

            // 1. Tear the running level down. AfterUnload sets
            //    currentLevelNumber = -1, state = Inactive, clears the level —
            //    which is what lets step 3 actually reload the scene.
            game.UnloadLevel();

            // 2. Load the empty transition scene (R6.2.1.3). Yield a frame so it
            //    activates and is shown.
            SceneManager.LoadScene("Empty");
            yield return null;

            // 3. Hold the empty scene until the minimum dwell elapses. If the
            //    level would otherwise reload faster, this guarantees the pause.
            while (Time.unscaledTime - start < minDwell)
                yield return null;

            // 4. Re-launch the same level. LaunchSinglePlayer → BeginLoadLevel →
            //    LoadLevel reloads the scene (currentLevelNumber != levelNumber
            //    now) → AfterLoad (state = PlayingLevel, player reset to 0).
            App.instance.LaunchSinglePlayer(levelNumber, levelType, 0, 0);
        }
    }
}
