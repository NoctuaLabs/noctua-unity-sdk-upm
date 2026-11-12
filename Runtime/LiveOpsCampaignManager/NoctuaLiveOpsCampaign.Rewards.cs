using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// Reward delivery shared by every source — missions, milestone chests and campaign bundle
    /// purchases. A reward is saved on the device for the player who earned it as soon as it is
    /// earned, handed to the game's <c>Rewards</c> handler, and dropped only when the handler
    /// finishes without throwing; otherwise it is delivered again after the next init or login.
    /// A recent-claim history keeps the same reward from being recorded twice (e.g. a purchase
    /// reported again).
    /// </summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        /// <summary>Order <c>extra</c> key the SDK sets when a purchase's items are delivered by <c>Rewards</c>.</summary>
        public const string ExtraDelivery = "noctua_delivery";

        /// <summary>Value of <see cref="ExtraDelivery"/> for such an order.</summary>
        public const string DeliveryRewards = "rewards";

        // Claim ids remembered per player so a reward is never recorded twice; oldest dropped first.
        private const int RewardHistoryLimit = 500;

        private readonly List<LiveOpsReward> _undeliveredRewards = new();
        private readonly List<string> _rewardHistory = new();
        private Func<IReadOnlyList<LiveOpsReward>, UniTask> _rewards;
        private bool _deliveringRewards;
        private bool _warnedNoRewardHandler;

        /// <summary>Rewards earned by the current player that the game has not confirmed yet.</summary>
        public IReadOnlyList<LiveOpsReward> UndeliveredRewards => _undeliveredRewards.ToList();

        /// <summary>True once the game has set a <c>Rewards</c> handler.</summary>
        public bool HasRewardsHandler => _rewards != null;

        /// <summary>
        /// True when the SDK delivers this order's items through the <c>Rewards</c> handler, so the
        /// game must not grant it by SKU too. Decided when the purchase started and kept in the order.
        /// </summary>
        public static bool IsDeliveredByRewards(IReadOnlyDictionary<string, string> orderExtra) =>
            orderExtra != null
            && orderExtra.TryGetValue(ExtraDelivery, out var mode)
            && mode == DeliveryRewards;

        /// <summary>
        /// Hands every undelivered reward to the <c>Rewards</c> handler. The SDK calls it after each
        /// reward, when the handler is set and after login; call it to retry sooner. Never throws.
        /// </summary>
        public async UniTask DeliverRewardsAsync()
        {
            EnsureProgressPlayer();
            if (_deliveringRewards || _undeliveredRewards.Count == 0) return;
            if (_rewards == null)
            {
                if (!_warnedNoRewardHandler)
                {
                    _warnedNoRewardHandler = true;
                    _log.Warning($"{LogTag} {_undeliveredRewards.Count} reward(s) are waiting for the game: " +
                                 "set Rewards in Noctua.LiveOps.Campaign.Configure to grant them.");
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
                    await _rewards(batch);

                    // Only this player's list, and only what the handler was given: rewards earned
                    // while it ran go in the next round.
                    if (player != _progressPlayer) break;
                    _undeliveredRewards.RemoveAll(batch.Contains);
                    SaveProgress();
                    _log.Info($"{LogTag} delivered rewards: {string.Join("; ", batch)}");
                }
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} Rewards handler threw, keeping the rewards to deliver again: {e.Message}");
            }
            finally
            {
                _deliveringRewards = false;
            }
        }

        /// <summary>
        /// Records the items of a verified purchase that the SDK delivers (<see cref="IsDeliveredByRewards"/>)
        /// for <paramref name="buyerId"/> — the player logged in when it was paid, or the current
        /// one when unknown. Reporting the same order again records nothing. Never throws.
        /// </summary>
        public void RecordPurchaseReward(OrderRequest order, long? buyerId)
        {
            try
            {
                var reward = PurchaseRewardOf(order);
                if (reward == null) return;

                EnsureProgressPlayer();
                var current = _progressPlayer;
                if (buyerId is not > 0 || buyerId == current)
                {
                    if (current == null)
                    {
                        _log.Warning($"{LogTag} purchase reward {reward.ClaimId} has no player to go to; not recorded");
                        return;
                    }
                    RecordRewards(new[] { reward });
                    return;
                }

                // Paid by a player who is not logged in now (the account changed meanwhile): it is
                // theirs, so it waits in their own save until they log in again.
                RecordForOtherPlayer(buyerId.Value, reward);
            }
            catch (Exception e)
            {
                _log.Warning($"{LogTag} recording the reward of order {order?.Id} failed: {e.Message}");
            }
        }

        /// <summary>The reward of an order the SDK delivers, or null for any other order.</summary>
        public static LiveOpsReward PurchaseRewardOf(OrderRequest order)
        {
            if (order == null || order.Id <= 0 || !IsDeliveredByRewards(order.Extra)) return null;

            order.Extra.TryGetValue(ExtraBundle, out var bundle);
            var items = RewardItem.ParseBundle(bundle);
            if (items == null) return null;

            order.Extra.TryGetValue(ExtraCampaignId, out var campaignId);
            return new LiveOpsReward
            {
                ClaimId = PurchaseClaimId(order.Id),
                Source = RewardSource.Purchase,
                CampaignId = campaignId ?? string.Empty,
                Key = order.ProductId ?? string.Empty,
                Items = items,
                ClaimedAtUtc = DateTime.UtcNow,
            };
        }

        /// <summary>The claim id of a purchase reward: <c>order:&lt;order id&gt;</c>.</summary>
        public static string PurchaseClaimId(int orderId) => "order:" + orderId;

        /// <summary>Sets the game's reward handler and delivers anything that was waiting for it.</summary>
        private void SetRewardsHandler(Func<IReadOnlyList<LiveOpsReward>, UniTask> handler)
        {
            _rewards = handler;
            _warnedNoRewardHandler = false;
            DeliverRewardsAsync().Forget();
        }

        /// <summary>Saves and delivers new rewards for the current player, skipping any already recorded.</summary>
        private void RecordRewards(IEnumerable<LiveOpsReward> rewards)
        {
            var added = false;
            foreach (var reward in rewards)
            {
                if (reward == null || string.IsNullOrEmpty(reward.ClaimId) || IsKnownClaim(reward.ClaimId)) continue;
                _undeliveredRewards.Add(reward);
                RememberClaim(_rewardHistory, reward.ClaimId);
                added = true;
            }
            if (!added) return;

            SaveProgress();
            DeliverRewardsAsync().Forget();
        }

        private bool IsKnownClaim(string claimId) =>
            _rewardHistory.Contains(claimId) || _undeliveredRewards.Any(r => r.ClaimId == claimId);

        private static void RememberClaim(List<string> history, string claimId)
        {
            history.Add(claimId);
            if (history.Count > RewardHistoryLimit) history.RemoveRange(0, history.Count - RewardHistoryLimit);
        }

        private void RecordForOtherPlayer(long playerId, LiveOpsReward reward)
        {
            var key = ProgressPrefsKey(playerId);
            var save = ReadSave(PlayerPrefs.GetString(key, null)) ?? new ProgressSave();
            save.UndeliveredRewards ??= new List<LiveOpsReward>();
            save.RewardHistory ??= new List<string>();
            if (save.RewardHistory.Contains(reward.ClaimId) || save.UndeliveredRewards.Any(r => r.ClaimId == reward.ClaimId)) return;

            save.UndeliveredRewards.Add(reward);
            RememberClaim(save.RewardHistory, reward.ClaimId);
            PlayerPrefs.SetString(key, JsonConvert.SerializeObject(save));
            PlayerPrefs.Save();
            _log.Info($"{LogTag} {reward.ClaimId} saved for player {playerId}, who bought it; delivered when they log in");
        }

        /// <summary>The items the admin set under <paramref name="dataKey"/> in the campaign's data; empty when none.</summary>
        private List<RewardItem> ItemsAt(CampaignItem item, string dataKey)
        {
            if (item?.Data == null || !item.Data.TryGetValue(dataKey, out var json) || string.IsNullOrWhiteSpace(json))
            {
                return new List<RewardItem>();
            }

            var items = RewardItem.ParseList(json);
            if (items != null) return items;

            _log.Warning($"{LogTag} campaign '{item.Id}' has unreadable reward items at '{dataKey}'; delivering none");
            return new List<RewardItem>();
        }

        /// <summary>Whether <paramref name="bundle"/> lists items the SDK can deliver itself.</summary>
        private static bool HasDeliverableItems(JToken bundle) => RewardItem.ParseBundle(bundle) != null;
    }
}
