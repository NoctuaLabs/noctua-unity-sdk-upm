using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using System.Collections.Generic;
using Newtonsoft.Json;
using com.noctuagames.sdk.AdPlaceholder;
using com.noctuagames.sdk.Events;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
using static GoogleMobileAds.Api.AdValue;
using com.noctuagames.sdk.Admob;
#endif

namespace com.noctuagames.sdk
{
    public partial class NoctuaIAAService
    {
        // --- Public ad show methods ---

        /// <summary>Loads an interstitial ad from the ad network.</summary>
        public void LoadInterstitialAd()
        {
            _log.Debug($"{LogTag} load_interstitial - load interstitial ad");
            if (_routing == null)
            {
                _log.Warning("Ad routing not initialized. Cannot load interstitial ad.");
                return;
            }
            _routing.Primary.LoadInterstitialAd();
        }

        /// <summary>Shows a full-screen interstitial ad with a placeholder overlay while loading.</summary>
        public void ShowInterstitial()
        {
            _log.Info($"{LogTag} show_interstitial - show interstitial (no placement)");
            ShowInterstitial(null);
        }

#if UNITY_ADMOB
        // isFallback=true means AdMob is the secondary network; on failure fire OnAdNotAvailable
        // rather than recursing back to TryInterstitialFallback.
        private void ShowAdmobInterstitial(IAdNetwork admobNetwork, string placement = null, bool isFallback = false)
        {
            _log.Info($"{LogTag} show_admob_interstitial - show AdMob interstitial");
            _hasClosedPlaceholder = false;
            ShowAdPlaceholder(AdPlaceholderType.Interstitial);

#if !UNITY_EDITOR
            // Device: use preload manager only when it was initialized (AdMob is primary).
            // When AdMob is secondary, _preloader is null and we fall through to the legacy path.
            if (_preloader != null)
            {
                if (_preloader.IsAdAvailable(_interstitialAdUnitID, AdFormat.INTERSTITIAL))
                {
                    var ad = _preloader.PollInterstitialAd(_interstitialAdUnitID);
                    if (ad != null)
                    {
                        _performanceTracker?.RecordFillAttempt(AdNetworkName.Admob, AdFormatKey.Interstitial, true);
                        try
                        {
                            _log.Info(placement != null
                                ? $"Showing Admob Interstitial Ad (placement: {placement})"
                                : "Showing Admob Interstitial Ad");

                            // Canonical ad_loaded on preload path — fires once per preloaded ad
                            // consumed from the buffer. Parity with the legacy path which emits
                            // ad_loaded from InterstitialAd.Load success callback.
                            string loadedAdSource = null;
                            try { loadedAdSource = ad.GetResponseInfo()?.GetLoadedAdapterResponseInfo()?.AdSourceName; } catch {}
                            EmitCanonicalIaa(IAAEventNames.AdLoaded, IAAPayloadBuilder.BuildAdLoaded(
                                placement:  placement,
                                adType:     AdFormatKey.Interstitial,
                                adUnitId:   _interstitialAdUnitID,
                                adUnitName: _interstitialAdUnitID,
                                adSize:     IAAAdSize.Fullscreen,
                                adSource:   loadedAdSource,
                                adPlatform: AdNetworkName.Admob));

                            RegisterCallbackAdInterstitial(ad, placement);
                            ad.Show();
                            // Record impression only after a successful show attempt.
                            _frequencyCap?.RecordImpression(AdFormatKey.Interstitial);
                        }
                        catch (Exception ex)
                        {
                            _log.Error($"Exception showing Admob Interstitial Ad: {ex.Message}\n{ex.StackTrace}");
                            CloseAdPlaceholder();
                        }
                        return;
                    }
                    else
                    {
                        _log.Warning("Admob Interstitial Ad poll returned null");
                        _performanceTracker?.RecordFillAttempt(AdNetworkName.Admob, AdFormatKey.Interstitial, false);
                        CloseAdPlaceholder();
                    }
                }
                else
                {
                    _log.Info("Admob Interstitial Ad not available");
                    _performanceTracker?.RecordFillAttempt(AdNetworkName.Admob, AdFormatKey.Interstitial, false);
                    CloseAdPlaceholder();
                }

                // Preload path failed — try secondary or fire not-available.
                if (!isFallback) TryInterstitialFallback(admobNetwork, placement);
                else NotifyAdNotAvailable(AdFormatKey.Interstitial);
                return;
            }
#endif

            // Legacy path: Unity Editor (preload API not supported) OR AdMob as secondary (no preload manager).
            bool filled = admobNetwork.IsInterstitialReady();
            _performanceTracker?.RecordFillAttempt(AdNetworkName.Admob, AdFormatKey.Interstitial, filled);
            if (filled)
            {
                _log.Info(placement != null
                    ? $"Showing Admob Interstitial Ad via legacy path (placement: {placement})"
                    : "Showing Admob Interstitial Ad via legacy path");
                if (placement != null) admobNetwork.ShowInterstitial(placement);
                else admobNetwork.ShowInterstitial();
                _frequencyCap?.RecordImpression(AdFormatKey.Interstitial);
            }
            else
            {
                _log.Info("Admob Interstitial Ad not ready (legacy path)");
                CloseAdPlaceholder();
                if (!isFallback) TryInterstitialFallback(admobNetwork, placement);
                else NotifyAdNotAvailable(AdFormatKey.Interstitial);
            }
        }
#endif

