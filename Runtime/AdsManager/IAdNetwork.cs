using System;
using UnityEngine;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
#endif

namespace com.noctuagames.sdk
{
    public interface IAdNetwork
    {
        //Event handler
        event Action OnAdDisplayed { add{} remove{} }
        event Action OnAdFailedDisplayed { add{} remove{} }
        event Action OnAdClicked { add{} remove{} }
        event Action OnAdImpressionRecorded { add{} remove{} }
        event Action OnAdClosed { add{} remove{} }
        event Action OnUserEarnedReward { add{} remove{} }

        //Revenue event handler
        #if UNITY_ADMOB
        event Action<AdValue> AdmobOnAdRevenuePaid { add{} remove{} }
        #endif
        
        #if UNITY_APPLOVIN
        event Action<MaxSdkBase.AdInfo> AppLovinOnAdRevenuePaid { add{} remove{} }
        #endif
        
        // Initialize IAA SDK
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
        void ShowBannerAd();
        //Banner Admob
        #if UNITY_ADMOB
        void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition) { throw new NotImplementedException(); }
        #endif

        //Banner AppLovin
        #if UNITY_APPLOVIN
        void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition) { throw new NotImplementedException(); }
        void HideBannerAppLovin() { throw new NotImplementedException(); }
        void DestroyBannerAppLovin() { throw new NotImplementedException(); }
        void SetBannerWidth(int width) { throw new NotImplementedException(); }
        Rect GetBannerPosition() { throw new NotImplementedException(); }
        void StopBannerAutoRefresh() { throw new NotImplementedException(); }
        void StartBannerAutoRefresh() { throw new NotImplementedException(); }
        #endif
    }
}
