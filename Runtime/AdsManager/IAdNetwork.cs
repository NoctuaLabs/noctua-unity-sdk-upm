using System;
using UnityEngine;

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
        
        //Interstitial
        void SetInterstitialAdUnitID(string adUnitID);
        void LoadInterstitialAd();
        void ShowInterstitial();

        //Rewarded
        void SetRewardedAdUnitID(string adUnitID);
        void LoadRewardedAd();
        void ShowRewardedAd();

        //Banner
        void SetBannerAdUnitId(string adUnitID);
        //Banner Admob
        #if UNITY_ADMOB
        void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition) { throw new NotImplementedException(); }
        void LoadAdmobBannerAd() { throw new NotImplementedException(); }
        #endif

        //Banner AppLovin
        #if UNITY_APPLOVIN
        void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition) { throw new NotImplementedException(); }
        void LoadAppLovinBanner() { throw new NotImplementedException(); }
        void ShowBannerAppLovin() { throw new NotImplementedException(); }
        void HideBannerAppLovin() { throw new NotImplementedException(); }
        void DestroyBannerAppLovin() { throw new NotImplementedException(); }
        void SetBannerWidth(int width) { throw new NotImplementedException(); }
        Rect GetBannerPosition() { throw new NotImplementedException(); }
        void StopBannerAutoRefresh() { throw new NotImplementedException(); }
        void StartBannerAutoRefresh() { throw new NotImplementedException(); }
        
        #endif
        void OnDestroy();
    }
}
