using System;
using UnityEngine;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
#endif

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Defines the contract for an ad network implementation, providing methods and events for managing
    /// interstitial, rewarded, rewarded interstitial, and banner ads across different ad mediation platforms.
    /// </summary>
    public interface IAdNetwork
    {
        /// <summary>Raised when the ad network SDK has completed initialization.</summary>
        event Action OnInitialized { add{} remove{} }

        /// <summary>Raised when an ad is successfully displayed to the user.</summary>
        event Action OnAdDisplayed { add{} remove{} }

        /// <summary>Raised when an ad fails to display after being requested to show.</summary>
        event Action OnAdFailedDisplayed { add{} remove{} }

        /// <summary>Raised when the user clicks on a displayed ad.</summary>
        event Action OnAdClicked { add{} remove{} }

        /// <summary>Raised when an ad impression is recorded by the ad network.</summary>
        event Action OnAdImpressionRecorded { add{} remove{} }

        /// <summary>Raised when an ad is closed by the user.</summary>
        event Action OnAdClosed { add{} remove{} }

        #if UNITY_ADMOB
        /// <summary>Raised when the user earns a reward from watching a rewarded ad (AdMob).</summary>
        event Action<Reward> AdmobOnUserEarnedReward { add{} remove{} }

        /// <summary>Raised when ad revenue is recorded by AdMob, providing the ad value and response info.</summary>
        event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid { add{} remove{} }
        #endif

        #if UNITY_APPLOVIN
        /// <summary>Raised when the user earns a reward from watching a rewarded ad (AppLovin).</summary>
        event Action<MaxSdk.Reward> AppLovinOnUserEarnedReward { add{} remove{} }

        /// <summary>Raised when ad revenue is recorded by AppLovin, providing the ad info with revenue data.</summary>
        event Action<MaxSdkBase.AdInfo> AppLovinOnAdRevenuePaid { add{} remove{} }
        #endif

        /// <summary>
        /// Initializes the ad network SDK.
        /// </summary>
        /// <param name="initCompleteAction">Callback invoked when initialization is complete.</param>
        void Initialize(Action initCompleteAction);

        /// <summary>
        /// Sets the ad unit ID for interstitial ads.
        /// </summary>
        /// <param name="adUnitID">The ad unit ID assigned by the ad network for interstitial ads.</param>
        void SetInterstitialAdUnitID(string adUnitID);

        /// <summary>
        /// Loads an interstitial ad into memory so it is ready to be displayed.
        /// </summary>
        void LoadInterstitialAd();

        /// <summary>
        /// Shows a previously loaded interstitial ad to the user.
        /// </summary>
        void ShowInterstitial();

        /// <summary>
        /// Sets the ad unit ID for rewarded ads.
        /// </summary>
        /// <param name="adUnitID">The ad unit ID assigned by the ad network for rewarded ads.</param>
        void SetRewardedAdUnitID(string adUnitID);

        /// <summary>
        /// Loads a rewarded ad into memory so it is ready to be displayed.
        /// </summary>
        void LoadRewardedAd();

        /// <summary>
        /// Shows a previously loaded rewarded ad to the user.
        /// </summary>
        void ShowRewardedAd();

        #if UNITY_ADMOB
        /// <summary>
        /// Sets the ad unit ID for rewarded interstitial ads (AdMob only).
        /// </summary>
        /// <param name="adUnitID">The ad unit ID assigned by AdMob for rewarded interstitial ads.</param>
        void SetRewardedInterstitialAdUnitID(string adUnitID) { throw new NotImplementedException(); }

        /// <summary>
        /// Loads a rewarded interstitial ad into memory (AdMob only).
        /// </summary>
        void LoadRewardedInterstitialAd() { throw new NotImplementedException(); }

        /// <summary>
        /// Shows a previously loaded rewarded interstitial ad (AdMob only).
        /// </summary>
        void ShowRewardedInterstitialAd() { throw new NotImplementedException(); }
        #endif

        /// <summary>
        /// Sets the ad unit ID for banner ads.
        /// </summary>
        /// <param name="adUnitID">The ad unit ID assigned by the ad network for banner ads.</param>
        void SetBannerAdUnitId(string adUnitID);

        /// <summary>
        /// Shows a banner ad to the user.
        /// </summary>
        void ShowBannerAd();

        #if UNITY_ADMOB
        /// <summary>
        /// Creates a banner view with the specified size and position (AdMob only).
        /// </summary>
        /// <param name="adSize">The size of the banner ad.</param>
        /// <param name="adPosition">The screen position where the banner should be displayed.</param>
        void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition) { throw new NotImplementedException(); }
        #endif

        #if UNITY_APPLOVIN
        /// <summary>
        /// Creates a banner view with the specified background color and position (AppLovin, deprecated).
        /// </summary>
        /// <param name="color">The background color for the banner.</param>
        /// <param name="bannerPosition">The screen position where the banner should be displayed.</param>
        [Obsolete("This method is deprecated. Please use CreateBannerViewAdAppLovin(Color, MaxSdkBase.AdViewPosition) instead.")]
        void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition) { throw new NotImplementedException(); }

        /// <summary>
        /// Creates a banner view with the specified background color and position (AppLovin).
        /// </summary>
        /// <param name="color">The background color for the banner.</param>
        /// <param name="bannerPosition">The screen position where the banner should be displayed.</param>
        void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.AdViewPosition bannerPosition) { throw new NotImplementedException(); }

        /// <summary>
        /// Hides the currently displayed AppLovin banner ad without destroying it.
        /// </summary>
        void HideBannerAppLovin() { throw new NotImplementedException(); }

        /// <summary>
        /// Destroys the AppLovin banner ad instance and frees its resources.
        /// </summary>
        void DestroyBannerAppLovin() { throw new NotImplementedException(); }

        /// <summary>
        /// Sets the banner width in pixels (AppLovin). Must be at least 320 on phones or 728 on tablets.
        /// </summary>
        /// <param name="width">The desired banner width in pixels.</param>
        void SetBannerWidth(int width) { throw new NotImplementedException(); }

        /// <summary>
        /// Gets the current position and size of the AppLovin banner ad.
        /// </summary>
        /// <returns>A <see cref="Rect"/> representing the banner's position and dimensions.</returns>
        Rect GetBannerPosition() { throw new NotImplementedException(); }

        /// <summary>
        /// Stops automatic banner ad refresh (AppLovin), allowing manual refresh control.
        /// </summary>
        void StopBannerAutoRefresh() { throw new NotImplementedException(); }

        /// <summary>
        /// Resumes automatic banner ad refresh (AppLovin).
        /// </summary>
        void StartBannerAutoRefresh() { throw new NotImplementedException(); }
        #endif

        /// <summary>
        /// Opens the creative debugger tool for inspecting ad creatives.
        /// </summary>
        void ShowCreativeDebugger() { throw new NotImplementedException(); }

        /// <summary>
        /// Opens the mediation debugger tool for inspecting ad network mediation status.
        /// </summary>
        void ShowMediationDebugger() { throw new NotImplementedException(); }
        // void SetTestDeviceId(string testDeviceId) { throw new NotImplementedException(); }
        // void SetTestDeviceIds(string[] testDeviceIds) { throw new NotImplementedException(); }
        // void SetTagForChildDirectedTreatment(bool tagForChildDirectedTreatment) { throw new NotImplementedException(); }
        // void SetTagForUnderAgeOfConsent(bool tagForUnderAgeOfConsent) { throw new NotImplementedException(); }
        // void SetRequestConfiguration(RequestConfiguration requestConfiguration) { throw new NotImplementedException(); }
        // void SetMaxAdContentRating(MaxAdContentRating maxAdContentRating) { throw new NotImplementedException(); }

    }
}
