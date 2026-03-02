using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class FirebaseConfig
    {
        [JsonProperty("android"), JsonRequired] public FirebaseAndroidConfig Android;
        [JsonProperty("ios"), JsonRequired] public FirebaseIosConfig Ios;
    }

    [Preserve]
    public class FirebaseAndroidConfig
    {
        [JsonProperty("customEventDisabled"), JsonRequired] public bool CustomEventDisabled;
    }

    [Preserve]
    public class FirebaseIosConfig
    {
        [JsonProperty("customEventDisabled"), JsonRequired] public bool CustomEventDisabled;
    }
}
