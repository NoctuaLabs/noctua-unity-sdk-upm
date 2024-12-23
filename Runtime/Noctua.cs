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
using Serilog;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class AdjustConfig
    {
	[JsonProperty("android"), JsonRequired] public AdjustAndroidConfig Android;

	[JsonProperty("ios"), JsonRequired] public AdjustIosConfig Ios;
    }

    [Preserve]
    public class AdjustAndroidConfig
    {
        [JsonProperty("appToken"), JsonRequired] public string AppToken;

        [JsonProperty("environment")] public string Environment = "sandbox";

        [JsonProperty("eventMap")] public Dictionary<string, string> EventMap = new();
    }

    [Preserve]
    public class AdjustIosConfig
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
        public const string DefaultPaymentBaseUrl = "https://dev.noctua.gg/noctua-gold-payment-webview";
        public const string DefaultAnnouncementBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/announcements";
        public const string DefaultRewardBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/rewards";
        public const string DefaultCustomerServiceBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/cs";

        [JsonProperty("trackerUrl")] public string TrackerUrl = DefaultTrackerUrl;

        [JsonProperty("baseUrl")] public string BaseUrl = DefaultBaseUrl;

        [JsonProperty("paymentBaseUrl")] public string PaymentBaseUrl = DefaultPaymentBaseUrl;

        [JsonProperty("announcementBaseUrl")] public string AnnouncementBaseUrl = DefaultAnnouncementBaseUrl;

        [JsonProperty("rewardBaseUrl")] public string RewardBaseUrl = DefaultRewardBaseUrl;

        [JsonProperty("customerServiceBaseUrl")] public string CustomerServiceBaseUrl = DefaultCustomerServiceBaseUrl;
        [JsonProperty("sentryDsnUrl")] public string SentryDsnUrl = "";

        [JsonProperty("trackerBatchSize")] public uint TrackerBatchSize = 20;
        [JsonProperty("trackerBatchPeriodMs")] public uint TrackerBatchPeriodMs = 300_000;
        [JsonProperty("sessionHeartbeatPeriodMs")] public uint SessionHeartbeatPeriodMs = 60_000;
        [JsonProperty("sessionTimeoutMs")] public uint SessionTimeoutMs = 900_000;

        [JsonProperty("isSandbox")] public bool IsSandbox;
        [JsonProperty("region")]  public string Region;
        [JsonProperty("flags")]  public string Flags;
    }
    
    [Preserve]
    public class FacebookConfig
    {
	[JsonProperty("android"), JsonRequired] public FacebookAndroidConfig Android;
	[JsonProperty("ios"), JsonRequired] public FacebookIosConfig Ios;
    }

    [Preserve]
    public class FacebookAndroidConfig
    {
        [JsonProperty("appId"), JsonRequired] public string AppId;

        [JsonProperty("clientToken"), JsonRequired] public string ClientToken;
    }

    [Preserve]
    public class FacebookIosConfig
    {
        [JsonProperty("appId"), JsonRequired] public string AppId;
        
        [JsonProperty("clientToken"), JsonRequired] public string ClientToken;
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

        private readonly ILogger _log = new NoctuaLogger();
        private readonly EventSender _eventSender;
        private readonly SessionTracker _sessionTracker;
        private readonly NoctuaEventService _event;
        private readonly NoctuaAuthentication _auth;
        private readonly NoctuaIAPService _iap;
        private readonly NoctuaGameService _game;
        private readonly NoctuaPlatform _platform;
        private readonly UIFactory _uiFactory;

        private readonly INativePlugin _nativePlugin;
        private bool _initialized = false;

        private Noctua()
        {
            var configPath = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            Debug.Log($"Loading config from: {configPath}");
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
            
            NoctuaLogger.Init(config);

            var locale = new NoctuaLocale(config.Noctua.Region);

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

            _log.Debug($"Noctua config: \n{config.PrintFields()}");
            
            _eventSender = new EventSender(
                new EventSenderConfig
                {
                    BaseUrl = config.Noctua.TrackerUrl,
                    ClientId = config.ClientId,
                    BundleId = Application.identifier,
                    BatchSize = config.Noctua.TrackerBatchSize,
                    BatchPeriodMs = config.Noctua.TrackerBatchPeriodMs
                },
                locale
            );
            
            _sessionTracker = new SessionTracker(
                new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = config.Noctua.SessionHeartbeatPeriodMs,
                    SessionTimeoutMs = config.Noctua.SessionTimeoutMs
                },
                _eventSender
            );
            
            _nativePlugin = GetNativePlugin();
            _log.Debug($"_nativePlugin type: {_nativePlugin?.GetType().FullName}");
            _nativePlugin?.Init(new List<string>());
            
            _event = new NoctuaEventService(_nativePlugin, _eventSender);
            _event.SetProperties(isSandbox: config.Noctua.IsSandbox);
            _eventSender.SetProperties(isSandbox: config.Noctua.IsSandbox);
            

            var panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("NoctuaTheme");
            // Calculate the scale factor based on the screen width and height short side
            // Apply the scale to the panel settings to keep the UI consistent.
            //panelSettings.scale = 1.0f * Mathf.Min(Screen.width, Screen.height) / panelSettings.referenceResolution.y;
            
            var noctuaUIGameObject = new GameObject("NoctuaUI");
            noctuaUIGameObject.AddComponent<PauseBehaviour>();
            noctuaUIGameObject.AddComponent<GlobalExceptionLogger>();
            Object.DontDestroyOnLoad(noctuaUIGameObject);
            
            SceneManager.sceneLoaded += (_, _) => EventSystem.SetUITookitEventSystemOverride(EventSystem.current);
            
            var sessionTrackerBehaviour = noctuaUIGameObject.AddComponent<SessionTrackerBehaviour>();
            
            sessionTrackerBehaviour.SessionTracker = _sessionTracker;
            
            _uiFactory = new UIFactory(noctuaUIGameObject, panelSettings, locale);
            
            var authService = new NoctuaAuthenticationService(
                baseUrl: config.Noctua.BaseUrl, 
                clientId: config.ClientId, 
                nativeAccountStore: _nativePlugin,
                locale: locale,
                bundleId: Application.identifier,
                eventSender: _eventSender
            );

            
            var accessTokenProvider = new AccessTokenProvider(authService);

            _iap = new NoctuaIAPService(
                new NoctuaIAPService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId,
                    WebPaymentBaseUrl = config.Noctua.PaymentBaseUrl
                },
                accessTokenProvider,
                _uiFactory,
                _nativePlugin,
                _eventSender
            );

            _auth = new NoctuaAuthentication(authService, _iap, _uiFactory, config, _eventSender, locale);

            _game = new NoctuaGameService(
                new NoctuaGameService.Config
                {
                    BaseUrl = config.Noctua.BaseUrl,
                    ClientId = config.ClientId
                }
            );

            _platform = new NoctuaPlatform(config.Noctua, accessTokenProvider, _uiFactory, _eventSender);
            
            _log.Info("Noctua instance created");
        }

        private void Enable()
        {
            _iap.Enable();
            _auth.Enable();
            _initialized = true;
        }

        public static async UniTask InitAsync()
        {
            if (Instance.Value._initialized)
            {
                Instance.Value._log.Info("InitAsync() called but already initialized");

                return;
            }
            
            var log = Instance.Value._log;

            // Init game, retries on intermittent network failure
            InitGameResponse initResponse = null;
            
            try
            {
                initResponse = await Utility.RetryAsyncTask(Instance.Value._game.InitGameAsync);
            }
            catch (Exception e)
            {
                log.Exception(e);

                await Noctua.Instance.Value._uiFactory.ShowStartGameErrorDialog(e.Message);
            }
            
            var iapReadyTimeout = DateTime.UtcNow.AddSeconds(5);
            
            log.Debug($"IAP ready: {Instance.Value._iap.IsReady}");

            while (!Instance.Value._iap.IsReady && DateTime.UtcNow < iapReadyTimeout)
            {
                Instance.Value._iap.Init();

                var completedTask = await UniTask.WhenAny(
                    UniTask.WaitUntil(() => Noctua.Instance.Value._iap.IsReady),
                    UniTask.Delay(1000)
                );
                
                log.Debug($"IAP ready: {Instance.Value._iap.IsReady}");

                if (completedTask == 0)
                {
                    break;
                }
            }
            
            if (!Instance.Value._iap.IsReady)
            {
                log.Error("IAP is not ready after timeout");
            }
            
            if (string.IsNullOrEmpty(initResponse.Country))
            {
                try
                {
                    initResponse.Country = await Instance.Value._game.GetCountryIDFromCloudflareTraceAsync();
                    log.Info("Using country from cloudflare: " + initResponse.Country);
                }
                catch (Exception)
                {
                    log.Info("Using country from default value: " + initResponse.Country);
                    initResponse.Country = "ID";
                }
            } else {
                log.Info("Using country from geoIP: " + initResponse.Country);
            }

            if (initResponse != null)
            {
                Instance.Value._event.SetProperties(initResponse.Country, initResponse.IpAddress);
                Instance.Value._eventSender.SetProperties(ipAddress: initResponse.IpAddress);
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
                        log.Info("Found active currency: " + activeCurrency);
                        if (initResponse.SupportedCurrencies != null && 
                        initResponse.SupportedCurrencies.Contains(activeCurrency))
                        {
                            log.Info("Active currency is supported: " + activeCurrency);
                            Instance.Value._platform.Locale.SetCurrency(activeCurrency);
                        }
                        else
                        {
                            log.Warning("Active currency is not supported. Fallback to USD.");
                            Instance.Value._platform.Locale.SetCurrency("USD");
                        }
                    } else {
                        log.Warning("Active currency is not found. Try to use country to currency map.");
                        if (initResponse.CountryToCurrencyMap != null &&
                        initResponse.CountryToCurrencyMap.ContainsKey(initResponse.Country))
                        {
                            var currencyFromMap = initResponse.CountryToCurrencyMap[initResponse.Country];
                            log.Info("Using currency from country map: " + currencyFromMap);
                            Instance.Value._platform.Locale.SetCurrency(currencyFromMap);
                        }
                        else
                        {
                            log.Warning("Currency not found in country map. Fallback to USD.");
                            Instance.Value._platform.Locale.SetCurrency("USD");
                        }
                    }
                }
                catch (Exception)
                {
                    log.Warning("Failed to get active currency. Try to use country to currency map.");
                    if (initResponse.CountryToCurrencyMap != null &&
                    initResponse.CountryToCurrencyMap.ContainsKey(initResponse.Country))
                    {
                        if (initResponse.CountryToCurrencyMap.TryGetValue(
                            initResponse.Country, out var currencyFromMap
                        ))
                        {
                            log.Info("Using currency from country map: " + currencyFromMap);
                            Instance.Value._platform.Locale.SetCurrency(currencyFromMap);
                        }
                    }
                    else
                    {
                        log.Warning("Currency not found in country map. Fallback to USD.");
                        Instance.Value._platform.Locale.SetCurrency("USD");
                    }
                }
            }

            var enabledPaymentTypes = initResponse.RemoteConfigs.EnabledPaymentTypes;

            if (!Noctua.Instance.Value._iap.IsReady)
            {
                enabledPaymentTypes.Remove(PaymentType.appstore);
                enabledPaymentTypes.Remove(PaymentType.playstore);
            }


            // Remove irrelevant payment by runtime platform
