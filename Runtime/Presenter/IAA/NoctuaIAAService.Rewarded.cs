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
        /// <summary>Loads a rewarded ad from the ad network.</summary>
        public void LoadRewardedAd()
        {
            _log.Debug($"{LogTag} load_rewarded - load rewarded ad");
            if (_routing == null)
            {
                _log.Warning("Ad routing not initialized. Cannot load rewarded ad.");
                return;
            }
            _routing.Primary.LoadRewardedAd();
        }

        /// <summary>Shows a rewarded ad with a placeholder overlay while loading.</summary>
        public void ShowRewardedAd()
        {
            _log.Info($"{LogTag} show_rewarded - show rewarded (no placement)");
            ShowRewardedAd(null);
        }

#if UNITY_ADMOB
        // isFallback=true means AdMob is the secondary network; on failure fire OnAdNotAvailable
        // rather than recursing back to TryRewardedFallback.
        private void ShowAdmobRewarded(IAdNetwork admobNetwork, string placement = null, bool isFallback = false)
        {
            _log.Info($"{LogTag} show_admob_rewarded - show AdMob rewarded");
            _hasClosedPlaceholder = false;
            ShowAdPlaceholder(AdPlaceholderType.Rewarded);

#if !UNITY_EDITOR
            // Device: use preload manager only when it was initialized (AdMob is primary).
            if (_preloader != null)
            {
                if (_preloader.IsAdAvailable(_rewardedAdUnitID, AdFormat.REWARDED))
                {
                    var ad = _preloader.PollRewardedAd(_rewardedAdUnitID);
                    if (ad != null)
                    {
                        _performanceTracker?.RecordFillAttempt(AdNetworkName.Admob, AdFormatKey.Rewarded, true);
                        try
                        {
                            _log.Info(placement != null
                                ? $"Showing Admob Rewarded Ad (placement: {placement})"
                                : "Showing Admob Rewarded Ad");
                            // Canonical ad_loaded on preload path — parity with legacy Load callback.
                            string loadedAdSourceR = null;
                            try { loadedAdSourceR = ad.GetResponseInfo()?.GetLoadedAdapterResponseInfo()?.AdSourceName; } catch {}
                            EmitCanonicalIaa(IAAEventNames.AdLoaded, IAAPayloadBuilder.BuildAdLoaded(
                                placement:  placement,
                                adType:     AdFormatKey.Rewarded,
                                adUnitId:   _rewardedAdUnitID,
                                adUnitName: _rewardedAdUnitID,
                                adSize:     IAAAdSize.Fullscreen,
                                adSource:   loadedAdSourceR,
                                adPlatform: AdNetworkName.Admob));

                            RegisterCallbackAdRewarded(ad, placement);
                            ad.Show((Reward reward) => PostToMainThread(() =>
                            {
                                _log.Info("User earned reward: " + reward.Type + " - " + reward.Amount);
                                _admobOnUserEarnedReward?.Invoke(reward);
                            }));
                            // Record impression only after a successful show attempt.
                            _frequencyCap?.RecordImpression(AdFormatKey.Rewarded);
                        }
                        catch (Exception ex)
                        {
                            _log.Error($"Exception showing Admob Rewarded Ad: {ex.Message}\n{ex.StackTrace}");
                            CloseAdPlaceholder();
                        }
                        return;
                    }
                    else
                    {
                        _log.Warning("Admob Rewarded Ad poll returned null");
                        _performanceTracker?.RecordFillAttempt(AdNetworkName.Admob, AdFormatKey.Rewarded, false);
                        CloseAdPlaceholder();
                    }
                }
                else
                {
                    _log.Info("Admob Rewarded Ad not available");
                    _performanceTracker?.RecordFillAttempt(AdNetworkName.Admob, AdFormatKey.Rewarded, false);
                    CloseAdPlaceholder();
                }

                // Preload path failed — try secondary or fire not-available.
                if (!isFallback) TryRewardedFallback(admobNetwork, placement);
                else NotifyAdNotAvailable(AdFormatKey.Rewarded);
                return;
            }
#endif

            // Legacy path: Unity Editor (preload API not supported) OR AdMob as secondary (no preload manager).
            bool filled = admobNetwork.IsRewardedAdReady();
            _performanceTracker?.RecordFillAttempt(AdNetworkName.Admob, AdFormatKey.Rewarded, filled);
            if (filled)
            {
                _log.Info(placement != null
                    ? $"Showing Admob Rewarded Ad via legacy path (placement: {placement})"
                    : "Showing Admob Rewarded Ad via legacy path");
                if (placement != null) admobNetwork.ShowRewardedAd(placement);
                else admobNetwork.ShowRewardedAd();
                _frequencyCap?.RecordImpression(AdFormatKey.Rewarded);
            }
            else
            {
                _log.Info("Admob Rewarded Ad not ready (legacy path)");
                CloseAdPlaceholder();
                if (!isFallback) TryRewardedFallback(admobNetwork, placement);
                else NotifyAdNotAvailable(AdFormatKey.Rewarded);
            }
        }