        private void TryInterstitialFallback(IAdNetwork failedNetwork, string placement)
        {
            _log.Debug($"{LogTag} try_interstitial_fallback - try interstitial fallback network");
            var fallback = failedNetwork == _routing.Primary
                ? _routing.Secondary
                : _routing.Primary;

            if (fallback == null)
            {
                _log.Info("Interstitial: no secondary network to fall back to.");
                NotifyAdNotAvailable(AdFormatKey.Interstitial);
                return;
            }

            if (!IsCpmFloorAcceptable(fallback, AdFormatKey.Interstitial))
            {
                _log.Info($"Interstitial fallback to {fallback.NetworkName} blocked by CPM hard floor. No ad available.");
                NotifyAdNotAvailable(AdFormatKey.Interstitial);
                return;
            }

            _log.Info($"{failedNetwork.NetworkName} interstitial not available. Falling back to {fallback.NetworkName}.");

            if (IsAdmobNetwork(fallback))
            {
#if UNITY_ADMOB
                ShowAdmobInterstitial(fallback, placement, isFallback: true);
#endif
                return;
            }

            // Fallback is AppLovin
            bool filled = fallback.IsInterstitialReady();
            _performanceTracker?.RecordFillAttempt(fallback.NetworkName, AdFormatKey.Interstitial, filled);
            if (filled)
            {
                _hasClosedPlaceholder = false;
                ShowAdPlaceholder(AdPlaceholderType.Interstitial);
                if (placement != null) fallback.ShowInterstitial(placement);
                else fallback.ShowInterstitial();
                _frequencyCap?.RecordImpression(AdFormatKey.Interstitial);
            }
            else
            {
                _log.Info($"{fallback.NetworkName} interstitial also not ready. No ad available.");
                NotifyAdNotAvailable(AdFormatKey.Interstitial);
            }
        }

