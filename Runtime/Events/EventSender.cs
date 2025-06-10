using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk.Events
{
    [Preserve]
    public class EventSenderConfig
    {
        public string BaseUrl;
        public string ClientId;
        public string BundleId = Application.identifier;
        public uint BatchSize = 20;
        public int MaxBatchSize = 100;
        public uint BatchPeriodMs = 60_000; // 1 minute, in ms
        public int CycleDelay = 1000; // 1 sec, in ms
    }
    
    [Preserve]
    public class EventResponse
    {
        [JsonProperty("message")]
        public string Message;
    }

    public class EventSender : IDisposable
    {
        public DateTime LastEventTime { get; private set; }

        private readonly ILogger _log = new NoctuaLogger(typeof(EventSender));
        private readonly EventSenderConfig _config;
        private readonly NoctuaLocale _locale;
        private ConcurrentQueue<Dictionary<string, IConvertible>> _eventQueue;
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

            _log.Debug($"Setting fields: " +
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
            _sendTask = UniTask.Create(SendEvents, _cancelSendSource.Token);

            _deviceId = SystemInfo.deviceUniqueIdentifier;
            _sdkVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();

#if UNITY_ANDROID && !UNITY_EDITOR
            _uniqueId = GetGoogleAdId();
#elif UNITY_IOS && !UNITY_EDITOR
            _uniqueId = UnityEngine.iOS.Device.vendorIdentifier;
#else
            _uniqueId = null;
#endif

            LoadEventsFromPlayerPrefs();
        }

        private void LoadEventsFromPlayerPrefs()
        {
            _log.Info("Loading NoctuaEvents from PlayerPrefs to event queue");

            string eventsJson = PlayerPrefs.GetString("NoctuaEvents", "[]");

            if (eventsJson.Length > 800000) // Around 1000 events
            {
                _log.Warning("NoctuaEvents too large, clearing PlayerPrefs to prevent issues");
                PlayerPrefs.SetString("NoctuaEvents", "[]");
                PlayerPrefs.Save();
                eventsJson = "[]";
            }

            // Try to parse into IConvertible first because it is
            // the native type of the queue.
            // There will be nested try catch to make it safe.
            var events = new List<Dictionary<string, IConvertible>>();

            try
            {
                // First try parsing directly into convertible dictionaries
                events = JsonConvert.DeserializeObject<List<Dictionary<string, IConvertible>>>(eventsJson);
            }
            catch (Exception e)
            {
                // If fail, try to parse to object.
                // IConvertible cannot parse null value from JSON.
                // Load from PlayerPrefs and re-enqueue them all
                
                _log.Warning($"Failed to parse events as IConvertible: {e.Message}");

                // Fallback to generic object parsing
                _log.Info("Trying fallback JSON parse as List<Dictionary<string, object>>");
                try
                {
                    var objectEvents = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(eventsJson);

                    foreach (var evt in objectEvents)
                    {
                        var convertedEvent = new Dictionary<string, IConvertible>();
                        foreach (var (key, val) in evt)
                        {
                            if (val is IConvertible convertible)
                            {
                                convertedEvent[key] = convertible;
                            }
                            else
                            {
                                _log.Warning($"Skipping non-convertible key: {key}, value: {val}");
                            }
                        }
                        events.Add(convertedEvent);
                    }
                }
                catch (Exception e2)
                {
                    _log.Error($"Fallback JSON parse failed: {e2.Message}");
                }
            }

            _log.Info($"Total events loaded into queue: {events.Count}");

            _eventQueue = new ConcurrentQueue<Dictionary<string, IConvertible>>(events);
            _log.Info($"Concurrent queue initialized with {_eventQueue.Count} events");
        }

        public void Send(string name, Dictionary<string, IConvertible> data = null)
        {
            if (_eventQueue.Count > 1000)
            {
                _log.Warning($"Event queue is full ({_eventQueue.Count}), ignore this event {name}");
                return;
            }

            data ??= new Dictionary<string, IConvertible>();

            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            var nullFields = data.Where(kv => kv.Value == null).Select(kv => kv.Key).ToList();

            foreach (var field in nullFields)
            {
                data.Remove(field);
            }

            // Fire-and-forget safe background task
            UniTask.Void(async () =>
            {
                try
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
                    data.TryAdd("country", _locale.GetCountry());
                    data.TryAdd("ipAddress", _ipAddress);
                    data.TryAdd("is_sandbox", _isSandbox);

                    LastEventTime = _start.AddSeconds(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);
                    data.TryAdd("timestamp", LastEventTime.ToString("O"));

                    if (_userId != null) data.TryAdd("user_id", _userId);
                    if (_playerId != null) data.TryAdd("player_id", _playerId);
                    if (_credentialId != null) data.TryAdd("credential_id", _credentialId);
                    if (_credentialProvider != null) data.TryAdd("credential_provider", _credentialProvider);
                    if (_gameId != null) data.TryAdd("game_id", _gameId);
                    if (_gamePlatformId != null) data.TryAdd("game_platform_id", _gamePlatformId);
                    if (_sessionId != null) data.TryAdd("session_id", _sessionId);
                    if (_uniqueId != null) data.TryAdd("unique_id", _uniqueId);

                    _log.Info($"queued event '{LastEventTime:O}|{name}|{_deviceId}|{_sessionId}|{_userId}|{_playerId}'");

                    // Deprecated: Use ConcurrentQueue to avoid blocking the main thread
                    // _eventQueue.Add(data);

                    _eventQueue.Enqueue(data);

                    PlayerPrefs.SetString("NoctuaEvents", JsonConvert.SerializeObject(_eventQueue));
                    PlayerPrefs.Save();
                    _log.Info($"{name} added to the queue. Current total event in queue: {_eventQueue.Count}");

                    // This check is used to maintain the offline state more frequent to update.
                    // This also prevent "offline" event flooding the queue
                    // by not sending another "offline" event if the event name is "offline"
                    if (data.TryGetValue("event_name", out var eventName) && eventName.ToString() != "offline")
                    {
                        _ = Noctua.IsOfflineAsync().ContinueWith((isOffline) =>
                        {
                            if (isOffline)
                            {
                                Noctua.OnOffline();
                            }
                            else
                            {
                                Noctua.OnOnline();
                            }
                        });
                    }
                }
                catch (Exception e)
                {
                    _log.Warning($"Failed to enqueue or persist event {name}: {e}");
                }
            });
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
                _log.Warning("Failed to get Google Advertising ID: " + e.Message);
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

        public void Flush()
        {
        #if UNITY_IOS && !UNITY_EDITOR
            // This patch only applied for IOS to cover Sortify specific crash
            // where the HTTP request causes crash when the app is trying to quit
            _log.Debug("On Flush called on iOS. Abort to avoid crash");
            return;

            // No need to backup to PlayerPrefs. The latest backup from Send() is already sufficient
        #endif

            _log.Debug($"On Flush called. Current total events in queue: {_eventQueue.Count}");

            // Snapshot the queue contents in a thread-safe way
            var snapshot = _eventQueue.ToArray();

            var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/events")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-DEVICE-ID", SanitizeHeaderValue(_deviceId))
                .WithNdjsonBody(snapshot);

            UniTask.Void(async () =>
            {
                try
                {
                    await request.Send<EventResponse>();

                    // All dequeued events is sent successfuly to server,
                    // then it's safe to remove all items from PlayerPrefs
                    // Clear the queue only after success
                    while (_eventQueue.TryDequeue(out _)) { }

                    await UniTask.SwitchToMainThread();

                    // Clear PlayerPrefs after successful send
                    PlayerPrefs.SetString("NoctuaEvents", "[]");
                    PlayerPrefs.Save();

                    _log.Info($"Sent {snapshot.Length} events. PlayerPrefs cleared.");
                }
                catch (Exception e)
                {
                    // No need to backup to PlayerPrefs. The latest backup from Send() is already sufficient.
                    // No need to re-enqueue because the clearing queue part is on the success branch above
                    // Simply print the error as warning
                    // We don't clear the queue on failure
                    _log.Warning("Failed to send events: " + e.Message);
                }
            });
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _cancelSendSource.Cancel();
            _disposed = true;
        }

        ~EventSender()
        {
            Dispose();
        }

        private async UniTask SendEvents(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                // For any early continue or next cycle, they will be guarded by this delay
                await UniTask.Delay(_config.CycleDelay, cancellationToken: token);

                // If the queue is empty, wait for another 1 sec
                while (_eventQueue.IsEmpty)
                {
                    await UniTask.Delay(1000, cancellationToken: token);
                }

                var nextBatchSchedule = DateTime.UtcNow.AddMilliseconds(_config.BatchPeriodMs);

                // If the queue length is less than batch size or it is not reached the next batch schedule yet
                while (_eventQueue.Count < _config.BatchSize && DateTime.UtcNow < nextBatchSchedule)
                {
                    await UniTask.Delay(1000, cancellationToken: token);
                }

                if (_eventQueue.IsEmpty)
                {
                    // If the queue is still empty, immediately return.
                    // At this point, the maximum delay for a cycle is "CycleDelay" + 2 seconds = 7 seconds
                    continue;
                }

                // The minimum delay time is 
                var isOffline = await Noctua.IsOfflineAsync();
                if (isOffline)
                {
                    Noctua.OnOffline();
                    _log.Info("Device is offline, continue to next cycle");
                    continue;
                }

                Noctua.OnOnline();

                // Build batch from the queue
                var events = new List<Dictionary<string, IConvertible>>();
                while (events.Count < _config.MaxBatchSize && _eventQueue.TryDequeue(out var item))
                {
                    events.Add(item);
                }

                _log.Info($"Batch size: {_config.MaxBatchSize}, events to be sent: {events.Count}, events remaining in queue: {_eventQueue.Count}");

                try
                {
                    var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/events")
                        .WithHeader("X-CLIENT-ID", _config.ClientId)
                        .WithHeader("X-DEVICE-ID", SanitizeHeaderValue(_deviceId))
                        .WithNdjsonBody(events);

                    await request.Send<EventResponse>();
                    _log.Info($"Sent {events.Count} events. Events left in queue: {_eventQueue.Count}");
                }
                catch (Exception e)
                {
                    _log.Error($"Failed to send events to server: {e.Message}");

                    // Re-enqueue failed events
                    foreach (var ev in events)
                    {
                        _eventQueue.Enqueue(ev);
                    }
                }
            }
        }

        
        private string SanitizeHeaderValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            var sanitized = new string(value.Where(c => c >= 32 && c != 127).ToArray());
            if (sanitized != value)
            {
                _log.Info($"Header value sanitized. Original: {value}, Sanitized: {sanitized}");
            }
            return sanitized;
        }

    }
}