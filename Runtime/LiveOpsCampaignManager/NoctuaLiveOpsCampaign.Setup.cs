using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// The game's side of live ops in one place. Set it once with
    /// <c>Noctua.ConfigureLiveOps(handlers)</c> — before or after <c>Noctua.InitAsync</c> — and
    /// the SDK applies it as soon as the campaign module exists, ahead of any <c>auto_show</c>
    /// popup. Every field is optional; a null field leaves that part as it is.
    /// </summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        /// <summary>What the game supplies to live ops. All optional.</summary>
        public sealed class GameHandlers
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
            /// Grants the rewards of missions claimed in the SDK (a Claim All button): called with
            /// the campaign id and the mission keys just claimed.
            /// </summary>
            public Action<string, IReadOnlyList<string>> MissionsClaimed;

            /// <summary>A new set: <paramref name="newer"/>'s fields, falling back to <paramref name="older"/>'s.</summary>
            public static GameHandlers Merge(GameHandlers older, GameHandlers newer)
            {
                if (older == null) return newer;
                if (newer == null) return older;
                return new GameHandlers
                {
                    Deeplink = newer.Deeplink ?? older.Deeplink,
                    Purchase = newer.Purchase ?? older.Purchase,
                    ProductResolver = newer.ProductResolver ?? older.ProductResolver,
                    OwnershipProvider = newer.OwnershipProvider ?? older.OwnershipProvider,
                    MissionsClaimed = newer.MissionsClaimed ?? older.MissionsClaimed,
                };
            }
        }

        // The MissionsClaimed subscriber Configure added, so a second Configure replaces it.
        private Action<string, IReadOnlyList<string>> _configuredMissionsClaimed;

        /// <summary>
        /// Applies <paramref name="handlers"/>: each non-null field replaces what was set before.
        /// Prefer <c>Noctua.ConfigureLiveOps</c>, which also works before init.
        /// </summary>
        public void Configure(GameHandlers handlers)
        {
            if (handlers == null) return;

            if (handlers.Deeplink != null) RegisterDeeplinkHandler(handlers.Deeplink);
            if (handlers.Purchase != null) SetPurchaseHandler(handlers.Purchase);
            if (handlers.ProductResolver != null) SetProductResolver(handlers.ProductResolver);
            if (handlers.OwnershipProvider != null) SetOwnershipProvider(handlers.OwnershipProvider);

            if (handlers.MissionsClaimed != null)
            {
                if (_configuredMissionsClaimed != null) OnMissionsClaimed -= _configuredMissionsClaimed;
                _configuredMissionsClaimed = handlers.MissionsClaimed;
                OnMissionsClaimed += _configuredMissionsClaimed;
            }
        }
    }
}
