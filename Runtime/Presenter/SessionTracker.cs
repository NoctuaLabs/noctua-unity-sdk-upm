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
    /// <summary>
    /// Configuration for <see cref="SessionTracker"/> heartbeat and session timeout intervals.
    /// </summary>
    [Preserve]
    public class SessionTrackerConfig
    {
        /// <summary>Interval in milliseconds between session heartbeat events. Default is 60 seconds.</summary>
        public uint HeartbeatPeriodMs = 60_000;
        /// <summary>Duration in milliseconds after which a paused session is considered expired. Default is 5 minutes.</summary>
        public uint SessionTimeoutMs = 300_000;
    }

    /// <summary>
    /// Tracks user session lifecycle (start, pause, continue, heartbeat, end) and sends session events via an event sender.
    /// </summary>
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

        // Engagement time tracking (Firebase-like user_engagement)
        private readonly Stopwatch _foregroundStopwatch = new Stopwatch();
        private long _accumulatedEngagementMs;

        /// <summary>
        /// Initializes a new session tracker and starts the heartbeat loop.
        /// </summary>
        /// <param name="config">Heartbeat and timeout configuration.</param>
        /// <param name="eventSender">Event sender for dispatching session events.</param>
        /// <param name="remoteFeatureFlags">Optional remote feature flags (e.g., flush-on-end toggle).</param>
        public SessionTracker(SessionTrackerConfig config, IEventSender eventSender, Dictionary<string, bool> remoteFeatureFlags = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _eventSender = eventSender ?? throw new ArgumentNullException(nameof(eventSender));

            _remoteFeatureFlags = remoteFeatureFlags ?? new Dictionary<string, bool>();
        
            _cancelHeartbeatSource = new CancellationTokenSource();
            _pauseStatus = true;
            _heartbeatTask = UniTask.Create(RunHeartbeat, _cancelHeartbeatSource.Token);
        }
        
        /// <summary>
        /// Harvests accumulated foreground time and sends a user_engagement event with engagement_time_msec.
        /// Resets the accumulator after sending. Skips if no foreground time was accumulated.
        /// </summary>
        private void SendUserEngagementEvent()
        {
            var currentMs = _foregroundStopwatch.ElapsedMilliseconds;
            _foregroundStopwatch.Reset();

            if (!_pauseStatus)
            {
                _foregroundStopwatch.Start();
            }

            var totalMs = _accumulatedEngagementMs + currentMs;
            _accumulatedEngagementMs = 0;

            if (totalMs <= 0) return;

            _log.Info($"[Session Tracker] Sending noctua_user_engagement: engagement_time_msec={totalMs}");
            _eventSender.Send("noctua_user_engagement", new Dictionary<string, IConvertible>
            {
                { "engagement_time_msec", totalMs }
            });
        }

        /// <summary>
        /// Handles application pause/resume transitions, sending session_pause, session_continue, or session_start events as appropriate.
        /// </summary>
        /// <param name="pauseStatus">True when the application is pausing; false when resuming.</param>
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
                _foregroundStopwatch.Stop();
                SendUserEngagementEvent();

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

                _accumulatedEngagementMs = 0;
                _foregroundStopwatch.Reset();

                _sessionId = null;
            }

            if (_sessionId != null)
            {
            	_log.Info($"[Session Tracker] Application unpaused, resume session");
                _eventSender.Send("session_continue");
                _foregroundStopwatch.Start();
            }
            else
            {
            	_log.Info($"[Session Tracker] Application unpaused, start a new session");
                _sessionId = Guid.NewGuid().ToString();
                ExperimentManager.SetSessionId(_sessionId);
                _eventSender.Send("session_start");
                _foregroundStopwatch.Start();
            }
        }
        
        /// <summary>
        /// Ends the current session, sends a session_end event, cancels the heartbeat loop, and optionally flushes pending events.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _cancelHeartbeatSource.Cancel();
            _disposed = true;

            // Send final engagement time before session_end
            _foregroundStopwatch.Stop();
            SendUserEngagementEvent();

            // Send session_end so it gets persisted to local storage.
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

                SendUserEngagementEvent();
                _eventSender.Send("session_heartbeat");
                _nextHeartbeat = DateTime.UtcNow.AddMilliseconds(_config.HeartbeatPeriodMs);
            }
        }
    }
}
