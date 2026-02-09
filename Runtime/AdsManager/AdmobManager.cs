#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;
using com.noctuagames.sdk.Admob;
using System.Collections.Generic;

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
        private event Action _initCompleteAction;
        private event Action _onAdDisplayed;
        private event Action _onAdFailedDisplayed;
        private event Action _onAdClicked;
        private event Action _onAdImpressionRecorded;
        private event Action _onAdClosed;
        private event Action<Reward> _onUserEarnedReward;

        private event Action<AdValue, ResponseInfo> _admobOnAdRevenuePaid;

        // public event handlers
        public event Action OnInitialized { add => _initCompleteAction += value; remove => _initCompleteAction -= value; }
        public event Action OnAdDisplayed { add => _onAdDisplayed += value; remove => _onAdDisplayed -= value; }
        public event Action OnAdFailedDisplayed { add => _onAdFailedDisplayed += value; remove => _onAdFailedDisplayed -= value; } 
        public event Action OnAdClicked { add => _onAdClicked += value; remove => _onAdClicked -= value; }
        public event Action OnAdImpressionRecorded { add => _onAdImpressionRecorded += value; remove => _onAdImpressionRecorded -= value; }
        public event Action OnAdClosed { add => _onAdClosed += value; remove => _onAdClosed -= value; }
        public event Action<Reward> AdmobOnUserEarnedReward { add => _onUserEarnedReward += value; remove => _onUserEarnedReward -= value; }
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid { add => _admobOnAdRevenuePaid += value; remove => _admobOnAdRevenuePaid -= value; }

        internal AdmobManager()
        {
            _log.Debug("AdmobManager constructor");

            _bannerAdmob = new BannerAdmob();
            _rewardedAdmob = new RewardedAdmob();
            _interstitialAdmob = new InterstitialAdmob();
            _rewardedInterstitialAdmob = new RewardedInterstitialAdmob();

        }

        public void Initialize(Action initCompleteAction)
        {
            _log.Info("Initializing Admob SDK");

            MobileAds.Initialize(initStatus =>
            {
                _log.Info("Admob initialized");

                initCompleteAction?.Invoke();
                _initCompleteAction?.Invoke();

                Dictionary<string, AdapterStatus> map = initStatus.getAdapterStatusMap();
                foreach (KeyValuePair<string, AdapterStatus> keyValuePair in map)
                {
                    string className = keyValuePair.Key;
                    AdapterStatus status = keyValuePair.Value;
                    switch (status.InitializationState)
                    {
                    case AdapterState.NotReady:
                        // The adapter initialization did not complete.
                        _log.Info("Adapter: " + className + " not ready.");
                        break;
                    case AdapterState.Ready:
                        // The adapter was successfully initialized.
                        _log.Info("Adapter: " + className + " is initialized.");
                        break;
                    }
                }
            });
        }

        public void SetInterstitialAdUnitID(string adUnitID)
        {            
            _interstitialAdmob.SetInterstitialAdUnitID(adUnitID);

            // Subscribe to events
            _interstitialAdmob.InterstitialOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _interstitialAdmob.InterstitialOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _interstitialAdmob.InterstitialOnAdClicked += () => { _onAdClicked?.Invoke(); };
            _interstitialAdmob.InterstitialOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _interstitialAdmob.InterstitialOnAdClosed += () => { _onAdClosed?.Invoke(); };
            _interstitialAdmob.AdmobOnAdRevenuePaid += (adValue, responseInfo) => { _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo); };
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
            _rewardedAdmob.SetRewardedAdUnitID(adUnitID);

            // Subscribe to events
            _rewardedAdmob.RewardedOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _rewardedAdmob.RewardedOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _rewardedAdmob.RewardedOnAdClicked += () => { _onAdClicked?.Invoke(); };
            _rewardedAdmob.RewardedOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _rewardedAdmob.RewardedOnAdClosed += () => { _onAdClosed?.Invoke(); };
            _rewardedAdmob.RewardedOnUserEarnedReward += (reward) => { _onUserEarnedReward?.Invoke(reward); };
            _rewardedAdmob.AdmobOnAdRevenuePaid += (adValue, responseInfo) => { _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo); };
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
            _bannerAdmob.SetAdUnitId(adUnitID);

            // Subscribe to events
            _bannerAdmob.BannerOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _bannerAdmob.BannerOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _bannerAdmob.BannerOnAdClicked += () => { _onAdClicked?.Invoke(); };
            _bannerAdmob.BannerOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _bannerAdmob.BannerOnAdClosed += () => { _onAdClosed?.Invoke(); };
            _bannerAdmob.AdmobOnAdRevenuePaid += (adValue, responseInfo) => { _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo); };
           
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
            _rewardedInterstitialAdmob.SetRewardedInterstitialAdUnitID(adUnitID);

            // Subscribe to events
            _rewardedInterstitialAdmob.RewardedOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
            _rewardedInterstitialAdmob.RewardedOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
            _rewardedInterstitialAdmob.RewardedOnAdClicked += () => { _onAdClicked?.Invoke(); };
            _rewardedInterstitialAdmob.RewardedOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
            _rewardedInterstitialAdmob.RewardedOnAdClosed += () => { _onAdClosed?.Invoke(); };
            _rewardedInterstitialAdmob.RewardedOnUserEarnedReward += (reward) => { _onUserEarnedReward?.Invoke(reward); };
            _rewardedInterstitialAdmob.AdmobOnAdRevenuePaid += (adValue, responseInfo) => { _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo); };

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
