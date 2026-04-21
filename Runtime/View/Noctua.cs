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
    public partial class Noctua
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

        /// <summary>Access in-app review and update manager.</summary>
        public static NoctuaAppManager App => Instance.Value._app;

        /// <summary>Access loaded global configuration.</summary>
        public static GlobalConfig Config => Instance.Value._config;

        /// <summary>
        /// Ring-buffered log of recent SDK HTTP exchanges, populated only when
        /// <c>noctuagg.json</c> has <c>sandboxEnabled: true</c>. <c>null</c> in
        /// production builds.
        /// </summary>
        public static HttpInspectorLog HttpLog => Instance.Value._httpLog;

        /// <summary>
        /// Ring-buffered log of recent tracker emissions (Firebase, Adjust,
        /// Facebook), populated only when sandbox is enabled. <c>null</c> in
        /// production builds.
        /// </summary>
        public static TrackerDebugMonitor DebugMonitor => Instance.Value._debugMonitor;

        /// <summary>
        /// True iff SDK was initialized with <c>sandboxEnabled: true</c>. Used
        /// by the Inspector bootstrap to decide whether to auto-spawn the
        /// on-device overlay.
        /// </summary>
        public static bool IsSandbox()
        {
            var cfg = Instance.Value._config;
            return cfg?.Noctua?.IsSandbox == true;
        }

        /// <summary>
        /// Handle to the auto-spawned Inspector overlay. Non-null only when
        /// <see cref="IsSandbox"/> is true (the overlay is never instantiated
        /// in production builds). Game code that wants a programmatic open
        /// button — e.g. a sample-app debug menu — can call
        /// <c>Noctua.Inspector?.Show()</c> directly.
        /// </summary>
        public static com.noctuagames.sdk.Inspector.NoctuaInspectorController Inspector => Instance.Value._inspector;

        /// <summary>Convenience: open the Inspector overlay if sandbox is on; no-op otherwise.</summary>
        public static void ShowInspector()   => Instance.Value._inspector?.Show();
        /// <summary>Convenience: hide the Inspector overlay. Safe to call any time.</summary>
        public static void HideInspector()   => Instance.Value._inspector?.Hide();
        /// <summary>Convenience: toggle the Inspector overlay. Safe to call any time.</summary>
        public static void ToggleInspector() => Instance.Value._inspector?.Toggle();

        private readonly ILogger _log = new NoctuaLogger();
        private readonly EventSender _eventSender;
        private HttpInspectorLog _httpLog;
        private TrackerDebugMonitor _debugMonitor;
        private com.noctuagames.sdk.Inspector.NoctuaInspectorController _inspector;
        private readonly SessionTracker _sessionTracker;
        private readonly NoctuaEventService _event;
        private readonly NoctuaAuthentication _auth;
        private readonly NoctuaIAPService _iap;
        private readonly NoctuaGameService _game;
        private readonly NoctuaPlatform _platform;
        private readonly UIFactory _uiFactory;
        private readonly MediationManager _iaa;
        private readonly NoctuaAppManager _app;
        private readonly NativeSessionTracker _nativeSessionTracker;
        private NativeSessionTrackerBehaviour _nativeSessionTrackerBehaviour;
        private GlobalConfig _config;

        private readonly INativePlugin _nativePlugin;
        // This is the flag from noctuagg.json config.
        // Not all game has this feature enabled.
        private bool _isOfflineFirst = false;
        // Will be true if offline first is enabled AND
        // there is network issue on init attempt
        private static volatile bool _offlineMode = false;
        private static bool _initialized = false;
        private bool _isNativePluginInitialized = false;
        private readonly UniTaskCompletionSource _nativePluginInitTcs = new UniTaskCompletionSource();

        /// <summary>
        /// Optional callback invoked when Noctua initialization completes successfully.
        /// </summary>
        public static Action? OnInitSuccess;

        /// <summary>
        /// Returns whether the SDK is running in offline mode.
        /// </summary>
        /// <returns><c>true</c> if offline mode is active; otherwise <c>false</c>.</returns>
        public static bool IsOfflineMode()
        {
            return _offlineMode;
        }

        /// <summary>
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
        /// Save events to native plugin where supported.
        /// </summary>
        public static void SaveEvents(string jsonString)
        {
           try
           {
              Instance.Value._nativePlugin.SaveEvents(jsonString);
           }
           catch (Exception ex)
           {
               Instance.Value._log.Warning("SaveEvents exception: " + ex.Message);
           }
        }

        /// <summary>
        /// Get saved events from native plugin asynchronously where supported.
        /// </summary>
        /// <returns>A task that resolves to the list of saved events.</returns>
       public static Task<List<string>> GetEventsAsync()
        {
            var tcs = new TaskCompletionSource<List<string>>();

            try
            {
                Instance.Value._nativePlugin.GetEvents(events =>
                {
                    tcs.SetResult(events);
                });
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning("GetEvents exception: " + ex.Message);
                tcs.SetResult(new List<string>());
            }

            return tcs.Task;
        }

        /// <summary>
        /// Delete saved events from native plugin where supported.
        /// </summary>
        public static void DeleteEvents()
        {
           try
           {
              Instance.Value._nativePlugin.DeleteEvents();
           }
           catch (Exception ex)
           {
               Instance.Value._log.Warning("DeleteEvents exception: " + ex.Message);
           }
        }

        // Per-row event storage async helpers

        /// <summary>
        /// Insert a single event into per-row native storage.
        /// </summary>
        public static void InsertEvent(string eventJson)
        {
            try
            {
                Instance.Value._nativePlugin.InsertEvent(eventJson);
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning("InsertEvent exception: " + ex.Message);
            }
        }

        /// <summary>
        /// Get a batch of events from per-row native storage asynchronously.
        /// </summary>
        public static Task<List<NativeEvent>> GetEventsBatchAsync(int limit, int offset)
        {
            var tcs = new TaskCompletionSource<List<NativeEvent>>();
            try
            {
                Instance.Value._nativePlugin.GetEventsBatch(limit, offset, events =>
                {
                    tcs.SetResult(events);
                });
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning("GetEventsBatch exception: " + ex.Message);
                tcs.SetResult(new List<NativeEvent>());
            }
            return tcs.Task;
        }

        /// <summary>
        /// Delete specific events by ID from per-row native storage asynchronously.
        /// </summary>
        public static Task<int> DeleteEventsByIdsAsync(long[] ids)
        {
            var tcs = new TaskCompletionSource<int>();
            try
            {
                Instance.Value._nativePlugin.DeleteEventsByIds(ids, deletedCount =>
                {
                    tcs.SetResult(deletedCount);
                });
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning("DeleteEventsByIds exception: " + ex.Message);
                tcs.SetResult(0);
            }
            return tcs.Task;
        }

        /// <summary>
        /// Get the total count of stored events asynchronously.
        /// </summary>
        public static async Task<int> GetEventCountAsync()
        {
            var tcs = new TaskCompletionSource<int>();
            try
            {
                Instance.Value._nativePlugin.GetEventCount(count =>
                {
                    tcs.TrySetResult(count);
                });

                var completed = await Task.WhenAny(tcs.Task, Task.Delay(5000));
                if (completed != tcs.Task)
                {
                    Instance.Value._log.Warning("GetEventCountAsync timed out after 5s");
                    return 0;
                }
                return tcs.Task.Result;
            }
            catch (Exception ex)
            {
                Instance.Value._log.Warning("GetEventCount exception: " + ex.Message);
                return 0;
            }
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

        /// <summary>
        /// Get the deterministic pseudo user ID derived from device identity.
        /// Survives app reinstalls. Scoped per app bundle.
        /// </summary>
        /// <returns>A 32-character lowercase hex string.</returns>
        public static string GetPseudoUserId()
        {
            return Instance.Value._eventSender.PseudoUserId;
        }

        /// <summary>
        /// Internal accessor for native session tracker stats (used by sample app test button).
        /// </summary>
        internal static NativeSessionTracker NativeSessionTrackerInstance => Instance.Value._nativeSessionTracker;

        /// <summary>
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

                if (!pause) // If resumed from background
                {
                    Noctua.Instance.Value._iap?.QueryPurchasesAsync();
                    Noctua.Instance.Value._iaa?.OnApplicationForeground();
                }
            }

            private void OnApplicationQuit()
            {
                try
                {
                    var log = new NoctuaLogger();
                    log.Info("NoctuaPauseBehaviour: OnApplicationQuit — disposing StoreKit and session tracker");
                    Noctua.Instance.Value._nativePlugin?.DisposeStoreKit();
                    Noctua.Instance.Value._sessionTracker?.Dispose();
                }
                catch (Exception) { }
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

        // BackupPlayerPrefs, RestorePlayerPrefs, GetPlayerPrefsKeys
        // moved to Noctua.PlayerPrefs.cs
    }
}
