using System;
using System.Threading;
using UnityEngine;
using System.Collections.Generic;
using com.noctuagames.sdk.AdPlaceholder;

#if UNITY_ADMOB
using GoogleMobileAds.Api;
using static GoogleMobileAds.Api.AdValue;
using com.noctuagames.sdk.Admob;
#endif

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Manages ad mediation across multiple ad networks (AdMob, AppLovin), handling initialization,
    /// ad loading, display, revenue tracking, and hybrid fallback orchestration.
    /// </summary>
    public class MediationManager
    {
        private readonly NoctuaLogger _log = new(typeof(MediationManager));

        // Captured on the main thread in the constructor; used to dispatch AdMob preload
        // callbacks (which fire on the GMA JNI thread) back to Unity's main thread.
        private readonly SynchronizationContext _mainThreadContext;

        private HybridAdOrchestrator _orchestrator;
        private AdRevenueTrackingManager _revenueTracker;
        private AdFrequencyManager _frequencyManager;
        private AppOpenAdManager _appOpenAdManager;
        private AdNetworkPerformanceTracker _performanceTracker;
        private string _mediationType;

        // CPM floor + segmentation (preserved across CreateNetworks calls)
        private UserSegmentManager _segmentManager;
        private CpmFloorManager _cpmFloorManager;
        private string _cachedCountryCode;

        // Cached secondary app open unit ID for when secondary inits before primary (e.g. iOS: AppLovin before AdMob)
        private string _pendingSecondaryAppOpenId;

        // Private event handlers
        private event Action _onInitialized;
        private event Action _onAdDisplayed;
        private event Action _onAdFailedDisplayed;
        private event Action _onAdClicked;
        private event Action _onAdImpressionRecorded;
        private event Action _onAdClosed;

#if UNITY_ADMOB
        private event Action<Reward> _admobOnUserEarnedReward;
        private event Action<AdValue, ResponseInfo> _admobOnAdRevenuePaid;
#endif

#if UNITY_APPLOVIN
        private event Action<MaxSdk.Reward> _appLovinOnUserEarnedReward;
        private event Action<MaxSdkBase.AdInfo> _appLovinOnAdRevenuePaid;
#endif

        /// <summary>Fires when the ad mediation SDK finishes initialization.</summary>
        public event Action OnInitialized { add => _onInitialized += value; remove => _onInitialized -= value; }
        /// <summary>Fires when an ad is successfully displayed to the user.</summary>
        public event Action OnAdDisplayed { add => _onAdDisplayed += value; remove => _onAdDisplayed -= value; }
        /// <summary>Fires when an ad fails to display.</summary>
        public event Action OnAdFailedDisplayed { add => _onAdFailedDisplayed += value; remove => _onAdFailedDisplayed -= value; }
        /// <summary>Fires when the user clicks on a displayed ad.</summary>
        public event Action OnAdClicked { add => _onAdClicked += value; remove => _onAdClicked -= value; }
        /// <summary>Fires when an ad impression is recorded.</summary>
        public event Action OnAdImpressionRecorded { add => _onAdImpressionRecorded += value; remove => _onAdImpressionRecorded -= value; }
        /// <summary>Fires when a displayed ad is closed by the user.</summary>
        public event Action OnAdClosed { add => _onAdClosed += value; remove => _onAdClosed -= value; }

        private event Action<string> _onAdNotAvailable;

        /// <summary>
        /// Fired when a Show method is called but cannot display an ad because:
        /// the ad network has no fill, a frequency cap is active, a cooldown is active,
        /// or the format is disabled. The <c>string</c> argument is the ad format key
        /// (e.g., <c>"interstitial"</c>, <c>"rewarded"</c>, <c>"app_open"</c>).
        /// Subscribe to this event to update your UI (e.g. hide a "Watch Ad" button)
        /// or implement retry logic.
        /// </summary>
        public event Action<string> OnAdNotAvailable { add => _onAdNotAvailable += value; remove => _onAdNotAvailable -= value; }

#if UNITY_ADMOB
        /// <summary>Fires when the user earns a reward from an AdMob rewarded ad.</summary>
        public event Action<Reward> AdmobOnUserEarnedReward { add => _admobOnUserEarnedReward += value; remove => _admobOnUserEarnedReward -= value; }
        /// <summary>Fires when AdMob reports impression-level ad revenue data.</summary>
        public event Action<AdValue, ResponseInfo> AdmobOnAdRevenuePaid { add => _admobOnAdRevenuePaid += value; remove => _admobOnAdRevenuePaid -= value; }
#endif
#if UNITY_APPLOVIN
        /// <summary>Fires when the user earns a reward from an AppLovin rewarded ad.</summary>
        public event Action<MaxSdk.Reward> AppLovinOnUserEarnedReward { add => _appLovinOnUserEarnedReward += value; remove => _appLovinOnUserEarnedReward -= value; }
        /// <summary>Fires when AppLovin reports impression-level ad revenue data.</summary>
        public event Action<MaxSdkBase.AdInfo> AppLovinOnAdRevenuePaid { add => _appLovinOnAdRevenuePaid += value; remove => _appLovinOnAdRevenuePaid -= value; }
#endif

#if UNITY_ADMOB
        private static AdmobAdPreloadManager _preloadManager;
        private RewardedInterstitialAdmob _rewardedInterstitialAdmob;
        private event Action<PreloadConfiguration> _onAdsAvailable;
        private event Action<PreloadConfiguration> _onAdExhausted;

        /// <summary>Fires when a preloaded ad becomes available for display.</summary>
        public event Action<PreloadConfiguration> OnAdsAvailable { add => _onAdsAvailable += value; remove => _onAdsAvailable -= value; }
        /// <summary>Fires when all preloaded ads of a given configuration are exhausted.</summary>
        public event Action<PreloadConfiguration> OnAdExhausted { add => _onAdExhausted += value; remove => _onAdExhausted -= value; }
#endif

        private string _interstitialAdUnitID = "unused";
        private string _rewardedAdUnitID = "unused";
        private string _rewardedInterstitialAdUnitID = "unused";
        private string _bannerAdUnitID = "unused";
        private string _appOpenAdUnitID = "unused";

        /// <summary>Gets the configured interstitial ad unit ID for the current platform.</summary>
        public string InterstitialAdUnitID => _interstitialAdUnitID;
        /// <summary>Gets the configured rewarded ad unit ID for the current platform.</summary>
        public string RewardedAdUnitID => _rewardedAdUnitID;
        /// <summary>Gets the configured rewarded interstitial ad unit ID for the current platform (AdMob only).</summary>
        public string RewardedInterstitialAdUnitID => _rewardedInterstitialAdUnitID;
        /// <summary>Gets the configured banner ad unit ID for the current platform.</summary>
        public string BannerAdUnitID => _bannerAdUnitID;

        private readonly IAdPlaceholderUI _adPlaceholderUI;
        private IAdRevenueTracker _adRevenueTracker;
        private bool _hasClosedPlaceholder;
        private bool _adNetworkEventsSubscribed;
        private bool _preloadManagerEventsSubscribed;

        private IAA _iaaResponse;

        internal IAA IAAResponse
        {
            get => _iaaResponse;
            set
            {
                _iaaResponse = value;
                if (value != null)
                {
                    _adNetworkEventsSubscribed = false;
                    _preloadManagerEventsSubscribed = false;
                    CreateNetworks(value);
                }
            }
        }

        /// <summary>Returns the App Open ad manager for foreground auto-show control.</summary>
        public AppOpenAdManager AppOpenManager => _appOpenAdManager;

        /// <summary>Returns true if running in hybrid mode (both networks active).</summary>
        public bool IsHybridMode => _orchestrator?.IsHybridMode ?? false;

        /// <summary>Returns the active mediation type string.</summary>
        public string MediationType => _mediationType;

        // ── Diagnostic APIs (for sample app / debug use) ──────────────────────────

        /// <summary>
        /// Returns the current composite user segment key (e.g. "t1_nonpayer_loyal_d30plus").
        /// Returns "not initialized" if the segment manager has not been created yet.
        /// </summary>
        public string GetSegmentKey()
        {
            if (_segmentManager == null) return "not initialized";
            return _segmentManager.GetCompositeSegment(_cachedCountryCode);
        }

        /// <summary>
        /// Returns a dictionary mapping each configured experiment ID to the assigned variant ID.
        /// Reads from persisted PlayerPrefs so results are stable across sessions.
        /// Returns an empty dictionary if no experiments are configured.
        /// </summary>
        public Dictionary<string, string> GetExperimentAssignments()
        {
            var result = new Dictionary<string, string>();
            var experiments = IAAResponse?.AdExperiments;
            if (experiments == null || experiments.Count == 0) return result;

            foreach (var exp in experiments)
            {
                string variantKey = $"NoctuaExp_{exp.ExperimentId}_variant";
                string variant = PlayerPrefs.GetString(variantKey, "unassigned");
                result[exp.ExperimentId] = exp.Enabled ? variant : $"{variant} [off]";
            }
            return result;
        }

        /// <summary>
        /// Returns CPM floor evaluation results for each format and active network.
        /// Returns a status entry of "CPM floors disabled" if no floor manager is active.
        /// </summary>
        public Dictionary<string, string> GetCpmFloorStatus()
        {
            var result = new Dictionary<string, string>();

            if (_cpmFloorManager == null)
            {
                result["status"] = "CPM floors disabled";
                return result;
            }

            string segmentKey = _segmentManager?.GetCompositeSegment(_cachedCountryCode) ?? "";
            string[] formats = { "interstitial", "rewarded", "banner", "app_open" };

            foreach (string format in formats)
            {
                if (_orchestrator?.Primary != null)
                {
                    string network = _orchestrator.Primary.NetworkName;
                    double avgCpm = _performanceTracker?.GetAverageCpm(network, format) ?? 0;
                    int samples   = _performanceTracker?.GetSampleCount(network, format) ?? 0;
                    var floor     = _cpmFloorManager.EvaluateFloor(network, format, avgCpm, samples, segmentKey);
                    result[$"{format}/{network}"] = $"{floor} (cpm={avgCpm:F4}, n={samples})";
                }

                if (_orchestrator?.Secondary != null)
                {
                    string network = _orchestrator.Secondary.NetworkName;
                    double avgCpm = _performanceTracker?.GetAverageCpm(network, format) ?? 0;
                    int samples   = _performanceTracker?.GetSampleCount(network, format) ?? 0;
                    var floor     = _cpmFloorManager.EvaluateFloor(network, format, avgCpm, samples, segmentKey);
                    result[$"{format}/{network}"] = $"{floor} (cpm={avgCpm:F4}, n={samples})";
                }
            }

            return result;
        }

        internal void SetAdRevenueTracker(IAdRevenueTracker tracker)
        {
            _adRevenueTracker = tracker;
            _revenueTracker?.SetAdRevenueTracker(tracker);
        }

        /// <summary>
        /// Sets the resolved country code so CPM floor and segmentation can use the correct tier.
        /// Call this after the country is resolved in Noctua.Initialization.cs (before or after
        /// IAAResponse is set — the orchestrator's segment key is updated either way).
        /// </summary>
        internal void SetCountryCode(string countryCode)
        {
            _cachedCountryCode = countryCode;
            string segmentKey = _segmentManager?.GetCompositeSegment(countryCode) ?? "";
            _orchestrator?.UpdateSegmentKey(segmentKey);
            _log.Debug($"Country code set to '{countryCode}', segment key: '{segmentKey}'");
        }

        /// <summary>
        /// Records a purchase for the current user, updating the payer tier in PlayerPrefs.
        /// Should be called from the IAP purchase completion callback.
        /// </summary>
        internal void RecordPurchase() => _segmentManager?.RecordPurchase();

        /// <summary>
        /// Returns the segment manager instance (used by Noctua.Initialization.cs to pass
        /// to AdExperimentManager without requiring a static reference).
        /// </summary>
        internal UserSegmentManager GetSegmentManager() => _segmentManager;

        /// <summary>
        /// Applies experiment overrides by updating only the frequency manager and CPM floor manager
        /// with the new effective config — without restarting ad networks.
        /// Called from Noctua.Initialization.cs after country is resolved and experiments are evaluated.
        /// </summary>
        internal void ApplyExperimentOverride(IAA effectiveIaa)
        {
            if (effectiveIaa == null) return;

            // Ensure AppOpen cooldown has a sensible default
            var mergedCooldowns = effectiveIaa.CooldownSeconds ?? new CooldownConfig();
            if (mergedCooldowns.AppOpen <= 0) mergedCooldowns.AppOpen = 30;

            // Recreate frequency manager with experiment-overridden config
            _frequencyManager = new AdFrequencyManager(
                effectiveIaa.FrequencyCaps,
                mergedCooldowns,
                effectiveIaa.EnabledFormats
            );

            // Recreate CPM floor manager with experiment-overridden config
            _cpmFloorManager = (effectiveIaa.CpmFloors?.Enabled == true)
                ? new CpmFloorManager(effectiveIaa.CpmFloors)
                : null;

            // Push new floor manager to the existing orchestrator (no network restart)
            _orchestrator?.UpdateCpmFloorManager(_cpmFloorManager);

            _log.Info("Experiment overrides applied: frequency caps and CPM floors updated.");
        }

        internal MediationManager(IAdPlaceholderUI adPlaceholderUI, IAA iAAResponse)
        {
            // Must be called from Unity's main thread (it is — Noctua() ctor runs on main thread).
            _mainThreadContext = SynchronizationContext.Current;
            _adPlaceholderUI = adPlaceholderUI;

            if (iAAResponse == null)
            {
                _log.Warning("Constructor received null IAA response. MediationManager will not be usable.");
                return;
            }

            if (IAAResponse != null)
            {
                _log.Info("IAA response already set in MediationManager");
                return;
            }

            IAAResponse = iAAResponse;
        }

        private void CreateNetworks(IAA iaaConfig)
        {
            // Clean up existing AppLovin instances before replacing them so that stale
            // handlers are unregistered from the static MaxSdkCallbacks events.
#if UNITY_APPLOVIN
            if (_orchestrator != null)
            {
                (_orchestrator.Primary as AppLovinManager)?.Cleanup();
                (_orchestrator.Secondary as AppLovinManager)?.Cleanup();
            }
#endif

            IAdNetwork primary = null;
            IAdNetwork secondary = null;

            #if UNITY_ADMOB
            primary = new AdmobManager();
            #endif

            #if UNITY_APPLOVIN
            // If ADMOB is also defined and secondary_mediation is set, AppLovin becomes secondary
            #if UNITY_ADMOB
            if (!string.IsNullOrEmpty(iaaConfig.SecondaryMediation) &&
                iaaConfig.SecondaryMediation == AdNetworkName.AppLovin)
            {
                secondary = new AppLovinManager();
            }
            #else
            primary = new AppLovinManager();
            #endif
            #endif

            // If no primary was set (no defines), log error
            if (primary == null)
            {
                #if UNITY_APPLOVIN
                // AppLovin is primary when ADMOB is not defined
                #else
                _log.Error("No ad network SDK is available. Define UNITY_ADMOB or UNITY_APPLOVIN.");
                return;
                #endif
            }

            // Check for secondary in the reverse direction: primary is applovin, secondary is admob
            #if UNITY_APPLOVIN && UNITY_ADMOB
            if (primary is AppLovinManager && !string.IsNullOrEmpty(iaaConfig.SecondaryMediation) &&
                iaaConfig.SecondaryMediation == AdNetworkName.Admob)
            {
                secondary = new AdmobManager();
            }
            #endif

            // Ensure AppOpen cooldown has a sensible default when not set in cooldown_seconds.
            var mergedCooldowns = iaaConfig.CooldownSeconds ?? new CooldownConfig();
            if (mergedCooldowns.AppOpen <= 0)
            {
                mergedCooldowns.AppOpen = 30;
            }

            // Create supporting managers
            _frequencyManager = new AdFrequencyManager(
                iaaConfig.FrequencyCaps,
                mergedCooldowns,
                iaaConfig.EnabledFormats
            );

            _revenueTracker = new AdRevenueTrackingManager(_adRevenueTracker, iaaConfig.Taichi);

            _performanceTracker = (iaaConfig.DynamicOptimization ?? false)
                ? new AdNetworkPerformanceTracker()
                : null;

            // Preserve the segment manager across CreateNetworks calls so session/install state
            // accumulated in PlayerPrefs is not lost when the config is reapplied.
            _segmentManager ??= new UserSegmentManager();

            _cpmFloorManager = (iaaConfig.CpmFloors?.Enabled == true)
                ? new CpmFloorManager(iaaConfig.CpmFloors)
                : null;

            string segmentKey = _segmentManager.GetCompositeSegment(_cachedCountryCode);

            _orchestrator = new HybridAdOrchestrator(
                primary: primary,
                secondary: secondary,
                adFormatOverrides: iaaConfig.AdFormatOverrides,
                performanceTracker: _performanceTracker,
                dynamicOptimization: iaaConfig.DynamicOptimization ?? false,
                cpmFloorManager: _cpmFloorManager,
                segmentKey: segmentKey
            );

            _log.Info($"Networks created. Primary: {primary.NetworkName}" +
                (secondary != null ? $", Secondary: {secondary.NetworkName}" : "") +
                $", Hybrid: {_orchestrator.IsHybridMode}" +
                $", CpmFloors: {(_cpmFloorManager != null ? "enabled" : "disabled")}" +
                $", Segment: {segmentKey}");
        }

        /// <summary>
        /// Initializes the ad mediation SDK based on the configured mediation type.
        /// </summary>
        public void Initialize(Action initCompleteAction = null)
        {
            if (IAAResponse == null)
            {
                _log.Error("Cannot initialize MediationManager: IAA response is null.");
                return;
            }

            if (_orchestrator == null)
            {
                _log.Error("Cannot initialize MediationManager: orchestrator not created.");
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

            SubscribeToOrchestratorEvents();
            SubscribeToNetworkSpecificEvents();

            // Use orchestrator to initialize both networks concurrently.
            // Primary callback fires when primary SDK is ready → load primary ads.
            // Secondary callback fires when secondary SDK is ready → load secondary ads.
            // Never call Load on a network before its own init callback fires (per AdMob and AppLovin docs).
            _orchestrator.Initialize(
                onPrimaryReady: () =>
                {
                    _log.Info("Primary network initialized: " + _orchestrator.Primary.NetworkName);

                    if (IAAResponse.AdFormat == null && (IAAResponse.Networks == null || IAAResponse.Networks.Count == 0))
                    {
                        _log.Info("No ad format config. Cannot proceed with ad unit ID setup.");
                        initCompleteAction?.Invoke();
                        return;
                    }

                    if (IsAdmob())
                    {
#if UNITY_ADMOB
                        _preloadManager = AdmobAdPreloadManager.Instance;
#endif
                    }

                    SetupAdUnitID(IAAResponse);
                    initCompleteAction?.Invoke();
                },
                onSecondaryReady: () =>
                {
                    var secondary = _orchestrator.Secondary;
                    if (secondary == null || IAAResponse == null) return;

                    _log.Info("Secondary network initialized: " + secondary.NetworkName);
                    SetupSecondaryAdUnits(IAAResponse, secondary);
                    SetupSecondaryAppOpen(IAAResponse, secondary);
                });
        }

        private void SubscribeToOrchestratorEvents()
        {
            if (_adNetworkEventsSubscribed) return;
            _adNetworkEventsSubscribed = true;

            _orchestrator.OnAdDisplayed += () =>
            {
                CloseAdPlaceholder();
                _appOpenAdManager?.SetFullscreenAdShowing(true);
                _onAdDisplayed?.Invoke();
            };

            _orchestrator.OnAdFailedDisplayed += () =>
            {
                CloseAdPlaceholder();
                _appOpenAdManager?.SetFullscreenAdShowing(false);
                _onAdFailedDisplayed?.Invoke();
            };

            _orchestrator.OnAdClicked += () => _onAdClicked?.Invoke();
            _orchestrator.OnAdImpressionRecorded += () => _onAdImpressionRecorded?.Invoke();

            _orchestrator.OnAdClosed += () =>
            {
                _appOpenAdManager?.SetFullscreenAdShowing(false);
                _onAdClosed?.Invoke();
            };
        }

        private void SubscribeToNetworkSpecificEvents()
        {
            var primary = _orchestrator.Primary;
            var secondary = _orchestrator.Secondary;

#if UNITY_ADMOB
            SubscribeAdmobRevenueEvents(primary);
            if (secondary != null) SubscribeAdmobRevenueEvents(secondary);
#endif

#if UNITY_APPLOVIN
            SubscribeAppLovinRevenueEvents(primary);
            if (secondary != null) SubscribeAppLovinRevenueEvents(secondary);
#endif
        }

#if UNITY_ADMOB
        private void SubscribeAdmobRevenueEvents(IAdNetwork network)
        {
            if (network.NetworkName != AdNetworkName.Admob) return;

            network.AdmobOnUserEarnedReward += (reward) => _admobOnUserEarnedReward?.Invoke(reward);

            // Per-format routing: the aggregate AdmobOnAdRevenuePaid event does not carry
            // format info, so Taichi counters and performance tracker would be misattributed
            // (everything was previously counted as banner). AdmobManager now exposes
            // per-format revenue events; subscribe to each and route to the correct
            // Process*Thresholds / RecordRevenue call.
            //
            // Note: the preload path for interstitial/rewarded uses a raw AdMob ad object
            // (not _interstitialAdmob/_rewardedAdmob), so OnAdPaid is wired directly in
            // RegisterCallbackAdInterstitial / RegisterCallbackAdRewarded. The per-format
            // events below fire for the legacy (non-preload) paths, including when AdMob
            // is the secondary network.
            if (network is AdmobManager admobManager)
            {
                admobManager.AdmobOnBannerRevenuePaid += (adValue, responseInfo) =>
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

                admobManager.AdmobOnInterstitialRevenuePaid += (adValue, responseInfo) =>
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

                admobManager.AdmobOnRewardedRevenuePaid += (adValue, responseInfo) =>
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

                admobManager.AdmobOnRewardedInterstitialRevenuePaid += (adValue, responseInfo) =>
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

                admobManager.AdmobOnAppOpenRevenuePaid += (adValue, responseInfo) =>
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
                // Fallback — if the network isn't AdmobManager (e.g. a test double),
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
            if (network.NetworkName != AdNetworkName.AppLovin) return;

            network.AppLovinOnUserEarnedReward += (reward) => _appLovinOnUserEarnedReward?.Invoke(reward);
            network.AppLovinOnAdRevenuePaid += (adInfo) =>
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
            };
        }

        /// <summary>Maps AppLovin AdInfo.AdFormat strings to <see cref="AdFormatKey"/> constants.</summary>
        private static string MapAppLovinFormatToKey(string appLovinFormat)
        {
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

        /// <summary>
        /// Configures ad unit IDs for all ad formats based on the IAA server response, then loads initial ads.
        /// </summary>
        public void SetupAdUnitID(IAA iAAResponse)
        {
            var primary = _orchestrator.Primary;
            var secondary = _orchestrator.Secondary;

            ResolveAdUnitIDs(iAAResponse, primary.NetworkName);

            primary.SetBannerAdUnitId(_bannerAdUnitID);

            if (IsAdmob() && primary.NetworkName == AdNetworkName.Admob)
            {
#if UNITY_ADMOB
                // All AdMob fullscreen formats use the Preload API exclusively.
                // Do NOT call primary.SetRewardedInterstitialAdUnitID() / LoadRewardedInterstitialAd()
                // or AppOpenAd.Load() — mixing preload and legacy paths for the same ad unit
                // causes race conditions per AdMob docs.

                // _preloadManager already assigned in Initialize() callback; reuse the same singleton.
                // RewardedInterstitialAd does NOT support the Preload API — use legacy Load() path.
                if (!string.IsNullOrEmpty(_rewardedInterstitialAdUnitID) && _rewardedInterstitialAdUnitID != "unknown")
                {
                    _rewardedInterstitialAdmob = new RewardedInterstitialAdmob();
                    _rewardedInterstitialAdmob.SetRewardedInterstitialAdUnitID(_rewardedInterstitialAdUnitID);
                    _rewardedInterstitialAdmob.RewardedOnAdDisplayed += () => { CloseAdPlaceholder(); _frequencyManager?.RecordImpression(AdFormatKey.RewardedInterstitial); _onAdDisplayed?.Invoke(); };
                    _rewardedInterstitialAdmob.RewardedOnAdFailedDisplayed += () => { CloseAdPlaceholder(); _onAdFailedDisplayed?.Invoke(); };
                    _rewardedInterstitialAdmob.RewardedOnAdClosed += () => _onAdClosed?.Invoke();
                    _rewardedInterstitialAdmob.RewardedOnAdClicked += () => _onAdClicked?.Invoke();
                    _rewardedInterstitialAdmob.RewardedOnAdImpressionRecorded += () => _onAdImpressionRecorded?.Invoke();
                    _rewardedInterstitialAdmob.RewardedOnUserEarnedReward += reward => _admobOnUserEarnedReward?.Invoke(reward);
                    _rewardedInterstitialAdmob.AdmobOnAdRevenuePaid += (adValue, responseInfo) =>
                    {
                        _revenueTracker.ProcessAdmobRevenue(adValue, responseInfo);
                        _admobOnAdRevenuePaid?.Invoke(adValue, responseInfo);
                    };
                    _rewardedInterstitialAdmob.LoadRewardedInterstitialAd();
                }

#if !UNITY_EDITOR
                // Device only: AdMob Preload API. Not supported in the Unity Editor.
                var configs = new List<PreloadConfiguration>
                {
                    _preloadManager.CreateInterstitialPreloadConfig(_interstitialAdUnitID),
                    _preloadManager.CreateRewardedPreloadConfig(_rewardedAdUnitID),
                };

                // App Open: buffer=1 is recommended (Google docs: app open shown once per foreground).
                if (!string.IsNullOrEmpty(_appOpenAdUnitID) && _appOpenAdUnitID != "unknown")
                {
                    configs.Add(_preloadManager.CreateAppOpenPreloadConfig(_appOpenAdUnitID, bufferSize: 1));
                }

                if (!_preloadManagerEventsSubscribed)
                {
                    _preloadManagerEventsSubscribed = true;

                    _preloadManager.OnAdsAvailable += (config) =>
                    {
                        _log.Debug($"Ad available for {config.Format}");
                        _onAdsAvailable?.Invoke(config);
                    };

                    _preloadManager.OnAdExhausted += (config) =>
                    {
                        _log.Debug($"Ad exhausted for {config.Format}");
                        _onAdExhausted?.Invoke(config);
                    };
                }

                _preloadManager.StartPreloading(configs);
#else
                // Editor: Preload API not supported. Use the legacy load path so
                // IsInterstitialReady() / IsRewardedAdReady() can return true and
                // ShowAdmobInterstitial / ShowAdmobRewarded can show the ad.
                primary.SetInterstitialAdUnitID(_interstitialAdUnitID);
                primary.SetRewardedAdUnitID(_rewardedAdUnitID);
                primary.LoadInterstitialAd();
                primary.LoadRewardedAd();
#endif
#endif
            }
            else
            {
                primary.SetInterstitialAdUnitID(_interstitialAdUnitID);
                primary.SetRewardedAdUnitID(_rewardedAdUnitID);
                primary.LoadInterstitialAd();
                primary.LoadRewardedAd();
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
            string primaryAppOpenId = ResolveAdUnitIdForNetwork(iAAResponse, _orchestrator.Primary.NetworkName, AdFormatKey.AppOpen);

            if (string.IsNullOrEmpty(primaryAppOpenId) || primaryAppOpenId == "unknown")
            {
                _log.Debug("No primary app open ad unit ID configured.");
                return;
            }

            // Resolve preferred network from format overrides (null = primary first, default behaviour).
            string preferredAppOpenNetwork = null;
            iAAResponse.AdFormatOverrides?.TryGetValue(AdFormatKey.AppOpen, out preferredAppOpenNetwork);

            _appOpenAdManager = new AppOpenAdManager(
                primaryNetwork: _orchestrator.Primary,
                secondaryNetwork: _orchestrator.Secondary,
                frequencyManager: _frequencyManager,
                autoShowOnForeground: iAAResponse.AppOpenAutoShow ?? false,
                preferredNetworkName: preferredAppOpenNetwork,
                onAdNotAvailable: format => _onAdNotAvailable?.Invoke(format)
            );

            // Only pass primary here; secondary will be added in SetupSecondaryAppOpen after secondary SDK is ready.
            // Exception: if secondary already initialized before primary (e.g. iOS AppLovin before AdMob),
            // _pendingSecondaryAppOpenId was cached in SetupSecondaryAppOpen — apply it now.
            _appOpenAdManager.Configure(primaryAppOpenId, null);

            if (!string.IsNullOrEmpty(_pendingSecondaryAppOpenId))
            {
                _log.Info($"Applying cached secondary App Open unit: {_pendingSecondaryAppOpenId}");
                _appOpenAdManager.ConfigureSecondary(_pendingSecondaryAppOpenId);
                _pendingSecondaryAppOpenId = null;
            }
        }

        /// <summary>
        /// Adds secondary App Open ad support after the secondary SDK has finished initialization.
        /// Safe to call only from the onSecondaryReady callback.
        /// </summary>
        private void SetupSecondaryAppOpen(IAA iAAResponse, IAdNetwork secondary)
        {
            string secondaryAppOpenId = ResolveAdUnitIdForNetwork(iAAResponse, secondary.NetworkName, AdFormatKey.AppOpen);

            if (string.IsNullOrEmpty(secondaryAppOpenId) || secondaryAppOpenId == "unknown")
                return;

            if (_appOpenAdManager == null)
            {
                // Secondary initialized before primary (e.g. iOS: AppLovin before AdMob).
                // Cache the ID so SetupAppOpenAds() can apply it once the manager is created.
                _log.Info($"App Open secondary unit cached (manager not ready yet): {secondaryAppOpenId}");
                _pendingSecondaryAppOpenId = secondaryAppOpenId;
                return;
            }

            _appOpenAdManager.ConfigureSecondary(secondaryAppOpenId);
        }

        private void ResolveAdUnitIDs(IAA iAAResponse, string networkName)
        {
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

        // --- Public ad show methods ---

        /// <summary>Loads an interstitial ad from the ad network.</summary>
        public void LoadInterstitialAd()
        {
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot load interstitial ad.");
                return;
            }
            _orchestrator.Primary.LoadInterstitialAd();
        }

        /// <summary>Shows a full-screen interstitial ad with a placeholder overlay while loading.</summary>
        public void ShowInterstitial()
        {
            ShowInterstitial(null);
        }

#if UNITY_ADMOB
        // isFallback=true means AdMob is the secondary network; on failure fire OnAdNotAvailable
        // rather than recursing back to TryInterstitialFallback.
        private void ShowAdmobInterstitial(IAdNetwork admobNetwork, string placement = null, bool isFallback = false)
        {
            _hasClosedPlaceholder = false;
            ShowAdPlaceholder(AdPlaceholderType.Interstitial);

#if !UNITY_EDITOR
            // Device: use preload manager only when it was initialized (AdMob is primary).
            // When AdMob is secondary, _preloadManager is null and we fall through to the legacy path.
            if (_preloadManager != null)
            {
                if (_preloadManager.IsAdAvailable(_interstitialAdUnitID, AdFormat.INTERSTITIAL))
                {
                    var ad = _preloadManager.PollInterstitialAd(_interstitialAdUnitID);
                    if (ad != null)
                    {
                        _performanceTracker?.RecordFillAttempt(AdNetworkName.Admob, AdFormatKey.Interstitial, true);
                        try
                        {
                            _log.Info(placement != null
                                ? $"Showing Admob Interstitial Ad (placement: {placement})"
                                : "Showing Admob Interstitial Ad");
                            RegisterCallbackAdInterstitial(ad, placement);
                            ad.Show();
                            // Record impression only after a successful show attempt.
                            _frequencyManager?.RecordImpression(AdFormatKey.Interstitial);
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
                else _onAdNotAvailable?.Invoke(AdFormatKey.Interstitial);
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
                _frequencyManager?.RecordImpression(AdFormatKey.Interstitial);
            }
            else
            {
                _log.Info("Admob Interstitial Ad not ready (legacy path)");
                CloseAdPlaceholder();
                if (!isFallback) TryInterstitialFallback(admobNetwork, placement);
                else _onAdNotAvailable?.Invoke(AdFormatKey.Interstitial);
            }
        }

        private void TryInterstitialFallback(IAdNetwork failedNetwork, string placement)
        {
            var fallback = failedNetwork == _orchestrator.Primary
                ? _orchestrator.Secondary
                : _orchestrator.Primary;

            if (fallback == null)
            {
                _log.Info("Interstitial: no secondary network to fall back to.");
                _onAdNotAvailable?.Invoke(AdFormatKey.Interstitial);
                return;
            }

            _log.Info($"{failedNetwork.NetworkName} interstitial not available. Falling back to {fallback.NetworkName}.");

            if (IsAdmobNetwork(fallback))
            {
                ShowAdmobInterstitial(fallback, placement, isFallback: true);
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
                _frequencyManager?.RecordImpression(AdFormatKey.Interstitial);
            }
            else
            {
                _log.Info($"{fallback.NetworkName} interstitial also not ready. No ad available.");
                _onAdNotAvailable?.Invoke(AdFormatKey.Interstitial);
            }
        }

        /// <summary>
        /// Dispatches <paramref name="action"/> to Unity's main thread.
        /// AdMob preloaded-ad callbacks fire on the GMA JNI thread; running Unity API
        /// calls (UI updates, AudioSource, etc.) directly from that thread crashes.
        /// </summary>
        private void PostToMainThread(Action action)
        {
            if (_mainThreadContext != null)
                _mainThreadContext.Post(_ => action(), null);
            else
                action(); // fallback: already on main thread (Editor / tests)
        }

        private void RegisterCallbackAdInterstitial(InterstitialAd interstitialAd, string placement = null)
        {
            // Preload path needs to emit canonical IAA events (ad_shown / ad_impression /
            // ad_clicked / ad_show_failed) just like the legacy path does inside
            // InterstitialAdmob.RegisterEventHandlers. Without these calls the preload path
            // silently drops every canonical event on device — breaking dashboards that
            // filter by canonical event names (user-visible symptom: only banner events show).
            AdValue capturedAdValue = null;
            var showStopwatch = System.Diagnostics.Stopwatch.StartNew();

            string ResolveAdSource()
            {
                try { return interstitialAd.GetResponseInfo()?.GetLoadedAdapterResponseInfo()?.AdSourceName; }
                catch { return null; }
            }

            interstitialAd.OnAdFullScreenContentOpened += () => PostToMainThread(() =>
            {
                CloseAdPlaceholder();
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
                CloseAdPlaceholder();
                EmitCanonicalIaa(IAAEventNames.AdShowFailed, IAAPayloadBuilder.BuildAdShowFailed(
                    adFormat:   AdFormatKey.Interstitial,
                    adPlatform: AdNetworkName.Admob,
                    adUnitName: _interstitialAdUnitID,
                    error:      IAAPayloadBuilder.FormatError(error.GetCode(), error.GetMessage(), error.GetDomain())));
                _onAdFailedDisplayed?.Invoke();
                _log.Warning("Interstitial Ad failed to show. Error: " + error);
            });
            interstitialAd.OnAdFullScreenContentClosed += () => PostToMainThread(() =>
            {
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
                var engagementMs = showStopwatch.IsRunning ? showStopwatch.ElapsedMilliseconds : 0L;
                showStopwatch.Stop();
                var valueMicros = capturedAdValue?.Value ?? 0L;
                var value       = valueMicros / 1_000_000d;
                var currency    = capturedAdValue?.CurrencyCode;
                var valueUsd    = currency == "USD" ? value : 0d;

                EmitCanonicalIaa(IAAEventNames.AdImpression, IAAPayloadBuilder.BuildAdImpression(
                    placement:        placement,
                    adType:           AdFormatKey.Interstitial,
                    adUnitId:         _interstitialAdUnitID,
                    adUnitName:       _interstitialAdUnitID,
                    value:            value,
                    valueUsd:         valueUsd,
                    adSize:           IAAAdSize.Fullscreen,
                    adSource:         ResolveAdSource(),
                    adPlatform:       AdNetworkName.Admob,
                    engagementTimeMs: engagementMs));
                _onAdImpressionRecorded?.Invoke();
            });
            interstitialAd.OnAdPaid += (AdValue adValue) => PostToMainThread(() =>
            {
                capturedAdValue = adValue; // cached for the OnAdImpressionRecorded canonical emit
                _revenueTracker.ProcessAdmobInterstitialRevenue(adValue, interstitialAd.GetResponseInfo());
                _admobOnAdRevenuePaid?.Invoke(adValue, interstitialAd.GetResponseInfo());
                _performanceTracker?.RecordRevenue(AdNetworkName.Admob, AdFormatKey.Interstitial, adValue.Value / 1_000_000.0);
            });
        }
#endif

        /// <summary>Loads a rewarded ad from the ad network.</summary>
        public void LoadRewardedAd()
        {
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot load rewarded ad.");
                return;
            }
            _orchestrator.Primary.LoadRewardedAd();
        }

        /// <summary>Shows a rewarded ad with a placeholder overlay while loading.</summary>
        public void ShowRewardedAd()
        {
            ShowRewardedAd(null);
        }

#if UNITY_ADMOB
        // isFallback=true means AdMob is the secondary network; on failure fire OnAdNotAvailable
        // rather than recursing back to TryRewardedFallback.
        private void ShowAdmobRewarded(IAdNetwork admobNetwork, string placement = null, bool isFallback = false)
        {
            _hasClosedPlaceholder = false;
            ShowAdPlaceholder(AdPlaceholderType.Rewarded);

#if !UNITY_EDITOR
            // Device: use preload manager only when it was initialized (AdMob is primary).
            if (_preloadManager != null)
            {
                if (_preloadManager.IsAdAvailable(_rewardedAdUnitID, AdFormat.REWARDED))
                {
                    var ad = _preloadManager.PollRewardedAd(_rewardedAdUnitID);
                    if (ad != null)
                    {
                        _performanceTracker?.RecordFillAttempt(AdNetworkName.Admob, AdFormatKey.Rewarded, true);
                        try
                        {
                            _log.Info(placement != null
                                ? $"Showing Admob Rewarded Ad (placement: {placement})"
                                : "Showing Admob Rewarded Ad");
                            RegisterCallbackAdRewarded(ad, placement);
                            ad.Show((Reward reward) => PostToMainThread(() =>
                            {
                                _log.Info("User earned reward: " + reward.Type + " - " + reward.Amount);
                                _admobOnUserEarnedReward?.Invoke(reward);
                            }));
                            // Record impression only after a successful show attempt.
                            _frequencyManager?.RecordImpression(AdFormatKey.Rewarded);
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
                else _onAdNotAvailable?.Invoke(AdFormatKey.Rewarded);
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
                _frequencyManager?.RecordImpression(AdFormatKey.Rewarded);
            }
            else
            {
                _log.Info("Admob Rewarded Ad not ready (legacy path)");
                CloseAdPlaceholder();
                if (!isFallback) TryRewardedFallback(admobNetwork, placement);
                else _onAdNotAvailable?.Invoke(AdFormatKey.Rewarded);
            }
        }

        private void TryRewardedFallback(IAdNetwork failedNetwork, string placement)
        {
            var fallback = failedNetwork == _orchestrator.Primary
                ? _orchestrator.Secondary
                : _orchestrator.Primary;

            if (fallback == null)
            {
                _log.Info("Rewarded: no secondary network to fall back to.");
                _onAdNotAvailable?.Invoke(AdFormatKey.Rewarded);
                return;
            }

            _log.Info($"{failedNetwork.NetworkName} rewarded not available. Falling back to {fallback.NetworkName}.");

            if (IsAdmobNetwork(fallback))
            {
                ShowAdmobRewarded(fallback, placement, isFallback: true);
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
                _frequencyManager?.RecordImpression(AdFormatKey.Rewarded);
            }
            else
            {
                _log.Info($"{fallback.NetworkName} rewarded also not ready. No ad available.");
                _onAdNotAvailable?.Invoke(AdFormatKey.Rewarded);
            }
        }

        private void RegisterCallbackAdRewarded(RewardedAd rewardedAd, string placement = null)
        {
            // See comment in RegisterCallbackAdInterstitial — canonical IAA events must be
            // emitted here as well, otherwise the preload path silently drops every
            // canonical rewarded event on device.
            AdValue capturedAdValue = null;
            var showStopwatch = System.Diagnostics.Stopwatch.StartNew();

            string ResolveAdSource()
            {
                try { return rewardedAd.GetResponseInfo()?.GetLoadedAdapterResponseInfo()?.AdSourceName; }
                catch { return null; }
            }

            rewardedAd.OnAdFullScreenContentOpened += () => PostToMainThread(() =>
            {
                CloseAdPlaceholder();
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
                CloseAdPlaceholder();
                EmitCanonicalIaa(IAAEventNames.AdShowFailed, IAAPayloadBuilder.BuildAdShowFailed(
                    adFormat:   AdFormatKey.Rewarded,
                    adPlatform: AdNetworkName.Admob,
                    adUnitName: _rewardedAdUnitID,
                    error:      IAAPayloadBuilder.FormatError(error.GetCode(), error.GetMessage(), error.GetDomain())));
                _onAdFailedDisplayed?.Invoke();
                _log.Warning("Rewarded Ad failed to show. Error: " + error);
            });
            rewardedAd.OnAdFullScreenContentClosed += () => PostToMainThread(() =>
            {
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
                var engagementMs = showStopwatch.IsRunning ? showStopwatch.ElapsedMilliseconds : 0L;
                showStopwatch.Stop();
                var valueMicros = capturedAdValue?.Value ?? 0L;
                var value       = valueMicros / 1_000_000d;
                var currency    = capturedAdValue?.CurrencyCode;
                var valueUsd    = currency == "USD" ? value : 0d;

                EmitCanonicalIaa(IAAEventNames.AdImpression, IAAPayloadBuilder.BuildAdImpression(
                    placement:        placement,
                    adType:           AdFormatKey.Rewarded,
                    adUnitId:         _rewardedAdUnitID,
                    adUnitName:       _rewardedAdUnitID,
                    value:            value,
                    valueUsd:         valueUsd,
                    adSize:           IAAAdSize.Fullscreen,
                    adSource:         ResolveAdSource(),
                    adPlatform:       AdNetworkName.Admob,
                    engagementTimeMs: engagementMs));
                _onAdImpressionRecorded?.Invoke();
            });
            rewardedAd.OnAdPaid += (AdValue adValue) => PostToMainThread(() =>
            {
                capturedAdValue = adValue; // cached for the OnAdImpressionRecorded canonical emit
                _revenueTracker.ProcessAdmobRewardedRevenue(adValue, rewardedAd.GetResponseInfo());
                _admobOnAdRevenuePaid?.Invoke(adValue, rewardedAd.GetResponseInfo());
                _performanceTracker?.RecordRevenue(AdNetworkName.Admob, AdFormatKey.Rewarded, adValue.Value / 1_000_000.0);
            });
        }

        /// <summary>
        /// Routes a canonical IAA event through <see cref="Noctua.Event.TrackCustomEvent"/>
        /// with a try/catch so analytics failures never break ad delivery. Mirrors
        /// the per-format adapter's EmitCanonical.
        /// </summary>
        private void EmitCanonicalIaa(string eventName, System.Collections.Generic.Dictionary<string, System.IConvertible> payload)
        {
            try
            {
                Noctua.Event.TrackCustomEvent(eventName, payload);
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
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show rewarded interstitial ad.");
                return;
            }

            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.RewardedInterstitial))
            {
                _log.Info("Rewarded interstitial ad blocked by frequency manager.");
                _onAdNotAvailable?.Invoke(AdFormatKey.RewardedInterstitial);
                return;
            }

            // rewarded_interstitial is AdMob-only. Warn and skip if mistakenly overridden to AppLovin.
            var riNetwork = _orchestrator.GetNetworkForFormat(AdFormatKey.RewardedInterstitial);
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
            if (_rewardedInterstitialAdmob == null)
            {
                _log.Warning("Rewarded interstitial ad not initialized (ad unit ID may be missing). Cannot show.");
                return;
            }

            _hasClosedPlaceholder = false;
            ShowAdPlaceholder(AdPlaceholderType.Rewarded);
            // Placeholder is closed via RewardedOnAdDisplayed / RewardedOnAdFailedDisplayed events
            // wired during SetupAdUnitID. RewardedInterstitialAdmob manages its own reload lifecycle.
            _rewardedInterstitialAdmob.ShowRewardedInterstitialAd();
        }
#endif

        /// <summary>Shows a banner ad using the configured ad network, falling back to secondary if needed.</summary>
        public void ShowBannerAd()
        {
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show banner ad.");
                return;
            }

            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.Banner))
            {
                _log.Info("Banner ad blocked by frequency/enabled config.");
                _onAdNotAvailable?.Invoke(AdFormatKey.Banner);
                return;
            }

            var preferred = _orchestrator.GetNetworkForFormat(AdFormatKey.Banner);
            if (preferred.HasBannerAdUnit())
            {
                preferred.ShowBannerAd();
                return;
            }

            var fallback = preferred == _orchestrator.Primary ? _orchestrator.Secondary : _orchestrator.Primary;
            if (fallback != null && fallback.HasBannerAdUnit())
            {
                _log.Info($"{preferred.NetworkName} has no banner unit. Falling back to {fallback.NetworkName}.");
                fallback.ShowBannerAd();
                return;
            }

            _log.Warning("No banner ad unit configured on any network.");
            _onAdNotAvailable?.Invoke(AdFormatKey.Banner);
        }

        /// <summary>
        /// Network-agnostic banner hide. Hides the currently displayed banner on both
        /// Primary and Secondary networks (whichever one is actually showing it), without
        /// destroying the underlying banner instance. Prefer this over
        /// <see cref="HideAppLovinBanner"/> when you want mediation-agnostic behavior,
        /// e.g. after a successful "remove ads" IAP on a game that may run either AdMob
        /// or AppLovin as its primary network.
        /// Safe no-op when the orchestrator is not yet initialized.
        /// </summary>
        public void HideBannerAd()
        {
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot hide banner ad.");
                return;
            }

            _orchestrator.Primary?.HideBannerAd();
            _orchestrator.Secondary?.HideBannerAd();
        }

#if UNITY_ADMOB
        /// <summary>Creates a banner ad view with specified size and position (AdMob only).</summary>
        public void CreateBannerViewAdAdmob(AdSize adSize, AdPosition adPosition)
        {
            if (!IsAdmob() || _orchestrator == null) return;
            _orchestrator.Primary.CreateBannerViewAdAdmob(adSize, adPosition);
        }
#endif

#if UNITY_APPLOVIN
        /// <summary>Creates a banner ad view (AppLovin, deprecated).</summary>
        [Obsolete("Use CreateBannerViewAdAppLovin(Color, MaxSdkBase.AdViewPosition) instead.")]
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.CreateBannerViewAdAppLovin(color, bannerPosition);
        }

        /// <summary>Creates a banner ad view with specified background color and position (AppLovin).</summary>
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.AdViewPosition bannerPosition)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.CreateBannerViewAdAppLovin(color, bannerPosition);
        }

        /// <summary>Hides the currently displayed AppLovin banner ad.</summary>
        public void HideAppLovinBanner()
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.HideBannerAppLovin();
        }

        /// <summary>Destroys the AppLovin banner ad view and releases resources.</summary>
        public void DestroyBannerAppLovin()
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.DestroyBannerAppLovin();
        }

        /// <summary>Sets the width of the AppLovin banner ad in pixels.</summary>
        public void SetBannerWidth(int width)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.SetBannerWidth(width);
        }

        /// <summary>Gets the current screen position and size of the AppLovin banner ad.</summary>
        public Rect GetBannerPosition()
        {
            if (!IsAppLovin() || _orchestrator == null) return new Rect();
            return _orchestrator.Primary.GetBannerPosition();
        }

        /// <summary>Stops automatic refresh of the AppLovin banner ad.</summary>
        public void StopBannerAutoRefresh()
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.StopBannerAutoRefresh();
        }

        /// <summary>Starts automatic refresh of the AppLovin banner ad.</summary>
        public void StartBannerAutoRefresh()
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.StartBannerAutoRefresh();
        }

        /// <summary>Mutes or unmutes ad audio (AppLovin only).</summary>
        public void SetMuted(bool muted)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.SetMuted(muted);
        }

        /// <summary>Sets the placement name for the AppLovin banner ad.</summary>
        public void SetBannerPlacement(string placement)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.SetBannerPlacement(placement);
        }

        /// <summary>Sets the banner auto-refresh interval in seconds (AppLovin). Clamped to 10-120s.</summary>
        public void SetBannerRefreshInterval(int seconds)
        {
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.SetBannerRefreshInterval(seconds);
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
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show interstitial ad.");
                return;
            }

            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.Interstitial))
            {
                _log.Info("Interstitial ad blocked by frequency manager.");
                _onAdNotAvailable?.Invoke(AdFormatKey.Interstitial);
                return;
            }

            var network = _orchestrator.GetNetworkForFormat(AdFormatKey.Interstitial);
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

                _frequencyManager?.RecordImpression(AdFormatKey.Interstitial);
            }
            else
            {
                TryInterstitialFallback(network, placement);
            }
        }

        /// <summary>
        /// Shows a rewarded ad with an optional placement name for analytics segmentation.
        /// For AppLovin the placement is passed natively to MAX SDK.
        /// For AdMob the placement is included in custom event tracking only.
        /// Passing <c>null</c> is identical to calling <see cref="ShowRewardedAd()"/>.
        /// </summary>
        public void ShowRewardedAd(string placement)
        {
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show rewarded ad.");
                return;
            }

            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.Rewarded))
            {
                _log.Info("Rewarded ad blocked by frequency manager.");
                _onAdNotAvailable?.Invoke(AdFormatKey.Rewarded);
                return;
            }

            var network = _orchestrator.GetNetworkForFormat(AdFormatKey.Rewarded);
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

                _frequencyManager?.RecordImpression(AdFormatKey.Rewarded);
            }
            else
            {
                TryRewardedFallback(network, placement);
            }
        }

        // --- App Open Ad public methods ---

        /// <summary>Shows an app open ad (tries primary then secondary network).</summary>
        public void ShowAppOpenAd()
        {
            if (_appOpenAdManager == null)
            {
                _log.Warning("App Open ad manager not configured.");
                return;
            }
            _appOpenAdManager.ShowAppOpenAd();
        }

        /// <summary>Returns whether an app open ad is ready on any network.</summary>
        public bool IsAppOpenAdReady()
        {
            return _appOpenAdManager?.IsAppOpenAdReady() ?? false;
        }

        /// <summary>
        /// Returns <c>true</c> if an interstitial ad is loaded and ready to display,
        /// and the current frequency cap and cooldown allow showing it.
        /// Use this to conditionally show UI elements like "Watch Ad" buttons.
        /// </summary>
        public bool IsInterstitialReady()
        {
            if (_orchestrator == null) return false;
            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.Interstitial)) return false;

            var preferred = _orchestrator.GetNetworkForFormat(AdFormatKey.Interstitial);
            if (IsInterstitialReadyOnNetwork(preferred)) return true;

            // When ad_format_overrides explicitly pins interstitial to a network, only that
            // network's readiness counts — the secondary is a last-resort fallback, not the
            // intended source. Returning secondary-ready here would make IsInterstitialReady()
            // return true and then ShowInterstitial() silently fall back to the secondary network,
            // which is confusing (e.g. MAX ad shown when config says admob).
            if (_orchestrator.HasFormatOverride(AdFormatKey.Interstitial)) return false;

            // No override → check secondary so "Watch Ad" buttons stay enabled when only
            // secondary has fill and the primary is temporarily dry.
            var secondary = _orchestrator.Secondary;
            return secondary != null && IsInterstitialReadyOnNetwork(secondary);
        }

        private bool IsInterstitialReadyOnNetwork(IAdNetwork network)
        {
#if UNITY_ADMOB && !UNITY_EDITOR
            if (IsAdmobNetwork(network) && _preloadManager != null)
                return _preloadManager.IsAdAvailable(_interstitialAdUnitID, AdFormat.INTERSTITIAL);
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
            if (_orchestrator == null) return false;
            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.Rewarded)) return false;

            var preferred = _orchestrator.GetNetworkForFormat(AdFormatKey.Rewarded);
            if (IsRewardedReadyOnNetwork(preferred)) return true;

            // Same reasoning as IsInterstitialReady: an explicit override pins the format to
            // one network; secondary readiness would cause ShowRewardedAd() to silently serve
            // from the wrong network.
            if (_orchestrator.HasFormatOverride(AdFormatKey.Rewarded)) return false;

            // No override → check secondary so "Watch Ad" buttons stay enabled when only
            // secondary has fill and the primary is temporarily dry.
            var secondary = _orchestrator.Secondary;
            return secondary != null && IsRewardedReadyOnNetwork(secondary);
        }

        private bool IsRewardedReadyOnNetwork(IAdNetwork network)
        {
#if UNITY_ADMOB && !UNITY_EDITOR
            if (IsAdmobNetwork(network) && _preloadManager != null)
                return _preloadManager.IsAdAvailable(_rewardedAdUnitID, AdFormat.REWARDED);
#endif
            return network.IsRewardedAdReady();
        }

        /// <summary>Handles app foreground transitions for app open ad auto-show.</summary>
        public void OnApplicationForeground()
        {
            _appOpenAdManager?.OnApplicationForeground();
        }

        // --- Debugger methods ---

        /// <summary>Opens the ad network's creative debugger UI. Always routes to AppLovin.</summary>
        public void ShowCreativeDebugger()
        {
            if (_orchestrator == null) return;

            // Creative Debugger is AppLovin-only. Find the AppLovin network.
            if (_orchestrator.Primary.NetworkName == AdNetworkName.AppLovin)
            {
                _orchestrator.Primary.ShowCreativeDebugger();
                return;
            }

            if (_orchestrator.Secondary != null && _orchestrator.Secondary.NetworkName == AdNetworkName.AppLovin)
            {
                _orchestrator.Secondary.ShowCreativeDebugger();
                return;
            }

            _log.Warning("Creative Debugger requires AppLovin SDK. AppLovin is not installed or not configured.");
        }

        /// <summary>
        /// Opens the mediation debugger UI. Uses primary network, falls back to secondary if available.
        /// </summary>
        public void ShowMediationDebugger()
        {
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
            if (_orchestrator == null)
            {
                _log.Warning("Cannot show mediation debugger — orchestrator not initialized.");
                return;
            }

            // If no network specified, use primary; fall back to secondary
            if (string.IsNullOrEmpty(networkName))
            {
                _log.Info($"No network specified. Using primary network: {_orchestrator.Primary.NetworkName}");
                _orchestrator.Primary.ShowMediationDebugger();
                return;
            }

            var normalizedName = networkName.ToLowerInvariant();

            if (_orchestrator.Primary.NetworkName == normalizedName)
            {
                _orchestrator.Primary.ShowMediationDebugger();
                return;
            }

            if (_orchestrator.Secondary != null && _orchestrator.Secondary.NetworkName == normalizedName)
            {
                _orchestrator.Secondary.ShowMediationDebugger();
                return;
            }

            // Requested network not found — fall back to primary, then secondary
            _log.Warning($"Network '{networkName}' is not configured. Falling back to primary: {_orchestrator.Primary.NetworkName}");
            _orchestrator.Primary.ShowMediationDebugger();
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
            if (_orchestrator == null)
            {
                _log.Warning("Cannot set test device IDs — orchestrator not initialized.");
                return;
            }

            _orchestrator.Primary.SetTestDeviceIds(testDeviceIds);
            _orchestrator.Secondary?.SetTestDeviceIds(testDeviceIds);

            _log.Info($"Test device IDs set on all networks: [{string.Join(", ", testDeviceIds)}]");
        }

        // --- Placeholder methods ---

        /// <summary>Shows a loading placeholder overlay while an ad is being prepared.</summary>
        public void ShowAdPlaceholder(AdPlaceholderType adType)
        {
            _log.Info($"Showing ad placeholder for type: {adType}");

            if (_adPlaceholderUI == null)
            {
                _log.Warning("Ad placeholder UI is not initialized.");
                return;
            }

            _adPlaceholderUI.ShowAdPlaceholder(adType);
        }

        /// <summary>Closes the ad loading placeholder overlay. No-op if already closed.</summary>
        public void CloseAdPlaceholder()
        {
            if (_hasClosedPlaceholder) return;

            if (_adPlaceholderUI == null)
            {
                _log.Warning("Ad placeholder UI is not initialized.");
                return;
            }

            _log.Info("Closing ad placeholder");
            _adPlaceholderUI.CloseAdPlaceholder();
            _hasClosedPlaceholder = true;
        }

        private bool IsAppLovin()
        {
            if (_mediationType == AdNetworkName.AppLovin) return true;

            _log.Info("Mediation type is not AppLovin. Current: " + _mediationType);
            return false;
        }

        private bool IsAdmob()
        {
            if (_mediationType == AdNetworkName.Admob) return true;

            _log.Info("Mediation type is not Admob. Current: " + _mediationType);
            return false;
        }

        /// <summary>Returns true if the given network instance is AdMob.</summary>
        private bool IsAdmobNetwork(IAdNetwork network) => network.NetworkName == AdNetworkName.Admob;
    }
}
