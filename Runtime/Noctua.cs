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
using System.Threading;
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
        public const string DefaultAnnouncementBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/announcements";
        public const string DefaultRewardBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/rewards";
        public const string DefaultCustomerServiceBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/cs";
        public const string DefaultSocialMediaBaseUrl = "https://sdk-api-v2.noctuaprojects.com/api/v1/games/social-media";

        [JsonProperty("trackerUrl")] public string TrackerUrl = DefaultTrackerUrl;

        [JsonProperty("baseUrl")] public string BaseUrl = DefaultBaseUrl;

        [JsonProperty("announcementBaseUrl")] public string AnnouncementBaseUrl = DefaultAnnouncementBaseUrl;

        [JsonProperty("rewardBaseUrl")] public string RewardBaseUrl = DefaultRewardBaseUrl;

        [JsonProperty("socialMediaBaseUrl")] public string SocialMediaBaseUrl = DefaultSocialMediaBaseUrl;

        [JsonProperty("customerServiceBaseUrl")] public string CustomerServiceBaseUrl = DefaultCustomerServiceBaseUrl;
        [JsonProperty("sentryDsnUrl")] public string SentryDsnUrl = "";

        [JsonProperty("trackerBatchSize")] public uint TrackerBatchSize = 20;
        [JsonProperty("trackerBatchPeriodMs")] public uint TrackerBatchPeriodMs = 300_000;
        [JsonProperty("sessionHeartbeatPeriodMs")] public uint SessionHeartbeatPeriodMs = 60_000;
        [JsonProperty("sessionTimeoutMs")] public uint SessionTimeoutMs = 900_000;

        [JsonProperty("sandboxEnabled")] public bool IsSandbox;
        [JsonProperty("region")]  public string Region;

        // Client side feature flags that will not be overrided by server config
        // For feature flags that will be overrided by server config, see NoctuaGameService.cs -> RemoteConfigs
        [JsonProperty("welcomeToastDisabled")] public bool welcomeToastDisabled  = false;
        [JsonProperty("iaaEnabled")] public bool isIAAEnabled  = false;
        [JsonProperty("iapDisabled")] public bool isIAPDisabled  = false;
        [JsonProperty("offlineFirstEnabled")] public bool IsOfflineFirst = false;

        // Deprecated because of inconsistent naming
        // [JsonProperty("isOfflineFirst")] public bool IsOfflineFirst = false;
        // [JsonProperty("isIAAEnabled")] public bool isIAAEnabled  = false;
        [JsonProperty("remoteFeatureFlags")]
        public Dictionary<string, bool> RemoteFeatureFlags;
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

        [JsonProperty("iaa")] public IAA IAA;
    }

    public class Noctua
    {

        private static readonly Lazy<Noctua> Instance = new(() => new Noctua());
        public static NoctuaEventService Event => Instance.Value._event;
        public static NoctuaAuthentication Auth => Instance.Value._auth;
        public static NoctuaIAPService IAP => Instance.Value._iap;
        public static NoctuaPlatform Platform => Instance.Value._platform;
        public static MediationManager IAA => Instance.Value._iaa;
        public static GlobalConfig Config => Instance.Value._config;

        public event Action<bool> OnInternetReachable;
        private readonly ILogger _log = new NoctuaLogger();
        private readonly EventSender _eventSender;
        private readonly SessionTracker _sessionTracker;
        private readonly NoctuaEventService _event;
        private readonly NoctuaAuthentication _auth;
        private readonly NoctuaIAPService _iap;
        private readonly NoctuaGameService _game;
        private readonly NoctuaPlatform _platform;
        private readonly UIFactory _uiFactory;
        private readonly MediationManager _iaa = new MediationManager();
        private GlobalConfig _config;

        private readonly INativePlugin _nativePlugin;
        // This is the flag from noctuagg.json config.
        // Not all game has this feature enabled.
        private bool _isOfflineFirst = false;
        // Will be true if offline first is enabled AND
        // there is network issue on init attempt
        private static bool _offlineMode = false;
        private static bool _initialized = false;
        private bool _isNativePluginInitialized = false;

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

            try
            {
                _config = JsonConvert.DeserializeObject<GlobalConfig>(jsonConfig);
            }
            catch (Exception e)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Failed to parse config: " + e.Message);
            }
            
            if (_config == null)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Failed to parse config: config is null");
            }
            NoctuaLogger.Init(_config);

            var locale = new NoctuaLocale(_config.Noctua.Region);

            _config.Noctua ??= new NoctuaConfig();
            _config.Adjust ??= new AdjustConfig();

            // Let's fill the empty fields, if any
            if (string.IsNullOrEmpty(_config.Noctua.BaseUrl))
            {
                _config.Noctua.BaseUrl = NoctuaConfig.DefaultBaseUrl;
            }

            if (string.IsNullOrEmpty(_config.Noctua.TrackerUrl))
            {
                _config.Noctua.TrackerUrl = NoctuaConfig.DefaultTrackerUrl;
            }

            if (_config.Noctua.IsSandbox)
            {
                _config.Noctua.BaseUrl = NoctuaConfig.DefaultSandboxBaseUrl;
            }

            _eventSender = new EventSender(
                new EventSenderConfig
                {
                    BaseUrl = _config.Noctua.TrackerUrl,
                    ClientId = _config.ClientId,
                    BundleId = Application.identifier,
                    BatchSize = _config.Noctua.TrackerBatchSize,
                    BatchPeriodMs = _config.Noctua.TrackerBatchPeriodMs
                },
                locale
            );
            
            _sessionTracker = new SessionTracker(
                new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = _config.Noctua.SessionHeartbeatPeriodMs,
                    SessionTimeoutMs = _config.Noctua.SessionTimeoutMs
                },
                _eventSender
            );
            
            _nativePlugin = GetNativePlugin();
            _log.Debug($"_nativePlugin type: {_nativePlugin?.GetType().FullName}");

            // Initialize Analytics first when IAA (In-App Advertising) is not enabled.
            // Reason:
            // - Both AppLovin MAX and AdMob handle user consent for GDPR, CCPA, and other privacy regulations.
            // - Analytics SDKs (such as Adjust, Firebase, Facebook) collect user data, so they must respect user privacy choices based on user consent.
            // Note:
            // This code has a continuation
            // See the line that has this comment:
            // - Initialize IAA (In-App Advertising) SDK and prepare IAA to be ready for showing ads to the user.
            // Do not move or reorder this code since it follows a specific initialization flow.
            if(!_config.Noctua.isIAAEnabled)
            {              
                _log.Info("Initialize nativePlugin while IAA is not enabled");
                InitializeNativePlugin();
            }
            else
            {
                if(_config.IAA == null)
                {
                    _log.Error("IAA local config is null, please check your config file");
                    return;
                }

                #if UNITY_ADMOB || UNITY_APPLOVIN
                _iaa.Initialize(_config.IAA, () => {
                    _log.Info("IAA SDK initialized from Local Config");

                    InitializeNativePlugin();
                });
                #else
                InitializeNativePlugin();
                _log.Info("Initialize nativePlugin while IAA is not enabled and UNITY_ADMOB or UNITY_APPLOVIN is not defined");
                #endif
            }

            _event = new NoctuaEventService(_nativePlugin, _eventSender);
            _event.SetProperties(isSandbox: _config.Noctua.IsSandbox);
            _eventSender.SetProperties(isSandbox: _config.Noctua.IsSandbox);
            _isOfflineFirst = _config.Noctua.IsOfflineFirst;
            

            var panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("NoctuaTheme");            
            
            var noctuaUIGameObject = new GameObject("NoctuaUI");
            noctuaUIGameObject.AddComponent<PauseBehaviour>();
            noctuaUIGameObject.AddComponent<GlobalExceptionLogger>();
            var screenRotationMonitor = noctuaUIGameObject.AddComponent<ScreenRotationMonitor>();
            screenRotationMonitor.PanelSettings = panelSettings;
            Object.DontDestroyOnLoad(noctuaUIGameObject);
            
            SceneManager.sceneLoaded += (_, _) => EventSystem.SetUITookitEventSystemOverride(EventSystem.current);
            
            var sessionTrackerBehaviour = noctuaUIGameObject.AddComponent<SessionTrackerBehaviour>();
            
            sessionTrackerBehaviour.SessionTracker = _sessionTracker;
            
            _uiFactory = new UIFactory(noctuaUIGameObject, panelSettings, locale);
            
            var authService = new NoctuaAuthenticationService(
                baseUrl: _config.Noctua.BaseUrl, 
                clientId: _config.ClientId, 
                nativeAccountStore: _nativePlugin,
                locale: locale,
                bundleId: Application.identifier,
                eventSender: _eventSender
            );

            
            var accessTokenProvider = new AccessTokenProvider(authService);

            _iap = new NoctuaIAPService(
                new NoctuaIAPService.Config
                {
                    BaseUrl = _config.Noctua.BaseUrl,
                    ClientId = _config.ClientId,
                    isIAPDisabled = _config.Noctua.isIAPDisabled,
                },
                accessTokenProvider,
                _uiFactory,
                _nativePlugin,
                _eventSender
            );

            _auth = new NoctuaAuthentication(authService, _iap, _uiFactory, _config, _eventSender, locale);

            _game = new NoctuaGameService(
                new NoctuaGameService.Config
                {
                    BaseUrl = _config.Noctua.BaseUrl,
                    ClientId = _config.ClientId,
                    IsOfflineFirst = _config.Noctua.IsOfflineFirst,
                }
            );

            _platform = new NoctuaPlatform(_config.Noctua, accessTokenProvider, _uiFactory, _eventSender);
            
            _log.Info("Noctua instance created");
        }

        private void InitializeNativePlugin()
        {
            if (_isNativePluginInitialized) {
                _log.Debug("nativePlugin is already initialized");
                return;
            }

            _nativePlugin?.Init(new List<string>());
            _isNativePluginInitialized = true;
            _log.Debug("nativePlugin is initialized");
        }

        private void Enable()
        {
            _iap.Enable();
            _auth.Enable();
            _initialized = true;
        }

        public static bool IsOfflineMode()
        {
            return _offlineMode;
        }

        public static bool IsOfflineFirst()
        {
            return Instance.Value._isOfflineFirst;
        }

        public static bool IsInitialized()
        {
            return _initialized;
        }

        public static void OnOnline()
        {
            if(AdjustOfflineModeDisabled())
            {
                return;
            }
            
            if (Instance.Value._nativePlugin != null)
            {
                Instance.Value._nativePlugin.OnOnline();
            }
        }

        public static void OnOffline()
        {
            if(AdjustOfflineModeDisabled())
            {
                return;
            }

            if (Instance.Value._nativePlugin != null)
            {
                Instance.Value._nativePlugin.OnOffline();
            }
        }

        public static bool AdjustOfflineModeDisabled()
        {
            if (Instance.Value._config.Noctua.RemoteFeatureFlags.TryGetValue("adjustOfflineModeDisabled", out var value) && value is bool flag && flag == true)
            {
                Instance.Value._log.Debug("Adjust offline mode is disabled");
                return true;
            }

            return false;
        }

        public static async UniTask<bool> IsOfflineAsync()
        {
            var log = Instance.Value._log;
            var prevOfflineMode = _offlineMode;

            var tcs = new UniTaskCompletionSource<bool>();

            await InternetChecker.CheckInternetConnectionAsync((isConnected) =>
            {
                tcs.TrySetResult(isConnected);
            });

            bool isConnected = await tcs.Task;
            _offlineMode = !isConnected;

            if (isConnected)
            {
                log.Debug("Internet is available.");
                if (Instance.Value._nativePlugin != null)
                {
                    if(!AdjustOfflineModeDisabled())
                    {
                        Instance.Value._nativePlugin.OnOnline();
                    }
                }
            }
            else
            {
                log.Debug("No internet connection.");
                if (Instance.Value._nativePlugin != null)
                {
                    if(!AdjustOfflineModeDisabled())
                    {
                        Instance.Value._nativePlugin.OnOffline();
                    }
                }

                if (prevOfflineMode != _offlineMode)
                {
                    log.Info("send offline event");
                    // Send offline event only if previously online.
                    Instance.Value._eventSender.Send("offline");
                }
            }
            
            return !isConnected;
        }


        public static async UniTask InitAsync()
        {
            if (_initialized)
            {
                Instance.Value._log.Info("InitAsync() called but already initialized");

                return;
            }
            
            var log = Instance.Value._log;

            // Init game, retries on intermittent network failure
            InitGameResponse initResponse = null;
            var offlineModeInitResponse = new InitGameResponse
            {
                Country = "",
                IpAddress = "0.0.0.0",
                // Enable all features. These will be revisited at the next init.
                RemoteConfigs = new RemoteConfigs
                {
                    EnabledPaymentTypes = new List<PaymentType> {
                        PaymentType.playstore,
                        PaymentType.appstore,
                        PaymentType.noctuastore,
                    },
                    RemoteFeatureFlags = new Dictionary<string, string>
                    {
                    },
                },
                OfflineMode = true,
            };
            
            try
            {
                initResponse = await Utility.RetryAsyncTask(Instance.Value._game.InitGameAsync);

                if (Instance.Value._isOfflineFirst && initResponse == null)
                {
                    initResponse = offlineModeInitResponse;
                }
            }
            catch (Exception e)
            {
                if (Instance.Value._isOfflineFirst && (
                    e.Message.Contains("Networking") ||
                    e.Message.Contains("500")
                ))
                {
                    log.Info($"{e.Message}");
                    // We are suppressing and returning a dummy offline mode
                    // response because:
                    // 1. We want the init process to be done silently
                    // 2. We want the Noctua.InitAsync to be reusable for
                    //    the next init attempt.
                    Instance.Value._log.Warning("Init: network issue on offline-first mode. Supress and continue to init silently.");
                    // Construct the response with dummy values
                    initResponse = offlineModeInitResponse;
                } else {
                    log.Exception(e);

                    await Instance.Value._uiFactory.ShowStartGameErrorDialog(e.Message);
                }
            }
            log.Debug("Initial noctua config: " + JsonConvert.SerializeObject(Noctua.Instance.Value._config?.Noctua));

            _offlineMode = initResponse.OfflineMode;
            Instance.Value._log.Info($"Offline mode: {_offlineMode}");

            if (_offlineMode)
            {
                Instance.Value._log.Info("InitAsync() offline mode is enabled.");
                Instance.Value._eventSender.Send("offline");
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
            
            // Override the client RemoteFeatureFlags if any
            foreach (var key in initResponse.RemoteConfigs.RemoteFeatureFlags.Keys)
            {
                var rawValue = initResponse.RemoteConfigs.RemoteFeatureFlags[key];

                if (bool.TryParse(rawValue, out var parsedBool))
                {
                    Noctua.Instance.Value._config.Noctua.RemoteFeatureFlags[key] = parsedBool;
                }
                else
                {
                    log.Warning($"Invalid boolean flag: {key} = {rawValue}. Expected 'true' or 'false'.");
                }
            }
            
            Noctua.Instance.Value._auth.SetFlag(Noctua.Instance.Value._config.Noctua.RemoteFeatureFlags);

            log.Debug("Final noctua config: " + JsonConvert.SerializeObject(Noctua.Instance.Value._config?.Noctua));

            if (!Noctua.Instance.Value._iap.IsReady)
            {
                enabledPaymentTypes.Remove(PaymentType.appstore);
                enabledPaymentTypes.Remove(PaymentType.playstore);
            }

            log.Info("FeatureFlags: " + Noctua.Instance.Value._config.Noctua.RemoteFeatureFlags);


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
            Instance.Value._iap.SetDistributionPlatform(initResponse.DistributionPlatform);

            Instance.Value._eventSender.Send("init");
            
            if (IsFirstOpen())
            {
                Instance.Value._eventSender.Send("sdk_first_open");
            }

            // Initialize IAA (In-App Advertising) SDK and prepare IAA to be ready for showing ads to the user.
            InitMediationSDK(log, initResponse);

            log.Info("Noctua.InitAsync() completed");

            // If the SDK is in offline mode, the initialized flag remains
            // false so PurchaseAsync() and other online-relian API could
            // detect this.
            // Some feature like Retry Pending Purchase mechanism is also disabled
            if (!_offlineMode)
            {
                Instance.Value._iap.Enable();
                Instance.Value._auth.Enable();
                _initialized = true;

                // Trigger retry pending purchase after all module get enabled.
                Instance.Value._iap.RetryPendingPurchasesAsync();
                // Query purchases against Google Play Billing
#if UNITY_ANDROID
                Instance.Value._iap.QueryPurchasesAsync();
#endif
            }
            else
            {
                // Start the realtime check for internet connection
                RealtimeCheckInternetConnectionAndRetryAuth().Forget();
            }
        }

        private static async UniTask RealtimeCheckInternetConnectionAndRetryAuth()
        {
            var log = Instance.Value._log;

            if (_offlineMode)
            {
                bool loop = true;

                log.Debug("Noctua: Offline mode is enabled, entering reconnection loop...");

                while (loop)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(5));

                    log.Debug("Checking internet connection...");

                    try
                    {
                        if (!IsInitialized())
                        {
                            await InitAsync();
                        }

                        await Instance.Value._auth.AuthenticateAsync();
                    
                        log.Debug("Authentication succeeded from offline.");

                        loop = false;
                    }
                    catch (NoctuaException noctuaEx)
                    {
                        log.Info($"Auth or init failed: {noctuaEx.Message}");
                        

                        if (noctuaEx.ErrorCode == (int)NoctuaErrorCode.Networking)
                        {
                            log.Debug("Detected network-related failure, will retry.");
                            loop = true;
                        }
                        else
                        {
                            log.Warning("Non-network exception, aborting retry loop.");
                            loop = false;
                        }
                    }
                }
                log.Debug("Reconnection loop exited.");
            }
        }

        private static void InitMediationSDK(ILogger log, InitGameResponse initResponse)
        {
             if(initResponse.RemoteConfigs.IAA != null)
            {
                #if UNITY_ADMOB || UNITY_APPLOVIN

                log.Info("initializing IAA SDK from remote config : " + initResponse.RemoteConfigs.IAA.Mediation);

                Instance.Value._iaa.Initialize(initResponse.RemoteConfigs.IAA, () => {

                    log.Info("IAA SDK initialized from remote config");

                    //Init analytics
                    #if UNITY_ANDROID
                    Instance.Value.InitializeNativePlugin();
                    log.Info("nativePlugin initialized");
                    #endif
                });
                #else
                Instance.Value.InitializeNativePlugin();
                log.Info("nativePlugin initialized because UNITY_ADMOB or UNITY_APPLOVIN is not defined");
                #endif
            }
            else
            {
                log.Info("Remote config IAA is not configured yet");
                #if UNITY_ANDROID
                Instance.Value.InitializeNativePlugin();
                log.Info("nativePlugin initialized");
                #endif
            }
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
            var log = Instance.Value._log;

            if (Instance.Value._nativePlugin != null)
            {
                try
                {
                    Instance.Value._nativePlugin?.ShowDatePicker(year, month, day, id);
                }
                catch (Exception ex)
                {
                    log.Debug("Failed to call method ShowDatePicker: " + ex.Message);
                }
            }
            else
            {
                log.Error("Native plugin is null");
            }
        }

        public static void CloseDatePicker()
        {
            var log = Instance.Value._log;

            if (Instance.Value._nativePlugin != null)
            {
                try
                {
                    Instance.Value._nativePlugin.CloseDatePicker();
                }
                catch (Exception ex)
                {
                    //this method is optional for android, so we can ignore the exception
                    log.Debug("Failed to call method closeDatePicker: " + ex.Message);
                }
            }
            else
            {
                log.Error("Native plugin is null");
            }
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
                var log = new NoctuaLogger();
                log.Info($"NoctuaPauseBehaviour: OnApplicationPause: {pause}");

                Noctua.Instance.Value._nativePlugin?.OnApplicationPause(pause);

                if (!pause) // If resumed from background, try to fetch purchases data
                {
                    Noctua.Instance.Value._iap?.QueryPurchasesAsync();
                }
            }

            private void OnApplicationResumed()
            {
                var log = new NoctuaLogger();
                log.Info("NoctuaPauseBehaviour: OnApplicationResumed");
            }
            private void OnApplicationFocusGained()
            {
                var log = new NoctuaLogger();
                log.Info("NoctuaPauseBehaviour: OnApplicationFocusGained");
            }
        }

        public static KeyValuePair<string, string>[] BackupPlayerPrefs()
        {
            KeyValuePair<string, string>[] keyValueArray = new KeyValuePair<string, string>[]{};

            var IntegerKeys = new string[] {
                "NoctuaFirstOpen",
                "NoctuaAccountContainer.UseFallback",
                "NativeGalleryPermission",
            };

            var StringKeys = new string[] {
                "NoctuaWebContent.Announcement.LastShown",
                "NoctuaAccountContainer",
                "NoctuaPendingPurchases",
                "NoctuaLocaleCountry",
                "NoctuaLocaleCurrency",
                "NoctuaLocaleUserPrefsLanguage",
                "NoctuaUnpairedOrders",
                "NoctuaPurchaseHistory",
                "NoctuaEvents",
            };

            foreach (var key in IntegerKeys)
            {
                var value = PlayerPrefs.GetInt(key, 0).ToString();
                Debug.Log($"Backing up playerPrefs {key}:{value}");
                Array.Resize(ref keyValueArray, keyValueArray.Length + 1);
                keyValueArray[keyValueArray.Length - 1] = new KeyValuePair<string, string>(
                    $"{key}:int",
                    value
                );
            }

            foreach (var key in StringKeys)
            {
                var value = PlayerPrefs.GetString(key, string.Empty);
                Debug.Log($"Backing up playerPrefs {key}:{value}");
                Array.Resize(ref keyValueArray, keyValueArray.Length + 1);
                keyValueArray[keyValueArray.Length - 1] = new KeyValuePair<string, string>(
                    $"{key}:string",
                    value
                );
            }

            return keyValueArray;
        }

        public static void RestorePlayerPrefs(KeyValuePair<string, string>[] keyValues)
        {
            foreach (var keyValue in keyValues)
            {
                var parts = keyValue.Key.Split(':');
                var key = parts[0];
                var type = parts[1];

                if (type == "int")
                {
                    if (int.TryParse(keyValue.Value, out int value))
                    {
                        Debug.Log($"Restoring playerPrefs {key}:{keyValue.Value}");
                        PlayerPrefs.SetInt(key, value);
                    }
                }
                else if (type == "string")
                {
                    Debug.Log($"Restoring playerPrefs {key}:{keyValue.Value}");
                    PlayerPrefs.SetString(key, keyValue.Value);
                }
            }

            PlayerPrefs.Save();
        }

        public static string[] GetPlayerPrefsKeys()
        {
            return new string[] {
                // Integer
                "NoctuaFirstOpen",
                "NoctuaAccountContainer.UseFallback",
                "NativeGalleryPermission",
                // String
                "NoctuaWebContent.Announcement.LastShown",
                "NoctuaAccountContainer",
                "NoctuaPendingPurchases",
                "NoctuaLocaleCountry",
                "NoctuaLocaleCurrency",
                "NoctuaLocaleUserPrefsLanguage",
                "NoctuaUnpairedOrders",
                "NoctuaPurchaseHistory",
                "NoctuaEvents",
            };
        }
    }
}
