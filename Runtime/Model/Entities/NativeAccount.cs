using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class NativeAccount
    {
        [JsonProperty("playerId")] public long PlayerId;
        [JsonProperty("gameId")] public long GameId;
        [JsonProperty("rawData")] public string RawData;
        [JsonProperty("lastUpdated")] public long LastUpdated;
    }
}
