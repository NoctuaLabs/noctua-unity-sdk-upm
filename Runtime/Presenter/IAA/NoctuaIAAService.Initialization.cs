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
        /// <summary>
        /// Initializes the ad mediation SDK based on the configured mediation type.
        /// </summary>
        public void Initialize(Action initCompleteAction = null)
        {
            _log.Debug($"{LogTag} initialize - initialize networks and routing");
            if (IAAResponse == null)
            {
                _log.Error("Cannot initialize NoctuaIAAService: IAA response is null.");
                return;
            }

            if (_routing == null)
            {
                _log.Error("Cannot initialize NoctuaIAAService: routing not created.");
                return;
            }

            _log.Info("Initializing Ad Mediation : " + IAAResponse.Mediation);

            var mediationType =
            #if UNITY_APPLOVIN
                AdNetworkName.AppLovin;
            #elif UNITY_ADMOB
                AdNetworkName.Admob;
            #else
                "unknown";
            #endif

            _mediationType = !string.IsNullOrEmpty(IAAResponse.Mediation)
                ? IAAResponse.Mediation
                : mediationType;

            if (string.IsNullOrEmpty(_mediationType))
            {
                _log.Error("Mediation type is empty or null.");
                return;
            }

            SubscribeToRoutingEvents();
            SubscribeToNetworkSpecificEvents();

            // Use routing to initialize both networks concurrently.
            // Primary callback fires when primary SDK is ready → load primary ads.
            // Secondary callback fires when secondary SDK is ready → load secondary ads.
            // Never call Load on a network before its own init callback fires (per AdMob and AppLovin docs).
            _routing.Initialize(
                onPrimaryReady: () =>
                {
                    _log.Info("Primary network initialized: " + _routing.Primary.NetworkName);

                    if (IAAResponse.AdFormat == null && (IAAResponse.Networks == null || IAAResponse.Networks.Count == 0))
                    {
                        _log.Info("No ad format config. Cannot proceed with ad unit ID setup.");
                        initCompleteAction?.Invoke();
                        return;
                    }

                    if (IsAdmob())
                    {
#if UNITY_ADMOB
                        _preloader = _admobPreloader;
#endif
                    }

                    SetupAdUnitID(IAAResponse);
                    initCompleteAction?.Invoke();
                },
                onSecondaryReady: () =>
                {
                    var secondary = _routing.Secondary;
                    if (secondary == null || IAAResponse == null) return;

                    _log.Info("Secondary network initialized: " + secondary.NetworkName);
                    SetupSecondaryAdUnits(IAAResponse, secondary);
                    SetupSecondaryAppOpen(IAAResponse, secondary);
                });
        }

        private void SubscribeToRoutingEvents()
        {
            _log.Debug($"{LogTag} subscribe_orchestrator_events - subscribe routing events");
            if (_adNetworkEventsSubscribed) return;
            _adNetworkEventsSubscribed = true;

            _routing.OnAdDisplayed += () => PostToMainThread(() =>
            {
                CloseAdPlaceholder(force: true);
                _appOpenAds?.SetFullscreenAdShowing(true);
                _onAdDisplayed?.Invoke();
            });

            // Subscribe to the format-carrying failure event, never the format-agnostic one: the
            // failing format decides which cross-promotion surface (if any) stands in for it.
            // Guessing from _lastRequestedType made every banner auto-refresh load failure — which
            // fires unprompted while the user is idle — pop the *fullscreen* interstitial house ad.
            _routing.OnAdFailedDisplayedFormat += format => PostToMainThread(() =>
            {
                // Only a fullscreen ad occupied the screen, so only a fullscreen failure releases
                // the app-open guard. A banner failure never blocked app-open in the first place.
                if (format != AdFormatKey.Banner)
                {
                    _appOpenAds?.SetFullscreenAdShowing(false);
                }

                // Show the cross-promotion fallback for the format that actually failed; only report
                // failure to the game if it can't show. A banner failure routes to the non-modal
                // banner house ad; app_open has no cross-promotion surface and reports directly.
                var placeholderType = MapFormatToPlaceholderType(format);

                if (placeholderType.HasValue && ShowCrossPromoFallback(placeholderType.Value)) return;

                _onAdFailedDisplayed?.Invoke();
            });

            _routing.OnAdClicked += () => PostToMainThread(() => _onAdClicked?.Invoke());
            _routing.OnAdImpressionRecorded += () => PostToMainThread(() => _onAdImpressionRecorded?.Invoke());

            _routing.OnAdClosed += () => PostToMainThread(() =>
            {
                _appOpenAds?.SetFullscreenAdShowing(false);
                _onAdClosed?.Invoke();
            });
        }

        private void SubscribeToNetworkSpecificEvents()
        {
            _log.Debug($"{LogTag} subscribe_network_events - subscribe network-specific events");
            var primary = _routing.Primary;
            var secondary = _routing.Secondary;

            // Platform-agnostic reward-completion tracking — not gated by #if so it works
            // in Editor tests and covers both mediation networks with a single subscription.
            SubscribeRewardCompletionEvent(primary);
            if (secondary != null) SubscribeRewardCompletionEvent(secondary);

#if UNITY_ADMOB
            SubscribeAdmobRevenueEvents(primary);
            if (secondary != null) SubscribeAdmobRevenueEvents(secondary);
#endif

#if UNITY_APPLOVIN
            SubscribeAppLovinRevenueEvents(primary);
            if (secondary != null) SubscribeAppLovinRevenueEvents(secondary);
#endif
        }

        /// <summary>
        /// Subscribes to the platform-agnostic <see cref="IAdNetwork.OnUserEarnedReward"/> event and
        /// emits an <c>ad_rewarded_complete</c> analytics event. Dispatches to the main thread because
        /// both AppLovin MAX (background thread) and AdMob may fire the callback off the Unity thread.
        /// </summary>
        private void SubscribeRewardCompletionEvent(IAdNetwork network)
        {
            _log.Debug($"{LogTag} subscribe_reward_completion - subscribe reward completion event");
            network.OnUserEarnedReward += (amount, type) => PostToMainThread(() =>
            {
                _adRevenueTracker?.TrackCustomEvent("ad_rewarded_complete", new Dictionary<string, IConvertible>
                {
                    { "network",       network.NetworkName },
                    { "reward_amount", amount },
                    { "reward_type",   type ?? "" }
                });
            });
        }

