#if UNITY_APPLOVIN
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk.AppLovin
{
    public class BannerAppLovin
    {
        private readonly NoctuaLogger _log = new(typeof(BannerAppLovin));

        private string _adUnitIDBanner;

        public event Action BannerOnAdDisplayed;
        public event Action BannerOnAdFailedDisplayed;
        public event Action BannerOnAdClicked;
        public event Action BannerOnAdImpressionRecorded;
        public event Action BannerOnAdClosed;
        public event Action<MaxSdkBase.AdInfo> BannerOnAdRevenuePaid;
        private readonly long _timeoutThreshold = 5000; // 5 seconds

        public void SetBannerAdUnitId(string adUnitIDBanner)
        {
            if (adUnitIDBanner == null)
            {
                _log.Error("Ad unit ID banner is empty.");
                return;
            }

            _adUnitIDBanner = adUnitIDBanner;

            _log.Debug("Banner ad loaded for ad unit id : " + adUnitIDBanner);
        }

        public void InitializeBannerAds(Color color, MaxSdkBase.BannerPosition bannerPosition)
        {
            TrackAdCustomEventBanner("wf_banner_request_start");
            // Banners are automatically sized to 320×50 on phones and 728×90 on tablets
            // You may call the utility method MaxSdkUtils.isTablet() to help with view sizing adjustments
            MaxSdk.CreateBanner(_adUnitIDBanner, bannerPosition);

            // Set background color for banners to be fully functional
            MaxSdk.SetBannerBackgroundColor(_adUnitIDBanner, color);

            MaxSdkCallbacks.Banner.OnAdLoadedEvent      += OnBannerAdLoadedEvent;
            MaxSdkCallbacks.Banner.OnAdLoadFailedEvent  += OnBannerAdLoadFailedEvent;
            MaxSdkCallbacks.Banner.OnAdClickedEvent     += OnBannerAdClickedEvent;
            MaxSdkCallbacks.Banner.OnAdRevenuePaidEvent += OnBannerAdRevenuePaidEvent;
            MaxSdkCallbacks.Banner.OnAdExpandedEvent    += OnBannerAdExpandedEvent;
            MaxSdkCallbacks.Banner.OnAdCollapsedEvent   += OnBannerAdCollapsedEvent;

            _log.Debug("Banner ad initialized for ad unit id : " + _adUnitIDBanner);
            TrackAdCustomEventBanner("wf_banner_started_playing");
        }

        public void ShowBanner()
        {
            MaxSdk.ShowBanner(_adUnitIDBanner);

            _log.Debug("Banner ad shown for ad unit id : " + _adUnitIDBanner);
        }

        public void HideBanner()
        {
            MaxSdk.HideBanner(_adUnitIDBanner);

            _log.Debug("Banner ad hidden for ad unit id : " + _adUnitIDBanner);

            TrackAdCustomEventBanner("wf_banner_closed");
        }

        //Destroying Banners
        // You may no longer need an ad instance (for example, if the user purchased ad removal). 
        // If so, call the DestroyBanner() method to free resources. 
        // Do not call DestroyBanner() if you use multiple ad instances with the same Ad Unit ID.
        public void DestroyBanner()
        {
            MaxSdk.DestroyBanner(_adUnitIDBanner);

            _log.Debug("Banner ad destroyed for ad unit id : " + _adUnitIDBanner);

            TrackAdCustomEventBanner("wf_banner_closed");
        }

        //Setting Banner Width
        // To manually set the banner’s width, call SetBannerWidth(). Set the width to a size larger than the minimum value (320 on phones, 728 on tablets).
        //  Banners under this width may not be considered viewable by the advertiser, which will affect your revenue:
        public void SetBannerWidth(int width)
        {
            MaxSdk.SetBannerWidth(_adUnitIDBanner, width);

            _log.Debug("Banner ad width set to : " + width + " for ad unit id : " + _adUnitIDBanner);
        }

        // Getting Banner Position
        // To get the banner’s position and size, call GetBannerLayout(). This uses the same Unity coordinate system as explained in Loading a Banner.
        public Rect GetBannerPosition()
        {
            _log.Debug("Getting banner position for ad unit id : " + _adUnitIDBanner);

            return MaxSdk.GetBannerLayout(_adUnitIDBanner);
        }
        // Stopping and Starting Auto-Refresh
        // You may want to stop auto-refresh for an ad, for instance if you want to manually refresh banner ads. 
        // To stop auto-refresh for a banner, use the following code:
        public void StopBannerAutoRefresh()
        {
            _log.Debug("Stopping banner auto refresh for ad unit id : " + _adUnitIDBanner);

            MaxSdk.StopBannerAutoRefresh(_adUnitIDBanner);
        }

        public void StartBannerAutoRefresh()
        {
            _log.Debug("Starting banner auto refresh for ad unit id : " + _adUnitIDBanner);
            
            MaxSdk.StartBannerAutoRefresh(_adUnitIDBanner);
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
                _log.Warning($"Interstitial ad request took too long: {errorInfo.LatencyMillis} ms, exceeding threshold of {_timeoutThreshold} ms.");

                TrackAdCustomEventBanner("wf_banner_request_adunit_timeout", extraPayload: extraPayload);
            }

            TrackAdCustomEventBanner("ad_show_failed", adUnitId, null, extraPayload);
            TrackAdCustomEventBanner("wf_banner_request_adunit_failed", extraPayload: extraPayload);
            TrackAdCustomEventBanner("wf_banner_show_sdk_failed	", extraPayload: extraPayload);

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