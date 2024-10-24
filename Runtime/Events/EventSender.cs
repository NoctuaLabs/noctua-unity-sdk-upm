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
using Debug = UnityEngine.Debug;

namespace com.noctuagames.sdk.Events
{
    [Preserve]
    public class EventSenderConfig
    {
        public string BaseUrl;
        public string ClientId;
        public string BundleId = Application.identifier;
        public uint BatchingNumberThreshold = 20;
        public uint BatchingTimoutMs = 300_000;
    }
    
    [Preserve]
    public class EventResponse
    {
        [JsonProperty("message")]
        public string Message;
    }

    public class EventSender : IDisposable
    {
        private readonly ILogger _log = new NoctuaUnityDebugLogger();
        private readonly EventSenderConfig _config;
        private readonly NoctuaLocale _locale;
        private readonly Queue<Dictionary<string, IConvertible>> _eventQueue = new();
        private readonly UniTask _sendTask;
        private readonly CancellationTokenSource _cancelSendSource;
        private readonly DateTime _start;
        private readonly string _uniqueId;
        private readonly string _deviceId;

        private long? _userId;
        private long? _playerId;
        private long? _gameId;
        private long? _gamePlatformId;
        private string _sessionId;
        
        public void SetUser(long? userId, long? playerId)
        {
            _userId = userId;
            _playerId = playerId;
        }

        public void SetFields(
            long? userId = null,
            long? playerId = null,
            long? gameId = null,
            long? gamePlatformId = null,
            string sessionId = null
        )
        {
            _userId = userId;
            _playerId = playerId;
            _gameId = gameId;
            _gamePlatformId = gamePlatformId;
            _sessionId = sessionId;
            
            _log.Log($"Setting fields: " +
                $"userId={userId}, " +
                $"playerId={playerId}, " +
                $"gameId={gameId}, " +
                $"gamePlatformId={gamePlatformId}, " +
                $"sessionId={sessionId}"
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
            
            if (_config.BatchingNumberThreshold <= 0)
            {
                throw new ArgumentException("Batching number threshold must be greater than 0", nameof(config));
            }
            
            if (_config.BatchingTimoutMs <= 0)
            {
                throw new ArgumentException("Batching timeout seconds must be greater than 0", nameof(config));
            }
            
            _locale = locale ?? throw new ArgumentNullException(nameof(locale));
            _start = DateTime.UtcNow - TimeSpan.FromSeconds(Stopwatch.GetTimestamp() / (double) Stopwatch.Frequency);
            _cancelSendSource = new CancellationTokenSource();
            _sendTask = UniTask.Create(async () => await SendEvents(_cancelSendSource.Token));
            
            _deviceId = SystemInfo.deviceUniqueIdentifier;

#if UNITY_ANDROID && !UNITY_EDITOR
                _uniqueId = GetGoogleAdId();
#elif UNITY_IOS && !UNITY_EDITOR
                _uniqueId = UnityEngine.iOS.Device.vendorIdentifier;
#else
                _uniqueId = null;
#endif
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
            
            data.TryAdd("event_name", name);
            data.TryAdd("sdk_version", Assembly.GetExecutingAssembly().GetName().Version.ToString());
            data.TryAdd("device_id", _deviceId);
            data.TryAdd("device_os_version", SystemInfo.operatingSystem);
            data.TryAdd("device_os", SystemInfo.operatingSystemFamily.ToString());
            data.TryAdd("device_type", SystemInfo.deviceType.ToString());
            data.TryAdd("device_model", SystemInfo.deviceModel);
            data.TryAdd("bundle_id", _config.BundleId);
            data.TryAdd("game_version", Application.version);
            data.TryAdd("country", _locale.GetCountry());

            var timestamp = _start.AddSeconds(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency).ToString("o");
            data.TryAdd("timestamp", timestamp);
            
            if (_userId   != null) data.TryAdd("user_id", _userId);
            if (_playerId != null) data.TryAdd("player_id", _playerId);
            if (_gameId != null) data.TryAdd("game_id", _gameId);
            if (_gamePlatformId != null) data.TryAdd("game_platform_id", _gamePlatformId);
            if (_sessionId != null) data.TryAdd("session_id", _sessionId);
            if (_uniqueId != null) data.TryAdd("unique_id", _uniqueId);
            
            _log.Log($"Sending event {name} with data: {JsonConvert.SerializeObject(data)}");
            
            _eventQueue.Enqueue(data);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static string GetGoogleAdId()
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
                Debug.Log("Failed to get Google Advertising ID: " + e.Message);
            }
            
            return null;
        }
#endif
        
        public void Dispose()
        {
            _cancelSendSource.Cancel();
            _sendTask.GetAwaiter().GetResult();
        }
        
        private async UniTask SendEvents(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                while (_eventQueue.Count == 0)
                {
                    await UniTask.Delay(100, cancellationToken: token);
                }
                
                var batchTimeout = DateTime.UtcNow.AddMilliseconds(_config.BatchingTimoutMs);

                while (_eventQueue.Count < _config.BatchingNumberThreshold && DateTime.UtcNow < batchTimeout)
                {
                    await UniTask.Delay(100, cancellationToken: token);
                }

                var events = new List<Dictionary<string, IConvertible>>();

                while (_eventQueue.TryDequeue(out var evt))
                {
                    events.Add(evt);
                }
                
                var request = new HttpRequest(HttpMethod.Post, $"{_config.BaseUrl}/events")
                    .WithHeader("X-CLIENT-ID", _config.ClientId)
                    .WithHeader("X-DEVICE-ID", _deviceId)
                    .WithNdjsonBody(events);

                await request.Send<EventResponse>();
                
                _log.Log($"Sent {events.Count} events");
            }
        }
    }
}