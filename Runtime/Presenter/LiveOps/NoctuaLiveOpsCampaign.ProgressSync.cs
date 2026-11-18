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
    /// <summary>Live ops progress on its way out: sending waiting updates to the tracker with retries, and the per-player save on the device.</summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        // ---- sending -----------------------------------------------------------

        /// <summary>Sends waiting updates in turn; stops at the first sign the tracker is down.</summary>
        private async UniTask<int> FlushPendingCoreAsync(string token, string playerId)
        {
            if (_flushingProgress || _pendingProgress.Count + _pendingPurchaseClaims.Count == 0) return _pendingProgress.Count;

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

                if (_pendingProgress.Count == 0) await RetryPurchaseClaimsAsync();
                if (_pendingProgress.Count == 0 && _pendingPurchaseClaims.Count == 0) _retryAttempt = 0;
                return _pendingProgress.Count;
            }
            finally
            {
                _flushingProgress = false;
            }
        }

        private void ScheduleProgressRetry()
        {
            if (_retryScheduled || _pendingProgress.Count + _pendingPurchaseClaims.Count == 0) return;
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
                    "Live ops progress tracker URL is not set on this facade");
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
            var saved = ReadSave(PlayerPrefs.GetString(ProgressPrefsKey(playerId), null));
            if (saved == null) return;

            foreach (var row in saved.Items ?? new List<LiveOpsProgress>())
            {
                if (!string.IsNullOrEmpty(row?.Key)) _serverProgress[row.Key] = row;
            }
            foreach (var pair in saved.Pending ?? new Dictionary<string, long>())
            {
                if (!string.IsNullOrEmpty(pair.Key)) _pendingProgress[pair.Key] = pair.Value;
            }
            foreach (var key in saved.PendingClaims ?? new List<string>())
            {
                if (!string.IsNullOrEmpty(key)) _pendingPurchaseClaims.Add(key);
            }
            _undeliveredRewards.AddRange((saved.UndeliveredRewards ?? new List<LiveOpsReward>())
                .Where(r => r != null && !string.IsNullOrEmpty(r.ClaimId)));
            _rewardHistory.AddRange((saved.RewardHistory ?? new List<string>()).Where(id => !string.IsNullOrEmpty(id)));
            _progressFetchedAtUtc = saved.FetchedAtUtc;
        }

        /// <summary>A player's save from its JSON, or null when there is none or it cannot be read.</summary>
        private ProgressSave ReadSave(string json)
        {
            if (string.IsNullOrEmpty(json)) return null;
            try
            {
                return JsonConvert.DeserializeObject<ProgressSave>(json);
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} saved progress unreadable, ignoring it: {e.Message}");
                return null;
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
                    PendingClaims = _pendingPurchaseClaims.ToList(),
                    UndeliveredRewards = _undeliveredRewards.ToList(),
                    RewardHistory = _rewardHistory.ToList(),
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
            [JsonProperty("pending_claims")] public List<string> PendingClaims;
            [JsonProperty("undelivered_rewards")] public List<LiveOpsReward> UndeliveredRewards;
            [JsonProperty("reward_history")] public List<string> RewardHistory;
        }

        [Preserve]
        private class SetProgressBody
        {
            public long Value;
        }
    }
}
