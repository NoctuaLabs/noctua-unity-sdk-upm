using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class CoPublisherConfig
    {
        [JsonProperty("companyName"), JsonRequired] public string CompanyName;
        [JsonProperty("companyWebsiteUrl"), JsonRequired] public string CompanyWebsiteUrl;
        [JsonProperty("companyTermUrl"), JsonRequired] public string CompanyTermUrl;
        [JsonProperty("companyPrivacyUrl"), JsonRequired] public string CompanyPrivacyUrl;
    }
}
