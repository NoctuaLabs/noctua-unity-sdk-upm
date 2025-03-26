using System;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
#endif

namespace com.noctuagames.sdk
{
    public class MediationManager
    {
        private readonly NoctuaLogger _log = new(typeof(MediationManager));
        private IAdNetwork _adNetwork;

        #if UNITY_ADMOB
        public Action<AdValue> OnAdPaid;
        #endif
        public Action OnAdImpressionRecorded;
        public Action OnAdClicked;
        public Action OnAdFullScreenContentOpened;
        public Action OnAdFullScreenContentClosed;

        public void Initialize(Action initCompleteAction)
        {
            _log.Info("Initializing Ad Network");

            #if !UNITY_ADMOB && !UNITY_APPLOVIN
            _log.Error("Admob or AppLovin is not enabled. Please enable one of them.");
            return;
            #endif

            #if UNITY_ADMOB
            _adNetwork = new AdmobManager();
            // _adNetwork.InterstitialOnAdPaid += OnAdPaid;
            // _adNetwork.InterstitialOnAdImpressionRecorded += OnAdImpressionRecorded;
            _adNetwork.InterstitialOnAdClicked += OnAdClicked;
            _adNetwork.InterstitialOnAdFullScreenContentOpened += OnAdFullScreenContentOpened;
            _adNetwork.InterstitialOnAdFullScreenContentClosed += OnAdFullScreenContentClosed;
            #endif

            // #if UNITY_APPLOVIN
            // _adNetwork = new AppLovinManager();
            // #endif

            _adNetwork.Initialize(() => initCompleteAction?.Invoke());
        }

        public void LoadInterstitialAd() => _adNetwork.LoadInterstitialAd();
        public void ShowInterstitial() => _adNetwork.ShowInterstitial();
        public void LoadRewardedAd() => _adNetwork.LoadRewardedAd();
        public void ShowRewardedAd() => _adNetwork.ShowRewardedAd();
        public void SetBannerAdUnitId(string adUnitID) => _adNetwork.SetBannerAdUnitId(adUnitID);
        #if UNITY_ADMOB
        public void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition) => _adNetwork.CreateBannerViewAdAdmob(adSize, adPosition);
        #endif
        public void LoadBannerAd() => _adNetwork.LoadAdmobBannerAd();

        public void OnIAADestroy()
        {
            if (_adNetwork != null)
            {
                #if UNITY_ADMOB
                // if (OnAdPaid != null) _adNetwork.InterstitialOnAdPaid -= OnAdPaid;
                #endif
                // if (OnAdImpressionRecorded != null) _adNetwork.InterstitialOnAdImpressionRecorded -= OnAdImpressionRecorded;
                // if (OnAdClicked != null) _adNetwork.InterstitialOnAdClicked -= OnAdClicked;
                // if (OnAdFullScreenContentOpened != null) _adNetwork.InterstitialOnAdFullScreenContentOpened -= OnAdFullScreenContentOpened;
                // if (OnAdFullScreenContentClosed != null) _adNetwork.InterstitialOnAdFullScreenContentClosed -= OnAdFullScreenContentClosed;

                _adNetwork.OnDestroy();
                // _adNetwork = null;
            }
        }
    }
}
