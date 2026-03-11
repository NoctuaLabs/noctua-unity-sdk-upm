using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using com.noctuagames.sdk.Events;
using com.noctuagames.sdk.UI;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace com.noctuagames.sdk
{
    public partial class Noctua
    {
        /// <summary>
        /// Private constructor - initializes Noctua SDK internals by reading <c>noctuagg.json</c> and preparing services.
        /// </summary>
        private Noctua()
        {
            var configPath = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
            _log.Debug($"Loading config from: {configPath}");
            string jsonConfig;

            // For Android
            #if UNITY_ANDROID || UNITY_EDITOR_WIN

            _log.Debug("Loading streaming assets in Android by using UnityWebRequest: " + configPath);

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

            #elif UNITY_IOS || UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX

            _log.Debug("Loading streaming assets in IOS by using System.IO.File.ReadAllText: " + configPath);

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
            HttpRequest.SetLocaleProvider(locale);

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

            _nativePlugin = GetNativePlugin();
            _log.Debug($"_nativePlugin type: {_nativePlugin?.GetType().FullName}");

            MobileDateTimePicker.SetShowDatePickerAction(ShowDatePicker);

            _eventSender = new EventSender(
                new EventSenderConfig
                {
                    BaseUrl = _config.Noctua.TrackerUrl,
                    ClientId = _config.ClientId,
                    BundleId = Application.identifier,
                    BatchSize = _config.Noctua.TrackerBatchSize,
                    BatchPeriodMs = _config.Noctua.TrackerBatchPeriodMs,
                    NativePlugin = _nativePlugin,
                    FirebaseConfig = _config.Firebase,
                    NativeFirebase = _nativePlugin,
                    NativeTracker = _nativePlugin,
                    IsOfflineModeFunc = () => _offlineMode,
                    AdjustOfflineModeDisabledFunc = AdjustOfflineModeDisabled,
                },
                locale
            );

            _sessionTracker = new SessionTracker(
                new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = _config.Noctua.SessionHeartbeatPeriodMs,
                    SessionTimeoutMs = _config.Noctua.SessionTimeoutMs
                },
                _eventSender,
                _config.Noctua.RemoteFeatureFlags
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

                _iaa = new MediationManager(adPlaceholderUI: _uiFactory, iAAResponse: _config.IAA);

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
            _iaa?.SetAdRevenueTracker(_event);
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

            var paymentUI = new PaymentUIAdapter(_uiFactory);
            var lazyAuthProvider = new LazyAuthProvider();
            var connectivityProvider = new NoctuaConnectivityProvider();

            _iap = new NoctuaIAPService(
                new NoctuaIAPService.Config
                {
                    BaseUrl = _config.Noctua.BaseUrl,
                    ClientId = _config.ClientId,
                    isIAPDisabled = _config.Noctua.isIAPDisabled,
                },
                accessTokenProvider,
                paymentUI,
                _nativePlugin,
                _eventSender,
                lazyAuthProvider,
                locale,
                connectivityProvider
            );

            _auth = new NoctuaAuthentication(authService, _iap, _uiFactory, _config, _eventSender, locale);
            lazyAuthProvider.SetAuth(_auth);

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
            _nativePluginInitTcs.TrySetResult();
            _log.Debug("nativePlugin is initialized");
        }

        /// <summary>
        /// Waits for native plugin initialization to complete, with a timeout.
        /// When IAA is not enabled, native plugin is initialized synchronously in the constructor,
        /// so this returns immediately. When IAA is enabled, waits for the IAA SDK callback.
        /// </summary>
        private async UniTask WaitForNativePluginInitAsync(int timeoutMs = 10000)
        {
            if (_isNativePluginInitialized)
            {
                return;
            }

            _log.Info($"Waiting for native plugin initialization (timeout: {timeoutMs}ms)...");

            var completedIndex = await UniTask.WhenAny(
                _nativePluginInitTcs.Task,
                UniTask.Delay(timeoutMs)
            );

            if (completedIndex == 1)
            {
                _log.Warning("Native plugin init timed out. Force-initializing.");
                InitializeNativePlugin();
            }
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

            // Initialize IAA (In-App Advertising) SDK with remote config before IAP ready loop.
            // When IAA is enabled, native plugin init is deferred to the IAA callback.
            // This must happen BEFORE GetActiveCurrencyAsync and the IAP ready loop,
            // which depend on the native plugin being initialized.
            if (initResponse.RemoteConfigs?.IAA != null)
            {
                InitMediationSDK(log, initResponse);
            }

            // Wait for native plugin to be initialized.
            // When IAA is not enabled, this returns immediately (initialized in constructor).
            // When IAA is enabled, this waits for the IAA SDK callback to fire.
            await Instance.Value.WaitForNativePluginInitAsync();

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
                log.Info("Active product id: " + initResponse.ActiveProductId);
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

                // Ensure editor payment type is available for Editor mock IAP
                if (!enabledPaymentTypes.Contains(PaymentType.editor))
                {
                    enabledPaymentTypes.Insert(0, PaymentType.editor);
                }
            }
#else
            // Remove editor payment type on device builds
            if (enabledPaymentTypes != null)
            {
                enabledPaymentTypes.Remove(PaymentType.editor);
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
                Instance.Value._event.TrackCustomEvent("first_open");
            }

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
                // Deliver pending deliverables from server
                Instance.Value._iap.DeliverPendingDeliverablesAsync();
                // Query purchases against Google Play Billing
#if UNITY_ANDROID

                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_start_query_purchases");
                Instance.Value._iap.QueryPurchasesAsync();
#endif
                // Disabled for production to reduce event noise
                // Instance.Value._eventSender.Send("sdk_init_online_success");

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

            OnInitSuccess?.Invoke();
            if (onSuccess != null) await onSuccess.Invoke();

            var attribution = await GetAdjustAttributionAsync();

            log.Info("Adjust Attribution: " +
                $"tracker_token={attribution.TrackerToken}, " +
                $"tracker_name={attribution.TrackerName}, " +
                $"network={attribution.Network}, " +
                $"campaign={attribution.Campaign}, " +
                $"adgroup={attribution.Adgroup}, " +
                $"creative={attribution.Creative}, " +
                $"click_label={attribution.ClickLabel}, " +
                $"adid={attribution.Adid}, " +
                $"cost_type={attribution.CostType}, " +
                $"cost_amount={attribution.CostAmount}, " +
                $"cost_currency={attribution.CostCurrency}, " +
                $"fb_install_referrer={attribution.FbInstallReferrer}"
            );

            Instance.Value._eventSender.Send("adjust_attribution", new Dictionary<string, IConvertible> {
                { "tracker_token", attribution.TrackerToken ?? "" },
                { "tracker_name", attribution.TrackerName ?? "" },
                { "network", attribution.Network ?? "" },
                { "campaign", attribution.Campaign ?? "" },
                { "adgroup", attribution.Adgroup ?? "" },
                { "creative", attribution.Creative ?? "" },
                { "click_label", attribution.ClickLabel ?? "" },
                { "adid", attribution.Adid ?? "" },
                { "cost_type", attribution.CostType ?? "" },
                { "cost_amount", attribution.CostAmount },
                { "cost_currency", attribution.CostCurrency ?? "" },
                { "fb_install_referrer", attribution.FbInstallReferrer ?? "" }
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
                int delaySeconds = 10;
                const int maxDelaySeconds = 300; // 5 minutes

                log.Debug("Noctua: Offline mode is enabled, entering reconnection loop...");

                while (loop)
                {
                    log.Debug($"Reconnection retry in {delaySeconds} seconds...");
                    await UniTask.Delay(TimeSpan.FromSeconds(delaySeconds));

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
                            delaySeconds = Math.Min(delaySeconds * 2, maxDelaySeconds);
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

                if (Instance.Value._iaa == null)
                {
                    log.Warning("MediationManager is not initialized. Cannot apply remote IAA config. " +
                        "Check that isIAAEnabled is true and local IAA config exists.");
                    Instance.Value.InitializeNativePlugin();
                    return;
                }

                log.Info("initializing IAA SDK from remote config : " + initResponse.RemoteConfigs.IAA.Mediation);

                Instance.Value._iaa.IAAResponse = initResponse.RemoteConfigs.IAA;
                log.Debug("Noctua IAA config replaced with remote config: " + JsonConvert.SerializeObject(Instance.Value._iaa.IAAResponse));

                Instance.Value._iaa.Initialize(() => {

                    log.Info("IAA SDK initialized from remote config");

                    Instance.Value.InitializeNativePlugin();
                    log.Info("nativePlugin initialized");
                });
                #else
                Instance.Value.InitializeNativePlugin();
                log.Info("nativePlugin initialized because UNITY_ADMOB or UNITY_APPLOVIN is not defined");
                #endif
            }
            else
            {
                log.Info("Remote config IAA is not configured yet");
                Instance.Value.InitializeNativePlugin();
                log.Info("nativePlugin initialized");
            }
        }
    }
}
