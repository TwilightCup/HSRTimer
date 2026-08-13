using Multiplayer;

namespace HSRTimer
{
    /// <summary>
    /// Timing options read by <see cref="SegmentLogic"/>. Kept narrow so the
    /// logic stays pure and unit-testable independent of the full settings model.
    /// </summary>
    public struct TimingOptions
    {
        public bool CountInPause;
        public bool CountInMenu;
        public bool AutoReset;

        public static TimingOptions FromSettings(SettingsModel s) => new TimingOptions
        {
            CountInPause = s.CountInPause,
            CountInMenu = s.CountInMenu,
            AutoReset = s.AutoReset,
        };
    }

    /// <summary>
    /// Pure timing/segment/reset logic implementing Appendix B of the spec.
    /// Every decision is derived from game state transitions; no Unity side
    /// effects here (the engine applies the returned decisions).
    /// </summary>
    public static class SegmentLogic
    {
        // ── B.1: should the fixed-step clock accumulate this physics tick? ──
        public static bool ShouldAccumulateFixed(
            GameState state, AppSate appState, bool timingActive)
        {
            if (!timingActive)
                return false;
            if (state != GameState.PlayingLevel)
                return false;
            // LoadingLevel never reaches here (state guard). Lobbies and the
            // client-wait gap never count, with no switch to restore them.
            if (appState == AppSate.ClientWaitServerLoad)
                return false;
            if (appState == AppSate.ServerLobby)
                return false;
            if (appState == AppSate.ClientLobby)
                return false;
            return true;
        }

        /// <summary>B.3: should the clock accumulate during menu/lobby (fixed step)?</summary>
        public static bool ShouldAccumulateMenu(
            GameState state, AppSate appState, bool timingActive, TimingOptions opt, bool retrying)
        {
            // A retry dwells in Inactive while reloading — never count that.
            if (retrying)
                return false;
            if (!timingActive || !opt.CountInMenu)
                return false;
            if (state == GameState.Inactive)
                return true;
            if (appState == AppSate.ServerLobby || appState == AppSate.ClientLobby)
                return true;
            return false;
        }

        /// <summary>B.2: should the clock accumulate during pause (unscaled step)?</summary>
        public static bool ShouldAccumulatePause(
            GameState state, bool timingActive, TimingOptions opt)
            => timingActive && opt.CountInPause && state == GameState.Paused;

        // ── R1.2: segment (level) start ───────────────────────────────
        /// <summary>
        /// A segment starts when the game transitions into PlayingLevel from a
        /// loading or inactive state, while not sitting in a multiplayer lobby.
        /// </summary>
        public static bool IsSegmentStart(
            GameState prev, GameState now, AppSate appState)
        {
            if (now != GameState.PlayingLevel)
                return false;
            if (prev != GameState.LoadingLevel && prev != GameState.Inactive)
                return false;
            if (appState == AppSate.ServerLobby || appState == AppSate.ClientLobby)
                return false;
            return true;
        }

        // ── R1.4: segment (level) end ─────────────────────────────────
        /// <summary>
        /// A segment ends when the game leaves PlayingLevel for a load, or for
        /// Inactive while playing locally (local level exit). Lobby transitions
        /// do not end the segment here — they are auto-reset triggers instead.
        /// </summary>
        public static bool IsSegmentEnd(
            GameState prev, GameState now, bool isLocal)
        {
            if (prev != GameState.PlayingLevel)
                return false;
            if (now == GameState.LoadingLevel)
                return true;
            if (now == GameState.Inactive && isLocal)
                return true;
            return false;
        }

        // ── R1.3: resume from pause without resetting ─────────────────
        /// <summary>
        /// After a pause, the clock resumes (without touching segment start or
        /// game time) only if timing had been stopped (e.g. by a reset).
        /// </summary>
        public static bool IsResumeFromPause(
            GameState prev, GameState now, bool timingActive)
            => prev == GameState.Paused && now == GameState.PlayingLevel && !timingActive;

        // ── R6.4: campaign "entered from menu" edge ───────────────────
        /// <summary>
        /// True the instant a single-player campaign run is entered FROM THE
        /// MENU (the player picks a level on the main level-select screen):
        /// the App state machine crosses <c>Menu → LoadLevel</c>. This is the
        /// signal that the about-to-load level is the one-key retry should
        /// return to for a campaign run (R6.4).
        /// <para>
        /// This edge is exclusive to a menu entry. A mid-run campaign advance
        /// (<c>PassLevel</c> → <c>StartNextLevel</c> → <c>LaunchSinglePlayer</c>)
        /// goes <c>PlayLevel → LoadLevel</c> — never through <c>Menu</c>. A
        /// one-key retry's own reload stays in <c>LoadLevel</c> and runs under
        /// the <c>Retrying</c> flag. A multiplayer lobby launch instead hits
        /// <c>ServerLoadLevel</c>/<c>ClientLoadLevel</c>. So only a genuine
        /// menu pick lands here, which is exactly the level to remember.
        /// </para>
        /// </summary>
        public static bool IsMenuEntry(AppSate prevApp, AppSate nowApp)
            => prevApp == AppSate.Menu && nowApp == AppSate.LoadLevel;

        // ── R1.7: auto-reset triggers ─────────────────────────────────
        /// <summary>
        /// Returns true when an auto-reset transition has occurred (R1.7.2 + R1.7.3).
        /// Caller checks the AutoReset option before honoring this.
        /// <para><paramref name="retrying"/> suppresses the two Inactive-going
        /// branches (Paused→Inactive, R1.7.3) so a one-key level retry (R6), whose
        /// reload drives the level through PlayingLevel/Paused → Inactive, does
        /// not clear the run — R6's restart is independent of the run reset
        /// (R6.2.2).</para>
        /// </summary>
        public static bool IsAutoReset(
            GameState prevGame, GameState nowGame,
            AppSate prevApp, AppSate nowApp, bool isLocal, bool retrying)
        {
            // Paused → Inactive (local backs out to menu). Suppressed during retry.
            if (!retrying && prevGame == GameState.Paused && nowGame == GameState.Inactive)
                return true;
            // Host enters lobby
            if (prevApp == AppSate.ServerLoadLobby && nowApp == AppSate.ServerLobby)
                return true;
            // Client enters lobby
            if (prevApp == AppSate.ClientLoadLobby && nowApp == AppSate.ClientLobby)
                return true;
            // R1.7.3: local finishes the whole run (PlayingLevel → Inactive). A
            // retry also hits this edge while reloading, but a genuine run exit
            // always leaves to the menu (Fall → PauseLeave → ExitGame → EnterMenu
            // sets App.state = Menu), whereas a retry stays in LoadLevel. Require
            // both "not retrying" and "actually in the menu" so neither signal
            // alone can spuriously clear the run.
            if (!retrying
                && prevGame == GameState.PlayingLevel && nowGame == GameState.Inactive
                && isLocal && nowApp == AppSate.Menu)
                return true;
            return false;
        }
    }
}
