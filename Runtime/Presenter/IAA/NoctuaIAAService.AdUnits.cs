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
        /// Configures ad unit IDs for all ad formats based on the IAA server response, then loads initial ads.
        /// </summary>
        public void SetupAdUnitID(IAA iAAResponse)
        {
            _log.Debug($"{LogTag} setup_ad_unit_id - resolve and assign ad-unit IDs");
            var primary = _routing.Primary;
            var secondary = _routing.Secondary;

            ResolveAdUnitIDs(iAAResponse, primary.NetworkName);

            if (!string.IsNullOrEmpty(_bannerAdUnitID) && _bannerAdUnitID != "unknown")
            {
                primary.SetBannerAdUnitId(_bannerAdUnitID);
            }
            else
            {
                _log.Info($"Banner ad unit ID is missing for {primary.NetworkName}; skipping banner setup.");
            }

            if (IsAdmob() && primary.NetworkName == AdNetworkName.Admob)
            {
#if UNITY_ADMOB
                // All AdMob fullscreen formats use the Preload API exclusively.
                // Do NOT call primary.SetRewardedInterstitialAdUnitID() / LoadRewardedInterstitialAd()
                // or AppOpenAd.Load() — mixing preload and legacy paths for the same ad unit
                // causes race conditions per AdMob docs.

                // _preloader already assigned in Initialize() callback; reuse the same instance.
                // RewardedInterstitialAd does NOT support the Preload API — use legacy Load() path.
                if (!string.IsNullOrEmpty(_rewardedInterstitialAdUnitID) && _rewardedInterstitialAdUnitID != "unknown")
                {
                    // A new config runs this again: destroy the previous ad before replacing it.
                    _rewardedInterstitialAdmob?.CleanupAd();
                    _rewardedInterstitialAdmob = new RewardedInterstitialAdmob(_adRevenueTracker);
                    _rewardedInterstitialAdmob.SetRewardedInterstitialAdUnitID(_rewardedInterstitialAdUnitID);
                    _rewardedInterstitialAdmob.RewardedOnAdDisplayed += () => { CloseAdPlaceholder(force: true); _frequencyCap?.RecordImpression(AdFormatKey.RewardedInterstitial); _onAdDisplayed?.Invoke(); };
                    _rewardedInterstitialAdmob.RewardedOnAdFailedDisplayed += () => { if (!ShowCrossPromoFallback(AdPlaceholderType.RewardedInterstitial)) _onAdFailedDisplayed?.Invoke(); };
                    _rewardedInterstitialAdmob.RewardedOnAdClosed += () => _onAdClosed?.Invoke();
                    _rewardedInterstitialAdmob.RewardedOnAdClicked += () => _onAdClicked?.Invoke();
                    _rewardedInterstitialAdmob.RewardedOnAdImpressionRecorded += () => _onAdImpressionRecorded?.Invoke();
                    _rewardedInterstitialAdmob.RewardedOnUserEarnedReward += reward => _admobOnUserEarnedReward?.Invoke(reward);
                    _rewardedInterstitialAdmob.AdmobOnAdRevenuePaid += (adValue, responseInfo) => PostToMainThread(() =>
                    {
                        _revenueTracker.ProcessAdmobRevenue(adValue, responseInfo);
                        _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo);
                    });
                    _rewardedInterstitialAdmob.LoadRewardedInterstitialAd();
                }

#if !UNITY_EDITOR
                // Device only: AdMob Preload API. Not supported in the Unity Editor.
                var configs = new List<PreloadConfiguration>();

                if (!string.IsNullOrEmpty(_interstitialAdUnitID) && _interstitialAdUnitID != "unknown")
                {
                    configs.Add(_preloader.CreateInterstitialPreloadConfig(_interstitialAdUnitID));
                }
                else
                {
                    _log.Info("Interstitial ad unit ID is missing for AdMob; skipping interstitial preload.");
                }

                if (!string.IsNullOrEmpty(_rewardedAdUnitID) && _rewardedAdUnitID != "unknown")
                {
                    configs.Add(_preloader.CreateRewardedPreloadConfig(_rewardedAdUnitID));
                }
                else
                {
                    _log.Info("Rewarded ad unit ID is missing for AdMob; skipping rewarded preload.");
                }

                // App Open: buffer=1 is recommended (Google docs: app open shown once per foreground).
                if (!string.IsNullOrEmpty(_appOpenAdUnitID) && _appOpenAdUnitID != "unknown")
                {
                    configs.Add(_preloader.CreateAppOpenPreloadConfig(_appOpenAdUnitID, bufferSize: 1));
                }

                if (!_preloadManagerEventsSubscribed)
                {
                    _preloadManagerEventsSubscribed = true;

                    _preloader.OnAdsAvailable += (config) =>
                    {
                        _log.Debug($"Ad available for {config.Format}");
                        _onAdsAvailable?.Invoke(config);
                    };

                    _preloader.OnAdExhausted += (config) =>
                    {
                        _log.Debug($"Ad exhausted for {config.Format}");
                        _onAdExhausted?.Invoke(config);
                    };
                }

                if (configs.Count > 0)
                {
                    _preloader.StartPreloading(configs);
                }
                else
                {
                    _log.Warning("No AdMob fullscreen ad unit IDs configured; skipping preload.");
                }
#else
                // Editor: Preload API not supported. Use the legacy load path so
                // IsInterstitialReady() / IsRewardedAdReady() can return true and
                // ShowAdmobInterstitial / ShowAdmobRewarded can show the ad.
                if (!string.IsNullOrEmpty(_interstitialAdUnitID) && _interstitialAdUnitID != "unknown")
                {
                    primary.SetInterstitialAdUnitID(_interstitialAdUnitID);
                    primary.LoadInterstitialAd();
                }
                if (!string.IsNullOrEmpty(_rewardedAdUnitID) && _rewardedAdUnitID != "unknown")
                {
                    primary.SetRewardedAdUnitID(_rewardedAdUnitID);
                    primary.LoadRewardedAd();
                }
#endif
#endif
            }
            else
            {
                if (!string.IsNullOrEmpty(_interstitialAdUnitID) && _interstitialAdUnitID != "unknown")
                {
                    primary.SetInterstitialAdUnitID(_interstitialAdUnitID);
                    primary.LoadInterstitialAd();
                }
                else
                {
                    _log.Info($"Interstitial ad unit ID is missing for {primary.NetworkName}; skipping interstitial load.");
                }

                if (!string.IsNullOrEmpty(_rewardedAdUnitID) && _rewardedAdUnitID != "unknown")
                {
                    primary.SetRewardedAdUnitID(_rewardedAdUnitID);
                    primary.LoadRewardedAd();
                }
                else
                {
                    _log.Info($"Rewarded ad unit ID is missing for {primary.NetworkName}; skipping rewarded load.");
                }
            }

            // NOTE: Secondary ad units are NOT loaded here.
            // They are loaded in the onSecondaryReady callback in Initialize(),
            // which only fires after the secondary SDK has fully initialized.

            // Configure App Open ads (primary only; secondary added in SetupSecondaryAppOpen)
            SetupAppOpenAds(iAAResponse);

            _onInitialized?.Invoke();
            _log.Info("Ad Unit IDs set up for mediation type: " + _mediationType);
        }

        private void SetupSecondaryAdUnits(IAA iAAResponse, IAdNetwork secondary)
        {
            _log.Debug($"{LogTag} setup_secondary_ad_units - setup secondary network ad units");
            string secondaryInterstitial = ResolveAdUnitIdForNetwork(iAAResponse, secondary.NetworkName, AdFormatKey.Interstitial);
            string secondaryRewarded = ResolveAdUnitIdForNetwork(iAAResponse, secondary.NetworkName, AdFormatKey.Rewarded);
            string secondaryBanner = ResolveAdUnitIdForNetwork(iAAResponse, secondary.NetworkName, AdFormatKey.Banner);

            if (secondaryInterstitial != "unknown")
            {
                secondary.SetInterstitialAdUnitID(secondaryInterstitial);
                secondary.LoadInterstitialAd();
            }

            if (secondaryRewarded != "unknown")
            {
                secondary.SetRewardedAdUnitID(secondaryRewarded);
                secondary.LoadRewardedAd();
            }

            if (secondaryBanner != "unknown")
            {
                secondary.SetBannerAdUnitId(secondaryBanner);
            }
        }

        /// <summary>
        /// Sets up App Open ads on the primary network.
        /// Secondary App Open is normally wired in <see cref="SetupSecondaryAppOpen"/> once the secondary SDK is ready.
        /// If secondary initialized before primary (e.g. iOS AppLovin before AdMob), the cached
        /// <c>_pendingSecondaryAppOpenId</c> is applied immediately after the manager is created.
        /// </summary>
        private void SetupAppOpenAds(IAA iAAResponse)
        {
            _log.Debug($"{LogTag} setup_app_open_ads - setup app-open ad units");
            string primaryAppOpenId = ResolveAdUnitIdForNetwork(iAAResponse, _routing.Primary.NetworkName, AdFormatKey.AppOpen);

            // NOTE: intentionally do NOT bail out when primaryAppOpenId is empty — the manager
            // must still be created so a secondary-only App Open unit (set later via
            // SetupSecondaryAppOpen, or applied immediately below from _pendingSecondaryAppOpenId)
            // has something to attach to. AppOpenAds.Configure() already no-ops gracefully
            // per-network when an id is empty/"unknown".

            // Resolve preferred network from format overrides (null = primary first, default behaviour).
            string preferredAppOpenNetwork = null;
            iAAResponse.AdFormatOverrides?.TryGetValue(AdFormatKey.AppOpen, out preferredAppOpenNetwork);

            _appOpenAds = new AppOpenAds(
                primaryNetwork: _routing.Primary,
                secondaryNetwork: _routing.Secondary,
                frequencyCap: _frequencyCap,
                autoShowOnForeground: iAAResponse.AppOpenAutoShow ?? false,
                preferredNetworkName: preferredAppOpenNetwork,
                onAdNotAvailable: format => NotifyAdNotAvailable(format)
            );

            // Only pass primary here; secondary will be added in SetupSecondaryAppOpen after secondary SDK is ready.
            // Exception: if secondary already initialized before primary (e.g. iOS AppLovin before AdMob),
            // _pendingSecondaryAppOpenId was cached in SetupSecondaryAppOpen — apply it now.
            _appOpenAds.Configure(primaryAppOpenId, null);

            if (!string.IsNullOrEmpty(_pendingSecondaryAppOpenId))
            {
                _log.Info($"Applying cached secondary App Open unit: {_pendingSecondaryAppOpenId}");
                _appOpenAds.ConfigureSecondary(_pendingSecondaryAppOpenId);
                _pendingSecondaryAppOpenId = null;
            }
        }

        /// <summary>
        /// Adds secondary App Open ad support after the secondary SDK has finished initialization.
        /// Safe to call only from the onSecondaryReady callback.
        /// </summary>
        private void SetupSecondaryAppOpen(IAA iAAResponse, IAdNetwork secondary)
        {
            _log.Debug($"{LogTag} setup_secondary_app_open - setup secondary app-open ad units");
            string secondaryAppOpenId = ResolveAdUnitIdForNetwork(iAAResponse, secondary.NetworkName, AdFormatKey.AppOpen);

            if (string.IsNullOrEmpty(secondaryAppOpenId) || secondaryAppOpenId == "unknown")
                return;

            if (_appOpenAds == null)
            {
                // Secondary initialized before primary (e.g. iOS: AppLovin before AdMob).
                // Cache the ID so SetupAppOpenAds() can apply it once the manager is created.
                _log.Info($"App Open secondary unit cached (manager not ready yet): {secondaryAppOpenId}");
                _pendingSecondaryAppOpenId = secondaryAppOpenId;
                return;
            }

            _appOpenAds.ConfigureSecondary(secondaryAppOpenId);
        }

        private void ResolveAdUnitIDs(IAA iAAResponse, string networkName)
        {
            _log.Debug($"{LogTag} resolve_ad_unit_ids - resolve ad-unit IDs for network");
            _interstitialAdUnitID = ResolveAdUnitIdForNetwork(iAAResponse, networkName, AdFormatKey.Interstitial);
            _rewardedAdUnitID = ResolveAdUnitIdForNetwork(iAAResponse, networkName, AdFormatKey.Rewarded);
            _rewardedInterstitialAdUnitID = ResolveAdUnitIdForNetwork(iAAResponse, networkName, AdFormatKey.RewardedInterstitial);
            _bannerAdUnitID = ResolveAdUnitIdForNetwork(iAAResponse, networkName, AdFormatKey.Banner);
            _appOpenAdUnitID = ResolveAdUnitIdForNetwork(iAAResponse, networkName, AdFormatKey.AppOpen);
        }

        /// <summary>
        /// Resolves ad unit ID for a given network and format.
        /// Priority: networks block → flat ad_formats → "unknown".
        /// </summary>
        private string ResolveAdUnitIdForNetwork(IAA iAAResponse, string networkName, string format)
        {
            _log.Debug($"{LogTag} resolve_ad_unit_id_for_network - resolve ad-unit ID for network/format");
            // Try networks block first
            if (iAAResponse.Networks != null &&
                iAAResponse.Networks.TryGetValue(networkName, out var networkConfig) &&
                networkConfig?.AdFormat != null)
            {
                string id = GetAdUnitIdFromFormat(networkConfig.AdFormat, format);
                if (!string.IsNullOrEmpty(id) && id != "unknown")
                {
                    return id;
                }
            }

            // Fallback to flat ad_formats (backward compat)
            if (iAAResponse.AdFormat != null)
            {
                string id = GetAdUnitIdFromFormat(iAAResponse.AdFormat, format);
                if (!string.IsNullOrEmpty(id))
                {
                    return id;
                }
            }

            return "unknown";
        }

        private string GetAdUnitIdFromFormat(AdFormatNoctua adFormat, string format)
        {
            _log.Debug($"{LogTag} get_ad_unit_id_from_format - get ad-unit ID from format");
            AdUnit adUnit = null;

            switch (format)
            {
                case AdFormatKey.Interstitial:
                    adUnit = adFormat.Interstitial;
                    break;
                case AdFormatKey.Rewarded:
                    adUnit = adFormat.Rewarded;
                    break;
                case AdFormatKey.RewardedInterstitial:
                    adUnit = adFormat.RewardedInterstitial;
                    break;
                case AdFormatKey.Banner:
                    adUnit = adFormat.Banner;
                    break;
                case AdFormatKey.AppOpen:
                    adUnit = adFormat.AppOpen;
                    break;
            }

            if (adUnit == null) return "unknown";

#if UNITY_ANDROID
            return string.IsNullOrEmpty(adUnit.Android?.adUnitID) ? "unknown" : adUnit.Android.adUnitID;
#elif UNITY_IPHONE
            return string.IsNullOrEmpty(adUnit.IOS?.adUnitID) ? "unknown" : adUnit.IOS.adUnitID;
#else
            return "unknown";
#endif
        }
    }
}
