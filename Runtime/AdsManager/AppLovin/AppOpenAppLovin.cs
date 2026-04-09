#if UNITY_APPLOVIN
using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk.AppLovin
{
    /// <summary>
    /// Manages AppLovin MAX App Open ad loading, display, and lifecycle events.
    /// Handles full-screen app open ads with exponential backoff retry on load failure.
    /// </summary>
    public class AppOpenAppLovin
    {
        private readonly NoctuaLogger _log = new(typeof(AppOpenAppLovin));

        private string _adUnitIDAppOpen;
        private int _retryAttempt;
        private bool _callbacksRegistered;

        /// <summary>Raised when the app open ad is successfully displayed.</summary>
        public event Action AppOpenOnAdDisplayed;

        /// <summary>Raised when the app open ad fails to display.</summary>
        public event Action AppOpenOnAdFailedDisplayed;

        /// <summary>Raised when the user clicks on the app open ad.</summary>
        public event Action AppOpenOnAdClicked;

        /// <summary>Raised when an app open ad impression is recorded via revenue paid callback.</summary>
        public event Action AppOpenOnAdImpressionRecorded;

        /// <summary>Raised when the app open ad is closed (hidden) by the user.</summary>
        public event Action AppOpenOnAdClosed;

        /// <summary>Raised when app open ad revenue is recorded, providing the ad info with revenue data.</summary>
        public event Action<MaxSdkBase.AdInfo> AppOpenOnAdRevenuePaid;

        /// <summary>
        /// Sets the ad unit ID for the app open ad.
        /// </summary>
        /// <param name="adUnitID">The AppLovin MAX ad unit ID for app open ads.</param>
        public void SetAppOpenAdUnitID(string adUnitID)
        {
            if (adUnitID == null)
            {
                _log.Error("Ad unit ID App Open is empty.");
                return;
            }

            _adUnitIDAppOpen = adUnitID;
            _log.Debug("Ad unit ID App Open set to: " + _adUnitIDAppOpen);
        }

        /// <summary>
        /// Loads an app open ad and registers event callbacks (only once) for the ad lifecycle.
        /// </summary>
        public void LoadAppOpenAd()
        {
            if (_adUnitIDAppOpen == null)
            {
                _log.Error("Ad unit ID App Open is empty.");
                return;
            }

            if (!_callbacksRegistered)
            {
                _callbacksRegistered = true;
                MaxSdkCallbacks.AppOpen.OnAdLoadedEvent += OnAdLoadedEvent;
                MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent += OnAdLoadFailedEvent;
                MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent += OnAdDisplayedEvent;
                MaxSdkCallbacks.AppOpen.OnAdClickedEvent += OnAdClickedEvent;
                MaxSdkCallbacks.AppOpen.OnAdHiddenEvent += OnAdHiddenEvent;
                MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent += OnAdDisplayFailedEvent;
                MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent += OnAdRevenuePaidEvent;
            }

            LoadAppOpenAdInternal();

            _log.Debug("App open ad loading for ad unit id: " + _adUnitIDAppOpen);
        }

        /// <summary>
        /// Shows a previously loaded app open ad if it is ready.
        /// </summary>
        public void ShowAppOpenAd()
        {
            if (string.IsNullOrEmpty(_adUnitIDAppOpen))
            {
                _log.Info("Ad unit ID App Open is empty.");
                return;
            }

            TrackAdCustomEvent("wf_app_open_started_playing");

            if (MaxSdk.IsAppOpenAdReady(_adUnitIDAppOpen))
            {
                MaxSdk.ShowAppOpenAd(_adUnitIDAppOpen);
                _log.Debug("Showing app open ad for ad unit id: " + _adUnitIDAppOpen);
            }
            else
            {
                _log.Error("App open ad is not ready to be shown for ad unit id: " + _adUnitIDAppOpen);
                TrackAdCustomEvent("wf_app_open_show_not_ready");
            }
        }

        /// <summary>
        /// Returns whether an app open ad is loaded and ready to show.
        /// </summary>
        public bool IsAdReady()
        {
            return !string.IsNullOrEmpty(_adUnitIDAppOpen) && MaxSdk.IsAppOpenAdReady(_adUnitIDAppOpen);
        }

        /// <summary>
        /// Removes all registered callbacks from the static MaxSdkCallbacks events.
        /// Must be called when this instance is being replaced or discarded to prevent
        /// duplicate callbacks if a new AppOpenAppLovin instance is created.
        /// </summary>
        public void UnregisterCallbacks()
        {
            if (!_callbacksRegistered) return;

            MaxSdkCallbacks.AppOpen.OnAdLoadedEvent -= OnAdLoadedEvent;
            MaxSdkCallbacks.AppOpen.OnAdLoadFailedEvent -= OnAdLoadFailedEvent;
            MaxSdkCallbacks.AppOpen.OnAdDisplayedEvent -= OnAdDisplayedEvent;
            MaxSdkCallbacks.AppOpen.OnAdClickedEvent -= OnAdClickedEvent;
            MaxSdkCallbacks.AppOpen.OnAdHiddenEvent -= OnAdHiddenEvent;
            MaxSdkCallbacks.AppOpen.OnAdDisplayFailedEvent -= OnAdDisplayFailedEvent;
            MaxSdkCallbacks.AppOpen.OnAdRevenuePaidEvent -= OnAdRevenuePaidEvent;

            _callbacksRegistered = false;
            _log.Debug("AppOpen callbacks unregistered.");
        }

        private void LoadAppOpenAdInternal()
        {
            TrackAdCustomEvent("wf_app_open_request_start");
            MaxSdk.LoadAppOpenAd(_adUnitIDAppOpen);
            _log.Debug("Loading app open ad for ad unit id: " + _adUnitIDAppOpen);
        }

        private void OnAdLoadedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            _retryAttempt = 0;
            _log.Debug("App open ad loaded for ad unit id: " + adUnitId);
            TrackAdCustomEvent("ad_loaded", adUnitId, adInfo);
            TrackAdCustomEvent("wf_app_open_adunit_success");
        }

        private void OnAdLoadFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo)
        {
            _log.Debug("App open ad failed to load for ad unit id: " + adUnitId +
                " with error code: " + errorInfo.Code);

            var extraPayload = new Dictionary<string, IConvertible>
            {
                { "error_code", errorInfo.Code },
                { "error_message", errorInfo.Message },
                { "mediator_error_code", errorInfo.MediatedNetworkErrorCode },
                { "mediator_error_message", errorInfo.MediatedNetworkErrorMessage },
                { "latency_millis", errorInfo.LatencyMillis }
            };

            TrackAdCustomEvent("ad_load_failed", adUnitId, null, extraPayload);
            TrackAdCustomEvent("wf_app_open_request_adunit_failed", extraPayload: extraPayload);

            RetryLoadAsync().Forget();
        }

        private async UniTaskVoid RetryLoadAsync()
        {
            _retryAttempt++;
            double retryDelay = Math.Pow(2, Math.Min(6, _retryAttempt));

            await UniTask.Delay((int)(retryDelay * 1000));
            LoadAppOpenAdInternal();

            _log.Debug("Retrying to load app open ad after " + retryDelay + " seconds");
        }

        private void OnAdDisplayedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            _log.Debug("App open ad displayed for ad unit id: " + adUnitId);
            TrackAdCustomEvent("ad_shown", adUnitId, adInfo);
            TrackAdCustomEvent("wf_app_open_show_sdk");
            AppOpenOnAdDisplayed?.Invoke();
        }

        private void OnAdDisplayFailedEvent(string adUnitId, MaxSdkBase.ErrorInfo errorInfo, MaxSdkBase.AdInfo adInfo)
        {
            LoadAppOpenAdInternal();

            _log.Debug("App open ad failed to display for ad unit id: " + adUnitId +
                " with error code: " + errorInfo.Code);

            var extraPayload = new Dictionary<string, IConvertible>
            {
                { "error_code", errorInfo.Code },
                { "error_message", errorInfo.Message },
                { "mediator_error_code", errorInfo.MediatedNetworkErrorCode },
                { "mediator_error_message", errorInfo.MediatedNetworkErrorMessage },
                { "latency_millis", errorInfo.LatencyMillis }
            };

            TrackAdCustomEvent("ad_shown_failed", adUnitId, adInfo, extraPayload);
            TrackAdCustomEvent("wf_app_open_show_sdk_failed", extraPayload: extraPayload);
            AppOpenOnAdFailedDisplayed?.Invoke();
        }

        private void OnAdClickedEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            _log.Debug("App open ad clicked for ad unit id: " + adUnitId);
            TrackAdCustomEvent("ad_clicked", adUnitId, adInfo);
            TrackAdCustomEvent("wf_app_open_clicked");
            AppOpenOnAdClicked?.Invoke();
        }

        private void OnAdHiddenEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            LoadAppOpenAdInternal();

            _log.Debug("App open ad hidden for ad unit id: " + adUnitId);
            TrackAdCustomEvent("ad_closed", adUnitId, adInfo);
            TrackAdCustomEvent("wf_app_open_closed");
            AppOpenOnAdClosed?.Invoke();
        }

        private void OnAdRevenuePaidEvent(string adUnitId, MaxSdkBase.AdInfo adInfo)
        {
            _log.Debug("App open ad revenue paid for ad unit id: " + adUnitId +
                " with revenue: " + adInfo.Revenue);

            TrackAdCustomEvent("ad_impression");
            TrackAdCustomEvent("ad_impression_app_open");
            AppOpenOnAdImpressionRecorded?.Invoke();
            AppOpenOnAdRevenuePaid?.Invoke(adInfo);
        }

        private void TrackAdCustomEvent(string eventName, string adUnitId = null,
            MaxSdkBase.AdInfo adInfo = null, Dictionary<string, IConvertible> extraPayload = null)
        {
            try
            {
                _log.Debug("Tracking custom event for app open ad: " + eventName);

                // Copy so we never mutate the caller's dictionary — the same dict is often
                // passed to multiple sequential TrackAdCustomEvent calls.
                var payload = extraPayload != null
                    ? new Dictionary<string, IConvertible>(extraPayload)
                    : new Dictionary<string, IConvertible>();

                payload["ad_format"] = AdFormatKey.AppOpen;
                payload["mediation_service"] = AdNetworkName.AppLovin;
                payload["ad_unit_id"] = adUnitId ?? _adUnitIDAppOpen ?? "unknown";

                if (adInfo != null)
                {
                    payload["ad_network"] = adInfo.NetworkName ?? "unknown";
                    payload["placement"] = adInfo.Placement ?? "unknown";
                    payload["network_placement"] = adInfo.NetworkPlacement ?? "unknown";
                    payload["ntw"] = adInfo.WaterfallInfo?.Name ?? "unknown";
                    payload["latency_millis"] = adInfo.LatencyMillis;
                }
                else
                {
                    payload["ad_network"] = "unknown";
                }

                Noctua.Event.TrackCustomEvent(eventName, payload);
            }
            catch (Exception ex)
            {
                _log.Error($"Error tracking app open ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
#endif
