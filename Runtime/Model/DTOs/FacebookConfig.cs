using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Top-level Facebook SDK configuration containing platform-specific settings.
    /// </summary>
    [Preserve]
    public class FacebookConfig
    {
        /// <summary>Facebook configuration for Android builds.</summary>
        [JsonProperty("android"), JsonRequired] public FacebookAndroidConfig Android;
        /// <summary>Facebook configuration for iOS builds.</summary>
        [JsonProperty("ios"), JsonRequired] public FacebookIosConfig Ios;
    }

    /// <summary>
    /// Facebook SDK configuration specific to Android.
    /// </summary>
    [Preserve]
    public class FacebookAndroidConfig
    {
        /// <summary>Facebook application ID for Android.</summary>
        [JsonProperty("appId"), JsonRequired] public string AppId;

        /// <summary>Facebook client token for Android.</summary>
        [JsonProperty("clientToken"), JsonRequired] public string ClientToken;
    }

    /// <summary>
    /// Facebook SDK configuration specific to iOS.
    /// </summary>
    [Preserve]
    public class FacebookIosConfig
    {
        /// <summary>Facebook application ID for iOS.</summary>
        [JsonProperty("appId"), JsonRequired] public string AppId;

        /// <summary>Facebook client token for iOS.</summary>
        [JsonProperty("clientToken"), JsonRequired] public string ClientToken;
    }
}
