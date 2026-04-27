using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Contains Adjust attribution data describing the advertising source that led to app installation.
    /// </summary>
    [Preserve]
    public class NoctuaAdjustAttribution
    {
        /// <summary>Adjust tracker token identifier.</summary>
        [JsonProperty("trackerToken")] public string TrackerToken;
        /// <summary>Human-readable tracker name.</summary>
        [JsonProperty("trackerName")] public string TrackerName;
        /// <summary>Ad network name (e.g., "Facebook", "Google Ads").</summary>
        [JsonProperty("network")] public string Network;
        /// <summary>Campaign name within the ad network.</summary>
        [JsonProperty("campaign")] public string Campaign;
        /// <summary>Ad group name within the campaign.</summary>
        [JsonProperty("adgroup")] public string Adgroup;
        /// <summary>Creative/ad variant identifier.</summary>
        [JsonProperty("creative")] public string Creative;
        /// <summary>Click label assigned to the attribution link.</summary>
        [JsonProperty("clickLabel")] public string ClickLabel;
        /// <summary>Adjust device identifier.</summary>
        [JsonProperty("adid")] public string Adid;
        /// <summary>Cost model type (e.g., "CPI", "CPA").</summary>
        [JsonProperty("costType")] public string CostType;
        /// <summary>Cost amount for this attribution.</summary>
        [JsonProperty("costAmount")] public double CostAmount;
        /// <summary>ISO currency code for the cost amount.</summary>
        [JsonProperty("costCurrency")] public string CostCurrency;
        /// <summary>Facebook install referrer string, if available.</summary>
        [JsonProperty("fbInstallReferrer")] public string FbInstallReferrer;

        /// <summary>
        /// Deserializes a JSON string into a <see cref="NoctuaAdjustAttribution"/> instance. Returns an empty instance on failure.
        /// </summary>
        /// <param name="json">JSON string to deserialize.</param>
        /// <returns>Deserialized attribution data, or an empty instance if parsing fails.</returns>
        public static NoctuaAdjustAttribution FromJson(string json)
        {
            if (string.IsNullOrEmpty(json) || json == "{}")
                return new NoctuaAdjustAttribution();
            try
            {
                var settings = new JsonSerializerSettings
                {
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                    NullValueHandling = NullValueHandling.Ignore
                };

                return JsonConvert.DeserializeObject<NoctuaAdjustAttribution>(json, settings)
                    ?? new NoctuaAdjustAttribution();
            }
            catch (Exception)
            {
                return new NoctuaAdjustAttribution();
            }
        }
    }
}
