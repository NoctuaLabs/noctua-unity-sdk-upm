using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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
        // PlayerPrefs key caching the last remote-resolved sandbox override (from
        // RemoteFeatureFlags.sandboxEnabled). Read at construction so services wire from it;
        // written after init when the remote flag provides a value, deleted when init no
        // longer provides it (reverting the source of truth to noctuagg.json).
        // Stored as 1 / 0.
        private const string SandboxOverridePrefKey = "NoctuaSandboxOverride";

        // The bundled noctuagg.json sandboxEnabled value, captured before any persisted
        // override is applied. This is the source of truth that init reverts to when the
        // server stops sending the sandbox flag.
        private bool _sandboxFromJson;

        // Stable, greppable tag prefixed to every sandbox-override log line.
        // Search the logs for [sandbox-override] to trace this flow end-to-end.
        private const string SandboxLogTag = "[sandbox-override]";

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

            // In any Editor environment, Application.streamingAssetsPath is a bare filesystem path.
            // UnityWebRequest needs a proper file:// URI or it treats the path as a network URL.
            // On Android device, streamingAssetsPath is already "jar:file://..." so no change needed.
            // new Uri() correctly produces file:///C:/... on Windows and file:///Users/... on macOS.
            #if UNITY_EDITOR
            var requestUri = new System.Uri(configPath).AbsoluteUri;
            #else
            var requestUri = configPath;
            #endif

            _log.Debug("Loading streaming assets in Android by using UnityWebRequest: " + requestUri);

            var configLoadRequest = UnityWebRequest.Get(requestUri);
            var now = DateTime.UtcNow;
            var timeout = now.AddSeconds(5);
            configLoadRequest.SendWebRequest();

            while (!configLoadRequest.isDone && now < timeout)
            {
                // Thread.Sleep instead of Task.Delay(..).Wait(): same blocking wait
                // (this runs inside the Lazy<T> constructor and cannot be async) but
                // without allocating a Task + timer per 10 ms tick.
                Thread.Sleep(10);
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
            HttpRequest.SetSandboxProvider(ResolveLiveSandbox);

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

            // Flowchart: a prior session may have persisted a sandbox override resolved from
            // RemoteFeatureFlags. Apply it here — before any sandbox-dependent wiring (base
            // URL, Inspector, native init) — so services are built from the persisted value
            // rather than only the bundled noctuagg.json.
            // Source of truth is noctuagg.json; the persisted override is only a cache of the
            // last remote value. Capture the json value before applying the override so init
            // can revert to it if the server stops sending the sandbox flag.
            _sandboxFromJson = _config.Noctua.IsSandbox;

            var hasSandboxOverride = PlayerPrefs.HasKey(SandboxOverridePrefKey);
            var persistedSandbox = hasSandboxOverride && PlayerPrefs.GetInt(SandboxOverridePrefKey) == 1;
            var effectiveSandbox = SandboxOverrideResolver.ResolveEffective(
                hasSandboxOverride, persistedSandbox, _sandboxFromJson);
            if (effectiveSandbox != _config.Noctua.IsSandbox)
            {
                _log.Info($"{SandboxLogTag} Applying persisted sandbox override: {effectiveSandbox} (noctuagg.json={_sandboxFromJson})");
                _config.Noctua.IsSandbox = effectiveSandbox;
            }

            if (_config.Noctua.IsSandbox)
            {
                _config.Noctua.BaseUrl = NoctuaConfig.DefaultSandboxBaseUrl;

                // Capture raw config text for the Build sanity panel's
                // SHA-256 checksum. Kept only when sandbox is on so
                // production builds don't retain the text in memory.
                _rawConfigJson = jsonConfig;

                // Noctua Inspector — sandbox-only, zero work in production.
                // HttpInspectorLog subscribes to the static HttpRequest observer
                // list; TrackerDebugMonitor subscribes to the static tracker
                // registry which also receives native-bridge emissions from
                // the iOS / Android SDKs.
                // Size the ring buffers by device RAM — high-end QA devices keep much more
                // history; low-RAM devices stay at the conservative defaults (no OOM risk).
                var inspectorLimits = InspectorBufferLimits.ForCurrentDevice();
                _log.Info($"Inspector buffer limits (RAM {SystemInfo.systemMemorySize}MB): " +
                          $"logs={inspectorLimits.Logs}, trackers={inspectorLimits.Trackers}, http={inspectorLimits.Http}");
                _httpLog = new HttpInspectorLog(inspectorLimits.Http);
                _debugMonitor = new TrackerDebugMonitor(inspectorLimits.Trackers);
                _logLedger = new LogInspectorLedger(inspectorLimits.Logs);
                _unityLogStream = new UnityLogStream();
                HttpInspectorHooks.RegisterObserver(_httpLog);
                TrackerObserverRegistry.Register(_debugMonitor);
                LogInspectorHooks.RegisterObserver(_logLedger);
                _unityLogStream.Start();

                // Auto-spawn the on-device overlay. Runs on any platform that
                // supports UIElements runtime — Editor, iOS, Android, desktop.
                try
                {
                    _inspector = com.noctuagames.sdk.Inspector.NoctuaInspectorController.Install(
                        _httpLog, _debugMonitor, _logLedger);
                    _log.Info("Noctua Inspector enabled (sandboxEnabled=true) — shake 3× / 4-finger tap to open");
                }
                catch (Exception e)
                {
                    _log.Warning($"Failed to spawn Noctua Inspector overlay: {e.Message}");
                }
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
                    NativeAdjust = _nativePlugin,
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

            _nativeSessionTracker = new NativeSessionTracker(
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
            var globalExceptionLogger = noctuaUIGameObject.AddComponent<GlobalExceptionLogger>();
            globalExceptionLogger.SetEventSender(_eventSender);

            // Native crash forwarder — OS-reported crashes (iOS MetricKit,
            // Android ApplicationExitInfo) surface as client_error with
            // source=native on the NEXT launch after the crash.
            try
            {
                _nativeCrashForwarder = new NativeCrashForwarder(_eventSender);
                _nativeCrashForwarder.Start();
            }
            catch (Exception ex)
            {
                _log.Warning($"NativeCrashForwarder init failed: {ex.Message}");
            }
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

            _nativeSessionTrackerBehaviour = noctuaUIGameObject.AddComponent<NativeSessionTrackerBehaviour>();
            _nativeSessionTrackerBehaviour.NativeSessionTracker = _nativeSessionTracker;
            _nativeSessionTrackerBehaviour.NativeLifecycle = _nativePlugin;
            // Registration is deferred to InitNativePlugin() which runs after _nativePlugin.Init().
            // ensureInit() in Noctua.kt silently drops calls made before Init() — registering
            // here would always be a no-op because presenter is not yet initialized.

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
            // IAA flagged on but no local config — do NOT block init. Warn and fall through
            // to the else branch below, which inits the native plugin and continues wiring
            // the SDK (no ads). Aborting here would leave _iap/_auth/_game/_platform/_app null
            // and crash the async init phase at _iap.IsReady.
            if (_config.Noctua.isIAAEnabled && _config.IAA == null)
            {
                _log.Warning("IAA is enabled but local IAA config is null — skipping IAA setup; " +
                             "SDK init will continue without ads. Please check your config file.");
            }

            if (_config.Noctua.isIAAEnabled && _config.IAA != null)
            {
                // Create NoctuaEventService FIRST so the tracker can be injected directly into
                // MediationManager's constructor. This ensures CreateNetworks() — called inside
                // the constructor via the IAAResponse property setter — receives a non-null
                // IAdRevenueTracker from the start, eliminating the "created with null tracker"
                // startup warning. NoctuaEventService only stores references at this point;
                // native plugin Init() is called later inside the Initialize() callback.
                _event = new NoctuaEventService(_nativePlugin, _eventSender);
                _event.SetProperties(isSandbox: _config.Noctua.IsSandbox);

                _iaa = new MediationManager(adPlaceholderUI: _uiFactory, iAAResponse: _config.IAA, adRevenueTracker: _event);
                _log.Info("Ad revenue tracker wired at MediationManager construction");

                // Inject the Firebase Remote Config fetcher so the effortless
                // ShowCrossPromotion(adType) overload can pull creatives from the
                // "cross_promotion" key without the Presenter touching the Noctua static facade.
                // The delegate is invoked lazily at show-time, never during construction, so it
                // does not trigger Lazy<Noctua> re-entry.
                _iaa.SetRemoteConfigProvider(GetFirebaseRemoteConfigString);

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
            else
            {
                // IAA disabled, or enabled with a null config (warned above):
                // no mediation — just init the native plugin and continue.
                _log.Info("Initialize nativePlugin while IAA is not enabled");
                InitializeNativePlugin();
            }

            // _event is already created above in the IAA-enabled branch.
            // Create it here only for the IAA-disabled path.
            if (_event == null)
            {
                _event = new NoctuaEventService(_nativePlugin, _eventSender);
                _event.SetProperties(isSandbox: _config.Noctua.IsSandbox);
            }

            // Install the watch-count milestone tracker. Mediations call
            // AdWatchMilestoneTracker.Default.RecordWatch(adType) on rewarded reward / interstitial close.
            // Route through NoctuaEventService.TrackCustomEvent so milestones reach Noctua Analytics
            // AND third-party trackers (Adjust / Firebase / Facebook) via the unified dispatch path.
            new AdWatchMilestoneTracker((eventName, payload) => _event.TrackCustomEvent(eventName, payload))
                .InstallAsDefault();

            // Anchor the install timestamp at SDK init. Without this, the install date is only
            // written lazily when UserSegmentManager is constructed in the mediation path
            // (MediationManager.CreateNetworks) — so games that log in before/without ads would
            // never have an install date, and login_on_dN could never fire.
            UserSegmentManager.EnsureInstallTimestamp();

            // Install the login-retention milestone tracker. WelcomeNotificationPresenter calls
            // LoginMilestoneTracker.Default.RecordLogin() on each login (account changed). Routed
            // through TrackCustomEvent so login_on_dN reaches Noctua Analytics AND third-party trackers.
            new LoginMilestoneTracker((eventName, payload) => _event.TrackCustomEvent(eventName, payload))
                .InstallAsDefault();
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

            _app = new NoctuaAppManager(_nativePlugin);

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

            // Pass Unity's resolved sandbox flag so native logging + Inspector bus follow
            // Unity instead of the native layer's own noctuagg.json (single source of truth).
            _nativePlugin?.Init(new List<string>(), _config.Noctua.IsSandbox);
            _isNativePluginInitialized = true;
            _nativePluginInitTcs.TrySetResult();
            _log.Debug("nativePlugin is initialized");

            // Wire native-bridge emission callbacks to the Unity-side
            // TrackerObserverRegistry. Runs only when sandbox is on —
            // the native SDK self-gates on `config.sandboxEnabled` anyway,
            // but installing the callback needlessly in production would
            // still allocate a JavaProxy / function pointer.
            if (_config.Noctua.IsSandbox)
            {
#if UNITY_IOS && !UNITY_EDITOR
                try { IosPlugin.InstallInspectorBridge(); }
                catch (Exception e) { _log.Warning($"InstallInspectorBridge (iOS) failed: {e.Message}"); }
#elif UNITY_ANDROID && !UNITY_EDITOR
                try { AndroidPlugin.InstallInspectorBridge(); }
                catch (Exception e) { _log.Warning($"InstallInspectorBridge (Android) failed: {e.Message}"); }
#endif
                // Wire native device-metrics provider to MemoryMonitor.
                // Adapter sits between Platform (INativeDeviceMetrics) and
                // Presenter (IDeviceMetricsProvider) — keeps MemoryMonitor
                // free of any Platform-layer reference.
                try
                {
                    if (_inspector?.MemoryMonitorComponent != null && _nativePlugin != null)
                    {
                        _inspector.MemoryMonitorComponent.SetNativeMetricsProvider(
                            new NoctuaDeviceMetricsAdapter(_nativePlugin));
                        // Same indirection rationale: pass a delegate so
                        // MemoryMonitor doesn't need an INativeMaintenance ref.
                        var pluginRef = _nativePlugin;
                        _inspector.MemoryMonitorComponent.SetClearNativeHttpCacheAction(
                            () => pluginRef.ClearNativeHttpCache());
                    }
                }
                catch (Exception e) { _log.Warning($"Wire MemoryMonitor native bridge failed: {e.Message}"); }

                // Wire native log-stream callback. The native side stays
                // dormant until the Inspector "Logs" tab toggles it on, so
                // registering the callback here is cheap.
                try
                {
                    _nativePlugin?.RegisterNativeLogCallback((level, source, tag, message, tsMs) =>
                    {
                        if (!LogInspectorHooks.HasObservers) return;
                        var ts = DateTimeOffset.FromUnixTimeMilliseconds(tsMs).UtcDateTime;
                        // Native priorities follow logcat (Verbose=2…Error=6);
                        // anything outside the enum range falls back to Info.
                        var lvl = level >= 2 && level <= 6 ? (LogLevel)level : LogLevel.Info;
                        LogInspectorHooks.Emit(new LogEntry(ts, lvl, source, tag, message));
                    });
                }
                catch (Exception e) { _log.Warning($"RegisterNativeLogCallback failed: {e.Message}"); }

                // Hook the Inspector "Logs → Native: on/off" chip so the
                // controller can toggle the native stream without taking a
                // Platform-layer dependency. _nativePlugin captured by closure.
                if (_inspector != null)
                {
                    _inspector.NativeLogStreamToggle = enabled =>
                    {
                        try { _nativePlugin?.SetLogStreamEnabled(enabled); }
                        catch (Exception e) { _log.Warning($"SetLogStreamEnabled failed: {e.Message}"); }
                    };

                    // Trackers tab "Re-fire" button — replays the exact event
                    // through the same pipeline (Adjust + Firebase + Facebook
                    // + Noctua) so QA can repro side effects without restarting
                    // the game. Coerces IReadOnlyDictionary<string, object> →
                    // Dictionary<string, IConvertible> at the boundary; values
                    // that aren't IConvertible (rare, mostly nested dicts) are
                    // coerced via ToString().
                    var senderRef = _eventSender;
                    if (senderRef != null)
                    {
                        _inspector.EventReplayHandler = (name, payload) =>
                        {
                            try
                            {
                                Dictionary<string, IConvertible> coerced = null;
                                if (payload != null && payload.Count > 0)
                                {
                                    coerced = new Dictionary<string, IConvertible>(payload.Count);
                                    foreach (var kv in payload)
                                    {
                                        coerced[kv.Key] = kv.Value is IConvertible ic
                                            ? ic
                                            : kv.Value?.ToString() ?? "";
                                    }
                                }
                                senderRef.Send(name, coerced);
                            }
                            catch (Exception e) { _log.Warning($"Event replay failed for {name}: {e.Message}"); }
                        };
                    }
                }
            }

            // Register the native lifecycle callback now — AFTER Init() so the native
            // presenter is initialized and ensureInit() won't drop the call.
            if (_nativeSessionTrackerBehaviour != null)
            {
                _nativePlugin?.RegisterNativeLifecycleCallback(_nativeSessionTrackerBehaviour.OnNativeLifecycleEvent);
                if (UnityEngine.Device.Application.isFocused)
                {
                    _nativeSessionTrackerBehaviour.OnNativeLifecycleEvent("resume");
                }
            }
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
        /// Resolves the runtime sandbox override after init (decisions in
        /// <see cref="SandboxOverrideResolver"/>): cache a remote value, or revert to
        /// noctuagg.json when the flag is no longer provided, then prompt a restart if the
        /// resolved value differs from the one used to wire this session.
        /// </summary>
        private async UniTask ResolveSandboxOverrideAsync(IReadOnlyDictionary<string, string> remoteFlags)
        {
            var remoteProvided = SandboxOverrideResolver.TryGetRemoteSandbox(remoteFlags, out var remoteSandbox);

            // Remote provides a value -> cache it.
            if (remoteProvided)
            {
                PlayerPrefs.SetInt(SandboxOverridePrefKey, remoteSandbox ? 1 : 0);
                PlayerPrefs.Save();
                _log.Debug($"{SandboxLogTag} remote sandboxEnabled={remoteSandbox} cached.");
                await PromptRestartIfChangedAsync(remoteSandbox);
                return;
            }

            // Remote omitted the flag but a stale cache exists -> revert to noctuagg.json.
            if (SandboxOverrideResolver.ShouldRevertToConfig(remoteProvided, PlayerPrefs.HasKey(SandboxOverridePrefKey)))
            {
                PlayerPrefs.DeleteKey(SandboxOverridePrefKey);
                PlayerPrefs.Save();
                _log.Info($"{SandboxLogTag} sandboxEnabled no longer provided — reverting to noctuagg.json={_sandboxFromJson}.");
                await PromptRestartIfChangedAsync(_sandboxFromJson);
                return;
            }

            // No remote flag and no cache -> nothing to do.
            _log.Debug($"{SandboxLogTag} unchanged (no remote flag and no cache).");
        }

        /// <summary>
        /// Live (per-request) sandbox state for the X-SANDBOX-ENABLED HTTP header: a freshly
        /// read persisted override wins; otherwise the bundled noctuagg.json value. Re-read on
        /// each call so a mid-session <see cref="ResolveSandboxOverrideAsync"/> cache update is
        /// reflected immediately (the session itself still requires a restart to switch base
        /// URL / service wiring).
        /// </summary>
        private bool ResolveLiveSandbox()
            => SandboxOverrideResolver.ResolveEffective(
                PlayerPrefs.HasKey(SandboxOverridePrefKey),
                PlayerPrefs.GetInt(SandboxOverridePrefKey, 0) == 1,
                _sandboxFromJson);

        /// <summary>
        /// Prompts a one-time restart when <paramref name="target"/> differs from the sandbox
        /// value used to wire this session (service wiring + native init can't change mid-session).
        /// </summary>
        private async UniTask PromptRestartIfChangedAsync(bool target)
        {
            if (!SandboxOverrideResolver.NeedsRestart(target, _config.Noctua.IsSandbox))
            {
                return;
            }

            _log.Info($"{SandboxLogTag} value {target} differs from current={_config.Noctua.IsSandbox} — prompting restart.");
            if (_uiFactory == null)
            {
                _log.Warning($"{SandboxLogTag} cannot prompt restart: _uiFactory is null.");
                return;
            }

            // Blocking dialog; acknowledging it quits the app so the constructor picks up the
            // new value on relaunch.
            await _uiFactory.ShowSandboxChangedDialog(
                "Sandbox mode has changed and will apply after a restart. Please reopen the app.");
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

                    // initResponse was never assigned on this path — falling through
                    // would NRE on initResponse.OfflineMode right after the error dialog.
                    // Rethrow so the game receives the original init failure cleanly
                    // (InitAsync is documented as reusable for the next attempt).
                    log.Warning($"Init failed without offline-first fallback; rethrowing to caller: {errorMessage}");
                    throw;
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

                // Hook IAP purchase completion into payer-tier tracking for CPM floor segmentation.
#if UNITY_ADMOB || UNITY_APPLOVIN
                if (Instance.Value._iaa != null)
                {
                    Instance.Value._iap.OnPurchaseDone += _ => Instance.Value._iaa?.RecordPurchase();
                }
#endif
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

            // Apply country-aware IAA features now that the country code is resolved:
            //   1. Pass country code to MediationManager so CPM floor segment key is correct.
            //   2. Evaluate A/B experiments (segment-filtered) and apply overrides without restarting networks.
#if UNITY_ADMOB || UNITY_APPLOVIN
            if (Instance.Value._iaa != null)
            {
                Instance.Value._iaa.SetCountryCode(initResponse.Country);
                ApplyIAAExperiments(log, initResponse.Country);
            }
#endif

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

            // Resolve the runtime sandbox override: cache a remote value, or revert to
            // noctuagg.json when it's no longer provided; restart-if-different. (noctuagg.json
            // is the source of truth; the PlayerPref is only a cache of the last remote value.)
            await Instance.Value.ResolveSandboxOverrideAsync(remoteFlags);

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

            try
            {
                var rcIAPRevenue = await GetFirebaseRemoteConfigDouble("taichi_iap_revenue_threshold");

                if (rcIAPRevenue > 0)
                {
                    var iapTaichi = new IAPTaichiConfig { RevenueThreshold = rcIAPRevenue };
                    Instance.Value._iap.SetIAPTaichiConfig(iapTaichi);
                    log.Debug($"[taichi] iap config applied: revenueThreshold={iapTaichi.RevenueThreshold:G} USD");
                }
                else
                {
                    log.Warning("[taichi] iap Firebase Remote Config value is empty or not set, skipping config");
                }
            }
            catch (Exception e)
            {
                log.Warning($"[taichi] iap failed to load Firebase Remote Config: {e.Message}");
            }

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

            log.Debug("Adjust Attribution: " +
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

            // Register push-notification bridges with the native plugin so game code can
            // subscribe to Noctua.OnRemoteNotificationReceived / OnNotificationTapped /
            // OnFirebaseMessagingTokenRefresh. Deferred until init completes so the native
            // plugin is guaranteed to be constructed.
            RegisterPushHandlers();

            // Sandbox-only convenience: log the FCM token to Unity console so QA can copy it
            // for backend push testing without writing extra game code. Production builds
            // (isSandbox = false) skip this — prevents accidental token leakage in release
            // logs that could be scraped by third-party log collectors.
            if (Instance.Value._config?.Noctua?.IsSandbox == true)
            {
                LogFirebaseMessagingTokenForSandbox().Forget();
            }
        }

        /// <summary>
        /// Fires inside a short retry loop after init completes on sandbox builds. The iOS
        /// APNs ↔ FCM handshake typically finishes within a few seconds of init when the
        /// user has previously granted notification permission; on Android the token is
        /// usually available immediately. The loop caps at ~12 s (6 attempts × 2 s) so a
        /// permanently-unavailable token never becomes a long-lived background task.
        /// </summary>
        private static async UniTaskVoid LogFirebaseMessagingTokenForSandbox()
        {
            var log = Instance.Value._log;
            const int maxAttempts = 6;
            const int retryDelayMs = 2000;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    var token = await GetFirebaseMessagingToken();
                    if (!string.IsNullOrEmpty(token))
                    {
                        log.Info($"[sandbox] FCM token: {token}");
                        return;
                    }
                }
                catch (Exception ex)
                {
                    log.Warning($"[sandbox] FCM token fetch attempt {attempt} failed: {ex.Message}");
                }

                if (attempt < maxAttempts)
                {
                    await UniTask.Delay(retryDelayMs);
                }
            }

            log.Warning("[sandbox] FCM token still unavailable after retries — " +
                        "check notification permission grant, APNs entitlement, or Firebase Messaging library link.");
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
        /// Evaluates active A/B experiments from the merged IAA config and applies their overrides
        /// to frequency caps and CPM floors — without restarting the ad networks.
        /// Called after the country code is resolved so segment filters can be applied correctly.
        /// </summary>
        private static void ApplyIAAExperiments(ILogger log, string countryCode)
        {
#if UNITY_ADMOB || UNITY_APPLOVIN
            var iaa = Instance.Value._iaa;
            if (iaa == null) return;

            var currentIaa = iaa.IAAResponse;
            if (currentIaa?.AdExperiments == null || currentIaa.AdExperiments.Count == 0)
            {
                log.Debug("No IAA experiments configured. Skipping experiment override.");
                return;
            }

            var segmentManager = iaa.GetSegmentManager();
            var eventSender    = Instance.Value._eventSender;

            var experimentManager = new AdExperimentManager(
                currentIaa.AdExperiments,
                segmentManager,
                eventSender
            );

            var effectiveIaa = experimentManager.ApplyExperiments(currentIaa, countryCode);

            // Only update managers if the experiment actually changed any config
            if (!ReferenceEquals(effectiveIaa, currentIaa))
            {
                iaa.ApplyExperimentOverride(effectiveIaa);
                log.Info("IAA experiment overrides applied for country: " + countryCode);
            }
            else
            {
                log.Debug("IAA experiments evaluated — no overrides applied (control for all).");
            }
#endif
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

                var localIaa = Instance.Value._config.IAA;
                var mergedIaa = localIaa != null
                    ? localIaa.MergeWith(initResponse.RemoteConfigs.IAA)
                    : initResponse.RemoteConfigs.IAA;
                // Tag the upcoming applied_iaa_config event so analytics can
                // distinguish remote-served configs from the initial local load.
                Instance.Value._iaa.ApplyIaaConfigFromRemote(mergedIaa);
                log.Debug("Noctua IAA config merged with remote config: " + JsonConvert.SerializeObject(Instance.Value._iaa.IAAResponse));

                // Re-wire revenue tracker after IAAResponse assignment (which calls CreateNetworks
                // and creates a new AdRevenueTrackingManager). Defense-in-depth: ensures tracker
                // is always set even if initialization order changes in future.
                Instance.Value._iaa.SetAdRevenueTracker(Instance.Value._event);
                log.Info("Ad revenue tracker re-wired before IAA Initialize() from remote config");

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