#endif

        private void TryRewardedFallback(IAdNetwork failedNetwork, string placement)
        {
            _log.Debug($"{LogTag} try_rewarded_fallback - try rewarded fallback network");
            var fallback = failedNetwork == _routing.Primary
                ? _routing.Secondary
                : _routing.Primary;

            if (fallback == null)
            {
                _log.Info("Rewarded: no secondary network to fall back to.");
                NotifyAdNotAvailable(AdFormatKey.Rewarded);
                return;
            }

            if (!IsCpmFloorAcceptable(fallback, AdFormatKey.Rewarded))
            {
                _log.Info($"Rewarded fallback to {fallback.NetworkName} blocked by CPM hard floor. No ad available.");
                NotifyAdNotAvailable(AdFormatKey.Rewarded);
                return;
            }

            _log.Info($"{failedNetwork.NetworkName} rewarded not available. Falling back to {fallback.NetworkName}.");

            if (IsAdmobNetwork(fallback))
            {
#if UNITY_ADMOB
                ShowAdmobRewarded(fallback, placement, isFallback: true);
#endif
                return;
            }

            // Fallback is AppLovin
            bool filled = fallback.IsRewardedAdReady();
            _performanceTracker?.RecordFillAttempt(fallback.NetworkName, AdFormatKey.Rewarded, filled);
            if (filled)
            {
                _hasClosedPlaceholder = false;
                ShowAdPlaceholder(AdPlaceholderType.Rewarded);
                if (placement != null) fallback.ShowRewardedAd(placement);
                else fallback.ShowRewardedAd();
                _frequencyCap?.RecordImpression(AdFormatKey.Rewarded);
            }
            else
            {
                _log.Info($"{fallback.NetworkName} rewarded also not ready. No ad available.");
                NotifyAdNotAvailable(AdFormatKey.Rewarded);
            }
        }

