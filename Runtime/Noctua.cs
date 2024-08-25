using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using System.Threading.Tasks;



namespace com.noctuagames.sdk
{
    public class AdjustConfig
    {
        [JsonProperty("appToken")] public string AppToken { get; set; }

        [JsonProperty("environment")] public string Environment { get; set; }

        [JsonProperty("eventMap")] public Dictionary<string, string> EventMap { get; set; }
    }

public class NoctuaConfig
{
    public const string DefaultTrackerUrl = "https://kafka-proxy-poc.noctuaprojects.com";
    public const string DefaultBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1";
    public const string DefaultSandboxBaseUrl = "https://sandbox-sdk-api-v2.noctuaprojects.com/api/v1";

    [JsonProperty("trackerUrl")]
    public string TrackerUrl { get; set; } = "https://kafka-proxy-poc.noctuaprojects.com";

    [JsonProperty("baseUrl")]
    public string BaseUrl { get; set; } = "https://sandbox-sdk-api-v2.noctuaprojects.com/api/v1";

    [JsonProperty("isSandbox")]
    public bool IsSandbox { get; set; } = false;

}

    public class GlobalConfig
    {
        [JsonProperty("clientId")] public string ClientId { get; set; }

        [JsonProperty("adjust")] public AdjustConfig Adjust { get; set; }

        [JsonProperty("noctua")] public NoctuaConfig Noctua { get; set; }
    }

    public class Noctua
    {
        private static readonly Lazy<Noctua> Instance = new(() => new Noctua());
        public static NoctuaAuthService Auth => Instance.Value._auth;
        public static NoctuaIAPService IAP => Instance.Value._iap;

        public event Action<string> OnPurchaseDone;

        private readonly NoctuaAuthService _auth;
        private readonly NoctuaIAPService _iap;
        #if UNITY_ANDROID && !UNITY_EDITOR
        private readonly GoogleBilling _googleBilling;
        #endif
        private readonly INativePlugin _nativePlugin = GetNativePlugin();
        private bool _initialized = false;
        // Event to forward purchase results to the users of this class
        private Noctua()
        {
            GlobalConfig config = new GlobalConfig();
            Debug.Log("Loading streaming assets...");
            var configPath = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Debug.Log(configPath);
            var configLoadRequest = UnityWebRequest.Get(configPath);
            configLoadRequest.SendWebRequest();
            while (!configLoadRequest.isDone)
            {
                if (configLoadRequest.result == UnityWebRequest.Result.ProtocolError)
                {
                    Debug.Log("Loading streaming assets: configLoadRequest ProtocolError");
                    break;
                }
            }

            if (configLoadRequest.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.Log("Loading streaming assets: configLoadRequest ProtocolError");
            }
            else
            {
                config = JsonConvert.DeserializeObject<GlobalConfig>(configLoadRequest.downloadHandler.text[1..]);
                Debug.Log(config.ClientId);
            }

            // Let's fill the empty fields, if any
            if (string.IsNullOrEmpty(config.Noctua.BaseUrl))
            {
                config.Noctua.BaseUrl = NoctuaConfig.DefaultBaseUrl;
            }

            if (string.IsNullOrEmpty(config.Noctua.TrackerUrl))
            {
                config.Noctua.TrackerUrl = NoctuaConfig.DefaultTrackerUrl;
            }

            if (config.Noctua.IsSandbox)
            {
                config.Noctua.BaseUrl = NoctuaConfig.DefaultSandboxBaseUrl;
            }

            Debug.Log(config.ClientId);
            Debug.Log(config.Noctua.BaseUrl);
            Debug.Log(config.Noctua.TrackerUrl);


            _auth = new NoctuaAuthService(
                new NoctuaAuthService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId
                }
            );

#if UNITY_ANDROID
            _iap = new NoctuaIAPService(
                new NoctuaIAPService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId
                }
            );

            
            // TODO Move to somewhere where the JWT token is already loaded
            _iap.RetryPendingPurchases();
#else
            _iap = null;
#endif
        }

        public static void Init()
        {
            if (Instance.Value._initialized)
            {
                Debug.Log("Noctua.Init() has been called");

                return;
            }
            
            Debug.Log("Noctua.Init()");
            Instance.Value._nativePlugin?.Init();
            #if UNITY_ANDROID && !UNITY_EDITOR
            Instance.Value._googleBilling?.Init();
            #endif
            Instance.Value._initialized = true;
        }

        public static void OnApplicationPause(bool pause)
        {
            Instance.Value._nativePlugin?.OnApplicationPause(pause);
        }

        public static void TrackAdRevenue(
            string source,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Instance.Value._nativePlugin?.TrackAdRevenue(source, revenue, currency, extraPayload);
        }

        public static void TrackPurchase(
            string orderId,
            double amount,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Instance.Value._nativePlugin?.TrackPurchase(orderId, amount, currency, extraPayload);
        }

        public static void TrackCustomEvent(
            string name,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Instance.Value._nativePlugin?.TrackCustomEvent(name, extraPayload);
        }

        public static void PurchaseItem(
            string productId
        )
        {
            Debug.Log("Noctua.PurchaseItem");

            #if UNITY_ANDROID && !UNITY_EDITOR
            Instance.Value._googleBilling?.PurchaseItem(productId);
            #endif
        }

        private static INativePlugin GetNativePlugin()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
                Debug.Log("Plugin is NoctuaAndroidPlugin");
                return new AndroidPlugin();
#elif UNITY_IOS && !UNITY_EDITOR
                Debug.Log("Plugin is NoctuaIPhonePlugin");
                return new IosPlugin();
#else
            Debug.Log("Plugin is null");
            return null;
#endif
        }

    }
}
