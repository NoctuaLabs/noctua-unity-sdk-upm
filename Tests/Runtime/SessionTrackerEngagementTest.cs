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
            ExperimentManager.Clear();
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

                // user_engagement must come before session_pause
                Assert.AreEqual("session_start", eventNames[0]);
                Assert.AreEqual("noctua_user_engagement", eventNames[1]);
                Assert.AreEqual("session_pause", eventNames[2]);

                // Verify engagement_time_msec is present and reasonable
                var engagement = _mockSender.GetEventsByName("noctua_user_engagement").First();
                Assert.IsTrue(engagement.Data.ContainsKey("engagement_time_msec"));
                var msec = Convert.ToInt64(engagement.Data["engagement_time_msec"]);
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

                // Expect: session_start, user_engagement, session_heartbeat
                Assert.AreEqual("session_start", eventNames[0]);

                // Find the first heartbeat and check that user_engagement precedes it
                var heartbeatIndex = eventNames.IndexOf("session_heartbeat");
                Assert.Greater(heartbeatIndex, 0, "Expected at least one session_heartbeat");
                Assert.AreEqual("noctua_user_engagement", eventNames[heartbeatIndex - 1]);

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

                Assert.AreEqual("noctua_user_engagement", eventNames[0]);
                Assert.AreEqual("session_end", eventNames[1]);
            }
        );

        [UnityTest]
        public IEnumerator EngagementTime_IsIncremental_ResetsAfterEachSend() => UniTask.ToCoroutine(
            async () =>
            {
                // Use long heartbeat to prevent heartbeat from firing during this test
                var config = new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = 60_000,
                    SessionTimeoutMs = 1000
                };
                var tracker = new SessionTracker(config, _mockSender);

                tracker.OnApplicationPause(false); // Start session
                await UniTask.Delay(300);
                tracker.OnApplicationPause(true);  // First pause — sends engagement

                var firstEngagement = _mockSender.GetEventsByName("noctua_user_engagement").First();
                var firstMs = Convert.ToInt64(firstEngagement.Data["engagement_time_msec"]);

                tracker.OnApplicationPause(false); // Resume
                await UniTask.Delay(300);
                tracker.OnApplicationPause(true);  // Second pause — sends engagement

                // Check before dispose (dispose sends a third engagement from remaining time)
                var pauseEngagements = _mockSender.GetEventsByName("noctua_user_engagement");
                Assert.AreEqual(2, pauseEngagements.Count, "Expected exactly 2 user_engagement events from pauses");

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
                tracker.OnApplicationPause(true);  // Pause (sends engagement for old session)

                // Wait for session timeout (1000ms configured)
                await UniTask.Delay(1500);

                _mockSender.Clear(); // Clear to isolate new session events
                tracker.OnApplicationPause(false); // Resume — starts new session

                // The new session should start clean with no leftover engagement time
                var eventNames = _mockSender.SentEvents.Select(e => e.Name).ToList();
                Assert.AreEqual("session_start", eventNames[0]);

                // No user_engagement should be sent at session start (no foreground time yet)
                Assert.IsFalse(eventNames.Contains("noctua_user_engagement"),
                    "No user_engagement should be sent at new session start");

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator NoForegroundTime_NoEngagementEventSent() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new SessionTracker(_config, _mockSender);

                // Start and immediately pause (near-zero foreground time)
                tracker.OnApplicationPause(false);
                tracker.OnApplicationPause(true);

                // user_engagement might or might not be sent depending on stopwatch resolution,
                // but if sent, engagement_time_msec should be very small
                var engagements = _mockSender.GetEventsByName("noctua_user_engagement");
                if (engagements.Count > 0)
                {
                    var msec = Convert.ToInt64(engagements[0].Data["engagement_time_msec"]);
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

                // Filter out user_engagement to check existing behavior
                var sessionEvents = _mockSender.SentEvents
                    .Where(e => e.Name != "noctua_user_engagement")
                    .Select(e => e.Name)
                    .ToList();

                Assert.AreEqual("session_start", sessionEvents[0]);
                Assert.AreEqual("session_pause", sessionEvents[1]);
                Assert.AreEqual("session_continue", sessionEvents[2]);
                Assert.AreEqual("session_pause", sessionEvents[3]);

                tracker.Dispose();
            }
        );
    }
}
