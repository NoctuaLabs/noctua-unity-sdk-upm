#if UNITY_APPLOVIN
using System;
using System.Collections.Generic;
using com.noctuagames.sdk.AppLovin;
using UnityEngine;


namespace com.noctuagames.sdk
{
    /// <summary>
    /// AppLovin MAX implementation of <see cref="IAdNetwork"/> that manages interstitial, rewarded,
    /// and banner ads through the AppLovin MAX mediation SDK.
    /// </summary>
    public class AppLovinManager: IAdNetwork {

        private readonly NoctuaLogger _log = new(typeof(AppLovinManager));

        /// <inheritdoc />
        public string NetworkName => AdNetworkName.AppLovin;

        private InterstitialAppLovin _interstitialAppLovin;
        private RewardedAppLovin _rewardedAppLovin;
        private BannerAppLovin _bannerAppLovin;
        private AppOpenAppLovin _appOpenAppLovin;

        // Private event handlers
        private event Action _initCompleteAction;
        private event Action _onAdDisplayed;
        private event Action _onAdFailedDisplayed;
        private event Action _onAdClicked;
        private event Action _onAdImpressionRecorded;
        private event Action _onAdClosed;
        private event Action<MaxSdk.Reward> _onUserEarnedReward;
        private event Action<MaxSdkBase.AdInfo> _appLovinOnAdRevenuePaid;
        private event Action<double, string> _onUnifiedUserEarnedReward;
        private event Action<double, string, Dictionary<string, string>> _onUnifiedAdRevenuePaid;

        // Subscription guards to prevent duplicate event wiring on re-init
        private bool _interstitialEventsSubscribed;
        private bool _rewardedEventsSubscribed;
        private bool _bannerEventsSubscribed;
        private bool _appOpenEventsSubscribed;
        private bool _sdkInitCallbackSubscribed;

        /// <summary>Raised when the AppLovin MAX SDK has completed initialization.</summary>
        public event Action OnInitialized { add => _initCompleteAction += value; remove => _initCompleteAction -= value; }

        /// <summary>Raised when any ad format is successfully displayed to the user.</summary>
        public event Action OnAdDisplayed { add => _onAdDisplayed += value; remove => _onAdDisplayed -= value; }

        /// <summary>Raised when any ad format fails to display.</summary>
        public event Action OnAdFailedDisplayed { add => _onAdFailedDisplayed += value; remove => _onAdFailedDisplayed -= value; }

        /// <summary>Raised when the user clicks on any displayed ad.</summary>
        public event Action OnAdClicked { add => _onAdClicked += value; remove => _onAdClicked -= value; }

        /// <summary>Raised when an ad impression is recorded for any ad format.</summary>
        public event Action OnAdImpressionRecorded { add => _onAdImpressionRecorded += value; remove => _onAdImpressionRecorded -= value; }

        /// <summary>Raised when any ad is closed by the user.</summary>
        public event Action OnAdClosed { add => _onAdClosed += value; remove => _onAdClosed -= value; }

        /// <summary>Raised when the user earns a reward from watching a rewarded ad.</summary>
        public event Action<MaxSdk.Reward> AppLovinOnUserEarnedReward { add => _onUserEarnedReward += value; remove => _onUserEarnedReward -= value; }

        /// <summary>Raised when ad revenue is recorded, providing the ad info with revenue data.</summary>
        public event Action<MaxSdkBase.AdInfo> AppLovinOnAdRevenuePaid { add => _appLovinOnAdRevenuePaid += value; remove => _appLovinOnAdRevenuePaid -= value; }

        /// <summary>Raised when the user earns a reward (network-agnostic). Parameters: (amount, type).</summary>
        public event Action<double, string> OnUserEarnedReward { add => _onUnifiedUserEarnedReward += value; remove => _onUnifiedUserEarnedReward -= value; }

        /// <summary>Raised when ad revenue is recorded (network-agnostic). Parameters: (revenue, currency, metadata).</summary>
        public event Action<double, string, Dictionary<string, string>> OnAdRevenuePaid { add => _onUnifiedAdRevenuePaid += value; remove => _onUnifiedAdRevenuePaid -= value; }

        internal AppLovinManager()
        {
            _log.Debug("AppLovinManager constructor");

            _interstitialAppLovin = new InterstitialAppLovin();
            _rewardedAppLovin = new RewardedAppLovin();
            _bannerAppLovin = new BannerAppLovin();
            _appOpenAppLovin = new AppOpenAppLovin();

            // Wire network-specific events to unified (network-agnostic) events
            _onUserEarnedReward += (reward) =>
            {
                _onUnifiedUserEarnedReward?.Invoke(reward.Amount, reward.Label);
            };

            _appLovinOnAdRevenuePaid += (adInfo) =>
            {
                double revenue = adInfo.Revenue;
                string currency = "USD";

                var metadata = new Dictionary<string, string>
                {
                    { "network", adInfo.NetworkName ?? "unknown" },
                    { "placement", adInfo.Placement ?? "unknown" },
                    { "network_placement", adInfo.NetworkPlacement ?? "unknown" },
                    { "creative_id", adInfo.CreativeIdentifier ?? "unknown" },
                    { "precision", adInfo.RevenuePrecision ?? "unknown" }
                };

                _onUnifiedAdRevenuePaid?.Invoke(revenue, currency, metadata);
            };
        }