        /// <summary>
        /// Dispatches <paramref name="action"/> to Unity's main thread.
        /// AdMob preloaded-ad callbacks fire on the GMA JNI thread; running Unity API
        /// calls (UI updates, AudioSource, etc.) directly from that thread crashes.
        /// </summary>
        private void PostToMainThread(Action action)
        {
            _log.Debug($"{LogTag} post_to_main_thread - post action to main thread");
            if (_mainThreadContext != null)
            {
                try
                {
                    _mainThreadContext.Post(_ => action(), null);
                }
                catch (Exception ex)
                {
                    _log.Error($"[NoctuaIAAService] PostToMainThread failed ({ex.GetType().Name}: {ex.Message}). Executing inline as fallback.");
                    action();
                }
            }
            else
                action(); // fallback: already on main thread (Editor / tests)
        }

#if UNITY_ADMOB
        private void RegisterCallbackAdInterstitial(InterstitialAd interstitialAd, string placement = null)
        {
            _log.Debug($"{LogTag} register_interstitial_callbacks - register interstitial ad callbacks");
            // Preload path needs to emit canonical IAA events (ad_shown / ad_impression /
            // ad_clicked / ad_show_failed) just like the legacy path does inside
            // InterstitialAdmob.RegisterEventHandlers. Without these calls the preload path
            // silently drops every canonical event on device — breaking dashboards that
            // filter by canonical event names (user-visible symptom: only banner events show).
            AdValue capturedAdValue = null;
            string capturedImpressionId = null;
            var showStopwatch = System.Diagnostics.Stopwatch.StartNew();

            string ResolveAdSource()
            {
                try { return interstitialAd.GetResponseInfo()?.GetLoadedAdapterResponseInfo()?.AdSourceName; }
                catch { return null; }
            }

            interstitialAd.OnAdFullScreenContentOpened += () => PostToMainThread(() =>
            {
                CloseAdPlaceholder(force: true);
                EmitCanonicalIaa(IAAEventNames.AdShown, IAAPayloadBuilder.BuildAdLoaded(
                    placement:  placement,
                    adType:     AdFormatKey.Interstitial,
                    adUnitId:   _interstitialAdUnitID,
                    adUnitName: _interstitialAdUnitID,
                    adSize:     IAAAdSize.Fullscreen,
                    adSource:   ResolveAdSource(),
                    adPlatform: AdNetworkName.Admob));
                _onAdDisplayed?.Invoke();
            });
            interstitialAd.OnAdFullScreenContentFailed += (AdError error) => PostToMainThread(() =>
            {
                EmitCanonicalIaa(IAAEventNames.AdShowFailed, IAAPayloadBuilder.BuildAdShowFailed(
                    adFormat:   AdFormatKey.Interstitial,
                    adPlatform: AdNetworkName.Admob,
                    adUnitName: _interstitialAdUnitID,
                    error:      IAAPayloadBuilder.FormatError(error.GetCode(), error.GetMessage(), error.GetDomain())));
                // Show the cross-promotion fallback; only report failure to the game if it can't show.
                if (!ShowCrossPromoFallback(AdPlaceholderType.Interstitial)) _onAdFailedDisplayed?.Invoke();
                _log.Warning("Interstitial Ad failed to show. Error: " + error);
            });
            interstitialAd.OnAdFullScreenContentClosed += () => PostToMainThread(() =>
            {
                // Parity with the legacy path's OnAdFullScreenContentClosed handler:
                // an interstitial close counts as one watched ad for the watch-milestone tracker.
                AdWatchMilestoneTracker.Default?.RecordWatch(AdFormatKey.Interstitial);
                _onAdClosed?.Invoke();
            });
            interstitialAd.OnAdClicked += () => PostToMainThread(() =>
            {
                EmitCanonicalIaa(IAAEventNames.AdClicked, IAAPayloadBuilder.BuildAdClicked(
                    placement:  placement,
                    adType:     AdFormatKey.Interstitial,
                    adUnitId:   _interstitialAdUnitID,
                    adUnitName: _interstitialAdUnitID,
                    adSize:     IAAAdSize.Fullscreen,
                    adSource:   ResolveAdSource(),
                    adPlatform: AdNetworkName.Admob));
                _onAdClicked?.Invoke();
            });
            interstitialAd.OnAdImpressionRecorded += () => PostToMainThread(() =>
            {
                capturedImpressionId = System.Guid.NewGuid().ToString("N");
                var engagementMs = showStopwatch.IsRunning ? showStopwatch.ElapsedMilliseconds : 0L;
                showStopwatch.Stop();
                var valueMicros = capturedAdValue?.Value ?? 0L;
                if (capturedAdValue == null)
                    _log.Warning("OnAdImpressionRecorded fired before OnAdPaid; revenue value will be 0 in impression payload.");
                var value       = valueMicros / 1_000_000d;
                var currency    = capturedAdValue?.CurrencyCode;
                var valueUsd    = currency == "USD" ? value : 0d;

                var impPayload = IAAPayloadBuilder.BuildAdImpression(
                    placement:        placement,
                    adType:           AdFormatKey.Interstitial,
                    adUnitId:         _interstitialAdUnitID,
                    adUnitName:       _interstitialAdUnitID,
                    valueUsd:         valueUsd,
                    adSize:           IAAAdSize.Fullscreen,
                    adSource:         ResolveAdSource(),
                    adPlatform:       AdNetworkName.Admob,
                    engagementTimeMs: engagementMs);
                impPayload["sdk_impression_id"] = capturedImpressionId ?? "";
                EmitCanonicalIaa(IAAEventNames.AdImpression, impPayload);
                _onAdImpressionRecorded?.Invoke();
            });
            interstitialAd.OnAdPaid += (AdValue adValue) => PostToMainThread(() =>
            {
                capturedAdValue = adValue; // cached for the OnAdImpressionRecorded canonical emit
                var capturedResponseInfo = interstitialAd.GetResponseInfo();
                try
                {
                    var revenue    = adValue.Value / 1_000_000.0;
                    var deviceId   = UnityEngine.SystemInfo.deviceUniqueIdentifier;
                    var revPayload = IAAPayloadBuilder.BuildAdmobRevenuePayload(adValue, capturedResponseInfo, deviceId);
                    revPayload["sdk_impression_id"] = capturedImpressionId ?? "";
                    if (string.IsNullOrEmpty(capturedImpressionId))
                        _log.Warning("OnAdPaid fired before OnAdImpressionRecorded; impression_id will be empty in revenue payload.");
                    revPayload["sdk_revenue_id"]    = System.Guid.NewGuid().ToString("N");
                    Noctua.Event.TrackAdRevenue("admob_sdk", revenue, adValue.CurrencyCode, revPayload);
                }
                catch (Exception ex) { _log.Error($"Error tracking AdMob interstitial preload revenue: {ex.Message}\n{ex.StackTrace}"); }
                // Not double-tracking: ProcessAdmob*Revenue only logs + accumulates Taichi
                // thresholds; the TrackAdRevenue call above is the sole revenue event emitter.
                _revenueTracker.ProcessAdmobInterstitialRevenue(adValue, capturedResponseInfo);
                _admobOnAdRevenuePaid?.Invoke(adValue, capturedResponseInfo);
                _performanceTracker?.RecordRevenue(AdNetworkName.Admob, AdFormatKey.Interstitial, adValue.Value / 1_000_000.0);
            });
        }
#endif

