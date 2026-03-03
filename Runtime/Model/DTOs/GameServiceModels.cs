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
        /// <summary>Mediation provider name (e.g., "applovin", "admob").</summary>
        [JsonProperty("mediation")]
        public string Mediation;

        /// <summary>Ad format definitions with per-platform ad unit IDs.</summary>
        [JsonProperty("ad_formats")]
        public AdFormatNoctua AdFormat;

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
