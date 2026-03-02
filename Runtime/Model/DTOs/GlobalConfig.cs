using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class GlobalConfig
    {
        [JsonProperty("clientId"), JsonRequired] public string ClientId;
        [JsonProperty("gameId")] public long GameID = 0;

        [JsonProperty("adjust")] public AdjustConfig Adjust;

        [JsonProperty("facebook")] public FacebookConfig Facebook;

        [JsonProperty("firebase")] public FirebaseConfig Firebase;

        [JsonProperty("noctua")] public NoctuaConfig Noctua;

        [JsonProperty("copublisher")] public CoPublisherConfig CoPublisher;

        [JsonProperty("iaa")] public IAA IAA;
    }
}
