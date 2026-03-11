#if UNITY_ADMOB
using GoogleMobileAds.Api;
using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.Admob
{
    /// <summary>
    /// Manages AdMob banner ad loading, display, and lifecycle events.
    /// Handles creating banner views, loading ads, and tracking analytics events.
    /// </summary>
    public class BannerAdmob
    {
        private readonly NoctuaLogger _log = new(typeof(BannerAdmob));
        private BannerView _bannerView;
        private AdSize _adSize;
        private AdPosition _adPosition;

        private string _adUnitIdBanner;

        /// <summary>Raised when a banner ad is successfully loaded and displayed.</summary>
        public event Action BannerOnAdDisplayed;

        /// <summary>Raised when a banner ad fails to load or display.</summary>
        public event Action BannerOnAdFailedDisplayed;

        /// <summary>Raised when the user clicks on the banner ad.</summary>
        public event Action BannerOnAdClicked;

        /// <summary>Raised when a banner ad impression is recorded.</summary>
        public event Action BannerOnAdImpressionRecorded;

        /// <summary>Raised when the banner ad's full-screen content is closed.</summary>
        public event Action BannerOnAdClosed;

        /// <summary>Raised when banner ad revenue is recorded, providing the ad value and response info.</summary>
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid;
        private readonly long _timeoutThreshold = 5000; // milliseconds,
        private bool _bannerEventsRegistered;

        /// <summary>
        /// Sets the ad unit ID for the banner ad.
        /// </summary>
        /// <param name="adUnitId">The AdMob ad unit ID for banner ads.</param>
        public void SetAdUnitId(string adUnitId)
        {
            if (adUnitId == null)
            {
                _log.Error("Ad unit ID Banner is empty.");
                return;
            }

            _adUnitIdBanner = adUnitId;

            _log.Debug("Ad unit ID Banner set to : " + _adUnitIdBanner);
        }

        /// <summary>
        /// Creates a 320x50 banner view at top of the screen.
        /// </summary>
        public void CreateBannerView(AdSize adSize, AdPosition adPosition)
        {
            if(_adUnitIdBanner == null)
            {
                _log.Error("Ad unit ID Banner is empty.");
                return;
            }

            if (adSize == null)
            {
                _log.Info("Ad size is null.");

                adSize = AdSize.Banner; // Default to Banner size if not provided

                _log.Debug("Defaulting to Banner ad size.");
            }

            _adSize = adSize;
            _adPosition = adPosition;

            _log.Debug("Creating banner view");

            // If we already have a banner, destroy the old one.
            CleanupAd();

            // Create a 320x50 banner at top of the screen
            _bannerView = new BannerView(_adUnitIdBanner, adSize, adPosition);
        }

        /// <summary>
        /// Creates the banner view and loads a banner ad.
        /// </summary>
        public void LoadAd()
        {
            if (string.IsNullOrEmpty(_adUnitIdBanner) || _adUnitIdBanner == "unknown")
            {
                _log.Error("Ad unit ID Banner is not configured.");
                return;
            }

            TrackAdCustomEventBanner("wf_banner_request_start");

            // create an instance of a banner view first.
            if(_bannerView == null)
            {
                CreateBannerView(adSize: _adSize, adPosition: _adPosition);
            }

            // create our request used to load the ad.
            var adRequest = new AdRequest();

            // send the request to load the ad.
            _log.Debug("Loading banner ad.");
            TrackAdCustomEventBanner("wf_banner_started_playing");

            _bannerView.LoadAd(adRequest);

            ListenToAdEvents();
        }

        /// <summary>
        /// listen to events the banner view may raise.
        /// </summary>
        private void ListenToAdEvents()
        {
            if (_bannerEventsRegistered) return;
            _bannerEventsRegistered = true;

            // Raised when an ad is loaded into the banner view.
            _bannerView.OnBannerAdLoaded += () =>
            {
                _log.Debug("Banner view loaded an ad with response : "
                    + _bannerView.GetResponseInfo());

                TrackAdCustomEventBanner("ad_loaded");
                TrackAdCustomEventBanner("wf_banner_request_adunit_success");
                TrackAdCustomEventBanner("wf_banner_show_sdk");
                TrackAdCustomEventBanner("wf_banner_request_finished_success");
                
                BannerOnAdDisplayed?.Invoke();
            };
            // Raised when an ad fails to load into the banner view.
            _bannerView.OnBannerAdLoadFailed += (LoadAdError error) =>
            {
                _log.Error("Banner view failed to load an ad with error : "
                    + error);

                var responseInfo = error.GetResponseInfo();
                if (responseInfo != null)
                {
                    _log.Warning($"Response ID: {responseInfo.GetResponseId()}");
                    _log.Warning($"Mediation adapter: {responseInfo.GetMediationAdapterClassName()}");
                }

                var extraPayload = new Dictionary<string, IConvertible>
                {
                    { "error_code", error.GetCode() },
                    { "error_message", error.GetMessage() },
                    { "domain", error.GetDomain() }
                };

                TrackAdCustomEventBanner("ad_show_failed", extraPayload);
                TrackAdCustomEventBanner("wf_banner_request_adunit_failed", extraPayload);
                TrackAdCustomEventBanner("wf_banner_show_sdk_failed", extraPayload);

                if (_bannerView.GetResponseInfo() != null)
                {
                    AdapterResponseInfo loadedAdapterResponseInfo = _bannerView.GetResponseInfo().GetLoadedAdapterResponseInfo();

                    long latencyMillis = loadedAdapterResponseInfo?.LatencyMillis ?? 0;

                    if (latencyMillis > _timeoutThreshold)
                    {
                        _log.Warning($"Banner ad request took too long: {latencyMillis} ms, exceeding threshold of {_timeoutThreshold} ms.");

                        TrackAdCustomEventBanner("wf_banner_request_adunit_timeout");
                    }
                }
                
                BannerOnAdFailedDisplayed?.Invoke();
            };
            // Raised when the ad is estimated to have earned money.
            _bannerView.OnAdPaid += (AdValue adValue) =>
            {
                _log.Debug(String.Format("Banner view paid {0} {1}.",
                    adValue.Value,
                    adValue.CurrencyCode));
                
                AdmobOnAdRevenuePaid?.Invoke(adValue, _bannerView.GetResponseInfo());
            };
            // Raised when an impression is recorded for an ad.
            _bannerView.OnAdImpressionRecorded += () =>
            {
                _log.Debug("Banner view recorded an impression.");

                TrackAdCustomEventBanner("ad_impression");
                TrackAdCustomEventBanner("ad_impression_banner");

                BannerOnAdImpressionRecorded?.Invoke();
            };
            // Raised when a click is recorded for an ad.
            _bannerView.OnAdClicked += () =>
            {
                _log.Debug("Banner view was clicked.");

                TrackAdCustomEventBanner("ad_clicked");
                TrackAdCustomEventBanner("wf_banner_clicked");

                BannerOnAdClicked?.Invoke();
            };
            // Raised when an ad opened full screen content.
            _bannerView.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("Banner view full screen content opened.");

                TrackAdCustomEventBanner("ad_shown");

                BannerOnAdDisplayed?.Invoke();
            };
            // Raised when the ad closed full screen content.
            _bannerView.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("Banner view full screen content closed.");

                TrackAdCustomEventBanner("ad_closed");
                TrackAdCustomEventBanner("wf_banner_closed");

                BannerOnAdClosed?.Invoke();
            };
        }

        /// <summary>
        /// Destroys the current banner view and releases its resources.
        /// </summary>
        public void CleanupAd()
        {
            if (_bannerView != null)
            {
                _bannerView.Destroy();
                _bannerView = null;
                _bannerEventsRegistered = false;

                _log.Debug("Banner view cleaned up.");
            }
        }

        private void TrackAdCustomEventBanner(string eventName, Dictionary<string, IConvertible> extraPayload = null)
        {
            try
            {
                _log.Debug("Tracking custom event for banner ad: " + eventName);

                extraPayload ??= new Dictionary<string, IConvertible>();

                // Add basic information that doesn't require the ad instance
                extraPayload.Add("ad_format", "banner");
                extraPayload.Add("mediation_service", "admob");
                
                // Only add ad-specific information if the ad instance exists
                if (_bannerView != null)
                {
                    var responseInfo = _bannerView.GetResponseInfo();
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
                }
                else
                {
                    extraPayload.Add("ad_network", "unknown");
                    extraPayload.Add("ad_unit_id", _adUnitIdBanner ?? "unknown");
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
            }
        }
    }
}

#endif