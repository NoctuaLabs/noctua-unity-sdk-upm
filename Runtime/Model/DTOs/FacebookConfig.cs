using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class FacebookConfig
    {
        [JsonProperty("android"), JsonRequired] public FacebookAndroidConfig Android;
        [JsonProperty("ios"), JsonRequired] public FacebookIosConfig Ios;
    }

    [Preserve]
    public class FacebookAndroidConfig
    {
        [JsonProperty("appId"), JsonRequired] public string AppId;

        [JsonProperty("clientToken"), JsonRequired] public string ClientToken;
    }

    [Preserve]
    public class FacebookIosConfig
    {
        [JsonProperty("appId"), JsonRequired] public string AppId;

        [JsonProperty("clientToken"), JsonRequired] public string ClientToken;
    }
}
