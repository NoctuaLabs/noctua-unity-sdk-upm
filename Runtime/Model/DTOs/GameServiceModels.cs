using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Response returned by the game initialization API, containing region info, remote configs, and feature flags.
    /// </summary>
    [Preserve]
    public class InitGameResponse
    {
        /// <summary>ISO country code detected from the user's IP address.</summary>
        [JsonProperty("country")]
        public string Country;

        /// <summary>User's public IP address as detected by the server.</summary>
        [JsonProperty("ip_address")]
        public string IpAddress;

        /// <summary>Currently active product identifier for this game/platform.</summary>
        [JsonProperty("active_product_id")]
        public string ActiveProductId;

        /// <summary>Server-provided remote configuration and feature flags.</summary>
        [JsonProperty("remote_configs")]
        public RemoteConfigs RemoteConfigs;

        /// <summary>List of bundle identifiers that are active for this game.</summary>
        [JsonProperty("active_bundle_ids")]
        public List<string> ActiveBundleIds;

        /// <summary>List of ISO currency codes supported for in-app purchases.</summary>
        [JsonProperty("supported_currencies")]
        public List<string> SupportedCurrencies;

        /// <summary>Mapping of ISO country codes to their default ISO currency codes.</summary>
        [JsonProperty("country_to_currency_map")]
        public Dictionary<string, string> CountryToCurrencyMap;

        /// <summary>Distribution platform determined by the server (e.g., "google", "apple").</summary>
        [JsonProperty("distribution_platform")]
        public string DistributionPlatform;

        /// <summary>When true, the server instructs the SDK to operate in offline mode.</summary>
        [JsonProperty("offline_mode")]
        public bool OfflineMode;
    }

    /// <summary>
    /// Server-provided remote configuration containing feature flags, enabled payment types, and ad settings.
    /// </summary>
    [Preserve]
    public class RemoteConfigs
    {
        // Special feature flags that:
        // - Does not defined in noctua.gg.json
        // - Require custom value type, not a simple boolean or string or integer
        // - Fully controlled by server

        /// <summary>List of payment types enabled by the server for this game/region.</summary>
        [JsonProperty("enabled_payment_types")]
        public List<PaymentType> EnabledPaymentTypes;

        /// <summary>In-app advertising configuration provided by the server.</summary>
        [JsonProperty("iaa")]
        public IAA IAA;

        // Generic feature flags that could be defined in client side, but will be overrided by server, if any
        //
        // This contains key value pairs, the value is always in string format despite the parsed value is boolean or integer
        // Current available remote configs:
        // - ssoDisabled - Boolean
        // - vnLegalPurposeEnabled - Boolean
        // - vnLegalPurposeFullKycEnabled - Boolean
        // - vnLegalPurposePhoneNumberVerificationEnabled - Boolean

        /// <summary>Generic feature flags as string key-value pairs (values are stringified booleans/integers).</summary>
        [JsonProperty("feature_flags")]
        public Dictionary<string, string> RemoteFeatureFlags;
    }

    /// <summary>
    /// In-app advertising configuration specifying the mediation provider and available ad formats.
    /// </summary>
    [Preserve]
    public class IAA
    {
        /// <summary>Primary mediation provider name (e.g., "applovin", "admob").</summary>
        [JsonProperty("mediation")]
        public string Mediation;

        /// <summary>Secondary mediation provider for hybrid fallback. When non-null, enables hybrid mode.</summary>
        [JsonProperty("secondary_mediation")]
        public string SecondaryMediation;

        /// <summary>Per-network ad unit configurations keyed by network name (e.g., "admob", "applovin").</summary>
        [JsonProperty("networks")]
        public Dictionary<string, NetworkConfig> Networks;

        /// <summary>Per-format network overrides (e.g., {"interstitial":"admob","rewarded":"applovin"}).</summary>
        [JsonProperty("ad_format_overrides")]
        public Dictionary<string, string> AdFormatOverrides;

        /// <summary>Ad format definitions with per-platform ad unit IDs (flat, backward compat).</summary>
        [JsonProperty("ad_formats")]
        public AdFormatNoctua AdFormat;

        /// <summary>Per-format frequency cap configuration.</summary>
        [JsonProperty("frequency_caps")]
        public FrequencyCapConfig FrequencyCaps;

        /// <summary>Per-format minimum seconds between ad impressions.</summary>
        [JsonProperty("cooldown_seconds")]
        public CooldownConfig CooldownSeconds;

        /// <summary>Per-format feature flags for enabling/disabling ad formats.</summary>
        [JsonProperty("enabled_formats")]
        public EnabledFormatsConfig EnabledFormats;

        /// <summary>When true, enables performance-based dynamic network routing.</summary>
        [JsonProperty("dynamic_optimization")]
        public bool? DynamicOptimization;

        /// <summary>When true, automatically shows an app open ad on each foreground transition.</summary>
        [JsonProperty("app_open_auto_show")]
        public bool? AppOpenAutoShow;

        /// <summary>Taichi tROAS integration thresholds. When null, Taichi tracking is disabled.</summary>
        [JsonProperty("taichi")]
        public TaichiConfig Taichi;

        /// <summary>
        /// Returns a new IAA config where fields from <paramref name="remote"/> override
        /// only when they are non-null. Fields absent in the remote response retain their
        /// local values, so game developers can set defaults in noctuagg.json that survive
        /// a partial remote config.
        /// </summary>
        public IAA MergeWith(IAA remote)
        {
            if (remote == null) return this;

            return new IAA
            {
                Mediation           = remote.Mediation           ?? Mediation,
                SecondaryMediation  = remote.SecondaryMediation  ?? SecondaryMediation,
                Networks            = remote.Networks            ?? Networks,
                AdFormatOverrides   = remote.AdFormatOverrides   ?? AdFormatOverrides,
                AdFormat            = remote.AdFormat            ?? AdFormat,
                FrequencyCaps       = remote.FrequencyCaps       ?? FrequencyCaps,
                CooldownSeconds     = remote.CooldownSeconds     ?? CooldownSeconds,
                EnabledFormats      = MergeEnabledFormats(EnabledFormats, remote.EnabledFormats),
                DynamicOptimization = remote.DynamicOptimization ?? DynamicOptimization,
                AppOpenAutoShow     = remote.AppOpenAutoShow     ?? AppOpenAutoShow,
                Taichi              = remote.Taichi              ?? Taichi,
            };
        }

        private static EnabledFormatsConfig MergeEnabledFormats(
            EnabledFormatsConfig local, EnabledFormatsConfig remote)
        {
            if (remote == null) return local ?? new EnabledFormatsConfig();
            var base_ = local ?? new EnabledFormatsConfig();

            return new EnabledFormatsConfig
            {
                Interstitial         = remote.Interstitial         ?? base_.Interstitial,
                Rewarded             = remote.Rewarded             ?? base_.Rewarded,
                RewardedInterstitial = remote.RewardedInterstitial ?? base_.RewardedInterstitial,
                Banner               = remote.Banner               ?? base_.Banner,
                AppOpen              = remote.AppOpen              ?? base_.AppOpen,
            };
        }
    }

    /// <summary>
    /// Taichi tROAS (Target Return On Ad Spend) tracking configuration.
    /// Controls revenue and impression thresholds for Taichi custom events.
    /// </summary>
    [Preserve]
    public class TaichiConfig
    {
        /// <summary>
        /// Total ad revenue threshold in USD. Fires Total_Ads_Revenue_001 when cumulative revenue crosses this value.
        /// Default: 0.01
        /// </summary>
        [JsonProperty("revenue_threshold")]
        public float RevenueThreshold = 0.01f;

        /// <summary>
        /// Total ad impression threshold. Fires TenAdsShown when combined impression count crosses this value.
        /// Default: 10
        /// </summary>
        [JsonProperty("ad_count_threshold")]
        public int AdCountThreshold = 10;

        /// <summary>
        /// Combined interstitial + rewarded impression threshold. Fires taichi_total_ad_impression.
        /// Default: 10
        /// </summary>
        [JsonProperty("total_impression_threshold")]
        public int TotalImpressionThreshold = 10;

        /// <summary>
        /// Interstitial-only impression threshold. Fires taichi_interstitial_ad_impression.
        /// Default: 10
        /// </summary>
        [JsonProperty("interstitial_count_threshold")]
        public int InterstitialCountThreshold = 10;

        /// <summary>
        /// Rewarded-only impression threshold. Fires taichi_rewarded_ad_impression.
        /// Default: 10
        /// </summary>
        [JsonProperty("rewarded_count_threshold")]
        public int RewardedCountThreshold = 10;

        /// <summary>
        /// Rewarded-only revenue threshold in USD. Fires taichi_rewarded_ad_revenue.
        /// Default: 0.01
        /// </summary>
        [JsonProperty("rewarded_revenue_threshold")]
        public float RewardedRevenueThreshold = 0.01f;
    }

    /// <summary>
    /// Per-network ad configuration containing ad format definitions.
    /// </summary>
    [Preserve]
    public class NetworkConfig
    {
        /// <summary>Ad format definitions with per-platform ad unit IDs for this network.</summary>
        [JsonProperty("ad_formats")]
        public AdFormatNoctua AdFormat;
    }

    /// <summary>
    /// Per-format frequency cap configuration specifying maximum impressions within a time window.
    /// </summary>
    [Preserve]
    public class FrequencyCapConfig
    {
        /// <summary>Frequency cap for interstitial ads.</summary>
        [JsonProperty("interstitial")]
        public FrequencyCapEntry Interstitial;

        /// <summary>Frequency cap for rewarded ads.</summary>
        [JsonProperty("rewarded")]
        public FrequencyCapEntry Rewarded;

        /// <summary>Frequency cap for rewarded interstitial ads.</summary>
        [JsonProperty("rewarded_interstitial")]
        public FrequencyCapEntry RewardedInterstitial;

        /// <summary>Frequency cap for banner ads.</summary>
        [JsonProperty("banner")]
        public FrequencyCapEntry Banner;

        /// <summary>Frequency cap for app open ads.</summary>
        [JsonProperty("app_open")]
        public FrequencyCapEntry AppOpen;
    }

    /// <summary>
    /// A single frequency cap entry specifying maximum impressions within a time window.
    /// </summary>
    [Preserve]
    public class FrequencyCapEntry
    {
        /// <summary>Maximum number of impressions allowed within the time window.</summary>
        [JsonProperty("max_impressions")]
        public int MaxImpressions;

        /// <summary>Time window in seconds for the frequency cap.</summary>
        [JsonProperty("window_seconds")]
        public int WindowSeconds;
    }

    /// <summary>
    /// Per-format minimum cooldown seconds between consecutive ad impressions.
    /// </summary>
    [Preserve]
    public class CooldownConfig
    {
        /// <summary>Cooldown seconds for interstitial ads.</summary>
        [JsonProperty("interstitial")]
        public int Interstitial;

        /// <summary>Cooldown seconds for rewarded ads.</summary>
        [JsonProperty("rewarded")]
        public int Rewarded;

        /// <summary>Cooldown seconds for rewarded interstitial ads.</summary>
        [JsonProperty("rewarded_interstitial")]
        public int RewardedInterstitial;

        /// <summary>Cooldown seconds for banner ads.</summary>
        [JsonProperty("banner")]
        public int Banner;

        /// <summary>Cooldown seconds for app open ads.</summary>
        [JsonProperty("app_open")]
        public int AppOpen;
    }

    /// <summary>
    /// Per-format feature flags for enabling or disabling individual ad formats.
    /// </summary>
    [Preserve]
    public class EnabledFormatsConfig
    {
        /// <summary>Whether interstitial ads are enabled. Null means "not specified" (defaults to true at usage site).</summary>
        [JsonProperty("interstitial")]
        public bool? Interstitial;

        /// <summary>Whether rewarded ads are enabled. Null means "not specified" (defaults to true at usage site).</summary>
        [JsonProperty("rewarded")]
        public bool? Rewarded;

        /// <summary>Whether rewarded interstitial ads are enabled. Null means "not specified" (defaults to true at usage site).</summary>
        [JsonProperty("rewarded_interstitial")]
        public bool? RewardedInterstitial;

        /// <summary>Whether banner ads are enabled. Null means "not specified" (defaults to true at usage site).</summary>
        [JsonProperty("banner")]
        public bool? Banner;

        /// <summary>Whether app open ads are enabled. Null means "not specified" (defaults to true at usage site).</summary>
        [JsonProperty("app_open")]
        public bool? AppOpen;
    }

    /// <summary>
    /// Collection of ad unit configurations for each supported ad format.
    /// </summary>
    [Preserve]
    public class AdFormatNoctua
    {
        /// <summary>Interstitial ad unit configuration.</summary>
        [JsonProperty("interstitial")]
        public AdUnit Interstitial;

        /// <summary>Rewarded ad unit configuration.</summary>
        [JsonProperty("rewarded")]
        public AdUnit Rewarded;

        /// <summary>Rewarded interstitial ad unit configuration.</summary>
        [JsonProperty("rewarded_interstitial")]
        public AdUnit RewardedInterstitial;

        /// <summary>Banner ad unit configuration.</summary>
        [JsonProperty("banner")]
        public AdUnit Banner;

        /// <summary>App open ad unit configuration.</summary>
        [JsonProperty("app_open")]
        public AdUnit AppOpen;

        /// <summary>Native ad unit configuration.</summary>
        [JsonProperty("native")]
        public AdUnit Native;
    }

    /// <summary>
    /// Platform-specific ad unit identifiers for a single ad format.
    /// </summary>
    [Preserve]
    public class AdUnit
    {
        /// <summary>Android ad unit identifier.</summary>
        [JsonProperty("android")]
        public AdUnitID Android;

        /// <summary>iOS ad unit identifier.</summary>
        [JsonProperty("ios")]
        public AdUnitID IOS;

    }

    /// <summary>
    /// Wraps a single ad unit ID string for a specific platform.
    /// </summary>
    [Preserve]
    public class AdUnitID
    {
        /// <summary>The mediation network ad unit identifier string.</summary>
        [JsonProperty("ad_unit_id")]
        public string adUnitID;
    }
}
