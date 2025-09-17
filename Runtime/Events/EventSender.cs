﻿using System;
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
        public int CycleDelay = 5000; // 5 sec, in ms
        public FirebaseConfig FirebaseConfig = new FirebaseConfig();
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
        private List<Dictionary<string, IConvertible>> _eventQueue;
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
        private readonly object _queueLock = new();

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
            var eventsJson = PlayerPrefs.GetString("NoctuaEvents", "[]");
            if (eventsJson == null)
            {
                eventsJson = "[]";
            }

            if (eventsJson.Length > 800000) // Around 1000 events
            {
                _log.Info("NoctuaEvents is too large, clearing it");
                PlayerPrefs.SetString("NoctuaEvents", "[]");
                PlayerPrefs.Save();
                eventsJson = "[]";
            }
            

            // Try to parse into IConvertible first because it is
            // the native type of the queue.
            // There will be nested try catch to make it safe.
            var events = new List<Dictionary<string, IConvertible>>();
            //_log.Debug(eventsJson);
            try
            {
                events = JsonConvert.DeserializeObject<List<Dictionary<string, IConvertible>>>(eventsJson);
            }
            catch (Exception e)
            {
                events = new List<Dictionary<string, IConvertible>>();
                _log.Error($"Failed to load events from PlayerPrefs: {e.Message}.");

                // If fail, try to parse to object.
                // IConvertible cannot parse null value from JSON.
                // Load from PlayerPrefs and re-enqueue them all
                _log.Info("Try to parse NoctuaEvents with object type");
                var objects = new List<Dictionary<string, object>>();
                try
                {
                    objects = JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(eventsJson);
                }
                catch (Exception e2)
                {
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
                    }

                    // Per object iteration, add the dict to events list
                    events.Add(dict);
                }
            }
            if (events == null)
            {
                events = JsonConvert.DeserializeObject<List<Dictionary<string, IConvertible>>>(eventsJson);
            }
            _log.Info($"Total loaded events from PlayerPrefs: {events.Count}");

            lock (_queueLock)
            {
                _log.Debug("Locking the event queue to prevent concurrent access");
                _eventQueue = new List<Dictionary<string, IConvertible>>(events);
            }

            _log.Info($"Total loaded events from PlayerPrefs: {_eventQueue.Count}");
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

                #if UNITY_ANDROID
                if (!_config.FirebaseConfig.Android.CustomEventDisabled)
                {
                    var firebaseSessionId = await Noctua.GetFirebaseAnalyticsSessionID();
                    var firebaseInstallationId = await Noctua.GetFirebaseInstallationID();

                    data.TryAdd("firebase_analytics_session_id", firebaseSessionId);
                    data.TryAdd("firebase_installation_id", firebaseInstallationId);
                }
                #elif UNITY_IOS
                if (!_config.FirebaseConfig.Ios.CustomEventDisabled)
                {
                    var firebaseSessionId = await Noctua.GetFirebaseAnalyticsSessionID();
                    var firebaseInstallationId = await Noctua.GetFirebaseInstallationID();

                    data.TryAdd("firebase_analytics_session_id", firebaseSessionId);
                    data.TryAdd("firebase_installation_id", firebaseInstallationId);
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
                if (_sessionId != null) data.TryAdd("session_id", _sessionId);
                if (_uniqueId != null) data.TryAdd("unique_id", _uniqueId);

                _log.Info($"queued event '{LastEventTime:O}|{name}|{_deviceId}|{_sessionId}|{_userId}|{_playerId}'");

                lock (_queueLock)
                {
                    _eventQueue.Add(data);

                    PlayerPrefs.SetString("NoctuaEvents", JsonConvert.SerializeObject(_eventQueue));
                    PlayerPrefs.Save();
                    _log.Info($"{name} added to the queue. Current total event in queue: {_eventQueue.Count}");
                }
              
                // This check is used to maintain the offline state more frequent to update.
                // This also prevent "offline" event flooding the queue
                // by not sending another "offline" event if the event name is "offline"
                if (data.TryGetValue("event_name", out var eventName) && eventName.ToString() != "offline")
                {
                    Noctua.IsOfflineAsync().ContinueWith((isOffline) =>
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
            // where the HTTP request cause crash when the app is trying to quit
            _log.Debug("On Flush called on IOS. Abort to avoid crash");
            return;

            // No need to backup to PlayerPrefs. The latest backup from Send() is already sufficient
#endif

            _log.Debug("On Flush called. " + $"Current total event in queue: {_eventQueue.Count}");

            List<Dictionary<string, IConvertible>> snapshot;
            lock (_queueLock)
            {
                snapshot = new List<Dictionary<string, IConvertible>>(_eventQueue);
            }

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
                    PlayerPrefs.SetString("NoctuaEvents", "[]");
                    PlayerPrefs.Save();

                    lock (_queueLock)
                    {
                        // Clear the event queue
                        _eventQueue.Clear();
                    }
                    _log.Info($"Sent {_eventQueue.Count} events. PlayerPrefs cleared.");
                }
                catch (Exception e)
                {
                    // No need to backup to PlayerPrefs. The latest backup from Send() is already sufficient.
                    // No need to re-enqueue because the clearing queue part is on the success branch above
                    // Simply print the error as warning
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
                while (_eventQueue.Count == 0)
                {
                    await UniTask.Delay(1000, cancellationToken: token);
                }
                var nextBatchSchedule = DateTime.UtcNow.AddMilliseconds(_config.BatchPeriodMs);
                // If the queue length is less than batch size
                while (_eventQueue.Count < _config.BatchSize &&
                // or it is not reached the next batch schedule yet
                DateTime.UtcNow < nextBatchSchedule)
                {
                    // Then wait for another 1 sec.
                    await UniTask.Delay(1000, cancellationToken: token);
                }

                if (_eventQueue.Count == 0)
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

                    _log.Info($"Device is offline, continue to next cycle");
                    continue;
                }
                
                Noctua.OnOnline();

                // Dequeue to be sent to server
                var events = new List<Dictionary<string, IConvertible>>();
                lock (_queueLock)
                {
                    if (_eventQueue.Count <= _config.MaxBatchSize)
                    {
                        events = new List<Dictionary<string, IConvertible>>(_eventQueue);
                        _eventQueue.Clear();
                    }
                    else
                    {
                        events = _eventQueue.GetRange(0, _config.MaxBatchSize);
                        _eventQueue.RemoveRange(0, _config.MaxBatchSize);
                    }
                }
               

                _log.Info($"Batch size: {_config.MaxBatchSize}, events to be send: {events.Count}, events in the queue: {_eventQueue.Count}");

                try
                {
                    var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/events")
                        .WithHeader("X-CLIENT-ID", _config.ClientId)
                        .WithHeader("X-DEVICE-ID", SanitizeHeaderValue(_deviceId))
                        .WithNdjsonBody(events);

                    await request.Send<EventResponse>();
                    _log.Info($"Sent {events.Count} events. Events in queue: {_eventQueue.Count}");
                }
                catch (Exception e)
                {
                    _log.Error($"Failed to send events to server: {e.Message}");
                    lock (_queueLock)
                    {
                        // If the request failed, we need to re-enqueue the events back to the queue
                        _log.Info("Re-enqueueing events back to the queue due to failure");
                         // Re-enqueue all the events
                        _eventQueue.AddRange(events);
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