#if UNITY_ADMOB
        private void RegisterCallbackAdRewarded(RewardedAd rewardedAd, string placement = null)
        {
            _log.Debug($"{LogTag} register_rewarded_callbacks - register rewarded ad callbacks");
            // See comment in RegisterCallbackAdInterstitial — canonical IAA events must be
            // emitted here as well, otherwise the preload path silently drops every
            // canonical rewarded event on device.
            AdValue capturedAdValue = null;
            string capturedImpressionId = null;
            var showStopwatch = System.Diagnostics.Stopwatch.StartNew();

            string ResolveAdSource()
            {
                try { return rewardedAd.GetResponseInfo()?.GetLoadedAdapterResponseInfo()?.AdSourceName; }
                catch { return null; }
            }

            rewardedAd.OnAdFullScreenContentOpened += () => PostToMainThread(() =>
            {
                CloseAdPlaceholder(force: true);
                EmitCanonicalIaa(IAAEventNames.AdShown, IAAPayloadBuilder.BuildAdLoaded(
                    placement:  placement,
                    adType:     AdFormatKey.Rewarded,
                    adUnitId:   _rewardedAdUnitID,
                    adUnitName: _rewardedAdUnitID,
                    adSize:     IAAAdSize.Fullscreen,
                    adSource:   ResolveAdSource(),
                    adPlatform: AdNetworkName.Admob));
                _onAdDisplayed?.Invoke();
            });
            rewardedAd.OnAdFullScreenContentFailed += (AdError error) => PostToMainThread(() =>
            {
                EmitCanonicalIaa(IAAEventNames.AdShowFailed, IAAPayloadBuilder.BuildAdShowFailed(
                    adFormat:   AdFormatKey.Rewarded,
                    adPlatform: AdNetworkName.Admob,
                    adUnitName: _rewardedAdUnitID,
                    error:      IAAPayloadBuilder.FormatError(error.GetCode(), error.GetMessage(), error.GetDomain())));
                // Show the cross-promotion fallback; only report failure to the game if it can't show.
                if (!ShowCrossPromoFallback(AdPlaceholderType.Rewarded)) _onAdFailedDisplayed?.Invoke();
                _log.Warning("Rewarded Ad failed to show. Error: " + error);
            });
            rewardedAd.OnAdFullScreenContentClosed += () => PostToMainThread(() =>
            {
                // Parity with the legacy path: a rewarded close counts as one watched ad for
                // the watch-milestone tracker (fires watch_ads_5x/10x/25x/50x).
                _watchMilestones?.RecordWatch(AdFormatKey.Rewarded);
                _onAdClosed?.Invoke();
            });
            rewardedAd.OnAdClicked += () => PostToMainThread(() =>
            {
                EmitCanonicalIaa(IAAEventNames.AdClicked, IAAPayloadBuilder.BuildAdClicked(
                    placement:  placement,
                    adType:     AdFormatKey.Rewarded,
                    adUnitId:   _rewardedAdUnitID,
                    adUnitName: _rewardedAdUnitID,
                    adSize:     IAAAdSize.Fullscreen,
                    adSource:   ResolveAdSource(),
                    adPlatform: AdNetworkName.Admob));
                _onAdClicked?.Invoke();
            });
            rewardedAd.OnAdImpressionRecorded += () => PostToMainThread(() =>
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
                    adType:           AdFormatKey.Rewarded,
                    adUnitId:         _rewardedAdUnitID,
                    adUnitName:       _rewardedAdUnitID,
                    valueUsd:         valueUsd,
                    adSize:           IAAAdSize.Fullscreen,
                    adSource:         ResolveAdSource(),
                    adPlatform:       AdNetworkName.Admob,
                    engagementTimeMs: engagementMs);
                impPayload["sdk_impression_id"] = capturedImpressionId ?? "";
                EmitCanonicalIaa(IAAEventNames.AdImpression, impPayload);
                _onAdImpressionRecorded?.Invoke();
            });
            rewardedAd.OnAdPaid += (AdValue adValue) => PostToMainThread(() =>
            {
                capturedAdValue = adValue; // cached for the OnAdImpressionRecorded canonical emit
                var capturedResponseInfo = rewardedAd.GetResponseInfo();
                try
                {
                    var revenue    = adValue.Value / 1_000_000.0;
                    var deviceId   = UnityEngine.SystemInfo.deviceUniqueIdentifier;
                    var revPayload = IAAPayloadBuilder.BuildAdmobRevenuePayload(adValue, capturedResponseInfo, deviceId);
                    revPayload["sdk_impression_id"] = capturedImpressionId ?? "";
                    if (string.IsNullOrEmpty(capturedImpressionId))
                        _log.Warning("OnAdPaid fired before OnAdImpressionRecorded; impression_id will be empty in revenue payload.");
                    revPayload["sdk_revenue_id"]    = System.Guid.NewGuid().ToString("N");
                    _adRevenueTracker?.TrackAdRevenue("admob_sdk", revenue, adValue.CurrencyCode, revPayload);
                }
                catch (Exception ex) { _log.Error($"Error tracking AdMob rewarded preload revenue: {ex.Message}\n{ex.StackTrace}"); }
                // Not double-tracking: ProcessAdmob*Revenue only logs + accumulates Taichi
                // thresholds; the TrackAdRevenue call above is the sole revenue event emitter.
                _revenueTracker.ProcessAdmobRewardedRevenue(adValue, capturedResponseInfo);
                _admobOnAdRevenuePaid?.Invoke(adValue, capturedResponseInfo);
                _performanceTracker?.RecordRevenue(AdNetworkName.Admob, AdFormatKey.Rewarded, adValue.Value / 1_000_000.0);
            });
        }

        /// <summary>
        /// Routes a canonical IAA event through the injected tracker
        /// with a try/catch so analytics failures never break ad delivery. Mirrors
        /// the per-format adapter's EmitCanonical.
        /// </summary>
        private void EmitCanonicalIaa(string eventName, System.Collections.Generic.Dictionary<string, System.IConvertible> payload)
        {
            _log.Debug($"{LogTag} emit_canonical_iaa - emit canonical IAA event");
            try
            {
                _adRevenueTracker?.TrackCustomEvent(eventName, payload);
            }
            catch (Exception ex)
            {
                _log.Error($"Error emitting canonical IAA event '{eventName}' from preload path: {ex.Message}");
            }
        }
