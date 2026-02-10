#if UNITY_APPLOVIN
using System;
using com.noctuagames.sdk.AppLovin;
using UnityEngine;


namespace com.noctuagames.sdk
{
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
        private event Action _onAdClosed;
        private event Action<MaxSdk.Reward> _onUserEarnedReward;
        private event Action<MaxSdkBase.AdInfo> _appLovinOnAdRevenuePaid;

        // public event handlers
        public event Action OnInitialized { add => _initCompleteAction += value; remove => _initCompleteAction -= value; }
        public event Action OnAdDisplayed { add => _onAdDisplayed += value; remove => _onAdDisplayed -= value; }
        public event Action OnAdFailedDisplayed { add => _onAdFailedDisplayed += value; remove => _onAdFailedDisplayed -= value; } 
        public event Action OnAdClicked { add => _onAdClicked += value; remove => _onAdClicked -= value; }
        public event Action OnAdClosed { add => _onAdClosed += value; remove => _onAdClosed -= value; }
        public event Action<MaxSdk.Reward> AppLovinOnUserEarnedReward { add => _onUserEarnedReward += value; remove => _onUserEarnedReward -= value; }
        public event Action<MaxSdkBase.AdInfo> AppLovinOnAdRevenuePaid { add => _appLovinOnAdRevenuePaid += value; remove => _appLovinOnAdRevenuePaid -= value; }

        internal AppLovinManager()
        {
            _log.Debug("AppLovinManager constructor");

            _interstitialAppLovin = new InterstitialAppLovin();
            _rewardedAppLovin = new RewardedAppLovin();
            _bannerAppLovin = new BannerAppLovin();

        } 
        public void Initialize(Action initCompleteAction)
        {
            _log.Info("Initializing AppLovin SDK");

            MaxSdkCallbacks.OnSdkInitializedEvent += (MaxSdk.SdkConfiguration sdkConfiguration) =>
            {
                _log.Debug("AppLovin initialized");

                initCompleteAction?.Invoke();
                _initCompleteAction.Invoke();
            };

            MaxSdk.InitializeSdk();
        }

        public void SetInterstitialAdUnitID(string adUnitID)
        {
            _interstitialAppLovin.SetInterstitialAdUnitID(adUnitID);

            // Subscribe to events
            _interstitialAppLovin.InterstitialOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _interstitialAppLovin.InterstitialOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _interstitialAppLovin.InterstitialOnAdClicked += () => { _onAdClicked?.Invoke(); };
            _interstitialAppLovin.InterstitialOnAdClosed += () => { _onAdClosed?.Invoke(); };
            _interstitialAppLovin.InterstitialOnAdRevenuePaid += (adInfo) => { _appLovinOnAdRevenuePaid?.Invoke(adInfo); };

        }

        public void LoadInterstitialAd()
        {
            _interstitialAppLovin.LoadInterstitial();
        }

        public void ShowInterstitial()
        {
            _interstitialAppLovin.ShowInterstitial();
        }

        public void SetRewardedAdUnitID(string adUnitID)
        {            
            _rewardedAppLovin.SetRewardedAdUnitID(adUnitID);

            // Subscribe to events
            _rewardedAppLovin.RewardedOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _rewardedAppLovin.RewardedOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _rewardedAppLovin.RewardedOnAdClicked += () => { _onAdClicked?.Invoke(); };
            _rewardedAppLovin.RewardedOnAdClosed += () => { _onAdClosed?.Invoke(); };
            _rewardedAppLovin.RewardedOnUserEarnedReward += (reward) => { _onUserEarnedReward?.Invoke(reward); };
            _rewardedAppLovin.RewardedOnAdRevenuePaid += (adInfo) => { _appLovinOnAdRevenuePaid?.Invoke(adInfo); };
        }

        public void LoadRewardedAd()
        {
            _rewardedAppLovin.LoadRewardedAds();
        }

        public void ShowRewardedAd()
        {
           _rewardedAppLovin.ShowRewardedAd();
        }

        public void SetBannerAdUnitId(string adUnitID)
        {
            _bannerAppLovin.SetBannerAdUnitId(adUnitID);

            // Subscribe to events
            _bannerAppLovin.BannerOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _bannerAppLovin.BannerOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _bannerAppLovin.BannerOnAdClicked += () => { _onAdClicked?.Invoke(); };
            _bannerAppLovin.BannerOnAdClosed += () => { _onAdClosed?.Invoke(); };
            _bannerAppLovin.BannerOnAdRevenuePaid += (adInfo) => { _appLovinOnAdRevenuePaid?.Invoke(adInfo); };
        }

        [Obsolete("This method is deprecated. Please use CreateBannerViewAdAppLovin(Color, MaxSdkBase.AdViewPosition) instead.")]
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition)
        {
            _bannerAppLovin.InitializeBannerAds(color, bannerPosition);
        }

        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.AdViewPosition bannerPosition)
        {
            _bannerAppLovin.InitializeBannerAds(color, bannerPosition);
        }

        public void ShowBannerAd()
        {
            _bannerAppLovin.ShowBanner();
        }

        public void HideBannerAppLovin()
        {
            _bannerAppLovin.HideBanner();
        }

        public void DestroyBannerAppLovin()
        {
            _bannerAppLovin.DestroyBanner();
        }

        public void SetBannerWidth(int width)
        {
            _bannerAppLovin.SetBannerWidth(width);
        }

        public Rect GetBannerPosition()
        {
            return _bannerAppLovin.GetBannerPosition();
        }

        public void StopBannerAutoRefresh()
        {
            _bannerAppLovin.StopBannerAutoRefresh();
        }

        public void StartBannerAutoRefresh()
        {
            _bannerAppLovin.StartBannerAutoRefresh();
        }

        public void ShowCreativeDebugger()
        {
            MaxSdk.ShowCreativeDebugger();
        }

        public void ShowMediationDebugger()
        {
            MaxSdk.ShowMediationDebugger();
        }
    }
}
#endif