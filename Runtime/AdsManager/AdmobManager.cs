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
        private BannerAdmob _bannerAdmob;

        // Private event handlers
        // private Action<AdValue> _interstitialOnAdPaid;
        // private Action _interstitialOnAdImpressionRecorded;
        private Action _interstitialOnAdClicked;
        public event Action _interstitialOnAdFullScreenContentOpened;
        private Action _interstitialOnAdFullScreenContentClosed;
        // private Action<AdError> _interstitialOnAdFullScreenContentFailed;

        // Public event properties
        // public event Action<AdValue> InterstitialOnAdPaid
        // {
        //     add => _interstitialOnAdPaid += value;
        //     remove => _interstitialOnAdPaid -= value;
        // }
        // public event Action InterstitialOnAdImpressionRecorded
        // {
        //     add => _interstitialOnAdImpressionRecorded += value;
        //     remove => _interstitialOnAdImpressionRecorded -= value;
        // }
        public event Action InterstitialOnAdClicked
        {
            add => _interstitialOnAdClicked += value;
            remove => _interstitialOnAdClicked -= value;
        }
        public event Action InterstitialOnAdFullScreenContentOpened
        {
            add => _interstitialOnAdFullScreenContentOpened += value;
            remove => _interstitialOnAdFullScreenContentOpened -= value;
        }
        public event Action InterstitialOnAdFullScreenContentClosed
        {
            add => _interstitialOnAdFullScreenContentClosed += value;
            remove => _interstitialOnAdFullScreenContentClosed -= value;
        }
        // public event Action<AdError> InterstitialOnAdFullScreenContentFailed
        // {
        //     add => _interstitialOnAdFullScreenContentFailed += value;
        //     remove => _interstitialOnAdFullScreenContentFailed -= value;
        // }

        public void Initialize(Action initCompleteAction)
        {
            _log.Info("Initializing Admob SDK");

            MobileAds.Initialize(initStatus =>
            {
                _log.Info("Admob initialized");

                initCompleteAction?.Invoke();
            });
        }

        public void LoadInterstitialAd(string adUnitID)
        {
            _interstitialAdmob = new InterstitialAdmob();

            _interstitialAdmob.InterstitialOnAdClicked += _interstitialOnAdClicked;
            _interstitialAdmob.InterstitialOnAdFullScreenContentOpened += () => {
                _log.Info("Interstitial ad opened. AdmobManager");

                _interstitialOnAdFullScreenContentOpened?.Invoke();
            };

            _interstitialAdmob.LoadInterstitialAd(adUnitID);
        }

        public void ShowInterstitial()
        {
            _interstitialAdmob.ShowInterstitialAd();
        }

        public void LoadRewardedAd(string adUnitID)
        {
            _rewardedAdmob = new RewardedAdmob();

            _rewardedAdmob.LoadRewardedAd(adUnitID);
        }

        public void ShowRewardedAd()
        {
            _rewardedAdmob.ShowRewardedAd();
        }

        public void SetBannerAdUnitId(string adUnitID)
        {
            _bannerAdmob = new BannerAdmob();

            _bannerAdmob.SetAdUnitId(adUnitID);
           
        }

        public void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition)
        {
            _bannerAdmob.CreateBannerView(adSize, adPosition);
        }

        public void LoadBannerAd()
        {
            _bannerAdmob.LoadAd();
        }

        public void OnDestroy()
        {
            if (_interstitialAdmob != null)
            {
                // _interstitialAdmob.InterstitialOnAdClicked -= _interstitialOnAdClicked;
                // _interstitialAdmob.InterstitialOnAdFullScreenContentOpened -= _interstitialOnAdFullScreenContentOpened;
                // _interstitialAdmob.InterstitialOnAdFullScreenContentClosed -= _interstitialOnAdFullScreenContentClosed;

                // _interstitialAdmob = null;

                _log.Info("AdmobManager destroyed.");
            }
        }
    }
}
#endif
