using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class NativeEvent
    {
        [JsonProperty("id")] public long Id;
        [JsonProperty("eventJson")]
        [JsonConverter(typeof(RawJsonStringConverter))]
        public string EventJson;
        [JsonProperty("createdAt")] public long CreatedAt;
    }
}
