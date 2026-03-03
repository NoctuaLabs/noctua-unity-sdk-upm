using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Represents an account record stored in the native platform's secure storage (Android Keystore / iOS Keychain).
    /// </summary>
    [Preserve]
    public class NativeAccount
    {
        /// <summary>Player identifier associated with this stored account.</summary>
        [JsonProperty("playerId")] public long PlayerId;
        /// <summary>Game identifier this account belongs to.</summary>
        [JsonProperty("gameId")] public long GameId;
        /// <summary>Serialized JSON payload containing the full account data (UserBundle).</summary>
        [JsonProperty("rawData")] public string RawData;
        /// <summary>Timestamp (milliseconds since epoch) when this account was last updated.</summary>
        [JsonProperty("lastUpdated")] public long LastUpdated;
    }
}
