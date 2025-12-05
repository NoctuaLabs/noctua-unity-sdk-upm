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
        [JsonProperty("gameId")] public long GameID = 0;

        [JsonProperty("adjust")] public AdjustConfig Adjust;

        [JsonProperty("facebook")] public FacebookConfig Facebook;
        
        [JsonProperty("firebase")] public FirebaseConfig Firebase;

        [JsonProperty("noctua")] public NoctuaConfig Noctua;
        
        [JsonProperty("copublisher")] public CoPublisherConfig CoPublisher;

        [JsonProperty("iaa")] public IAA IAA;
    }

    public class Noctua
    {

        private static readonly Lazy<Noctua> Instance = new(() => new Noctua());

        /// <summary>Access Noctua event service.</summary>
        public static NoctuaEventService Event => Instance.Value._event;

        /// <summary>Access Noctua authentication service.</summary>
        public static NoctuaAuthentication Auth => Instance.Value._auth;

        /// <summary>Access Noctua IAP service.</summary>
        public static NoctuaIAPService IAP => Instance.Value._iap;

        /// <summary>Access platform utilities.</summary>
        public static NoctuaPlatform Platform => Instance.Value._platform;

        /// <summary>Access mediation manager (IAA).</summary>
        public static MediationManager IAA => Instance.Value._iaa;
        
        /// <summary>Access loaded global configuration.</summary>
        public static GlobalConfig Config => Instance.Value._config;

        private readonly ILogger _log = new NoctuaLogger();
        private readonly EventSender _eventSender;
        private readonly SessionTracker _sessionTracker;
        private readonly NoctuaEventService _event;
        private readonly NoctuaAuthentication _auth;
        private readonly NoctuaIAPService _iap;
        private readonly NoctuaGameService _game;
        private readonly NoctuaPlatform _platform;
        private readonly UIFactory _uiFactory;
        private readonly MediationManager _iaa;
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

        /// <summary>
        /// Optional callback invoked when Noctua initialization completes successfully.
        /// </summary>
        public static Action? OnInitSuccess;

        /// <summary>
        /// Private constructor - initializes Noctua SDK internals by reading <c>noctuagg.json</c> and preparing services.
        /// </summary>
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
                    BatchPeriodMs = _config.Noctua.TrackerBatchPeriodMs,
                    FirebaseConfig = _config.Firebase,
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

            // Initialize ui factory
            var panelSettings = Resources.Load<PanelSettings>("NoctuaPanelSettings");
            panelSettings.themeStyleSheet = Resources.Load<ThemeStyleSheet>("NoctuaTheme");            
            var noctuaUIGameObject = new GameObject("NoctuaUI");
            noctuaUIGameObject.AddComponent<PauseBehaviour>();
            noctuaUIGameObject.AddComponent<GlobalExceptionLogger>();
            var screenRotationMonitor = noctuaUIGameObject.AddComponent<ScreenRotationMonitor>();
            screenRotationMonitor.PanelSettings = panelSettings;
            Object.DontDestroyOnLoad(noctuaUIGameObject);

            SceneManager.sceneLoaded += (_, _) =>
            {
                if (EventSystem.current == null)
                {
                    var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                    Object.DontDestroyOnLoad(eventSystem);
                    _log.Warning("Created missing EventSystem automatically.");
                }

                EventSystem.SetUITookitEventSystemOverride(EventSystem.current);
            };

            var sessionTrackerBehaviour = noctuaUIGameObject.AddComponent<SessionTrackerBehaviour>();
            sessionTrackerBehaviour.SessionTracker = _sessionTracker;
            _uiFactory = new UIFactory(noctuaUIGameObject, panelSettings, locale);

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
            if (!_config.Noctua.isIAAEnabled)
            {
                _log.Info("Initialize nativePlugin while IAA is not enabled");
                InitializeNativePlugin();
            }
            else
            {
                if (_config.IAA == null)
                {
                    _log.Error("IAA local config is null, please check your config file");
                    return;
                }

                _iaa = new MediationManager(iAAResponse: _config.IAA, uiFactory: _uiFactory);

#if UNITY_ADMOB || UNITY_APPLOVIN
                _iaa.Initialize(() =>
                {
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
            _eventSender.SetProperties(isSandbox: _config.Noctua.IsSandbox, gameId: _config.GameID);
            _isOfflineFirst = _config.Noctua.IsOfflineFirst;
            
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

        /// <summary>
        /// Initialize native plugin if not already initialized.
        /// This calls <see cref="INativePlugin.Init(List{string})"/>.
        /// </summary>
        private void InitializeNativePlugin()
        {
            if (_isNativePluginInitialized)
            {
                _log.Debug("nativePlugin is already initialized");
                return;
            }

            _nativePlugin?.Init(new List<string>());
            _isNativePluginInitialized = true;
            _log.Debug("nativePlugin is initialized");
        }

        /// <summary>
        /// Enable runtime services (IAP and Auth).
        /// </summary>
        private void Enable()
        {
            _iap.Enable();
            _auth.Enable();
            _initialized = true;
        }

        /// <summary>
        /// Returns whether the SDK is running in offline mode.
        /// </summary>
        /// <returns><c>true</c> if offline mode is active; otherwise <c>false</c>.</returns>
        public static bool IsOfflineMode()
        {
            return _offlineMode;
        }

        // <summary>
        /// Returns whether the SDK was configured as "offline-first".
        /// </summary>
        /// <returns><c>true</c> if offline-first is enabled; otherwise <c>false</c>.</returns>
        public static bool IsOfflineFirst()
        {
            return Instance.Value._isOfflineFirst;
        }

        /// <summary>
        /// Returns whether the SDK has completed initialization.
        /// </summary>
        /// <returns><c>true</c> if initialized; otherwise <c>false</c>.</returns>
        public static bool IsInitialized()
        {
            return _initialized;
        }

        /// <summary>
        /// Notify native plugin that the app is online.
        /// </summary>
        public static void OnOnline()
        {
            if (AdjustOfflineModeDisabled())
            {
                return;
            }

            if (Instance.Value._nativePlugin != null)
            {
                Instance.Value._nativePlugin.OnOnline();
            }
        }

        /// <summary>
        /// Notify native plugin that the app is offline.
        /// </summary>
        public static void OnOffline()
        {
            if (AdjustOfflineModeDisabled())
            {
                return;
            }

            if (Instance.Value._nativePlugin != null)
            {
                Instance.Value._nativePlugin.OnOffline();
            }
        }

        /// <summary>
        /// Checks if Adjust offline mode handling is disabled via remote feature flags.
        /// </summary>
        /// <returns><c>true</c> if Adjust offline mode is disabled; otherwise <c>false</c>.</returns>
        public static bool AdjustOfflineModeDisabled()
        {
            if (Instance.Value._config?.Noctua?.RemoteFeatureFlags?.TryGetValue("adjustOfflineModeDisabled", out var value) == true && value is bool flag && flag == true)
            {
                Instance.Value._log.Debug("Adjust offline mode is disabled");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Asynchronously checks internet connectivity and updates offline mode.
        /// This will also call native plugin OnOnline/OnOffline when appropriate and send events.
        /// </summary>
        /// <returns><c>true</c> when offline; <c>false</c> when online.</returns>
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
                    if (!AdjustOfflineModeDisabled())
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
                    if (!AdjustOfflineModeDisabled())
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

        /// <summary>
        /// Initialize the Noctua SDK asynchronously, including IAP, auth, and mediation initialization.
        /// </summary>
        /// <param name="onSuccess">Optional callback invoked after successful initialization.</param>
        /// <param name="OnInitSuccess">Optional callback invoked after successful initialization.</param>
        public static async UniTask InitAsync(Func<UniTask>? onSuccess = null)
        {
            Instance.Value._eventSender.Send("sdk_init_start");
            if (_initialized)
            {
                Instance.Value._log.Info("InitAsync() called but already initialized");

                return;
            }

            Instance.Value._eventSender.Send("game_platform_type", new Dictionary<string, IConvertible> {
                { "platform_type", Utility.GetPlatformType() }
            });

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

            // Disabled for production to reduce event noise
            // Instance.Value._eventSender.Send("sdk_init_offline_mode_response_prepared");

            try
            {
                // This will call the backend API with automatic retry, except when offline.
                initResponse = _offlineMode ? await Instance.Value._game.InitGameAsync() : await Utility.RetryAsyncTask(Instance.Value._game.InitGameAsync);

                if (Instance.Value._isOfflineFirst && initResponse == null)
                {
                    // Disabled for production to reduce event noise
                    // Instance.Value._eventSender.Send("sdk_init_response_with_offline_mode");
                    initResponse = offlineModeInitResponse;
                }

                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_internal_success");
            }
            catch (Exception e)
            {
                if (Instance.Value._isOfflineFirst && (
                    e.Message.Contains("Networking") ||
                    e.Message.Contains("500")
                ))
                {
                    // Disabled for production to reduce event noise
                    // Instance.Value._eventSender.Send("sdk_init_network_error", new Dictionary<string, IConvertible>
                    // {
                    //     { "error_message", e.Message }
                    // });

                    log.Info($"{e.Message}");
                    // We are suppressing and returning a dummy offline mode
                    // response because:
                    // 1. We want the init process to be done silently
                    // 2. We want the Noctua.InitAsync to be reusable for
                    //    the next init attempt.
                    Instance.Value._log.Warning("Init: network issue on offline-first mode. Supress and continue to init silently.");
                    // Construct the response with dummy values
                    initResponse = offlineModeInitResponse;
                }
                else
                {
                    var errorMessage = e.Message ?? "An unexpected error occurred";

                    // Disabled for production to reduce event noise
                    // Instance.Value._eventSender.Send("sdk_init_other_error", new Dictionary<string, IConvertible>
                    // {
                    //     { "error_message", errorMessage }
                    // });

                    log.Exception(e);

                    if (Instance.Value._uiFactory != null)
                    {
                        // Disabled for production to reduce event noise
                        // Instance.Value._eventSender.Send("sdk_init_show_error_dialog", new Dictionary<string, IConvertible>
                        // {
                        //     { "error_message", errorMessage }
                        // });
                        await Instance.Value._uiFactory.ShowStartGameErrorDialog(errorMessage);
                    }
                    else
                    {
                        // Disabled for production to reduce event noise
                        // Instance.Value._eventSender.Send("sdk_init_show_error_dialog_failed", new Dictionary<string, IConvertible>
                        // {
                        //     { "error_message", errorMessage }
                        // });
                        log.Warning($"_uiFactory is null, cannot show error dialog: {errorMessage}");
                    }
                }
            }
            log.Debug("Initial noctua config: " + JsonConvert.SerializeObject(Noctua.Instance.Value._config?.Noctua));

            _offlineMode = initResponse.OfflineMode;
            Instance.Value._log.Info($"Offline mode: {_offlineMode}");

            if (_offlineMode)
            {
                Instance.Value._log.Info("InitAsync() offline mode is enabled.");
                Instance.Value._eventSender.Send("offline");
                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_offline_mode_enabled");
            }

            var iapReadyTimeout = DateTime.UtcNow.AddSeconds(5);

            log.Debug($"IAP ready: {Instance.Value._iap.IsReady}");

            while (!Instance.Value._iap.IsReady && DateTime.UtcNow < iapReadyTimeout)
            {
                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_iap_init");
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
                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_iap_init_not_ready_or_timeout");
                log.Error("IAP is not ready after timeout");
            }
            else
            {
                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_iap_init_success");
            }

            if (string.IsNullOrEmpty(initResponse.Country))
            {
                try
                {
                    initResponse.Country = await Instance.Value._game.GetCountryIDFromCloudflareTraceAsync();
                    log.Info("Using country from cloudflare: " + initResponse.Country);
                    // Disabled for production to reduce event noise
                    // Instance.Value._eventSender.Send("sdk_init_get_country_from_cloudflare_success");
                }
                catch (Exception)
                {
                    // Disabled for production to reduce event noise
                    // Instance.Value._eventSender.Send("sdk_init_get_country_from_cloudflare_failed");
                    log.Info("Using country from default value: " + initResponse.Country);
                    initResponse.Country = "XX";
                }
            }
            else
            {
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

                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_set_locale_success");
            }

            // Try to get active currency
            if (!string.IsNullOrEmpty(initResponse.ActiveProductId))
            {
                try
                {
                    var activeCurrency = await Instance.Value._iap.GetActiveCurrencyAsync(initResponse.ActiveProductId);
                    // Disabled for production to reduce event noise
                    // Instance.Value._eventSender.Send("sdk_init_get_active_currency_success");
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

                        // Disabled for production to reduce event noise
                        // Instance.Value._eventSender.Send("sdk_init_set_currency_success");
                    }
                    else
                    {
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
                        // Disabled for production to reduce event noise
                        // Instance.Value._eventSender.Send("sdk_init_set_fallback_currency_success");
                    }
                }
                catch (Exception)
                {
                    var errorMessage = "Failed to get active currency. Try to use country to currency map.";

                    // Disabled for production to reduce event noise
                    // Instance.Value._eventSender.Send("sdk_init_set_currency_failed", new Dictionary<string, IConvertible>
                    // {
                    //     { "error_message", errorMessage}
                    // });
                    log.Warning(errorMessage);
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

                    // Disabled for production to reduce event noise
                    // Instance.Value._eventSender.Send("sdk_init_set_fallback_currency_success");
                }
            }

            var enabledPaymentTypes = initResponse.RemoteConfigs.EnabledPaymentTypes;

            // Override the client RemoteFeatureFlags if any
            log.Debug("Overriding RemoteFeatureFlags...");

            var remoteFlags = initResponse?.RemoteConfigs?.RemoteFeatureFlags;

            if (remoteFlags == null)
            {
                log.Warning("RemoteFeatureFlags is null — skipping feature flag override.");
            }
            else
            {
                foreach (var key in remoteFlags.Keys)
                {
                    // Defensive checks for key/value
                    if (string.IsNullOrEmpty(key))
                    {
                        log.Warning("Empty key found in RemoteFeatureFlags. Skipping.");
                        continue;
                    }

                    var rawValue = remoteFlags[key];

                    // Normalize the value to ensure consistent parsing
                    if (bool.TryParse(rawValue?.Trim().ToLowerInvariant(), out var parsedBool))
                    {
                        Noctua.Instance.Value._config.Noctua.RemoteFeatureFlags[key] = parsedBool;
                        log.Debug($"Feature flag set: {key} = {parsedBool}");
                    }
                    else
                    {
                        log.Warning($"Invalid boolean flag: {key} = '{rawValue}'. Expected 'true' or 'false'.");
                    }
                }
            }

            Noctua.Instance.Value._auth.SetFlag(Noctua.Instance.Value._config.Noctua.RemoteFeatureFlags);

            // Disabled for production to reduce event noise
            // Instance.Value._eventSender.Send("sdk_init_set_remote_feature_flags_success");

            log.Debug("Final noctua config: " + JsonConvert.SerializeObject(Noctua.Instance.Value._config?.Noctua));

            if (!Noctua.Instance.Value._iap.IsReady)
            {
                enabledPaymentTypes.Remove(PaymentType.appstore);
                enabledPaymentTypes.Remove(PaymentType.playstore);

                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_remove_platform_payment_types");
            }

            // If there is pending redeem orders, let's deliver it.
            if (Instance.Value._iap.IsReady)
            {
                Instance.Value._iap.DeliverPendingRedeemOrders(initResponse.PendingNoctuaRedeemOrders);
            }

            log.Info("FeatureFlags: " + Noctua.Instance.Value._config.Noctua.RemoteFeatureFlags);


            // Remove irrelevant payment by runtime platform
#if !UNITY_ANDROID
            if (enabledPaymentTypes != null)
            {
                enabledPaymentTypes.Remove(PaymentType.playstore);

                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_remove_irrelevant_payment_types");
            }
#endif

#if !UNITY_IOS
            if (enabledPaymentTypes != null)
            {
                enabledPaymentTypes.Remove(PaymentType.appstore);

                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_remove_irrelevant_payment_types");
            }
#endif

#if UNITY_EDITOR
            // UNITY_ANDROID macro is not accurate in In-Editor
            if (enabledPaymentTypes != null)
            {
                enabledPaymentTypes.Remove(PaymentType.appstore);
                enabledPaymentTypes.Remove(PaymentType.playstore);

                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_remove_irrelevant_payment_types");
            }
#endif

            Instance.Value._iap.SetEnabledPaymentTypes(enabledPaymentTypes);

            // Disabled for production to reduce event noise
            // Instance.Value._eventSender.Send("sdk_init_set_enabled_payment_types");

            Instance.Value._iap.SetDistributionPlatform(initResponse.DistributionPlatform);

            // Disabled for production to reduce event noise
            // Instance.Value._eventSender.Send("sdk_init_set_distribution_platform");

            Instance.Value._eventSender.Send("init");

            if (IsFirstOpen())
            {
                Instance.Value._eventSender.Send("sdk_first_open");
            }

            // Initialize IAA (In-App Advertising) SDK and prepare IAA to be ready for showing ads to the user.
            if (initResponse.RemoteConfigs.IAA != null)
            {
                InitMediationSDK(log, initResponse);
            }

            // Disabled for production to reduce event noise
            // Instance.Value._eventSender.Send("sdk_init_mediation_init");

            log.Info("Noctua.InitAsync() completed");
            var initOnlineCompleted = false;

            // If the SDK is in offline mode, the initialized flag remains
            // false so PurchaseAsync() and other online-relian API could
            // detect this.
            // Some feature like Retry Pending Purchase mechanism is also disabled
            if (!_offlineMode)
            {
                initOnlineCompleted = true;

                Instance.Value._iap.Enable();
                Instance.Value._auth.Enable();
                _initialized = true;

                // Trigger retry pending purchase after all module get enabled.
                Instance.Value._iap.RetryPendingPurchasesAsync();
                // Query purchases against Google Play Billing
#if UNITY_ANDROID

                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_start_query_purchases");
                Instance.Value._iap.QueryPurchasesAsync();
#endif
                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_online_success");
                OnInitSuccess?.Invoke();
                if (onSuccess != null) await onSuccess.Invoke();

            }
            else
            {
                initOnlineCompleted = false;
                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_offline_success");
                // Start the realtime check for internet connection
                RunReconnectionLoopAsync().Forget();

                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_offline_mode_retry_conn");
            }

            Instance.Value._eventSender.Send("sdk_init_complete", new Dictionary<string, IConvertible>
            {
                { "offline", initOnlineCompleted ? false : true }
            });
        }

        /// <summary>
        /// Internal method to check connection and perform init & authentication when offline mode is detected.
        /// </summary>
        private static async Task CheckConnectionAndReauthAsync()
        {
            var log = Instance.Value._log;
            var isOffline = await IsOfflineAsync();

            if (isOffline)
            {
                log.Debug("Still offline, will retry.");

                throw new NoctuaException(NoctuaErrorCode.Networking, "Still offline");
            }

            if (!IsInitialized())
            {
                await InitAsync();
            }

            await Instance.Value._auth.AuthenticateAsync();

            log.Debug("Authentication succeeded from offline.");
        }

        /// <summary>
        /// If offline mode is true, periodically checks connectivity and attempts initialization and authentication.
        /// This method loops while offline until success or a non-network error occurs.
        /// </summary>
        private static async UniTask RunReconnectionLoopAsync()
        {
            var log = Instance.Value._log;

            if (_offlineMode)
            {
                bool loop = true;

                log.Debug("Noctua: Offline mode is enabled, entering reconnection loop...");

                while (loop)
                {
                    await UniTask.Delay(TimeSpan.FromSeconds(10));

                    log.Debug("Checking internet connection...");

                    try
                    {
                        await CheckConnectionAndReauthAsync();

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
        
        /// <summary>
        /// Initialize mediation SDKs based on <paramref name="initResponse"/> remote config.
        /// If remote IAA config is present, use it to initialize the mediation manager and optionally initialize native plugin afterwards.
        /// </summary>
        /// <param name="initResponse">Init game response containing remote configs.</param>
        private static void InitMediationSDK(ILogger log, InitGameResponse initResponse)
        {
            if(initResponse.RemoteConfigs.IAA != null)
            {
                #if UNITY_ADMOB || UNITY_APPLOVIN
                
                log.Info("initializing IAA SDK from remote config : " + initResponse.RemoteConfigs.IAA.Mediation);

                Instance.Value._iaa._iAAResponse = Instance.Value._config.IAA;
                log.Debug("Noctua IAA config replaced with remote config: " + JsonConvert.SerializeObject(Instance.Value._iaa._iAAResponse));

                Instance.Value._iaa.Initialize(() => {

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
        
        /// <summary>
        /// Get Firebase Installation ID asynchronously using native plugin where supported.
        /// </summary>
        /// <returns>A task that resolves to the Firebase Installation ID or empty string when not available.</returns>
        public static Task<string> GetFirebaseInstallationID() 
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<string>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseInstallationID((id) =>
                    {
                        // Normalize null to empty string
                        var safeId = id ?? string.Empty;
                        tcs.TrySetResult(safeId);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(string.Empty);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning("exception: " + ex.Message);             
                
                tcs.TrySetResult(string.Empty);

            }

            return tcs.Task;
        #else
            return Task.FromResult(string.Empty);
        #endif
        }
        
        /// <summary>
        /// Get Firebase Analytics session ID asynchronously using native plugin where supported.
        /// </summary>
        /// <returns>A task that resolves to the Firebase Analytics session ID or empty string when not available.</returns>
        public static Task<string> GetFirebaseAnalyticsSessionID()
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<string>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseAnalyticsSessionID((id) =>
                    {
                        var safeId = id ?? string.Empty;
                        tcs.TrySetResult(safeId);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(string.Empty);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning("exception: " + ex.Message);

                tcs.TrySetResult(string.Empty);
            }

            return tcs.Task;
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        /// <summary>
        /// Get a string value from Firebase Remote Config asynchronously using native plugin where supported.
        /// </summary>
        /// <param name="key">The Remote Config key to retrieve.</param>
        /// <returns>A task that resolves to the string value or empty string when not available.</returns>
        public static Task<string> GetFirebaseRemoteConfigString(string key)
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<string>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseRemoteConfigString(key, (value) =>
                    {
                        var safeValue = value ?? string.Empty;
                        tcs.TrySetResult(safeValue);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(string.Empty);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning($"GetFirebaseRemoteConfigString exception: {ex.Message}");
                tcs.TrySetResult(string.Empty);
            }

            return tcs.Task;
        #else
            return Task.FromResult(string.Empty);
        #endif
        }

        /// <summary>
        /// Get a boolean value from Firebase Remote Config asynchronously using native plugin where supported.
        /// </summary>
        /// <param name="key">The Remote Config key to retrieve.</param>
        /// <returns>A task that resolves to the boolean value or false when not available.</returns>
        public static Task<bool> GetFirebaseRemoteConfigBoolean(string key)
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<bool>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseRemoteConfigBoolean(key, (value) =>
                    {
                        tcs.TrySetResult(value);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(false);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning($"GetFirebaseRemoteConfigBoolean exception: {ex.Message}");
                tcs.TrySetResult(false);
            }

            return tcs.Task;
        #else
            return Task.FromResult(false);
        #endif
        }

        /// <summary>
        /// Get a double value from Firebase Remote Config asynchronously using native plugin where supported.
        /// </summary>
        /// <param name="key">The Remote Config key to retrieve.</param>
        /// <returns>A task that resolves to the double value or 0.0 when not available.</returns>
        public static Task<double> GetFirebaseRemoteConfigDouble(string key)
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<double>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseRemoteConfigDouble(key, (value) =>
                    {
                        tcs.TrySetResult(value);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(0.0);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning($"GetFirebaseRemoteConfigDouble exception: {ex.Message}");
                tcs.TrySetResult(0.0);
            }

            return tcs.Task;
        #else
            return Task.FromResult(0.0);
        #endif
        }

        /// <summary>
        /// Get a long value from Firebase Remote Config asynchronously using native plugin where supported.
        /// </summary>
        /// <param name="key">The Remote Config key to retrieve.</param>
        /// <returns>A task that resolves to the long value or 0L when not available.</returns>
        public static Task<long> GetFirebaseRemoteConfigLong(string key)
        {
        #if UNITY_ANDROID || UNITY_IOS
            var tcs = new TaskCompletionSource<long>();

            try
            {
                if (Instance.Value._nativePlugin != null)
                {
                    Instance.Value._nativePlugin.GetFirebaseRemoteConfigLong(key, (value) =>
                    {
                        tcs.TrySetResult(value);
                    });
                }
                else
                {
                    Instance.Value._log.Warning("Native plugin is null");
                    tcs.TrySetResult(0L);
                }
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning($"GetFirebaseRemoteConfigLong exception: {ex.Message}");
                tcs.TrySetResult(0L);
            }

            return tcs.Task;
        #else
            return Task.FromResult(0L);
        #endif
        }

        /// <summary>
        /// Returns whether this is the first open of the app (and sets the flag when it is).
        /// </summary>
        /// <returns><c>true</c> if first open; otherwise <c>false</c>.</returns>
        private static bool IsFirstOpen()
        {
            var isFirstOpen = PlayerPrefs.GetInt("NoctuaFirstOpen", 1) == 1;

            if (isFirstOpen)
            {
                PlayerPrefs.SetInt("NoctuaFirstOpen", 0);
            }

            return isFirstOpen;
        }

        /// <summary>
        /// Set a general experiment key/value pair to the experiment manager.
        /// </summary>
        /// <param name="key">Experiment key.</param>
        /// <param name="value">Experiment value.</param>
        public static void SetGeneralExperiment(string key, string value)
        {
            ExperimentManager.SetGeneralExperiment(key, value);
        }
        
        /// <summary>
        /// Get a general experiment value by key from the experiment manager.
        /// </summary>
        /// <param name="key">Experiment key.</param>
        public static string GetGeneralExperiment(string key)
        {
            return ExperimentManager.GetGeneralExperiment(key);
        }
        
        /// <summary>
        /// Set an experiment identifier to the experiment manager.
        /// </summary>
        /// <param name="experimentName">Experiment name.</param>
        public static void SetExperiment(string experimentName)
        {
            ExperimentManager.SetExperiment(experimentName);
        }

        /// <summary>
        /// Get currently active experiment identifier.
        /// </summary>
        /// <returns>Active experiment name or empty string.</returns>
        public static string GetActiveExperiment()
        {
            return ExperimentManager.GetActiveExperiment();
        }

        // <summary>
        /// Show a native date picker via the native plugin.
        /// </summary>
        /// <param name="year">Start year.</param>
        /// <param name="month">Start month (1-12).</param>
        /// <param name="day">Start day.</param>
        /// <param name="id">Picker identifier.</param>
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
        
        /// <summary>
        /// Close native date picker if the native plugin supports it.
        /// </summary>
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

        /// <summary>
        /// Open a date picker UI implemented in managed code.
        /// </summary>
        /// <param name="year">Initial year.</param>
        /// <param name="month">Initial month.</param>
        /// <param name="day">Initial day.</param>
        /// <param name="pickerId">Picker identifier.</param>
        /// <param name="onChange">Callback when date changes.</param>
        /// <param name="onClose">Callback when picker closes.</param>
        public static void OpenDatePicker(int year, int month, int day, int pickerId = 1, Action<DateTime> onChange = null, Action<DateTime> onClose = null)
        {
            MobileDateTimePicker.CreateDate(pickerId, year, month, day, onChange, onClose);
        }

        /// <summary>
        /// Get the appropriate native plugin implementation for the current platform.
        /// </summary>
        /// <returns>Platform specific <see cref="INativePlugin"/> implementation or a default plugin for editor/unsupported platforms.</returns>
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
        
        /// <summary>
        /// A small MonoBehaviour used to hook into application pause/resume events.
        /// </summary>
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

        /// <summary>
        /// Backup selected PlayerPrefs keys into a key/value array for export/backup.
        /// Keys that are integers are suffixed with ":int", strings with ":string".
        /// </summary>
        /// <returns>Array of key/value pairs representing backed up PlayerPrefs.</returns>
        public static KeyValuePair<string, string>[] BackupPlayerPrefs()
        {
            KeyValuePair<string, string>[] keyValueArray = new KeyValuePair<string, string>[] { };

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
                "NoctuaAccessToken",
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

        /// <summary>
        /// Restore PlayerPrefs from an array previously produced by <see cref="BackupPlayerPrefs"/>.
        /// </summary>
        /// <param name="keyValues">Array of key/value pairs containing PlayerPrefs data. Keys must have type suffix (":int" or ":string").</param>
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
        
        /// <summary>
        /// Returns an array of PlayerPrefs keys used by Noctua.
        /// </summary>
        /// <returns>Array of keys.</returns>
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
                "NoctuaAccessToken",
            };
        }
    }
}
