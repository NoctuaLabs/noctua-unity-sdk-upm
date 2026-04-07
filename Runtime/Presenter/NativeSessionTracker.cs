using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk.Events
{
    /// <summary>
    /// Tracks user engagement time driven by native platform lifecycle callbacks (Android Activity / iOS UIApplication).
    /// Sends <c>native_user_engagement</c> and <c>native_user_engagement_per_session</c> events.
    /// This is a parallel tracker to <see cref="SessionTracker"/>, which is driven by Unity's OnApplicationPause.
    /// </summary>
    public class NativeSessionTracker : IDisposable
    {
        private readonly SessionTrackerConfig _config;
        private readonly IEventSender _eventSender;
        private readonly UniTask _heartbeatTask;
        private readonly CancellationTokenSource _cancelHeartbeatSource;
        private readonly ILogger _log = new NoctuaLogger(typeof(NativeSessionTracker));

        private DateTime _nextHeartbeat;
        private DateTime _nextSessionTimeout;
        private bool _pauseStatus;
        private bool _disposed;
        private bool _started;

        // Engagement time tracking
        private readonly Stopwatch _foregroundStopwatch = new Stopwatch();
        private long _accumulatedEngagementMs;
        private long _cumulativeSessionEngagementMs;

        // Read-only stats for test button
        internal long CumulativeSessionEngagementMs => _cumulativeSessionEngagementMs;
        internal bool IsInForeground => !_pauseStatus;
        internal long CurrentForegroundMs => _foregroundStopwatch.ElapsedMilliseconds + _accumulatedEngagementMs;

        public NativeSessionTracker(SessionTrackerConfig config, IEventSender eventSender,
            Dictionary<string, bool> remoteFeatureFlags = null)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _eventSender = eventSender ?? throw new ArgumentNullException(nameof(eventSender));
            _cancelHeartbeatSource = new CancellationTokenSource();
            _pauseStatus = true;
            _heartbeatTask = UniTask.Create(RunHeartbeat, _cancelHeartbeatSource.Token);
        }

        /// <summary>
        /// Called when native platform reports the app has resumed (become active).
        /// </summary>
        internal void OnNativeResume()
        {
            if (!_pauseStatus)
            {
                _log.Info("[Native Session Tracker] Already in foreground, ignoring duplicate resume");
                return;
            }

            _pauseStatus = false;

            // Check session timeout
            if (_started && DateTime.UtcNow >= _nextSessionTimeout)
            {
                SendPerSessionEngagementEvent();
                _accumulatedEngagementMs = 0;
                _cumulativeSessionEngagementMs = 0;
                _foregroundStopwatch.Reset();
                _started = false;
            }

            if (!_started)
            {
                _log.Info("[Native Session Tracker] First resume, sending lifecycle=start");
                _started = true;
                _cumulativeSessionEngagementMs = 0;
                SendNativeUserEngagementEvent("start");
                _foregroundStopwatch.Start();
            }
            else
            {
                _log.Info("[Native Session Tracker] Resume from pause");
                _foregroundStopwatch.Start();
            }
        }

        /// <summary>
        /// Called when native platform reports the app has paused (resigned active).
        /// </summary>
        internal void OnNativePause()
        {
            if (_pauseStatus)
            {
                _log.Info("[Native Session Tracker] Already paused, ignoring duplicate pause");
                return;
            }

            _pauseStatus = true;
            _foregroundStopwatch.Stop();
            SendNativeUserEngagementEvent("pause");
            _nextSessionTimeout = DateTime.UtcNow.AddMilliseconds(_config.SessionTimeoutMs);
        }

        private void SendNativeUserEngagementEvent(string lifecycle)
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

            _log.Info($"[Native Session Tracker] Sending native_user_engagement: engagement_time_msec={totalMs}, lifecycle={lifecycle}");
            _eventSender.Send("native_user_engagement", new Dictionary<string, IConvertible>
            {
                { "engagement_time_msec", totalMs },
                { "lifecycle", lifecycle }
            });
        }

        private void SendPerSessionEngagementEvent()
        {
            if (_cumulativeSessionEngagementMs <= 0) return;

            _log.Info($"[Native Session Tracker] Sending native_user_engagement_per_session: engagement_time_msec={_cumulativeSessionEngagementMs}");
            _eventSender.Send("native_user_engagement_per_session", new Dictionary<string, IConvertible>
            {
                { "engagement_time_msec", _cumulativeSessionEngagementMs }
            });
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cancelHeartbeatSource.Cancel();
            _disposed = true;

            _foregroundStopwatch.Stop();
            SendNativeUserEngagementEvent("end");
            SendPerSessionEngagementEvent();
        }

        ~NativeSessionTracker()
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

                SendNativeUserEngagementEvent("foreground");
                _nextHeartbeat = DateTime.UtcNow.AddMilliseconds(_config.HeartbeatPeriodMs);
            }
        }
    }
}
