using System;
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
        public uint BatchSize = 10;
        public uint MaxBatchSize = 100;
        public uint BatchPeriodMs = 60_000; // 1 minute
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
        private readonly Queue<Dictionary<string, IConvertible>> _eventQueue = new();
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
            _start = DateTime.UtcNow - TimeSpan.FromSeconds(Stopwatch.GetTimestamp() / (double) Stopwatch.Frequency);
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
            var eventsJson = PlayerPrefs.GetString("NoctuaEvents", "[]");
            if (eventsJson == null)
            {
                eventsJson = "[]";
            }

            // Try to parse into IConvertible first because it is
            // the native type of the queue.
            // There will be nested try catch to make it safe.
            var events = new List<Dictionary<string, IConvertible>>();
            //_log.Debug(eventsJson);
            try {
                events = JsonConvert.DeserializeObject<List<Dictionary<string, IConvertible>>>(eventsJson);
            } catch (Exception e) {
                events = new List<Dictionary<string, IConvertible>>();
                _log.Error($"Failed to load events from PlayerPrefs: {e.Message}.");

                // If fail, try to parse to object.
                // IConvertible cannot parse null value from JSON.
                // Load from PlayerPrefs and re-enqueue them all
                _log.Info("Try to parse NoctuaEvents with object type");
                var objects = new List<Dictionary<string, object>>();
                try {
                    objects = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(eventsJson);
                } catch (Exception e2) {
                    _log.Error($"Failed to load events from PlayerPrefs: {e2.Message}");
                    objects = new List<Dictionary<string, object>>();
                }
                if (objects == null)
                {
                    objects = new List<Dictionary<string, object>>();
                }
                foreach (var evt in objects)
                {
                    var dict = new Dictionary<string, IConvertible>();
                    foreach (var (key, val) in evt)
                    {
                        if (val is IConvertible convertible)
                        {
                            dict[key] = convertible;
                        }
                        else
                        {
                            _log.Warning($"Event has non-convertible value for key {key} of value {val}");
                        }
                        events.Add(dict);
                    }
                }
            }
            if (events == null)
            {
                events = JsonConvert.DeserializeObject<List<Dictionary<string, IConvertible>>>(eventsJson);
            }
            foreach (var evt in events)
            {
                _eventQueue.Enqueue(evt);
            }
            _log.Info($"Total loaded events: {events.Count}");
        }

        public void Send(string name, Dictionary<string, IConvertible> data = null)
        {
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
            
            if (_userId   != null) data.TryAdd("user_id", _userId);
            if (_playerId != null) data.TryAdd("player_id", _playerId);
            if (_credentialId != null) data.TryAdd("credential_id", _credentialId);
            if (_credentialProvider != null) data.TryAdd("credential_provider", _credentialProvider);
            if (_gameId != null) data.TryAdd("game_id", _gameId);
            if (_gamePlatformId != null) data.TryAdd("game_platform_id", _gamePlatformId);
            if (_sessionId != null) data.TryAdd("session_id", _sessionId);
            if (_uniqueId != null) data.TryAdd("unique_id", _uniqueId);
            
            _log.Info($"queued event '{LastEventTime:O}|{name}|{_deviceId}|{_sessionId}|{_userId}|{_playerId}'");
            
            _eventQueue.Enqueue(data);

            var events = new List<Dictionary<string, IConvertible>>();
            while (_eventQueue.TryDequeue(out var evt))
            {
                events.Add(evt);
            }
            PlayerPrefs.SetString("NoctuaEvents", JsonConvert.SerializeObject(events));
            PlayerPrefs.Save();
            foreach (var evt in events)
            {
                _eventQueue.Enqueue(evt);
            }
            _log.Info($"{name} added to the queue. Current total event in queue: {events.Count}");

            InternetChecker.CheckInternetConnection((isConnected) =>
            {
                if (isConnected)
                {
                    // TODO call nativePlugin.onOnline()
                } else {
                    // TODO call nativePlugin.onOffline()
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

        public void Flush()
        {
            var events = new List<Dictionary<string, IConvertible>>();

            while (_eventQueue.TryDequeue(out var evt))
            {
                events.Add(evt);
            }

            var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/events")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-DEVICE-ID", _deviceId)
                .WithNdjsonBody(events);

            UniTask.Void(async () => {
                try {
                    await request.Send<EventResponse>();
                    PlayerPrefs.SetString("NoctuaEvents", "[]");
                    PlayerPrefs.Save();
                    _log.Info($"Sent {events.Count} events. PlayerPrefs cleared.");
                } catch (Exception e) {
                    _log.Error($"Failed to send events: {e.Message}");
                    _log.Info($"Backup events to PlayerPrefs");
                    PlayerPrefs.SetString("NoctuaEvents", JsonConvert.SerializeObject(events));
                        PlayerPrefs.Save();
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
                while (_eventQueue.Count == 0)
                {
                    await UniTask.Delay(1000, cancellationToken: token);
                }
                
                var nextBatchSchedule = DateTime.UtcNow.AddMilliseconds(_config.BatchPeriodMs);

                while (_eventQueue.Count < _config.BatchSize && DateTime.UtcNow < nextBatchSchedule)
                {
                    await UniTask.Delay(1000, cancellationToken: token);
                }
                
                if (_eventQueue.Count == 0)
                {
                    continue;
                }

                // Dequeue to be sent
                var backup = new List<Dictionary<string, IConvertible>>();
                var events = new List<Dictionary<string, IConvertible>>();
                while (_eventQueue.TryDequeue(out var evt) && events.Count < _config.MaxBatchSize)
                {
                    events.Add(evt);
                }

                // Dequeue the rest to be saved to player prefs
                while (_eventQueue.TryDequeue(out var evt))
                {
                    backup.Add(evt);
                }
                PlayerPrefs.SetString("NoctuaEvents", JsonConvert.SerializeObject(backup));
                PlayerPrefs.Save();
                foreach (var evt in backup)
                {
                    _eventQueue.Enqueue(evt);
                }

                try
                {
                    var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/events")
                        .WithHeader("X-CLIENT-ID", _config.ClientId)
                        .WithHeader("X-DEVICE-ID", _deviceId)
                        .WithNdjsonBody(events);

                    await request.Send<EventResponse>();
                    _log.Info($"Sent {events.Count} events");

                    // TODO call nativePlugin.onOnline()
                }
                catch (Exception e)
                {
                    _log.Error($"Failed to send events");
                    // Re-enqueue all the events
                    foreach (var evt in events)
                    {
                        _eventQueue.Enqueue(evt);
                    }

                    // Append events to backup then save to PlayerPrefs
                    _log.Info($"Backup events to PlayerPrefs");
                    backup.AddRange(events);
                    PlayerPrefs.SetString("NoctuaEvents", JsonConvert.SerializeObject(backup));
                    PlayerPrefs.Save();

                    InternetChecker.CheckInternetConnection((isConnected) =>
                    {
                        if (isConnected)
                        {
                            // TODO call nativePlugin.onOnline()
                        } else {
                            // TODO call nativePlugin.onOffline()
                        }
                    });

                    await UniTask.Delay(1000, cancellationToken: token);
                }
            }
        }
    }
}