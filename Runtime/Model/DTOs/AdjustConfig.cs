using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class AdjustConfig
    {
        [JsonProperty("android"), JsonRequired] public AdjustAndroidConfig Android;

        [JsonProperty("ios"), JsonRequired] public AdjustIosConfig Ios;
    }

    [Preserve]
    public class AdjustAndroidConfig
    {
        [JsonProperty("appToken"), JsonRequired] public string AppToken;

        [JsonProperty("environment")] public string Environment = "sandbox";

        [JsonProperty("eventMap")] public Dictionary<string, string> EventMap = new();
    }

    [Preserve]
    public class AdjustIosConfig
    {
        [JsonProperty("appToken"), JsonRequired] public string AppToken;

        [JsonProperty("environment")] public string Environment = "sandbox";

        [JsonProperty("eventMap")] public Dictionary<string, string> EventMap = new();
    }
}
