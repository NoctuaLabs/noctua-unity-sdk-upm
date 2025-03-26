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

        // public event Action InterstitialOnAdImpressionRecorded;
        public event Action InterstitialOnAdClicked;
        public event Action InterstitialOnAdFullScreenContentOpened;
        public event Action InterstitialOnAdFullScreenContentClosed;
        public void Initialize(Action initCompleteAction) {
            _log.Info("Initializing AppLovin SDK");

            MaxSdkCallbacks.OnSdkInitializedEvent += (MaxSdkBase.SdkConfiguration sdkConfiguration) => {
                _log.Debug("AppLovin initialized");

                initCompleteAction?.Invoke();
            };

            MaxSdk.InitializeSdk();
        }

        public void SetInterstitialAdUnitID(string adUnitID)
        {
            _interstitialAppLovin = new InterstitialAppLovin();

            _interstitialAppLovin.SetInterstitialAdUnitID(adUnitID);

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
            _rewardedAppLovin = new RewardedAppLovin();
            
            _rewardedAppLovin.SetRewardedAdUnitID(adUnitID);
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
            _bannerAppLovin = new BannerAppLovin();

            _bannerAppLovin.SetBannerAdUnitId(adUnitID);
        }

        public void LoadAppLovinBanner(Color color, MaxSdkBase.BannerPosition bannerPosition)
        {
            _bannerAppLovin.InitializeBannerAds(color, bannerPosition);
        }

        public void OnDestroy()
        {
            throw new NotImplementedException();
        }
    }
}
#endif