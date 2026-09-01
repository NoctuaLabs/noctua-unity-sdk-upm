using Newtonsoft.Json;
using UnityEngine.Scripting;
using com.noctuagames.sdk.Campaign;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Root configuration object loaded from the noctua-sdk-config JSON file, containing all SDK module configs.
    /// </summary>
    [Preserve]
    public class GlobalConfig
    {
        /// <summary>OAuth client identifier for this game.</summary>
        [JsonProperty("clientId"), JsonRequired] public string ClientId;
        /// <summary>Server-side game identifier (0 means not set, will be resolved at init).</summary>
        [JsonProperty("gameId")] public long GameID = 0;

        /// <summary>Adjust analytics SDK configuration (optional).</summary>
        [JsonProperty("adjust")] public AdjustConfig Adjust;

        /// <summary>Facebook SDK configuration (optional).</summary>
        [JsonProperty("facebook")] public FacebookConfig Facebook;

        /// <summary>Firebase SDK configuration (optional).</summary>
        [JsonProperty("firebase")] public FirebaseConfig Firebase;

        /// <summary>Noctua-specific SDK configuration (URLs, feature flags, batch settings).</summary>
        [JsonProperty("noctua")] public NoctuaConfig Noctua;

        /// <summary>Co-publisher company information for legal/compliance screens (optional).</summary>
        [JsonProperty("copublisher")] public CoPublisherConfig CoPublisher;

        /// <summary>In-app advertising configuration (optional).</summary>
        [JsonProperty("iaa")] public IAA IAA;

        /// <summary>
        /// Local/offline seed for the server-driven campaign feature. Merged with
        /// <c>remote_configs.live_ops_engagement_campaigns</c> at init (remote wins per campaign id).
        /// </summary>
        [JsonProperty("live_ops_engagement_campaigns")] public CampaignConfig Campaigns;
    }
}
