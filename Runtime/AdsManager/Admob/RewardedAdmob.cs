#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.Admob
{
    public class RewardedAdmob
    {
        private readonly NoctuaLogger _log = new(typeof(InterstitialAdmob));
        private string _adUnitIDRewarded;

        private RewardedAd _rewardedAd;

        //public event handler
        public event Action RewardedOnAdDisplayed;
        public event Action RewardedOnAdFailedDisplayed;
        public event Action RewardedOnAdClicked;
        public event Action RewardedOnAdImpressionRecorded;
        public event Action RewardedOnAdClosed;
        public event Action<Reward> RewardedOnUserEarnedReward;
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid;
        
        public void SetRewardedAdUnitID(string adUnitID)
        {
            if (adUnitID == null)
            {
                _log.Error("Ad unit ID Rewarded is empty.");
                return;
            }

            _adUnitIDRewarded = adUnitID;

            _log.Debug("Ad unit ID Rewarded set to : " + _adUnitIDRewarded);
        }

        /// <summary>
        /// Loads the rewarded ad.
        /// </summary>
        public void LoadRewardedAd()
        {
            if (_adUnitIDRewarded == null)
            {
                _log.Error("Ad unit ID Rewarded is empty.");
                return;
            }
            
            // Clean up the old ad before loading a new one.
            CleanupAd();

            _log.Debug("Loading the rewarded ad.");

            // create our request used to load the ad.
            var adRequest = new AdRequest();

            // send the request to load the ad.
            RewardedAd.Load(_adUnitIDRewarded, adRequest,
                (RewardedAd ad, LoadAdError error) =>
                {
                    // if error is not null, the load request failed.
                    if (error != null || ad == null)
                    {
                        _log.Error("Rewarded ad failed to load an ad " +
                                        "with error : " + error);
                        return;
                    }

                    _log.Debug("Rewarded ad loaded with response : "
                                + ad.GetResponseInfo());

                    // // Create and pass the SSV options to the rewarded ad.
                    // var options = new ServerSideVerificationOptions
                    //                     .Builder()
                    //                     .SetCustomData("SAMPLE_CUSTOM_DATA_STRING")
                    //                     .Build();

                    // ad.SetServerSideVerificationOptions(options);

                    TrackAdCustomEventRewarded("ad_loaded");

                    _rewardedAd = ad;
                    RegisterEventHandlers(ad);
                });
        }

        public void ShowRewardedAd()
        {
            const string rewardMsg =
                "Rewarded ad rewarded the user. Type: {0}, amount: {1}.";

            if (_rewardedAd != null && _rewardedAd.CanShowAd())
            {
                _rewardedAd.Show((Reward reward) =>
                {
                    // Called when the user should be rewarded.

                    RewardedOnUserEarnedReward.Invoke(reward);

                    _log.Debug(String.Format(rewardMsg, reward.Type, reward.Amount));

                    _log.Debug("Rewarded ad shown.");
                });
            }
        }

        private void RegisterEventHandlers(RewardedAd ad)
        {
            // Raised when the ad is estimated to have earned money.
            ad.OnAdPaid += (AdValue adValue) =>
            {
                _log.Debug(String.Format("Rewarded ad paid {0} {1}.",
                    adValue.Value,
                    adValue.CurrencyCode));
                
                AdmobOnAdRevenuePaid?.Invoke(adValue, ad.GetResponseInfo());
            };
            // Raised when an impression is recorded for an ad.
            ad.OnAdImpressionRecorded += () =>
            {
                _log.Debug("Rewarded ad recorded an impression.");

                RewardedOnAdImpressionRecorded?.Invoke();
            };
            // Raised when a click is recorded for an ad.
            ad.OnAdClicked += () =>
            {
                _log.Debug("Rewarded ad was clicked.");

                TrackAdCustomEventRewarded("ad_clicked");

                RewardedOnAdClicked?.Invoke();
            };
            // Raised when an ad opened full screen content.
            ad.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("Rewarded ad full screen content opened.");

                TrackAdCustomEventRewarded("ad_shown");

                RewardedOnAdDisplayed?.Invoke();
            };
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("Rewarded ad full screen content closed.");

                LoadRewardedAd();

                TrackAdCustomEventRewarded("ad_closed");

                RewardedOnAdClosed?.Invoke();
            };
            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                _log.Error("Rewarded ad failed to open full screen content " +
                            "with error : " + error);

                LoadRewardedAd();

                TrackAdCustomEventRewarded("ad_shown_failed", new Dictionary<string, IConvertible>()
                {
                    { "error_code", error.GetCode() },
                    { "error_message", error.GetMessage() },
                    { "domain", error.GetDomain() }
                });

                RewardedOnAdFailedDisplayed?.Invoke();
            };
        }

        public void CleanupAd()
        {
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;

                _log.Debug("Rewarded ad cleaned up.");
            }
        }

        private void TrackAdCustomEventRewarded(string eventName, Dictionary<string, IConvertible> extraPayload = null)
        {
            try
            {
                _log.Debug("Tracking custom event for rewarded ad: " + eventName);

                extraPayload ??= new Dictionary<string, IConvertible>();

                // Add basic information that doesn't require the ad instance
                extraPayload.Add("ad_format", "rewarded");
                extraPayload.Add("mediation_service", "admob");
                
                // Only add ad-specific information if the ad instance exists
                if (_rewardedAd != null)
                {
                    var responseInfo = _rewardedAd.GetResponseInfo();
                    if (responseInfo != null)
                    {
                        AdapterResponseInfo loadedAdapterResponseInfo = responseInfo.GetLoadedAdapterResponseInfo();
                        string adSourceName = loadedAdapterResponseInfo?.AdSourceName ?? "empty";
                        extraPayload.Add("ad_network", adSourceName);
                    }
                    else
                    {
                        extraPayload.Add("ad_network", "unknown");
                    }

                    extraPayload.Add("ad_unit_id", _rewardedAd.GetAdUnitID());
                }
                else
                {
                    extraPayload.Add("ad_network", "unknown");
                    extraPayload.Add("ad_unit_id", _adUnitIDRewarded ?? "unknown");
                }

                string properties = "";
                foreach (var (key, value) in extraPayload)
                {
                    properties += $"{key}={value}, ";
                }

                _log.Debug($"Event name: {eventName}, Event properties: {properties}");
            
                // Use the thread-safe event queue instead of direct tracking
                AdmobEventQueue.EnqueueEvent(eventName, extraPayload);
            }
            catch (Exception ex)
            {
                _log.Error($"Error tracking rewarded ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
                // Continue execution - tracking errors shouldn't affect ad functionality
            }
        }
    }
}

#endif