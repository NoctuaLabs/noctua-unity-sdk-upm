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
        // Cached AdValue from OnAdPaid for canonical ad_impression payload.
        private AdValue _lastAdValue;

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

                var loadedAdapter = _bannerView.GetResponseInfo()?.GetLoadedAdapterResponseInfo();
                string adSource = null;
                try { adSource = loadedAdapter?.AdSourceName; } catch {}

                EmitCanonical(IAAEventNames.AdLoaded, IAAPayloadBuilder.BuildAdLoaded(
                    placement:  null,
                    adType:     AdFormatKey.Banner,
                    adUnitId:   _adUnitIdBanner,
                    adUnitName: _adUnitIdBanner,
                    adSize:     IAAAdSize.Banner320,
                    adSource:   adSource,
                    adPlatform: AdNetworkName.Admob
                ));

                // Banner has no native display callback in AdMob — emit ad_shown here so
                // analytics parity with AppLovin / Interstitial / Rewarded is maintained.
                EmitCanonical(IAAEventNames.AdShown, IAAPayloadBuilder.BuildAdLoaded(
                    placement:  null,
                    adType:     AdFormatKey.Banner,
                    adUnitId:   _adUnitIdBanner,
                    adUnitName: _adUnitIdBanner,
                    adSize:     IAAAdSize.Banner320,
                    adSource:   adSource,
                    adPlatform: AdNetworkName.Admob
                ));

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

                // Banner load-failure routes to ad_load_failed (this callback is the load-failure
                // path; there is no separate banner show callback). Keep emitting ad_show_failed
                // for one release for dashboard back-compat.
                EmitCanonical(IAAEventNames.AdLoadFailed, IAAPayloadBuilder.BuildAdLoadFailed(
                    adFormat:   AdFormatKey.Banner,
                    adPlatform: AdNetworkName.Admob,
                    adUnitName: _adUnitIdBanner,
                    error:      IAAPayloadBuilder.FormatError(
                        error.GetCode(), error.GetMessage(), error.GetDomain())
                ));

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

                _lastAdValue = adValue;
                AdmobOnAdRevenuePaid?.Invoke(adValue, _bannerView.GetResponseInfo());
            };
            // Raised when an impression is recorded for an ad.
            _bannerView.OnAdImpressionRecorded += () =>
            {
                _log.Debug("Banner view recorded an impression.");

                var valueMicros = _lastAdValue?.Value ?? 0L;
                var value       = valueMicros / 1_000_000d;
                var valueUsd    = value;

                var loadedAdapter = _bannerView?.GetResponseInfo()?.GetLoadedAdapterResponseInfo();
                string adSource = null;
                long latency = 0;
                try { adSource = loadedAdapter?.AdSourceName; } catch {}
                try { latency = loadedAdapter?.LatencyMillis ?? 0; } catch {}

                EmitCanonical(IAAEventNames.AdImpression, IAAPayloadBuilder.BuildAdImpression(
                    placement:        null,
                    adType:           AdFormatKey.Banner,
                    adUnitId:         _adUnitIdBanner,
                    adUnitName:       _adUnitIdBanner,
                    value:            value,
                    valueUsd:         valueUsd,
                    adSize:           IAAAdSize.Banner320,
                    adSource:         adSource,
                    adPlatform:       AdNetworkName.Admob,
                    engagementTimeMs: latency
                ));

                // Keep legacy banner-specific impression marker for one release.
                TrackAdCustomEventBanner("ad_impression_banner");

                BannerOnAdImpressionRecorded?.Invoke();
            };
            // Raised when a click is recorded for an ad.
            _bannerView.OnAdClicked += () =>
            {
                _log.Debug("Banner view was clicked.");

                var loadedAdapter = _bannerView?.GetResponseInfo()?.GetLoadedAdapterResponseInfo();
                string adSource = null;
                try { adSource = loadedAdapter?.AdSourceName; } catch {}

                EmitCanonical(IAAEventNames.AdClicked, IAAPayloadBuilder.BuildAdClicked(
                    placement:  null,
                    adType:     AdFormatKey.Banner,
                    adUnitId:   _adUnitIdBanner,
                    adUnitName: _adUnitIdBanner,
                    adSize:     IAAAdSize.Banner320,
                    adSource:   adSource,
                    adPlatform: AdNetworkName.Admob
                ));

                TrackAdCustomEventBanner("wf_banner_clicked");

                BannerOnAdClicked?.Invoke();
            };
            // Raised when an ad opened full screen content (banner expanded).
            _bannerView.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("Banner view full screen content opened.");

                EmitCanonical(IAAEventNames.AdExpanded, IAAPayloadBuilder.BuildAdLoaded(
                    placement:  null,
                    adType:     AdFormatKey.Banner,
                    adUnitId:   _adUnitIdBanner,
                    adUnitName: _adUnitIdBanner,
                    adSize:     IAAAdSize.Banner320,
                    adSource:   null,
                    adPlatform: AdNetworkName.Admob
                ));

                BannerOnAdDisplayed?.Invoke();
            };
            // Raised when the ad closed full screen content (banner collapsed).
            _bannerView.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("Banner view full screen content closed.");

                EmitCanonical(IAAEventNames.AdCollapsed, IAAPayloadBuilder.BuildAdLoaded(
                    placement:  null,
                    adType:     AdFormatKey.Banner,
                    adUnitId:   _adUnitIdBanner,
                    adUnitName: _adUnitIdBanner,
                    adSize:     IAAAdSize.Banner320,
                    adSource:   null,
                    adPlatform: AdNetworkName.Admob
                ));

                TrackAdCustomEventBanner("wf_banner_closed");

                BannerOnAdClosed?.Invoke();
            };
        }

        /// <summary>
        /// Hide the banner without destroying it. Emits <c>wf_banner_hidden</c> for parity
        /// with the AppLovin banner lifecycle.
        /// </summary>
        public void HideBanner()
        {
            if (_bannerView == null) return;
            _bannerView.Hide();
            _log.Debug("Banner ad hidden for ad unit id : " + _adUnitIdBanner);
            TrackAdCustomEventBanner("wf_banner_hidden");
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

                // Copy so we never mutate the caller's dictionary — the same dict is often
                // passed to multiple sequential TrackAdCustomEventBanner calls.
                var payload = extraPayload != null
                    ? new Dictionary<string, IConvertible>(extraPayload)
                    : new Dictionary<string, IConvertible>();

                payload["ad_format"] = AdFormatKey.Banner;
                payload["mediation_service"] = AdNetworkName.Admob;

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
                }
                else
                {
                    payload["ad_network"] = "unknown";
                    payload["ad_unit_id"] = _adUnitIdBanner ?? "unknown";
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
                _log.Error($"Error emitting canonical banner event '{eventName}': {ex.Message}");
            }
        }
    }
}

#endif