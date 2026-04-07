using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Contains Google AdMob official test ad unit IDs for all ad formats on both platforms.
    /// These IDs always produce test ads and must be replaced with real IDs before publishing.
    /// See: https://developers.google.com/admob/unity/test-ads
    /// </summary>
    public static class AdTestUnitIds
    {
        // Android test ad unit IDs
        private const string AndroidBanner = "ca-app-pub-3940256099942544/6300978111";
        private const string AndroidInterstitial = "ca-app-pub-3940256099942544/1033173712";
        private const string AndroidRewarded = "ca-app-pub-3940256099942544/5224354917";
        private const string AndroidRewardedInterstitial = "ca-app-pub-3940256099942544/5354046379";
        private const string AndroidAppOpen = "ca-app-pub-3940256099942544/9257395921";
        private const string AndroidNative = "ca-app-pub-3940256099942544/2247696110";

        // iOS test ad unit IDs
        private const string IosBanner = "ca-app-pub-3940256099942544/2934735716";
        private const string IosInterstitial = "ca-app-pub-3940256099942544/4411468910";
        private const string IosRewarded = "ca-app-pub-3940256099942544/1712485313";
        private const string IosRewardedInterstitial = "ca-app-pub-3940256099942544/6978759866";
        private const string IosAppOpen = "ca-app-pub-3940256099942544/5575463023";
        private const string IosNative = "ca-app-pub-3940256099942544/3986624511";

        /// <summary>
        /// Returns the AdMob test ad unit ID for the given ad format and platform.
        /// </summary>
        /// <param name="format">Ad format name: "banner", "interstitial", "rewarded", "rewarded_interstitial", "app_open", "native".</param>
        /// <param name="platform">The runtime platform to get the test ID for.</param>
        /// <returns>The test ad unit ID, or null if the format or platform is not recognized.</returns>
        public static string GetTestAdUnitId(string format, RuntimePlatform platform)
        {
            var isAndroid = platform == RuntimePlatform.Android;

            return format switch
            {
                AdFormatKey.Banner => isAndroid ? AndroidBanner : IosBanner,
                AdFormatKey.Interstitial => isAndroid ? AndroidInterstitial : IosInterstitial,
                AdFormatKey.Rewarded => isAndroid ? AndroidRewarded : IosRewarded,
                AdFormatKey.RewardedInterstitial => isAndroid ? AndroidRewardedInterstitial : IosRewardedInterstitial,
                AdFormatKey.AppOpen => isAndroid ? AndroidAppOpen : IosAppOpen,
                "native" => isAndroid ? AndroidNative : IosNative,
                _ => null
            };
        }
    }
}
