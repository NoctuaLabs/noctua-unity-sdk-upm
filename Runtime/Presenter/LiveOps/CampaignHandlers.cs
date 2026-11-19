using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// What the game supplies to live ops campaigns, all optional. Pass it to
    /// <c>Noctua.LiveOps.Campaign.Configure</c> — before <c>Noctua.InitAsync</c> so an
    /// <c>auto_show</c> popup already has it. A null field leaves that part as it is.
    /// </summary>
    public sealed class CampaignHandlers
    {
        /// <summary>Routes a popup's deeplink action (e.g. <c>"shop/gems"</c>) into the game.</summary>
        public Action<string> Deeplink;

        /// <summary>
        /// Grants the missions and milestone chests the SDK claimed (popup Claim / Claim All, or
        /// <c>Noctua.LiveOps.Campaign.Claim*Async</c>). Campaign purchases are not rewards: the game
        /// delivers them from <c>Noctua.IAP.OnPurchaseDone</c> (the product's <c>Items</c>). Each <see cref="LiveOpsReward"/>
        /// carries its <see cref="LiveOpsReward.Source"/>, the admin's <see cref="LiveOpsReward.Items"/>
        /// and a <see cref="LiveOpsReward.ClaimId"/>. A reward counts as delivered once this finishes
        /// without throwing; otherwise it is delivered again after the next init or login, so grant
        /// and save the items before returning, and ignore a <c>ClaimId</c> already granted.
        /// </summary>
        public Func<IReadOnlyList<LiveOpsReward>, UniTask> Rewards;

        /// <summary>A new set: <paramref name="newer"/>'s fields, falling back to <paramref name="older"/>'s.</summary>
        public static CampaignHandlers Merge(CampaignHandlers older, CampaignHandlers newer)
        {
            if (older == null) return newer;
            if (newer == null) return older;
            return new CampaignHandlers
            {
                Deeplink = newer.Deeplink ?? older.Deeplink,
                Rewards = newer.Rewards ?? older.Rewards,
            };
        }
    }
}
