#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.Admob
{
    /// <summary>
    /// Manages AdMob rewarded interstitial ad loading, display, and lifecycle events.
    /// Combines interstitial and rewarded ad behavior with user reward callbacks and automatic reload.
    /// </summary>
    public class RewardedInterstitialAdmob
    {
        private readonly NoctuaLogger _log = new(typeof(RewardedInterstitialAdmob));
        private string _adUnitIDRewarded;

        private RewardedInterstitialAd _rewardedAd;

        /// <summary>Raised when the rewarded interstitial ad is successfully displayed.</summary>
        public event Action RewardedOnAdDisplayed;

        /// <summary>Raised when the rewarded interstitial ad fails to display.</summary>
        public event Action RewardedOnAdFailedDisplayed;

        /// <summary>Raised when the user clicks on the rewarded interstitial ad.</summary>
        public event Action RewardedOnAdClicked;

        /// <summary>Raised when a rewarded interstitial ad impression is recorded.</summary>
        public event Action RewardedOnAdImpressionRecorded;

        /// <summary>Raised when the rewarded interstitial ad is closed by the user.</summary>
        public event Action RewardedOnAdClosed;

        /// <summary>Raised when the user earns a reward from watching the rewarded interstitial ad.</summary>
        public event Action<Reward> RewardedOnUserEarnedReward;

        /// <summary>Raised when rewarded interstitial ad revenue is recorded, providing the ad value and response info.</summary>
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid;
        private readonly long _timeoutThreshold = 5000; // 5 seconds
        private int _retryAttempt;
        
        /// <summary>
        /// Sets the ad unit ID for the rewarded interstitial ad.
        /// </summary>
        /// <param name="adUnitID">The AdMob ad unit ID for rewarded interstitial ads.</param>
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
            TrackAdCustomEventRewardedInterstitial("wf_rewarded_interstitial_request_start");

            if (string.IsNullOrEmpty(_adUnitIDRewarded) || _adUnitIDRewarded == "unknown")
            {
                _log.Error("Ad unit ID Rewarded Interstitial is not configured.");
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
                    // Check for error first to avoid NullReferenceException on ad
                    if (error != null || ad == null)
                    {
                        _log.Error("Rewarded Interstitial ad failed to load an ad " +
                                        "with error : " + error);

                        var extraPayload = new Dictionary<string, IConvertible>
                        {
                            { "error_code", error?.GetCode() ?? -1 },
                            { "error_message", error?.GetMessage() ?? "unknown" },
                            { "domain", error?.GetDomain() ?? "unknown" },
                            { "ad_unit_id", _adUnitIDRewarded ?? "unknown" }
                        };

                        var responseInfo = error?.GetResponseInfo();
                        if (responseInfo != null)
                        {
                            _log.Warning($"Response ID: {responseInfo.GetResponseId()}");
                            _log.Warning($"Mediation adapter: {responseInfo.GetMediationAdapterClassName()}");
                        }

                        TrackAdCustomEventRewardedInterstitial("wf_rewarded_interstitial_request_adunit_failed", extraPayload);
                        TrackAdCustomEventRewardedInterstitial("wf_rewarded_interstitial_request_finished_failed", extraPayload);

                        RetryLoadRewardedInterstitialAsync().Forget();
                        return;
                    }

                    // Reset retry attempt on success
                    _retryAttempt = 0;

                    if (ad.GetResponseInfo() != null)
                    {
                        AdapterResponseInfo loadedAdapterResponseInfo = ad.GetResponseInfo().GetLoadedAdapterResponseInfo();

                        long latencyMillis = loadedAdapterResponseInfo?.LatencyMillis ?? 0;

                        if (latencyMillis > _timeoutThreshold)
                        {
                            _log.Warning($"Rewarded interstitial ad request took too long: {latencyMillis} ms, exceeding threshold of {_timeoutThreshold} ms.");

                            TrackAdCustomEventRewardedInterstitial("wf_rewarded_interstitial_request_adunit_timeout");
                        }
                    }

                    _log.Debug("Rewarded Interstitial ad loaded with response : "
                                + ad.GetResponseInfo());

                    _rewardedAd = ad;
                    RegisterEventHandlers(ad);
                });
        }

        /// <summary>
        /// Shows a previously loaded rewarded interstitial ad, invoking the reward callback when the user completes viewing.
        /// </summary>
        public void ShowRewardedInterstitialAd()
        {
            const string rewardMsg =
                "Rewarded Interstitial ad rewarded the user. Type: {0}, amount: {1}.";

            TrackAdCustomEventRewardedInterstitial("wf_rewarded_interstitial_started_playing");

            if (_rewardedAd != null && _rewardedAd.CanShowAd())
            {
                _rewardedAd.Show((Reward reward) =>
                {
                    // Called when the user should be rewarded.

                    RewardedOnUserEarnedReward?.Invoke(reward);

                    _log.Debug(String.Format(rewardMsg, reward.Type, reward.Amount));

                    _log.Debug("Rewarded Interstitial ad shown.");
                });
            }
            else
            {
                TrackAdCustomEventRewardedInterstitial("wf_rewarded_interstitial_show_not_ready");
                TrackAdCustomEventRewardedInterstitial("wf_rewarded_interstitial_show_failed_null");

                _log.Error("Rewarded Interstitial ad is not ready to be shown.");
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

                TrackAdCustomEventRewardedInterstitial("ad_impression");
                TrackAdCustomEventRewardedInterstitial("ad_impression_rewarded_interstitial");

                RewardedOnAdImpressionRecorded?.Invoke();
            };
            // Raised when a click is recorded for an ad.
            ad.OnAdClicked += () =>
            {
                _log.Debug("Rewarded Interstitial ad was clicked.");

                TrackAdCustomEventRewardedInterstitial("ad_clicked");
                TrackAdCustomEventRewardedInterstitial("wf_rewarded_interstitial_clicked");

                RewardedOnAdClicked?.Invoke();
            };
            // Raised when an ad opened full screen content.
            ad.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("Rewarded Interstitial ad full screen content opened.");

                TrackAdCustomEventRewardedInterstitial("ad_shown");
                TrackAdCustomEventRewardedInterstitial("wf_rewarded_interstitial_show_sdk");

                RewardedOnAdDisplayed?.Invoke();
            };
            // Raised when the ad closed full screen content.
            ad.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("Rewarded Interstitial ad full screen content closed.");

                LoadRewardedInterstitialAd();

                TrackAdCustomEventRewardedInterstitial("ad_closed");
                TrackAdCustomEventRewardedInterstitial("wf_rewarded_interstitial_closed");

                RewardedOnAdClosed?.Invoke();
            };
            // Raised when the ad failed to open full screen content.
            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                _log.Error("Rewarded Interstitial ad failed to open full screen content " +
                            "with error : " + error);

                LoadRewardedInterstitialAd();

                TrackAdCustomEventRewardedInterstitial("ad_show_failed", new Dictionary<string, IConvertible>()
                {
                    { "error_code", error.GetCode() },
                    { "error_message", error.GetMessage() },
                    { "domain", error.GetDomain() }
                });

                TrackAdCustomEventRewardedInterstitial("wf_rewarded_interstitial_show_sdk_failed");

                RewardedOnAdFailedDisplayed?.Invoke();
            };
        }

        private async UniTaskVoid RetryLoadRewardedInterstitialAsync()
        {
            _retryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, _retryAttempt));

            _log.Debug($"Retrying to load rewarded interstitial ad after {retryDelay} seconds (attempt {_retryAttempt})");

            await UniTask.Delay((int)(retryDelay * 1000));
            LoadRewardedInterstitialAd();
        }

        /// <summary>
        /// Destroys the current rewarded interstitial ad instance and releases its resources.
        /// </summary>
        public void CleanupAd()
        {
            if (_rewardedAd != null)
            {
                _rewardedAd.Destroy();
                _rewardedAd = null;

                _log.Debug("Rewarded Interstitial ad cleaned up.");
            }
        }
        private void TrackAdCustomEventRewardedInterstitial(string eventName, Dictionary<string, IConvertible> extraPayload = null)
        {
            try
            {
                _log.Debug("Tracking custom event for rewarded interstitial ad: " + eventName);

                extraPayload ??= new Dictionary<string, IConvertible>();

                // Add basic information that doesn't require the ad instance
                extraPayload.Add("ad_format", AdFormatKey.RewardedInterstitial);
                extraPayload.Add("mediation_service", AdNetworkName.Admob);
                
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
                _log.Error($"Error tracking rewarded interstitial ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}

#endif