#if !UNITY_ANDROID
            enabledPaymentTypes.Remove(PaymentType.playstore);
#endif

#if !UNITY_IOS
            enabledPaymentTypes.Remove(PaymentType.appstore);
#endif

#if UNITY_EDITOR
            // UNITY_ANDROID macro is not accurate in In-Editor
            enabledPaymentTypes.Remove(PaymentType.appstore);
            enabledPaymentTypes.Remove(PaymentType.playstore);
#endif
            
            Instance.Value._iap.SetEnabledPaymentTypes(enabledPaymentTypes);

            Instance.Value._eventSender.Send("init");
            
            if (Noctua.IsFirstOpen())
            {
                Instance.Value._eventSender.Send("sdk_first_open");
            }

            log.Info("Noctua.InitAsync() completed");

            Instance.Value.Enable();

            // Trigger retry pending purchase after all module get enabled.
            Instance.Value._iap.RetryPendingPurchasesAsync();
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
                return new AndroidPlugin();
#elif UNITY_IOS && !UNITY_EDITOR
                return new IosPlugin();
#else
            return new DefaultNativePlugin();
#endif
        }

        private class PauseBehaviour : MonoBehaviour
        {
            private void OnApplicationPause(bool pause)
            {
                Noctua.Instance.Value._nativePlugin?.OnApplicationPause(pause);
            }
        }
    }
    
}
