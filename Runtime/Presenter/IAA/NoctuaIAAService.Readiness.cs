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
        // --- App Open Ad public methods ---

        /// <summary>Shows an app open ad (tries primary then secondary network).</summary>
        public void ShowAppOpenAd()
        {
            _log.Info($"{LogTag} show_app_open - show app-open ad");
            if (_appOpenAds == null)
            {
                _log.Warning("App Open ad manager not configured.");
                return;
            }
            _appOpenAds.ShowAppOpenAd();
        }

        /// <summary>Returns whether an app open ad is ready on any network.</summary>
        public bool IsAppOpenAdReady()
        {
            _log.Debug($"{LogTag} is_app_open_ready - check app-open ad ready");
            return _appOpenAds?.IsAppOpenAdReady() ?? false;
        }

        /// <summary>
        /// Returns <c>true</c> if an interstitial ad is loaded and ready to display,
        /// and the current frequency cap and cooldown allow showing it.
        /// Use this to conditionally show UI elements like "Watch Ad" buttons.
        /// </summary>
        public bool IsInterstitialReady()
        {
            _log.Debug($"{LogTag} is_interstitial_ready - check interstitial ready");
            if (_routing == null) return false;

            // A configured cross-promotion creative is served by ShowInterstitial() as a house-ad
            // fallback whenever no real ad displays — so report ready to keep the game unblocked even
            // when the network is dry or frequency-capped (parity with the actual show path).
            if (IsCrossPromoAvailable(AdPlaceholderType.Interstitial)) return true;

            if (_frequencyCap != null && !_frequencyCap.CanShowAd(AdFormatKey.Interstitial)) return false;

            var preferred = _routing.GetNetworkForFormat(AdFormatKey.Interstitial);
            if (IsInterstitialReadyOnNetwork(preferred)) return true;

            // When ad_format_overrides explicitly pins interstitial to a network, only that
            // network's readiness counts — the secondary is a last-resort fallback, not the
            // intended source. Returning secondary-ready here would make IsInterstitialReady()
            // return true and then ShowInterstitial() silently fall back to the secondary network,
            // which is confusing (e.g. MAX ad shown when config says admob).
            if (_routing.HasFormatOverride(AdFormatKey.Interstitial)) return false;

            // No override → check secondary so "Watch Ad" buttons stay enabled when only
            // secondary has fill and the primary is temporarily dry.
            var secondary = _routing.Secondary;
            return secondary != null && IsInterstitialReadyOnNetwork(secondary);
        }

        private bool IsInterstitialReadyOnNetwork(IAdNetwork network)
        {
            _log.Debug($"{LogTag} is_interstitial_ready_on_network - check interstitial ready on network");
#if UNITY_ADMOB && !UNITY_EDITOR
            if (IsAdmobNetwork(network) && _preloader != null)
                return _preloader.IsAdAvailable(_interstitialAdUnitID, AdFormat.INTERSTITIAL);
#endif
            return network.IsInterstitialReady();
        }

        /// <summary>
        /// Returns <c>true</c> if a rewarded ad is loaded and ready to display,
        /// and the current frequency cap and cooldown allow showing it.
        /// Use this to conditionally show UI elements like "Watch Ad" buttons.
        /// </summary>
        public bool IsRewardedAdReady()
        {
            _log.Debug($"{LogTag} is_rewarded_ready - check rewarded ad ready");
            if (_routing == null) return false;

            // A configured cross-promotion creative is served by ShowRewardedAd() as a house-ad
            // fallback whenever no real ad displays — so report ready to keep the game unblocked.
            // NOTE: the cross-promo grants NO reward (it fires OnAdClosed, never OnUserEarnedReward),
            // so treat a rewarded "ready" as "something will show", not "a reward is guaranteed".
            if (IsCrossPromoAvailable(AdPlaceholderType.Rewarded)) return true;

            if (_frequencyCap != null && !_frequencyCap.CanShowAd(AdFormatKey.Rewarded)) return false;

            var preferred = _routing.GetNetworkForFormat(AdFormatKey.Rewarded);
            if (IsRewardedReadyOnNetwork(preferred)) return true;

            // Same reasoning as IsInterstitialReady: an explicit override pins the format to
            // one network; secondary readiness would cause ShowRewardedAd() to silently serve
            // from the wrong network.
            if (_routing.HasFormatOverride(AdFormatKey.Rewarded)) return false;

            // No override → check secondary so "Watch Ad" buttons stay enabled when only
            // secondary has fill and the primary is temporarily dry.
            var secondary = _routing.Secondary;
            return secondary != null && IsRewardedReadyOnNetwork(secondary);
        }

        private bool IsRewardedReadyOnNetwork(IAdNetwork network)
        {
            _log.Debug($"{LogTag} is_rewarded_ready_on_network - check rewarded ready on network");
#if UNITY_ADMOB && !UNITY_EDITOR
            if (IsAdmobNetwork(network) && _preloader != null)
                return _preloader.IsAdAvailable(_rewardedAdUnitID, AdFormat.REWARDED);
#endif
            return network.IsRewardedAdReady();
        }

        /// <summary>
        /// Returns <c>true</c> when a cross-promotion creative is configured for the given format AND
        /// its asset is already cached locally — i.e. <c>Show*()</c> can serve the house-ad placeholder
        /// as a fallback and render it immediately. Requiring the cache hit (not just configuration)
        /// keeps the <c>Is*Ready</c> checkers honest: a configured-but-uncached creative would flash a
        /// blank placeholder while it downloads (or fail when offline), so it is not reported ready.
        /// </summary>
        private bool IsCrossPromoAvailable(AdPlaceholderType type)
        {
            if (_adPlaceholderUI == null) return false;

            var crossPromotion = IAAResponse?.CrossPromotion;
            if (crossPromotion == null) return false;

            var entry = ResolveCrossPromotionEntry(crossPromotion, type);
            if (entry == null || string.IsNullOrEmpty(entry.AssetUrl)) return false;

            return _adPlaceholderUI.IsAssetCached(entry.AssetUrl);
        }

        /// <summary>Handles app foreground transitions for app open ad auto-show.</summary>
        public void OnApplicationForeground()
        {
            _log.Debug($"{LogTag} on_application_foreground - handle app foreground");
            _appOpenAds?.OnApplicationForeground();
        }

        // --- Debugger methods ---

        /// <summary>Opens the ad network's creative debugger UI. Always routes to AppLovin.</summary>
        public void ShowCreativeDebugger()
        {
            _log.Debug($"{LogTag} show_creative_debugger - show creative debugger");
            if (_routing == null) return;

            // Creative Debugger is AppLovin-only. Find the AppLovin network.
            if (_routing.Primary.NetworkName == AdNetworkName.AppLovin)
            {
                _routing.Primary.ShowCreativeDebugger();
                return;
            }

            if (_routing.Secondary != null && _routing.Secondary.NetworkName == AdNetworkName.AppLovin)
            {
                _routing.Secondary.ShowCreativeDebugger();
                return;
            }

            _log.Warning("Creative Debugger requires AppLovin SDK. AppLovin is not installed or not configured.");
        }

        /// <summary>
        /// Opens the mediation debugger UI. Uses primary network, falls back to secondary if available.
        /// </summary>
        public void ShowMediationDebugger()
        {
            _log.Debug($"{LogTag} show_mediation_debugger - show mediation debugger");
            ShowMediationDebugger(null);
        }

        /// <summary>
        /// Opens the mediation debugger UI for a specific ad network.
        /// If <paramref name="networkName"/> is null or empty, uses primary network first,
        /// then falls back to secondary if primary is not available.
        /// Use <see cref="AdNetworkName.Admob"/> or <see cref="AdNetworkName.AppLovin"/> as the network name.
        /// </summary>
        /// <param name="networkName">The network name (e.g., "admob" or "applovin"), or null to use primary.</param>
        public void ShowMediationDebugger(string networkName)
        {
            _log.Debug($"{LogTag} show_mediation_debugger_network - show mediation debugger (network)");
            if (_routing == null)
            {
                _log.Warning("Cannot show mediation debugger — routing not initialized.");
                return;
            }

            // If no network specified, use primary; fall back to secondary
            if (string.IsNullOrEmpty(networkName))
            {
                _log.Info($"No network specified. Using primary network: {_routing.Primary.NetworkName}");
                _routing.Primary.ShowMediationDebugger();
                return;
            }

            var normalizedName = networkName.ToLowerInvariant();

            if (_routing.Primary.NetworkName == normalizedName)
            {
                _routing.Primary.ShowMediationDebugger();
                return;
            }

            if (_routing.Secondary != null && _routing.Secondary.NetworkName == normalizedName)
            {
                _routing.Secondary.ShowMediationDebugger();
                return;
            }

            // Requested network not found — fall back to primary, then secondary
            _log.Warning($"Network '{networkName}' is not configured. Falling back to primary: {_routing.Primary.NetworkName}");
            _routing.Primary.ShowMediationDebugger();
        }

        /// <summary>
        /// Sets test device IDs on all configured ad networks.
        /// For AdMob: registers via RequestConfiguration.TestDeviceIds.
        /// For AppLovin: registers via MaxSdk.SetTestDeviceAdvertisingIdentifiers.
        /// Call this before showing ads to receive test ads on specified devices.
        /// </summary>
        /// <param name="testDeviceIds">List of device IDs (AdMob device ID or advertising ID for AppLovin).</param>
        public void SetTestDeviceIds(List<string> testDeviceIds)
        {
            _log.Debug($"{LogTag} set_test_device_ids - set test device IDs");
            if (_routing == null)
            {
                _log.Warning("Cannot set test device IDs — routing not initialized.");
                return;
            }

            _routing.Primary.SetTestDeviceIds(testDeviceIds);
            _routing.Secondary?.SetTestDeviceIds(testDeviceIds);

            _log.Info($"Test device IDs set on all networks: [{string.Join(", ", testDeviceIds)}]");
        }
    }
}
