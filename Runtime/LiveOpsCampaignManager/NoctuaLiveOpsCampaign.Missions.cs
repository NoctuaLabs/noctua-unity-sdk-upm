using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Daily mission claims done by the SDK — a popup's <c>claim</c> / <c>claim_all</c> button, or
    /// the game calling <see cref="ClaimMissionRewardAsync"/> / <see cref="ClaimAllMissionsAsync"/>
    /// — and delivery of their rewards to the game's <c>MissionRewards</c> handler.
    ///
    /// <para>Each mission is its own tracker claim, so the server allows it once per player. The
    /// reward is saved on the device (per player) the moment the claim succeeds and handed to the
    /// handler; it is dropped only when the handler finishes without throwing. If the app dies
    /// first, or the handler throws, it is delivered again after the next init or login — so a
    /// reward is never lost, and a game that grants on its own server dedupes on
    /// <see cref="MissionReward.ClaimId"/>.</para>
    /// </summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        /// <summary>
        /// <c>player_data</c> key a daily missions popup can declare: how many of its missions are
        /// completed and not claimed. Show a Claim All button with <c>visible_if claimable_count gt 0</c>.
        /// </summary>
        public const string ClaimableCountKey = "claimable_count";

        private readonly List<MissionReward> _undeliveredRewards = new();
        private Func<IReadOnlyList<MissionReward>, UniTask> _missionRewards;
        private bool _deliveringRewards;
        private bool _warnedNoRewardHandler;

        /// <summary>Rewards claimed for the current player that the game has not confirmed yet.</summary>
        public IReadOnlyList<MissionReward> UndeliveredMissionRewards => _undeliveredRewards.ToList();

        /// <summary>
        /// Claims daily mission <paramref name="missionKey"/> of campaign <paramref name="campaignId"/>
        /// and delivers its reward through the <c>MissionRewards</c> handler; also returns it. Throws
        /// like <see cref="ClaimProgressAsync"/> (e.g. already claimed, not completed, offline).
        /// </summary>
        public async UniTask<MissionReward> ClaimMissionRewardAsync(string campaignId, string missionKey)
        {
            RequireMissionKey(missionKey);
            var item = RequireMissionCampaign(campaignId, missionKey);

            var trackerKey = TrackerKey(item, missionKey);
            await ClaimProgressAsync(trackerKey);

            var reward = RewardOf(item, missionKey, trackerKey);
            RecordRewards(new[] { reward });
            return reward;
        }

        /// <summary>
        /// Claims every completed, unclaimed mission of daily missions campaign
        /// <paramref name="campaignId"/> and delivers their rewards through the <c>MissionRewards</c>
        /// handler; also returns them. Missions already claimed elsewhere are skipped. Stops when
        /// the tracker goes down (or rejects the login) and returns what was claimed before; throws
        /// that error only when nothing could be claimed.
        /// </summary>
        public async UniTask<IReadOnlyList<MissionReward>> ClaimAllMissionsAsync(string campaignId)
        {
            var item = FindCampaign(campaignId)
                       ?? throw new NoctuaException(NoctuaErrorCode.Application, $"No live ops campaign '{campaignId}'");

            var progress = await GetProgressAsync();
            var claimed = new List<MissionReward>();
            NoctuaException down = null;

            foreach (var mission in ClaimableMissions(item, progress))
            {
                var trackerKey = TrackerKey(item, mission);
                try
                {
                    await ClaimProgressAsync(trackerKey);
                    claimed.Add(RewardOf(item, mission, trackerKey));
                }
                catch (NoctuaException e) when (
                    e.ErrorCode == (int)NoctuaErrorCode.Networking || Classify(e) == ProgressFailure.Auth)
                {
                    down = e;
                    break;
                }
                catch (NoctuaException e) when (IsClaimSkip(e))
                {
                    _log.Debug($"{LogTag} claim all '{campaignId}': '{mission}' skipped ({e.Message})");
                }
                catch (NoctuaException e)
                {
                    _log.Warning($"{LogTag} claim all '{campaignId}': '{mission}' refused: {e.Message}");
                }
            }

            if (claimed.Count == 0 && down != null) throw down;
            RecordRewards(claimed);
            return claimed;
        }

        /// <summary>
        /// The missions of <paramref name="item"/> that are completed and not claimed in
        /// <paramref name="progress"/>, in progress order.
        /// </summary>
        public static IReadOnlyList<string> ClaimableMissions(CampaignItem item, IEnumerable<LiveOpsProgress> progress)
        {
            if (item == null || progress == null) return Array.Empty<string>();

            return progress
                .Where(row => row != null && !string.IsNullOrEmpty(row.Key) && row.Completed && !row.Claimed)
                .Select(row => DeclaredMissionOf(item, row.Key))
                .Where(mission => !string.IsNullOrEmpty(mission))
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>
        /// Hands every undelivered reward to the <c>MissionRewards</c> handler. The SDK calls it after
        /// a claim, when the handler is set and after login; call it to retry sooner. Never throws.
        /// </summary>
        public async UniTask DeliverMissionRewardsAsync()
        {
            EnsureProgressPlayer();
            if (_deliveringRewards || _undeliveredRewards.Count == 0) return;
            if (_missionRewards == null)
            {
                if (!_warnedNoRewardHandler)
                {
                    _warnedNoRewardHandler = true;
                    _log.Warning($"{LogTag} {_undeliveredRewards.Count} mission reward(s) are waiting for the game: " +
                                 "set MissionRewards in Noctua.LiveOps.Campaign.Configure to grant them.");
                }
                return;
            }

            _deliveringRewards = true;
            var player = _progressPlayer;
            try
            {
                while (_undeliveredRewards.Count > 0 && player == _progressPlayer)
                {
                    var batch = _undeliveredRewards.ToList();
                    await _missionRewards(batch);

                    // Only this player's list, and only what the handler was given: rewards claimed
                    // while it ran go in the next round.
                    if (player != _progressPlayer) break;
                    _undeliveredRewards.RemoveAll(batch.Contains);
                    SaveProgress();
                    _log.Info($"{LogTag} delivered mission rewards: {string.Join(", ", batch)}");
                }
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} MissionRewards handler threw, keeping the rewards to deliver again: {e.Message}");
            }
            finally
            {
                _deliveringRewards = false;
            }
        }

        /// <summary>Sets the game's reward handler and delivers anything that was waiting for it.</summary>
        private void SetMissionRewardsHandler(Func<IReadOnlyList<MissionReward>, UniTask> handler)
        {
            _missionRewards = handler;
            _warnedNoRewardHandler = false;
            DeliverMissionRewardsAsync().Forget();
        }

        private void RecordRewards(IReadOnlyCollection<MissionReward> rewards)
        {
            if (rewards.Count == 0) return;
            _undeliveredRewards.AddRange(rewards);
            SaveProgress();
            DeliverMissionRewardsAsync().Forget();
        }

        /// <summary>The reward the admin set on <paramref name="mission"/> (<c>m_&lt;key&gt;_reward_id</c> / <c>_reward_qty</c>).</summary>
        private MissionReward RewardOf(CampaignItem item, string mission, string trackerKey)
        {
            string Data(string field) =>
                item.Data != null && item.Data.TryGetValue($"m_{mission}_{field}", out var value) ? value?.Trim() ?? "" : "";

            return new MissionReward
            {
                ClaimId = trackerKey,
                CampaignId = item.Id,
                MissionKey = mission,
                RewardId = Data("reward_id"),
                Quantity = Data("reward_qty"),
                ClaimedAtUtc = _utcNow(),
            };
        }

        private CampaignItem RequireMissionCampaign(string campaignId, string missionKey)
        {
            var item = FindCampaign(campaignId)
                       ?? throw new NoctuaException(NoctuaErrorCode.Application, $"No live ops campaign '{campaignId}'");
            if (!HasMission(item, missionKey))
            {
                throw new NoctuaException(NoctuaErrorCode.Application, $"Campaign '{campaignId}' has no mission '{missionKey}'");
            }
            return item;
        }

        private static bool IsClaimSkip(NoctuaException e) =>
            e.ErrorCode == (int)NoctuaErrorCode.LiveOpsProgressAlreadyClaimed
            || e.ErrorCode == (int)NoctuaErrorCode.LiveOpsProgressNotCompleted;

        /// <summary>
        /// A <c>claim_all</c> button: claims everything finished; the rewards go to the handler and
        /// the open popup refreshes from the new progress. When the tracker is down or the login is
        /// gone, the player gets the usual "missions unavailable" notice. Never throws.
        /// </summary>
        private async UniTask ClaimAllFromPopupAsync(CampaignItem item)
        {
            try
            {
                var claimed = await ClaimAllMissionsAsync(item?.Id);
                _log.Info($"{LogTag} claim all in '{item?.Id}': {(claimed.Count == 0 ? "nothing to claim" : string.Join(", ", claimed))}");
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} claim all in '{item?.Id}' failed: {e.Message}");
                ShowMissionsUnavailableNotice();
            }
        }

        /// <summary>
        /// A mission's <c>claim</c> button: like <see cref="ClaimAllFromPopupAsync"/> for one mission.
        /// Already claimed (another device) or not completed just refreshes the popup. Never throws.
        /// </summary>
        private async UniTask ClaimMissionFromPopupAsync(CampaignItem item, string mission)
        {
            try
            {
                var reward = await ClaimMissionRewardAsync(item?.Id, mission);
                _log.Info($"{LogTag} claimed {reward}");
            }
            catch (NoctuaException e) when (IsClaimSkip(e))
            {
                _log.Debug($"{LogTag} claim '{mission}' in '{item?.Id}' skipped ({e.Message})");
                RefreshProgressQuietlyAsync().Forget();
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} claim '{mission}' in '{item?.Id}' failed: {e.Message}");
                ShowMissionsUnavailableNotice();
            }
        }
    }
}
