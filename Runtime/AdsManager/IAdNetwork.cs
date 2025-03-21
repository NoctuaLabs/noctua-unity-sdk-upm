using System;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
#endif

namespace com.noctuagames.sdk
{
    public interface IAdNetwork
    {
        #if UNITY_ADMOB
        // event Action<AdValue> InterstitialOnAdPaid { add{} remove{} }
        // event Action<AdError> InterstitialOnAdFullScreenContentFailed { add{} remove{} }
        #endif

        // event Action InterstitialOnAdImpressionRecorded;
        event Action InterstitialOnAdClicked;
        event Action InterstitialOnAdFullScreenContentOpened;
        event Action InterstitialOnAdFullScreenContentClosed;

        void Initialize(Action initCompleteAction);
        void LoadInterstitialAd(string adUnitID);
        void ShowInterstitial();
        void LoadRewardedAd(string adUnitID);
        void ShowRewardedAd();
        void SetBannerAdUnitId(string adUnitID);

        #if UNITY_ADMOB
        void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition) {
            throw new NotImplementedException();
        }
        #endif
        void LoadBannerAd();
        void OnDestroy();
    }
}
