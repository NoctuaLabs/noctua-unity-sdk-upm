using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace com.noctuagames.sdk
{
    public class NoctuaConfig
    {
        [JsonProperty("baseUrl")] public string BaseUrl;
    }

    public class GlobalConfig
    {
        [JsonProperty("noctua")] public NoctuaConfig Noctua;
        [JsonProperty("clientId")] public string ClientId;
    }

    public class Noctua
    {
        public static readonly NoctuaAuthService Auth;
        private static readonly INativePlugin Plugin = GetNativePlugin();

        static Noctua()
        {
            var configPath = Application.streamingAssetsPath + "/noctuagg.json";
            var config = JsonConvert.DeserializeObject<GlobalConfig>(System.IO.File.ReadAllText(configPath));

            Auth = new NoctuaAuthService(
                new NoctuaAuthService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId
                }
            );
        }

        public static void Init()
        {
            Plugin?.Init();
        }

        public static void OnApplicationPause(bool pause)
        {
            Plugin?.OnApplicationPause(pause);
        }

        public static void TrackAdRevenue(
            string source,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin?.TrackAdRevenue(source, revenue, currency, extraPayload);
        }

        public static void TrackPurchase(
            string orderId,
            double amount,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin?.TrackPurchase(orderId, amount, currency, extraPayload);
        }

        public static void TrackCustomEvent(
            string name,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin?.TrackCustomEvent(name, extraPayload);
        }

        private static INativePlugin GetNativePlugin()
        {
#if UNITY_ANDROID
                Debug.Log("Plugin is NoctuaAndroidPlugin");
                return new AndroidPlugin();
#elif UNITY_IOS
                Debug.Log("Plugin is NoctuaIPhonePlugin");
                return new IosPlugin();
#else
            Debug.Log("Plugin is null");
            return null;
#endif
        }
    }
}