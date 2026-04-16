using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
#endif

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Defines the contract for an ad network implementation, providing methods and events for managing
    /// interstitial, rewarded, rewarded interstitial, banner, and app open ads across different ad mediation platforms.
    /// </summary>
    public interface IAdNetwork
    {
        /// <summary>Returns the network identifier (e.g., "admob" or "applovin").</summary>
        string NetworkName { get; }

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

        /// <summary>Raised when the user earns a reward (network-agnostic). Parameters: (amount, type).</summary>
        event Action<double, string> OnUserEarnedReward { add{} remove{} }

        /// <summary>Raised when ad revenue is recorded (network-agnostic). Parameters: (revenue, currency, metadata).</summary>
        event Action<double, string, Dictionary<string, string>> OnAdRevenuePaid { add{} remove{} }

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
        // NOTE: The following AdMob-specific methods default to no-ops so that
        // non-AdMob implementations (e.g. AppLovinManager) can be called through an
        // IAdNetwork reference without crashing. Callers should prefer the guarded
        // MediationManager wrappers (which check IsAdmob()), but direct calls on a
        // non-AdMob primary silently no-op instead of throwing
        // NotImplementedException. Only AdmobManager overrides these with real
        // behavior.

        /// <summary>
        /// Sets the ad unit ID for rewarded interstitial ads (AdMob only).
        /// </summary>
        /// <param name="adUnitID">The ad unit ID assigned by AdMob for rewarded interstitial ads.</param>
        void SetRewardedInterstitialAdUnitID(string adUnitID) { }

        /// <summary>
        /// Loads a rewarded interstitial ad into memory (AdMob only).
        /// </summary>
        void LoadRewardedInterstitialAd() { }

        /// <summary>
        /// Shows a previously loaded rewarded interstitial ad (AdMob only).
        /// </summary>
        void ShowRewardedInterstitialAd() { }
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

        /// <summary>
        /// Network-agnostic hide: hides the currently displayed banner ad without destroying it.
        /// No-op when no banner is displayed or the implementation does not support hiding.
        /// Safe to call regardless of which ad network (AdMob/AppLovin) is primary —
        /// prefer this over <c>HideBannerAppLovin</c> when you want cross-network behavior.
        /// </summary>
        void HideBannerAd() { }

        #if UNITY_ADMOB
        /// <summary>
        /// Creates a banner view with the specified size and position (AdMob only).
        /// No-op on non-AdMob implementations so cross-network calls do not throw.
        /// </summary>
        /// <param name="adSize">The size of the banner ad.</param>
        /// <param name="adPosition">The screen position where the banner should be displayed.</param>
        void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition) { }
        #endif

        #if UNITY_APPLOVIN
        // NOTE: The following AppLovin-specific methods default to no-ops so that
        // non-AppLovin implementations (e.g. AdmobManager) can be called through an
        // IAdNetwork reference without crashing. Callers should prefer the guarded
        // MediationManager wrappers (which check IsAppLovin()), but direct calls on
        // a non-AppLovin primary silently no-op instead of throwing
        // NotImplementedException. Only AppLovinManager overrides these with real
        // behavior.

        /// <summary>
        /// Creates a banner view with the specified background color and position (AppLovin, deprecated).
        /// </summary>
        /// <param name="color">The background color for the banner.</param>
        /// <param name="bannerPosition">The screen position where the banner should be displayed.</param>
        [Obsolete("This method is deprecated. Please use CreateBannerViewAdAppLovin(Color, MaxSdkBase.AdViewPosition) instead.")]
        void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition) { }

        /// <summary>
        /// Creates a banner view with the specified background color and position (AppLovin).
        /// </summary>
        /// <param name="color">The background color for the banner.</param>
        /// <param name="bannerPosition">The screen position where the banner should be displayed.</param>
        void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.AdViewPosition bannerPosition) { }

        /// <summary>
        /// Hides the currently displayed AppLovin banner ad without destroying it.
        /// </summary>
        void HideBannerAppLovin() { }

        /// <summary>
        /// Destroys the AppLovin banner ad instance and frees its resources.
        /// </summary>
        void DestroyBannerAppLovin() { }

        /// <summary>
        /// Sets the banner width in pixels (AppLovin). Must be at least 320 on phones or 728 on tablets.
        /// </summary>
        /// <param name="width">The desired banner width in pixels.</param>
        void SetBannerWidth(int width) { }

        /// <summary>
        /// Gets the current position and size of the AppLovin banner ad.
        /// </summary>
        /// <returns>A <see cref="Rect"/> representing the banner's position and dimensions. Returns an empty Rect when not implemented.</returns>
        Rect GetBannerPosition() { return new Rect(); }

        /// <summary>
        /// Stops automatic banner ad refresh (AppLovin), allowing manual refresh control.
        /// </summary>
        void StopBannerAutoRefresh() { }

        /// <summary>
        /// Resumes automatic banner ad refresh (AppLovin).
        /// </summary>
        void StartBannerAutoRefresh() { }

        /// <summary>
        /// Mutes or unmutes ad audio (AppLovin).
        /// </summary>
        /// <param name="muted">True to mute ad audio, false to unmute.</param>
        void SetMuted(bool muted) { }

        /// <summary>
        /// Sets the placement name for the banner ad (AppLovin).
        /// </summary>
        /// <param name="placement">The placement name for analytics segmentation.</param>
        void SetBannerPlacement(string placement) { }

        /// <summary>
        /// Sets the banner auto-refresh interval in seconds (AppLovin). Clamped to 10-120s.
        /// </summary>
        /// <param name="seconds">Refresh interval in seconds (10-120).</param>
        void SetBannerRefreshInterval(int seconds) { }
        #endif

        /// <summary>
        /// Returns true if a banner ad unit ID has been configured on this network.
        /// Used to select which network to show the banner from when falling back.
        /// </summary>
        bool HasBannerAdUnit() { return false; }

        /// <summary>
        /// Returns whether an interstitial ad is loaded and ready to show.
        /// </summary>
        bool IsInterstitialReady() { return false; }

        /// <summary>
        /// Returns whether a rewarded ad is loaded and ready to show.
        /// </summary>
        bool IsRewardedAdReady() { return false; }

        /// <summary>
        /// Shows a previously loaded interstitial ad with an optional placement name.
        /// For AppLovin the placement is passed natively to MAX SDK.
        /// For AdMob the placement is recorded in custom event analytics only.
        /// Defaults to the no-placement overload when not overridden.
        /// </summary>
        /// <param name="placement">The placement name for analytics segmentation, or null for no placement.</param>
        void ShowInterstitial(string placement) { ShowInterstitial(); }

        /// <summary>
        /// Shows a previously loaded rewarded ad with an optional placement name.
        /// For AppLovin the placement is passed natively to MAX SDK.
        /// For AdMob the placement is recorded in custom event analytics only.
        /// Defaults to the no-placement overload when not overridden.
        /// </summary>
        /// <param name="placement">The placement name for analytics segmentation, or null for no placement.</param>
        void ShowRewardedAd(string placement) { ShowRewardedAd(); }

        /// <summary>
        /// Sets the ad unit ID for app open ads. No-op when the implementation does not support app open ads.
        /// </summary>
        /// <param name="adUnitID">The ad unit ID assigned by the ad network for app open ads.</param>
        void SetAppOpenAdUnitID(string adUnitID) { }

        /// <summary>
        /// Loads an app open ad into memory so it is ready to be displayed. No-op when unsupported.
        /// </summary>
        void LoadAppOpenAd() { }

        /// <summary>
        /// Shows a previously loaded app open ad to the user. No-op when unsupported.
        /// </summary>
        void ShowAppOpenAd() { }

        /// <summary>
        /// Returns whether an app open ad is loaded and ready to show.
        /// </summary>
        bool IsAppOpenAdReady() { return false; }

        /// <summary>
        /// Opens the creative debugger tool for inspecting ad creatives. No-op when unsupported (AdMob does not expose one).
        /// </summary>
        void ShowCreativeDebugger() { }

        /// <summary>
        /// Opens the mediation debugger tool for inspecting ad network mediation status. No-op when unsupported.
        /// </summary>
        void ShowMediationDebugger() { }
        /// <summary>
        /// Registers test device IDs with the ad network for receiving test ads.
        /// </summary>
        /// <param name="testDeviceIds">List of device IDs to register as test devices.</param>
        void SetTestDeviceIds(List<string> testDeviceIds) { }

    }
}
