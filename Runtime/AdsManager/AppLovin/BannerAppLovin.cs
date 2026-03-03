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
            var adViewConfiguration = new MaxSdk.AdViewConfiguration(bannerPosition);
            MaxSdk.CreateBanner(_adUnitIDBanner, adViewConfiguration);
            MaxSdk.SetBannerBackgroundColor(_adUnitIDBanner, color);

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
            
            MaxSdk.ShowBanner(_adUnitIDBanner);

            _log.Debug("Banner ad shown for ad unit id : " + _adUnitIDBanner);
        }

        /// <summary>
        /// Hides the banner ad without destroying it, allowing it to be shown again later.
        /// </summary>
        public void HideBanner()
        {
            MaxSdk.HideBanner(_adUnitIDBanner);

            _log.Debug("Banner ad hidden for ad unit id : " + _adUnitIDBanner);

            TrackAdCustomEventBanner("wf_banner_closed");
        }

        /// <summary>
        /// Destroys the banner ad and frees its resources.
        /// Do not call this if multiple ad instances share the same ad unit ID.
        /// </summary>
        public void DestroyBanner()
        {
            MaxSdk.DestroyBanner(_adUnitIDBanner);

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

            // Track ad loaded event
            TrackAdCustomEventBanner("ad_loaded", adUnitId, adInfo);
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

            TrackAdCustomEventBanner("ad_show_failed", adUnitId, null, extraPayload);
            TrackAdCustomEventBanner("wf_banner_request_adunit_failed", extraPayload: extraPayload);
            TrackAdCustomEventBanner("wf_banner_show_sdk_failed", extraPayload: extraPayload);

            BannerOnAdFailedDisplayed?.Invoke();
        }

        private void OnBannerAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            _log.Debug("Banner ad clicked for ad unit id : " + adUnitId);

            // Track ad clicked event
            TrackAdCustomEventBanner("ad_clicked", adUnitId, adInfo);
            TrackAdCustomEventBanner("wf_banner_clicked");

            BannerOnAdClicked?.Invoke();
        }

        private void OnBannerAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            _log.Debug("Banner ad revenue paid for ad unit id : " + adUnitId);

            TrackAdCustomEventBanner("ad_impression");
            TrackAdCustomEventBanner("ad_impression_banner");

            BannerOnAdRevenuePaid?.Invoke(adInfo);
        }

        private void OnBannerAdExpandedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)  {
            _log.Debug("Banner ad expanded for ad unit id : " + adUnitId);
            
            // Track ad expanded event
            TrackAdCustomEventBanner("ad_expanded", adUnitId, adInfo);
        }

        private void OnBannerAdCollapsedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            _log.Debug("Banner ad collapsed for ad unit id : " + adUnitId);
            
            // Track ad collapsed event
            TrackAdCustomEventBanner("ad_collapsed", adUnitId, adInfo);
        }
        
        private void TrackAdCustomEventBanner(string eventName, string adUnitId = null, MaxSdkBase.AdInfo adInfo = null, Dictionary<string, IConvertible> extraPayload = null)
        {
            try
            {
                _log.Debug("Tracking custom event for banner ad: " + eventName);

                extraPayload ??= new Dictionary<string, IConvertible>();

                // Add basic information that doesn't require the ad info
                extraPayload.Add("ad_format", "banner");
                extraPayload.Add("mediation_service", "applovin");
                extraPayload.Add("ad_unit_id", adUnitId ?? _adUnitIDBanner ?? "unknown");

                // Add ad info if available
                if (adInfo != null)
                {
                    extraPayload.Add("ad_network", adInfo.NetworkName ?? "unknown");
                    extraPayload.Add("placement", adInfo.Placement ?? "unknown");
                    extraPayload.Add("network_placement", adInfo.NetworkPlacement ?? "unknown");
                    extraPayload.Add("ntw", adInfo.WaterfallInfo.Name ?? "unknown");
                    extraPayload.Add("latency_millis", adInfo.LatencyMillis);
                }
                else
                {
                    extraPayload.Add("ad_network", "unknown");
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
                _log.Error($"Error tracking banner ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
                // Continue execution - tracking errors shouldn't affect ad functionality
            }
        }
    }
}
#endif