#endif

        /// <summary>Shows a rewarded interstitial ad with a placeholder overlay (AdMob only).</summary>
        public void ShowRewardedInterstitialAd()
        {
            _log.Info($"{LogTag} show_rewarded_interstitial - show rewarded interstitial");
            if (_routing == null)
            {
                _log.Warning("Ad routing not initialized. Cannot show rewarded interstitial ad.");
                return;
            }

            // New game-initiated request — re-arm the cross-promo (clears any dismiss from a prior request).
            _hasClosedPlaceholder = false;

            if (_frequencyCap != null && !_frequencyCap.CanShowAd(AdFormatKey.RewardedInterstitial))
            {
                _log.Info("Rewarded interstitial ad blocked by frequency manager.");
                NotifyAdNotAvailable(AdFormatKey.RewardedInterstitial);
                return;
            }

            // rewarded_interstitial is AdMob-only. Warn and skip if mistakenly overridden to AppLovin.
            var riNetwork = _routing.GetNetworkForFormat(AdFormatKey.RewardedInterstitial);
            if (!IsAdmobNetwork(riNetwork))
            {
                _log.Warning($"rewarded_interstitial is AdMob-only; ignoring override to {riNetwork.NetworkName}. Routing to AdMob.");
            }
#if UNITY_ADMOB
            ShowAdmobRewardedInterstitial();
#else
            _log.Warning("rewarded_interstitial requires UNITY_ADMOB define.");
#endif
        }

#if UNITY_ADMOB
        private void ShowAdmobRewardedInterstitial()
        {
            _log.Info($"{LogTag} show_admob_rewarded_interstitial - show AdMob rewarded interstitial");
            if (_rewardedInterstitialAdmob == null)
            {
                _log.Warning("Rewarded interstitial ad not initialized (ad unit ID may be missing). Cannot show.");
                return;
            }

            _hasClosedPlaceholder = false;
            ShowAdPlaceholder(AdPlaceholderType.RewardedInterstitial);
            // Placeholder is closed via RewardedOnAdDisplayed / RewardedOnAdFailedDisplayed events
            // wired during SetupAdUnitID. RewardedInterstitialAdmob manages its own reload lifecycle.
            _rewardedInterstitialAdmob.ShowRewardedInterstitialAd();
        }
#endif

        /// <summary>
        /// Shows a rewarded ad with an optional placement name for analytics segmentation.
        /// For AppLovin the placement is passed natively to MAX SDK.
        /// For AdMob the placement is included in custom event tracking only.
        /// Passing <c>null</c> is identical to calling <see cref="ShowRewardedAd()"/>.
        /// </summary>
        public void ShowRewardedAd(string placement)
        {
            _log.Info($"{LogTag} show_rewarded_placement - show rewarded (with placement)");
            if (_routing == null)
            {
                _log.Warning("Ad routing not initialized. Cannot show rewarded ad.");
                return;
            }

            // New game-initiated request — re-arm the cross-promo (clears any dismiss from a prior request).
            _hasClosedPlaceholder = false;

            if (_frequencyCap != null && !_frequencyCap.CanShowAd(AdFormatKey.Rewarded))
            {
                _log.Info("Rewarded ad blocked by frequency manager.");
                NotifyAdNotAvailable(AdFormatKey.Rewarded);
                return;
            }

            var network = _routing.GetNetworkForFormat(AdFormatKey.Rewarded);

            if (!IsCpmFloorAcceptable(network, AdFormatKey.Rewarded))
            {
                _log.Info($"Preferred network {network.NetworkName} rewarded blocked by CPM hard floor. Trying fallback.");
                TryRewardedFallback(network, placement);
                return;
            }

            if (IsAdmobNetwork(network))
            {
#if UNITY_ADMOB
                ShowAdmobRewarded(network, placement, isFallback: false);
                // RecordImpression is called inside ShowAdmobRewarded only when the ad is actually shown.
#endif
                return;
            }

            // Non-AdMob (AppLovin) path
            bool filled = network.IsRewardedAdReady();
            _performanceTracker?.RecordFillAttempt(network.NetworkName, AdFormatKey.Rewarded, filled);

            if (filled)
            {
                _hasClosedPlaceholder = false;
                ShowAdPlaceholder(AdPlaceholderType.Rewarded);
                if (placement != null)
                    network.ShowRewardedAd(placement);
                else
                    network.ShowRewardedAd();

                _frequencyCap?.RecordImpression(AdFormatKey.Rewarded);
            }
            else
            {
                TryRewardedFallback(network, placement);
            }
        }
    }
}
