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

        //Revenue event handler
        #if UNITY_ADMOB
        event Action<Reward> AdmobOnUserEarnedReward { add{} remove{} }
        event Action<AdValue> AdmobOnAdRevenuePaid { add{} remove{} }
        #endif
        
        #if UNITY_APPLOVIN
        event Action<MaxSdk.Reward> AppLovinOnUserEarnedReward { add{} remove{} }
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

        //Rewarded Interstitial Admob
        #if UNITY_ADMOB
        void SetRewardeInterstitialdAdUnitID(string adUnitID) { throw new NotImplementedException(); }
        void LoadRewardedInterstitialAd() { throw new NotImplementedException(); }
        void ShowRewardedInterstitialAd() { throw new NotImplementedException(); }
        #endif
        
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

        //Other methods
        void ShowCreativeDebugger() { throw new NotImplementedException(); }
        void ShowMediationDebugger() { throw new NotImplementedException(); }
        // void SetTestDeviceId(string testDeviceId) { throw new NotImplementedException(); }
        // void SetTestDeviceIds(string[] testDeviceIds) { throw new NotImplementedException(); }
        // void SetTagForChildDirectedTreatment(bool tagForChildDirectedTreatment) { throw new NotImplementedException(); }
        // void SetTagForUnderAgeOfConsent(bool tagForUnderAgeOfConsent) { throw new NotImplementedException(); }
        // void SetRequestConfiguration(RequestConfiguration requestConfiguration) { throw new NotImplementedException(); }
        // void SetMaxAdContentRating(MaxAdContentRating maxAdContentRating) { throw new NotImplementedException(); }

    }
}
