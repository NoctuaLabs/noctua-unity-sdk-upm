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
    /// Also persists session state to PlayerPrefs so that orphaned sessions (terminated by SIGKILL before Dispose runs)
    /// can be recovered and their end-of-session events sent on the next app launch.
    /// </summary>
    public class SessionTracker : IDisposable
    {
        // PlayerPrefs keys for crash-recovery of orphaned sessions.
        // Written on session_start, heartbeat, and pause; cleared on clean Dispose() or session timeout.
        private const string KeyOrphanedSessionId          = "NoctuaOrphanedSessionId";
        private const string KeyOrphanedSessionCumulativeMs = "NoctuaOrphanedSessionCumulativeMs";
        private const string KeyOrphanedSessionLastTimestamp = "NoctuaOrphanedSessionLastTimestamp";

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
        private long _cumulativeSessionEngagementMs;

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
        /// Harvests accumulated foreground time and sends a user_engagement event with engagement_time_msec and lifecycle.
        /// Resets the accumulator after sending. Skips if no foreground time was accumulated (except for lifecycle=start).
        /// </summary>
        private void SendUserEngagementEvent(string lifecycle)
        {
            var currentMs = _foregroundStopwatch.ElapsedMilliseconds;
            _foregroundStopwatch.Reset();

            if (!_pauseStatus)
            {
                _foregroundStopwatch.Start();
            }

            var totalMs = _accumulatedEngagementMs + currentMs;
            _accumulatedEngagementMs = 0;

            if (totalMs <= 0 && lifecycle != "start") return;

            _cumulativeSessionEngagementMs += totalMs;

            _log.Info($"[Session Tracker] Sending noctua_user_engagement: engagement_time_msec={totalMs}, lifecycle={lifecycle}");
            _eventSender.Send("noctua_user_engagement", new Dictionary<string, IConvertible>
            {
                { "engagement_time_msec", totalMs },
                { "lifecycle", lifecycle }
            });
        }

        /// <summary>
        /// Sends a cumulative per-session engagement event. Skips if no foreground time was accumulated.
        /// </summary>
        private void SendPerSessionEngagementEvent()
        {
            if (_cumulativeSessionEngagementMs <= 0) return;

            _log.Info($"[Session Tracker] Sending noctua_user_engagement_per_session: engagement_time_msec={_cumulativeSessionEngagementMs}");
            _eventSender.Send("noctua_user_engagement_per_session", new Dictionary<string, IConvertible>
            {
                { "engagement_time_msec", _cumulativeSessionEngagementMs }
            });
        }

        /// <summary>
        /// Persists current session state to PlayerPrefs so it can be recovered on next launch if the process is killed.
        /// Must be called from the main thread (PlayerPrefs is not thread-safe).
        /// </summary>
        private void SaveSessionState()
        {
            if (_sessionId == null) return;

            PlayerPrefs.SetString(KeyOrphanedSessionId, _sessionId);
            PlayerPrefs.SetString(KeyOrphanedSessionCumulativeMs, _cumulativeSessionEngagementMs.ToString());
            PlayerPrefs.SetString(KeyOrphanedSessionLastTimestamp, DateTime.UtcNow.ToString("O"));
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Removes orphaned session state from PlayerPrefs. Call on clean exit paths (Dispose, session timeout)
        /// to prevent spurious recovery on the next launch.
        /// </summary>
        private void ClearSessionState()
        {
            PlayerPrefs.DeleteKey(KeyOrphanedSessionId);
            PlayerPrefs.DeleteKey(KeyOrphanedSessionCumulativeMs);
            PlayerPrefs.DeleteKey(KeyOrphanedSessionLastTimestamp);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Checks PlayerPrefs for an orphaned session left by a previous process that was force-killed.
        /// If found, sends the missing end-of-session events (noctua_user_engagement, per_session, session_end)
        /// tagged with the old session_id, then clears the stored state.
        /// Must be called before the new session_start is sent.
        /// </summary>
        private void RecoverOrphanedSession()
        {
            var savedSessionId = PlayerPrefs.GetString(KeyOrphanedSessionId, null);
            if (string.IsNullOrEmpty(savedSessionId)) return;

            if (!long.TryParse(PlayerPrefs.GetString(KeyOrphanedSessionCumulativeMs, "0"), out var cumulativeMs))
            {
                cumulativeMs = 0;
            }

            _log.Info($"[Session Tracker] Recovering orphaned session {savedSessionId}, cumulativeMs={cumulativeMs}");

            // Tag recovery events with the old session_id.
            // session_id is also baked into each data dict so the async enrichment
            // task in EventSender.Send() picks up the correct value even after
            // SetProperties(null) resets the shared field before the task runs.
            _eventSender.SetProperties(sessionId: savedSessionId);

            if (cumulativeMs > 0)
            {
                _eventSender.Send("noctua_user_engagement", new Dictionary<string, IConvertible>
                {
                    { "engagement_time_msec", cumulativeMs },
                    { "lifecycle", "end" },
                    { "session_id", savedSessionId }
                });

                _eventSender.Send("noctua_user_engagement_per_session", new Dictionary<string, IConvertible>
                {
                    { "engagement_time_msec", cumulativeMs },
                    { "session_id", savedSessionId }
                });
            }

            _eventSender.Send("session_end", new Dictionary<string, IConvertible>
            {
                { "session_id", savedSessionId }
            });

            // Clear so we don't recover the same session twice
            ClearSessionState();

            // Reset session_id — the new session will assign its own
            _eventSender.SetProperties(sessionId: null);
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
                SendUserEngagementEvent("pause");

                _eventSender.Send("session_pause");
                _log.Info($"[Session Tracker] Application paused, let's flush events");
                _eventSender.Flush();
                _nextSessionTimeout = DateTime.UtcNow.AddMilliseconds(_config.SessionTimeoutMs);

                // Persist state so a crash during background doesn't orphan this session
                SaveSessionState();

                return;
            }

            if (_sessionId != null && DateTime.UtcNow >= _nextSessionTimeout)
            {
                // Send per-session event WHILE old session_id is still active
                SendPerSessionEngagementEvent();

                _accumulatedEngagementMs = 0;
                _cumulativeSessionEngagementMs = 0;
                _foregroundStopwatch.Reset();
                _sessionId = null;
                _eventSender.SetProperties(sessionId: null);

                // per_session was sent cleanly — no orphan to recover
                ClearSessionState();
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

                // Recover any session orphaned by a previous force-kill before starting the new one.
                // This runs here (not in Noctua.Initialization) to guarantee ordering:
                // recovery events fire before session_start, all on the main thread.
                RecoverOrphanedSession();

                _sessionId = Guid.NewGuid().ToString();
                _cumulativeSessionEngagementMs = 0;
                _eventSender.SetProperties(sessionId: _sessionId);
                _eventSender.Send("session_start");
                SendUserEngagementEvent("start");
                _foregroundStopwatch.Start();

                // Persist initial session state immediately after session_start
                SaveSessionState();
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

            // Clear orphan state — this is a clean exit; no recovery needed on next launch
            ClearSessionState();

            // Send final engagement time before session_end
            _foregroundStopwatch.Stop();
            SendUserEngagementEvent("end");
            SendPerSessionEngagementEvent();

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

                SendUserEngagementEvent("foreground");
                _eventSender.Send("session_heartbeat");
                _nextHeartbeat = DateTime.UtcNow.AddMilliseconds(_config.HeartbeatPeriodMs);

                // Save updated cumulative engagement time after each heartbeat.
                // Must switch to main thread — PlayerPrefs is not thread-safe.
                await UniTask.SwitchToMainThread(cancellationToken: token);
                SaveSessionState();
            }
        }
    }
}
