using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// A mission the SDK claimed for the player, with the reward the admin set on it. Handed to the
    /// game's <c>MissionRewards</c> handler, which grants it.
    /// </summary>
    [Preserve]
    public sealed class MissionReward
    {
        /// <summary>
        /// Unique per player and claim (the tracker key, which can be claimed only once). A reward
        /// is delivered again until the handler finishes, so a game that grants on its own server
        /// should ignore a <c>ClaimId</c> it has already granted.
        /// </summary>
        [JsonProperty("claim_id")] public string ClaimId;

        /// <summary>The daily missions campaign the mission belongs to.</summary>
        [JsonProperty("campaign_id")] public string CampaignId;

        /// <summary>The mission key from the admin, e.g. <c>use_stm</c>.</summary>
        [JsonProperty("mission")] public string MissionKey;

        /// <summary>The game's item id the admin set as the reward (e.g. <c>gem</c>); empty when unset.</summary>
        [JsonProperty("reward_id")] public string RewardId;

        /// <summary>The reward quantity as the admin typed it (the badge text, e.g. <c>25</c>); empty when unset.</summary>
        [JsonProperty("quantity")] public string Quantity;

        /// <summary>When the SDK claimed it (UTC).</summary>
        [JsonProperty("claimed_at")] public DateTime ClaimedAtUtc;

        public override string ToString() =>
            $"{CampaignId}/{MissionKey}: {(string.IsNullOrEmpty(RewardId) ? "?" : RewardId)} x{(string.IsNullOrEmpty(Quantity) ? "?" : Quantity)}";
    }
}
