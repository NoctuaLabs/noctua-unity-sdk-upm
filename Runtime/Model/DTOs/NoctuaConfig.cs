using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Noctua-specific SDK configuration controlling API endpoints, event batching, session tracking, and feature flags.
    /// </summary>
    [Preserve]
    public class NoctuaConfig
    {
        /// <summary>Default event tracker API URL.</summary>
        public const string DefaultTrackerUrl = "https://sdk-tracker.noctuaprojects.com/api/v1";
        /// <summary>Default SDK API base URL.</summary>
        public const string DefaultBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1";
        /// <summary>Default sandbox SDK API base URL.</summary>
        public const string DefaultSandboxBaseUrl = "https://sandbox-sdk-api-v2.noctuaprojects.com/api/v1";
        /// <summary>Default announcements API base URL.</summary>
        public const string DefaultAnnouncementBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/announcements";
        /// <summary>Default rewards API base URL.</summary>
        public const string DefaultRewardBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/rewards";
        /// <summary>Default customer service API base URL.</summary>
        public const string DefaultCustomerServiceBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/cs";
        /// <summary>Default social media API base URL.</summary>
        public const string DefaultSocialMediaBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/social-media";

        /// <summary>URL for the event tracker service.</summary>
        [JsonProperty("trackerUrl")] public string TrackerUrl = DefaultTrackerUrl;

        /// <summary>Base URL for the main SDK API.</summary>
        [JsonProperty("baseUrl")] public string BaseUrl = DefaultBaseUrl;

        /// <summary>Base URL for the announcements API.</summary>
        [JsonProperty("announcementBaseUrl")] public string AnnouncementBaseUrl = DefaultAnnouncementBaseUrl;

        /// <summary>Base URL for the rewards API.</summary>
        [JsonProperty("rewardBaseUrl")] public string RewardBaseUrl = DefaultRewardBaseUrl;

        /// <summary>Base URL for the social media API.</summary>
        [JsonProperty("socialMediaBaseUrl")] public string SocialMediaBaseUrl = DefaultSocialMediaBaseUrl;

        /// <summary>Base URL for the customer service API.</summary>
        [JsonProperty("customerServiceBaseUrl")] public string CustomerServiceBaseUrl = DefaultCustomerServiceBaseUrl;
        /// <summary>Sentry DSN URL for error reporting (empty string to disable).</summary>
        [JsonProperty("sentryDsnUrl")] public string SentryDsnUrl = "";

        /// <summary>Maximum number of events to batch before flushing to the tracker.</summary>
        [JsonProperty("trackerBatchSize")] public uint TrackerBatchSize = 20;
        /// <summary>Maximum time in milliseconds between tracker batch flushes.</summary>
        [JsonProperty("trackerBatchPeriodMs")] public uint TrackerBatchPeriodMs = 60_000;
        /// <summary>Interval in milliseconds between session heartbeat pings.</summary>
        [JsonProperty("sessionHeartbeatPeriodMs")] public uint SessionHeartbeatPeriodMs = 60_000;
        /// <summary>Session timeout in milliseconds; a new session starts after this period of inactivity.</summary>
        [JsonProperty("sessionTimeoutMs")] public uint SessionTimeoutMs = 900_000;

        /// <summary>When true, the SDK operates against the sandbox API environment.</summary>
        [JsonProperty("sandboxEnabled")] public bool IsSandbox;
        /// <summary>Region identifier used for geo-specific behavior (e.g., "VN", "ID").</summary>
        [JsonProperty("region")]  public string Region;

        // Client side feature flags that will not be overrided by server config
        // For feature flags that will be overrided by server config, see NoctuaGameService.cs -> RemoteConfigs
        /// <summary>When true, the welcome toast notification is suppressed on login.</summary>
        [JsonProperty("welcomeToastDisabled")] public bool welcomeToastDisabled  = false;
        /// <summary>When true, in-app advertising (IAA) features are enabled.</summary>
        [JsonProperty("iaaEnabled")] public bool isIAAEnabled  = false;
        /// <summary>When true, in-app purchase (IAP) features are disabled.</summary>
        [JsonProperty("iapDisabled")] public bool isIAPDisabled  = false;
        /// <summary>When true, the SDK initializes in offline-first mode without requiring network connectivity.</summary>
        [JsonProperty("offlineFirstEnabled")] public bool IsOfflineFirst = false;

        // Deprecated because of inconsistent naming
        // [JsonProperty("isOfflineFirst")] public bool IsOfflineFirst = false;
        // [JsonProperty("isIAAEnabled")] public bool isIAAEnabled  = false;
        /// <summary>Client-side remote feature flags as key-value pairs (overridden by server config at runtime).</summary>
        [JsonProperty("remoteFeatureFlags")]
        public Dictionary<string, bool> RemoteFeatureFlags;
    }
}
