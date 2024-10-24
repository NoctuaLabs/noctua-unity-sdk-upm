using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using System.Threading.Tasks;
using com.noctuagames.sdk.Events;
using com.noctuagames.sdk.UI;
using UnityEngine.Scripting;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

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
        public const string DefaultTrackerUrl = "https://sdk-tracker.noctuaprojects.com/api/v1";
        public const string DefaultBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1";
        public const string DefaultSandboxBaseUrl = "https://sandbox-sdk-api-v2.noctuaprojects.com/api/v1";

        [JsonProperty("trackerUrl")] public string TrackerUrl = DefaultTrackerUrl;

        [JsonProperty("baseUrl")] public string BaseUrl = DefaultBaseUrl;

        [JsonProperty("paymentBaseUrl")] public string PaymentBaseUrl;

        [JsonProperty("announcementBaseUrl")] public string AnnouncementBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/announcements";

        [JsonProperty("rewardBaseUrl")] public string RewardBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/rewards";

        [JsonProperty("customerServiceBaseUrl")] public string CustomerServiceBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/cs";

        [JsonProperty("isSandbox")] public bool IsSandbox;
        [JsonProperty("region")]  public string Region;
        [JsonProperty("flags")]  public string Flags;
    }
    
    [Preserve]
    public class FirebaseConfig
    {
        [JsonProperty("eventMap")] public Dictionary<string, string> EventMap = new();
    }
    
    [Preserve]
    public class FacebookConfig
    {
        [JsonProperty("appId"), JsonRequired] public string AppId;
        
        [JsonProperty("clientToken"), JsonRequired] public string ClientToken;
        
        [JsonProperty("eventMap")] public Dictionary<string, string> EventMap = new();
    }

    [Preserve]
    public class CoPublisherConfig
    {
        [JsonProperty("companyName"), JsonRequired] public string CompanyName;
        [JsonProperty("companyWebsiteUrl"), JsonRequired] public string CompanyWebsiteUrl;
        [JsonProperty("companyTermUrl"), JsonRequired] public string CompanyTermUrl;
        [JsonProperty("companyPrivacyUrl"), JsonRequired] public string CompanyPrivacyUrl;
    }

    [Preserve]
    public class GlobalConfig
    {
        [JsonProperty("clientId"), JsonRequired] public string ClientId;

        [JsonProperty("adjust")] public AdjustConfig Adjust;

        [JsonProperty("firebase")] public FirebaseConfig Firebase;
        
        [JsonProperty("facebook")] public FacebookConfig Facebook;

        [JsonProperty("noctua")] public NoctuaConfig Noctua;
        
        [JsonProperty("copublisher")] public CoPublisherConfig CoPublisher;
    }

    public class Noctua
    {
        private static readonly Lazy<Noctua> Instance = new(() => new Noctua());
        public static NoctuaEventService Event => Instance.Value._event;
        public static NoctuaAuthentication Auth => Instance.Value._auth;
        public static NoctuaIAPService IAP => Instance.Value._iap;
        public static NoctuaPlatform Platform => Instance.Value._platform;

        public event Action<string> OnPurchaseDone;

        private readonly EventSender _eventSender;
        private readonly NoctuaEventService _event;
        private readonly NoctuaAuthentication _auth;
        private readonly NoctuaIAPService _iap;
        private readonly NoctuaGameService _game;
        private readonly NoctuaPlatform _platform;

        #if UNITY_ANDROID && !UNITY_EDITOR
        private readonly GoogleBilling _googleBilling;
        #endif
        private readonly INativePlugin _nativePlugin;
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

            Debug.Log($"Noctua config: \n{config.PrintFields()}");
            
            _eventSender = new EventSender(
                new EventSenderConfig
                {
                    BaseUrl = config.Noctua.TrackerUrl,
                    ClientId = config.ClientId,
                    BundleId = Application.identifier
                },
                new NoctuaLocale()
            );
            
            _nativePlugin = GetNativePlugin();
            _event = new NoctuaEventService(_nativePlugin, _eventSender);

            var panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("NoctuaTheme");
            // Calculate the scale factor based on the screen width and height short side
            // Apply the scale to the panel settings to keep the UI consistent.
            panelSettings.scale = 1.0f * Mathf.Min(Screen.width, Screen.height) / panelSettings.referenceResolution.y;

            
            var noctuaUIGameObject = new GameObject("NoctuaUI");
            Object.DontDestroyOnLoad(noctuaUIGameObject);
            
            var uiFactory = new UIFactory(noctuaUIGameObject, panelSettings);
            
            var authService = new NoctuaAuthenticationService(
                baseUrl: config.Noctua.BaseUrl, 
                clientId: config.ClientId, 
                nativeAccountStore: _nativePlugin,
                bundleId: Application.identifier,
                eventSender: _eventSender
            );

            _auth = new NoctuaAuthentication(authService, uiFactory, config, _eventSender);
            
            var accessTokenProvider = new AccessTokenProvider(authService);

            _iap = new NoctuaIAPService(
                new NoctuaIAPService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId,
                    WebPaymentBaseUrl = config.Noctua.PaymentBaseUrl
                },
                accessTokenProvider,
                uiFactory,
                _nativePlugin
            );

            _game = new NoctuaGameService(
                new NoctuaGameService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId
                }
            );

            _platform = new NoctuaPlatform(config.Noctua, accessTokenProvider, uiFactory);
        }

        public static async UniTask InitAsync()
        {
            Debug.Log("Noctua Init()");

            if (Instance.Value._initialized)
            {
                Debug.Log("Noctua.Init() has been called");

                return;
            }
            

            // Init game
            var initResponse = await Instance.Value._game.InitGameAsync();

            Debug.Log("Noctua.Init() -> nativePlugin?.Init()");
            Instance.Value._nativePlugin?.Init(initResponse.ActiveBundleIds);

#if UNITY_ANDROID && !UNITY_EDITOR
            Instance.Value._googleBilling?.Init();
#endif

            if (string.IsNullOrEmpty(initResponse.Country))
            {
                try
                {
                    initResponse.Country = await Instance.Value._game.GetCountryIDFromCloudflareTraceAsync();
                    Debug.Log("Noctua.Init() -> Using country from cloudflare: " + initResponse.Country);
                }
                catch (Exception e)
                {
                    Debug.Log("Noctua.Init() -> Using country from default value: " + initResponse.Country);
                    initResponse.Country = "IDR";
                }
            } else {
                Debug.Log("Noctua.Init() -> Using country from geoIP: " + initResponse.Country);
            }

            // Set locale values
            if (!string.IsNullOrEmpty(initResponse.Country))
            {
                Instance.Value._platform.Locale.SetCountry(initResponse.Country);
            }

            // Try to get active currency
            if (!string.IsNullOrEmpty(initResponse.ActiveProductId))
            {
                try
                {
                    var activeCurrency = await Instance.Value._iap.GetActiveCurrencyAsync(initResponse.ActiveProductId);
                    if (!string.IsNullOrEmpty(activeCurrency))
                    {
                        Debug.Log("Found active currency: " + activeCurrency);
                        Instance.Value._platform.Locale.SetCurrency(activeCurrency);
                    }
                }
                catch (Exception e)
                {
                    Instance.Value._platform.Locale.SetCurrency("IDR");
                }
            }

            // Remote config
            Instance.Value._iap.SetEnabledPaymentTypes(initResponse.RemoteConfigs.EnabledPaymentTypes);
            
            Instance.Value._eventSender.Send("init");
            
            if (Noctua.IsFirstOpen())
            {
                Instance.Value._eventSender.Send("first_open");
            }

            Debug.Log("Noctua.Init() set _initialized to true");
            Instance.Value._initialized = true;
        }

        private static bool IsFirstOpen()
        {
            var isFirstOpen = PlayerPrefs.GetInt("NoctuaFirstOpen", 1) == 1;
            
            if (isFirstOpen)
            {
                PlayerPrefs.SetInt("NoctuaFirstOpen", 0);
            }
            
            return isFirstOpen;
        }

        public static void OnApplicationPause(bool pause)
        {
            Instance.Value._nativePlugin?.OnApplicationPause(pause);
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

        public static void ShowDatePicker(int year, int month, int day, int id)
        {
            Instance.Value._nativePlugin?.ShowDatePicker(year, month, day, id);
        }

        public static void OpenDatePicker(int year, int month, int day, int pickerId = 1, Action<DateTime> onChange = null, Action<DateTime> onClose = null)
        {
            MobileDateTimePicker.CreateDate(pickerId, year, month, day, onChange, onClose);
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
            Debug.Log("Plugin is default");
            return new DefaultNativePlugin();
#endif
        }

    }
}
