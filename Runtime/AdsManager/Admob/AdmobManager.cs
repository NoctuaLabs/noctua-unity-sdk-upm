#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;
using com.noctuagames.sdk.Admob;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// AdMob implementation of <see cref="IAdNetwork"/> that manages interstitial, rewarded,
    /// rewarded interstitial, and banner ads through the Google Mobile Ads SDK.
    /// </summary>
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

        // Subscription guards to prevent duplicate event wiring on re-init
        private bool _interstitialEventsSubscribed;
        private bool _rewardedEventsSubscribed;
        private bool _bannerEventsSubscribed;
        private bool _rewardedInterstitialEventsSubscribed;

        /// <summary>Raised when the AdMob SDK has completed initialization.</summary>
        public event Action OnInitialized { add => _initCompleteAction += value; remove => _initCompleteAction -= value; }

        /// <summary>Raised when any ad format is successfully displayed to the user.</summary>
        public event Action OnAdDisplayed { add => _onAdDisplayed += value; remove => _onAdDisplayed -= value; }

        /// <summary>Raised when any ad format fails to display.</summary>
        public event Action OnAdFailedDisplayed { add => _onAdFailedDisplayed += value; remove => _onAdFailedDisplayed -= value; }

        /// <summary>Raised when the user clicks on any displayed ad.</summary>
        public event Action OnAdClicked { add => _onAdClicked += value; remove => _onAdClicked -= value; }

        /// <summary>Raised when an ad impression is recorded for any ad format.</summary>
        public event Action OnAdImpressionRecorded { add => _onAdImpressionRecorded += value; remove => _onAdImpressionRecorded -= value; }

        /// <summary>Raised when any ad is closed by the user.</summary>
        public event Action OnAdClosed { add => _onAdClosed += value; remove => _onAdClosed -= value; }

        /// <summary>Raised when the user earns a reward from watching a rewarded or rewarded interstitial ad.</summary>
        public event Action<Reward> AdmobOnUserEarnedReward { add => _onUserEarnedReward += value; remove => _onUserEarnedReward -= value; }

        /// <summary>Raised when ad revenue is recorded, providing the ad value and response information.</summary>
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid { add => _admobOnAdRevenuePaid += value; remove => _admobOnAdRevenuePaid -= value; }

        internal AdmobManager()
        {
            _log.Debug("AdmobManager constructor");

            _bannerAdmob = new BannerAdmob();
            _rewardedAdmob = new RewardedAdmob();
            _interstitialAdmob = new InterstitialAdmob();
            _rewardedInterstitialAdmob = new RewardedInterstitialAdmob();

        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public void SetInterstitialAdUnitID(string adUnitID)
        {
            _interstitialAdmob.SetInterstitialAdUnitID(adUnitID);

            if (!_interstitialEventsSubscribed)
            {
                _interstitialEventsSubscribed = true;

                // Subscribe to events (only once)
                _interstitialAdmob.InterstitialOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
                _interstitialAdmob.InterstitialOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
                _interstitialAdmob.InterstitialOnAdClicked += () => { _onAdClicked?.Invoke(); };
                _interstitialAdmob.InterstitialOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
                _interstitialAdmob.InterstitialOnAdClosed += () => { _onAdClosed?.Invoke(); };
                _interstitialAdmob.AdmobOnAdRevenuePaid += (adValue, responseInfo) => { _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo); };
            }
        }

        /// <inheritdoc />
        public void LoadInterstitialAd()
        {
            _interstitialAdmob.LoadInterstitialAd();
        }

        /// <inheritdoc />
        public void ShowInterstitial()
        {
            _interstitialAdmob.ShowInterstitialAd();
        }

        /// <inheritdoc />
        public void SetRewardedAdUnitID(string adUnitID)
        {
            _rewardedAdmob.SetRewardedAdUnitID(adUnitID);

            if (!_rewardedEventsSubscribed)
            {
                _rewardedEventsSubscribed = true;

                // Subscribe to events (only once)
                _rewardedAdmob.RewardedOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
                _rewardedAdmob.RewardedOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
                _rewardedAdmob.RewardedOnAdClicked += () => { _onAdClicked?.Invoke(); };
                _rewardedAdmob.RewardedOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
                _rewardedAdmob.RewardedOnAdClosed += () => { _onAdClosed?.Invoke(); };
                _rewardedAdmob.RewardedOnUserEarnedReward += (reward) => { _onUserEarnedReward?.Invoke(reward); };
                _rewardedAdmob.AdmobOnAdRevenuePaid += (adValue, responseInfo) => { _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo); };
            }
        }

        /// <inheritdoc />
        public void LoadRewardedAd()
        {
            _rewardedAdmob.LoadRewardedAd();
        }

        /// <inheritdoc />
        public void ShowRewardedAd()
        {
            _rewardedAdmob.ShowRewardedAd();
        }

        /// <inheritdoc />
        public void SetBannerAdUnitId(string adUnitID)
        {
            _bannerAdmob.SetAdUnitId(adUnitID);

            if (!_bannerEventsSubscribed)
            {
                _bannerEventsSubscribed = true;

                // Subscribe to events (only once)
                _bannerAdmob.BannerOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
                _bannerAdmob.BannerOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
                _bannerAdmob.BannerOnAdClicked += () => { _onAdClicked?.Invoke(); };
                _bannerAdmob.BannerOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
                _bannerAdmob.BannerOnAdClosed += () => { _onAdClosed?.Invoke(); };
                _bannerAdmob.AdmobOnAdRevenuePaid += (adValue, responseInfo) => { _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo); };
            }
        }

        /// <inheritdoc />
        public void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition)
        {
            _bannerAdmob.CreateBannerView(adSize, adPosition);
        }

        /// <inheritdoc />
        public void ShowBannerAd()
        {
            _bannerAdmob.LoadAd();
        }

        /// <inheritdoc />
        public void SetRewardedInterstitialAdUnitID(string adUnitID)
        {
            _rewardedInterstitialAdmob.SetRewardedInterstitialAdUnitID(adUnitID);

            if (!_rewardedInterstitialEventsSubscribed)
            {
                _rewardedInterstitialEventsSubscribed = true;

                // Subscribe to events (only once)
                _rewardedInterstitialAdmob.RewardedOnAdDisplayed += () => { _onAdDisplayed?.Invoke(); };
                _rewardedInterstitialAdmob.RewardedOnAdFailedDisplayed += () => { _onAdFailedDisplayed?.Invoke(); };
                _rewardedInterstitialAdmob.RewardedOnAdClicked += () => { _onAdClicked?.Invoke(); };
                _rewardedInterstitialAdmob.RewardedOnAdImpressionRecorded += () => { _onAdImpressionRecorded?.Invoke(); };
                _rewardedInterstitialAdmob.RewardedOnAdClosed += () => { _onAdClosed?.Invoke(); };
                _rewardedInterstitialAdmob.RewardedOnUserEarnedReward += (reward) => { _onUserEarnedReward?.Invoke(reward); };
                _rewardedInterstitialAdmob.AdmobOnAdRevenuePaid += (adValue, responseInfo) => { _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo); };
            }
        }
        /// <inheritdoc />
        public void LoadRewardedInterstitialAd()
        {
            _rewardedInterstitialAdmob.LoadRewardedInterstitialAd();
        }

        /// <inheritdoc />
        public void ShowRewardedInterstitialAd()
        {
            _rewardedInterstitialAdmob.ShowRewardedInterstitialAd();
        }

        /// <inheritdoc />
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
