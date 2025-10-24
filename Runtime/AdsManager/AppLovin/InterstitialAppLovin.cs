#if UNITY_APPLOVIN
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk.AppLovin
{
    public class InterstitialAppLovin
    {
        private readonly NoctuaLogger _log = new(typeof(InterstitialAppLovin));

        private string _adUnitIDInterstitial;

        int retryAttempt;

        public event Action InterstitialOnAdDisplayed;
        public event Action InterstitialOnAdFailedDisplayed;
        public event Action InterstitialOnAdClicked;
        public event Action InterstitialOnAdClosed;
        public event Action<MaxSdkBase.AdInfo> InterstitialOnAdRevenuePaid;
        private readonly long _timeoutThreshold = 5000; // 5 seconds

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

        public void LoadInterstitial()
        {
            if (_adUnitIDInterstitial == null)
            {
                _log.Error("Ad unit ID Interstitial is empty.");
                return;
            }

            // Attach callback
            MaxSdkCallbacks.Interstitial.OnAdLoadedEvent += OnInterstitialLoadedEvent;
            MaxSdkCallbacks.Interstitial.OnAdLoadFailedEvent += OnInterstitialLoadFailedEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayedEvent += OnInterstitialDisplayedEvent;
            MaxSdkCallbacks.Interstitial.OnAdClickedEvent += OnInterstitialClickedEvent;
            MaxSdkCallbacks.Interstitial.OnAdHiddenEvent += OnInterstitialHiddenEvent;
            MaxSdkCallbacks.Interstitial.OnAdDisplayFailedEvent += OnInterstitialAdFailedToDisplayEvent;
            MaxSdkCallbacks.Interstitial.OnAdRevenuePaidEvent += OnAdRevenuePaidEvent;

            // Load the first interstitial
            LoadInterstitialInternal();

            _log.Debug("Interstitial ad loaded for ad unit id : " + _adUnitIDInterstitial);
        }

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
            
            // Track ad loaded event
            TrackAdCustomEventInterstitial("ad_loaded", adUnitId, adInfo);
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

                TrackAdCustomEventInterstitial("wf_interstitial_request_adunit_timeout", extraPayload: extraPayload);
            }

            TrackAdCustomEventInterstitial("ad_load_failed", adUnitId, null, extraPayload);
            TrackAdCustomEventInterstitial("wf_interstitial_request_adunit_failed", extraPayload: extraPayload);
            TrackAdCustomEventInterstitial("wf_interstitial_request_finished_failed	", extraPayload: extraPayload);
        }

        // Async method handling the delay
        private async UniTaskVoid RetryLoadInterstitialAsync()
        {
            retryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, retryAttempt));

            await UniTask.Delay((int)(retryDelay * 1000));
            LoadInterstitialInternal();

            _log.Debug("Retrying to load interstitial ad after " + retryDelay + " seconds");
        }

        private void OnInterstitialDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {
            // Interstitial ad displayed

            _log.Debug("Interstitial ad displayed for ad unit id : " + adUnitId);

            // Track ad shown event
            TrackAdCustomEventInterstitial("ad_shown", adUnitId, adInfo);
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
            
            TrackAdCustomEventInterstitial("ad_shown_failed", adUnitId, adInfo, extraPayload);
            TrackAdCustomEventInterstitial("wf_interstitial_show_sdk_failed", extraPayload: extraPayload);

            InterstitialOnAdFailedDisplayed?.Invoke();
        }

        private void OnInterstitialClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo) {

            _log.Debug("Interstitial ad clicked for ad unit id : " + adUnitId);

            // Track ad clicked event
            TrackAdCustomEventInterstitial("ad_clicked", adUnitId, adInfo);
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

            TrackAdCustomEventInterstitial("ad_impression");
            TrackAdCustomEventInterstitial("ad_impression_interstitial");

            InterstitialOnAdRevenuePaid?.Invoke(adInfo);
        }

        private void TrackAdCustomEventInterstitial(string eventName, string adUnitId = null, MaxSdkBase.AdInfo adInfo = null, Dictionary<string, IConvertible> extraPayload = null)
        {
            try
            {
                _log.Debug("Tracking custom event for interstitial ad: " + eventName);

                extraPayload ??= new Dictionary<string, IConvertible>();

                // Add basic information that doesn't require the ad info
                extraPayload.Add("ad_format", "interstitial");
                extraPayload.Add("mediation_service", "applovin");
                extraPayload.Add("ad_unit_id", adUnitId ?? _adUnitIDInterstitial ?? "unknown");

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
                _log.Error($"Error tracking interstitial ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
#endif