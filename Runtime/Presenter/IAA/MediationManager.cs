using System;
using System.Threading;
using UnityEngine;
using System.Collections.Generic;
using com.noctuagames.sdk.AdPlaceholder;
using com.noctuagames.sdk.Events;

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
        // Static logger for the static helper methods (cannot use the instance _log).
        private static readonly NoctuaLogger _sLog = new(typeof(MediationManager));

        // Greppable tag prefixed to every method-entry log so automation tests can
        // detect that a function ran by matching "MediationManager.<Method>: [mediation]".
        private const string LogTag = "[mediation]";

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
        // True once the cross-promotion placeholder has been dismissed for the CURRENT ad request.
        // Reset to false at the start of every game-initiated Show* call. Guards against an async
        // network straggler (e.g. a late OnAdFailedDisplayed arriving after the user already closed
        // the cross-promo) re-showing the placeholder a second time for the same request.
        private bool _hasClosedPlaceholder;
        // Cross-promotion is a FALLBACK house-ad shown only when no real ad displays. Its asset loads
        // asynchronously, so the ad lifecycle is driven by UI callbacks: OnAdDisplayed fires only once
        // the asset actually renders (placeholder "shown"); if the asset can't be loaded the placeholder
        // reports "failed" and we fire OnAdNotAvailable (treated as no ad). OnAdClicked on CTA,
        // OnAdClosed on dismiss. The "no real ad" signal is deferred until that shown/failed outcome.
        private bool _crossPromoPending; // show requested, awaiting the shown/failed callback
        private bool _crossPromoShown;   // asset rendered (OnAdDisplayed fired)
        private string _pendingCrossPromoFormat;
        private AdPlaceholderType _lastRequestedType;
        private bool _suppressNextCloseEvent;
        private bool _adNetworkEventsSubscribed;
        private bool _preloadManagerEventsSubscribed;

        private IAA _iaaResponse;

        // Tracks where the next CreateNetworks pass was triggered from. Defaults
        // to "local" so the constructor's direct assignment (from noctuagg.json)
        // is correctly attributed even when callers don't go through
        // ApplyIaaConfigFromRemote. Surfaced in the applied_iaa_config event
        // payload as the "config_origin" field. (Named "config_origin" rather
        // than "source" to avoid collision with TrackAdRevenue's "source"
        // field, which carries the SDK provider name.)
        public const string IaaConfigOriginLocal          = "local";
        public const string IaaConfigOriginRemoteOverride = "remote_override";

        private string _nextConfigOrigin = IaaConfigOriginLocal;

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
                    // Warm the cross-promotion cache so the placeholder shows instantly when needed
                    // (load-then-show, like mediation ads). No-op when cross-promotion is unconfigured.
                    _log.Debug($"{LogTag} preload_cross_promotion - warm placeholder cache from applied IAA config");
                    _adPlaceholderUI?.PreloadAdPlaceholder(value.CrossPromotion);
                    // Reset back to the default so the next assignment that
                    // doesn't pre-declare its origin isn't mis-tagged.
                    _nextConfigOrigin = IaaConfigOriginLocal;
                }
            }
        }

        /// <summary>
        /// Replaces the active IAA config with one derived from a remote/server
        /// response (typically the local noctuagg.json merged with
        /// initResponse.RemoteConfigs.IAA). The next applied_iaa_config event
        /// emitted by CreateNetworks will be tagged with
        /// <see cref="IaaConfigOriginRemoteOverride"/>.
        /// </summary>
        public void ApplyIaaConfigFromRemote(IAA mergedConfig)
        {
            _log.Debug($"{LogTag} apply_iaa_config - apply remote IAA config");
            _nextConfigOrigin = IaaConfigOriginRemoteOverride;
            IAAResponse       = mergedConfig;
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
            _log.Debug($"{LogTag} get_segment_key - get composite segment key");
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
            _log.Debug($"{LogTag} get_experiment_assignments - get experiment assignments");
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
            _log.Debug($"{LogTag} get_cpm_floor_status - get cpm floor status");
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

        // Stashed applied_iaa_config payload from the first CreateNetworks call —
        // CreateNetworks runs inside the MediationManager constructor (via the
        // IAAResponse setter), which itself is inside Noctua's Lazy<T> factory.
        // The event tracker isn't injected until SetAdRevenueTracker fires later
        // in the composition root, so we defer the emission to that hand-off.
        private Dictionary<string, IConvertible> _pendingAppliedIaaConfigPayload;

        public void SetAdRevenueTracker(IAdRevenueTracker tracker)
        {
            _log.Debug($"{LogTag} set_ad_revenue_tracker - set ad revenue tracker");
            _adRevenueTracker = tracker;
            _revenueTracker?.SetAdRevenueTracker(tracker);
            FlushPendingAppliedIaaConfigEvent();
        }

        /// <summary>
        /// Sets the resolved country code so CPM floor and segmentation can use the correct tier.
        /// Call this after the country is resolved in Noctua.Initialization.cs (before or after
        /// IAAResponse is set — the orchestrator's segment key is updated either way).
        /// </summary>
        internal void SetCountryCode(string countryCode)
        {
            _log.Debug($"{LogTag} set_country_code - set country code");
            _cachedCountryCode = countryCode;
            string segmentKey = _segmentManager?.GetCompositeSegment(countryCode) ?? "";
            _orchestrator?.UpdateSegmentKey(segmentKey);
            _log.Debug($"Country code set to '{countryCode}', segment key: '{segmentKey}'");
        }

        /// <summary>
        /// Records a purchase for the current user, updating the payer tier in PlayerPrefs.
        /// Should be called from the IAP purchase completion callback.
        /// </summary>
        internal void RecordPurchase()
        {
            _log.Debug($"{LogTag} record_purchase - record a completed purchase");
            _segmentManager?.RecordPurchase();
        }

        /// <summary>
        /// Returns the segment manager instance (used by Noctua.Initialization.cs to pass
        /// to AdExperimentManager without requiring a static reference).
        /// </summary>
        internal UserSegmentManager GetSegmentManager()
        {
            _log.Debug($"{LogTag} get_segment_manager - get segment manager");
            return _segmentManager;
        }

        /// <summary>
        /// Applies experiment overrides by updating only the frequency manager and CPM floor manager
        /// with the new effective config — without restarting ad networks.
        /// Called from Noctua.Initialization.cs after country is resolved and experiments are evaluated.
        /// </summary>
        internal void ApplyExperimentOverride(IAA effectiveIaa)
        {
            _log.Debug($"{LogTag} apply_experiment_override - apply experiment override");
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

        public MediationManager(IAdPlaceholderUI adPlaceholderUI, IAA iAAResponse, IAdRevenueTracker adRevenueTracker = null)
        {
            _log.Debug($"{LogTag} init_constructor - construct mediation manager");
            // Wire the tracker BEFORE setting IAAResponse so CreateNetworks() — called by the
            // IAAResponse setter — can pass it straight through to AdRevenueTrackingManager,
            // eliminating the "created with null tracker" startup warning.
            _adRevenueTracker = adRevenueTracker;

            // Must be called from Unity's main thread (it is — Noctua() ctor runs on main thread).
            _mainThreadContext = SynchronizationContext.Current;
            _adPlaceholderUI = adPlaceholderUI;

            // Wire the cross-promotion placeholder into the ad lifecycle: a dismiss fires OnAdClosed
            // (game resumes, no reward) and a CTA tap fires OnAdClicked — the same handlers the game
            // already uses for real ads.
            _adPlaceholderUI?.SetPlaceholderClosedCallback(OnPlaceholderClosed);
            _adPlaceholderUI?.SetPlaceholderClickedCallback(OnPlaceholderClicked);
            _adPlaceholderUI?.SetPlaceholderShownCallback(OnPlaceholderShown);
            _adPlaceholderUI?.SetPlaceholderFailedCallback(OnPlaceholderFailed);

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
            _log.Debug($"{LogTag} create_networks - create ad networks from config");
            // ── Compiled-in SDK availability check ─────────────────────────────
            // Surfaces which mediation SDKs the build was compiled against so
            // game devs can spot a missing UPM package (or unset scripting
            // define) early in the log instead of debugging silent ad failures.
            string admobStatus    = "missing";
            string applovinStatus = "missing";
#if UNITY_ADMOB
            admobStatus = "integrated";
#endif
#if UNITY_APPLOVIN
            applovinStatus = "integrated";
#endif
            _log.Info($"IAA SDK availability: AdMob={admobStatus}, AppLovin={applovinStatus}. " +
                $"Requested in noctuagg.json — mediation='{iaaConfig.Mediation}', " +
                $"secondary_mediation='{iaaConfig.SecondaryMediation}'.");

#if !UNITY_ADMOB && !UNITY_APPLOVIN
            _log.Warning(
                "No ad mediation SDK is integrated in this build. Install AppLovin MAX " +
                "and/or Google Mobile Ads via the Noctua Integration Manager " +
                "(menu: Noctua → Noctua Integration Manager) and ensure UNITY_APPLOVIN / " +
                "UNITY_ADMOB scripting defines are set in Player Settings. Without one of " +
                "these, no ads can be shown and iaa.mediation in noctuagg.json has no effect.");
#endif

            // Clean up existing AppLovin instances before replacing them so that stale
            // handlers are unregistered from the static MaxSdkCallbacks events.
#if UNITY_APPLOVIN
            if (_orchestrator != null)
            {
                (_orchestrator.Primary as AppLovinManager)?.Cleanup();
                (_orchestrator.Secondary as AppLovinManager)?.Cleanup();
            }
#endif

            // Network selection is config-driven: iaa.mediation picks primary,
            // iaa.secondary_mediation picks secondary. The compiled-in SDK
            // defines (UNITY_ADMOB / UNITY_APPLOVIN) gate availability — they
            // do NOT override the config's intent. If the requested primary's
            // SDK isn't compiled in but the secondary's is, the secondary is
            // promoted to primary so the game still gets ads.

            // Diagnose typos / unsupported names early — without this, an
            // unrecognised name would silently fall through to the "no SDK
            // available" error even when the SDK is actually compiled in.
            WarnIfUnknownMediationName("mediation",           iaaConfig.Mediation);
            WarnIfUnknownMediationName("secondary_mediation", iaaConfig.SecondaryMediation);

            // Compile-time SDK availability. Hoisted into local bools so the
            // pure policy function ResolveMediationSelection can be unit-tested
            // without compile-define gymnastics — see MediationSelectionMatrixTest.
            bool admobAvailable    = false;
            bool applovinAvailable = false;
#if UNITY_ADMOB
            admobAvailable = true;
#endif
#if UNITY_APPLOVIN
            applovinAvailable = true;
#endif

            string primaryRequested   = NormalizeMediationName(iaaConfig.Mediation);
            string secondaryRequested = NormalizeMediationName(iaaConfig.SecondaryMediation);

            // Detect diagnostic conditions BEFORE selection so warnings fire
            // with the original config intent intact.
            bool isDuplicate =
                !string.IsNullOrEmpty(primaryRequested) &&
                primaryRequested == secondaryRequested;

            var (selectedPrimary, selectedSecondary) = ResolveMediationSelection(
                iaaConfig.Mediation, iaaConfig.SecondaryMediation,
                admobAvailable, applovinAvailable);

            if (isDuplicate)
            {
                _log.Warning(
                    $"iaa.mediation and iaa.secondary_mediation are both '{primaryRequested}'. " +
                    "Treating as single-network — secondary is ignored. " +
                    "Set them to different networks to enable hybrid mode.");
            }

            // Promotion fired when primary's SDK is missing but secondary's exists.
            bool wasPromoted =
                !isDuplicate &&
                !string.IsNullOrEmpty(primaryRequested) &&
                IsRecognisedMediationName(primaryRequested) &&
                !IsAvailable(primaryRequested, admobAvailable, applovinAvailable) &&
                selectedPrimary != null;
            if (wasPromoted)
            {
                _log.Warning(
                    $"Primary mediation '{iaaConfig.Mediation}' requested but its SDK is not compiled in. " +
                    $"Promoting secondary '{iaaConfig.SecondaryMediation}' to primary.");
            }

            // Secondary requested but its SDK isn't compiled in (and no promotion).
            bool secondaryUnavailable =
                !isDuplicate &&
                !wasPromoted &&
                !string.IsNullOrEmpty(secondaryRequested) &&
                IsRecognisedMediationName(secondaryRequested) &&
                !IsAvailable(secondaryRequested, admobAvailable, applovinAvailable) &&
                selectedPrimary != null;
            if (secondaryUnavailable)
            {
                _log.Warning(
                    $"Secondary mediation '{iaaConfig.SecondaryMediation}' requested but its SDK is not compiled in. " +
                    $"Continuing in single-network mode with primary '{selectedPrimary}'.");
            }

            IAdNetwork primary   = TryCreateNetwork(selectedPrimary);
            IAdNetwork secondary = TryCreateNetwork(selectedSecondary);

            if (primary == null)
            {
                _log.Error(
                    $"No ad network SDK is available for the requested config " +
                    $"(mediation='{iaaConfig.Mediation}', secondary_mediation='{iaaConfig.SecondaryMediation}'). " +
                    "Define UNITY_ADMOB or UNITY_APPLOVIN, or set iaa.mediation in noctuagg.json " +
                    "to a network whose SDK is integrated.");
                // Drop the prior orchestrator reference — its AppLovin handlers
                // were already Cleanup()'d at the top of this method, so leaving
                // it in place would expose half-dead state to subsequent calls.
                _orchestrator = null;
                return;
            }

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

            EmitAppliedIaaConfigEvent(
                primary:           primary.NetworkName,
                secondary:         secondary?.NetworkName,
                hybrid:            _orchestrator.IsHybridMode,
                cpmFloorsEnabled:  _cpmFloorManager != null,
                segmentKey:        segmentKey,
                configOrigin:      _nextConfigOrigin);
        }

        /// <summary>
        /// Builds the <c>applied_iaa_config</c> payload mirroring the "Networks
        /// created" Info log and routes it through the injected NoctuaEventService.
        /// If the tracker isn't yet wired (first CreateNetworks call from the
        /// constructor), the payload is stashed and replayed by
        /// <see cref="FlushPendingAppliedIaaConfigEvent"/> when SetAdRevenueTracker
        /// fires.
        /// </summary>
        private void EmitAppliedIaaConfigEvent(
            string primary,
            string secondary,
            bool   hybrid,
            bool   cpmFloorsEnabled,
            string segmentKey,
            string configOrigin)
        {
            _log.Debug($"{LogTag} emit_applied_iaa_config - emit applied IAA config event");
            var payload = new Dictionary<string, IConvertible>
            {
                { "primary",          primary   ?? "" },
                { "secondary",        secondary ?? "" },
                { "hybrid",           hybrid },
                { "cpm_floors",       cpmFloorsEnabled ? "enabled" : "disabled" },
                { "segment",          segmentKey ?? "" },
                { "config_origin",    configOrigin ?? IaaConfigOriginLocal },
            };

            if (_adRevenueTracker is NoctuaEventService eventService)
            {
                try
                {
                    eventService.TrackCustomEvent("applied_iaa_config", payload);
                }
                catch (Exception ex)
                {
                    _log.Error($"Error emitting applied_iaa_config event: {ex.Message}");
                }
            }
            else
            {
                // Most recent payload wins — if CreateNetworks runs again before
                // the tracker is wired, we want the latest snapshot, not a stale one.
                _pendingAppliedIaaConfigPayload = payload;
                _log.Debug("applied_iaa_config queued — will emit after event tracker is wired.");
            }
        }

        /// <summary>
        /// Replays a deferred <c>applied_iaa_config</c> event the moment a tracker
        /// becomes available. Called from <see cref="SetAdRevenueTracker"/>.
        /// </summary>
        private void FlushPendingAppliedIaaConfigEvent()
        {
            _log.Debug($"{LogTag} flush_applied_iaa_config - flush pending applied IAA config event");
            if (_pendingAppliedIaaConfigPayload == null) return;
            if (_adRevenueTracker is NoctuaEventService eventService)
            {
                try
                {
                    eventService.TrackCustomEvent("applied_iaa_config", _pendingAppliedIaaConfigPayload);
                }
                catch (Exception ex)
                {
                    _log.Error($"Error emitting deferred applied_iaa_config event: {ex.Message}");
                }
                _pendingAppliedIaaConfigPayload = null;
            }
        }

        /// <summary>
        /// Lower-cases and trims a raw mediation name so noctuagg.json can use
        /// any casing (e.g. "AdMob", "ADMOB", "AppLovin") without breaking the
        /// equality-based dispatch in <see cref="TryCreateNetwork"/>.
        /// Returns null for null/empty input so callers can short-circuit.
        /// </summary>
        public static string NormalizeMediationName(string raw)
        {
            _sLog.Debug($"{LogTag} normalize_mediation_name - normalize mediation name");
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return raw.Trim().ToLowerInvariant();
        }

        /// <summary>True iff <paramref name="normalized"/> is one of the supported network names.</summary>
        public static bool IsRecognisedMediationName(string normalized)
        {
            _sLog.Debug($"{LogTag} is_recognised_mediation_name - check mediation name recognised");
            return normalized == AdNetworkName.Admob
                || normalized == AdNetworkName.AppLovin;
        }

        /// <summary>
        /// True iff the (already-normalised) mediation name's SDK is compiled in
        /// per the supplied availability flags.
        /// </summary>
        public static bool IsAvailable(string normalized, bool admobAvailable, bool applovinAvailable)
        {
            _sLog.Debug($"{LogTag} is_network_available - check network availability");
            if (string.IsNullOrEmpty(normalized)) return false;
            if (normalized == AdNetworkName.Admob)    return admobAvailable;
            if (normalized == AdNetworkName.AppLovin) return applovinAvailable;
            return false;
        }

        /// <summary>
        /// Pure selection policy for <c>iaa.mediation</c> / <c>iaa.secondary_mediation</c>.
        /// Returns the (primary, secondary) network names (lower-case "admob" /
        /// "applovin", or null) that the orchestrator should be wired with,
        /// given the requested config and the SDK availability flags.
        ///
        /// Rules, in order:
        ///   1. Normalise both names (lower-case, trim).
        ///   2. If primary == secondary → secondary dropped (dedup).
        ///   3. Each name resolves to itself when its SDK is available, else null.
        ///   4. If primary resolved to null but secondary did not → secondary
        ///      promoted to primary, secondary becomes null.
        ///
        /// This function emits no logs and has no side effects — diagnostic
        /// warnings are emitted by <see cref="CreateNetworks"/> based on the
        /// difference between input and output.
        /// </summary>
        public static (string primary, string secondary) ResolveMediationSelection(
            string primaryRaw,
            string secondaryRaw,
            bool admobAvailable,
            bool applovinAvailable)
        {
            string p = NormalizeMediationName(primaryRaw);
            string s = NormalizeMediationName(secondaryRaw);

            // Dedup — identical names collapse to single-network.
            if (!string.IsNullOrEmpty(p) && p == s)
            {
                s = null;
            }

            string resolvedPrimary   = IsAvailable(p, admobAvailable, applovinAvailable) ? p : null;
            string resolvedSecondary = IsAvailable(s, admobAvailable, applovinAvailable) ? s : null;

            // Promote — fall back to whichever SDK is actually available so the
            // game still gets ads when the configured primary is missing.
            if (resolvedPrimary == null && resolvedSecondary != null)
            {
                resolvedPrimary   = resolvedSecondary;
                resolvedSecondary = null;
            }

            return (resolvedPrimary, resolvedSecondary);
        }

        /// <summary>
        /// Logs a warning when a non-empty mediation name doesn't match any
        /// supported network. Helps surface typos in noctuagg.json instead of
        /// letting them fall through to the generic "no SDK available" error.
        /// </summary>
        private void WarnIfUnknownMediationName(string field, string raw)
        {
            _log.Debug($"{LogTag} warn_unknown_mediation_name - warn on unknown mediation name");
            string normalized = NormalizeMediationName(raw);
            if (string.IsNullOrEmpty(normalized)) return;

            if (normalized != AdNetworkName.Admob &&
                normalized != AdNetworkName.AppLovin)
            {
                _log.Warning(
                    $"Unknown iaa.{field} value '{raw}' in noctuagg.json. " +
                    $"Supported: '{AdNetworkName.Admob}', '{AdNetworkName.AppLovin}'. " +
                    "This entry will be ignored.");
            }
        }

        /// <summary>
        /// Pure factory: returns an <see cref="IAdNetwork"/> for the given mediation
        /// name, or <c>null</c> when the name is empty or the corresponding SDK is
        /// not compiled in. Used by <see cref="CreateNetworks"/> so primary/secondary
        /// selection follows the config rather than the build's SDK defines.
        /// Caller is expected to pass an already-normalised name (see
        /// <see cref="NormalizeMediationName"/>).
        /// </summary>
        private static IAdNetwork TryCreateNetwork(string mediationName)
        {
            _sLog.Debug($"{LogTag} try_create_network - try create network instance");
            if (string.IsNullOrEmpty(mediationName)) return null;

            if (mediationName == AdNetworkName.Admob)
            {
#if UNITY_ADMOB
                return new AdmobManager();
#else
                return null;
#endif
            }

            if (mediationName == AdNetworkName.AppLovin)
            {
#if UNITY_APPLOVIN
                return new AppLovinManager();
#else
                return null;
#endif
            }

            return null;
        }

        /// <summary>
        /// Initializes the ad mediation SDK based on the configured mediation type.
        /// </summary>
        public void Initialize(Action initCompleteAction = null)
        {
            _log.Debug($"{LogTag} initialize - initialize networks and orchestrator");
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
            _log.Debug($"{LogTag} subscribe_orchestrator_events - subscribe orchestrator events");
            if (_adNetworkEventsSubscribed) return;
            _adNetworkEventsSubscribed = true;

            _orchestrator.OnAdDisplayed += () => PostToMainThread(() =>
            {
                CloseAdPlaceholder(force: true);
                _appOpenAdManager?.SetFullscreenAdShowing(true);
                _onAdDisplayed?.Invoke();
            });

            _orchestrator.OnAdFailedDisplayed += () => PostToMainThread(() =>
            {
                _appOpenAdManager?.SetFullscreenAdShowing(false);
                // Show the cross-promotion fallback; only report failure to the game if it can't show.
                if (!ShowCrossPromoFallback(_lastRequestedType)) _onAdFailedDisplayed?.Invoke();
            });

            _orchestrator.OnAdClicked += () => PostToMainThread(() => _onAdClicked?.Invoke());
            _orchestrator.OnAdImpressionRecorded += () => PostToMainThread(() => _onAdImpressionRecorded?.Invoke());

            _orchestrator.OnAdClosed += () => PostToMainThread(() =>
            {
                _appOpenAdManager?.SetFullscreenAdShowing(false);
                _onAdClosed?.Invoke();
            });
        }

        private void SubscribeToNetworkSpecificEvents()
        {
            _log.Debug($"{LogTag} subscribe_network_events - subscribe network-specific events");
            var primary = _orchestrator.Primary;
            var secondary = _orchestrator.Secondary;

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

        /// <summary>
        /// Configures ad unit IDs for all ad formats based on the IAA server response, then loads initial ads.
        /// </summary>
        public void SetupAdUnitID(IAA iAAResponse)
        {
            _log.Debug($"{LogTag} setup_ad_unit_id - resolve and assign ad-unit IDs");
            var primary = _orchestrator.Primary;
            var secondary = _orchestrator.Secondary;

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

                // _preloadManager already assigned in Initialize() callback; reuse the same singleton.
                // RewardedInterstitialAd does NOT support the Preload API — use legacy Load() path.
                if (!string.IsNullOrEmpty(_rewardedInterstitialAdUnitID) && _rewardedInterstitialAdUnitID != "unknown")
                {
                    _rewardedInterstitialAdmob = new RewardedInterstitialAdmob();
                    _rewardedInterstitialAdmob.SetRewardedInterstitialAdUnitID(_rewardedInterstitialAdUnitID);
                    _rewardedInterstitialAdmob.RewardedOnAdDisplayed += () => { CloseAdPlaceholder(force: true); _frequencyManager?.RecordImpression(AdFormatKey.RewardedInterstitial); _onAdDisplayed?.Invoke(); };
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
                    configs.Add(_preloadManager.CreateInterstitialPreloadConfig(_interstitialAdUnitID));
                }
                else
                {
                    _log.Info("Interstitial ad unit ID is missing for AdMob; skipping interstitial preload.");
                }

                if (!string.IsNullOrEmpty(_rewardedAdUnitID) && _rewardedAdUnitID != "unknown")
                {
                    configs.Add(_preloadManager.CreateRewardedPreloadConfig(_rewardedAdUnitID));
                }
                else
                {
                    _log.Info("Rewarded ad unit ID is missing for AdMob; skipping rewarded preload.");
                }

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

                if (configs.Count > 0)
                {
                    _preloadManager.StartPreloading(configs);
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
                onAdNotAvailable: format => NotifyAdNotAvailable(format)
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
            _log.Debug($"{LogTag} setup_secondary_app_open - setup secondary app-open ad units");
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

        // --- Public ad show methods ---

        /// <summary>Loads an interstitial ad from the ad network.</summary>
        public void LoadInterstitialAd()
        {
            _log.Debug($"{LogTag} load_interstitial - load interstitial ad");
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
                _frequencyManager?.RecordImpression(AdFormatKey.Interstitial);
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
            var fallback = failedNetwork == _orchestrator.Primary
                ? _orchestrator.Secondary
                : _orchestrator.Primary;

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
                _frequencyManager?.RecordImpression(AdFormatKey.Interstitial);
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
                    _log.Error($"[MediationManager] PostToMainThread failed ({ex.GetType().Name}: {ex.Message}). Executing inline as fallback.");
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

        /// <summary>Loads a rewarded ad from the ad network.</summary>
        public void LoadRewardedAd()
        {
            _log.Debug($"{LogTag} load_rewarded - load rewarded ad");
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
                _frequencyManager?.RecordImpression(AdFormatKey.Rewarded);
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
            var fallback = failedNetwork == _orchestrator.Primary
                ? _orchestrator.Secondary
                : _orchestrator.Primary;

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
                _frequencyManager?.RecordImpression(AdFormatKey.Rewarded);
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
                AdWatchMilestoneTracker.Default?.RecordWatch(AdFormatKey.Rewarded);
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
                    Noctua.Event.TrackAdRevenue("admob_sdk", revenue, adValue.CurrencyCode, revPayload);
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
        /// Routes a canonical IAA event through <see cref="Noctua.Event.TrackCustomEvent"/>
        /// with a try/catch so analytics failures never break ad delivery. Mirrors
        /// the per-format adapter's EmitCanonical.
        /// </summary>
        private void EmitCanonicalIaa(string eventName, System.Collections.Generic.Dictionary<string, System.IConvertible> payload)
        {
            _log.Debug($"{LogTag} emit_canonical_iaa - emit canonical IAA event");
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
            _log.Info($"{LogTag} show_rewarded_interstitial - show rewarded interstitial");
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show rewarded interstitial ad.");
                return;
            }

            // New game-initiated request — re-arm the cross-promo (clears any dismiss from a prior request).
            _hasClosedPlaceholder = false;

            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.RewardedInterstitial))
            {
                _log.Info("Rewarded interstitial ad blocked by frequency manager.");
                NotifyAdNotAvailable(AdFormatKey.RewardedInterstitial);
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

        /// <summary>Shows a banner ad using the configured ad network, falling back to secondary if needed.</summary>
        public void ShowBannerAd()
        {
            _log.Info($"{LogTag} show_banner - show banner ad");
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show banner ad.");
                return;
            }

            // New game-initiated request — re-arm the cross-promo (clears any dismiss from a prior request).
            _hasClosedPlaceholder = false;

            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.Banner))
            {
                _log.Info("Banner ad blocked by frequency/enabled config.");
                NotifyAdNotAvailable(AdFormatKey.Banner);
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
            NotifyAdNotAvailable(AdFormatKey.Banner);
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
            _log.Debug($"{LogTag} hide_banner - hide banner ad");
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
            _log.Debug($"{LogTag} create_banner_admob - create AdMob banner view");
            if (!IsAdmob() || _orchestrator == null) return;
            _orchestrator.Primary.CreateBannerViewAdAdmob(adSize, adPosition);
        }
#endif

#if UNITY_APPLOVIN
        /// <summary>Creates a banner ad view (AppLovin, deprecated).</summary>
        [Obsolete("Use CreateBannerViewAdAppLovin(Color, MaxSdkBase.AdViewPosition) instead.")]
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.BannerPosition bannerPosition)
        {
            _log.Debug($"{LogTag} create_banner_applovin_banner_pos - create AppLovin banner view (banner pos)");
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.CreateBannerViewAdAppLovin(color, bannerPosition);
        }

        /// <summary>Creates a banner ad view with specified background color and position (AppLovin).</summary>
        public void CreateBannerViewAdAppLovin(Color color, MaxSdkBase.AdViewPosition bannerPosition)
        {
            _log.Debug($"{LogTag} create_banner_applovin_adview_pos - create AppLovin banner view (adview pos)");
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.CreateBannerViewAdAppLovin(color, bannerPosition);
        }

        /// <summary>Hides the currently displayed AppLovin banner ad.</summary>
        public void HideAppLovinBanner()
        {
            _log.Debug($"{LogTag} hide_applovin_banner - hide AppLovin banner");
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.HideBannerAppLovin();
        }

        /// <summary>Destroys the AppLovin banner ad view and releases resources.</summary>
        public void DestroyBannerAppLovin()
        {
            _log.Debug($"{LogTag} destroy_applovin_banner - destroy AppLovin banner");
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.DestroyBannerAppLovin();
        }

        /// <summary>Sets the width of the AppLovin banner ad in pixels.</summary>
        public void SetBannerWidth(int width)
        {
            _log.Debug($"{LogTag} set_banner_width - set banner width");
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.SetBannerWidth(width);
        }

        /// <summary>Gets the current screen position and size of the AppLovin banner ad.</summary>
        public Rect GetBannerPosition()
        {
            _log.Debug($"{LogTag} get_banner_position - get banner position rect");
            if (!IsAppLovin() || _orchestrator == null) return new Rect();
            return _orchestrator.Primary.GetBannerPosition();
        }

        /// <summary>Stops automatic refresh of the AppLovin banner ad.</summary>
        public void StopBannerAutoRefresh()
        {
            _log.Debug($"{LogTag} stop_banner_auto_refresh - stop banner auto-refresh");
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.StopBannerAutoRefresh();
        }

        /// <summary>Starts automatic refresh of the AppLovin banner ad.</summary>
        public void StartBannerAutoRefresh()
        {
            _log.Debug($"{LogTag} start_banner_auto_refresh - start banner auto-refresh");
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.StartBannerAutoRefresh();
        }

        /// <summary>Mutes or unmutes ad audio (AppLovin only).</summary>
        public void SetMuted(bool muted)
        {
            _log.Debug($"{LogTag} set_muted - set muted state");
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.SetMuted(muted);
        }

        /// <summary>Sets the placement name for the AppLovin banner ad.</summary>
        public void SetBannerPlacement(string placement)
        {
            _log.Debug($"{LogTag} set_banner_placement - set banner placement");
            if (!IsAppLovin() || _orchestrator == null) return;
            _orchestrator.Primary.SetBannerPlacement(placement);
        }

        /// <summary>Sets the banner auto-refresh interval in seconds (AppLovin). Clamped to 10-120s.</summary>
        public void SetBannerRefreshInterval(int seconds)
        {
            _log.Debug($"{LogTag} set_banner_refresh_interval - set banner refresh interval");
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
            _log.Info($"{LogTag} show_interstitial_placement - show interstitial (with placement)");
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show interstitial ad.");
                return;
            }

            // New game-initiated request — re-arm the cross-promo (clears any dismiss from a prior request).
            _hasClosedPlaceholder = false;

            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.Interstitial))
            {
                _log.Info("Interstitial ad blocked by frequency manager.");
                NotifyAdNotAvailable(AdFormatKey.Interstitial);
                return;
            }

            var network = _orchestrator.GetNetworkForFormat(AdFormatKey.Interstitial);

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
            _log.Info($"{LogTag} show_rewarded_placement - show rewarded (with placement)");
            if (_orchestrator == null)
            {
                _log.Warning("Orchestrator not initialized. Cannot show rewarded ad.");
                return;
            }

            // New game-initiated request — re-arm the cross-promo (clears any dismiss from a prior request).
            _hasClosedPlaceholder = false;

            if (_frequencyManager != null && !_frequencyManager.CanShowAd(AdFormatKey.Rewarded))
            {
                _log.Info("Rewarded ad blocked by frequency manager.");
                NotifyAdNotAvailable(AdFormatKey.Rewarded);
                return;
            }

            var network = _orchestrator.GetNetworkForFormat(AdFormatKey.Rewarded);

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
            _log.Info($"{LogTag} show_app_open - show app-open ad");
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
            _log.Debug($"{LogTag} is_app_open_ready - check app-open ad ready");
            return _appOpenAdManager?.IsAppOpenAdReady() ?? false;
        }

        /// <summary>
        /// Returns <c>true</c> if an interstitial ad is loaded and ready to display,
        /// and the current frequency cap and cooldown allow showing it.
        /// Use this to conditionally show UI elements like "Watch Ad" buttons.
        /// </summary>
        public bool IsInterstitialReady()
        {
            _log.Debug($"{LogTag} is_interstitial_ready - check interstitial ready");
            if (_orchestrator == null) return false;

            // A configured cross-promotion creative is served by ShowInterstitial() as a house-ad
            // fallback whenever no real ad displays — so report ready to keep the game unblocked even
            // when the network is dry or frequency-capped (parity with the actual show path).
            if (IsCrossPromoAvailable(AdPlaceholderType.Interstitial)) return true;

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
            _log.Debug($"{LogTag} is_interstitial_ready_on_network - check interstitial ready on network");
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
            _log.Debug($"{LogTag} is_rewarded_ready - check rewarded ad ready");
            if (_orchestrator == null) return false;

            // A configured cross-promotion creative is served by ShowRewardedAd() as a house-ad
            // fallback whenever no real ad displays — so report ready to keep the game unblocked.
            // NOTE: the cross-promo grants NO reward (it fires OnAdClosed, never OnUserEarnedReward),
            // so treat a rewarded "ready" as "something will show", not "a reward is guaranteed".
            if (IsCrossPromoAvailable(AdPlaceholderType.Rewarded)) return true;

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
            _log.Debug($"{LogTag} is_rewarded_ready_on_network - check rewarded ready on network");
#if UNITY_ADMOB && !UNITY_EDITOR
            if (IsAdmobNetwork(network) && _preloadManager != null)
                return _preloadManager.IsAdAvailable(_rewardedAdUnitID, AdFormat.REWARDED);
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
            _appOpenAdManager?.OnApplicationForeground();
        }

        // --- Debugger methods ---

        /// <summary>Opens the ad network's creative debugger UI. Always routes to AppLovin.</summary>
        public void ShowCreativeDebugger()
        {
            _log.Debug($"{LogTag} show_creative_debugger - show creative debugger");
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
            _log.Debug($"{LogTag} set_test_device_ids - set test device IDs");
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

        /// <summary>
        /// Arms the cross-promotion placeholder for the given ad format. Nothing is shown yet — the
        /// cross-promotion is a fallback house-ad that only appears if the real ad attempt fails / has
        /// no fill / is offline (see <see cref="CloseAdPlaceholder"/>). If the real ad displays, the
        /// arming is cleared (force-close) so a ready ad never flashes the placeholder.
        /// Arming is a no-op when <c>cross_promotion</c> is not configured for this format.
        /// </summary>
        public void ShowAdPlaceholder(AdPlaceholderType adType)
        {
            // Record the requested format only. The cross-promotion is a FALLBACK — it is shown when
            // the real ad does not display (no fill / fail / offline), via ShowCrossPromoFallback.
            // Nothing is shown up-front, so a ready ad never flashes a placeholder.
            _lastRequestedType = adType;
        }

        /// <summary>Resolves the per-format cross-promotion entry, or null when unset for that format.</summary>
        private static CrossPromotionEntry ResolveCrossPromotionEntry(CrossPromotionConfig config, AdPlaceholderType adType)
        {
            switch (adType)
            {
                case AdPlaceholderType.Interstitial:         return config.Interstitial;
                case AdPlaceholderType.Rewarded:             return config.Rewarded;
                case AdPlaceholderType.RewardedInterstitial: return config.RewardedInterstitial;
                case AdPlaceholderType.Banner:               return config.Banner;
                default:                                     return null;
            }
        }

        /// <summary>
        /// Closes a shown cross-promotion placeholder. Used when a real ad is about to display
        /// (<paramref name="force"/> = true) so the real ad takes the screen; the cross-promotion's
        /// OnAdClosed is suppressed in that case because the real ad fires its own lifecycle. No-op
        /// when no cross-promotion is showing.
        /// </summary>
        /// <param name="force">True when a real ad is taking over — suppresses the cross-promo OnAdClosed.</param>
        public void CloseAdPlaceholder(bool force = false)
        {
            if (_adPlaceholderUI == null) return;
            if (!_crossPromoShown && !_crossPromoPending) return;

            if (force) _suppressNextCloseEvent = true;

            // A real ad is taking over while the cross-promo was still loading: cancel the pending
            // show so its later shown/failed callback is ignored.
            _crossPromoPending = false;

            // OnPlaceholderClosed (the UI close callback) clears _crossPromoShown and fires OnAdClosed
            // unless suppressed.
            _adPlaceholderUI.CloseAdPlaceholder();
        }

        /// <summary>
        /// Requests the cross-promotion placeholder as a fallback house-ad for a format with no real ad
        /// (no fill / not ready / failed display / offline). The asset loads asynchronously: the ad
        /// lifecycle (OnAdDisplayed / OnAdNotAvailable) is driven by the UI's shown/failed callbacks,
        /// not here. Returns true if a show was requested (caller should NOT fire its own failure event —
        /// the outcome arrives via OnPlaceholderShown / OnPlaceholderFailed). Returns false when no
        /// cross-promotion asset is configured for the format.
        /// </summary>
        private bool ShowCrossPromoFallback(AdPlaceholderType type)
        {
            if (_adPlaceholderUI == null) return false;
            if (_crossPromoShown || _crossPromoPending) return true;

            // The user already dismissed the cross-promo for this ad request — a late/duplicate
            // network callback must not resurrect it. Suppress the re-show; the game already received
            // its terminal event (OnAdClosed) when the placeholder was closed. Cleared by the next
            // game-initiated Show* call.
            if (_hasClosedPlaceholder)
            {
                _log.Debug($"{LogTag} cross_promo_fallback - placeholder already dismissed for this request; suppressing re-show ({type})");
                return true;
            }

            var crossPromotion = IAAResponse?.CrossPromotion;
            var entry = crossPromotion == null ? null : ResolveCrossPromotionEntry(crossPromotion, type);
            if (entry == null || string.IsNullOrEmpty(entry.AssetUrl))
            {
                _log.Debug($"{LogTag} cross_promo_fallback - no asset for {type}, nothing to show");
                return false;
            }

            _log.Info($"{LogTag} cross_promo_fallback - requesting cross-promotion for {type} (awaiting asset)");
            _crossPromoPending = true;
            _pendingCrossPromoFormat = PlaceholderTypeToFormat(type);
            _suppressNextCloseEvent = false;
            _adPlaceholderUI.ShowAdPlaceholder(type, entry);
            return true;
        }

        /// <summary>
        /// Invoked when the cross-promotion asset has actually rendered. Enters the ad lifecycle by
        /// firing OnAdDisplayed (the game pauses). OnAdClosed follows on dismiss.
        /// </summary>
        private void OnPlaceholderShown()
        {
            if (!_crossPromoPending) return; // superseded (e.g. force-closed) — ignore
            _crossPromoPending = false;
            _crossPromoShown = true;
            _log.Info($"{LogTag} cross_promo - asset shown, firing OnAdDisplayed");
            _onAdDisplayed?.Invoke();
        }

        /// <summary>
        /// Invoked when the cross-promotion asset could not be loaded/shown (not ready / offline / no
        /// cache). No ad is on screen, so report it through the existing "no ad available" path.
        /// </summary>
        private void OnPlaceholderFailed()
        {
            if (!_crossPromoPending) return; // superseded — ignore
            _crossPromoPending = false;
            _crossPromoShown = false;

            var format = _pendingCrossPromoFormat;
            _pendingCrossPromoFormat = null;
            _log.Info($"{LogTag} cross_promo - asset not ready, reporting OnAdNotAvailable ({format})");
            _onAdNotAvailable?.Invoke(format);
        }

        /// <summary>
        /// Invoked when the cross-promotion placeholder is dismissed (user close / auto-close). Mirrors
        /// a real ad's close by firing OnAdClosed so the game resumes — unless the close was forced by
        /// a real ad taking over (then the real ad's own OnAdClosed fires). Never grants a reward.
        /// </summary>
        private void OnPlaceholderClosed()
        {
            if (!_crossPromoShown) return;
            _crossPromoShown = false;
            // Block any async straggler from re-showing the placeholder for this request (cleared on
            // the next game-initiated Show*). Covers both the user-close and force-close (real ad
            // took over) paths.
            _hasClosedPlaceholder = true;

            if (_suppressNextCloseEvent)
            {
                _suppressNextCloseEvent = false;
                return;
            }

            _log.Info($"{LogTag} cross_promo - placeholder dismissed, firing OnAdClosed (no reward)");
            _onAdClosed?.Invoke();
        }

        /// <summary>Invoked when the user taps the cross-promotion asset; fires OnAdClicked.</summary>
        private void OnPlaceholderClicked()
        {
            _log.Debug($"{LogTag} cross_promo - CTA tapped, firing OnAdClicked");
            _onAdClicked?.Invoke();
        }

        /// <summary>Maps an <see cref="AdFormatKey"/> string to its placeholder type, or null (app open has none).</summary>
        private static AdPlaceholderType? MapFormatToPlaceholderType(string format)
        {
            switch (format)
            {
                case AdFormatKey.Interstitial:         return AdPlaceholderType.Interstitial;
                case AdFormatKey.Rewarded:             return AdPlaceholderType.Rewarded;
                case AdFormatKey.RewardedInterstitial: return AdPlaceholderType.RewardedInterstitial;
                case AdFormatKey.Banner:               return AdPlaceholderType.Banner;
                default:                               return null; // app_open: no placeholder
            }
        }

        /// <summary>Maps a placeholder type back to its <see cref="AdFormatKey"/> string.</summary>
        private static string PlaceholderTypeToFormat(AdPlaceholderType type)
        {
            switch (type)
            {
                case AdPlaceholderType.Interstitial:         return AdFormatKey.Interstitial;
                case AdPlaceholderType.Rewarded:             return AdFormatKey.Rewarded;
                case AdPlaceholderType.RewardedInterstitial: return AdFormatKey.RewardedInterstitial;
                case AdPlaceholderType.Banner:               return AdFormatKey.Banner;
                default:                                     return AdFormatKey.Interstitial;
            }
        }

        /// <summary>
        /// Single choke point for "no ad available" for a format. If a cross-promotion fallback can be
        /// shown, it is shown (firing OnAdDisplayed) and OnAdNotAvailable is deferred — the game gets
        /// OnAdClosed when the user dismisses the cross-promo. Only when no cross-promo can be shown is
        /// the game notified immediately via <see cref="_onAdNotAvailable"/>. All no-fill / not-ready /
        /// frequency-capped / exhausted-fallback paths route through here.
        /// </summary>
        private void NotifyAdNotAvailable(string format)
        {
            var type = MapFormatToPlaceholderType(format);
            if (type.HasValue && ShowCrossPromoFallback(type.Value)) return;
            _onAdNotAvailable?.Invoke(format);
        }

        private bool IsAppLovin()
        {
            _log.Debug($"{LogTag} is_applovin - check primary network is AppLovin");
            if (_mediationType == AdNetworkName.AppLovin) return true;

            _log.Info("Mediation type is not AppLovin. Current: " + _mediationType);
            return false;
        }

        private bool IsAdmob()
        {
            _log.Debug($"{LogTag} is_admob - check primary network is AdMob");
            if (_mediationType == AdNetworkName.Admob) return true;

            _log.Info("Mediation type is not Admob. Current: " + _mediationType);
            return false;
        }

        /// <summary>Returns true if the given network instance is AdMob.</summary>
        private bool IsAdmobNetwork(IAdNetwork network)
        {
            _log.Debug($"{LogTag} is_admob_network - check network is AdMob");
            return network.NetworkName == AdNetworkName.Admob;
        }

        /// <summary>
        /// Returns false when the network's recent CPM is below the configured hard floor for the
        /// given format (mirrors HybridAdOrchestrator.EvaluateCpmFloor). Returns true when no floor
        /// is configured, when the performance tracker has no data, or on SoftFail/Allow.
        /// </summary>
        private bool IsCpmFloorAcceptable(IAdNetwork network, string format)
        {
            _log.Debug($"{LogTag} is_cpm_floor_acceptable - check CPM floor acceptable");
            if (_cpmFloorManager == null || _performanceTracker == null) return true;
            if (network == null) return true;

            string segmentKey = _segmentManager?.GetCompositeSegment(_cachedCountryCode) ?? "";
            double avgCpm = _performanceTracker.GetAverageCpm(network.NetworkName, format);
            int samples   = _performanceTracker.GetSampleCount(network.NetworkName, format);
            var result    = _cpmFloorManager.EvaluateFloor(network.NetworkName, format, avgCpm, samples, segmentKey);

            return result != CpmFloorResult.HardFail;
        }
    }
}
