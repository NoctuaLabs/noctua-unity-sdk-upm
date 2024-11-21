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
        private readonly EventSender _eventSender;
        private readonly UniTask _heartbeatTask;
        private readonly CancellationTokenSource _cancelHeartbeatSource;
        
        private DateTime _nextHeartbeat;
        private DateTime _nextSessionTimeout;
        private bool _pauseStatus;
        private bool _disposed;
        
        private string _sessionId;

        public SessionTracker(SessionTrackerConfig config, EventSender eventSender)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _eventSender = eventSender ?? throw new ArgumentNullException(nameof(eventSender));
            
            _cancelHeartbeatSource = new CancellationTokenSource();
            _pauseStatus = true;
            _heartbeatTask = UniTask.Create(RunHeartbeat, _cancelHeartbeatSource.Token);
        }
        
        public void OnApplicationPause(bool pauseStatus)
        {
            if (_pauseStatus == pauseStatus)
            {
                return;
            }
            
            _pauseStatus = pauseStatus;
            
            if (pauseStatus)
            {
                _eventSender.Send("session_pause");
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
                _eventSender.Send("session_continue");
            }
            else
            {
                _sessionId = Guid.NewGuid().ToString();
                _eventSender.SetProperties(sessionId: _sessionId);
                _eventSender.Send("session_start");
            }
        }
        
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }
            
            _eventSender.Send("session_end");
            _eventSender.Flush();
            _cancelHeartbeatSource.Cancel();
            _disposed = true;
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