        /// <inheritdoc />
        public void Initialize(Action initCompleteAction)
        {
            _log.Info("Initializing AppLovin SDK");

            MaxSdk.SetUserId(SystemInfo.deviceUniqueIdentifier);

            if (!_sdkInitCallbackSubscribed)
            {
                _sdkInitCallbackSubscribed = true;
                MaxSdkCallbacks.OnSdkInitializedEvent += (MaxSdk.SdkConfiguration sdkConfiguration) =>
                {
                    _log.Debug("AppLovin initialized");

                    initCompleteAction?.Invoke();
                    _initCompleteAction?.Invoke();
                };
            }

            #if DEBUG || DEVELOPMENT_BUILD
            MaxSdk.SetVerboseLogging(true);
            #endif

            MaxSdk.InitializeSdk();
        }

        /// <inheritdoc />
        public void SetInterstitialAdUnitID(string adUnitID)
        {
            _interstitialAppLovin.SetInterstitialAdUnitID(adUnitID);

            if (!_interstitialEventsSubscribed)
            {
                _interstitialEventsSubscribed = true;

                // Subscribe to events (only once)
                _interstitialAppLovin.InterstitialOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
                _interstitialAppLovin.InterstitialOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
                _interstitialAppLovin.InterstitialOnAdClicked += () => { _onAdClicked?.Invoke(); };
                _interstitialAppLovin.InterstitialOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
                _interstitialAppLovin.InterstitialOnAdClosed += () => { _onAdClosed?.Invoke(); };
                _interstitialAppLovin.InterstitialOnAdRevenuePaid += (adInfo) => { _appLovinOnAdRevenuePaid?.Invoke(adInfo); };
            }
        }

        /// <inheritdoc />
        public void LoadInterstitialAd()
        {
            _interstitialAppLovin.LoadInterstitial();
        }

        /// <inheritdoc />
        public bool IsInterstitialReady() => _interstitialAppLovin.IsReady();

        /// <inheritdoc />
        public void ShowInterstitial()
        {
            _interstitialAppLovin.ShowInterstitial();
        }

        /// <inheritdoc />
        public void ShowInterstitial(string placement)
        {
            _interstitialAppLovin.ShowInterstitial(placement);
        }

        /// <inheritdoc />
        public void SetRewardedAdUnitID(string adUnitID)
        {
            _rewardedAppLovin.SetRewardedAdUnitID(adUnitID);

            if (!_rewardedEventsSubscribed)
            {
                _rewardedEventsSubscribed = true;

                // Subscribe to events (only once)
                _rewardedAppLovin.RewardedOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
                _rewardedAppLovin.RewardedOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
                _rewardedAppLovin.RewardedOnAdClicked += () => { _onAdClicked?.Invoke(); };
                _rewardedAppLovin.RewardedOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
                _rewardedAppLovin.RewardedOnAdClosed += () => { _onAdClosed?.Invoke(); };
                _rewardedAppLovin.RewardedOnUserEarnedReward += (reward) => { _onUserEarnedReward?.Invoke(reward); };
                _rewardedAppLovin.RewardedOnAdRevenuePaid += (adInfo) => { _appLovinOnAdRevenuePaid?.Invoke(adInfo); };
            }
        }

        /// <inheritdoc />
        public void LoadRewardedAd()
        {
            _rewardedAppLovin.LoadRewardedAds();
        }

        /// <inheritdoc />
        public bool IsRewardedAdReady() => _rewardedAppLovin.IsReady();

        /// <inheritdoc />
        public void ShowRewardedAd()
        {
           _rewardedAppLovin.ShowRewardedAd();
        }

        /// <inheritdoc />
        public void ShowRewardedAd(string placement)
        {
           _rewardedAppLovin.ShowRewardedAd(placement);
        }

        /// <inheritdoc />
        public void SetBannerAdUnitId(string adUnitID)
        {
            _bannerAppLovin.SetBannerAdUnitId(adUnitID);

            if (!_bannerEventsSubscribed)
            {
                _bannerEventsSubscribed = true;

                // Subscribe to events (only once)
                _bannerAppLovin.BannerOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
                _bannerAppLovin.BannerOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
                _bannerAppLovin.BannerOnAdClicked += () => { _onAdClicked?.Invoke(); };
                _bannerAppLovin.BannerOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
                _bannerAppLovin.BannerOnAdClosed += () => { _onAdClosed?.Invoke(); };
                _bannerAppLovin.BannerOnAdRevenuePaid += (adInfo) => { _appLovinOnAdRevenuePaid?.Invoke(adInfo); };
            }
        }