        /// <summary>
        /// Shows an interstitial ad with an optional placement name for analytics segmentation.
        /// For AppLovin the placement is passed natively to MAX SDK.
        /// For AdMob the placement is included in custom event tracking only.
        /// Passing <c>null</c> is identical to calling <see cref="ShowInterstitial()"/>.
        /// </summary>
        public void ShowInterstitial(string placement)
        {
            _log.Info($"{LogTag} show_interstitial_placement - show interstitial (with placement)");
            if (_routing == null)
            {
                _log.Warning("Ad routing not initialized. Cannot show interstitial ad.");
                return;
            }

            // New game-initiated request — re-arm the cross-promo (clears any dismiss from a prior request).
            _hasClosedPlaceholder = false;

            if (_frequencyCap != null && !_frequencyCap.CanShowAd(AdFormatKey.Interstitial))
            {
                _log.Info("Interstitial ad blocked by frequency manager.");
                NotifyAdNotAvailable(AdFormatKey.Interstitial);
                return;
            }

            var network = _routing.GetNetworkForFormat(AdFormatKey.Interstitial);

            if (!IsCpmFloorAcceptable(network, AdFormatKey.Interstitial))
            {
                _log.Info($"Preferred network {network.NetworkName} interstitial blocked by CPM hard floor. Trying fallback.");
                TryInterstitialFallback(network, placement);
                return;
            }

            if (IsAdmobNetwork(network))
            {
#if UNITY_ADMOB
                ShowAdmobInterstitial(network, placement, isFallback: false);
                // RecordImpression is called inside ShowAdmobInterstitial only when the ad is actually shown.
#endif
                return;
            }

            // Non-AdMob (AppLovin) path
            bool filled = network.IsInterstitialReady();
            _performanceTracker?.RecordFillAttempt(network.NetworkName, AdFormatKey.Interstitial, filled);

            if (filled)
            {
                _hasClosedPlaceholder = false;
                ShowAdPlaceholder(AdPlaceholderType.Interstitial);
                if (placement != null)
                    network.ShowInterstitial(placement);
                else
                    network.ShowInterstitial();

                _frequencyCap?.RecordImpression(AdFormatKey.Interstitial);
            }
            else
            {
                TryInterstitialFallback(network, placement);
            }
        }
    }
}
