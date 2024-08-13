using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;

namespace com.noctuagames.sdk
{

    public class AdjustConfig
{
    [JsonProperty("appToken")]
    public string AppToken { get; set; }

    [JsonProperty("environment")]
    public string Environment { get; set; }

    [JsonProperty("eventMap")]
    public Dictionary<string, string> EventMap { get; set; }
}

public class NoctuaConfig
{
    [JsonProperty("trackerUrl")]
    public string TrackerUrl { get; set; }

    [JsonProperty("baseUrl")]
    public string BaseUrl { get; set; }
}

public class GlobalConfig
{
    [JsonProperty("clientId")]
    public string ClientId { get; set; }

    [JsonProperty("adjust")]
    public AdjustConfig Adjust { get; set; }

    [JsonProperty("noctua")]
    public NoctuaConfig Noctua { get; set; }
}

    public class Noctua
    {
        public static readonly NoctuaAuthService Auth;
        private static readonly INativePlugin Plugin = GetNativePlugin();

        static Noctua()
        {
            GlobalConfig config = new GlobalConfig();
            Debug.Log("Loading streaming assets...");
            var configPath = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Debug.Log(configPath);
            var loadingRequest = UnityWebRequest.Get(configPath);
            loadingRequest.SendWebRequest();
            while (!loadingRequest.isDone) {
                if (loadingRequest.result == UnityWebRequest.Result.ProtocolError) {
                    Debug.Log("Loading streaming assets: loadingRequest ProtocolError");
                    break;
                }
            }
            if (loadingRequest.result == UnityWebRequest.Result.ProtocolError) {
                    Debug.Log("Loading streaming assets: loadingRequest ProtocolError");
            } else {
                File.WriteAllBytes(Path.Combine(Application.persistentDataPath , "your.bytes"), loadingRequest.downloadHandler.data);
                string jsonStr;
                jsonStr = System.Text.Encoding.UTF8.GetString(loadingRequest.downloadHandler.data, 3, loadingRequest.downloadHandler.data.Length - 3);
                config = JsonConvert.DeserializeObject<GlobalConfig>(jsonStr);
            }

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