#if UNITY_APPLOVIN
using System;
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

        private InterstitialAppLovin _interstitialAppLovin;
        private RewardedAppLovin _rewardedAppLovin;
        private BannerAppLovin _bannerAppLovin;

        // Private event handlers
        private event Action _initCompleteAction;
        private event Action _onAdDisplayed;
        private event Action _onAdFailedDisplayed;
        private event Action _onAdClicked;
        private event Action _onAdImpressionRecorded;
        private event Action _onAdClosed;
        private event Action<MaxSdk.Reward> _onUserEarnedReward;
        private event Action<MaxSdkBase.AdInfo> _appLovinOnAdRevenuePaid;

        // Subscription guards to prevent duplicate event wiring on re-init
        private bool _interstitialEventsSubscribed;
        private bool _rewardedEventsSubscribed;
        private bool _bannerEventsSubscribed;
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

        internal AppLovinManager()
        {
            _log.Debug("AppLovinManager constructor");

            _interstitialAppLovin = new InterstitialAppLovin();
            _rewardedAppLovin = new RewardedAppLovin();
            _bannerAppLovin = new BannerAppLovin();

        }

        /// <inheritdoc />
        public void Initialize(Action initCompleteAction)
        {
            _log.Info("Initializing AppLovin SDK");

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
        public void ShowInterstitial()
        {
            _interstitialAppLovin.ShowInterstitial();
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
        public void ShowRewardedAd()
        {
           _rewardedAppLovin.ShowRewardedAd();
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
        public void ShowCreativeDebugger()
        {
            MaxSdk.ShowCreativeDebugger();
        }

        /// <inheritdoc />
        public void ShowMediationDebugger()
        {
            MaxSdk.ShowMediationDebugger();
        }
    }
}
#endif