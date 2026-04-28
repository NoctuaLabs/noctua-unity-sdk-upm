#if UNITY_APPLOVIN
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace com.noctuagames.sdk.AppLovin
{
    /// <summary>
    /// Manages AppLovin MAX rewarded ad loading, display, and lifecycle events.
    /// Handles rewarded ads with user reward callbacks and exponential backoff retry on load failure.
    /// </summary>
    public class RewardedAppLovin
    {
        private readonly NoctuaLogger _log = new(typeof(RewardedAppLovin));

        private string _adUnitIDRewarded;

        int retryAttempt;

        /// <summary>Raised when the rewarded ad is successfully displayed.</summary>
        public event Action RewardedOnAdDisplayed;

        /// <summary>Raised when the rewarded ad fails to display.</summary>
        public event Action RewardedOnAdFailedDisplayed;

        /// <summary>Raised when the user clicks on the rewarded ad.</summary>
        public event Action RewardedOnAdClicked;

        /// <summary>Raised when a rewarded ad impression is recorded via revenue paid callback.</summary>
        public event Action RewardedOnAdImpressionRecorded;

        /// <summary>Raised when the user earns a reward from watching the ad.</summary>
        public event Action<MaxSdk.Reward> RewardedOnUserEarnedReward;

        /// <summary>Raised when the rewarded ad is closed (hidden) by the user.</summary>
        public event Action RewardedOnAdClosed;

        /// <summary>Raised when rewarded ad revenue is recorded, providing the ad info with revenue data.</summary>
        public event Action<MaxSdkBase.AdInfo> RewardedOnAdRevenuePaid;
        private readonly long _timeoutThreshold = 5000; // 5 seconds
        private bool _callbacksRegistered;
        // Per-show stopwatch — populates `engagement_time` on canonical ad_impression.
        private readonly Stopwatch _showStopwatch = new();

        /// <summary>
        /// Sets the ad unit ID for the rewarded ad.
        /// </summary>
        /// <param name="adUnitID">The AppLovin MAX ad unit ID for rewarded ads.</param>
        public void SetRewardedAdUnitID(string adUnitID)
        {
            if (adUnitID == null)
            {
                _log.Error("Ad unit ID rewarded is empty.");
                return;
            }

            _adUnitIDRewarded = adUnitID;

            _log.Debug("Ad unit ID rewarded set to : " + _adUnitIDRewarded);
        }

        /// <summary>
        /// Loads a rewarded ad and registers event callbacks (only once) for the ad lifecycle.
        /// </summary>
        public void LoadRewardedAds()
        {
            if(_adUnitIDRewarded == null)
            {
                _log.Error("Ad unit ID rewarded is empty.");
                return;
            }

            // Attach callback (only once to prevent duplicate subscriptions)
            if (!_callbacksRegistered)
            {
                _callbacksRegistered = true;
                MaxSdkCallbacks.Rewarded.OnAdLoadedEvent += OnRewardedAdLoadedEvent;
                MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent += OnRewardedAdLoadFailedEvent;
                MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent += OnRewardedAdDisplayedEvent;
                MaxSdkCallbacks.Rewarded.OnAdClickedEvent += OnRewardedAdClickedEvent;
                MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent += OnRewardedAdRevenuePaidEvent;
                MaxSdkCallbacks.Rewarded.OnAdHiddenEvent += OnRewardedAdHiddenEvent;
                MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent += OnRewardedAdFailedToDisplayEvent;
                MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent += OnRewardedAdReceivedRewardEvent;
            }

            // Load the first rewarded ad
            LoadRewardedAd();

            _log.Debug("Rewarded ad loaded for ad unit id : " + _adUnitIDRewarded);
        }

        /// <summary>
        /// Removes all registered callbacks from the static MaxSdkCallbacks events.
        /// Must be called when this instance is being replaced or discarded to prevent
        /// duplicate callbacks if a new RewardedAppLovin instance is created.
        /// </summary>
        public void UnregisterCallbacks()
        {
            if (!_callbacksRegistered) return;

            MaxSdkCallbacks.Rewarded.OnAdLoadedEvent -= OnRewardedAdLoadedEvent;
            MaxSdkCallbacks.Rewarded.OnAdLoadFailedEvent -= OnRewardedAdLoadFailedEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayedEvent -= OnRewardedAdDisplayedEvent;
            MaxSdkCallbacks.Rewarded.OnAdClickedEvent -= OnRewardedAdClickedEvent;
            MaxSdkCallbacks.Rewarded.OnAdRevenuePaidEvent -= OnRewardedAdRevenuePaidEvent;
            MaxSdkCallbacks.Rewarded.OnAdHiddenEvent -= OnRewardedAdHiddenEvent;
            MaxSdkCallbacks.Rewarded.OnAdDisplayFailedEvent -= OnRewardedAdFailedToDisplayEvent;
            MaxSdkCallbacks.Rewarded.OnAdReceivedRewardEvent -= OnRewardedAdReceivedRewardEvent;

            _callbacksRegistered = false;
            _log.Debug("Rewarded callbacks unregistered.");
        }

        private void LoadRewardedAd()
        {
            TrackAdCustomEventRewarded("wf_rewarded_request_start");

            MaxSdk.LoadRewardedAd(_adUnitIDRewarded);

            _log.Debug("Loading rewarded ad for ad unit id : " + _adUnitIDRewarded);
        }
        /// <summary>
        /// Returns whether a rewarded ad is loaded and ready to show.
        /// </summary>
        public bool IsReady() => !string.IsNullOrEmpty(_adUnitIDRewarded) && MaxSdk.IsRewardedAdReady(_adUnitIDRewarded);

        /// <summary>
        /// Shows a previously loaded rewarded ad if it is ready.
        /// </summary>
        public void ShowRewardedAd()
        {
            if (string.IsNullOrEmpty(_adUnitIDRewarded))
            {
                _log.Error("Ad unit ID rewarded is empty.");
                return;
            }

            TrackAdCustomEventRewarded("wf_rewarded_started_playing");

            if (MaxSdk.IsRewardedAdReady(_adUnitIDRewarded))
            {
                _showStopwatch.Restart();
                MaxSdk.ShowRewardedAd(_adUnitIDRewarded);

                _log.Debug("Showing rewarded ad for ad unit id : " + _adUnitIDRewarded);
            }
            else
            {
                _log.Warning("Rewarded ad is not ready to be shown for ad unit id : " + _adUnitIDRewarded);

                TrackAdCustomEventRewarded("wf_rewarded_show_not_ready");
                TrackAdCustomEventRewarded("wf_rewarded_show_failed_null");
            }
        }

        /// <summary>
        /// Shows a previously loaded rewarded ad with a placement name for analytics segmentation.
        /// </summary>
        /// <param name="placement">The placement name for analytics.</param>
        public void ShowRewardedAd(string placement)
        {
            if (string.IsNullOrEmpty(_adUnitIDRewarded))
            {
                _log.Error("Ad unit ID rewarded is empty.");
                return;
            }

            TrackAdCustomEventRewarded("wf_rewarded_started_playing");

            if (MaxSdk.IsRewardedAdReady(_adUnitIDRewarded))
            {
                _showStopwatch.Restart();
                MaxSdk.ShowRewardedAd(_adUnitIDRewarded, placement);

                _log.Debug($"Showing rewarded ad for ad unit id : {_adUnitIDRewarded} with placement : {placement}");
            }
            else
            {
                _log.Warning("Rewarded ad is not ready to be shown for ad unit id : " + _adUnitIDRewarded);

                TrackAdCustomEventRewarded("wf_rewarded_show_not_ready");
                TrackAdCustomEventRewarded("wf_rewarded_show_failed_null");
            }
        }


        private void OnRewardedAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Rewarded ad is ready for you to show. MaxSdk.IsRewardedAdReady(adUnitId) now returns 'true'.

            // Reset retry attempt
            retryAttempt = 0;

            _log.Debug("Rewarded ad loaded for ad unit id : " + adUnitId);

            // Log full waterfall response details
            if (adInfo?.WaterfallInfo?.NetworkResponses != null)
            {
                foreach (var network in adInfo.WaterfallInfo.NetworkResponses)
                {
                    _log.Debug($"Waterfall: {network.MediatedNetwork?.Name ?? "unknown"} " +
                        $"state={network.AdLoadState} latency={network.LatencyMillis}ms");
                }
            }

            // Canonical ad_loaded.
            EmitCanonical(IAAEventNames.AdLoaded, IAAPayloadBuilder.BuildAdLoaded(
                placement:  adInfo?.Placement,
                adType:     AdFormatKey.Rewarded,
                adUnitId:   adUnitId ?? _adUnitIDRewarded,
                adUnitName: _adUnitIDRewarded,
                adSize:     IAAAdSize.Fullscreen,
                adSource:   adInfo?.NetworkName,
                adPlatform: AdNetworkName.AppLovin
            ));
            TrackAdCustomEventRewarded("wf_rewarded_request_adunit_success", adUnitId, adInfo);
            TrackAdCustomEventRewarded("wf_rewarded_request_finished_success", adUnitId, adInfo);
        }

        private void OnRewardedAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            // Rewarded ad failed to load
            // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds).
            RetryLoadRewardedAsync().Forget();

            _log.Debug("Rewarded ad failed to load for ad unit id : " + adUnitId + " with error code : " + errorInfo.Code);

            // Track ad load failed event
            var extraPayload = new Dictionary<string, IConvertible>
            {
                { "error_code", errorInfo.Code },
                { "error_message", errorInfo.Message },
                { "mediator_error_code", errorInfo.MediatedNetworkErrorCode },
                { "mediator_error_message", errorInfo.MediatedNetworkErrorMessage },
                { "latency_millis", errorInfo.LatencyMillis }
            };

            if (errorInfo.LatencyMillis > _timeoutThreshold)
            {
                _log.Warning($"Rewarded ad request took too long: {errorInfo.LatencyMillis} ms, exceeding threshold of {_timeoutThreshold} ms.");

                TrackAdCustomEventRewarded("wf_rewarded_request_adunit_timeout");
            }

            // Canonical ad_load_failed (single `error` string).
            EmitCanonical(IAAEventNames.AdLoadFailed, IAAPayloadBuilder.BuildAdLoadFailed(
                adFormat:   AdFormatKey.Rewarded,
                adPlatform: AdNetworkName.AppLovin,
                adUnitName: adUnitId ?? _adUnitIDRewarded,
                error:      IAAPayloadBuilder.FormatError(
                    (int)errorInfo.Code, errorInfo.Message,
                    errorInfo.MediatedNetworkErrorCode, errorInfo.MediatedNetworkErrorMessage)
            ));
            TrackAdCustomEventRewarded("wf_rewarded_request_adunit_failed", extraPayload: extraPayload);
            TrackAdCustomEventRewarded("wf_rewarded_request_finished_failed", extraPayload: extraPayload);
        }

        private async UniTaskVoid RetryLoadRewardedAsync()
        {
            retryAttempt++;
            double retryDelay = retryAttempt * 10;

            await UniTask.Delay((int)(retryDelay * 1000));
            LoadRewardedAd();

            _log.Debug("Retrying to load rewarded ad after " + retryDelay + " seconds");
        }


        private void OnRewardedAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            // Rewarded ad displayed

            _log.Debug("Rewarded ad displayed for ad unit id : " + adUnitId);

            // Canonical ad_shown.
            EmitCanonical(IAAEventNames.AdShown, IAAPayloadBuilder.BuildAdLoaded(
                placement:  adInfo?.Placement,
                adType:     AdFormatKey.Rewarded,
                adUnitId:   adUnitId ?? _adUnitIDRewarded,
                adUnitName: _adUnitIDRewarded,
                adSize:     IAAAdSize.Fullscreen,
                adSource:   adInfo?.NetworkName,
                adPlatform: AdNetworkName.AppLovin
            ));
            TrackAdCustomEventRewarded("wf_rewarded_show_sdk", adUnitId, adInfo);

            RewardedOnAdDisplayed?.Invoke();
        }

        private void OnRewardedAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            // Rewarded ad failed to display. AppLovin recommends that you load the next ad.
            LoadRewardedAd();

            _log.Debug("Rewarded ad failed to display for ad unit id : " + adUnitId + " with error code : " + errorInfo.Code);

            // Track ad show failed event
            var extraPayload = new Dictionary<string, IConvertible>
            {
                { "error_code", errorInfo.Code },
                { "error_message", errorInfo.Message },
                { "mediator_error_code", errorInfo.MediatedNetworkErrorCode },
                { "mediator_error_message", errorInfo.MediatedNetworkErrorMessage }
            };
            // Canonical ad_show_failed.
            var canonicalShowFailedPayload = IAAPayloadBuilder.BuildAdShowFailed(
                adFormat:   AdFormatKey.Rewarded,
                adPlatform: AdNetworkName.AppLovin,
                adUnitName: adUnitId ?? _adUnitIDRewarded,
                error:      IAAPayloadBuilder.FormatError(
                    (int)errorInfo.Code, errorInfo.Message,
                    errorInfo.MediatedNetworkErrorCode, errorInfo.MediatedNetworkErrorMessage));
            EmitCanonical(IAAEventNames.AdShowFailed, canonicalShowFailedPayload);
            // Deprecated alias — one-release back-compat for dashboards.
            EmitCanonical(IAAEventNames.AdShownFailedLegacy, canonicalShowFailedPayload);
            TrackAdCustomEventRewarded("wf_rewarded_show_sdk_failed", adUnitId, adInfo, extraPayload);

            RewardedOnAdFailedDisplayed?.Invoke();
        }

        private void OnRewardedAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            _log.Debug("Rewarded ad clicked for ad unit id : " + adUnitId);

            // Canonical ad_clicked.
            EmitCanonical(IAAEventNames.AdClicked, IAAPayloadBuilder.BuildAdClicked(
                placement:  adInfo?.Placement,
                adType:     AdFormatKey.Rewarded,
                adUnitId:   adUnitId ?? _adUnitIDRewarded,
                adUnitName: _adUnitIDRewarded,
                adSize:     IAAAdSize.Fullscreen,
                adSource:   adInfo?.NetworkName,
                adPlatform: AdNetworkName.AppLovin
            ));
            TrackAdCustomEventRewarded("wf_rewarded_clicked", adUnitId, adInfo: adInfo);

            RewardedOnAdClicked?.Invoke();
        }

        private void OnRewardedAdHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Rewarded ad is hidden. Pre-load the next ad
            LoadRewardedAd();

            _log.Debug("Rewarded ad hidden for ad unit id : " + adUnitId);

            // Track ad closed event
            TrackAdCustomEventRewarded("ad_closed", adUnitId, adInfo);
            TrackAdCustomEventRewarded("wf_rewarded_closed", adUnitId, adInfo);

            RewardedOnAdClosed?.Invoke();
        }

        private void OnRewardedAdReceivedRewardEvent(string adUnitId, MaxSdk.Reward reward, MaxSdkBase.AdInfo adInfo)
        {
            // The rewarded ad displayed and the user should receive the reward.
            _log.Info("Rewarded user: " + reward.Amount + " " + reward.Label);

            _log.Debug("Rewarded ad received reward for ad unit id : " + adUnitId);

            // Track reward earned event
            var extraPayload = new Dictionary<string, IConvertible>
            {
                { "reward_amount", reward.Amount },
                { "reward_type", reward.Label }
            };
            TrackAdCustomEventRewarded("reward_earned", adUnitId, adInfo, extraPayload);

            // Canonical: a successful rewarded view counts as one ad-watch.
            // Fires watch_ads_5x/10x/25x/50x at thresholds (rewarded + interstitial only).
            AdWatchMilestoneTracker.Default?.RecordWatch(AdFormatKey.Rewarded);

            RewardedOnUserEarnedReward?.Invoke(reward);
        }

        private void OnRewardedAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Ad revenue paid. Use this callback to track user revenue.
            _log.Debug("Rewarded ad revenue paid for ad unit id : " + adUnitId);

            // Canonical ad_impression with revenue + engagement_time.
            var engagementMs = _showStopwatch.IsRunning ? _showStopwatch.ElapsedMilliseconds : 0;
            EmitCanonical(IAAEventNames.AdImpression, IAAPayloadBuilder.BuildAdImpression(
                placement:        adInfo?.Placement,
                adType:           AdFormatKey.Rewarded,
                adUnitId:         adInfo?.AdUnitIdentifier ?? adUnitId ?? _adUnitIDRewarded,
                adUnitName:       _adUnitIDRewarded,
                value:            adInfo?.Revenue ?? 0,
                valueUsd:         adInfo?.Revenue ?? 0,
                adSize:           IAAAdSize.Fullscreen,
                adSource:         adInfo?.NetworkName,
                adPlatform:       AdNetworkName.AppLovin,
                engagementTimeMs: engagementMs
            ));
            _showStopwatch.Reset();
            // Legacy per-format alias retained one release.
            TrackAdCustomEventRewarded("ad_impression_rewarded", adUnitId: adUnitId, adInfo: adInfo);

            RewardedOnAdImpressionRecorded?.Invoke();
            RewardedOnAdRevenuePaid?.Invoke(adInfo);
        }
        
        private void TrackAdCustomEventRewarded(string eventName, string adUnitId = null, MaxSdkBase.AdInfo adInfo = null, Dictionary<string, IConvertible> extraPayload = null)
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
                payload["mediation_service"] = AdNetworkName.AppLovin;
                payload["ad_unit_id"] = adUnitId ?? _adUnitIDRewarded ?? "unknown";

                // Add ad info if available
                if (adInfo != null)
                {
                    payload["ad_network"] = adInfo.NetworkName ?? "unknown";
                    payload["placement"] = adInfo.Placement ?? "unknown";
                    payload["network_placement"] = adInfo.NetworkPlacement ?? "unknown";
                    payload["ntw"] = adInfo.WaterfallInfo.Name ?? "unknown";
                    payload["latency_millis"] = adInfo.LatencyMillis;
                }
                else
                {
                    payload["ad_network"] = "unknown";
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
                // Continue execution - tracking errors shouldn't affect ad functionality
            }
        }

        // Emits a canonical IAA event. Payload must already be built with IAAPayloadBuilder.*.
        private void EmitCanonical(string eventName, Dictionary<string, IConvertible> payload)
        {
            try
            {
                Noctua.Event.TrackCustomEvent(eventName, payload);
            }
            catch (Exception ex)
            {
                _log.Error($"Error tracking canonical event '{eventName}': {ex.Message}");
            }
        }
    }
}
#endif