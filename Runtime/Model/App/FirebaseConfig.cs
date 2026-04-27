using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Top-level Firebase configuration containing platform-specific settings.
    /// </summary>
    [Preserve]
    public class FirebaseConfig
    {
        /// <summary>Firebase configuration for Android builds.</summary>
        [JsonProperty("android"), JsonRequired] public FirebaseAndroidConfig Android;
        /// <summary>Firebase configuration for iOS builds.</summary>
        [JsonProperty("ios"), JsonRequired] public FirebaseIosConfig Ios;
    }

    /// <summary>
    /// Firebase configuration specific to Android.
    /// </summary>
    [Preserve]
    public class FirebaseAndroidConfig
    {
        /// <summary>When true, custom event tracking via Firebase Analytics is disabled on Android.</summary>
        [JsonProperty("customEventDisabled"), JsonRequired] public bool CustomEventDisabled;
    }

    /// <summary>
    /// Firebase configuration specific to iOS.
    /// </summary>
    [Preserve]
    public class FirebaseIosConfig
    {
        /// <summary>When true, custom event tracking via Firebase Analytics is disabled on iOS.</summary>
        [JsonProperty("customEventDisabled"), JsonRequired] public bool CustomEventDisabled;
    }
}
