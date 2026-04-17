#if UNITY_APPLOVIN
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk.AppLovin
{
    /// <summary>
    /// Manages AppLovin MAX banner ad creation, display, hiding, and lifecycle events.
    /// Supports banner positioning, width configuration, and auto-refresh control.
    /// </summary>
    public class BannerAppLovin
    {
        private readonly NoctuaLogger _log = new(typeof(BannerAppLovin));

        private string _adUnitIDBanner;

        // Creation state — MaxSdk.CreateBanner must be called before MaxSdk.ShowBanner.
        // We store the last-used color + position so ShowBanner can auto-create if the caller
        // never called InitializeBannerAds explicitly.
        private bool _bannerCreated;
        private Color _lastColor = Color.black;
        private MaxSdk.AdViewPosition _lastPosition = MaxSdk.AdViewPosition.BottomCenter;

        /// <summary>Raised when a banner ad is successfully loaded and displayed.</summary>
        public event Action BannerOnAdDisplayed;

        /// <summary>Raised when a banner ad fails to load.</summary>
        public event Action BannerOnAdFailedDisplayed;

        /// <summary>Raised when the user clicks on the banner ad.</summary>
        public event Action BannerOnAdClicked;

        /// <summary>Raised when a banner ad impression is recorded.</summary>
        public event Action BannerOnAdImpressionRecorded;

        /// <summary>Raised when the banner ad is closed or collapsed.</summary>
        public event Action BannerOnAdClosed;

        /// <summary>Raised when banner ad revenue is recorded, providing the ad info with revenue data.</summary>
        public event Action<MaxSdkBase.AdInfo> BannerOnAdRevenuePaid;
        private readonly long _timeoutThreshold = 5000; // 5 seconds
        private bool _callbacksRegistered;

        /// <summary>
        /// Sets the ad unit ID for the banner ad.
        /// </summary>
        /// <param name="adUnitIDBanner">The AppLovin MAX ad unit ID for banner ads.</param>
        public void SetBannerAdUnitId(string adUnitIDBanner)
        {
            if (adUnitIDBanner == null)
            {
                _log.Error("Ad unit ID banner is empty.");
                return;
            }

            _adUnitIDBanner = adUnitIDBanner;

            _log.Debug("Banner ad unit ID set to : " + adUnitIDBanner);
        }

        /// <summary>
        /// Creates and initializes a banner ad with the specified background color and position (deprecated).
        /// </summary>
        /// <param name="color">The background color for the banner.</param>
        /// <param name="bannerPosition">The screen position where the banner should be displayed.</param>
        [Obsolete(
            "This method is deprecated. Please use InitializeBannerAds(Color, MaxSdk.AdViewPosition) instead."
        )]
        public void InitializeBannerAds(Color color, MaxSdkBase.BannerPosition bannerPosition)
        {
            TrackAdCustomEventBanner("wf_banner_request_start");

            MaxSdk.CreateBanner(_adUnitIDBanner, bannerPosition);
            MaxSdk.SetBannerBackgroundColor(_adUnitIDBanner, color);
            _bannerCreated = true;

            RegisterCallbacks();

            _log.Debug("Banner ad initialized for ad unit id : " + _adUnitIDBanner);
            TrackAdCustomEventBanner("wf_banner_started_playing");
        }

        /// <summary>
        /// Creates and initializes a banner ad with the specified background color and position.
        /// </summary>
        /// <param name="color">The background color for the banner.</param>
        /// <param name="bannerPosition">The screen position where the banner should be displayed.</param>
        public void InitializeBannerAds(Color color, MaxSdk.AdViewPosition bannerPosition)
        {
            _lastColor    = color;
            _lastPosition = bannerPosition;

            var adViewConfiguration = new MaxSdk.AdViewConfiguration(bannerPosition);
            MaxSdk.CreateBanner(_adUnitIDBanner, adViewConfiguration);
            MaxSdk.SetBannerBackgroundColor(_adUnitIDBanner, color);
            _bannerCreated = true;

            RegisterCallbacks();

            _log.Debug("Banner ad initialized for ad unit id : " + _adUnitIDBanner);
            TrackAdCustomEventBanner("wf_banner_started_playing");
        }

        /// <summary>
        /// Shows the banner ad for the configured ad unit ID.
        /// </summary>
        public void ShowBanner()
        {
            if (string.IsNullOrEmpty(_adUnitIDBanner))
            {
                _log.Error("Ad unit ID banner is empty.");
                return;
            }

            // Auto-create the banner if InitializeBannerAds was never called.
            // This handles callers that call ShowBannerAd() directly without an explicit
            // CreateBannerViewAdAppLovin() step (e.g. the MediationScene sample app).
            if (!_bannerCreated)
            {
                _log.Warning("Banner not yet created — auto-creating with defaults before show.");
                InitializeBannerAds(_lastColor, _lastPosition);
            }

            MaxSdk.ShowBanner(_adUnitIDBanner);

            _log.Debug("Banner ad shown for ad unit id : " + _adUnitIDBanner);

            // Banner has no native display callback in MAX — emit ad_shown here so
            // analytics parity with Interstitial/Rewarded/AppOpen is maintained.
            EmitCanonical(IAAEventNames.AdShown, IAAPayloadBuilder.BuildAdLoaded(
                placement:  _lastPlacement,
                adType:     AdFormatKey.Banner,
                adUnitId:   _adUnitIDBanner,
                adUnitName: _adUnitIDBanner,
                adSize:     IAAAdSize.Banner320,
                adSource:   null,
                adPlatform: AdNetworkName.AppLovin
            ));
        }

        /// <summary>
        /// Hides the banner ad without destroying it, allowing it to be shown again later.
        /// </summary>
        public void HideBanner()
        {
            MaxSdk.HideBanner(_adUnitIDBanner);

            _log.Debug("Banner ad hidden for ad unit id : " + _adUnitIDBanner);

            // Use wf_banner_hidden (not wf_banner_closed) to distinguish a temporary hide
            // from a permanent DestroyBanner(). Both previously emitted wf_banner_closed,
            // making it impossible to differentiate hide vs destroy in analytics.
            TrackAdCustomEventBanner("wf_banner_hidden");
        }

        /// <summary>
        /// Destroys the banner ad and frees its resources.
        /// Do not call this if multiple ad instances share the same ad unit ID.
        /// </summary>
        public void DestroyBanner()
        {
            MaxSdk.DestroyBanner(_adUnitIDBanner);
            _bannerCreated = false;

            _log.Debug("Banner ad destroyed for ad unit id : " + _adUnitIDBanner);

            TrackAdCustomEventBanner("wf_banner_closed");
        }

        /// <summary>
        /// Sets the banner width in pixels. Must be at least 320 on phones or 728 on tablets for viewability.
        /// </summary>
        /// <param name="width">The desired banner width in pixels.</param>
        public void SetBannerWidth(int width)
        {
            MaxSdk.SetBannerWidth(_adUnitIDBanner, width);

            _log.Debug("Banner ad width set to : " + width + " for ad unit id : " + _adUnitIDBanner);
        }

        /// <summary>
        /// Gets the current position and size of the banner ad in Unity screen coordinates.
        /// </summary>
        /// <returns>A <see cref="Rect"/> representing the banner’s layout position and dimensions.</returns>
        public Rect GetBannerPosition()
        {
            _log.Debug("Getting banner position for ad unit id : " + _adUnitIDBanner);

            return MaxSdk.GetBannerLayout(_adUnitIDBanner);
        }
        /// <summary>
        /// Stops automatic banner ad refresh, allowing manual refresh control.
        /// </summary>
        public void StopBannerAutoRefresh()
        {
            _log.Debug("Stopping banner auto refresh for ad unit id : " + _adUnitIDBanner);

            MaxSdk.StopBannerAutoRefresh(_adUnitIDBanner);
        }

        /// <summary>
        /// Resumes automatic banner ad refresh.
        /// </summary>
        public void StartBannerAutoRefresh()
        {
            _log.Debug("Starting banner auto refresh for ad unit id : " + _adUnitIDBanner);

            MaxSdk.StartBannerAutoRefresh(_adUnitIDBanner);
        }

        // Placement cached locally so the synchronous ad_shown emission (no adInfo yet) can fill the canonical payload.
        // AdInfo-driven emissions (impression / click / revenue) continue to use adInfo?.Placement directly.
        private string _lastPlacement;

        /// <summary>
        /// Sets the placement name for the banner ad for analytics segmentation.
        /// </summary>
        /// <param name="placement">The placement name.</param>
        public void SetPlacement(string placement)
        {
            _lastPlacement = placement;
            MaxSdk.SetBannerPlacement(_adUnitIDBanner, placement);

            _log.Debug($"Banner placement set to '{placement}' for ad unit id : {_adUnitIDBanner}");
        }

        /// <summary>
        /// Sets the banner auto-refresh interval in seconds. Clamped to 10-120s per AppLovin requirements.
        /// </summary>
        /// <param name="seconds">Refresh interval in seconds (10-120).</param>
        public void SetRefreshInterval(int seconds)
        {
            seconds = Math.Max(10, Math.Min(120, seconds));
            MaxSdk.SetBannerExtraParameter(_adUnitIDBanner, "ad_refresh_seconds", seconds.ToString());

            _log.Debug($"Banner refresh interval set to {seconds}s for ad unit id : {_adUnitIDBanner}");
        }

        /// <summary>
        /// Removes all registered callbacks from the static MaxSdkCallbacks events.
        /// Must be called when this instance is being replaced or discarded to prevent
        /// duplicate callbacks if a new BannerAppLovin instance is created.
        /// </summary>
        public void UnregisterCallbacks()
        {
            if (!_callbacksRegistered) return;

            MaxSdkCallbacks.Banner.OnAdLoadedEvent      -= OnBannerAdLoadedEvent;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent  -= OnBannerAdLoadFailedEvent;
            MaxSdkCallbacks.Banner.OnAdClickedEvent     -= OnBannerAdClickedEvent;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent -= OnBannerAdRevenuePaidEvent;
            MaxSdkCallbacks.Banner.OnAdExpandedEvent    -= OnBannerAdExpandedEvent;
            MaxSdkCallbacks.Banner.OnAdCollapsedEvent   -= OnBannerAdCollapsedEvent;

            _callbacksRegistered = false;
            _log.Debug("Banner callbacks unregistered.");
        }

        private void RegisterCallbacks()
        {
            if (!_callbacksRegistered)
            {
                _callbacksRegistered = true;
                MaxSdkCallbacks.Banner.OnAdLoadedEvent      += OnBannerAdLoadedEvent;
                MaxSdkCallbacks.Banner.OnAdLoadFailedEvent  += OnBannerAdLoadFailedEvent;
                MaxSdkCallbacks.Banner.OnAdClickedEvent     += OnBannerAdClickedEvent;
                MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaidEvent;
                MaxSdkCallbacks.Banner.OnAdExpandedEvent    += OnBannerAdExpandedEvent;
                MaxSdkCallbacks.Banner.OnAdCollapsedEvent   += OnBannerAdCollapsedEvent;
            }
        }

        private void OnBannerAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            _log.Debug("Banner ad loaded for ad unit id : " + adUnitId);

            // Canonical ad_loaded — see IAAEventNames / IAAPayloadBuilder
            EmitCanonical(IAAEventNames.AdLoaded, IAAPayloadBuilder.BuildAdLoaded(
                placement:  adInfo?.Placement,
                adType:     AdFormatKey.Banner,
                adUnitId:   adUnitId,
                adUnitName: adUnitId,
                adSize:     IAAAdSize.Banner320,
                adSource:   adInfo?.NetworkName,
                adPlatform: AdNetworkName.AppLovin
            ));

            TrackAdCustomEventBanner("wf_banner_request_adunit_success");
            TrackAdCustomEventBanner("wf_banner_show_sdk");
            TrackAdCustomEventBanner("wf_banner_request_finished_success");

            BannerOnAdDisplayed?.Invoke();
        }

        private void OnBannerAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo) {
            _log.Error("Banner ad failed to load for ad unit id : " + adUnitId + " with error code : " + errorInfo.Code + " and message : " + errorInfo.Message);

            // Track ad load failed event
            var extraPayload = new Dictionary<string, IConvertible>
            {
                { "error_code", errorInfo.Code },
                { "error_message", errorInfo.Message },
                { "mediator_error_code", errorInfo.MediatedNetworkErrorCode },
                { "mediator_error_message", errorInfo.MediatedNetworkErrorMessage }
            };

            if (errorInfo.LatencyMillis > _timeoutThreshold)
            {
                _log.Warning($"Banner ad request took too long: {errorInfo.LatencyMillis} ms, exceeding threshold of {_timeoutThreshold} ms.");

                TrackAdCustomEventBanner("wf_banner_request_adunit_timeout", extraPayload: extraPayload);
            }

            // Canonical ad_load_failed (banner load failure — there is no separate banner show callback)
            var error = IAAPayloadBuilder.FormatError(
                (int)errorInfo.Code, errorInfo.Message,
                errorInfo.MediatedNetworkErrorCode, errorInfo.MediatedNetworkErrorMessage);

            EmitCanonical(IAAEventNames.AdLoadFailed, IAAPayloadBuilder.BuildAdLoadFailed(
                adFormat:   AdFormatKey.Banner,
                adPlatform: AdNetworkName.AppLovin,
                adUnitName: adUnitId,
                error:      error
            ));

            TrackAdCustomEventBanner("wf_banner_request_adunit_failed", extraPayload: extraPayload);
            TrackAdCustomEventBanner("wf_banner_show_sdk_failed", extraPayload: extraPayload);

            BannerOnAdFailedDisplayed?.Invoke();
        }

        private void OnBannerAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            _log.Debug("Banner ad clicked for ad unit id : " + adUnitId);

            // Canonical ad_clicked
            EmitCanonical(IAAEventNames.AdClicked, IAAPayloadBuilder.BuildAdClicked(
                placement:  adInfo?.Placement,
                adType:     AdFormatKey.Banner,
                adUnitId:   adUnitId,
                adUnitName: adUnitId,
                adSize:     IAAAdSize.Banner320,
                adSource:   adInfo?.NetworkName,
                adPlatform: AdNetworkName.AppLovin
            ));

            TrackAdCustomEventBanner("wf_banner_clicked");

            BannerOnAdClicked?.Invoke();
        }

        private void OnBannerAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            _log.Debug("Banner ad revenue paid for ad unit id : " + adUnitId);

            // AppLovin MAX revenue values are reported in USD per AppLovin docs.
            var revenueUsd = adInfo?.Revenue ?? 0d;

            EmitCanonical(IAAEventNames.AdImpression, IAAPayloadBuilder.BuildAdImpression(
                placement:        adInfo?.Placement,
                adType:           AdFormatKey.Banner,
                adUnitId:         adUnitId,
                adUnitName:       adUnitId,
                value:            revenueUsd,
                valueUsd:         revenueUsd,
                adSize:           IAAAdSize.Banner320,
                adSource:         adInfo?.NetworkName,
                adPlatform:       AdNetworkName.AppLovin,
                engagementTimeMs: adInfo?.LatencyMillis ?? 0L
            ));

            // Keep legacy banner-specific impression marker for one release for dashboard back-compat.
            TrackAdCustomEventBanner("ad_impression_banner");

            BannerOnAdImpressionRecorded?.Invoke();
            BannerOnAdRevenuePaid?.Invoke(adInfo);
        }

        private void OnBannerAdExpandedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)  {
            _log.Debug("Banner ad expanded for ad unit id : " + adUnitId);

            EmitCanonical(IAAEventNames.AdExpanded, IAAPayloadBuilder.BuildAdLoaded(
                placement:  adInfo?.Placement,
                adType:     AdFormatKey.Banner,
                adUnitId:   adUnitId,
                adUnitName: adUnitId,
                adSize:     IAAAdSize.Banner320,
                adSource:   adInfo?.NetworkName,
                adPlatform: AdNetworkName.AppLovin
            ));
        }

        private void OnBannerAdCollapsedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            _log.Debug("Banner ad collapsed for ad unit id : " + adUnitId);

            EmitCanonical(IAAEventNames.AdCollapsed, IAAPayloadBuilder.BuildAdLoaded(
                placement:  adInfo?.Placement,
                adType:     AdFormatKey.Banner,
                adUnitId:   adUnitId,
                adUnitName: adUnitId,
                adSize:     IAAAdSize.Banner320,
                adSource:   adInfo?.NetworkName,
                adPlatform: AdNetworkName.AppLovin
            ));
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
                _log.Error($"Error emitting canonical banner event '{eventName}': {ex.Message}");
            }
        }
        
        private void TrackAdCustomEventBanner(string eventName, string adUnitId = null, MaxSdkBase.AdInfo adInfo = null, Dictionary<string, IConvertible> extraPayload = null)
        {
            try
            {
                _log.Debug("Tracking custom event for banner ad: " + eventName);

                // Copy so we never mutate the caller's dictionary — the same dict is often
                // passed to multiple sequential TrackAdCustomEventBanner calls.
                var payload = extraPayload != null
                    ? new Dictionary<string, IConvertible>(extraPayload)
                    : new Dictionary<string, IConvertible>();

                payload["ad_format"] = AdFormatKey.Banner;
                payload["mediation_service"] = AdNetworkName.AppLovin;
                payload["ad_unit_id"] = adUnitId ?? _adUnitIDBanner ?? "unknown";

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
                _log.Error($"Error tracking banner ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
                // Continue execution - tracking errors shouldn't affect ad functionality
            }
        }
    }
}
#endif