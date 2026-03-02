using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Scripting;
using Application = UnityEngine.Device.Application;
using Debug = UnityEngine.Debug;

namespace com.noctuagames.sdk.Events
{
    [Preserve]
    public class SessionTrackerConfig
    {
        public uint HeartbeatPeriodMs = 60_000;
        public uint SessionTimeoutMs = 300_000;
    }

    public class SessionTracker : IDisposable
    {
        private readonly SessionTrackerConfig _config;
        private readonly IEventSender _eventSender;
        private readonly UniTask _heartbeatTask;
        private readonly CancellationTokenSource _cancelHeartbeatSource;
        private  Dictionary<string, bool> _remoteFeatureFlags;
        private readonly ILogger _log = new NoctuaLogger(typeof(SessionTracker));

        
        private DateTime _nextHeartbeat;
        private DateTime _nextSessionTimeout;
        private bool _pauseStatus;
        private bool _disposed;
        
        private string _sessionId;

        public SessionTracker(SessionTrackerConfig config, IEventSender eventSender, Dictionary<string, bool> remoteFeatureFlags = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _eventSender = eventSender ?? throw new ArgumentNullException(nameof(eventSender));

            _remoteFeatureFlags = remoteFeatureFlags ?? new Dictionary<string, bool>();
        
            _cancelHeartbeatSource = new CancellationTokenSource();
            _pauseStatus = true;
            _heartbeatTask = UniTask.Create(RunHeartbeat, _cancelHeartbeatSource.Token);
        }
        
        public void OnApplicationPause(bool pauseStatus)
        {
            if (_pauseStatus == pauseStatus)
            {
                _log.Info($"[Session Tracker] Application pause status unchanged: {pauseStatus}");
                return;
            }
            
            _pauseStatus = pauseStatus;
            
            if (pauseStatus)
            {
                _eventSender.Send("session_pause");
                _log.Info($"[Session Tracker] Application paused, let's flush events");
                _eventSender.Flush();
                _nextSessionTimeout = DateTime.UtcNow.AddMilliseconds(_config.SessionTimeoutMs);
                
                return;
            }
            
            if (_sessionId != null && DateTime.UtcNow >= _nextSessionTimeout)
            {
                // Sending session_end here seems makes sense, but the last event is already session_pause
                // It also means we have to send two more events: session_continue and session_end with exact same
                // timestamp, which is weird and waste of resources
                
                _sessionId = null;
            }

            if (_sessionId != null)
            {
            	_log.Info($"[Session Tracker] Application unpaused, resume session");
                _eventSender.Send("session_continue");
            }
            else
            {
            	_log.Info($"[Session Tracker] Application unpaused, start a new session");
                _sessionId = Guid.NewGuid().ToString();
                ExperimentManager.SetSessionId(_sessionId);
                _eventSender.Send("session_start");
            }
        }
        
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _cancelHeartbeatSource.Cancel();
            _disposed = true;

            // Send session_end first so it gets persisted to local storage.
            // Even if the HTTP flush below is skipped, the event won't be lost —
            // it will be sent on next app launch.
            _eventSender.Send("session_end");

            // Guard: when called from the GC finalizer thread, Flush() uses
            // UniTask and UnityWebRequest which are main-thread-only and will
            // crash. Skip Flush — the session_end event above is already queued
            // for local storage and will be sent on next launch.
            if (Thread.CurrentThread.ManagedThreadId != 1)
            {
                return;
            }

            if (_remoteFeatureFlags.TryGetValue("sendEventsOnFlushEnabled", out var enabled) && enabled)
            {
                _eventSender.Flush();
            }
        }

        ~SessionTracker()
        {
            Dispose();
        }
        
        private async UniTask RunHeartbeat(CancellationToken token)
        {
            _nextHeartbeat = DateTime.UtcNow.AddMilliseconds(_config.HeartbeatPeriodMs);

            while (!token.IsCancellationRequested)
            {
                if (_pauseStatus || DateTime.UtcNow < _nextHeartbeat)
                {
                    await UniTask.Delay(100, cancellationToken: token);
                    
                    continue;
                }

                _eventSender.Send("session_heartbeat");
                _nextHeartbeat = DateTime.UtcNow.AddMilliseconds(_config.HeartbeatPeriodMs);
            }
        }
    }
}
