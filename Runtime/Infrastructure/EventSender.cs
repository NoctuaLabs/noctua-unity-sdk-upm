using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.Events
{
    /// <summary>
    /// Configuration parameters for the <see cref="EventSender"/> background event batching and dispatch system.
    /// </summary>
    [Preserve]
    public class EventSenderConfig
    {
        /// <summary>The base URL of the event tracking API endpoint.</summary>
        public string BaseUrl;

        /// <summary>The client identifier sent as an HTTP header with each event batch.</summary>
        public string ClientId;

        /// <summary>The application bundle identifier, defaults to <see cref="Application.identifier"/>.</summary>
        public string BundleId = Application.identifier;

        /// <summary>Number of events that triggers an immediate batch send. Defaults to 20.</summary>
        public uint BatchSize = 20;

        /// <summary>Maximum number of events to include in a single HTTP request. Defaults to 100.</summary>
        public int MaxBatchSize = 100;

        /// <summary>Maximum time in milliseconds to wait before sending a partial batch. Defaults to 60000 (1 minute).</summary>
        public uint BatchPeriodMs = 60_000; // 1 minute, in ms

        /// <summary>Delay in milliseconds between background send loop cycles. Defaults to 10000 (10 seconds).</summary>
        public int CycleDelay = 10_000; // 10 sec, in ms

        /// <summary>Maximum number of events to store locally before FIFO eviction. Defaults to 100000.</summary>
        public int MaxStoredEvents = 100_000; // 100K events cap with FIFO eviction

        /// <summary>Native plugin providing per-row event storage operations (insert, query, delete).</summary>
        public INativeEventStorage NativePlugin;

        /// <summary>Native plugin for retrieving Firebase analytics session and installation IDs.</summary>
        public INativeFirebase NativeFirebase;

        /// <summary>Native plugin for notifying the tracker of online/offline state transitions.</summary>
        public INativeTracker NativeTracker;

        /// <summary>Delegate that returns whether the SDK is currently in offline mode.</summary>
        public Func<bool> IsOfflineModeFunc;

        /// <summary>Delegate that returns whether Adjust offline mode toggling is disabled.</summary>
        public Func<bool> AdjustOfflineModeDisabledFunc;

        /// <summary>Firebase configuration controlling custom event tracking per platform.</summary>
        public FirebaseConfig FirebaseConfig = new FirebaseConfig();
    }

    /// <summary>
    /// Response returned by the event tracking API after a batch submission.
    /// </summary>
    [Preserve]
    public class EventResponse
    {
        /// <summary>The server response message confirming receipt of the event batch.</summary>
        [JsonProperty("message")]
        public string Message;
    }

    /// <summary>
    /// Data returned by the GeoIP service containing the user's country and IP address.
    /// </summary>
    [Preserve]
    public class GeoIPData
    {
        /// <summary>The ISO country code determined by the GeoIP service.</summary>
        [JsonProperty("country")]
        public string Country;

        /// <summary>The public IP address of the requesting device.</summary>
        [JsonProperty("ip_address")]
        public string IpAddress;

        /// <summary>
        /// Creates a shallow copy of this <see cref="GeoIPData"/> instance.
        /// </summary>
        /// <returns>A new <see cref="GeoIPData"/> with the same field values.</returns>
        public GeoIPData ShallowCopy()
        {
            return (GeoIPData)MemberwiseClone();
        }
    }

    /// <summary>
    /// Queues analytics events into native per-row storage and sends them to the server in batches.
    /// Events are batched by count (<see cref="EventSenderConfig.BatchSize"/>) or time
    /// (<see cref="EventSenderConfig.BatchPeriodMs"/>), persisted locally for offline resilience,
    /// and sent via NDJSON HTTP POST. Implements <see cref="IEventSender"/> and <see cref="IDisposable"/>.
    /// </summary>
    public class EventSender : IDisposable, IEventSender
    {
        /// <summary>The UTC timestamp of the most recently enqueued event.</summary>
        public DateTime LastEventTime { get; private set; }

        private readonly ILogger _log = new NoctuaLogger(typeof(EventSender));
        private readonly EventSenderConfig _config;
        private readonly NoctuaLocale _locale;
        private readonly UniTask _sendTask;
        private readonly CancellationTokenSource _cancelSendSource;
        private readonly DateTime _start;
        private readonly string _sdkVersion;
        private readonly string _uniqueId;
        private readonly string _deviceId;

        private bool _disposed;

        private long? _userId;
        private long? _playerId;
        private long? _credentialId;
        private string _credentialProvider;
        private long? _gameId;
        private long? _gamePlatformId;
        private string _sessionId;
        private string _ipAddress;
        private bool? _isSandbox;
        private static bool _isQuitting = false;
        private volatile bool _isFlushing;
        private DateTime _lastConnectivityCheck = DateTime.MinValue;
        private static readonly TimeSpan ConnectivityCheckInterval = TimeSpan.FromSeconds(30);

        // Cached Firebase IDs — fetched once per session to avoid static callback overwriting
        // on iOS when multiple async calls race (IosPlugin uses single static callback slots).
        private string _cachedFirebaseSessionId;
        private string _cachedFirebaseInstallationId;
        private bool _firebaseIdsFetched;

        // Write queue for burst-safe storage writes — each item is a serialized JSON string
        private readonly ConcurrentQueue<string> _writeQueue = new();
        private volatile bool _isProcessingWriteQueue;
        private volatile bool _writeQueuePaused;

        // --- Private helpers that call _config.NativePlugin directly ---
        // EventSender is constructed inside the Noctua() constructor (which runs inside
        // a Lazy<Noctua> factory). Calling Noctua.GetEventCountAsync() etc. from here
        // would trigger Instance.Value re-entry and throw
        // "ValueFactory attempted to access the Value property of this instance."
        // These helpers avoid the circular dependency by calling the native plugin directly.

        private Task<int> GetEventCountDirectAsync()
        {
            var tcs = new TaskCompletionSource<int>();
            try
            {
                _config.NativePlugin.GetEventCount(count => tcs.TrySetResult(count));
            }
            catch (Exception ex)
            {
                _log.Warning($"[Event Sender] GetEventCount failed: {ex.Message}");
                tcs.TrySetResult(0);
            }
            return tcs.Task;
        }

        private Task<List<NativeEvent>> GetEventsBatchDirectAsync(int limit, int offset)
        {
            var tcs = new TaskCompletionSource<List<NativeEvent>>();
            try
            {
                _config.NativePlugin.GetEventsBatch(limit, offset, events => tcs.TrySetResult(events));
            }
            catch (Exception ex)
            {
                _log.Warning($"[Event Sender] GetEventsBatch failed: {ex.Message}");
                tcs.TrySetResult(new List<NativeEvent>());
            }
            return tcs.Task;
        }

        private Task<int> DeleteEventsByIdsDirectAsync(long[] ids)
        {
            var tcs = new TaskCompletionSource<int>();
            try
            {
                _config.NativePlugin.DeleteEventsByIds(ids, deletedCount => tcs.TrySetResult(deletedCount));
            }
            catch (Exception ex)
            {
                _log.Warning($"[Event Sender] DeleteEventsByIds failed: {ex.Message}");
                tcs.TrySetResult(0);
            }
            return tcs.Task;
        }

        private Task<List<string>> GetEventsDirectAsync()
        {
            var tcs = new TaskCompletionSource<List<string>>();
            try
            {
                _config.NativePlugin.GetEvents(events => tcs.TrySetResult(events));
            }
            catch (Exception ex)
            {
                _log.Warning($"[Event Sender] GetEvents failed: {ex.Message}");
                tcs.TrySetResult(new List<string>());
            }
            return tcs.Task;
        }

        /// <summary>
        /// Sets the user, player, credential, game, and session properties attached to subsequent events.
        /// Pass the default value (0 for numeric, empty string for text) to leave a property unchanged.
        /// </summary>
        /// <param name="userId">The Noctua user ID, or 0 to keep the current value.</param>
        /// <param name="playerId">The game-specific player ID, or 0 to keep the current value.</param>
        /// <param name="credentialId">The credential ID used for authentication, or 0 to keep the current value.</param>
        /// <param name="credentialProvider">The authentication provider name, or empty string to keep the current value.</param>
        /// <param name="gameId">The game ID, or 0 to keep the current value.</param>
        /// <param name="gamePlatformId">The game platform ID, or 0 to keep the current value.</param>
        /// <param name="sessionId">The current session ID, or empty string to keep the current value.</param>
        /// <param name="ipAddress">The device IP address, or empty string to keep the current value.</param>
        /// <param name="isSandbox">Whether the environment is sandbox, or null to keep the current value.</param>
        public void SetProperties(
            long? userId = 0,
            long? playerId = 0,
            long? credentialId = 0,
            string credentialProvider = "",
            long? gameId = 0,
            long? gamePlatformId = 0,
            string sessionId = "",
            string ipAddress = "",
            bool? isSandbox = null
        )
        {
            if (userId != 0) _userId = userId;

            if (playerId != 0) _playerId = playerId;

            if (credentialId != 0) _credentialId = credentialId;

            if (credentialProvider != "") _credentialProvider = credentialProvider;

            if (gameId != 0) _gameId = gameId;

            if (gamePlatformId != 0) _gamePlatformId = gamePlatformId;

            if (sessionId != "") _sessionId = sessionId;

            if (ipAddress != "") _ipAddress = ipAddress;

            if (isSandbox != null) _isSandbox = isSandbox;

            _log.Debug($"[Event Sender] Setting fields: " +
                $"userId={userId}, " +
                $"playerId={playerId}, " +
                $"credentialId={credentialId}, " +
                $"credentialProvider={credentialProvider}, " +
                $"gameId={gameId}, " +
                $"gamePlatformId={gamePlatformId}, " +
                $"sessionId={sessionId}, " +
                $"ipAddress={ipAddress}, " +
                $"isSandbox={isSandbox}"
            );
        }

        /// <summary>
        /// Initializes a new <see cref="EventSender"/>, validates the configuration, starts the
        /// background send loop, and triggers migration of legacy blob-format events if needed.
        /// </summary>
        /// <param name="config">Configuration specifying the API endpoint, batching parameters, and native plugins.</param>
        /// <param name="locale">Locale provider for country and language resolution.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="config"/> or <paramref name="locale"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when BaseUrl, ClientId, or BundleId is not set.</exception>
        public EventSender(EventSenderConfig config, NoctuaLocale locale)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));

            if (string.IsNullOrEmpty(_config.BaseUrl))
            {
                throw new ArgumentException("Base URL must be provided", nameof(config));
            }

            if (string.IsNullOrEmpty(_config.ClientId))
            {
                throw new ArgumentException("Client ID must be provided", nameof(config));
            }

            if (string.IsNullOrEmpty(_config.BundleId))
            {
                throw new ArgumentException("Bundle ID must be provided", nameof(config));
            }

            _locale = locale ?? throw new ArgumentNullException(nameof(locale));
            _start = DateTime.UtcNow - TimeSpan.FromSeconds(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
            _cancelSendSource = new CancellationTokenSource();

            _deviceId = SystemInfo.deviceUniqueIdentifier;
            _sdkVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

#if UNITY_ANDROID && !UNITY_EDITOR
            _uniqueId = GetGoogleAdId();
#elif UNITY_IOS && !UNITY_EDITOR
            _uniqueId = UnityEngine.iOS.Device.vendorIdentifier;
#else
            _uniqueId = null;
#endif

            // Start background send loop
            _sendTask = UniTask.Create(SendEvents, _cancelSendSource.Token);

            // Async: check per-row event count and migrate old blob if needed
            UniTask.Void(async () =>
            {
                await MigrateOldBlobEventsIfNeeded();
            });
        }

        /// <summary>
        /// Migration safety net: if old blob-format events exist and per-row storage is empty,
        /// migrate the blob events into per-row storage and clear the old blob.
        /// </summary>
        private async UniTask MigrateOldBlobEventsIfNeeded()
        {
            try
            {
                var newCount = await GetEventCountDirectAsync();
                if (newCount > 0) return; // Per-row storage already has events, skip migration

                var oldEvents = await GetEventsDirectAsync();
                if (oldEvents == null || oldEvents.Count == 0) return;

                _log.Info($"[Event Sender] Migrating {oldEvents.Count} events from old blob format to per-row storage");
                foreach (var eventJson in oldEvents)
                {
                    _config.NativePlugin?.InsertEvent(eventJson);
                }
                _config.NativePlugin?.DeleteEvents(); // Clear old blob
                _log.Info($"[Event Sender] Migration complete. {oldEvents.Count} events moved to per-row storage.");
            }
            catch (Exception e)
            {
                _log.Warning($"[Event Sender] Old blob migration failed (non-fatal): {e.Message}");
            }
        }

        /// <summary>
        /// Enqueues an analytics event with the given name and optional data payload.
        /// The event is enriched with device, session, and user metadata, serialized to JSON,
        /// and persisted to native per-row storage for later batch transmission.
        /// </summary>
        /// <param name="name">The event name (e.g. "session_start", "purchase_complete").</param>
        /// <param name="data">Optional key-value pairs of event-specific data. Reserved keys (user_id, device_id, etc.) are removed.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="name"/> is null or empty.</exception>
        public void Send(string name, Dictionary<string, IConvertible> data = null)
        {

           if (string.IsNullOrEmpty(name)) { throw new ArgumentNullException(nameof(name)); }

           data ??= new Dictionary<string, IConvertible>();

            var eventKeys = new HashSet<string>
            {
                "user_id",
                "device_id",
                "player_id",
                "session_id",
                "game_id"
            };

            var overriddenKeys = eventKeys.Where(k => data.ContainsKey(k)).ToList();

            foreach (var key in overriddenKeys)
            {
                if (data.Remove(key))
                {
                    _log.Debug($"[Event Sender] Removed reserved key '{key}' from event payload.");
                }
            }

            var nullFields = data.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();

            foreach (var field in nullFields)
            {
                data.Remove(field);
            }

            // Fire-and-forget safe background task
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();

                data.TryAdd("event_version", 1);
                data.TryAdd("event_name", name);
                data.TryAdd("sdk_version", _sdkVersion);
                data.TryAdd("device_id", _deviceId);
                data.TryAdd("device_os_version", SystemInfo.operatingSystem);
                data.TryAdd("device_os", SystemInfo.operatingSystemFamily.ToString());
                data.TryAdd("device_type", SystemInfo.deviceType.ToString());
                data.TryAdd("device_model", SystemInfo.deviceModel);
                data.TryAdd("bundle_id", _config.BundleId);
                data.TryAdd("game_version", Application.version);
                data.TryAdd("ipAddress", _ipAddress);
                data.TryAdd("is_sandbox", _isSandbox);

                string country = _locale.GetCountry();

                var isOffline = _config.IsOfflineModeFunc?.Invoke() ?? false;
                data.TryAdd("offline_mode", isOffline);

                if (String.IsNullOrEmpty(country) && !isOffline)
                {
                    try {
                        country = await GetCountryIDAsync();
                        if (!String.IsNullOrEmpty(country))
                        {
                            _locale.SetCountry(country);
                        }
                    } catch (Exception e) {
                        _log.Warning($"[Event Sender] Failed to get country ID: {e.Message}");
                    }
                }

                data.TryAdd("country", country);

                var activeExperiment = ExperimentManager.GetActiveExperiment();

                if (!string.IsNullOrEmpty(activeExperiment))
                {
                    data.TryAdd("experiment", activeExperiment);
                }

                var activeFeature = ExperimentManager.GetSessionTag();
                var sessionEvents = new HashSet<string>
                {
                    "session_start",
                    "session_end",
                    "session_pause",
                    "session_continue",
                    "session_heartbeat"
                };

                if (!string.IsNullOrEmpty(activeFeature) && sessionEvents.Contains(name))
                {
                    data.TryAdd("tag", activeFeature);
                }

                #if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
                bool shouldFetchFirebaseIds = false;
                #if UNITY_ANDROID
                    shouldFetchFirebaseIds = !_config.FirebaseConfig.Android.CustomEventDisabled;
                #elif UNITY_IOS
                    shouldFetchFirebaseIds = !_config.FirebaseConfig.Ios.CustomEventDisabled;
                #endif

                if (shouldFetchFirebaseIds)
                {
                    // Cache Firebase IDs to avoid re-fetching per event.
                    // On iOS, IosPlugin uses single static callback slots for Firebase ID calls.
                    // Rapid concurrent calls overwrite the pending callback, causing all but the
                    // last await to hang forever. Caching avoids this entirely.
                    if (!_firebaseIdsFetched)
                    {
                        try {
                            if (_config.NativeFirebase != null)
                            {
                                var sessionTcs = new TaskCompletionSource<string>();
                                _config.NativeFirebase.GetFirebaseAnalyticsSessionID(id => sessionTcs.TrySetResult(id ?? ""));
                                _cachedFirebaseSessionId = await sessionTcs.Task;

                                var installTcs = new TaskCompletionSource<string>();
                                _config.NativeFirebase.GetFirebaseInstallationID(id => installTcs.TrySetResult(id ?? ""));
                                _cachedFirebaseInstallationId = await installTcs.Task;
                            }
                            _firebaseIdsFetched = true;
                        } catch (Exception e) {
                            _log.Warning($"[Event Sender] Failed to get Firebase IDs: {e.Message}");
                        }
                    }

                    if (!string.IsNullOrEmpty(_cachedFirebaseSessionId))
                        data.TryAdd("firebase_analytics_session_id", _cachedFirebaseSessionId);
                    if (!string.IsNullOrEmpty(_cachedFirebaseInstallationId))
                        data.TryAdd("firebase_installation_id", _cachedFirebaseInstallationId);
                }
                #endif

                LastEventTime = _start.AddSeconds(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
                data.TryAdd("timestamp", LastEventTime.ToString("O"));

                if (_userId != null) data.TryAdd("user_id", _userId);
                if (_playerId != null) data.TryAdd("player_id", _playerId);
                if (_credentialId != null) data.TryAdd("credential_id", _credentialId);
                if (_credentialProvider != null) data.TryAdd("credential_provider", _credentialProvider);
                if (_gameId != null) data.TryAdd("game_id", _gameId);
                if (_gamePlatformId != null) data.TryAdd("game_platform_id", _gamePlatformId);

                var currentSessionId = ExperimentManager.GetSessionId();
                if (!string.IsNullOrEmpty(currentSessionId))
                {
                    data.TryAdd("session_id", currentSessionId);
                }

                if (_uniqueId != null) data.TryAdd("unique_id", _uniqueId);

                // Serialize to JSON and enqueue for per-row INSERT
                var eventJson = JsonConvert.SerializeObject(
                    data.ToDictionary(kv => kv.Key, kv => (object)kv.Value)
                );
                EnqueueEventForStorage(eventJson);

                var count = await GetEventCountDirectAsync();
                _log.Debug($"[Event Sender] Event '{name}' enqueued, current queue length: {count}");

                // Throttled connectivity check to avoid flooding with HTTP pings
                if (data.TryGetValue("event_name", out var eventNameValue) && eventNameValue.ToString() != "offline")
                {
                    if (DateTime.UtcNow - _lastConnectivityCheck >= ConnectivityCheckInterval)
                    {
                        _lastConnectivityCheck = DateTime.UtcNow;
                        try
                        {
                            _log.Debug("[Event Sender] Checking internet connection status after sending event");
                            InternetChecker.CheckInternetConnectionAsync((isConnected) =>
                            {
                                if (isConnected)
                                {
                                    NotifyOnline();
                                }
                                else
                                {
                                    NotifyOffline();
                                }
                            }).Forget();
                        }
                        catch (Exception e)
                        {
                            _log.Warning($"[Event Sender] Connectivity check skipped: {e.Message}");
                        }
                    }
                }
            });
        }

        private void NotifyOnline()
        {
            if (_config.AdjustOfflineModeDisabledFunc?.Invoke() == true) return;
            try { _config.NativeTracker?.OnOnline(); } catch (Exception) { }
        }

        private void NotifyOffline()
        {
            if (_config.AdjustOfflineModeDisabledFunc?.Invoke() == true) return;
            try { _config.NativeTracker?.OnOffline(); } catch (Exception) { }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private string GetGoogleAdId()
        {
            try
            {
                // Getting the current activity context
                var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                var currentActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");

                // Accessing the AdvertisingIdClient class and calling getAdvertisingIdInfo method
                var advertisingIdClient = new AndroidJavaClass("com.google.android.gms.ads.identifier.AdvertisingIdClient");
                var adInfo = advertisingIdClient.CallStatic<AndroidJavaObject>("getAdvertisingIdInfo", currentActivity);

                // Getting the advertising ID
                var advertisingId = adInfo.Call<string>("getId");

                // Checking if the user has enabled "Limit Ad Tracking"
                var isLimitAdTrackingEnabled = adInfo.Call<bool>("isLimitAdTrackingEnabled");

                return isLimitAdTrackingEnabled ? null : advertisingId;
            }
            catch (AndroidJavaException e)
            {
                _log.Warning("[Event Sender] Failed to get Google Advertising ID: " + e.Message);
            }

            return null;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterQuitHandler()
        {
            Application.quitting += () =>
            {
                _isQuitting = true;
            };
        }

        /// <summary>
        /// Immediately flushes all persisted events to the server in paginated batches.
        /// Skips flushing if the application is quitting or a flush is already in progress.
        /// Events that fail to send remain in storage for the next attempt.
        /// </summary>
	// This will be called in SessionTracker.cs
        public void Flush()
        {
            // Guard: skip HTTP flush during app quit or when called from a non-main thread
            // (e.g. GC finalizer). Events are already persisted in per-row storage and will
            // be sent on the next app launch. Attempting async Unity operations (UniTask,
            // UnityWebRequest, P/Invoke) during shutdown causes crashes because the Unity
            // player loop and native plugins may already be torn down.
            if (_isQuitting)
            {
                _log.Debug("[Event Sender] Flush skipped: app is quitting. Events will be sent on next launch.");
                return;
            }

            if (!Thread.CurrentThread.IsBackground && Thread.CurrentThread.ManagedThreadId != 1)
            {
                // Extra safety: if somehow called from a non-main thread that isn't
                // the background thread (e.g. finalizer), skip to avoid Unity API crashes.
                return;
            }

            if (_isFlushing) return;
            _isFlushing = true;

            UniTask.Void(async () =>
            {
                try
                {
                    // Wait for any in-progress write queue to finish first
                    while (_isProcessingWriteQueue)
                    {
                        if (_isQuitting) break;
                        await UniTask.Yield();
                    }

                    // PAUSE write queue — new events stay in ConcurrentQueue during HTTP
                    _writeQueuePaused = true;

                    // Drain any remaining write queue items into storage first
                    ProcessWriteQueue();

                    // Paginated flush: read batch -> HTTP -> delete -> repeat
                    while (true)
                    {
                        if (_isQuitting)
                        {
                            _log.Debug("[Event Sender] Flush aborted: app is quitting mid-flush.");
                            break;
                        }

                        var batch = await GetEventsBatchDirectAsync(_config.MaxBatchSize, 0);
                        if (batch == null || batch.Count == 0)
                        {
                            _log.Debug("[Event Sender] Flush: no more events to send");
                            break;
                        }

                        _log.Debug($"[Event Sender] Flush: sending batch of {batch.Count} events");

                        var eventDicts = batch.Select(e =>
                            JsonConvert.DeserializeObject<Dictionary<string, object>>(e.EventJson)
                        ).Where(d => d != null).ToList();

                        if (eventDicts.Count == 0) break;

                        var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/events")
                            .WithHeader("X-CLIENT-ID", _config.ClientId)
                            .WithHeader("X-DEVICE-ID", SanitizeHeaderValue(_deviceId))
                            .WithNdjsonBody(eventDicts);

                        await request.Send<EventResponse>();

                        // SUCCESS: delete sent events by ID
                        var sentIds = batch.Select(e => e.Id).ToArray();
                        await DeleteEventsByIdsDirectAsync(sentIds);

                        _log.Info($"[Event Sender] Flushed {eventDicts.Count} events.");
                    }
                }
                catch (Exception e)
                {
                    if (!_isQuitting)
                    {
                        _log.Warning("[Event Sender] Failed to flush events: " + e.Message);
                    }
                    // Remaining events stay in per-row storage for next attempt
                }
                finally
                {
                    _isFlushing = false;
                    // RESUME write queue
                    _writeQueuePaused = false;
                    if (!_isQuitting)
                    {
                        ProcessWriteQueue();
                    }
                }
            });
        }

        /// <summary>
        /// Cancels the background send loop and releases resources. Safe to call multiple times.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _cancelSendSource.Cancel();
            _disposed = true;
        }

        ~EventSender()
        {
            Dispose();
        }

        private async UniTask SendEvents(CancellationToken token)
        {
            DateTime? batchStartTime = null;

            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(_config.CycleDelay, cancellationToken: token);

                // Check event count from per-row storage (async)
                int pendingCount;
                try
                {
                    pendingCount = await GetEventCountDirectAsync();
                }
                catch
                {
                    continue;
                }

                if (pendingCount == 0)
                {
                    batchStartTime = null;
                    continue;
                }

                // Track when we first noticed pending events
                batchStartTime ??= DateTime.UtcNow;

                var batchAgeMs = (DateTime.UtcNow - batchStartTime.Value).TotalMilliseconds;
                var batchFull = pendingCount >= _config.BatchSize;
                var periodElapsed = batchAgeMs >= _config.BatchPeriodMs;

                _log.Debug($"[Event Sender] pendingCount: {pendingCount}, batchFull: {batchFull}, periodElapsed: {periodElapsed}, batchAgeMs: {batchAgeMs:F0}, batchPeriodMs: {_config.BatchPeriodMs}");

                if (!batchFull && !periodElapsed)
                {
                    continue;
                }

                if (_isFlushing)
                {
                    _log.Debug("[Event Sender] flush in progress, skipping send cycle");
                    continue;
                }

                var isOffline = _config.IsOfflineModeFunc?.Invoke() ?? false;
                if (isOffline)
                {
                    NotifyOffline();
                    _log.Debug("[Event Sender] device is offline, continue to next cycle");
                    continue;
                }

                NotifyOnline();

                // Read batch from per-row storage (always offset 0, we delete after send)
                List<NativeEvent> batch;
                try
                {
                    batch = await GetEventsBatchDirectAsync(_config.MaxBatchSize, 0);
                }
                catch (Exception e)
                {
                    _log.Warning($"[Event Sender] failed to read events batch: {e.Message}");
                    continue;
                }

                if (batch == null || batch.Count == 0)
                {
                    _log.Debug("[Event Sender] batch is null or empty");
                    continue;
                }

                var eventDicts = batch.Select(e =>
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(e.EventJson)
                ).Where(d => d != null).ToList();

                if (eventDicts.Count == 0)
                {
                    _log.Debug("[Event Sender] eventDicts is empty after deserialization");
                    continue;
                }

                _log.Info($"[Event Sender] sending batch: {eventDicts.Count} events (batchFull={batchFull}, periodElapsed={periodElapsed})");

                try
                {
                    // PAUSE write queue during HTTP
                    _writeQueuePaused = true;

                    var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/events")
                        .WithHeader("X-CLIENT-ID", _config.ClientId)
                        .WithHeader("X-DEVICE-ID", SanitizeHeaderValue(_deviceId))
                        .WithNdjsonBody(eventDicts);

                    await request.Send<EventResponse>();

                    // SUCCESS: delete only the sent events by their IDs
                    var sentIds = batch.Select(e => e.Id).ToArray();
                    var deletedCount = await DeleteEventsByIdsDirectAsync(sentIds);

                    var remainingCount = await GetEventCountDirectAsync();
                    _log.Info($"[Event Sender] sent {eventDicts.Count} events, deleted {deletedCount}. {remainingCount} remaining.");
                }
                catch (Exception e)
                {
                    _log.Error($"[Event Sender] failed to send events: {e.Message}");
                    // Events remain in per-row storage for retry
                }
                finally
                {
                    // Reset batch timer after send attempt (success or failure)
                    batchStartTime = null;
                    // RESUME write queue
                    _writeQueuePaused = false;
                    ProcessWriteQueue();
                }
            }
        }

        private string SanitizeHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sanitized = new string(value.Where(c => c >= 32 && c != 127).ToArray());
            if (sanitized != value)
            {
                _log.Info($"[Event Sender] Header value sanitized. Original: {value}, Sanitized: {sanitized}");
            }
            return sanitized;
        }

        /// <summary>
        /// Resolves the user's ISO country code by querying the GeoIP service first,
        /// then falling back to Cloudflare Trace if GeoIP fails.
        /// </summary>
        /// <returns>The ISO country code, or an empty string if both lookups fail.</returns>
        public async UniTask<string> GetCountryIDAsync()
        {
            string country = "";

            try {
                country = await GetCountryIDFromGeoIPAsync();
                return country;
            } catch (Exception e)
            {
                _log.Warning($"[Event Sender] Failed to get country ID from GeoIP: {e.Message}");
            }
            try {
                country = await GetCountryIDFromCloudflareTraceAsync();
                return country;
            } catch (Exception e) {
                _log.Warning($"[Event Sender] Failed to get country ID from Cloudflare Trace: {e.Message}");
            }

            return country;
        }

        /// <summary>
        /// Retrieves the user's country code from the Noctua GeoIP API.
        /// </summary>
        /// <returns>The ISO country code from the GeoIP response.</returns>
        public async UniTask<string> GetCountryIDFromGeoIPAsync()
        {
            string country = "";

            var request = new HttpRequest(HttpMethod.Post, "https://geoip.noctuaprojects.com/api/v1/geoip/country")
                .WithJsonBody(
                    new Dictionary<string, object>()
                );


            var response = await request.Send<GeoIPData>();

            _log.Debug($"[Event Sender] GeoIP response (inner data): {JsonConvert.SerializeObject(response)}");

            if (response != null && !string.IsNullOrEmpty(response.Country))
            {
                country = response.Country;
                return country;
            }

            _log.Warning($"[Event Sender] Failed to get country from GeoIP response: {JsonConvert.SerializeObject(response)}");
            return country;
        }


        /// <summary>
        /// Retrieves the user's country code by parsing the Cloudflare CDN trace endpoint response.
        /// </summary>
        /// <returns>The ISO country code extracted from the "loc=" field in the trace response.</returns>
        public async UniTask<string> GetCountryIDFromCloudflareTraceAsync()
        {
            // Extract domain from baseUrl
            string domain = "sdk-tracker.noctuaprojects.com";
            _log.Debug($"[Event Sender] Domain extracted from baseUrl: {domain}");
            var request = new HttpRequest(HttpMethod.Get, $"https://{domain}/cdn-cgi/trace");

            string responseText = await request.SendRaw();

            // Parse the response to get the 'loc' value
            string locValue = null;
            string[] lines = responseText.Split('\n');
            foreach (string line in lines)
            {
                if (line.StartsWith("loc="))
                {
                    locValue = line.Substring(4).Trim();
                    break;
                }
            }

            _log.Debug($"[Event Sender] Location value: {locValue}");

            return locValue;
        }

        // ===== Storage Helper Methods =====

        /// <summary>
        /// Enqueue a serialized event JSON for per-row INSERT.
        /// The write queue processor will INSERT each event individually.
        /// </summary>
        private void EnqueueEventForStorage(string eventJson)
        {
            _writeQueue.Enqueue(eventJson);
            ProcessWriteQueue();
        }

        /// <summary>
        /// Drain the write queue: INSERT each event as an individual row in native storage.
        /// O(1) per event — no full-replace, no serialize-all.
        /// Also enforces the MaxStoredEvents cap with FIFO eviction.
        /// </summary>
        private void ProcessWriteQueue()
        {
            if (_isProcessingWriteQueue) return;
            if (_writeQueuePaused) return;
            _isProcessingWriteQueue = true;

            try
            {
                while (_writeQueue.TryDequeue(out var eventJson))
                {
                    _config.NativePlugin?.InsertEvent(eventJson);
                }

                // Async eviction check — fire-and-forget
                UniTask.Void(async () =>
                {
                    try
                    {
                        var count = await GetEventCountDirectAsync();
                        if (count > _config.MaxStoredEvents)
                        {
                            // Evict the excess plus 10% buffer to avoid frequent evictions
                            var excess = count - _config.MaxStoredEvents;
                            var buffer = Math.Max(1, _config.MaxStoredEvents / 10);
                            var evictionSize = excess + buffer;
                            _log.Warning($"[Event Sender] Storage exceeded cap ({count}/{_config.MaxStoredEvents}). Evicting {evictionSize} oldest events.");
                            var oldest = await GetEventsBatchDirectAsync(evictionSize, 0);
                            if (oldest != null && oldest.Count > 0)
                            {
                                var idsToDelete = oldest.Select(e => e.Id).ToArray();
                                await DeleteEventsByIdsDirectAsync(idsToDelete);
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        _log.Warning($"[Event Sender] Eviction check failed: {e.Message}");
                    }
                });
            }
            catch (Exception e)
            {
                _log.Warning($"[Event Sender] Write queue processing failed: {e.Message}");
            }
            finally
            {
                _isProcessingWriteQueue = false;

                // If new events arrived during processing, process again
                if (_writeQueue.Count > 0 && !_writeQueuePaused)
                {
                    ProcessWriteQueue();
                }
            }
        }
    }
}
