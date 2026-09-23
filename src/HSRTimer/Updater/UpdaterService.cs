using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace HSRTimer
{
    /// <summary>State machine phase of the update checker (R13).</summary>
    public enum UpdatePhase
    {
        /// <summary>Nothing pending; optionally holding a check error / "up to date" result.</summary>
        Idle,
        /// <summary>Fetching the GitHub releases feed is in flight.</summary>
        Checking,
        /// <summary>A newer release was found; <see cref="UpdaterService.PendingRelease"/> holds it.</summary>
        HasUpdate,
        /// <summary>Downloading / replacing the plugin DLL.</summary>
        Downloading,
        /// <summary>The new DLL is installed; a game restart is required to load it.</summary>
        RestartRequired,
    }

    /// <summary>
    /// Singleton MonoBehaviour that owns the whole "Check Update" feature (R13):
    /// reading the project's GitHub releases Atom feed, reporting the newest
    /// stable release, and downloading + installing the new plugin DLL via
    /// <see cref="UpdateInstaller"/>.
    ///
    /// The feed is fetched from <c>github.com</c> (not <c>api.github.com</c>) so
    /// the check is not subject to the unauthenticated GitHub API rate limit and
    /// works in networks that block api.github.com; the download URL is derived
    /// from the release tag and the repo's fixed asset naming convention
    /// (<c>HSRTimer-v{version}.dll</c>).
    ///
    /// The settings panel's About page and the <c>hsr update</c> console commands
    /// both drive this one state machine. All network/file work runs in
    /// coroutines on the main thread; every failure is captured into
    /// <see cref="ErrorText"/> (raw reason, wrapped in a localized template by the
    /// caller) and logged — nothing propagates into the timer flow.
    ///
    /// C# forbids <c>yield return</c> inside a try block that has a catch clause,
    /// so the network phase lives in <see cref="FetchBytes"/> (try/finally only),
    /// and every error path is handled in the non-yielding processing methods.
    /// </summary>
    public class UpdaterService : MonoBehaviour
    {
        public static UpdaterService Instance { get; private set; }

        // ── public state (read by the panel every frame / console on demand) ──

        /// <summary>Current phase of the update flow.</summary>
        public UpdatePhase Phase { get; private set; } = UpdatePhase.Idle;

        /// <summary>True after a successful check that found no newer release.</summary>
        public bool UpToDate { get; private set; }

        /// <summary>Version tag of the release the last check compared against.</summary>
        public string CheckedVersion { get; private set; }

        /// <summary>Version tag that was installed, set when Phase == RestartRequired.</summary>
        public string InstalledVersion { get; private set; }

        /// <summary>Raw failure reason from the last failed check/download/install, or null.</summary>
        public string ErrorText { get; private set; }

        /// <summary>True when <see cref="ErrorText"/> came from the check step
        /// (vs. the download/install step) — the panel picks the message template.</summary>
        public bool ErrorFromCheck { get; private set; }

        /// <summary>The newer release found by the last check, or null.</summary>
        public ReleaseInfo PendingRelease { get; private set; }

        /// <summary>Download progress 0–100 while <see cref="Phase"/> is Downloading.</summary>
        public int ProgressPercent { get; private set; }

        /// <summary>True while a check or download is in flight (buttons are disabled).</summary>
        public bool IsBusy { get; private set; }

        // ── internals ─────────────────────────────────────────────────────────

        private string _pluginDir;
        private string _currentVersion;
        private string _repoBaseOverride; // session-only test hook (hsr update base)
        private UnityWebRequest _activeRequest;

        private const int CheckTimeoutSeconds = 30;
        private const int DownloadTimeoutSeconds = 120;
        private const int MaxBodyChars = 160;

        /// <summary>Capture bag for <see cref="FetchBytes"/> — read after the
        /// request has been disposed, so it holds plain values only.</summary>
        private sealed class FetchOutcome
        {
            public bool Error;
            public string Message;
            public long HttpCode;
            public byte[] Data;
            public string Text;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            AbortActiveRequest();
        }

        /// <summary>Wire up paths; called once from <see cref="Plugin.Awake"/>.</summary>
        public void Init(string pluginDir, string currentVersion)
        {
            _pluginDir = pluginDir;
            _currentVersion = currentVersion ?? PluginInfo.PLUGIN_VERSION;
            Log($"updater ready (feed: {FeedUrl})");
        }

        /// <summary>Repo base URL — the session-only console override, else
        /// <c>https://github.com/{owner}/{repo}</c> (owner/repo parsed from
        /// <see cref="PluginInfo.PLUGIN_REPOSITORY_URL"/> so they never drift).
        /// The feed and download URLs are both derived from it.</summary>
        public string RepoBase
        {
            get
            {
                if (!string.IsNullOrEmpty(_repoBaseOverride))
                    return _repoBaseOverride;
                ParseRepository(out string owner, out string repo);
                return "https://github.com/" + owner + "/" + repo;
            }
        }

        /// <summary>URL of the GitHub releases Atom feed used for the check.</summary>
        public string FeedUrl => RepoBase + "/releases.atom";

        /// <summary>Construct the download URL for a release asset from its tag
        /// (GitHub redirects this to the real asset storage).</summary>
        public string DownloadUrl(string tag, string assetName)
        {
            string safeTag = Uri.EscapeDataString(tag ?? "");
            return RepoBase + "/releases/download/" + safeTag + "/" + (assetName ?? "");
        }

        /// <summary>Session-only repo-base override for offline testing
        /// (<c>hsr update base</c>). Empty/whitespace clears it.</summary>
        public void SetRepoBaseOverride(string url)
        {
            _repoBaseOverride = string.IsNullOrWhiteSpace(url) ? null : url.Trim().TrimEnd('/');
            Log("update repo base override = " + (_repoBaseOverride ?? "(cleared)"));
        }

        public string RepoBaseOverride => _repoBaseOverride;

        /// <summary>Start an async check for a newer release. No-op while busy.</summary>
        public void CheckForUpdate()
        {
            if (IsBusy)
            {
                Log("update check ignored: another operation is in progress");
                return;
            }
            StartCoroutine(CheckRoutine());
        }

        /// <summary>Download and install <see cref="PendingRelease"/>. No-op while
        /// busy or when no update is pending.</summary>
        public void ApplyUpdate()
        {
            if (IsBusy)
            {
                Log("update apply ignored: another operation is in progress");
                return;
            }
            if (PendingRelease == null)
            {
                Log("update apply ignored: run 'hsr update check' first and only when an update is available");
                return;
            }
            StartCoroutine(ApplyRoutine());
        }

        /// <summary>Abort any in-flight network request (test hook; also on destroy).</summary>
        public void Cancel()
        {
            AbortActiveRequest();
            if (IsBusy)
                Log("update operation cancelled");
        }

        private void AbortActiveRequest()
        {
            var req = _activeRequest;
            _activeRequest = null;
            if (req != null)
            {
                try { req.Abort(); } catch (Exception ex) { LogWarn("abort failed: " + ex.Message); }
            }
        }

        // ── network phase (yield allowed here: try/finally only, no catch) ────

        /// <summary>
        /// GET <paramref name="url"/> and fill <paramref name="outcome"/>. Any
        /// failure — network, HTTP status, unexpected exception — lands in
        /// <paramref name="outcome"/> instead of throwing, so the caller's state
        /// machine always reaches its cleanup. HTTP error bodies are kept (in the
        /// message) to help diagnose blocked/rate-limited responses. Progress is
        /// pushed to <see cref="ProgressPercent"/> when <paramref name="trackProgress"/> is set.
        /// </summary>
        private IEnumerator FetchBytes(string url, int timeout, FetchOutcome outcome, bool trackProgress)
        {
            UnityWebRequest req = null;

            // Phase A: create + configure (no yield; can throw → report + stop).
            try
            {
                req = UnityWebRequest.Get(url);
                _activeRequest = req;
                req.timeout = timeout;
                req.SetRequestHeader("User-Agent", "HSRTimer-Updater/" + _currentVersion);
            }
            catch (Exception ex)
            {
                outcome.Error = true;
                outcome.Message = ex.Message;
                yield break;
            }

            // Phase B: wait for completion (yield; try/finally only).
            try
            {
                var op = req.SendWebRequest();
                while (!op.isDone)
                {
                    if (trackProgress)
                        ProgressPercent = Mathf.Clamp(Mathf.RoundToInt(req.downloadProgress * 100f), 0, 100);
                    yield return null;
                }
                if (trackProgress)
                    ProgressPercent = 100;

                if (req.isNetworkError)
                {
                    outcome.Error = true;
                    outcome.Message = "network error: " + req.error;
                }
                else if (req.isHttpError)
                {
                    outcome.Error = true;
                    outcome.HttpCode = req.responseCode;
                    string body = req.downloadHandler != null ? req.downloadHandler.text : null;
                    outcome.Message = BuildHttpMessage(req.responseCode, body);
                }
                else
                {
                    outcome.Data = req.downloadHandler != null ? req.downloadHandler.data : null;
                    outcome.Text = req.downloadHandler != null ? req.downloadHandler.text : null;
                }
            }
            finally
            {
                _activeRequest = null;
                if (req != null) req.Dispose();
            }
        }

        private static string BuildHttpMessage(long code, string body)
        {
            string msg = "HTTP " + code;
            if (!string.IsNullOrEmpty(body))
            {
                string t = body.Trim();
                if (t.Length > MaxBodyChars)
                    t = t.Substring(0, MaxBodyChars) + "…";
                msg += ": " + t;
            }
            return msg;
        }

        // ── check ─────────────────────────────────────────────────────────────

        private IEnumerator CheckRoutine()
        {
            IsBusy = true;
            Phase = UpdatePhase.Checking;
            ErrorText = null;
            ErrorFromCheck = true;
            UpToDate = false;
            PendingRelease = null;
            Log($"update check started: {FeedUrl} (running {_currentVersion})");

            var outcome = new FetchOutcome();
            yield return FetchBytes(FeedUrl, CheckTimeoutSeconds, outcome, trackProgress: false);

            ProcessCheckOutcome(outcome);

            IsBusy = false;
            if (Phase == UpdatePhase.Checking)
                Phase = UpdatePhase.Idle; // error / up-to-date land here
        }

        private void ProcessCheckOutcome(FetchOutcome outcome)
        {
            try
            {
                if (outcome.Error)
                {
                    if (outcome.HttpCode == 404)
                    {
                        // The repo has no releases feed / releases yet — not an error.
                        UpToDate = true;
                        CheckedVersion = _currentVersion;
                        Log("update check: no releases found (HTTP 404); treating as up to date");
                    }
                    else
                    {
                        ErrorText = outcome.Message;
                        LogWarn("update check failed: " + ErrorText);
                    }
                    return;
                }
                HandleFeedPayload(outcome.Text);
            }
            catch (Exception ex)
            {
                ErrorText = ex.Message;
                LogWarn("update check exception: " + ex);
            }
        }

        private void HandleFeedPayload(string payload)
        {
            if (!AtomFeed.TryParseLatest(payload, out ReleaseInfo release, out string parseError))
            {
                ErrorText = "unexpected response (" + parseError + ")";
                LogWarn("update check failed: " + ErrorText);
                return;
            }

            CheckedVersion = release.Tag;
            bool newer = IsNewerThanCurrent(release.Tag);
            if (newer)
            {
                PendingRelease = release;
                Phase = UpdatePhase.HasUpdate;
                Log($"update available: {release.Tag} \"{release.Title}\"");
            }
            else
            {
                UpToDate = true;
                Log($"update check: running {_currentVersion}, latest release {release.Tag} — up to date");
            }
        }

        private bool IsNewerThanCurrent(string tag)
        {
            if (VersionUtil.TryParse(tag, out VersionUtil.SemVer remote)
                && VersionUtil.TryParse(_currentVersion, out VersionUtil.SemVer current))
                return VersionUtil.Compare(remote, current) > 0;
            // Unparseable side: be conservative and treat any different tag as newer.
            return !string.Equals(tag, _currentVersion, StringComparison.OrdinalIgnoreCase);
        }

        // ── apply (download + install) ────────────────────────────────────────

        private IEnumerator ApplyRoutine()
        {
            ReleaseInfo release = PendingRelease;
            if (release == null)
                yield break;

            IsBusy = true;
            Phase = UpdatePhase.Downloading;
            ErrorText = null;
            ErrorFromCheck = false;
            ProgressPercent = 0;

            string assetName;
            string assetUrl;
            string tempPath;
            try
            {
                // The repo ships versioned plugin DLLs under a fixed convention
                // (HSRTimer-v{version}.dll), so the asset URL is derived from the
                // release tag — no API round trip needed.
                string version = release.Tag.TrimStart('v', 'V');
                assetName = "HSRTimer-v" + version + ".dll";
                assetUrl = DownloadUrl(release.Tag, assetName);
                tempPath = Path.Combine(_pluginDir, assetName + ".tmp");
                Log($"update download started: {assetName} <- {assetUrl}");
            }
            catch (Exception ex)
            {
                ErrorText = ex.Message;
                LogWarn("update apply exception: " + ex);
                IsBusy = false;
                Phase = UpdatePhase.Idle;
                yield break;
            }

            var outcome = new FetchOutcome();
            yield return FetchBytes(assetUrl, DownloadTimeoutSeconds, outcome, trackProgress: true);

            ProcessDownloadOutcome(outcome, release, assetName, tempPath);

            IsBusy = false;
            if (Phase == UpdatePhase.Downloading)
                Phase = UpdatePhase.Idle; // failure lands here
        }

        private void ProcessDownloadOutcome(FetchOutcome outcome, ReleaseInfo release, string assetName, string tempPath)
        {
            try
            {
                if (outcome.Error)
                {
                    // A 404 on the derived asset URL almost always means the
                    // release does not ship a DLL under the expected name.
                    ErrorText = outcome.HttpCode == 404
                        ? "release asset not found: " + assetName
                        : outcome.Message;
                    LogWarn("update download failed: " + ErrorText);
                    return;
                }
                byte[] data = outcome.Data;
                if (data == null || data.Length == 0)
                {
                    ErrorText = "download failed: empty response";
                    LogWarn("update download failed: " + ErrorText);
                    return;
                }
                try
                {
                    File.WriteAllBytes(tempPath, data);
                    if (UpdateInstaller.Install(_pluginDir, tempPath, assetName, out string installError))
                    {
                        InstalledVersion = release.Tag;
                        Phase = UpdatePhase.RestartRequired;
                        Log($"update installed: {assetName} — restart the game to load it");
                    }
                    else
                    {
                        ErrorText = installError;
                        LogWarn("update install failed: " + ErrorText);
                    }
                }
                catch (Exception ex)
                {
                    ErrorText = "write failed: " + ex.Message;
                    LogWarn("update install exception: " + ex);
                }
                finally
                {
                    TryDeleteTemp(tempPath);
                }
            }
            catch (Exception ex)
            {
                ErrorText = ex.Message;
                LogWarn("update apply exception: " + ex);
            }
        }

        private static void TryDeleteTemp(string tempPath)
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"HSRTimer: failed to remove temp file '{tempPath}': {ex.Message}"); }
        }

        // ── helpers ───────────────────────────────────────────────────────────

        /// <summary>Parse {owner}/{repo} out of the repository URL (best effort).</summary>
        internal static void ParseRepository(out string owner, out string repo)
        {
            owner = "TwilightCup";
            repo = "HSRTimer";
            try
            {
                var uri = new Uri(PluginInfo.PLUGIN_REPOSITORY_URL);
                string[] seg = uri.AbsolutePath.Split('/');
                if (seg.Length >= 3 && !string.IsNullOrEmpty(seg[1]) && !string.IsNullOrEmpty(seg[2]))
                {
                    owner = seg[1];
                    repo = seg[2];
                    if (repo.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
                        repo = repo.Substring(0, repo.Length - 4);
                }
            }
            catch (Exception ex)
            {
                Plugin.Logger.LogWarning($"HSRTimer: failed to parse repository URL '{PluginInfo.PLUGIN_REPOSITORY_URL}': {ex.Message}");
            }
        }

        internal static void Log(string message)
        {
            if (Plugin.Logger != null)
                Plugin.Logger.LogInfo("HSRTimer[update]: " + message);
        }

        internal static void LogWarn(string message)
        {
            if (Plugin.Logger != null)
                Plugin.Logger.LogWarning("HSRTimer[update]: " + message);
        }
    }
}
