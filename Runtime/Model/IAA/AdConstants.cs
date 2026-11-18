namespace com.noctuagames.sdk
{
    /// <summary>
    /// Ad format key strings used in routing, frequency caps, and remote config
    /// (e.g. <see cref="IAA.AdFormatOverrides"/> dictionary keys).
    /// </summary>
    public static class AdFormatKey
    {
        public const string Interstitial         = "interstitial";
        public const string Rewarded             = "rewarded";
        public const string RewardedInterstitial = "rewarded_interstitial";
        public const string Banner               = "banner";
        public const string AppOpen              = "app_open";
    }

    /// <summary>
    /// Ad network name strings that match <see cref="IAdNetwork.NetworkName"/>.
    /// </summary>
    public static class AdNetworkName
    {
        public const string Admob    = "admob";
        public const string AppLovin = "applovin";
    }
}
