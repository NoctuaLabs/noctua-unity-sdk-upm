#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;
using com.noctuagames.sdk.Admob;

namespace com.noctuagames.sdk
{
    public class AdmobManager : IAdNetwork
    {
        private readonly NoctuaLogger _log = new(typeof(AdmobManager));

        private InterstitialAdmob _interstitialAdmob;
        private RewardedAdmob _rewardedAdmob;
        private RewardedInterstitialAdmob _rewardedInterstitialAdmob;
        private BannerAdmob _bannerAdmob;

        // Private event handlers
        private event Action _onAdDisplayed;
        private event Action _onAdFailedDisplayed;
        private event Action _onAdClicked;
        private event Action _onAdImpressionRecorded;
        private event Action _onAdClosed;
        private event Action<Reward> _onUserEarnedReward;

        private event Action<AdValue> _admobOnAdRevenuePaid;


        // public event handlers
        public event Action OnAdDisplayed { add => _onAdDisplayed += value; remove => _onAdDisplayed -= value; }
        public event Action OnAdFailedDisplayed { add => _onAdFailedDisplayed += value; remove => _onAdFailedDisplayed -= value; } 
        public event Action OnAdClicked { add => _onAdClicked += value; remove => _onAdClicked -= value; }
        public event Action OnAdImpressionRecorded { add => _onAdImpressionRecorded += value; remove => _onAdImpressionRecorded -= value; }
        public event Action OnAdClosed { add => _onAdClosed += value; remove => _onAdClosed -= value; }
        public event Action<Reward> OnUserEarnedReward { add => _onUserEarnedReward += value; remove => _onUserEarnedReward -= value; }
        public event Action<AdValue> AdmobOnAdRevenuePaid { add => _admobOnAdRevenuePaid += value; remove => _admobOnAdRevenuePaid -= value; }

        public void Initialize(Action initCompleteAction)
        {
            _log.Info("Initializing Admob SDK");

            MobileAds.Initialize(initStatus =>
            {
                _log.Info("Admob initialized");

                initCompleteAction?.Invoke();
            });
        }

        public void SetInterstitialAdUnitID(string adUnitID)
        {
            _interstitialAdmob = new InterstitialAdmob();
            
            _interstitialAdmob.SetInterstitialAdUnitID(adUnitID);

            // Subscribe to events
            _interstitialAdmob.InterstitialOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _interstitialAdmob.InterstitialOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _interstitialAdmob.InterstitialOnAdClicked += () => { _onAdClicked?.Invoke(); };
            _interstitialAdmob.InterstitialOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _interstitialAdmob.InterstitialOnAdClosed += () => { _onAdClosed?.Invoke(); };
            _interstitialAdmob.AdmobOnAdRevenuePaid += (adValue) => { _admobOnAdRevenuePaid?.Invoke(adValue); };
        }

        public void LoadInterstitialAd()
        {
            _interstitialAdmob.LoadInterstitialAd();
        }

        public void ShowInterstitial()
        {
            _interstitialAdmob.ShowInterstitialAd();
        }

        public void SetRewardedAdUnitID(string adUnitID)
        {
            _rewardedAdmob = new RewardedAdmob();
            _rewardedAdmob.SetRewardedAdUnitID(adUnitID);

            // Subscribe to events
            _rewardedAdmob.RewardedOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _rewardedAdmob.RewardedOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _rewardedAdmob.RewardedOnAdClicked += () => { _onAdClicked?.Invoke(); };
            _rewardedAdmob.RewardedOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _rewardedAdmob.RewardedOnAdClosed += () => { _onAdClosed?.Invoke(); };
            _rewardedAdmob.RewardedOnUserEarnedReward += (reward) => { _onUserEarnedReward?.Invoke(reward); };
            _rewardedAdmob.AdmobOnAdRevenuePaid += (adValue) => { _admobOnAdRevenuePaid?.Invoke(adValue); };
        }

        public void LoadRewardedAd()
        {
            _rewardedAdmob.LoadRewardedAd();
        }

        public void ShowRewardedAd()
        {
            _rewardedAdmob.ShowRewardedAd();
        }

        public void SetBannerAdUnitId(string adUnitID)
        {
            _bannerAdmob = new BannerAdmob();

            _bannerAdmob.SetAdUnitId(adUnitID);

            // Subscribe to events
            _bannerAdmob.BannerOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _bannerAdmob.BannerOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _bannerAdmob.BannerOnAdClicked += () => { _onAdClicked?.Invoke(); };
            _bannerAdmob.BannerOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _bannerAdmob.BannerOnAdClosed += () => { _onAdClosed?.Invoke(); };
            _bannerAdmob.AdmobOnAdRevenuePaid += (adValue) => { _admobOnAdRevenuePaid?.Invoke(adValue); };
           
        }

        public void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition)
        {
            _bannerAdmob.CreateBannerView(adSize, adPosition);
        }

        public void ShowBannerAd()
        {
            _bannerAdmob.LoadAd();
        }

        public void SetRewardeInterstitialdAdUnitID(string adUnitID)
        {
            _rewardedInterstitialAdmob = new RewardedInterstitialAdmob();
            _rewardedInterstitialAdmob.SetRewardedInterstitialAdUnitID(adUnitID);

            // Subscribe to events
            _rewardedInterstitialAdmob.RewardedOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _rewardedInterstitialAdmob.RewardedOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _rewardedInterstitialAdmob.RewardedOnAdClicked += () => { _onAdClicked?.Invoke(); };
            _rewardedInterstitialAdmob.RewardedOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _rewardedInterstitialAdmob.RewardedOnAdClosed += () => { _onAdClosed?.Invoke(); };
            _rewardedInterstitialAdmob.RewardedOnUserEarnedReward += (reward) => { _onUserEarnedReward?.Invoke(reward); };
            _rewardedInterstitialAdmob.AdmobOnAdRevenuePaid += (adValue) => { _admobOnAdRevenuePaid?.Invoke(adValue); };

            
        }
        public void LoadRewardedInterstitialAd()
        {
            _rewardedInterstitialAdmob.LoadRewardedInterstitialAd();
        }
        public void ShowRewardedInterstitialAd()
        {
            _rewardedInterstitialAdmob.ShowRewardedInterstitialAd();
        }

        public void ShowMediationDebugger()
        {
            _log.Info("Showing mediation debugger");

            MobileAds.OpenAdInspector((AdInspectorError error) =>
            {
                _log.Error("Admob mediation debugger closed with error: " + error);                
            });
        }
    }
}
#endif