        /// <inheritdoc />
        [Obsolete("This method is deprecated. Please use CreateBannerViewAdAppLovin(Color, MaxSdkBase.AdViewPosition) instead.")]
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition)
        {
            _bannerAppLovin.InitializeBannerAds(color, bannerPosition);
        }

        /// <inheritdoc />
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.AdViewPosition bannerPosition)
        {
            _bannerAppLovin.InitializeBannerAds(color, bannerPosition);
        }

        /// <inheritdoc />
        public void ShowBannerAd()
        {
            _bannerAppLovin.ShowBanner();
        }

        /// <inheritdoc />
        public void HideBannerAppLovin()
        {
            _bannerAppLovin.HideBanner();
        }

        /// <inheritdoc />
        public void DestroyBannerAppLovin()
        {
            _bannerAppLovin.DestroyBanner();
        }

        /// <inheritdoc />
        public void SetBannerWidth(int width)
        {
            _bannerAppLovin.SetBannerWidth(width);
        }

        /// <inheritdoc />
        public Rect GetBannerPosition()
        {
            return _bannerAppLovin.GetBannerPosition();
        }

        /// <inheritdoc />
        public void StopBannerAutoRefresh()
        {
            _bannerAppLovin.StopBannerAutoRefresh();
        }

        /// <inheritdoc />
        public void StartBannerAutoRefresh()
        {
            _bannerAppLovin.StartBannerAutoRefresh();
        }

        /// <inheritdoc />
        public void SetMuted(bool muted)
        {
            MaxSdk.SetMuted(muted);
        }

        /// <inheritdoc />
        public void SetBannerPlacement(string placement)
        {
            _bannerAppLovin.SetPlacement(placement);
        }

        /// <inheritdoc />
        public void SetBannerRefreshInterval(int seconds)
        {
            _bannerAppLovin.SetRefreshInterval(seconds);
        }

        /// <inheritdoc />
        public void SetAppOpenAdUnitID(string adUnitID)
        {
            _appOpenAppLovin.SetAppOpenAdUnitID(adUnitID);

            if (!_appOpenEventsSubscribed)
            {
                _appOpenEventsSubscribed = true;

                _appOpenAppLovin.AppOpenOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
                _appOpenAppLovin.AppOpenOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
                _appOpenAppLovin.AppOpenOnAdClicked += () => { _onAdClicked?.Invoke(); };
                _appOpenAppLovin.AppOpenOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
                _appOpenAppLovin.AppOpenOnAdClosed += () => { _onAdClosed?.Invoke(); };
                _appOpenAppLovin.AppOpenOnAdRevenuePaid += (adInfo) => { _appLovinOnAdRevenuePaid?.Invoke(adInfo); };
            }
        }

        /// <inheritdoc />
        public void LoadAppOpenAd()
        {
            _appOpenAppLovin.LoadAppOpenAd();
        }

        /// <inheritdoc />
        public void ShowAppOpenAd()
        {
            _appOpenAppLovin.ShowAppOpenAd();
        }

        /// <inheritdoc />
        public bool IsAppOpenAdReady()
        {
            return _appOpenAppLovin.IsAdReady();
        }

        /// <inheritdoc />
        public void ShowCreativeDebugger()
        {
            MaxSdk.ShowCreativeDebugger();
        }

        /// <inheritdoc />
        public void ShowMediationDebugger()
        {
            MaxSdk.ShowMediationDebugger();
        }

        /// <inheritdoc />
        public void SetTestDeviceIds(List<string> testDeviceIds)
        {
            if (testDeviceIds == null || testDeviceIds.Count == 0)
            {
                _log.Debug("No test device IDs to set for AppLovin.");
                return;
            }

            _log.Info($"Setting {testDeviceIds.Count} AppLovin test device advertising ID(s).");
            MaxSdk.SetTestDeviceAdvertisingIdentifiers(testDeviceIds.ToArray());
        }

        /// <summary>
        /// Unregisters all static MaxSdkCallbacks subscriptions held by the inner ad classes.
        /// Call this before discarding an AppLovinManager instance to prevent stale callback
        /// accumulation on the static MaxSdkCallbacks events (which persist for the app lifetime).
        /// </summary>
        public void Cleanup()
        {
            _interstitialAppLovin?.UnregisterCallbacks();
            _rewardedAppLovin?.UnregisterCallbacks();
            _bannerAppLovin?.UnregisterCallbacks();
            _appOpenAppLovin?.UnregisterCallbacks();

            // Reset subscription guards so a new instance can re-subscribe cleanly.
            _interstitialEventsSubscribed = false;
            _rewardedEventsSubscribed = false;
            _bannerEventsSubscribed = false;
            _appOpenEventsSubscribed = false;

            _log.Debug("AppLovinManager cleanup complete — all MaxSdkCallbacks unregistered.");
        }
    }
}

#endif