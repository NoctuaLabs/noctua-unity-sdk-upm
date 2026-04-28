#if UNITY_APPLOVIN
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace com.noctuagames.sdk.AppLovin
{
    /// <summary>
    /// Manages AppLovin MAX interstitial ad loading, display, and lifecycle events.
    /// Handles full-screen interstitial ads with exponential backoff retry on load failure.
    /// </summary>
    public class InterstitialAppLovin
    {
        private readonly NoctuaLogger _log = new(typeof(InterstitialAppLovin));

        private string _adUnitIDInterstitial;

        int retryAttempt;

        /// <summary>Raised when the interstitial ad is successfully displayed.</summary>
        public event Action InterstitialOnAdDisplayed;

        /// <summary>Raised when the interstitial ad fails to display.</summary>
        public event Action InterstitialOnAdFailedDisplayed;

        /// <summary>Raised when the user clicks on the interstitial ad.</summary>
        public event Action InterstitialOnAdClicked;

        /// <summary>Raised when an interstitial ad impression is recorded via revenue paid callback.</summary>
        public event Action InterstitialOnAdImpressionRecorded;

        /// <summary>Raised when the interstitial ad is closed (hidden) by the user.</summary>
        public event Action InterstitialOnAdClosed;

        /// <summary>Raised when interstitial ad revenue is recorded, providing the ad info with revenue data.</summary>
        public event Action<MaxSdkBase.AdInfo> InterstitialOnAdRevenuePaid;
        private readonly long _timeoutThreshold = 5000; // 5 seconds
        private bool _callbacksRegistered;
        // Per-show stopwatch, used to populate `engagement_time` on ad_impression.
        // Restarted on every Show() call; read on the impression callback.
        private readonly Stopwatch _showStopwatch = new();

        /// <summary>
        /// Sets the ad unit ID for the interstitial ad.
        /// </summary>
        /// <param name="adUnitID">The AppLovin MAX ad unit ID for interstitial ads.</param>
        public void SetInterstitialAdUnitID(string adUnitID)
        {
            if (adUnitID == null)
            {
                _log.Error("Ad unit ID Interstitial is empty.");
                return;
            }

            _adUnitIDInterstitial = adUnitID;

            _log.Debug("Ad unit ID Interstitial set to : " + _adUnitIDInterstitial);
        }

        /// <summary>
        /// Loads an interstitial ad and registers event callbacks (only once) for the ad lifecycle.
        /// </summary>
        public void LoadInterstitial()
        {
            if (_adUnitIDInterstitial == null)
            {
                _log.Error("Ad unit ID Interstitial is empty.");
                return;
            }

            // Attach callback (only once to prevent duplicate subscriptions)
            if (!_callbacksRegistered)
            {
                _callbacksRegistered = true;
                MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
                MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailedEvent;
                MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayedEvent;
                MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialClickedEvent;
                MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHiddenEvent;
                MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialAdFailedToDisplayEvent;
                MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnAdRevenuePaidEvent;
            }

            // Load the first interstitial
            LoadInterstitialInternal();

            _log.Debug("Interstitial ad loaded for ad unit id : " + _adUnitIDInterstitial);
        }

        /// <summary>
        /// Returns whether an interstitial ad is loaded and ready to show.
        /// </summary>
        public bool IsReady() => !string.IsNullOrEmpty(_adUnitIDInterstitial) && MaxSdk.IsInterstitialReady(_adUnitIDInterstitial);

        /// <summary>
        /// Shows a previously loaded interstitial ad if it is ready.
        /// </summary>
        public void ShowInterstitial()
        {
            if (string.IsNullOrEmpty(_adUnitIDInterstitial))
            {
                _log.Info("Ad unit ID Interstitial is empty.");
                return;
            }

            TrackAdCustomEventInterstitial("wf_interstitial_started_playing");

            if (MaxSdk.IsInterstitialReady(_adUnitIDInterstitial))
            {
                _showStopwatch.Restart();
                MaxSdk.ShowInterstitial(_adUnitIDInterstitial);

                _log.Debug("Showing interstitial ad for ad unit id : " + _adUnitIDInterstitial);
            }
            else
            {
                _log.Error("Interstitial ad is not ready to be shown for ad unit id : " + _adUnitIDInterstitial);

                TrackAdCustomEventInterstitial("wf_interstitial_show_not_ready");
                TrackAdCustomEventInterstitial("wf_interstitial_show_failed_null");
            }
        }

        /// <summary>
        /// Shows a previously loaded interstitial ad with a placement name for analytics segmentation.
        /// </summary>
        /// <param name="placement">The placement name for analytics.</param>
        public void ShowInterstitial(string placement)
        {
            if (string.IsNullOrEmpty(_adUnitIDInterstitial))
            {
                _log.Info("Ad unit ID Interstitial is empty.");
                return;
            }

            TrackAdCustomEventInterstitial("wf_interstitial_started_playing");

            if (MaxSdk.IsInterstitialReady(_adUnitIDInterstitial))
            {
                _showStopwatch.Restart();
                MaxSdk.ShowInterstitial(_adUnitIDInterstitial, placement);

                _log.Debug($"Showing interstitial ad for ad unit id : {_adUnitIDInterstitial} with placement : {placement}");
            }
            else
            {
                _log.Error("Interstitial ad is not ready to be shown for ad unit id : " + _adUnitIDInterstitial);

                TrackAdCustomEventInterstitial("wf_interstitial_show_not_ready");
                TrackAdCustomEventInterstitial("wf_interstitial_show_failed_null");
            }
        }

        /// <summary>
        /// Removes all registered callbacks from the static MaxSdkCallbacks events.
        /// Must be called when this instance is being replaced or discarded to prevent
        /// duplicate callbacks if a new InterstitialAppLovin instance is created.
        /// </summary>
        public void UnregisterCallbacks()
        {
            if (!_callbacksRegistered) return;

            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent -= OnInterstitialLoadedEvent;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent -= OnInterstitialLoadFailedEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent -= OnInterstitialDisplayedEvent;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent -= OnInterstitialClickedEvent;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent -= OnInterstitialHiddenEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent -= OnInterstitialAdFailedToDisplayEvent;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent -= OnAdRevenuePaidEvent;

            _callbacksRegistered = false;
            _log.Debug("Interstitial callbacks unregistered.");
        }

        private void LoadInterstitialInternal()
        {
            TrackAdCustomEventInterstitial("wf_interstitial_request_start");

            MaxSdk.LoadInterstitial(_adUnitIDInterstitial);

            _log.Debug("Loading interstitial ad for ad unit id : " + _adUnitIDInterstitial);
        }

        private void OnInterstitialLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Interstitial ad is ready for you to show. MaxSdk.IsInterstitialReady(adUnitId) now returns 'true'

            // Reset retry attempt
            retryAttempt = 0;

            _log.Debug("Interstitial ad loaded for ad unit id : " + adUnitId);

            // Log full waterfall response details
            if (adInfo?.WaterfallInfo?.NetworkResponses != null)
            {
                foreach (var network in adInfo.WaterfallInfo.NetworkResponses)
                {
                    _log.Debug($"Waterfall: {network.MediatedNetwork?.Name ?? "unknown"} " +
                        $"state={network.AdLoadState} latency={network.LatencyMillis}ms");
                }
            }

            // Canonical ad_loaded event (full canonical key set, no waterfall fields).
            EmitCanonical(IAAEventNames.AdLoaded, IAAPayloadBuilder.BuildAdLoaded(
                placement:  adInfo?.Placement,
                adType:     AdFormatKey.Interstitial,
                adUnitId:   adUnitId ?? _adUnitIDInterstitial,
                adUnitName: _adUnitIDInterstitial,
                adSize:     IAAAdSize.Fullscreen,
                adSource:   adInfo?.NetworkName,
                adPlatform: AdNetworkName.AppLovin
            ));
            TrackAdCustomEventInterstitial("wf_interstitial_adunit_success");
            TrackAdCustomEventInterstitial("wf_interstitial_finished_success");
        }

        private void OnInterstitialLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            // Interstitial ad failed to load
            // AppLovin recommends that you retry with exponentially higher delays, up to a maximum delay (in this case 64 seconds)
            RetryLoadInterstitialAsync().Forget();

            _log.Debug("Interstitial ad failed to load for ad unit id : " + adUnitId + " with error code : " + errorInfo.Code);
            
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
                _log.Warning($"Interstitial ad request took too long: {errorInfo.LatencyMillis} ms, exceeding threshold of {_timeoutThreshold} ms.");

                TrackAdCustomEventInterstitial("wf_inter_request_adunit_timeout", extraPayload: extraPayload);
            }

            // Canonical ad_load_failed: collapses code+message+mediator into single `error` string.
            EmitCanonical(IAAEventNames.AdLoadFailed, IAAPayloadBuilder.BuildAdLoadFailed(
                adFormat:   AdFormatKey.Interstitial,
                adPlatform: AdNetworkName.AppLovin,
                adUnitName: adUnitId ?? _adUnitIDInterstitial,
                error:      IAAPayloadBuilder.FormatError(
                    (int)errorInfo.Code, errorInfo.Message,
                    errorInfo.MediatedNetworkErrorCode, errorInfo.MediatedNetworkErrorMessage)
            ));
            TrackAdCustomEventInterstitial("wf_interstitial_request_adunit_failed", extraPayload: extraPayload);
            TrackAdCustomEventInterstitial("wf_inter_request_finished_failed", extraPayload: extraPayload);
        }

        // Async method handling the delay
        private async UniTaskVoid RetryLoadInterstitialAsync()
        {
            retryAttempt++;
            double retryDelay = retryAttempt * 10;

            await UniTask.Delay((int)(retryDelay * 1000));
            LoadInterstitialInternal();

            _log.Debug("Retrying to load interstitial ad after " + retryDelay + " seconds");
        }

        private void OnInterstitialDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            // Interstitial ad displayed

            _log.Debug("Interstitial ad displayed for ad unit id : " + adUnitId);

            // ad_shown carries the same canonical key set as ad_loaded (no revenue yet).
            EmitCanonical(IAAEventNames.AdShown, IAAPayloadBuilder.BuildAdLoaded(
                placement:  adInfo?.Placement,
                adType:     AdFormatKey.Interstitial,
                adUnitId:   adUnitId ?? _adUnitIDInterstitial,
                adUnitName: _adUnitIDInterstitial,
                adSize:     IAAAdSize.Fullscreen,
                adSource:   adInfo?.NetworkName,
                adPlatform: AdNetworkName.AppLovin
            ));
            TrackAdCustomEventInterstitial("wf_interstitial_show_sdk");

            InterstitialOnAdDisplayed?.Invoke();
        }

        private void OnInterstitialAdFailedToDisplayEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            // Interstitial ad failed to display. AppLovin recommends that you load the next ad.
            LoadInterstitialInternal();

            _log.Debug("Interstitial ad failed to display for ad unit id : " + adUnitId + " with error code : " + errorInfo.Code);

            // Track ad show failed event
            var extraPayload = new Dictionary<string, IConvertible>
            {
                { "error_code", errorInfo.Code },
                { "error_message", errorInfo.Message },
                { "mediator_error_code", errorInfo.MediatedNetworkErrorCode },
                { "mediator_error_message", errorInfo.MediatedNetworkErrorMessage },
                { "latency_millis", errorInfo.LatencyMillis }
            };
            
            // Canonical ad_show_failed (single `error` string).
            var canonicalShowFailedPayload = IAAPayloadBuilder.BuildAdShowFailed(
                adFormat:   AdFormatKey.Interstitial,
                adPlatform: AdNetworkName.AppLovin,
                adUnitName: adUnitId ?? _adUnitIDInterstitial,
                error:      IAAPayloadBuilder.FormatError(
                    (int)errorInfo.Code, errorInfo.Message,
                    errorInfo.MediatedNetworkErrorCode, errorInfo.MediatedNetworkErrorMessage));
            EmitCanonical(IAAEventNames.AdShowFailed, canonicalShowFailedPayload);
            // Deprecated alias — kept one release for dashboard back-compat.
            EmitCanonical(IAAEventNames.AdShownFailedLegacy, canonicalShowFailedPayload);
            TrackAdCustomEventInterstitial("wf_interstitial_show_sdk_failed", extraPayload: extraPayload);

            InterstitialOnAdFailedDisplayed?.Invoke();
        }

        private void OnInterstitialClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {

            _log.Debug("Interstitial ad clicked for ad unit id : " + adUnitId);

            // Canonical ad_clicked.
            EmitCanonical(IAAEventNames.AdClicked, IAAPayloadBuilder.BuildAdClicked(
                placement:  adInfo?.Placement,
                adType:     AdFormatKey.Interstitial,
                adUnitId:   adUnitId ?? _adUnitIDInterstitial,
                adUnitName: _adUnitIDInterstitial,
                adSize:     IAAAdSize.Fullscreen,
                adSource:   adInfo?.NetworkName,
                adPlatform: AdNetworkName.AppLovin
            ));
            TrackAdCustomEventInterstitial("wf_interstitial_clicked");

            InterstitialOnAdClicked?.Invoke();
        }

        private void OnInterstitialHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            // Interstitial ad is hidden. Pre-load the next ad.
            LoadInterstitialInternal();

            _log.Debug("Interstitial ad hidden for ad unit id : " + adUnitId);

            // Track ad closed event
            TrackAdCustomEventInterstitial("ad_closed", adUnitId, adInfo);
            TrackAdCustomEventInterstitial("wf_interstitial_closed");

            // A successful interstitial close counts as one ad-watch.
            // Fires watch_ads_5x/10x/25x/50x at thresholds (rewarded + interstitial only).
            AdWatchMilestoneTracker.Default?.RecordWatch(AdFormatKey.Interstitial);

            InterstitialOnAdClosed?.Invoke();
        }

        private void OnAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            double revenue = adInfo.Revenue;

            // Miscellaneous data
            string countryCode = MaxSdk.GetSdkConfiguration().CountryCode; // "US" for the United States, etc - Note: Do not confuse this with currency code which is "USD"
            string networkName = adInfo.NetworkName; // Display name of the network that showed the ad
            string adUnitIdentifier = adInfo.AdUnitIdentifier; // The MAX Ad Unit ID
            string placement = adInfo.Placement; // The placement this ad's postbacks are tied to
            string networkPlacement = adInfo.NetworkPlacement; // The placement ID from the network that showed the ad

            _log.Debug("Interstitial ad revenue paid for ad unit id : " + adUnitId + " with revenue : " + revenue + " and country code : " + countryCode);

            // Canonical ad_impression: AppLovin reports `Revenue` in USD per docs,
            // so value == value_usd. Engagement = ms between Show() and impression callback.
            var engagementMs = _showStopwatch.IsRunning ? _showStopwatch.ElapsedMilliseconds : 0;
            EmitCanonical(IAAEventNames.AdImpression, IAAPayloadBuilder.BuildAdImpression(
                placement:        placement,
                adType:           AdFormatKey.Interstitial,
                adUnitId:         adUnitIdentifier ?? _adUnitIDInterstitial,
                adUnitName:       _adUnitIDInterstitial,
                value:            revenue,
                valueUsd:         revenue,
                adSize:           IAAAdSize.Fullscreen,
                adSource:         networkName,
                adPlatform:       AdNetworkName.AppLovin,
                engagementTimeMs: engagementMs
            ));
            _showStopwatch.Reset();
            // Keep the legacy per-format alias for one release.
            TrackAdCustomEventInterstitial("ad_impression_interstitial");

            InterstitialOnAdImpressionRecorded?.Invoke();
            InterstitialOnAdRevenuePaid?.Invoke(adInfo);
        }

        private void TrackAdCustomEventInterstitial(string eventName, string adUnitId = null, MaxSdkBase.AdInfo adInfo = null, Dictionary<string, IConvertible> extraPayload = null)
        {
            try
            {
                _log.Debug("Tracking custom event for interstitial ad: " + eventName);

                // Copy so we never mutate the caller's dictionary — the same dict is often
                // passed to multiple sequential TrackAdCustomEventInterstitial calls.
                var payload = extraPayload != null
                    ? new Dictionary<string, IConvertible>(extraPayload)
                    : new Dictionary<string, IConvertible>();

                payload["ad_format"] = AdFormatKey.Interstitial;
                payload["mediation_service"] = AdNetworkName.AppLovin;
                payload["ad_unit_id"] = adUnitId ?? _adUnitIDInterstitial ?? "unknown";

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
                _log.Error($"Error tracking interstitial ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
            }
        }

        // Emits a canonical IAA event (ad_impression / ad_loaded / ad_load_failed /
        // ad_show_failed / ad_clicked / ad_shown). Payload must already be canonical —
        // use IAAPayloadBuilder.* to build it. Never adds waterfall/legacy fields.
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