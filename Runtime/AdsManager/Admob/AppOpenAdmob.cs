#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk.Admob
{
    /// <summary>
    /// Manages AdMob App Open ad display via the Preload API.
    /// Loading is handled entirely by <see cref="AdmobAdPreloadManager"/>; this class only
    /// receives the ad unit ID, checks availability, and orchestrates show + event forwarding.
    /// The 4-hour expiry check is NOT needed here — the Preload API manages ad freshness.
    /// </summary>
    public class AppOpenAdmob
    {
        private readonly NoctuaLogger _log = new(typeof(AppOpenAdmob));
        private string _adUnitIDAppOpen;

        /// <summary>Raised when the app open ad is successfully displayed.</summary>
        public event Action AppOpenOnAdDisplayed;

        /// <summary>Raised when the app open ad fails to display.</summary>
        public event Action AppOpenOnAdFailedDisplayed;

        /// <summary>Raised when the user clicks on the app open ad.</summary>
        public event Action AppOpenOnAdClicked;

        /// <summary>Raised when an app open ad impression is recorded.</summary>
        public event Action AppOpenOnAdImpressionRecorded;

        /// <summary>Raised when the app open ad is closed by the user.</summary>
        public event Action AppOpenOnAdClosed;

        /// <summary>Raised when app open ad revenue is recorded, providing the ad value and response info.</summary>
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid;

        /// <summary>
        /// Sets the ad unit ID for the app open ad.
        /// </summary>
        /// <param name="adUnitID">The AdMob ad unit ID for app open ads.</param>
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
        /// No-op — the Preload API manages loading automatically.
        /// Loading is initiated by <see cref="MediationManager"/> via <see cref="AdmobAdPreloadManager.StartPreloading"/>.
        /// </summary>
        public void LoadAppOpenAd()
        {
            _log.Debug("LoadAppOpenAd: preload API manages loading automatically. No-op.");
        }

        /// <summary>
        /// Shows the app open ad by polling a preloaded instance from <see cref="AdmobAdPreloadManager"/>.
        /// </summary>
        public void ShowAppOpenAd()
        {
            if (string.IsNullOrEmpty(_adUnitIDAppOpen) || _adUnitIDAppOpen == "unknown")
            {
                _log.Warning("App open ad unit ID not configured.");
                TrackAdCustomEvent("wf_app_open_show_not_ready");
                return;
            }

            var preload = AdmobAdPreloadManager.Instance;

            if (!preload.IsAdAvailable(_adUnitIDAppOpen, AdFormat.APP_OPEN_AD))
            {
                _log.Warning("App open ad is not ready yet (preload buffer empty).");
                TrackAdCustomEvent("wf_app_open_show_not_ready");
                return;
            }

            var ad = preload.PollAppOpenAd(_adUnitIDAppOpen);
            if (ad == null)
            {
                _log.Warning("App open ad poll returned null.");
                TrackAdCustomEvent("wf_app_open_show_not_ready");
                return;
            }

            try
            {
                _log.Debug("Showing app open ad.");
                TrackAdCustomEvent("wf_app_open_started_playing");
                RegisterEventHandlers(ad);
                ad.Show();
            }
            catch (Exception ex)
            {
                _log.Error($"Exception showing app open ad: {ex.Message}\n{ex.StackTrace}");
                AppOpenOnAdFailedDisplayed?.Invoke();
            }
        }

        /// <summary>
        /// Returns whether a preloaded app open ad is available.
        /// </summary>
        public bool IsAdReady()
        {
            if (string.IsNullOrEmpty(_adUnitIDAppOpen) || _adUnitIDAppOpen == "unknown")
                return false;

            return AdmobAdPreloadManager.Instance.IsAdAvailable(_adUnitIDAppOpen, AdFormat.APP_OPEN_AD);
        }

        private void RegisterEventHandlers(AppOpenAd ad)
        {
            ad.OnAdPaid += (AdValue adValue) =>
            {
                _log.Debug($"App open ad paid {adValue.Value} {adValue.CurrencyCode}.");
                AdmobOnAdRevenuePaid?.Invoke(adValue, ad.GetResponseInfo());
            };

            ad.OnAdImpressionRecorded += () =>
            {
                _log.Debug("App open ad recorded an impression.");
                TrackAdCustomEvent("ad_impression");
                TrackAdCustomEvent("ad_impression_app_open");
                AppOpenOnAdImpressionRecorded?.Invoke();
            };

            ad.OnAdClicked += () =>
            {
                _log.Debug("App open ad was clicked.");
                TrackAdCustomEvent("ad_clicked");
                TrackAdCustomEvent("wf_app_open_clicked");
                AppOpenOnAdClicked?.Invoke();
            };

            ad.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("App open ad full screen content opened.");
                TrackAdCustomEvent("ad_shown");
                TrackAdCustomEvent("wf_app_open_show_sdk");
                AppOpenOnAdDisplayed?.Invoke();
            };

            ad.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("App open ad full screen content closed.");
                TrackAdCustomEvent("ad_closed");
                TrackAdCustomEvent("wf_app_open_closed");
                AppOpenOnAdClosed?.Invoke();
                // No manual reload — preload API auto-refills the buffer after Poll.
            };

            ad.OnAdFullScreenContentFailed += (AdError error) =>
            {
                _log.Error("App open ad failed to open full screen content with error: " + error);

                var extraPayload = new Dictionary<string, IConvertible>
                {
                    { "error_code", error.GetCode() },
                    { "error_message", error.GetMessage() },
                    { "domain", error.GetDomain() },
                    { "ad_unit_id", _adUnitIDAppOpen ?? "unknown" }
                };

                TrackAdCustomEvent("ad_show_failed", extraPayload);
                TrackAdCustomEvent("wf_app_open_show_sdk_failed", extraPayload);
                AppOpenOnAdFailedDisplayed?.Invoke();
                // No manual reload — preload API auto-refills the buffer after Poll.
            };
        }

        private void TrackAdCustomEvent(string eventName, Dictionary<string, IConvertible> extraPayload = null)
        {
            try
            {
                _log.Debug("Tracking custom event for app open ad: " + eventName);

                extraPayload ??= new Dictionary<string, IConvertible>();

                extraPayload.Add("ad_format", AdFormatKey.AppOpen);
                extraPayload.Add("mediation_service", AdNetworkName.Admob);
                extraPayload.Add("ad_unit_id", _adUnitIDAppOpen ?? "unknown");

                // Best-effort: try to get adapter info from preload manager availability check.
                // We do not hold a reference to the current ad instance outside Show() to avoid
                // keeping a polled (consumed) ad alive beyond its show window.
                extraPayload.Add("ad_network", "unknown");

                Noctua.Event.TrackCustomEvent(eventName, extraPayload);
            }
            catch (Exception ex)
            {
                _log.Error($"Error tracking app open ad event '{eventName}': {ex.Message}\n{ex.StackTrace}");
            }
        }
    }
}
#endif
