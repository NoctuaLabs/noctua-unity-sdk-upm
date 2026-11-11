namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>Applies the game's <see cref="CampaignHandlers"/> to the module.</summary>
    public sealed partial class NoctuaLiveOpsCampaign
    {
        /// <summary>
        /// Applies <paramref name="handlers"/>: each non-null field replaces what was set before.
        /// Games use <c>Noctua.LiveOps.Campaign.Configure</c>, which also works before init.
        /// </summary>
        public void Configure(CampaignHandlers handlers)
        {
            if (handlers == null) return;

            if (handlers.Deeplink != null) RegisterDeeplinkHandler(handlers.Deeplink);
            if (handlers.Purchase != null) SetPurchaseHandler(handlers.Purchase);
            if (handlers.ProductResolver != null) SetProductResolver(handlers.ProductResolver);
            if (handlers.OwnershipProvider != null) SetOwnershipProvider(handlers.OwnershipProvider);

            if (handlers.MissionRewards != null) SetMissionRewardsHandler(handlers.MissionRewards);
        }
    }
}
