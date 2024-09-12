using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.Scripting;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class AdjustConfig
    {
        [JsonProperty("appToken"), JsonRequired] public string AppToken;

        [JsonProperty("environment")] public string Environment = "sandbox";

        [JsonProperty("eventMap")] public Dictionary<string, string> EventMap = new();
    }

    [Preserve]
    public class NoctuaConfig
    {
        public const string DefaultTrackerUrl = "https://kafka-proxy-poc.noctuaprojects.com";
        public const string DefaultBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1";
        public const string DefaultSandboxBaseUrl = "https://sandbox-sdk-api-v2.noctuaprojects.com/api/v1";

        [JsonProperty("trackerUrl")] public string TrackerUrl = DefaultTrackerUrl;

        [JsonProperty("baseUrl")] public string BaseUrl = DefaultBaseUrl;

        [JsonProperty("isSandbox")] public bool IsSandbox;
    }

    [Preserve]
    public class GlobalConfig
    {
        [JsonProperty("clientId"), JsonRequired] public string ClientId;

        [JsonProperty("adjust")] public AdjustConfig Adjust = new();

        [JsonProperty("noctua")] public NoctuaConfig Noctua = new();
    }

    public class Noctua
    {
        private static readonly Lazy<Noctua> Instance = new(() => new Noctua());
        public static NoctuaAuthentication Auth => Instance.Value._auth;
        public static NoctuaIAPService IAP => Instance.Value._iap;
        public static NoctuaLocale Locale => Instance.Value._locale;

        public event Action<string> OnPurchaseDone;

        private readonly NoctuaAuthentication _auth;
        private readonly NoctuaIAPService _iap;
        private readonly NoctuaGameService _game;
        private readonly NoctuaLocale _locale;

        #if UNITY_ANDROID && !UNITY_EDITOR
        private readonly GoogleBilling _googleBilling;
        #endif
        private readonly INativePlugin _nativePlugin = GetNativePlugin();
        private bool _initialized = false;
        // Event to forward purchase results to the users of this class
        private Noctua()
        {
            Debug.Log("Loading streaming assets...");
            var configPath = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Debug.Log(configPath);
            string jsonConfig;

            // For Android
            #if UNITY_ANDROID || UNITY_EDITOR_WIN
            
            Debug.Log("Loading streaming assets in Android by using UnityWebRequest: " + configPath);
            
            var configLoadRequest = UnityWebRequest.Get(configPath);
            var now = DateTime.UtcNow;
            var timeout = now.AddSeconds(5);
            configLoadRequest.SendWebRequest();
            
            while (!configLoadRequest.isDone && now < timeout)
            {
                Task.Delay(10).Wait();
                now = DateTime.UtcNow;
            }
            
            if (now > timeout)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Failed to load config: Timeout");
            }
            
            if (configLoadRequest.result != UnityWebRequest.Result.Success)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Failed to load config: " + configLoadRequest.error);
            }
            
            if (configLoadRequest.downloadHandler.data.Length < 7)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Config file is too short");
            }
            
            ReadOnlySpan<byte> rawConfig = configLoadRequest.downloadHandler.data;
            
            // Check if rawConfig prefix is UTF-8 BOM
            if (Encoding.UTF8.Preamble.SequenceEqual(rawConfig[..3]))
            {
                rawConfig = rawConfig[3..];
            }
            
            try
            {
                jsonConfig = Encoding.UTF8.GetString(rawConfig);
            }
            catch (Exception e)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Failed to parse config: " + e.Message);
            }
            
            #elif UNITY_IOS || UNITY_EDITOR_OSX
            
            Debug.Log("Loading streaming assets in IOS by using System.IO.File.ReadAllText: " + configPath);

            try {
                jsonConfig = File.ReadAllText(configPath, Encoding.UTF8);
            } catch (Exception e) {
                throw new NoctuaException(NoctuaErrorCode.Application, "Failed to load config: " + e.Message);
            }

            #endif

            GlobalConfig config;

            try
            {
                config = JsonConvert.DeserializeObject<GlobalConfig>(jsonConfig);
            }
            catch (Exception e)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Failed to parse config: " + e.Message);
            }
            
            if (config == null)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Failed to parse config: config is null");
            }
            
            config.Noctua ??= new NoctuaConfig();
            config.Adjust ??= new AdjustConfig();

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

            Debug.Log($"Noctua.ClientId: {config.ClientId}");
            Debug.Log($"Noctua.BaseUrl: {config.Noctua.BaseUrl}");
            Debug.Log($"Noctua.TrackerUrl: {config.Noctua.TrackerUrl}");

            _auth = new NoctuaAuthentication(
                new NoctuaAuthentication.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId
                }
            );

            _iap = new NoctuaIAPService(
                new NoctuaIAPService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId
                }
            );

            _game = new NoctuaGameService(
                new NoctuaGameService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId
                }
            );  

            _locale = new NoctuaLocale();
        }

        public static async UniTask InitAsync()
        {
            Debug.Log("Noctua Init()");

            Debug.Log("Noctua.Init() -> Checking if instance has been called");
            if (Instance.Value._initialized)
            {
                Debug.Log("Noctua.Init() has been called");

                return;
            }
            
            Debug.Log("Noctua.Init() -> nativePlugin?.Init()");
            Instance.Value._nativePlugin?.Init();

            #if UNITY_ANDROID && !UNITY_EDITOR
            Instance.Value._googleBilling?.Init();
            #endif

            // Init game
            var initResponse = await Instance.Value._game.InitGameAsync();
            if (string.IsNullOrEmpty(initResponse.Country))
            {

                // Get country ID from cloudflare
                initResponse.Country = await Instance.Value._game.GetCountryIDFromCloudflareTraceAsync();

            }

            // Set locale values
            if (!string.IsNullOrEmpty(initResponse.Country))
            {
                Instance.Value._locale.SetCountry(initResponse.Country);
            }

            // Try to get active currency
            if (!string.IsNullOrEmpty(initResponse.ActiveProductId))
            {
                var activeCurrency = await Instance.Value._iap.GetActiveCurrencyAsync(initResponse.ActiveProductId);
                if (!string.IsNullOrEmpty(activeCurrency))
                {
                    Debug.Log("Found active currency: " + activeCurrency);
                    Instance.Value._locale.SetCurrency(activeCurrency);
                }
            }

            // Remote config
            Instance.Value._iap.SetEnabledPaymentTypes(initResponse.RemoteConfigs.EnabledPaymentTypes);

            Debug.Log("Noctua.Init() set _initialized to true");
            Instance.Value._initialized = true;

            // Retry pending purchases, if any
            Instance.Value._iap.RetryPendingPurchases();
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
