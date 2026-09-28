using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
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
    /// claim succeeds. Writes never lower a value, so retries are safe.</para>
    ///
    /// <para>Needs a logged-in player (guest is fine) and <c>noctua.progressTrackerBaseUrl</c>.
    /// Failures throw <see cref="NoctuaException"/>; the tracker's own refusals carry one of the
    /// <c>LiveOpsProgress*</c> <see cref="NoctuaErrorCode"/> values (already claimed, not
    /// completed, inactive or unknown key).</para>
    /// </summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        /// <summary>Longest progress key the tracker accepts, in bytes.</summary>
        public const int MaxProgressKeyLength = 128;

        private readonly string _progressBaseUrl;
        private readonly IAccessTokenProvider _accessTokens;
        private readonly Func<long?> _playerId;
        private readonly Dictionary<string, LiveOpsProgress> _progress = new(StringComparer.Ordinal);

        /// <summary>
        /// Raised after every progress call that changes <see cref="Progress"/>, and by
        /// <see cref="ClearProgress"/>.
        /// </summary>
        public event Action<IReadOnlyList<LiveOpsProgress>> OnProgressChanged;

        /// <summary>False until <c>noctua.progressTrackerBaseUrl</c> is set in noctuagg.json.</summary>
        public bool IsProgressConfigured => _progressBaseUrl.Length > 0;

        /// <summary>
        /// The last progress seen, sorted by key: the result of the last
        /// <see cref="GetProgressAsync"/> with every later write applied. Empty until then.
        /// </summary>
        public IReadOnlyList<LiveOpsProgress> Progress =>
            _progress.Values.OrderBy(p => p.Key, StringComparer.Ordinal).ToList();

        /// <summary>
        /// Fetches every key the player has progress on (keys never touched are absent) and
        /// replaces <see cref="Progress"/>.
        /// </summary>
        public async UniTask<IReadOnlyList<LiveOpsProgress>> GetProgressAsync()
        {
            var (token, playerId) = RequireProgressCaller();

            var list = await new HttpRequest(HttpMethod.Get, $"{_progressBaseUrl}/progress/{{playerId}}")
                .WithPathParam("playerId", playerId)
                .WithHeader("Authorization", "Bearer " + token)
                .WithErrorEnvelope()
                .Send<List<LiveOpsProgress>>();

            _progress.Clear();
            foreach (var progress in list ?? new List<LiveOpsProgress>())
            {
                if (!string.IsNullOrEmpty(progress?.Key)) _progress[progress.Key] = progress;
            }

            return ProgressChanged();
        }

        /// <summary>
        /// Sets <paramref name="key"/> to the absolute <paramref name="value"/>. The server keeps
        /// the higher of the stored and new value and caps it at the key's max, so a late or
        /// repeated call cannot move progress back. Returns the stored progress.
        /// </summary>
        public async UniTask<LiveOpsProgress> SetProgressAsync(string key, long value)
        {
            ValidateProgressKey(key);
            if (value < 0)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Live ops progress value must not be negative");
            }

            var (token, playerId) = RequireProgressCaller();

            var progress = await new HttpRequest(HttpMethod.Put, $"{_progressBaseUrl}/progress/{{playerId}}/{{key}}")
                .WithPathParam("playerId", playerId)
                .WithPathParam("key", key)
                .WithHeader("Authorization", "Bearer " + token)
                .WithJsonBody(new SetProgressBody { Value = value })
                .WithErrorEnvelope()
                .Send<LiveOpsProgress>();

            return StoreProgress(progress);
        }

        /// <summary>
        /// Claims a completed <paramref name="key"/>. Exactly one claim ever succeeds, so grant
        /// the reward only when this returns. Throws with
        /// <see cref="NoctuaErrorCode.LiveOpsProgressAlreadyClaimed"/> or
        /// <see cref="NoctuaErrorCode.LiveOpsProgressNotCompleted"/> otherwise.
        /// </summary>
        public async UniTask<LiveOpsProgress> ClaimProgressAsync(string key)
        {
            ValidateProgressKey(key);
            var (token, playerId) = RequireProgressCaller();

            var progress = await new HttpRequest(HttpMethod.Post, $"{_progressBaseUrl}/progress/{{playerId}}/{{key}}/claim")
                .WithPathParam("playerId", playerId)
                .WithPathParam("key", key)
                .WithHeader("Authorization", "Bearer " + token)
                .WithErrorEnvelope()
                .Send<LiveOpsProgress>();

            return StoreProgress(progress);
        }

        /// <summary>Forgets <see cref="Progress"/>. The SDK calls this when the account changes.</summary>
        public void ClearProgress()
        {
            if (_progress.Count == 0) return;
            _progress.Clear();
            ProgressChanged();
        }

        /// <summary>
        /// Turns progress into the <c>player_data</c> a daily missions popup reads:
        /// <c>m_&lt;id&gt;_progress</c> (the value) and <c>m_&lt;id&gt;_claimed</c>
        /// (<c>"true"</c>/<c>"false"</c>). Pass the result to
        /// <see cref="ShowPopup(string, IReadOnlyDictionary{string, string})"/> or
        /// <see cref="UpdatePopupData"/>; keys the campaign does not declare are ignored there.
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

        private (string token, string playerId) RequireProgressCaller()
        {
            if (!IsProgressConfigured)
            {
                throw new NoctuaException(
                    NoctuaErrorCode.Application,
                    "Live ops progress tracker is not configured: set noctua.progressTrackerBaseUrl in noctuagg.json");
            }

            var playerId = _playerId?.Invoke();
            if (_accessTokens == null || !_accessTokens.IsAuthenticated || playerId is not > 0)
            {
                throw new NoctuaException(
                    NoctuaErrorCode.Authentication,
                    "Live ops progress needs a logged-in player: call Noctua.Auth.AuthenticateAsync first");
            }

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

        private LiveOpsProgress StoreProgress(LiveOpsProgress progress)
        {
            if (!string.IsNullOrEmpty(progress?.Key))
            {
                _progress[progress.Key] = progress;
                ProgressChanged();
            }

            return progress;
        }

        private IReadOnlyList<LiveOpsProgress> ProgressChanged()
        {
            var snapshot = Progress;
            try
            {
                OnProgressChanged?.Invoke(snapshot);
            }
            catch (Exception e)
            {
                // A game handler must not turn a successful call into a failed one.
                _log.Warning($"OnProgressChanged handler threw: {e.Message}");
            }

            return snapshot;
        }

        [Preserve]
        private class SetProgressBody
        {
            public long Value;
        }
    }
}
