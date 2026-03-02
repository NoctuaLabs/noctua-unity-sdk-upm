using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class NoctuaConfig
    {
        public const string DefaultTrackerUrl = "https://sdk-tracker.noctuaprojects.com/api/v1";
        public const string DefaultBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1";
        public const string DefaultSandboxBaseUrl = "https://sandbox-sdk-api-v2.noctuaprojects.com/api/v1";
        public const string DefaultAnnouncementBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/announcements";
        public const string DefaultRewardBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/rewards";
        public const string DefaultCustomerServiceBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/cs";
        public const string DefaultSocialMediaBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/social-media";

        [JsonProperty("trackerUrl")] public string TrackerUrl = DefaultTrackerUrl;

        [JsonProperty("baseUrl")] public string BaseUrl = DefaultBaseUrl;

        [JsonProperty("announcementBaseUrl")] public string AnnouncementBaseUrl = DefaultAnnouncementBaseUrl;

        [JsonProperty("rewardBaseUrl")] public string RewardBaseUrl = DefaultRewardBaseUrl;

        [JsonProperty("socialMediaBaseUrl")] public string SocialMediaBaseUrl = DefaultSocialMediaBaseUrl;

        [JsonProperty("customerServiceBaseUrl")] public string CustomerServiceBaseUrl = DefaultCustomerServiceBaseUrl;
        [JsonProperty("sentryDsnUrl")] public string SentryDsnUrl = "";

        [JsonProperty("trackerBatchSize")] public uint TrackerBatchSize = 20;
        [JsonProperty("trackerBatchPeriodMs")] public uint TrackerBatchPeriodMs = 60_000;
        [JsonProperty("sessionHeartbeatPeriodMs")] public uint SessionHeartbeatPeriodMs = 60_000;
        [JsonProperty("sessionTimeoutMs")] public uint SessionTimeoutMs = 900_000;

        [JsonProperty("sandboxEnabled")] public bool IsSandbox;
        [JsonProperty("region")]  public string Region;

        // Client side feature flags that will not be overrided by server config
        // For feature flags that will be overrided by server config, see NoctuaGameService.cs -> RemoteConfigs
        [JsonProperty("welcomeToastDisabled")] public bool welcomeToastDisabled  = false;
        [JsonProperty("iaaEnabled")] public bool isIAAEnabled  = false;
        [JsonProperty("iapDisabled")] public bool isIAPDisabled  = false;
        [JsonProperty("offlineFirstEnabled")] public bool IsOfflineFirst = false;

        // Deprecated because of inconsistent naming
        // [JsonProperty("isOfflineFirst")] public bool IsOfflineFirst = false;
        // [JsonProperty("isIAAEnabled")] public bool isIAAEnabled  = false;
        [JsonProperty("remoteFeatureFlags")]
        public Dictionary<string, bool> RemoteFeatureFlags;
    }
}
