using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Top-level Adjust SDK configuration containing platform-specific settings.
    /// </summary>
    [Preserve]
    public class AdjustConfig
    {
        /// <summary>Adjust configuration for Android builds.</summary>
        [JsonProperty("android"), JsonRequired] public AdjustAndroidConfig Android;

        /// <summary>Adjust configuration for iOS builds.</summary>
        [JsonProperty("ios"), JsonRequired] public AdjustIosConfig Ios;
    }

    /// <summary>
    /// Adjust SDK configuration specific to Android.
    /// </summary>
    [Preserve]
    public class AdjustAndroidConfig
    {
        /// <summary>Adjust app token for Android.</summary>
        [JsonProperty("appToken"), JsonRequired] public string AppToken;

        /// <summary>Adjust environment: "sandbox" for testing or "production" for release builds.</summary>
        [JsonProperty("environment")] public string Environment = "sandbox";

        /// <summary>Mapping of Noctua event names to Adjust event tokens.</summary>
        [JsonProperty("eventMap")] public Dictionary<string, string> EventMap = new();
    }

    /// <summary>
    /// Adjust SDK configuration specific to iOS.
    /// </summary>
    [Preserve]
    public class AdjustIosConfig
    {
        /// <summary>Adjust app token for iOS.</summary>
        [JsonProperty("appToken"), JsonRequired] public string AppToken;

        /// <summary>Adjust environment: "sandbox" for testing or "production" for release builds.</summary>
        [JsonProperty("environment")] public string Environment = "sandbox";

        /// <summary>Mapping of Noctua event names to Adjust event tokens.</summary>
        [JsonProperty("eventMap")] public Dictionary<string, string> EventMap = new();
    }
}
