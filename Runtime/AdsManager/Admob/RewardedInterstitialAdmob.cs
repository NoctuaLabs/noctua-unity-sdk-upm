#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;

namespace com.noctuagames.sdk.Admob
{
    public class RewardedInterstitialAdmob
    {
        private readonly NoctuaLogger _log = new(typeof(InterstitialAdmob));
        private string _adUnitIDRewarded;

        private RewardedInterstitialAd _rewardedAd;

        //public event handler
        public event Action RewardedOnAdDisplayed;
        public event Action RewardedOnAdFailedDisplayed;
        public event Action RewardedOnAdClicked;
        public event Action RewardedOnAdImpressionRecorded;
        public event Action RewardedOnAdClosed;
        public event Action<Reward> RewardedOnUserEarnedReward;
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid;
        
        public void SetRewardedInterstitialAdUnitID(string adUnitID)
        {
            if (adUnitID == null)
            {
                _log.Error("Ad unit ID Rewarded Interstitial is empty.");
                return;
            }

            _adUnitIDRewarded = adUnitID;

            _log.Debug("Ad unit ID Rewarded Interstitial set to : " + _adUnitIDRewarded);
        }

        /// <summary>
        /// Loads the rewarded ad.
        /// </summary>
        public void LoadRewardedInterstitialAd()
        {
            if (_adUnitIDRewarded == null)
            {
                _log.Error("Ad unit ID Rewarded Interstitial is empty.");
                return;
            }
            
            // Clean up the old ad before loading a new one.
            CleanupAd();

            _log.Debug("Loading the rewarded Interstitial ad.");

            // create our request used to load the ad.
            var adRequest = new AdRequest();

            // send the request to load the ad.
            RewardedInterstitialAd.Load(_adUnitIDRewarded, adRequest,
                (RewardedInterstitialAd ad, LoadAdError error) =>
                {
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        _log.Error("Rewarded Interstitial ad failed to load an ad " +
                                        "with error : " + error);
                        return;
                    }

                    _log.Debug("Rewarded Interstitial ad loaded with response : "
                                + ad.GetResponseInfo());

                    // // Create and pass the SSV options to the rewarded ad.
                    // var options = new ServerSideVerificationOptions
                    //                     .Builder()
                    //                     .SetCustomData("SAMPLE_CUSTOM_DATA_STRING")
                    //                     .Build();

                    // ad.SetServerSideVerificationOptions(options);

                    _rewardedAd = ad;
                    RegisterEventHandlers(ad);
                });
        }

        public void ShowRewardedInterstitialAd()
        {
            const string rewardMsg =
                "Rewarded Interstitial ad rewarded the user. Type: {0}, amount: {1}.";

            if (_rewardedAd != null && _rewardedAd.CanShowAd())
            {
                _rewardedAd.Show((Reward reward) =>
                {
                    // Called when the user should be rewarded.

                    RewardedOnUserEarnedReward.Invoke(reward);

                    _log.Debug(String.Format(rewardMsg, reward.Type, reward.Amount));

                    _log.Debug("Rewarded Interstitial ad shown.");
                });
            }
        }

        private void RegisterEventHandlers(RewardedInterstitialAd ad)
        {
            // Raised when the ad is estimated to have earned money.
            ad.OnAdPaid += (AdValue adValue) =>
            {
                _log.Debug(String.Format("Rewarded Interstitial ad paid {0} {1}.",
                    adValue.Value,
                    adValue.CurrencyCode));
                
                AdmobOnAdRevenuePaid?.Invoke(adValue, ad.GetResponseInfo());
            };
            // Raised when an impression is recorded for an ad.
            ad.OnAdImpressionRecorded += () =>
            {
                _log.Debug("Rewarded Interstitial ad recorded an impression.");

                RewardedOnAdImpressionRecorded?.Invoke();
            };
            // Raised when a click is recorded for an ad.
            ad.OnAdClicked += () =>
            {
                _log.Debug("Rewarded Interstitial ad was clicked.");

                RewardedOnAdClicked?.Invoke();
            };
            // Raised when an ad opened full screen content.
            ad.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("Rewarded Interstitial ad full screen content opened.");

                RewardedOnAdDisplayed?.Invoke();
            };
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("Rewarded Interstitial ad full screen content closed.");

                LoadRewardedInterstitialAd();

                RewardedOnAdClosed?.Invoke();
            };
            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                _log.Error("Rewarded Interstitial ad failed to open full screen content " +
                            "with error : " + error);

                LoadRewardedInterstitialAd();

                RewardedOnAdFailedDisplayed?.Invoke();
            };
        }

        public void CleanupAd()
        {
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;

                _log.Debug("Rewarded Interstitial ad cleaned up.");
            }
        }
    }
}

#endif