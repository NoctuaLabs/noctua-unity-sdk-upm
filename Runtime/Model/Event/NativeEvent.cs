using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Represents an analytics event stored in native platform storage, pending upload to the tracker server.
    /// </summary>
    [Preserve]
    public class NativeEvent
    {
        /// <summary>Auto-incremented local event identifier.</summary>
        [JsonProperty("id")] public long Id;
        /// <summary>Serialized JSON payload of the event data.</summary>
        [JsonProperty("eventJson")]
        [JsonConverter(typeof(RawJsonStringConverter))]
        public string EventJson;
        /// <summary>Timestamp (milliseconds since epoch) when the event was created.</summary>
        [JsonProperty("createdAt")] public long CreatedAt;
    }
}
