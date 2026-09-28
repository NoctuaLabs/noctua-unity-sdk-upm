using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.LiveOpsCampaign
{
    /// <summary>
    /// A player's progress on one live ops key (e.g. a mission), as kept by the live ops progress
    /// tracker. The server only ever raises <see cref="Value"/> and caps it at <see cref="Max"/>.
    /// </summary>
    [Preserve]
    public class LiveOpsProgress
    {
        /// <summary>The progress key, e.g. a mission id configured for the game.</summary>
        [JsonProperty("key")] public string Key;

        /// <summary>Current value, 0 to <see cref="Max"/>.</summary>
        [JsonProperty("value")] public long Value;

        /// <summary>The value at which the key is complete.</summary>
        [JsonProperty("max")] public long Max;

        /// <summary>True once <see cref="Value"/> has reached <see cref="Max"/>.</summary>
        [JsonProperty("completed")] public bool Completed;

        /// <summary>True once the reward was claimed. A key is claimed at most once.</summary>
        [JsonProperty("claimed")] public bool Claimed;

        /// <inheritdoc />
        public override string ToString() =>
            $"{Key}: {Value}/{Max}{(Claimed ? " claimed" : Completed ? " completed" : "")}";
    }
}
