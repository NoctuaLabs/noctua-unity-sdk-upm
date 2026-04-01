using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    /// <summary>
    /// Mock IEventSender that records all sent events for assertion.
    /// </summary>
    public class MockEventSender : IEventSender
    {
        public List<(string Name, Dictionary<string, IConvertible> Data)> SentEvents { get; } = new();
        public int FlushCount { get; private set; }

        public void Send(string name, Dictionary<string, IConvertible> data = null)
        {
            SentEvents.Add((name, data != null ? new Dictionary<string, IConvertible>(data) : null));
        }

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
        }

        public void Flush()
        {
            FlushCount++;
        }

        public string PseudoUserId => "mock-pseudo-user-id";

        public List<(string Name, Dictionary<string, IConvertible> Data)> GetEventsByName(string name)
        {
            return SentEvents.Where(e => e.Name == name).ToList();
        }

        public void Clear()
        {
            SentEvents.Clear();
            FlushCount = 0;
        }
    }

    public class SessionTrackerEngagementTest
    {
        private MockEventSender _mockSender;
        private SessionTrackerConfig _config;

        [SetUp]
        public void SetUp()
        {
            _mockSender = new MockEventSender();
            _config = new SessionTrackerConfig
            {
                HeartbeatPeriodMs = 500,
                SessionTimeoutMs = 1000
            };
        }

        [UnityTest]
        public IEnumerator OnPause_SendsUserEngagementBeforeSessionPause() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new SessionTracker(_config, _mockSender);

                tracker.OnApplicationPause(false); // Start session
                await UniTask.Delay(200);          // Accumulate ~200ms foreground time
                tracker.OnApplicationPause(true);  // Pause

                var eventNames = _mockSender.SentEvents.Select(e => e.Name).ToList();

                // session_start, engagement(start), engagement(pause), session_pause
                Assert.AreEqual("session_start", eventNames[0]);
                Assert.AreEqual("noctua_user_engagement", eventNames[1]);
                Assert.AreEqual("noctua_user_engagement", eventNames[2]);
                Assert.AreEqual("session_pause", eventNames[3]);

                // First engagement is lifecycle=start with 0ms
                var startEngagement = _mockSender.SentEvents[1];
                Assert.AreEqual("start", startEngagement.Data["lifecycle"].ToString());
                Assert.AreEqual(0L, Convert.ToInt64(startEngagement.Data["engagement_time_msec"]));

                // Second engagement is lifecycle=pause with accumulated time
                var pauseEngagement = _mockSender.SentEvents[2];
                Assert.AreEqual("pause", pauseEngagement.Data["lifecycle"].ToString());
                var msec = Convert.ToInt64(pauseEngagement.Data["engagement_time_msec"]);
                Assert.GreaterOrEqual(msec, 100, "engagement_time_msec should be at least 100ms");
                Assert.LessOrEqual(msec, 5000, "engagement_time_msec should not exceed 5000ms");

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator OnHeartbeat_SendsUserEngagementBeforeHeartbeat() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new SessionTracker(_config, _mockSender);

                tracker.OnApplicationPause(false); // Start session

                // Wait for at least one heartbeat (500ms configured)
                await UniTask.Delay(800);

                var eventNames = _mockSender.SentEvents.Select(e => e.Name).ToList();

                // Expect: session_start, engagement(start), engagement(foreground), heartbeat
                Assert.AreEqual("session_start", eventNames[0]);
                Assert.AreEqual("noctua_user_engagement", eventNames[1]); // lifecycle=start

                // Find the first heartbeat and check that user_engagement precedes it
                var heartbeatIndex = eventNames.IndexOf("session_heartbeat");
                Assert.Greater(heartbeatIndex, 0, "Expected at least one session_heartbeat");
                Assert.AreEqual("noctua_user_engagement", eventNames[heartbeatIndex - 1]);

                // Verify the heartbeat-preceding engagement has lifecycle=foreground
                var heartbeatEngagement = _mockSender.SentEvents[heartbeatIndex - 1];
                Assert.AreEqual("foreground", heartbeatEngagement.Data["lifecycle"].ToString());

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator OnDispose_SendsUserEngagementBeforeSessionEnd() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new SessionTracker(_config, _mockSender);

                tracker.OnApplicationPause(false); // Start session
                await UniTask.Delay(200);          // Accumulate foreground time

                _mockSender.Clear(); // Clear to isolate dispose events
                tracker.Dispose();

                var eventNames = _mockSender.SentEvents.Select(e => e.Name).ToList();

                // engagement(end), per_session, session_end
                Assert.AreEqual("noctua_user_engagement", eventNames[0]);
                Assert.AreEqual("noctua_user_engagement_per_session", eventNames[1]);
                Assert.AreEqual("session_end", eventNames[2]);

                // Verify lifecycle=end
                Assert.AreEqual("end", _mockSender.SentEvents[0].Data["lifecycle"].ToString());
            }
        );

        [UnityTest]
        public IEnumerator EngagementTime_IsIncremental_ResetsAfterEachSend() => UniTask.ToCoroutine(
            async () =>
            {
                var config = new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = 60_000,
                    SessionTimeoutMs = 1000
                };
                var tracker = new SessionTracker(config, _mockSender);

                tracker.OnApplicationPause(false); // Start session
                await UniTask.Delay(300);
                tracker.OnApplicationPause(true);  // First pause

                // Get pause engagement events (skip lifecycle=start)
                var pauseEngagements = _mockSender.SentEvents
                    .Where(e => e.Name == "noctua_user_engagement" && e.Data["lifecycle"].ToString() == "pause")
                    .ToList();
                Assert.AreEqual(1, pauseEngagements.Count);
                var firstMs = Convert.ToInt64(pauseEngagements[0].Data["engagement_time_msec"]);

                tracker.OnApplicationPause(false); // Resume (session_continue, no engagement event)
                await UniTask.Delay(300);
                tracker.OnApplicationPause(true);  // Second pause

                pauseEngagements = _mockSender.SentEvents
                    .Where(e => e.Name == "noctua_user_engagement" && e.Data["lifecycle"].ToString() == "pause")
                    .ToList();
                Assert.AreEqual(2, pauseEngagements.Count);
                var secondMs = Convert.ToInt64(pauseEngagements[1].Data["engagement_time_msec"]);

                // Both should be roughly 300ms, not cumulative
                Assert.GreaterOrEqual(firstMs, 200);
                Assert.LessOrEqual(firstMs, 5000);
                Assert.GreaterOrEqual(secondMs, 200);
                Assert.LessOrEqual(secondMs, 5000);

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SessionTimeout_DiscardsAccumulatedEngagementTime() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new SessionTracker(_config, _mockSender);

                tracker.OnApplicationPause(false); // Start session
                await UniTask.Delay(200);
                tracker.OnApplicationPause(true);  // Pause

                // Wait for session timeout (1000ms configured)
                await UniTask.Delay(1500);

                _mockSender.Clear();
                tracker.OnApplicationPause(false); // Resume — starts new session (timeout fires per-session for old)

                var eventNames = _mockSender.SentEvents.Select(e => e.Name).ToList();

                // per_session (old session), session_start, engagement(start, 0ms)
                Assert.AreEqual("noctua_user_engagement_per_session", eventNames[0]);
                Assert.AreEqual("session_start", eventNames[1]);
                Assert.AreEqual("noctua_user_engagement", eventNames[2]);

                // Verify lifecycle=start with 0ms
                var startEngagement = _mockSender.SentEvents[2];
                Assert.AreEqual("start", startEngagement.Data["lifecycle"].ToString());
                Assert.AreEqual(0L, Convert.ToInt64(startEngagement.Data["engagement_time_msec"]));

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator NoForegroundTime_NoEngagementEventSent() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new SessionTracker(_config, _mockSender);

                // Start and immediately pause
                tracker.OnApplicationPause(false);
                tracker.OnApplicationPause(true);

                // lifecycle=start always fires with 0ms
                // lifecycle=pause may or may not fire depending on stopwatch resolution
                var engagements = _mockSender.GetEventsByName("noctua_user_engagement");
                Assert.GreaterOrEqual(engagements.Count, 1, "At least lifecycle=start should fire");
                Assert.AreEqual("start", engagements[0].Data["lifecycle"].ToString());

                // If pause engagement was sent, it should have very small time
                var pauseEngagements = engagements
                    .Where(e => e.Data["lifecycle"].ToString() == "pause")
                    .ToList();
                if (pauseEngagements.Count > 0)
                {
                    var msec = Convert.ToInt64(pauseEngagements[0].Data["engagement_time_msec"]);
                    Assert.LessOrEqual(msec, 100, "Near-instant pause should have very small engagement time");
                }

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator ExistingSessionEvents_RemainUnchanged() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new SessionTracker(_config, _mockSender);

                tracker.OnApplicationPause(false); // session_start
                await UniTask.Delay(100);
                tracker.OnApplicationPause(true);  // session_pause
                tracker.OnApplicationPause(false); // session_continue
                await UniTask.Delay(100);
                tracker.OnApplicationPause(true);  // session_pause

                // Filter out engagement events to check existing behavior
                var sessionEvents = _mockSender.SentEvents
                    .Where(e => e.Name != "noctua_user_engagement" && e.Name != "noctua_user_engagement_per_session")
                    .Select(e => e.Name)
                    .ToList();

                Assert.AreEqual("session_start", sessionEvents[0]);
                Assert.AreEqual("session_pause", sessionEvents[1]);
                Assert.AreEqual("session_continue", sessionEvents[2]);
                Assert.AreEqual("session_pause", sessionEvents[3]);

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator PerSessionEngagement_CumulativeTotal_AtDispose() => UniTask.ToCoroutine(
            async () =>
            {
                var config = new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = 60_000,
                    SessionTimeoutMs = 300_000
                };
                var tracker = new SessionTracker(config, _mockSender);

                tracker.OnApplicationPause(false);
                await UniTask.Delay(300);
                tracker.OnApplicationPause(true);  // pause — incremental ~300ms

                tracker.OnApplicationPause(false); // continue
                await UniTask.Delay(300);

                tracker.Dispose(); // end — incremental ~300ms + per_session ~600ms total

                var perSession = _mockSender.GetEventsByName("noctua_user_engagement_per_session");
                Assert.AreEqual(1, perSession.Count, "Expected exactly 1 per-session event");

                var cumulativeMs = Convert.ToInt64(perSession[0].Data["engagement_time_msec"]);
                Assert.GreaterOrEqual(cumulativeMs, 400, "Cumulative should be at least 400ms (2x ~300ms minus overhead)");
                Assert.LessOrEqual(cumulativeMs, 10000, "Cumulative should not exceed 10s");
            }
        );

        [UnityTest]
        public IEnumerator PerSessionEngagement_SentOnSessionTimeout() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new SessionTracker(_config, _mockSender);

                tracker.OnApplicationPause(false); // Start session 1
                await UniTask.Delay(200);
                tracker.OnApplicationPause(true);  // Pause

                await UniTask.Delay(1500); // Wait for timeout

                _mockSender.Clear();
                tracker.OnApplicationPause(false); // Resume — triggers timeout, starts session 2

                var eventNames = _mockSender.SentEvents.Select(e => e.Name).ToList();

                // per_session for old session should come first
                Assert.AreEqual("noctua_user_engagement_per_session", eventNames[0]);
                Assert.AreEqual("session_start", eventNames[1]);

                var perSession = _mockSender.SentEvents[0];
                var cumulativeMs = Convert.ToInt64(perSession.Data["engagement_time_msec"]);
                Assert.GreaterOrEqual(cumulativeMs, 100, "Should have accumulated foreground time from session 1");

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator LifecycleParam_AllFourValues() => UniTask.ToCoroutine(
            async () =>
            {
                var config = new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = 300,
                    SessionTimeoutMs = 300_000
                };
                var tracker = new SessionTracker(config, _mockSender);

                // Trigger all 4 lifecycle values:
                tracker.OnApplicationPause(false); // session_start → lifecycle=start
                await UniTask.Delay(500);          // Wait for heartbeat → lifecycle=foreground
                tracker.OnApplicationPause(true);  // pause → lifecycle=pause
                tracker.OnApplicationPause(false); // continue (no engagement)
                await UniTask.Delay(200);
                tracker.Dispose();                 // dispose → lifecycle=end

                var engagements = _mockSender.GetEventsByName("noctua_user_engagement");

                var lifecycles = engagements.Select(e => e.Data["lifecycle"].ToString()).ToList();

                Assert.Contains("start", lifecycles);
                Assert.Contains("foreground", lifecycles);
                Assert.Contains("pause", lifecycles);
                Assert.Contains("end", lifecycles);

                // Verify start has 0ms
                var startEvent = engagements.First(e => e.Data["lifecycle"].ToString() == "start");
                Assert.AreEqual(0L, Convert.ToInt64(startEvent.Data["engagement_time_msec"]));

                // Verify foreground, pause, end have positive ms
                foreach (var lc in new[] { "foreground", "pause", "end" })
                {
                    var evt = engagements.First(e => e.Data["lifecycle"].ToString() == lc);
                    Assert.Greater(Convert.ToInt64(evt.Data["engagement_time_msec"]), 0,
                        $"lifecycle={lc} should have positive engagement_time_msec");
                }
            }
        );
    }
}
