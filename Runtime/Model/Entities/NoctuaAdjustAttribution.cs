using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class NoctuaAdjustAttribution
    {
        [JsonProperty("trackerToken")] public string TrackerToken;
        [JsonProperty("trackerName")] public string TrackerName;
        [JsonProperty("network")] public string Network;
        [JsonProperty("campaign")] public string Campaign;
        [JsonProperty("adgroup")] public string Adgroup;
        [JsonProperty("creative")] public string Creative;
        [JsonProperty("clickLabel")] public string ClickLabel;
        [JsonProperty("adid")] public string Adid;
        [JsonProperty("costType")] public string CostType;
        [JsonProperty("costAmount")] public double CostAmount;
        [JsonProperty("costCurrency")] public string CostCurrency;
        [JsonProperty("fbInstallReferrer")] public string FbInstallReferrer;

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
