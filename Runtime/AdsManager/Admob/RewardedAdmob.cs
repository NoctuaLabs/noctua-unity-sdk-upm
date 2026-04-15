#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private int _retryAttempt;
        // Last AdValue from OnAdPaid — captured so OnAdImpressionRecorded can emit canonical revenue.
        private AdValue _lastAdValue;
        // Monotonic clock — engagement_time = ms between Show() and impression callback.
        private readonly Stopwatch _showStopwatch = new();
        
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

                        var responseInfo = error?.GetResponseInfo();
                        if (responseInfo != null)
                        {
                            _log.Warning($"Response ID: {responseInfo.GetResponseId()}");
                            _log.Warning($"Mediation adapter: {responseInfo.GetMediationAdapterClassName()}");
                        }

                        // Canonical ad_load_failed (was missing entirely on AdMob)
                        EmitCanonical(IAAEventNames.AdLoadFailed, IAAPayloadBuilder.BuildAdLoadFailed(
                            adFormat:   AdFormatKey.Rewarded,
                            adPlatform: AdNetworkName.Admob,
                            adUnitName: _adUnitIDRewarded,
                            error:      IAAPayloadBuilder.FormatError(
                                error?.GetCode() ?? -1,
                                error?.GetMessage(),
                                error?.GetDomain())
                        ));

                        TrackAdCustomEventRewarded("wf_rewarded_request_adunit_failed", extraPayload);
                        TrackAdCustomEventRewarded("wf_rewarded_request_finished_failed", extraPayload);

                        RetryLoadRewardedAsync().Forget();
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
                            _log.Warning($"Rewarded ad request took too long: {latencyMillis} ms, exceeding threshold of {_timeoutThreshold} ms.");

                            TrackAdCustomEventRewarded("wf_rewarded_request_adunit_timeout");
                        }
                    }

                    _log.Debug("Rewarded ad loaded with response : "
                                + ad.GetResponseInfo());

                    _rewardedAd = ad;

                    // Canonical ad_loaded (was missing entirely on AdMob)
                    var loadedAdapter = ad.GetResponseInfo()?.GetLoadedAdapterResponseInfo();
                    string adSource = null;
                    try { adSource = loadedAdapter?.AdSourceName; } catch {}

                    EmitCanonical(IAAEventNames.AdLoaded, IAAPayloadBuilder.BuildAdLoaded(
                        placement:  null,
                        adType:     AdFormatKey.Rewarded,
                        adUnitId:   _adUnitIDRewarded,
                        adUnitName: _adUnitIDRewarded,
                        adSize:     IAAAdSize.Fullscreen,
                        adSource:   adSource,
                        adPlatform: AdNetworkName.Admob
                    ));

                    TrackAdCustomEventRewarded("wf_rewarded_request_adunit_success");
                    TrackAdCustomEventRewarded("wf_rewarded_request_finished_success");

                    RegisterEventHandlers(ad);
                });
        }

        /// <summary>
        /// Returns true if a legacy-loaded rewarded ad is ready to show.
        /// Used by the Editor fallback path and secondary-network checks.
        /// </summary>
        public bool IsReady() => _rewardedAd != null && _rewardedAd.CanShowAd();

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
                _showStopwatch.Restart();
                _rewardedAd.Show((Reward reward) =>
                {
                    // Called when the user should be rewarded.

                    RewardedOnUserEarnedReward?.Invoke(reward);

                    _log.Debug(String.Format(rewardMsg, reward.Type, reward.Amount));

                    // Rewarded counts as one watched ad on reward callback (user finished video).
                    AdWatchMilestoneTracker.Default?.RecordWatch(AdFormatKey.Rewarded);

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

                _lastAdValue = adValue;
                AdmobOnAdRevenuePaid?.Invoke(adValue, ad.GetResponseInfo());
            };
            // Raised when an impression is recorded for an ad.
            ad.OnAdImpressionRecorded += () =>
            {
                _log.Debug("Rewarded ad recorded an impression.");

                var engagementMs = _showStopwatch.IsRunning ? _showStopwatch.ElapsedMilliseconds : 0L;
                _showStopwatch.Reset();

                var valueMicros = _lastAdValue?.Value ?? 0L;
                var value       = valueMicros / 1_000_000d;
                var valueUsd    = value;

                var loadedAdapter = ad.GetResponseInfo()?.GetLoadedAdapterResponseInfo();
                string adSource = null;
                try { adSource = loadedAdapter?.AdSourceName; } catch {}

                EmitCanonical(IAAEventNames.AdImpression, IAAPayloadBuilder.BuildAdImpression(
                    placement:        null,
                    adType:           AdFormatKey.Rewarded,
                    adUnitId:         _adUnitIDRewarded,
                    adUnitName:       _adUnitIDRewarded,
                    value:            value,
                    valueUsd:         valueUsd,
                    adSize:           IAAAdSize.Fullscreen,
                    adSource:         adSource,
                    adPlatform:       AdNetworkName.Admob,
                    engagementTimeMs: engagementMs
                ));

                TrackAdCustomEventRewarded("ad_impression_rewarded");

                RewardedOnAdImpressionRecorded?.Invoke();
            };
            // Raised when a click is recorded for an ad.
            ad.OnAdClicked += () =>
            {
                _log.Debug("Rewarded ad was clicked.");

                var loadedAdapter = ad.GetResponseInfo()?.GetLoadedAdapterResponseInfo();
                string adSource = null;
                try { adSource = loadedAdapter?.AdSourceName; } catch {}

                EmitCanonical(IAAEventNames.AdClicked, IAAPayloadBuilder.BuildAdClicked(
                    placement:  null,
                    adType:     AdFormatKey.Rewarded,
                    adUnitId:   _adUnitIDRewarded,
                    adUnitName: _adUnitIDRewarded,
                    adSize:     IAAAdSize.Fullscreen,
                    adSource:   adSource,
                    adPlatform: AdNetworkName.Admob
                ));

                TrackAdCustomEventRewarded("wf_rewarded_clicked");

                RewardedOnAdClicked?.Invoke();
            };
            // Raised when an ad opened full screen content.
            ad.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("Rewarded ad full screen content opened.");

                var loadedAdapter = ad.GetResponseInfo()?.GetLoadedAdapterResponseInfo();
                string adSource = null;
                try { adSource = loadedAdapter?.AdSourceName; } catch {}

                EmitCanonical(IAAEventNames.AdShown, IAAPayloadBuilder.BuildAdLoaded(
                    placement:  null,
                    adType:     AdFormatKey.Rewarded,
                    adUnitId:   _adUnitIDRewarded,
                    adUnitName: _adUnitIDRewarded,
                    adSize:     IAAAdSize.Fullscreen,
                    adSource:   adSource,
                    adPlatform: AdNetworkName.Admob
                ));

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

                EmitCanonical(IAAEventNames.AdShowFailed, IAAPayloadBuilder.BuildAdShowFailed(
                    adFormat:   AdFormatKey.Rewarded,
                    adPlatform: AdNetworkName.Admob,
                    adUnitName: _adUnitIDRewarded,
                    error:      IAAPayloadBuilder.FormatError(
                        error.GetCode(), error.GetMessage(), error.GetDomain())
                ));

                TrackAdCustomEventRewarded("wf_rewarded_show_sdk_failed", showFailedPayload);

                RewardedOnAdFailedDisplayed?.Invoke();
            };
        }

        private async UniTaskVoid RetryLoadRewardedAsync()
        {
            _retryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, _retryAttempt));

            _log.Debug($"Retrying to load rewarded ad after {retryDelay} seconds (attempt {_retryAttempt})");

            await UniTask.Delay((int)(retryDelay * 1000));
            LoadRewardedAd();
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

                // Copy so we never mutate the caller's dictionary — the same dict is often
                // passed to multiple sequential TrackAdCustomEventRewarded calls.
                var payload = extraPayload != null
                    ? new Dictionary<string, IConvertible>(extraPayload)
                    : new Dictionary<string, IConvertible>();

                payload["ad_format"] = AdFormatKey.Rewarded;
                payload["mediation_service"] = AdNetworkName.Admob;

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

                            payload["ad_network"] = adSourceName;
                            payload["ntw"] = adapterClassName;
                            payload["latency_millis"] = latencyMillis;
                        }
                        else
                        {
                            payload["ad_network"] = "unknown";
                        }
                    }
                    else
                    {
                        payload["ad_network"] = "unknown";
                    }

                    payload["ad_unit_id"] = _rewardedAd.GetAdUnitID() ?? "unknown";
                }
                else
                {
                    payload["ad_network"] = "unknown";
                    payload["ad_unit_id"] = _adUnitIDRewarded ?? "unknown";
                }

                string properties = "";
                foreach (var (key, value) in payload)
                {
                    properties += $"{key}={value}, ";
                }

                _log.Debug($"Event name: {eventName}, Event properties: {properties}");

                Noctua.Event.TrackCustomEvent(eventName, payload);
            }
            catch (Exception ex)
            {
                _log.Error($"Error tracking rewarded ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Routes a canonical IAA event payload through Noctua.Event. Wrapped in try/catch
        // so analytics failures never break ad delivery.
        private void EmitCanonical(string eventName, Dictionary<string, IConvertible> payload)
        {
            try
            {
                Noctua.Event.TrackCustomEvent(eventName, payload);
            }
            catch (Exception ex)
            {
                _log.Error($"Error emitting canonical rewarded event '{eventName}': {ex.Message}");
            }
        }
    }
}

#endif