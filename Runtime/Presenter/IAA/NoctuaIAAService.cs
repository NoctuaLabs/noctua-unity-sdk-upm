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
    /// <summary>
    /// Manages ad mediation across multiple ad networks (AdMob, AppLovin), handling initialization,
    /// ad loading, display, revenue tracking, and hybrid fallback orchestration.
    /// </summary>
    public partial class NoctuaIAAService
    {
        private readonly NoctuaLogger _log = new(typeof(NoctuaIAAService));
        // Static logger for the static helper methods (cannot use the instance _log).
        private static readonly NoctuaLogger _sLog = new(typeof(NoctuaIAAService));

        // Greppable tag prefixed to every method-entry log so automation tests can
        // detect that a function ran by matching "NoctuaIAAService.<Method>: [mediation]".
        private const string LogTag = "[mediation]";

        // Captured on the main thread in the constructor; used to dispatch AdMob preload
        // callbacks (which fire on the GMA JNI thread) back to Unity's main thread.
        private readonly SynchronizationContext _mainThreadContext;

        private HybridAdRouting _routing;
        private AdRevenueReporter _revenueTracker;
        private AdFrequencyCap _frequencyCap;
        private AppOpenAds _appOpenAds;
        private AdNetworkPerformanceTracker _performanceTracker;
        private string _mediationType;

        // CPM floor + segmentation (preserved across CreateNetworks calls)
        private UserSegments _segments;
        private CpmFloor _cpmFloor;
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

        private event Action _onCrossPromoDisplayed;
        private event Action _onCrossPromoClicked;
        private event Action _onCrossPromoClosed;
        private event Action _onCrossPromoFailed;

        /// <summary>
        /// Fires when a direct <see cref="ShowCrossPromotion(AdPlaceholderType, CrossPromotionEntry)"/>
        /// creative actually renders on screen. Separate from <see cref="OnAdDisplayed"/> so games
        /// running their own mediation never get cross-wired with real-ad events.
        /// </summary>
        public event Action OnCrossPromoDisplayed { add => _onCrossPromoDisplayed += value; remove => _onCrossPromoDisplayed -= value; }
        /// <summary>Fires when the user taps a direct cross-promotion creative (its click-through).</summary>
        public event Action OnCrossPromoClicked { add => _onCrossPromoClicked += value; remove => _onCrossPromoClicked -= value; }
        /// <summary>Fires when a direct cross-promotion creative is dismissed (user close / auto-close).</summary>
        public event Action OnCrossPromoClosed { add => _onCrossPromoClosed += value; remove => _onCrossPromoClosed -= value; }
        /// <summary>Fires when a direct cross-promotion creative could not be loaded/shown (bad URL, offline, no cache).</summary>
        public event Action OnCrossPromoFailed { add => _onCrossPromoFailed += value; remove => _onCrossPromoFailed -= value; }

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
        private readonly AdmobAdPreloader _admobPreloader = new AdmobAdPreloader();
        // Set only while AdMob is the primary network: non-null means "use the Preload API".
        private static AdmobAdPreloader _preloader;
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
        private readonly AdWatchMilestoneTracker _watchMilestones;
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
        // True while the currently-showing placeholder was triggered by the public ShowCrossPromotion(...)
        // API rather than the mediation fallback. Routes the OnPlaceholder* callbacks to the dedicated
        // OnCrossPromo* events instead of the shared real-ad events. Reset on terminal (closed/failed).
        private bool _crossPromoDirect;
        private bool _suppressNextCloseEvent;

        // The banner cross-promotion is a SEPARATE, non-modal surface (own presenter / UIDocument):
        // it coexists with the full-screen placeholder above, so it has its own independent state.
        private bool _bannerCrossPromoPending;
        private bool _bannerCrossPromoShown;
        private bool _bannerCrossPromoDirect;
        private bool _suppressNextBannerCloseEvent;
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

        public IAA IAAResponse
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
        public AppOpenAds AppOpen => _appOpenAds;

        /// <summary>Returns true if running in hybrid mode (both networks active).</summary>
        public bool IsHybridMode => _routing?.IsHybridMode ?? false;

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
            if (_segments == null) return "not initialized";
            return _segments.GetCompositeSegment(_cachedCountryCode);
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

            if (_cpmFloor == null)
            {
                result["status"] = "CPM floors disabled";
                return result;
            }

            string segmentKey = _segments?.GetCompositeSegment(_cachedCountryCode) ?? "";
            string[] formats = { "interstitial", "rewarded", "banner", "app_open" };

            foreach (string format in formats)
            {
                if (_routing?.Primary != null)
                {
                    string network = _routing.Primary.NetworkName;
                    double avgCpm = _performanceTracker?.GetAverageCpm(network, format) ?? 0;
                    int samples   = _performanceTracker?.GetSampleCount(network, format) ?? 0;
                    var floor     = _cpmFloor.EvaluateFloor(network, format, avgCpm, samples, segmentKey);
                    result[$"{format}/{network}"] = $"{floor} (cpm={avgCpm:F4}, n={samples})";
                }

                if (_routing?.Secondary != null)
                {
                    string network = _routing.Secondary.NetworkName;
                    double avgCpm = _performanceTracker?.GetAverageCpm(network, format) ?? 0;
                    int samples   = _performanceTracker?.GetSampleCount(network, format) ?? 0;
                    var floor     = _cpmFloor.EvaluateFloor(network, format, avgCpm, samples, segmentKey);
                    result[$"{format}/{network}"] = $"{floor} (cpm={avgCpm:F4}, n={samples})";
                }
            }

            return result;
        }

        // Stashed applied_iaa_config payload from the first CreateNetworks call —
        // CreateNetworks runs inside the NoctuaIAAService constructor (via the
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
        /// IAAResponse is set — the routing's segment key is updated either way).
        /// </summary>
        public void SetCountryCode(string countryCode)
        {
            _log.Debug($"{LogTag} set_country_code - set country code");
            _cachedCountryCode = countryCode;
            string segmentKey = _segments?.GetCompositeSegment(countryCode) ?? "";
            _routing?.UpdateSegmentKey(segmentKey);
            _log.Debug($"Country code set to '{countryCode}', segment key: '{segmentKey}'");
        }

        /// <summary>
        /// Records a purchase for the current user, updating the payer tier in PlayerPrefs.
        /// Should be called from the IAP purchase completion callback.
        /// </summary>
        public void RecordPurchase()
        {
            _log.Debug($"{LogTag} record_purchase - record a completed purchase");
            _segments?.RecordPurchase();
        }

        /// <summary>
        /// Returns the segment manager instance (used by Noctua.Initialization.cs to pass
        /// to AdExperimentAssignments without requiring a static reference).
        /// </summary>
        public UserSegments GetSegments()
        {
            _log.Debug($"{LogTag} get_segment_manager - get segment manager");
            return _segments;
        }

        /// <summary>
        /// Applies experiment overrides by updating only the frequency manager and CPM floor manager
        /// with the new effective config — without restarting ad networks.
        /// Called from Noctua.Initialization.cs after country is resolved and experiments are evaluated.
        /// </summary>
        public void ApplyExperimentOverride(IAA effectiveIaa)
        {
            _log.Debug($"{LogTag} apply_experiment_override - apply experiment override");
            if (effectiveIaa == null) return;

            // Ensure AppOpen cooldown has a sensible default
            var mergedCooldowns = effectiveIaa.CooldownSeconds ?? new CooldownConfig();
            if (mergedCooldowns.AppOpen <= 0) mergedCooldowns.AppOpen = 30;

            // Recreate frequency manager with experiment-overridden config
            _frequencyCap = new AdFrequencyCap(
                effectiveIaa.FrequencyCaps,
                mergedCooldowns,
                effectiveIaa.EnabledFormats
            );

            // Recreate CPM floor manager with experiment-overridden config
            _cpmFloor = (effectiveIaa.CpmFloors?.Enabled == true)
                ? new CpmFloor(effectiveIaa.CpmFloors)
                : null;

            // Push new floor manager to the existing routing (no network restart)
            _routing?.UpdateCpmFloor(_cpmFloor);

            _log.Info("Experiment overrides applied: frequency caps and CPM floors updated.");
        }

        public NoctuaIAAService(IAdPlaceholderUI adPlaceholderUI, IAA iAAResponse, IAdRevenueTracker adRevenueTracker = null)
        {
            _log.Debug($"{LogTag} init_constructor - construct mediation manager");
            // Wire the tracker BEFORE setting IAAResponse so CreateNetworks() — called by the
            // IAAResponse setter — can pass it straight through to AdRevenueReporter,
            // eliminating the "created with null tracker" startup warning.
            _adRevenueTracker = adRevenueTracker;
            _watchMilestones = new AdWatchMilestoneTracker((name, payload) => _adRevenueTracker?.TrackCustomEvent(name, payload));

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

            // The banner cross-promotion surface has its own independent lifecycle callbacks.
            _adPlaceholderUI?.SetBannerPlaceholderClosedCallback(OnBannerPlaceholderClosed);
            _adPlaceholderUI?.SetBannerPlaceholderClickedCallback(OnBannerPlaceholderClicked);
            _adPlaceholderUI?.SetBannerPlaceholderShownCallback(OnBannerPlaceholderShown);
            _adPlaceholderUI?.SetBannerPlaceholderFailedCallback(OnBannerPlaceholderFailed);

            if (iAAResponse == null)
            {
                _log.Warning("Constructor received null IAA response. NoctuaIAAService will not be usable.");
                return;
            }

            if (IAAResponse != null)
            {
                _log.Info("IAA response already set in NoctuaIAAService");
                return;
            }

            IAAResponse = iAAResponse;
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
        /// given format (mirrors HybridAdRouting.EvaluateCpmFloor). Returns true when no floor
        /// is configured, when the performance tracker has no data, or on SoftFail/Allow.
        /// </summary>
        private bool IsCpmFloorAcceptable(IAdNetwork network, string format)
        {
            _log.Debug($"{LogTag} is_cpm_floor_acceptable - check CPM floor acceptable");
            if (_cpmFloor == null || _performanceTracker == null) return true;
            if (network == null) return true;

            string segmentKey = _segments?.GetCompositeSegment(_cachedCountryCode) ?? "";
            double avgCpm = _performanceTracker.GetAverageCpm(network.NetworkName, format);
            int samples   = _performanceTracker.GetSampleCount(network.NetworkName, format);
            var result    = _cpmFloor.EvaluateFloor(network.NetworkName, format, avgCpm, samples, segmentKey);

            return result != CpmFloorResult.HardFail;
        }
    }
}
