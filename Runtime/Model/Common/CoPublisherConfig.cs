using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Configuration for a co-publishing partner, displayed in legal/compliance UI screens.
    /// </summary>
    [Preserve]
    public class CoPublisherConfig
    {
        /// <summary>Display name of the co-publishing company.</summary>
        [JsonProperty("companyName"), JsonRequired] public string CompanyName;
        /// <summary>URL of the co-publisher's website.</summary>
        [JsonProperty("companyWebsiteUrl"), JsonRequired] public string CompanyWebsiteUrl;
        /// <summary>URL of the co-publisher's terms of service page.</summary>
        [JsonProperty("companyTermUrl"), JsonRequired] public string CompanyTermUrl;
        /// <summary>URL of the co-publisher's privacy policy page.</summary>
        [JsonProperty("companyPrivacyUrl"), JsonRequired] public string CompanyPrivacyUrl;
    }
}
