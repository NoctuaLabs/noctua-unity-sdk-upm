using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Live ops progress: per-player progress on live ops keys (missions), kept server-side by the
    /// live ops progress tracker so it survives reinstalls and a reward is claimed at most once.
    ///
    /// <para>The game decides when progress moves and grants the reward itself: call
    /// <see cref="SetProgressAsync"/> with the absolute value (e.g. total matches played) and
    /// <see cref="ClaimProgressAsync"/> when the player takes the reward, granting it only if the
    /// claim succeeds.</para>
    ///
    /// <para><b>When the tracker is down</b> nothing blocks the game:
    /// loads fall back to the player's last saved copy (<see cref="IsProgressStale"/>);
    /// updates are kept on disk and retried (<see cref="LiveOpsProgress.Pending"/>) — safe because
    /// the tracker only ever raises a value; claims fail and are never queued, so a reward is only
    /// granted when the tracker agrees. A daily missions popup is never shown with made-up zeros:
    /// with no saved copy an auto-shown one is skipped and one the player opens raises
    /// <see cref="OnCampaignUnavailable"/> with a short notice.</para>
    ///
    /// <para>Needs a logged-in player (guest is fine) and <c>noctua.progressTrackerBaseUrl</c>.</para>
    /// </summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        /// <summary>How the facade reaches the live ops progress tracker. Test hooks default to production.</summary>
        public sealed class ProgressTrackerOptions
        {
            /// <summary>Tracker base URL ending in <c>/api/v1</c>. Blank means not configured.</summary>
            public string BaseUrl;

            /// <summary>The player's SDK access token.</summary>
            public IAccessTokenProvider AccessTokens;

            /// <summary>The logged-in player's id, or null when nobody is logged in.</summary>
            public Func<long?> PlayerId;

            /// <summary>Shows a short message to the player (the SDK's error toast).</summary>
            public Action<string> ShowNotice;

            /// <summary>Per request; a slower answer counts as the tracker being down.</summary>
            public int TimeoutSeconds = 5;

            /// <summary>Clock for the saved copy's age. Null = <see cref="DateTime.UtcNow"/>.</summary>
            public Func<DateTime> UtcNow;

            /// <summary>Waits before a retry. Null = a real-time delay.</summary>
            public Func<TimeSpan, UniTask> RetryDelay;
        }

        /// <summary>Longest progress key the tracker accepts, in bytes.</summary>
        public const int MaxProgressKeyLength = 128;

        /// <summary>
        /// A saved copy older than this is not shown: it may belong to a missions period that has
        /// since reset, and the tracker does not say when a key's period ends.
        /// </summary>
        public static readonly TimeSpan ProgressCopyMaxAge = TimeSpan.FromHours(24);

        private const string ProgressPrefsPrefix = "Noctua.LiveOpsCampaign.Progress.";
        private const string MissionsUnavailableFallback =
            "Missions are unavailable right now. Please try again later.";
        private static readonly Regex MissionProgressKey = new("^m_.+_progress$", RegexOptions.CultureInvariant);

        private string _progressBaseUrl = "";
        private IAccessTokenProvider _accessTokens;
        private Func<long?> _playerId;
        private Action<string> _showNotice;
        private int _progressTimeoutSeconds = 5;
        private Func<DateTime> _utcNow = () => DateTime.UtcNow;
        private Func<TimeSpan, UniTask> _retryDelay = delay => UniTask.Delay(delay, ignoreTimeScale: true);
        private NoctuaLocale _progressLocale;

        // The loaded player's state: confirmed (or saved) server rows, and updates not yet confirmed.
        private bool _progressLoaded;
        private long? _progressPlayer;
        private readonly Dictionary<string, LiveOpsProgress> _serverProgress = new(StringComparer.Ordinal);
        private readonly Dictionary<string, long> _pendingProgress = new(StringComparer.Ordinal);
        private DateTime? _progressFetchedAtUtc;
        private bool _progressStale;

        private bool _flushingProgress;
        private bool _retryScheduled;
        private int _retryAttempt;
        private bool _autoShowNeedsPlayer;

        // The open popup, when it is a progress-backed one, and the values the game gave it.
        private bool _shownProgressBacked;
        private Dictionary<string, string> _shownGameData;

        /// <summary>Raised after every change to <see cref="Progress"/>, and by <see cref="ClearProgress"/>.</summary>
        public event Action<IReadOnlyList<LiveOpsProgress>> OnProgressChanged;

        /// <summary>
        /// Raised with a campaign id when the player opened a daily missions popup that cannot be
        /// shown: the tracker is down and there is no saved progress. The SDK also shows a notice.
        /// </summary>
        public event Action<string> OnCampaignUnavailable;

        /// <summary>False until <c>noctua.progressTrackerBaseUrl</c> is set in noctuagg.json.</summary>
        public bool IsProgressConfigured => _progressBaseUrl.Length > 0;

        /// <summary>True while <see cref="Progress"/> is a saved copy because the last load failed.</summary>
        public bool IsProgressStale
        {
            get { EnsureProgressPlayer(); return _progressStale; }
        }

        /// <summary>When the progress on screen was last confirmed by the tracker (UTC), or null.</summary>
        public DateTime? ProgressFetchedAtUtc
        {
            get { EnsureProgressPlayer(); return _progressFetchedAtUtc; }
        }

        /// <summary>True while updates wait on disk for the tracker to come back.</summary>
        public bool HasPendingProgress
        {
            get { EnsureProgressPlayer(); return _pendingProgress.Count > 0; }
        }

        /// <summary>
        /// The logged-in player's progress, sorted by key: the last confirmed or saved values with
        /// unconfirmed updates on top (<see cref="LiveOpsProgress.Pending"/>). Empty until loaded.
        /// </summary>
        public IReadOnlyList<LiveOpsProgress> Progress
        {
            get { EnsureProgressPlayer(); return ProgressView(); }
        }

        /// <summary>
        /// Loads the player's progress. Sends waiting updates first. When the tracker is down it
        /// returns the saved copy instead (<see cref="IsProgressStale"/>), and throws a
        /// <see cref="NoctuaErrorCode.Networking"/> error only when there is no copy younger than
        /// <see cref="ProgressCopyMaxAge"/>.
        /// </summary>
        public async UniTask<IReadOnlyList<LiveOpsProgress>> GetProgressAsync()
        {
            var (token, playerId) = RequireProgressCaller();
            EnsureProgressPlayer();

            await FlushPendingCoreAsync(token, playerId);

            try
            {
                var list = await ProgressRequest(HttpMethod.Get, "/progress/{playerId}", token, playerId)
                    .Send<List<LiveOpsProgress>>();

                _serverProgress.Clear();
                foreach (var row in list ?? new List<LiveOpsProgress>())
                {
                    if (!string.IsNullOrEmpty(row?.Key)) _serverProgress[row.Key] = row;
                }
                _progressFetchedAtUtc = _utcNow();
                _progressStale = false;
                DropConfirmedPending();
                SaveProgress();

                return ProgressChanged();
            }
            catch (Exception e) when (Classify(e) == ProgressFailure.Down)
            {
                ScheduleProgressRetry();
                if (!HasUsableProgressCopy()) throw Unreachable(e);

                _log.Warning($"{LogTag} progress tracker unreachable, showing the saved copy: {e.Message}");
                _progressStale = true;
                return ProgressView();
            }
        }

        /// <summary>
        /// Sets <paramref name="key"/> to the absolute <paramref name="value"/> and returns the
        /// progress to show. The tracker keeps the higher of the stored and new value and caps it
        /// at the key's max. Never throws because the tracker is down: the update waits on disk
        /// (<see cref="LiveOpsProgress.Pending"/>) and is retried. Throws when the tracker refuses
        /// the key (not configured, inactive) — retrying could never help.
        /// </summary>
        public async UniTask<LiveOpsProgress> SetProgressAsync(string key, long value)
        {
            ValidateProgressKey(key);
            if (value < 0)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Live ops progress value must not be negative");
            }

            var (token, playerId) = RequireProgressCaller();
            EnsureProgressPlayer();

            var current = LocalProgress(key);
            if (current != null && value <= current.Value) return current;

            _pendingProgress[key] = _pendingProgress.TryGetValue(key, out var queued) ? Math.Max(queued, value) : value;
            SaveProgress();
            ProgressChanged();

            try
            {
                var stored = await PutProgressAsync(key, _pendingProgress[key], token, playerId);
                ConfirmProgress(key, stored);
                await FlushPendingCoreAsync(token, playerId);
            }
            catch (Exception e)
            {
                switch (Classify(e))
                {
                    case ProgressFailure.Down:
                        _log.Debug($"{LogTag} progress '{key}' queued, tracker unreachable: {e.Message}");
                        ScheduleProgressRetry();
                        break;
                    case ProgressFailure.Auth:
                        _log.Debug($"{LogTag} progress '{key}' queued until the next login: {e.Message}");
                        break;
                    default:
                        DropPending(key);
                        throw;
                }
            }

            return LocalProgress(key);
        }

        /// <summary>
        /// Claims a completed <paramref name="key"/>; grant the reward only when this returns.
        /// Sends the key's waiting update first, so a mission completed while the tracker was
        /// down can be claimed. Online only: never queued. Throws
        /// <see cref="NoctuaErrorCode.LiveOpsProgressAlreadyClaimed"/>,
        /// <see cref="NoctuaErrorCode.LiveOpsProgressNotCompleted"/>, or
        /// <see cref="NoctuaErrorCode.Networking"/> when the tracker is down.
        /// </summary>
        public async UniTask<LiveOpsProgress> ClaimProgressAsync(string key)
        {
            ValidateProgressKey(key);
            var (token, playerId) = RequireProgressCaller();
            EnsureProgressPlayer();

            if (_pendingProgress.TryGetValue(key, out var pending))
            {
                try
                {
                    ConfirmProgress(key, await PutProgressAsync(key, pending, token, playerId));
                }
                catch (Exception e)
                {
                    var failure = Classify(e);
                    if (failure == ProgressFailure.Refused) DropPending(key);
                    if (failure == ProgressFailure.Down)
                    {
                        ScheduleProgressRetry();
                        throw Unreachable(e);
                    }
                    throw;
                }
            }

            try
            {
                var claimed = await ProgressRequest(HttpMethod.Post, "/progress/{playerId}/{key}/claim", token, playerId)
                    .WithPathParam("key", key)
                    .Send<LiveOpsProgress>();
                ConfirmProgress(key, claimed);
                return LocalProgress(key);
            }
            catch (NoctuaException e) when (e.ErrorCode == (int)NoctuaErrorCode.LiveOpsProgressAlreadyClaimed)
            {
                var row = _serverProgress.TryGetValue(key, out var known)
                    ? known.Clone()
                    : new LiveOpsProgress { Key = key, Completed = true };
                row.Claimed = true;
                _serverProgress[key] = row;
                SaveProgress();
                ProgressChanged();
                throw;
            }
            catch (Exception e) when (Classify(e) == ProgressFailure.Down)
            {
                throw Unreachable(e);
            }
        }

        /// <summary>
        /// Sends every waiting update now. Returns how many still wait (tracker down, or nobody
        /// logged in). The SDK also retries on its own with a growing delay, on reconnect and after
        /// login; call this to force it.
        /// </summary>
        public async UniTask<int> FlushPendingProgressAsync()
        {
            var caller = TryProgressCaller();
            EnsureProgressPlayer();
            if (caller == null) return _pendingProgress.Count;

            return await FlushPendingCoreAsync(caller.Value.token, caller.Value.playerId);
        }

        /// <summary>
        /// The composition root calls this when the device is back online: sends waiting updates
        /// and reloads progress that is showing a saved copy. Never throws.
        /// </summary>
        public async UniTask OnConnectivityRestoredAsync()
        {
            if (!IsProgressConfigured || TryProgressCaller() == null) return;

            try
            {
                EnsureProgressPlayer();
                await FlushPendingProgressAsync();
                if (_progressStale) await GetProgressAsync();
            }
            catch (Exception e)
            {
                _log.Debug($"{LogTag} progress refresh after reconnect failed: {e.Message}");
            }
        }

        /// <summary>
        /// The composition root calls this when the account changes: shows only the new player's
        /// progress (their saved copy), sends their waiting updates, and runs an auto-show that was
        /// held back for a login. The previous player's saved data stays on disk. Never throws.
        /// </summary>
        public async UniTask OnAccountChangedAsync()
        {
            ClearProgress();
            if (!IsProgressConfigured || TryProgressCaller() == null) return;

            try
            {
                EnsureProgressPlayer();
                await FlushPendingProgressAsync();
            }
            catch (Exception e)
            {
                _log.Debug($"{LogTag} progress flush after login failed: {e.Message}");
            }

            if (_autoShowNeedsPlayer)
            {
                _autoShowNeedsPlayer = false;
                await AutoShowCoreAsync();
            }
        }

        /// <summary>
        /// Forgets the progress in memory (the account changed). The player's saved copy and
        /// waiting updates stay on disk and are loaded again for whoever is logged in next.
        /// </summary>
        public void ClearProgress()
        {
            var had = _serverProgress.Count + _pendingProgress.Count > 0;
            _serverProgress.Clear();
            _pendingProgress.Clear();
            _progressFetchedAtUtc = null;
            _progressStale = false;
            _progressLoaded = false;
            _progressPlayer = null;
            if (had) NotifyProgress(ProgressView());
        }

        /// <summary>
        /// Turns progress into the <c>player_data</c> a daily missions popup reads:
        /// <c>m_&lt;id&gt;_progress</c> (the value) and <c>m_&lt;id&gt;_claimed</c>
        /// (<c>"true"</c>/<c>"false"</c>). The SDK does this itself for daily missions popups when
        /// the tracker is configured; use it when passing values to
        /// <see cref="ShowPopup(string, IReadOnlyDictionary{string, string})"/> yourself.
        /// The popup's target (e.g. "/20") comes from the campaign, not from
        /// <see cref="LiveOpsProgress.Max"/>.
        /// </summary>
        /// <param name="progress">Usually <see cref="Progress"/>.</param>
        /// <param name="missionIdForKey">Maps a tracker key to its mission id; the key itself when null.
        /// Return null or empty to skip a key.</param>
        public static IReadOnlyDictionary<string, string> ToDailyMissionsPlayerData(
            IEnumerable<LiveOpsProgress> progress,
            Func<string, string> missionIdForKey = null)
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal);
            if (progress == null) return data;

            foreach (var item in progress)
            {
                if (string.IsNullOrEmpty(item?.Key)) continue;
                var missionId = missionIdForKey != null ? missionIdForKey(item.Key) : item.Key;
                if (string.IsNullOrEmpty(missionId)) continue;

                data[$"m_{missionId}_progress"] = item.Value.ToString();
                data[$"m_{missionId}_claimed"] = item.Claimed ? "true" : "false";
            }

            return data;
        }

        /// <summary>
        /// True for a daily missions style campaign, i.e. one whose <c>player_data</c> declares
        /// <c>m_&lt;id&gt;_progress</c> keys. With the tracker configured the SDK fills those from
        /// the tracker and never shows the popup without real progress.
        /// </summary>
        public static bool IsProgressBacked(CampaignItem item) =>
            item?.PlayerData != null && item.PlayerData.Keys.Any(key => MissionProgressKey.IsMatch(key));

        /// <summary>The retry delay after <paramref name="attempt"/> failed flushes: 30 s, doubling, at most 10 min.</summary>
        public static TimeSpan ProgressRetryDelay(int attempt)
        {
            var seconds = 30.0 * Math.Pow(2, Math.Max(0, Math.Min(attempt, 10)));
            return TimeSpan.FromSeconds(Math.Min(seconds, 600));
        }

        // ---- setup -------------------------------------------------------------

        private void InitProgress(ProgressTrackerOptions options, NoctuaLocale locale)
        {
            _progressLocale = locale;
            if (options == null) return;

            _progressBaseUrl = (options.BaseUrl ?? "").Trim().TrimEnd('/');
            _accessTokens = options.AccessTokens;
            _playerId = options.PlayerId;
            _showNotice = options.ShowNotice;
            _progressTimeoutSeconds = Math.Max(1, options.TimeoutSeconds);
            if (options.UtcNow != null) _utcNow = options.UtcNow;
            if (options.RetryDelay != null) _retryDelay = options.RetryDelay;
        }

        // ---- popups ------------------------------------------------------------

        private bool GatesOnProgress(CampaignItem item) => IsProgressConfigured && IsProgressBacked(item);

        /// <summary>
        /// Shows a progress-backed campaign with the tracker's values under the game's own.
        /// A usable saved copy shows at once (then refreshes in place); otherwise waits for a load.
        /// False when there is no progress to show — auto-show then moves on, and a popup the
        /// player opened explains itself instead.
        /// </summary>
        private async UniTask<bool> ShowWithProgressAsync(CampaignItem stored, IReadOnlyDictionary<string, string> gameData, bool auto)
        {
            try
            {
                if (TryProgressCaller() == null)
                {
                    if (auto) return false;
                    Unavailable(stored.Id);
                    return false;
                }

                EnsureProgressPlayer();
                IReadOnlyList<LiveOpsProgress> progress = null;
                var refreshAfter = false;

                if (HasUsableProgressCopy())
                {
                    progress = ProgressView();
                    refreshAfter = true;
                }
                else
                {
                    try { progress = await GetProgressAsync(); }
                    catch (Exception e) { _log.Debug($"{LogTag} no progress for '{stored.Id}': {e.Message}"); }
                }

                if (progress == null)
                {
                    if (!auto) Unavailable(stored.Id);
                    return false;
                }

                ShowResolved(stored, MergeProgressData(stored, progress, gameData), progressBacked: true, gameData);
                if (refreshAfter) RefreshProgressQuietlyAsync().Forget();
                return true;
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} showing '{stored?.Id}' with progress failed: {e.Message}");
                return false;
            }
        }

        private async UniTaskVoid RefreshProgressQuietlyAsync()
        {
            try { await GetProgressAsync(); }
            catch (Exception e) { _log.Debug($"{LogTag} background progress refresh failed: {e.Message}"); }
        }

        /// <summary>The tracker's values for the keys the campaign declares, then the game's values on top.</summary>
        private static Dictionary<string, string> MergeProgressData(
            CampaignItem stored,
            IReadOnlyList<LiveOpsProgress> progress,
            IReadOnlyDictionary<string, string> gameData)
        {
            var data = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in ToDailyMissionsPlayerData(progress))
            {
                if (stored.PlayerData != null && stored.PlayerData.ContainsKey(pair.Key)) data[pair.Key] = pair.Value;
            }
            if (gameData != null) foreach (var pair in gameData) data[pair.Key] = pair.Value;
            return data;
        }

        private void Unavailable(string campaignId)
        {
            _log.Info($"{LogTag} campaign '{campaignId}' needs progress the tracker cannot give right now");
            SafeInvoke(OnCampaignUnavailable, campaignId);

            var message = _progressLocale?.GetTranslation(LocaleTextKey.LiveOpsMissionsUnavailable);
            if (string.IsNullOrEmpty(message) || message == nameof(LocaleTextKey.LiveOpsMissionsUnavailable))
            {
                message = MissionsUnavailableFallback;
            }

            try { _showNotice?.Invoke(message); }
            catch (Exception e) { _log.Warning($"{LogTag} notice failed: {e.Message}"); }
        }

        /// <summary>Re-renders an open progress-backed popup with the latest progress.</summary>
        private void RefreshShownProgress()
        {
            if (!_shownProgressBacked || _shownItem == null) return;
            var popup = _host?.PopupIfCreated;
            if (popup == null || !popup.IsShowing) return;

            var data = MergeProgressData(_shownItem, ProgressView(), _shownGameData);
            if (popup.Refresh(RenderItem(_shownItem, data), _manager.Config.SchemaVersion)) _shownPlayerData = data;
        }

        // ---- state -------------------------------------------------------------

        /// <summary>Loads the logged-in player's saved copy when the player changed.</summary>
        private void EnsureProgressPlayer()
        {
            var player = _playerId?.Invoke();
            if (player is not > 0) player = null;
            if (_progressLoaded && player == _progressPlayer) return;

            _serverProgress.Clear();
            _pendingProgress.Clear();
            _progressFetchedAtUtc = null;
            _progressStale = false;
            _progressPlayer = player;
            _progressLoaded = true;

            if (player != null && IsProgressConfigured) LoadProgress(player.Value);
        }

        private bool HasUsableProgressCopy() =>
            _progressFetchedAtUtc.HasValue && _utcNow() - _progressFetchedAtUtc.Value <= ProgressCopyMaxAge;

        private List<LiveOpsProgress> ProgressView()
        {
            var keys = new SortedSet<string>(_serverProgress.Keys, StringComparer.Ordinal);
            keys.UnionWith(_pendingProgress.Keys);

            var view = new List<LiveOpsProgress>(keys.Count);
            foreach (var key in keys) view.Add(ViewOf(key));
            return view;
        }

        private LiveOpsProgress ViewOf(string key)
        {
            var item = _serverProgress.TryGetValue(key, out var server)
                ? server.Clone()
                : new LiveOpsProgress { Key = key };

            if (_pendingProgress.TryGetValue(key, out var pending))
            {
                if (pending > item.Value) item.Value = item.Max > 0 ? Math.Min(pending, item.Max) : pending;
                if (item.Max > 0 && item.Value >= item.Max) item.Completed = true;
                item.Pending = true;
            }

            return item;
        }

        private LiveOpsProgress LocalProgress(string key) =>
            _serverProgress.ContainsKey(key) || _pendingProgress.ContainsKey(key) ? ViewOf(key) : null;

        /// <summary>Stores what the tracker answered; drops the waiting update once it covers it.</summary>
        private void ConfirmProgress(string key, LiveOpsProgress stored)
        {
            if (!string.IsNullOrEmpty(stored?.Key)) _serverProgress[stored.Key] = stored;
            DropConfirmedPending();
            SaveProgress();
            ProgressChanged();
        }

        private void DropConfirmedPending()
        {
            foreach (var key in _pendingProgress.Keys.ToList())
            {
                if (!_serverProgress.TryGetValue(key, out var server)) continue;
                var covered = server.Value >= _pendingProgress[key] || (server.Max > 0 && server.Value >= server.Max);
                if (covered) _pendingProgress.Remove(key);
            }
        }

        private void DropPending(string key)
        {
            if (!_pendingProgress.Remove(key)) return;
            SaveProgress();
            ProgressChanged();
        }

        private IReadOnlyList<LiveOpsProgress> ProgressChanged()
        {
            var snapshot = ProgressView();
            NotifyProgress(snapshot);
            RefreshShownProgress();
            return snapshot;
        }

        private void NotifyProgress(IReadOnlyList<LiveOpsProgress> snapshot)
        {
            try
            {
                OnProgressChanged?.Invoke(snapshot);
            }
            catch (Exception e)
            {
                // A game handler must not turn a successful call into a failed one.
                _log.Warning($"OnProgressChanged handler threw: {e.Message}");
            }
        }

        // ---- sending -----------------------------------------------------------

        /// <summary>Sends waiting updates in turn; stops at the first sign the tracker is down.</summary>
        private async UniTask<int> FlushPendingCoreAsync(string token, string playerId)
        {
            if (_flushingProgress || _pendingProgress.Count == 0) return _pendingProgress.Count;

            _flushingProgress = true;
            try
            {
                foreach (var key in _pendingProgress.Keys.ToList())
                {
                    if (!_pendingProgress.TryGetValue(key, out var value)) continue;
                    try
                    {
                        ConfirmProgress(key, await PutProgressAsync(key, value, token, playerId));
                    }
                    catch (Exception e)
                    {
                        var failure = Classify(e);
                        if (failure == ProgressFailure.Refused)
                        {
                            _log.Warning($"{LogTag} dropping progress '{key}', refused by the tracker: {e.Message}");
                            DropPending(key);
                            continue;
                        }
                        if (failure == ProgressFailure.Down) ScheduleProgressRetry();
                        break;
                    }
                }

                if (_pendingProgress.Count == 0) _retryAttempt = 0;
                return _pendingProgress.Count;
            }
            finally
            {
                _flushingProgress = false;
            }
        }

        private void ScheduleProgressRetry()
        {
            if (_retryScheduled || _pendingProgress.Count == 0) return;
            _retryScheduled = true;
            RetryPendingAfterAsync(ProgressRetryDelay(_retryAttempt++)).Forget();
        }

        private async UniTaskVoid RetryPendingAfterAsync(TimeSpan delay)
        {
            try { await _retryDelay(delay); }
            catch (Exception) { /* a cancelled wait just retries now */ }

            _retryScheduled = false;
            try { await FlushPendingProgressAsync(); }
            catch (Exception e) { _log.Debug($"{LogTag} progress retry failed: {e.Message}"); }
        }

        private UniTask<LiveOpsProgress> PutProgressAsync(string key, long value, string token, string playerId) =>
            ProgressRequest(HttpMethod.Put, "/progress/{playerId}/{key}", token, playerId)
                .WithPathParam("key", key)
                .WithJsonBody(new SetProgressBody { Value = value })
                .Send<LiveOpsProgress>();

        private HttpRequest ProgressRequest(HttpMethod method, string path, string token, string playerId) =>
            new HttpRequest(method, _progressBaseUrl + path)
                .WithPathParam("playerId", playerId)
                .WithHeader("Authorization", "Bearer " + token)
                .WithErrorEnvelope()
                .WithTimeout(_progressTimeoutSeconds);

        private enum ProgressFailure { Down, Auth, Refused }

        /// <summary>
        /// Down = says nothing about the player's data (no connection, timeout, 5xx, 408, 429, or a
        /// body that is not the API envelope). Auth = token or player rejected. Refused = a real
        /// answer about this key or claim.
        /// </summary>
        private static ProgressFailure Classify(Exception e)
        {
            if (e is not NoctuaException noctua) return ProgressFailure.Down;

            switch (noctua.ErrorCode)
            {
                case (int)NoctuaErrorCode.Networking:
                case (int)NoctuaErrorCode.Application:
                    return ProgressFailure.Down;
                case (int)NoctuaErrorCode.Authentication:
                case 2100:
                case 2200:
                    return ProgressFailure.Auth;
                default:
                    return ProgressFailure.Refused;
            }
        }

        private static NoctuaException Unreachable(Exception e) =>
            new(NoctuaErrorCode.Networking, $"Live ops progress tracker is unreachable: {e.Message}");

        private (string token, string playerId) RequireProgressCaller()
        {
            if (!IsProgressConfigured)
            {
                throw new NoctuaException(
                    NoctuaErrorCode.Application,
                    "Live ops progress tracker is not configured: set noctua.progressTrackerBaseUrl in noctuagg.json");
            }

            return TryProgressCaller() ?? throw new NoctuaException(
                NoctuaErrorCode.Authentication,
                "Live ops progress needs a logged-in player: call Noctua.Auth.AuthenticateAsync first");
        }

        private (string token, string playerId)? TryProgressCaller()
        {
            if (!IsProgressConfigured) return null;
            var playerId = _playerId?.Invoke();
            if (_accessTokens == null || !_accessTokens.IsAuthenticated || playerId is not > 0) return null;
            return (_accessTokens.AccessToken, playerId.Value.ToString());
        }

        /// <summary>Fails before any request on what the tracker would reject.</summary>
        private static void ValidateProgressKey(string key)
        {
            var bytes = string.IsNullOrEmpty(key) ? 0 : System.Text.Encoding.UTF8.GetByteCount(key);
            if (bytes == 0 || bytes > MaxProgressKeyLength)
            {
                throw new NoctuaException(
                    NoctuaErrorCode.Application,
                    $"Live ops progress key must be 1 to {MaxProgressKeyLength} bytes");
            }

            if (key.StartsWith("__", StringComparison.Ordinal))
            {
                throw new NoctuaException(
                    NoctuaErrorCode.Application,
                    "Live ops progress key must not start with \"__\"");
            }
        }

        // ---- disk --------------------------------------------------------------

        /// <summary>One saved entry per player, so accounts on one device never mix.</summary>
        public static string ProgressPrefsKey(long playerId) => ProgressPrefsPrefix + playerId;

        private void LoadProgress(long playerId)
        {
            var json = PlayerPrefs.GetString(ProgressPrefsKey(playerId), null);
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var saved = JsonConvert.DeserializeObject<ProgressSave>(json);
                if (saved == null) return;

                foreach (var row in saved.Items ?? new List<LiveOpsProgress>())
                {
                    if (!string.IsNullOrEmpty(row?.Key)) _serverProgress[row.Key] = row;
                }
                foreach (var pair in saved.Pending ?? new Dictionary<string, long>())
                {
                    if (!string.IsNullOrEmpty(pair.Key)) _pendingProgress[pair.Key] = pair.Value;
                }
                _progressFetchedAtUtc = saved.FetchedAtUtc;
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} saved progress unreadable, ignoring it: {e.Message}");
            }
        }

        private void SaveProgress()
        {
            if (_progressPlayer == null) return;

            try
            {
                var save = new ProgressSave
                {
                    FetchedAtUtc = _progressFetchedAtUtc,
                    Items = _serverProgress.Values.ToList(),
                    Pending = new Dictionary<string, long>(_pendingProgress),
                };
                PlayerPrefs.SetString(ProgressPrefsKey(_progressPlayer.Value), JsonConvert.SerializeObject(save));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} saving progress failed: {e.Message}");
            }
        }

        [Preserve]
        private sealed class ProgressSave
        {
            [JsonProperty("fetched_at")] public DateTime? FetchedAtUtc;
            [JsonProperty("items")] public List<LiveOpsProgress> Items;
            [JsonProperty("pending")] public Dictionary<string, long> Pending;
        }

        [Preserve]
        private class SetProgressBody
        {
            public long Value;
        }
    }
}
