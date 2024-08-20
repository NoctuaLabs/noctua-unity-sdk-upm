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
    [JsonProperty("appToken")]
    public string AppToken { get; set; }

    [JsonProperty("environment")]
    public string Environment { get; set; }

    [JsonProperty("eventMap")]
    public Dictionary<string, string> EventMap { get; set; }
}

public class NoctuaConfig
{
    public const string DefaultTrackerUrl = "https://kafka-proxy-poc.noctuaprojects.com";
    public const string DefaultBaseUrl = "https://sdk-srv-new.noctuaprojects.com";
    public const string DefaultSandboxBaseUrl = "https://sdk-srv-new-sandbox.noctuaprojects.com";

    [JsonProperty("trackerUrl")]
    public string TrackerUrl { get; set; } = "https://kafka-proxy-poc.noctuaprojects.com";

    [JsonProperty("baseUrl")]
    public string BaseUrl { get; set; } = "https://sdk-srv-new.noctuaprojects.com";

    [JsonProperty("isSandbox")]
    public bool IsSandbox { get; set; } = false;
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
        public static readonly NoctuaIAPService IAP;
        private static readonly INativePlugin Plugin = GetNativePlugin();
        private static readonly GoogleBilling GoogleBillingInstance = new GoogleBilling();

        // Event to forward purchase results to the users of this class
        public static event Action<string> OnPurchaseDone;

        static Noctua()
        {
            GlobalConfig config = new GlobalConfig();
            Debug.Log("Loading streaming assets...");
            var configPath = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Debug.Log(configPath);
            var configLoadRequest = UnityWebRequest.Get(configPath);
            configLoadRequest.SendWebRequest();
            while (!configLoadRequest.isDone) {
                if (configLoadRequest.result == UnityWebRequest.Result.ProtocolError) {
                    Debug.Log("Loading streaming assets: configLoadRequest ProtocolError");
                    break;
                }
            }
            if (configLoadRequest.result == UnityWebRequest.Result.ProtocolError) {
                    Debug.Log("Loading streaming assets: configLoadRequest ProtocolError");
            } else {
                config = JsonConvert.DeserializeObject<GlobalConfig>(configLoadRequest.downloadHandler.text[1..]);
                Debug.Log(config.ClientId);
            }

            // Let's fill the empty fields, if any
            if (config.Noctua.BaseUrl == null || config.Noctua.BaseUrl == "")
            {
                config.Noctua.BaseUrl = NoctuaConfig.DefaultBaseUrl;
            }

            if (config.Noctua.TrackerUrl == null || config.Noctua.TrackerUrl == "")
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


            Auth = new NoctuaAuthService(
                new NoctuaAuthService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId
                }
            );

            IAP = new NoctuaIAPService(
                new NoctuaIAPService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId
                }
            );

            // Subscribe to the GoogleBillingInstance's OnPurchaseDone event
            GoogleBillingInstance.OnPurchaseDone += HandlePurchaseDone;
        }

        public static void Init()
        {
            Debug.Log("Noctua.Init()");
            Plugin?.Init();

            GoogleBillingInstance?.Init();
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

        public static void PurchaseItem(
            string productId
        )
        {
            Debug.Log("Noctua.PurchaseItem");
            GoogleBillingInstance?.PurchaseItem(productId);
        }

        private static void HandlePurchaseDone(string result)
        {
            Debug.Log("Noctua.HandlePurchaseDone");
            // Forward the event to subscribers of Noctua's OnPurchaseDone event
            OnPurchaseDone?.Invoke(result);
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