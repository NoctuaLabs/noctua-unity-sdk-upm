using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class InitGameResponse
    {
        [JsonProperty("country")]
        public string Country;

        [JsonProperty("ip_address")]
        public string IpAddress;

        [JsonProperty("active_product_id")]
        public string ActiveProductId;

        [JsonProperty("remote_configs")]
        public RemoteConfigs RemoteConfigs;

        [JsonProperty("active_bundle_ids")]
        public List<string> ActiveBundleIds;

        [JsonProperty("supported_currencies")]
        public List<string> SupportedCurrencies;

        [JsonProperty("country_to_currency_map")]
        public Dictionary<string, string> CountryToCurrencyMap;

        [JsonProperty("distribution_platform")]
        public string DistributionPlatform;

        [JsonProperty("offline_mode")]
        public bool OfflineMode;
    }

    [Preserve]
    public class RemoteConfigs
    {
        // Special feature flags that:
        // - Does not defined in noctua.gg.json
        // - Require custom value type, not a simple boolean or string or integer
        // - Fully controlled by server

        [JsonProperty("enabled_payment_types")]
        public List<PaymentType> EnabledPaymentTypes;

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

        [JsonProperty("feature_flags")]
        public Dictionary<string, string> RemoteFeatureFlags;
    }

    [Preserve]
    public class IAA
    {
        [JsonProperty("mediation")]
        public string Mediation;

        [JsonProperty("ad_formats")]
        public AdFormatNoctua AdFormat;

    }

    [Preserve]
    public class AdFormatNoctua
    {
        [JsonProperty("interstitial")]
        public AdUnit Interstitial;

        [JsonProperty("rewarded")]
        public AdUnit Rewarded;

        [JsonProperty("rewarded_interstitial")]
        public AdUnit RewardedInterstitial;

        [JsonProperty("banner")]
        public AdUnit Banner;

        [JsonProperty("app_open")]
        public AdUnit AppOpen;

        [JsonProperty("native")]
        public AdUnit Native;
    }

    [Preserve]
    public class AdUnit
    {
        [JsonProperty("android")]
        public AdUnitID Android;

        [JsonProperty("ios")]
        public AdUnitID IOS;

    }

    [Preserve]
    public class AdUnitID
    {
        [JsonProperty("ad_unit_id")]
        public string adUnitID;
    }
}