#if UNITY_ADMOB
        private void SubscribeAdmobRevenueEvents(IAdNetwork network)
        {
            _log.Debug($"{LogTag} subscribe_admob_revenue - subscribe AdMob revenue events");
            if (network.NetworkName != AdNetworkName.Admob) return;

            network.AdmobOnUserEarnedReward += (reward) => _admobOnUserEarnedReward?.Invoke(reward);

            // Per-format routing: the aggregate AdmobOnAdRevenuePaid event does not carry
            // format info, so Taichi counters and performance tracker would be misattributed
            // (everything was previously counted as banner). AdmobNetwork now exposes
            // per-format revenue events; subscribe to each and route to the correct
            // Process*Thresholds / RecordRevenue call.
            //
            // Note: the preload path for interstitial/rewarded uses a raw AdMob ad object
            // (not _interstitialAdmob/_rewardedAdmob), so OnAdPaid is wired directly in
            // RegisterCallbackAdInterstitial / RegisterCallbackAdRewarded. The per-format
            // events below fire for the legacy (non-preload) paths, including when AdMob
            // is the secondary network.
            if (network is AdmobNetwork admobNetwork)
            {
                admobNetwork.AdmobOnBannerRevenuePaid += (adValue, responseInfo) =>
                {
                    PostToMainThread(() =>
                    {
                        _revenueTracker.ProcessAdmobRevenue(adValue, responseInfo);
                        _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo);
                        if (_performanceTracker != null)
                        {
                            double revenue = adValue.Value / 1_000_000.0;
                            _performanceTracker.RecordRevenue(AdNetworkName.Admob, AdFormatKey.Banner, revenue);
                        }
                    });
                };

                admobNetwork.AdmobOnInterstitialRevenuePaid += (adValue, responseInfo) =>
                {
                    PostToMainThread(() =>
                    {
                        _revenueTracker.ProcessAdmobInterstitialRevenue(adValue, responseInfo);
                        _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo);
                        if (_performanceTracker != null)
                        {
                            double revenue = adValue.Value / 1_000_000.0;
                            _performanceTracker.RecordRevenue(AdNetworkName.Admob, AdFormatKey.Interstitial, revenue);
                        }
                    });
                };

                admobNetwork.AdmobOnRewardedRevenuePaid += (adValue, responseInfo) =>
                {
                    PostToMainThread(() =>
                    {
                        _revenueTracker.ProcessAdmobRewardedRevenue(adValue, responseInfo);
                        _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo);
                        if (_performanceTracker != null)
                        {
                            double revenue = adValue.Value / 1_000_000.0;
                            _performanceTracker.RecordRevenue(AdNetworkName.Admob, AdFormatKey.Rewarded, revenue);
                        }
                    });
                };

                admobNetwork.AdmobOnRewardedInterstitialRevenuePaid += (adValue, responseInfo) =>
                {
                    PostToMainThread(() =>
                    {
                        // Rewarded interstitial is counted under the all-formats path only
                        // (not Step 3/5/6) — matches the existing behavior at line ~631.
                        _revenueTracker.ProcessAdmobRevenue(adValue, responseInfo);
                        _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo);
                        if (_performanceTracker != null)
                        {
                            double revenue = adValue.Value / 1_000_000.0;
                            _performanceTracker.RecordRevenue(AdNetworkName.Admob, AdFormatKey.RewardedInterstitial, revenue);
                        }
                    });
                };

                admobNetwork.AdmobOnAppOpenRevenuePaid += (adValue, responseInfo) =>
                {
                    PostToMainThread(() =>
                    {
                        _revenueTracker.ProcessAdmobRevenue(adValue, responseInfo);
                        _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo);
                        if (_performanceTracker != null)
                        {
                            double revenue = adValue.Value / 1_000_000.0;
                            _performanceTracker.RecordRevenue(AdNetworkName.Admob, AdFormatKey.AppOpen, revenue);
                        }
                    });
                };
            }
            else
            {
                // Fallback — if the network isn't AdmobNetwork (e.g. a test double),
                // keep the legacy aggregate wiring so revenue still flows to external
                // subscribers, but skip Taichi/performance attribution since we can't
                // identify the format.
                network.AdmobOnAdRevenuePaid += (adValue, responseInfo) =>
                {
                    PostToMainThread(() =>
                    {
                        _revenueTracker.ProcessAdmobRevenue(adValue, responseInfo);
                        _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo);
                    });
                };
            }
        }
