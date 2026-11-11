using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

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
        /// Buys a product from a popup's Buy button: build the <c>PurchaseRequest</c> and call
        /// <c>Noctua.IAP.PurchaseItemAsync</c>. The campaign is added to the order for you.
        /// </summary>
        public Func<string, CampaignItem, UniTask> Purchase;

        /// <summary>
        /// Maps a dynamic product's bundle to a SKU. Optional: without it the bundle's own
        /// <c>sku_android</c> / <c>sku_ios</c> / <c>sku</c> is used.
        /// </summary>
        public Func<CampaignItem, string, JToken, string> ProductResolver;

        /// <summary>
        /// "Does the player own this SKU?" for one-time consumable offers. Optional: the store
        /// check covers non-consumables and subscriptions.
        /// </summary>
        public Func<string, UniTask<bool>> OwnershipProvider;

        /// <summary>
        /// Grants mission rewards the SDK claimed (a popup's Claim / Claim All button, or
        /// <c>Noctua.LiveOps.Campaign.ClaimMissionAsync</c>). Each <see cref="MissionReward"/> carries the
        /// mission and the reward set in the admin. A reward counts as delivered once this
        /// finishes without throwing; otherwise it is delivered again after the next init or
        /// login. Grant on your server? Ignore a <see cref="MissionReward.ClaimId"/> already granted.
        /// </summary>
        public Func<IReadOnlyList<MissionReward>, UniTask> MissionRewards;

        /// <summary>A new set: <paramref name="newer"/>'s fields, falling back to <paramref name="older"/>'s.</summary>
        public static CampaignHandlers Merge(CampaignHandlers older, CampaignHandlers newer)
        {
            if (older == null) return newer;
            if (newer == null) return older;
            return new CampaignHandlers
            {
                Deeplink = newer.Deeplink ?? older.Deeplink,
                Purchase = newer.Purchase ?? older.Purchase,
                ProductResolver = newer.ProductResolver ?? older.ProductResolver,
                OwnershipProvider = newer.OwnershipProvider ?? older.OwnershipProvider,
                MissionRewards = newer.MissionRewards ?? older.MissionRewards,
            };
        }
    }
}
