using System;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
#endif

namespace com.noctuagames.sdk
{
    public class MediationManager
    {
        private readonly NoctuaLogger _log = new(typeof(MediationManager));
        private IAdNetwork _adNetwork;

        // Private event handlers
        private event Action _onAdDisplayed;
        private event Action _onAdFailedDisplayed;
        private event Action _onAdClicked;
        private event Action _onAdImpressionRecorded;
        private event Action _onAdClosed;
        private event Action _onUserEarnedReward;

        #if UNITY_ADMOB
        private event Action<AdValue> _admobOnAdRevenuePaid;
        #endif

        #if UNITY_APPLOVIN
        private event Action<MaxSdkBase.AdInfo> _appLovinOnAdDisplayed;
        #endif

        // public event handlers
        public event Action OnAdDisplayed { add => _onAdDisplayed += value; remove => _onAdDisplayed -= value; }
        public event Action OnAdFailedDisplayed { add => _onAdFailedDisplayed += value; remove => _onAdFailedDisplayed -= value; } 
        public event Action OnAdClicked { add => _onAdClicked += value; remove => _onAdClicked -= value; }
        public event Action OnAdImpressionRecorded { add => _onAdImpressionRecorded += value; remove => _onAdImpressionRecorded -= value; }
        public event Action OnAdClosed { add => _onAdClosed += value; remove => _onAdClosed -= value; }
        public event Action OnUserEarnedReward { add => _onUserEarnedReward += value; remove => _onUserEarnedReward -= value; }

        #if UNITY_ADMOB
        public event Action<AdValue> AdmobOnAdRevenuePaid { add => _admobOnAdRevenuePaid += value; remove => _admobOnAdRevenuePaid -= value; }
        #endif
        #if UNITY_APPLOVIN
        public event Action<MaxSdkBase.AdInfo> AppLovinOnAdDisplayed { add => _appLovinOnAdDisplayed += value; remove => _appLovinOnAdDisplayed -= value; }
        #endif

        public void Initialize(IAAResponse iAAResponse, Action initCompleteAction)
        {
            _log.Info("Initializing Ad Network");

            switch (iAAResponse.Mediation)
            {
                case "admob":
                    InitializeAdmob();
                    break;

                case "applovin":
                    InitializeAppLovin();
                    break;

                default:
                    _log.Info("No mediation found: " + iAAResponse.Mediation);
                    break;
            }

            _adNetwork.Initialize(() => {

                initCompleteAction?.Invoke();

                var interstitialAdUnitID = "unused";
                var rewardedAdUnitID = "unused";
                var bannerAdUnitID = "unused";

                #if UNITY_ANDROID
                    interstitialAdUnitID = iAAResponse.AdFormat.Interstitial.Android.adUnitID;
                #elif UNITY_IPHONE
                    interstitialAdUnitID = iAAResponse.AdFormat.Interstitial.IOS.adUnitID;
                #endif

                 #if UNITY_ANDROID
                    rewardedAdUnitID = iAAResponse.AdFormat.Rewarded.Android.adUnitID;
                #elif UNITY_IPHONE
                    rewardedAdUnitID = iAAResponse.AdFormat.Rewarded.IOS.adUnitID;
                #endif

                 #if UNITY_ANDROID
                    bannerAdUnitID = iAAResponse.AdFormat.Banner.Android.adUnitID;
                #elif UNITY_IPHONE
                    bannerAdUnitID = iAAResponse.AdFormat.Banner.IOS.adUnitID;
                #endif

                //Setup Ad Unit ID
                SetInterstitialAdUnitId(interstitialAdUnitID);
                SetRewardedAdUnitId(rewardedAdUnitID);
                SetBannerAdUnitId(bannerAdUnitID);

                _log.Debug("Setup Ad Unit ID is Done");

                //Prepare the ads
                LoadInterstitialAd();
                LoadRewardedAd();

            });
        }

        #if UNITY_ADMOB
        private void InitializeAdmob()
        {
            _adNetwork = new AdmobManager();

            _adNetwork.OnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _adNetwork.OnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _adNetwork.OnAdClicked += () => { _onAdClicked?.Invoke(); };
            _adNetwork.OnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _adNetwork.OnAdClosed += () => { _onAdClosed?.Invoke(); };
            _adNetwork.OnUserEarnedReward += () => { _onUserEarnedReward?.Invoke(); };
            _adNetwork.AdmobOnAdRevenuePaid += (adValue) => { _admobOnAdRevenuePaid?.Invoke(adValue); };
        }
        #endif

        #if UNITY_APPLOVIN
        private void InitializeAppLovin()
        {
            _adNetwork = new AppLovinManager();

            _adNetwork.OnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _adNetwork.OnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _adNetwork.OnAdClicked += () => { _onAdClicked?.Invoke(); };
            _adNetwork.OnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _adNetwork.OnAdClosed += () => { _onAdClosed?.Invoke(); };
            _adNetwork.OnUserEarnedReward += () => { _onUserEarnedReward?.Invoke(); };
            _adNetwork.AppLovinOnAdRevenuePaid += (adInfo) => { _appLovinOnAdDisplayed?.Invoke(adInfo); };
        }
        #endif

        //Interstitial public functions
        private void SetInterstitialAdUnitId(string adUnitID) => _adNetwork.SetInterstitialAdUnitID(adUnitID);
        public void LoadInterstitialAd() => _adNetwork.LoadInterstitialAd();
        public void ShowInterstitial() => _adNetwork.ShowInterstitial();

        //Rewarded public functions
        private void SetRewardedAdUnitId(string adUnitID) => _adNetwork.SetRewardedAdUnitID(adUnitID);
        public void LoadRewardedAd() => _adNetwork.LoadRewardedAd();
        public void ShowRewardedAd() => _adNetwork.ShowRewardedAd();

        //Banner public functions
        private void SetBannerAdUnitId(string adUnitID) => _adNetwork.SetBannerAdUnitId(adUnitID);
        public void ShowBannerAd() => _adNetwork.ShowBannerAd();

        #if UNITY_ADMOB
        public void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition) => _adNetwork.CreateBannerViewAdAdmob(adSize, adPosition);
        #endif

        //Banner public function for AppLovin
        #if UNITY_APPLOVIN
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition) => _adNetwork.CreateBannerViewAdAppLovin(color, bannerPosition);
        public void HideAppLovinBanner() => _adNetwork.HideBannerAppLovin();
        public void DestroyBannerAppLovin() => _adNetwork.DestroyBannerAppLovin();
        public void SetBannerWidth(int width) => _adNetwork.SetBannerWidth(width);
        public Rect GetBannerPosition() => _adNetwork.GetBannerPosition();
        public void StopBannerAutoRefresh() => _adNetwork.StopBannerAutoRefresh();
        public void StartBannerAutoRefresh() => _adNetwork.StartBannerAutoRefresh();
        #endif
    }
}
