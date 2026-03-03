#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.Admob
{
    /// <summary>
    /// Manages AdMob rewarded ad loading, display, and lifecycle events.
    /// Handles full-screen rewarded ads with user reward callbacks and automatic reload on close or failure.
    /// </summary>
    public class RewardedAdmob
    {
        private readonly NoctuaLogger _log = new(typeof(RewardedAdmob));
        private string _adUnitIDRewarded;

        private RewardedAd _rewardedAd;

        /// <summary>Raised when the rewarded ad is successfully displayed.</summary>
        public event Action RewardedOnAdDisplayed;

        /// <summary>Raised when the rewarded ad fails to display.</summary>
        public event Action RewardedOnAdFailedDisplayed;

        /// <summary>Raised when the user clicks on the rewarded ad.</summary>
        public event Action RewardedOnAdClicked;

        /// <summary>Raised when a rewarded ad impression is recorded.</summary>
        public event Action RewardedOnAdImpressionRecorded;

        /// <summary>Raised when the rewarded ad is closed by the user.</summary>
        public event Action RewardedOnAdClosed;

        /// <summary>Raised when the user earns a reward from watching the ad.</summary>
        public event Action<Reward> RewardedOnUserEarnedReward;

        /// <summary>Raised when rewarded ad revenue is recorded, providing the ad value and response info.</summary>
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid;
        private readonly long _timeoutThreshold = 5000; // 5 seconds
        
        /// <summary>
        /// Sets the ad unit ID for the rewarded ad.
        /// </summary>
        /// <param name="adUnitID">The AdMob ad unit ID for rewarded ads.</param>
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
            TrackAdCustomEventRewarded("wf_rewarded_request_start");

            if (string.IsNullOrEmpty(_adUnitIDRewarded) || _adUnitIDRewarded == "unknown")
            {
                _log.Error("Ad unit ID Rewarded is not configured.");
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
                    // Check for error first to avoid NullReferenceException on ad
                    if (error != null || ad == null)
                    {
                        _log.Error("Rewarded ad failed to load an ad " +
                                        "with error : " + error);

                        var extraPayload = new Dictionary<string, IConvertible>
                        {
                            { "error_code", error?.GetCode() ?? -1 },
                            { "error_message", error?.GetMessage() ?? "unknown" },
                            { "domain", error?.GetDomain() ?? "unknown" },
                            { "ad_unit_id", _adUnitIDRewarded ?? "unknown" }
                        };

                        TrackAdCustomEventRewarded("wf_rewarded_request_adunit_failed", extraPayload);
                        TrackAdCustomEventRewarded("wf_rewarded_request_finished_failed", extraPayload);
                        return;
                    }

                    if (ad.GetResponseInfo() != null)
                    {
                        AdapterResponseInfo loadedAdapterResponseInfo = ad.GetResponseInfo().GetLoadedAdapterResponseInfo();

                        long latencyMillis = loadedAdapterResponseInfo?.LatencyMillis ?? 0;

                        if (latencyMillis > _timeoutThreshold)
                        {
                            _log.Warning($"Rewarded ad request took too long: {latencyMillis} ms, exceeding threshold of {_timeoutThreshold} ms.");

                            TrackAdCustomEventRewarded("wf_rewarded_request_adunit_timeout");
                        }
                    }

                    _log.Debug("Rewarded ad loaded with response : "
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

        /// <summary>
        /// Shows a previously loaded rewarded ad, invoking the reward callback when the user completes viewing.
        /// </summary>
        public void ShowRewardedAd()
        {
            const string rewardMsg =
                "Rewarded ad rewarded the user. Type: {0}, amount: {1}.";

            TrackAdCustomEventRewarded("wf_rewarded_started_playing");

            if (_rewardedAd != null && _rewardedAd.CanShowAd())
            {
                _rewardedAd.Show((Reward reward) =>
                {
                    // Called when the user should be rewarded.

                    RewardedOnUserEarnedReward?.Invoke(reward);

                    _log.Debug(String.Format(rewardMsg, reward.Type, reward.Amount));

                    _log.Debug("Rewarded ad shown.");
                });
            }
            else
            {
                TrackAdCustomEventRewarded("wf_rewarded_show_not_ready");
                TrackAdCustomEventRewarded("wf_rewarded_show_failed_null");

                _log.Error("Rewarded ad is not ready to be shown.");
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

                TrackAdCustomEventRewarded("ad_impression");
                TrackAdCustomEventRewarded("ad_impression_rewarded");

                RewardedOnAdImpressionRecorded?.Invoke();
            };
            // Raised when a click is recorded for an ad.
            ad.OnAdClicked += () =>
            {
                _log.Debug("Rewarded ad was clicked.");

                TrackAdCustomEventRewarded("ad_clicked");
                TrackAdCustomEventRewarded("wf_rewarded_clicked");

                RewardedOnAdClicked?.Invoke();
            };
            // Raised when an ad opened full screen content.
            ad.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("Rewarded ad full screen content opened.");

                TrackAdCustomEventRewarded("ad_shown");
                TrackAdCustomEventRewarded("wf_rewarded_show_sdk");

                RewardedOnAdDisplayed?.Invoke();
            };
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("Rewarded ad full screen content closed.");

                LoadRewardedAd();

                TrackAdCustomEventRewarded("ad_closed");
                TrackAdCustomEventRewarded("wf_rewarded_closed");

                RewardedOnAdClosed?.Invoke();
            };
            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                _log.Error("Rewarded ad failed to open full screen content " +
                            "with error : " + error);

                LoadRewardedAd();

                var showFailedPayload = new Dictionary<string, IConvertible>()
                {
                    { "error_code", error.GetCode() },
                    { "error_message", error.GetMessage() },
                    { "domain", error.GetDomain() }
                };

                TrackAdCustomEventRewarded("ad_show_failed", showFailedPayload);
                TrackAdCustomEventRewarded("wf_rewarded_show_sdk_failed", showFailedPayload);

                RewardedOnAdFailedDisplayed?.Invoke();
            };
        }

        /// <summary>
        /// Destroys the current rewarded ad instance and releases its resources.
        /// </summary>
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
                        if (loadedAdapterResponseInfo != null)
                        {
                            string adSourceName = "empty";
                            string adapterClassName = "empty";
                            long latencyMillis = 0;

                            try { adSourceName = loadedAdapterResponseInfo.AdSourceName ?? "empty"; } catch {}
                            try { adapterClassName = loadedAdapterResponseInfo.AdapterClassName ?? "empty"; } catch {}
                            try { latencyMillis = loadedAdapterResponseInfo.LatencyMillis; } catch {}

                            extraPayload.Add("ad_network", adSourceName);
                            extraPayload.Add("ntw", adapterClassName);
                            extraPayload.Add("latency_millis", latencyMillis);
                        }
                        else
                        {
                            extraPayload.Add("ad_network", "unknown");
                        }
                    }
                    else
                    {
                        extraPayload.Add("ad_network", "unknown");
                    }

                    extraPayload.Add("ad_unit_id", _rewardedAd.GetAdUnitID() ?? "unknown");
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

                Noctua.Event.TrackCustomEvent(eventName, extraPayload);
            }
            catch (Exception ex)
            {
                _log.Error($"Error tracking rewarded ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

#endif