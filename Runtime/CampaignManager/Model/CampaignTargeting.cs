using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.Campaign
{
    /// <summary>
    /// Optional per-campaign audience filters. A null field (or null/empty list) means
    /// "no constraint on that dimension".
    /// </summary>
    [Preserve]
    public class CampaignTargeting
    {
        /// <summary>
        /// Player-tag filter (matched against <c>player_remote_configs.tags</c>).
        /// The player must carry at least one of these tags. Null/empty = no tag filter.
        /// </summary>
        [JsonProperty("tags")]
        public List<string> Tags;

        /// <summary>ISO country codes the campaign is allowed in. Null/empty = all countries.</summary>
        [JsonProperty("countries")]
        public List<string> Countries;

        /// <summary>Minimum app version (inclusive), dotted numeric. Null/empty = no lower bound.</summary>
        [JsonProperty("min_app_version")]
        public string MinAppVersion;

        /// <summary>Maximum app version (inclusive), dotted numeric. Null/empty = no upper bound.</summary>
        [JsonProperty("max_app_version")]
        public string MaxAppVersion;
    }

    /// <summary>Per-player display-frequency caps for a campaign.</summary>
    [Preserve]
    public class CampaignFrequency
    {
        /// <summary>Max shows in a rolling 24h window. 0 = unlimited.</summary>
        [JsonProperty("max_per_day")]
        public int MaxPerDay;

        /// <summary>Minimum hours between two shows. 0 = no cooldown.</summary>
        [JsonProperty("cooldown_hours")]
        public int CooldownHours;

        /// <summary>When true, the campaign shows at most once ever on this device.</summary>
        [JsonProperty("once_ever")]
        public bool OnceEver;
    }
}
