#if UNITY_ADMOB
using GoogleMobileAds.Api;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Diagnostics;

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
        // Placement captured on Show(placement). Forwarded to canonical IAA events.
        private string _lastPlacement;

        /// <summary>Records the placement name to attach to subsequent canonical IAA events.</summary>
        public void SetPlacement(string placement) => _lastPlacement = placement;
        private AppOpenAd _legacyAppOpenAd;
        private AdValue _lastAdValue;
        private readonly Stopwatch _showStopwatch = new();

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
        /// Attempts to load an app open ad. If the Preload API has ads available, this is a no-op.
        /// Otherwise falls back to legacy <see cref="AppOpenAd.Load"/> for environments where
        /// the Preload API is unavailable (e.g. emulator without GMS Dynamite module).
        /// </summary>
        public void LoadAppOpenAd()
        {
            if (string.IsNullOrEmpty(_adUnitIDAppOpen) || _adUnitIDAppOpen == "unknown")
            {
                _log.Warning("Cannot load app open ad — unit ID not configured.");
                return;
            }

            // Check if Preload API has ads available. If so, no manual load needed.
            try
            {
                if (AdmobAdPreloadManager.Instance.IsAdAvailable(_adUnitIDAppOpen, AdFormat.APP_OPEN_AD))
                {
                    _log.Debug("LoadAppOpenAd: preloaded ad already available. No manual load needed.");
                    return;
                }
            }
            catch (Exception)
            {
                // IsAdAvailable may throw on emulator — fall through to legacy load
            }

            // Legacy fallback: load via AppOpenAd.Load() when Preload API is unavailable
            _log.Info("LoadAppOpenAd: Preload buffer empty or unavailable. Using legacy AppOpenAd.Load() fallback.");
            TrackAdCustomEvent("wf_app_open_request_start");
            var adRequest = new AdRequest();
            AppOpenAd.Load(_adUnitIDAppOpen, adRequest, (AppOpenAd ad, LoadAdError error) =>
            {
                if (error != null || ad == null)
                {
                    _log.Warning($"Legacy app open ad load failed: {error?.GetMessage()}");

                    // Canonical ad_load_failed (was missing entirely on AdMob AO)
                    EmitCanonical(IAAEventNames.AdLoadFailed, IAAPayloadBuilder.BuildAdLoadFailed(
                        adFormat:   AdFormatKey.AppOpen,
                        adPlatform: AdNetworkName.Admob,
                        adUnitName: _adUnitIDAppOpen,
                        error:      IAAPayloadBuilder.FormatError(
                            error?.GetCode() ?? -1,
                            error?.GetMessage(),
                            error?.GetDomain())
                    ));

                    TrackAdCustomEvent("wf_app_open_request_adunit_failed");
                    TrackAdCustomEvent("wf_app_open_request_finished_failed");
                    return;
                }

                _log.Info("Legacy app open ad loaded successfully.");
                _legacyAppOpenAd = ad;

                var loadedAdapter = ad.GetResponseInfo()?.GetLoadedAdapterResponseInfo();
                string adSource = null;
                try { adSource = loadedAdapter?.AdSourceName; } catch {}

                EmitCanonical(IAAEventNames.AdLoaded, IAAPayloadBuilder.BuildAdLoaded(
                    placement:  _lastPlacement,
                    adType:     AdFormatKey.AppOpen,
                    adUnitId:   _adUnitIDAppOpen,
                    adUnitName: _adUnitIDAppOpen,
                    adSize:     IAAAdSize.Fullscreen,
                    adSource:   adSource,
                    adPlatform: AdNetworkName.Admob
                ));

                TrackAdCustomEvent("wf_app_open_adunit_success");
                TrackAdCustomEvent("wf_app_open_request_finished_success");
            });
        }

        /// <summary>
        /// Shows the app open ad. Tries legacy-loaded ad first, then falls back to the Preload API.
        /// </summary>
        public void ShowAppOpenAd()
        {
            if (string.IsNullOrEmpty(_adUnitIDAppOpen) || _adUnitIDAppOpen == "unknown")
            {
                _log.Warning("App open ad unit ID not configured.");
                TrackAdCustomEvent("wf_app_open_show_not_ready");
                TrackAdCustomEvent("wf_app_open_show_failed_null");
                return;
            }

            // Try legacy loaded ad first
            if (_legacyAppOpenAd != null)
            {
                if (!_legacyAppOpenAd.CanShowAd())
                {
                    _log.Warning("Legacy app open ad loaded but CanShowAd() returned false. Discarding stale ad.");
                    _legacyAppOpenAd.Destroy();
                    _legacyAppOpenAd = null;
                    TrackAdCustomEvent("wf_app_open_show_not_ready");
                TrackAdCustomEvent("wf_app_open_show_failed_null");
                    // Fall through to try preload path
                }
                else
                {
                    try
                    {
                        _log.Debug("Showing legacy-loaded app open ad.");
                        TrackAdCustomEvent("wf_app_open_started_playing");
                        RegisterEventHandlers(_legacyAppOpenAd);
                        _showStopwatch.Restart();
                        _legacyAppOpenAd.Show();
                        _legacyAppOpenAd = null; // Consumed
                        return;
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"Exception showing legacy app open ad: {ex.Message}");
                        _legacyAppOpenAd = null;
                        AppOpenOnAdFailedDisplayed?.Invoke();
                        // Fall through to try preload path
                    }
                }
            }

            // Preload path
            var preload = AdmobAdPreloadManager.Instance;

            if (!preload.IsAdAvailable(_adUnitIDAppOpen, AdFormat.APP_OPEN_AD))
            {
                _log.Warning("App open ad is not ready yet (preload buffer empty).");
                TrackAdCustomEvent("wf_app_open_show_not_ready");
                TrackAdCustomEvent("wf_app_open_show_failed_null");
                return;
            }

            var ad = preload.PollAppOpenAd(_adUnitIDAppOpen);
            if (ad == null)
            {
                _log.Warning("App open ad poll returned null.");
                TrackAdCustomEvent("wf_app_open_show_not_ready");
                TrackAdCustomEvent("wf_app_open_show_failed_null");
                return;
            }

            try
            {
                _log.Debug("Showing app open ad.");
                TrackAdCustomEvent("wf_app_open_started_playing");
                RegisterEventHandlers(ad);
                _showStopwatch.Restart();
                ad.Show();
            }
            catch (Exception ex)
            {
                _log.Error($"Exception showing app open ad: {ex.Message}\n{ex.StackTrace}");
                AppOpenOnAdFailedDisplayed?.Invoke();
            }
        }

        /// <summary>
        /// Returns whether an app open ad is available (legacy-loaded or preloaded).
        /// </summary>
        public bool IsAdReady()
        {
            // Check legacy loaded ad first
            if (_legacyAppOpenAd != null) return true;

            if (string.IsNullOrEmpty(_adUnitIDAppOpen) || _adUnitIDAppOpen == "unknown")
                return false;

            try
            {
                return AdmobAdPreloadManager.Instance.IsAdAvailable(_adUnitIDAppOpen, AdFormat.APP_OPEN_AD);
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void RegisterEventHandlers(AppOpenAd ad)
        {
            ad.OnAdPaid += (AdValue adValue) =>
            {
                _log.Debug($"App open ad paid {adValue.Value} {adValue.CurrencyCode}.");
                _lastAdValue = adValue;
                AdmobOnAdRevenuePaid?.Invoke(adValue, ad.GetResponseInfo());
            };

            ad.OnAdImpressionRecorded += () =>
            {
                _log.Debug("App open ad recorded an impression.");

                var engagementMs = _showStopwatch.IsRunning ? _showStopwatch.ElapsedMilliseconds : 0L;
                _showStopwatch.Reset();

                var valueMicros = _lastAdValue?.Value ?? 0L;
                var value       = valueMicros / 1_000_000d;
                var currency    = _lastAdValue?.CurrencyCode;
                var valueUsd    = currency == "USD" ? value : 0d;

                var loadedAdapter = ad.GetResponseInfo()?.GetLoadedAdapterResponseInfo();
                string adSource = null;
                try { adSource = loadedAdapter?.AdSourceName; } catch {}

                EmitCanonical(IAAEventNames.AdImpression, IAAPayloadBuilder.BuildAdImpression(
                    placement:        _lastPlacement,
                    adType:           AdFormatKey.AppOpen,
                    adUnitId:         _adUnitIDAppOpen,
                    adUnitName:       _adUnitIDAppOpen,
                    value:            value,
                    valueUsd:         valueUsd,
                    adSize:           IAAAdSize.Fullscreen,
                    adSource:         adSource,
                    adPlatform:       AdNetworkName.Admob,
                    engagementTimeMs: engagementMs
                ));

                TrackAdCustomEvent("ad_impression_app_open");
                AppOpenOnAdImpressionRecorded?.Invoke();
            };

            ad.OnAdClicked += () =>
            {
                _log.Debug("App open ad was clicked.");

                var loadedAdapter = ad.GetResponseInfo()?.GetLoadedAdapterResponseInfo();
                string adSource = null;
                try { adSource = loadedAdapter?.AdSourceName; } catch {}

                EmitCanonical(IAAEventNames.AdClicked, IAAPayloadBuilder.BuildAdClicked(
                    placement:  _lastPlacement,
                    adType:     AdFormatKey.AppOpen,
                    adUnitId:   _adUnitIDAppOpen,
                    adUnitName: _adUnitIDAppOpen,
                    adSize:     IAAAdSize.Fullscreen,
                    adSource:   adSource,
                    adPlatform: AdNetworkName.Admob
                ));

                TrackAdCustomEvent("wf_app_open_clicked");
                AppOpenOnAdClicked?.Invoke();
            };

            ad.OnAdFullScreenContentOpened += () =>
            {
                _log.Debug("App open ad full screen content opened.");

                var loadedAdapter = ad.GetResponseInfo()?.GetLoadedAdapterResponseInfo();
                string adSource = null;
                try { adSource = loadedAdapter?.AdSourceName; } catch {}

                EmitCanonical(IAAEventNames.AdShown, IAAPayloadBuilder.BuildAdLoaded(
                    placement:  _lastPlacement,
                    adType:     AdFormatKey.AppOpen,
                    adUnitId:   _adUnitIDAppOpen,
                    adUnitName: _adUnitIDAppOpen,
                    adSize:     IAAAdSize.Fullscreen,
                    adSource:   adSource,
                    adPlatform: AdNetworkName.Admob
                ));

                TrackAdCustomEvent("wf_app_open_show_sdk");
                AppOpenOnAdDisplayed?.Invoke();
            };

            ad.OnAdFullScreenContentClosed += () =>
            {
                _log.Debug("App open ad full screen content closed.");
                TrackAdCustomEvent("ad_closed");
                TrackAdCustomEvent("wf_app_open_closed");
                AppOpenOnAdClosed?.Invoke();
                // Reload for next show (legacy path; preload API auto-refills after Poll)
                LoadAppOpenAd();
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

                EmitCanonical(IAAEventNames.AdShowFailed, IAAPayloadBuilder.BuildAdShowFailed(
                    adFormat:   AdFormatKey.AppOpen,
                    adPlatform: AdNetworkName.Admob,
                    adUnitName: _adUnitIDAppOpen,
                    error:      IAAPayloadBuilder.FormatError(
                        error.GetCode(), error.GetMessage(), error.GetDomain())
                ));

                TrackAdCustomEvent("wf_app_open_show_sdk_failed", extraPayload);
                AppOpenOnAdFailedDisplayed?.Invoke();
                // No manual reload — preload API auto-refills the buffer after Poll.
            };
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
                _log.Error($"Error emitting canonical AO event '{eventName}': {ex.Message}");
            }
        }

        private void TrackAdCustomEvent(string eventName, Dictionary<string, IConvertible> extraPayload = null)
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
                payload["mediation_service"] = AdNetworkName.Admob;
                payload["ad_unit_id"] = _adUnitIDAppOpen ?? "unknown";

                // Best-effort: try to get adapter info from preload manager availability check.
                // We do not hold a reference to the current ad instance outside Show() to avoid
                // keeping a polled (consumed) ad alive beyond its show window.
                payload["ad_network"] = "unknown";

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