#endif

#if UNITY_APPLOVIN
        private void SubscribeAppLovinRevenueEvents(IAdNetwork network)
        {
            _log.Debug($"{LogTag} subscribe_applovin_revenue - subscribe AppLovin revenue events");
            if (network.NetworkName != AdNetworkName.AppLovin) return;

            network.AppLovinOnUserEarnedReward += (reward) => PostToMainThread(() => _appLovinOnUserEarnedReward?.Invoke(reward));
            // AppLovin MAX delivers OnAdRevenuePaidEvent on a background thread
            // (MaxSdkBase.HandleBackgroundCallback). ProcessAppLovinRevenue reads
            // PlayerPrefs via the Taichi threshold helpers, which is main-thread-only
            // — without this hop the impression throws and revenue is lost.
            network.AppLovinOnAdRevenuePaid += (adInfo) => PostToMainThread(() =>
            {
                _revenueTracker.ProcessAppLovinRevenue(adInfo);
                _appLovinOnAdRevenuePaid?.Invoke(adInfo);

                // Feed dynamic-optimization tracker with per-format revenue.
                if (_performanceTracker != null && adInfo != null)
                {
                    string rawFormat = adInfo.AdFormat ?? "";
                    string format = MapAppLovinFormatToKey(rawFormat);
                    _performanceTracker.RecordRevenue(AdNetworkName.AppLovin, format, adInfo.Revenue);
                }
            });
        }

        /// <summary>Maps AppLovin AdInfo.AdFormat strings to <see cref="AdFormatKey"/> constants.</summary>
        private static string MapAppLovinFormatToKey(string appLovinFormat)
        {
            _sLog.Debug($"{LogTag} map_applovin_format - map AppLovin format to key");
            return appLovinFormat.ToUpperInvariant() switch
            {
                "INTER" or "INTERSTITIAL"                          => AdFormatKey.Interstitial,
                "REWARDED" or "REWARDED_VIDEO" or "REWARDEDVIDEO" => AdFormatKey.Rewarded,
                "BANNER" or "MREC"                                 => AdFormatKey.Banner,
                "APPOPEN" or "APP_OPEN"                            => AdFormatKey.AppOpen,
                _                                                  => AdFormatKey.Banner
            };
        }
#endif
    }
}
