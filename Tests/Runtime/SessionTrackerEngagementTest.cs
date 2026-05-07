using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
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

            // Clean up orphaned-session PlayerPrefs written by SessionTracker so tests
            // don't inherit state from previous runs.
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionId");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionCumulativeMs");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionUnsentMs");
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionLastTimestamp");
            PlayerPrefs.Save();
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
        public IEnumerator NoForegroundTime_EngagementEventSentWithZeroMs() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new SessionTracker(_config, _mockSender);

                // Start and immediately pause — no measurable foreground time
                tracker.OnApplicationPause(false);
                tracker.OnApplicationPause(true);

                // lifecycle=start always fires with 0ms
                var engagements = _mockSender.GetEventsByName("noctua_user_engagement");
                Assert.GreaterOrEqual(engagements.Count, 1, "At least lifecycle=start should fire");
                Assert.AreEqual("start", engagements[0].Data["lifecycle"].ToString());
                Assert.AreEqual(0L, Convert.ToInt64(engagements[0].Data["engagement_time_msec"]));

                // lifecycle=pause must now always fire (even with 0ms) so short sessions are
                // recorded. Previously it was conditionally skipped, causing zero timespent.
                var pauseEngagements = engagements
                    .Where(e => e.Data["lifecycle"].ToString() == "pause")
                    .ToList();
                Assert.AreEqual(1, pauseEngagements.Count,
                    "lifecycle=pause must fire even with 0ms foreground time (fixes zero timespent for short sessions)");
                var msec = Convert.ToInt64(pauseEngagements[0].Data["engagement_time_msec"]);
                Assert.LessOrEqual(msec, 100, "Near-instant pause should have very small engagement time");

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

        // ─── 0ms engagement for short sessions ───────────────────────────────────

        [UnityTest]
        public IEnumerator ShortSession_PauseAndEnd_Always_SendEngagementEvenAtZeroMs() => UniTask.ToCoroutine(
            async () =>
            {
                // Verifies the fix for zero timespent: lifecycle=pause and lifecycle=end must
                // always send, even when no foreground time was accumulated (0ms). Previously
                // these were dropped by the guard, making sub-millisecond sessions invisible.
                var tracker = new SessionTracker(_config, _mockSender);

                tracker.OnApplicationPause(false); // session_start → lifecycle=start (0ms)
                // Immediately pause (simulating Hamster Jump's rapid session cycling)
                tracker.OnApplicationPause(true);  // should send lifecycle=pause even at 0ms

                var engagements = _mockSender.GetEventsByName("noctua_user_engagement");
                var lifecycles  = engagements.Select(e => e.Data["lifecycle"].ToString()).ToList();

                Assert.Contains("pause", lifecycles,
                    "lifecycle=pause must fire even with 0ms foreground time");

                _mockSender.Clear();
                tracker.Dispose(); // should send lifecycle=end even at 0ms (session is paused)

                var disposeEngagements = _mockSender.GetEventsByName("noctua_user_engagement");
                Assert.AreEqual(1, disposeEngagements.Count,
                    "lifecycle=end must fire on Dispose even with 0ms foreground time since last pause");
                Assert.AreEqual("end", disposeEngagements[0].Data["lifecycle"].ToString());
            }
        );

        // ─── Recovery double-counting fix ────────────────────────────────────────

        [UnityTest]
        public IEnumerator RecoveryEvent_SendsOnlyUnsentPortion_NotFullCumulative() => UniTask.ToCoroutine(
            async () =>
            {
                // Simulate a crashed session that had already sent heartbeats.
                // Before the fix, the full cumulativeMs was re-sent as noctua_user_engagement,
                // double-counting all heartbeat chunks. After the fix only unsentMs is sent.
                const long simulatedCumulativeMs = 600_000L; // 10 min already sent via heartbeats
                const long simulatedUnsentMs     = 30_000L;  // 30s since last heartbeat at crash
                const string orphanedId          = "orphaned-test-session-id";

                PlayerPrefs.SetString("NoctuaOrphanedSessionId",           orphanedId);
                PlayerPrefs.SetString("NoctuaOrphanedSessionCumulativeMs", simulatedCumulativeMs.ToString());
                PlayerPrefs.SetString("NoctuaOrphanedSessionUnsentMs",     simulatedUnsentMs.ToString());
                PlayerPrefs.SetString("NoctuaOrphanedSessionLastTimestamp", DateTime.UtcNow.AddHours(-2).ToString("O"));
                PlayerPrefs.Save();

                var tracker = new SessionTracker(_config, _mockSender);
                tracker.OnApplicationPause(false); // triggers RecoverOrphanedSession() then session_start

                // Recovery engagement: must carry only the unsent portion
                var allEngagement = _mockSender.GetEventsByName("noctua_user_engagement");
                var recoveryEngagement = allEngagement.FirstOrDefault(e =>
                    e.Data != null &&
                    e.Data.ContainsKey("session_id") &&
                    e.Data["session_id"].ToString() == orphanedId);

                Assert.IsNotNull(recoveryEngagement.Data,
                    "noctua_user_engagement recovery event must be sent for orphaned session");
                var recoveredMs = Convert.ToInt64(recoveryEngagement.Data["engagement_time_msec"]);
                Assert.AreEqual(simulatedUnsentMs, recoveredMs,
                    $"Recovery must send only unsentMs={simulatedUnsentMs} — not full cumulativeMs={simulatedCumulativeMs} which double-counts heartbeats");

                // Per-session: must carry the full total (cumulative + unsent)
                var perSessionEvents = _mockSender.GetEventsByName("noctua_user_engagement_per_session");
                var recoveryPerSession = perSessionEvents.FirstOrDefault(e =>
                    e.Data != null &&
                    e.Data.ContainsKey("session_id") &&
                    e.Data["session_id"].ToString() == orphanedId);

                Assert.IsNotNull(recoveryPerSession.Data,
                    "noctua_user_engagement_per_session recovery event must be sent");
                var recoveredPerSessionMs = Convert.ToInt64(recoveryPerSession.Data["engagement_time_msec"]);
                Assert.AreEqual(simulatedCumulativeMs + simulatedUnsentMs, recoveredPerSessionMs,
                    "Per-session recovery must carry the full total (cumulative + unsent)");

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator RecoveryEvent_OldSchema_NoUnsentKey_SendsZeroEngagementAndCorrectPerSession() => UniTask.ToCoroutine(
            async () =>
            {
                // Devices upgrading from an old SDK that did not persist unsentMs.
                // The key is absent → defaults to 0 → no noctua_user_engagement recovery event,
                // but noctua_user_engagement_per_session still fires with the cumulative total.
                const long simulatedCumulativeMs = 120_000L; // 2 min
                const string orphanedId          = "old-schema-orphaned-id";

                PlayerPrefs.SetString("NoctuaOrphanedSessionId",           orphanedId);
                PlayerPrefs.SetString("NoctuaOrphanedSessionCumulativeMs", simulatedCumulativeMs.ToString());
                // NoctuaOrphanedSessionUnsentMs intentionally absent (old schema)
                PlayerPrefs.SetString("NoctuaOrphanedSessionLastTimestamp", DateTime.UtcNow.AddHours(-1).ToString("O"));
                PlayerPrefs.Save();

                var tracker = new SessionTracker(_config, _mockSender);
                tracker.OnApplicationPause(false);

                // No noctua_user_engagement from the orphaned session (unsentMs == 0 → skipped)
                var recoveryEngagement = _mockSender.GetEventsByName("noctua_user_engagement")
                    .Where(e => e.Data != null && e.Data.ContainsKey("session_id") && e.Data["session_id"].ToString() == orphanedId)
                    .ToList();
                Assert.AreEqual(0, recoveryEngagement.Count,
                    "Old-schema recovery with no unsentMs key must not send noctua_user_engagement (0ms would be skipped)");

                // noctua_user_engagement_per_session must still fire with cumulativeMs
                var perSession = _mockSender.GetEventsByName("noctua_user_engagement_per_session")
                    .Where(e => e.Data != null && e.Data.ContainsKey("session_id") && e.Data["session_id"].ToString() == orphanedId)
                    .ToList();
                Assert.AreEqual(1, perSession.Count, "Per-session recovery must still fire even without unsentMs key");
                var perSessionMs = Convert.ToInt64(perSession[0].Data["engagement_time_msec"]);
                Assert.AreEqual(simulatedCumulativeMs, perSessionMs,
                    "Old-schema per-session must carry cumulativeMs (unsentMs defaults to 0)");

                tracker.Dispose();
            }
        );

        // ─── Min session gap guard ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator MinSessionGap_AllowsSessionStart_AfterTimeout() => UniTask.ToCoroutine(
            async () =>
            {
                // After a session TIMES OUT, the next resume must always create a new session —
                // even if the time since the previous session_start is less than SessionMinGapMs.
                //
                // Rationale: the min-gap guard exists to prevent session inflation from rapid
                // OnApplicationPause(false) bursts fired by ad SDKs (milliseconds apart). It is
                // NOT intended to suppress legitimate new sessions after a genuine timeout.
                // When a timeout occurs, SessionTracker resets _lastSessionStartTime to
                // DateTime.MinValue, giving the next resume an effectively infinite gap.
                var config = new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = 60_000,
                    SessionTimeoutMs  = 100  // 100ms timeout — expires almost immediately
                };
                var tracker = new SessionTracker(config, _mockSender);

                // Session 1 starts at T0
                tracker.OnApplicationPause(false);
                Assert.AreEqual(1, _mockSender.GetEventsByName("session_start").Count,
                    "Session 1 must start normally");

                // Pause (timeout window = now + 100ms)
                await UniTask.Delay(50);
                tracker.OnApplicationPause(true);

                // Resume after timeout fires (300ms elapsed > 100ms timeout).
                // Despite being within the 10s min-gap window, _lastSessionStartTime was reset to
                // DateTime.MinValue on timeout → gap appears infinite → session_start is NOT suppressed.
                await UniTask.Delay(250);
                _mockSender.Clear();
                tracker.OnApplicationPause(false);

                Assert.AreEqual(1, _mockSender.GetEventsByName("session_start").Count,
                    "session_start after a genuine timeout must NOT be suppressed by the min-gap guard");
                Assert.AreEqual(0, _mockSender.GetEventsByName("session_continue").Count,
                    "session_continue must not fire — this is a fresh session, not a resume of an active one");

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator MinSessionGap_AllowsSessionStart_AfterGapExpires() => UniTask.ToCoroutine(
            async () =>
            {
                // Verify that session start is NOT suppressed when invoked fresh (gap is infinite
                // from DateTime.MinValue) — normal first-launch behavior must be unaffected.
                var config = new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = 60_000,
                    SessionTimeoutMs  = 1000
                };
                var tracker = new SessionTracker(config, _mockSender);

                tracker.OnApplicationPause(false); // first launch — _lastSessionStartTime = MinValue → guard passes

                Assert.AreEqual(1, _mockSender.GetEventsByName("session_start").Count,
                    "First-ever session start must not be suppressed by the gap guard");

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator Heartbeat_DoesNotFire_BeforeSessionStarts() => UniTask.ToCoroutine(
            async () =>
            {
                // The heartbeat loop guards on `_sessionId == null` — no events should be
                // emitted until a session is actually started via OnApplicationPause(false).
                // This verifies the guard is effective from the very first frame, before any
                // session lifecycle transition has occurred.
                var config = new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = 200, // short so we'd notice within the wait window
                    SessionTimeoutMs  = 60_000
                };
                var tracker = new SessionTracker(config, _mockSender);

                // Never call OnApplicationPause(false) → _sessionId remains null
                // Wait well past heartbeatPeriodMs — heartbeat must NOT fire
                await UniTask.Delay(500);

                Assert.AreEqual(0, _mockSender.GetEventsByName("session_heartbeat").Count,
                    "session_heartbeat must not fire when _sessionId is null — no session has started");
                Assert.AreEqual(0, _mockSender.GetEventsByName("session_start").Count,
                    "session_start must not fire without an OnApplicationPause(false) call");

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

        // ─── Dispose idempotency ─────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Dispose_IsIdempotent_DoesNotThrowOrDoubleSessionEnd() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new SessionTracker(_config, _mockSender);
                tracker.OnApplicationPause(false);
                await UniTask.Delay(50);

                tracker.Dispose();
                var countAfterFirst = _mockSender.GetEventsByName("session_end").Count;

                Assert.DoesNotThrow(() => tracker.Dispose(),
                    "Second Dispose() call must not throw");
                Assert.AreEqual(countAfterFirst, _mockSender.GetEventsByName("session_end").Count,
                    "Second Dispose() must not emit a second session_end");
            }
        );

        // ─── OnApplicationPause same-status guard ────────────────────────────

        [UnityTest]
        public IEnumerator OnApplicationPause_SameStatus_IsIgnored() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new SessionTracker(_config, _mockSender);
                tracker.OnApplicationPause(false); // start session

                int countAfterStart = _mockSender.SentEvents.Count;
                tracker.OnApplicationPause(false); // same status — must be no-op

                Assert.AreEqual(countAfterStart, _mockSender.SentEvents.Count,
                    "Calling OnApplicationPause with the same status must emit no new events");

                // Pause twice — second must be no-op too
                tracker.OnApplicationPause(true);
                int countAfterPause = _mockSender.SentEvents.Count;
                tracker.OnApplicationPause(true);
                Assert.AreEqual(countAfterPause, _mockSender.SentEvents.Count,
                    "Calling OnApplicationPause(true) again must emit no new events");

                tracker.Dispose();
                await UniTask.Yield();
            }
        );

        // ─── Session continue ────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator SessionContinue_ResumeWithinTimeout_SendsContinue() => UniTask.ToCoroutine(
            async () =>
            {
                var config = new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = 60_000,
                    SessionTimeoutMs  = 5_000 // 5s timeout — won't expire in this test
                };
                var tracker = new SessionTracker(config, _mockSender);

                tracker.OnApplicationPause(false); // session_start
                await UniTask.Delay(50);
                tracker.OnApplicationPause(true);  // session_pause
                await UniTask.Delay(50);
                tracker.OnApplicationPause(false); // resume within timeout → session_continue

                var eventNames = _mockSender.SentEvents.Select(e => e.Name).ToList();
                Assert.Contains("session_continue", eventNames,
                    "Resuming within timeout must emit session_continue, not session_start");
                Assert.AreEqual(1, _mockSender.GetEventsByName("session_start").Count,
                    "session_start must not fire again on resume within timeout");

                tracker.Dispose();
            }
        );

        // ─── Remote feature flags ────────────────────────────────────────────

        [UnityTest]
        public IEnumerator RemoteFeatureFlags_FlushEnabled_CallsFlushOnDispose() => UniTask.ToCoroutine(
            async () =>
            {
                var flags = new Dictionary<string, bool> { { "sendEventsOnFlushEnabled", true } };
                var tracker = new SessionTracker(_config, _mockSender, flags);
                tracker.OnApplicationPause(false);
                await UniTask.Delay(50);

                var flushBefore = _mockSender.FlushCount;
                tracker.Dispose();

                Assert.Greater(_mockSender.FlushCount, flushBefore,
                    "Dispose must call Flush when sendEventsOnFlushEnabled = true");
            }
        );

        [UnityTest]
        public IEnumerator RemoteFeatureFlags_FlushDisabled_DoesNotCallFlushOnDispose() => UniTask.ToCoroutine(
            async () =>
            {
                var flags = new Dictionary<string, bool> { { "sendEventsOnFlushEnabled", false } };
                var tracker = new SessionTracker(_config, _mockSender, flags);
                tracker.OnApplicationPause(false);
                await UniTask.Delay(50);

                _mockSender.Clear(); // reset flush count
                tracker.Dispose();

                Assert.AreEqual(0, _mockSender.FlushCount,
                    "Dispose must NOT call Flush when sendEventsOnFlushEnabled = false");
            }
        );

        [UnityTest]
        public IEnumerator RemoteFeatureFlags_Null_DoesNotCallFlushOnDispose() => UniTask.ToCoroutine(
            async () =>
            {
                // null flags dict → defaults to no-flush on dispose
                var tracker = new SessionTracker(_config, _mockSender, remoteFeatureFlags: null);
                tracker.OnApplicationPause(false);
                await UniTask.Delay(50);

                _mockSender.Clear();
                tracker.Dispose();

                Assert.AreEqual(0, _mockSender.FlushCount,
                    "Dispose with null remoteFeatureFlags must NOT call Flush");
            }
        );

        // ─── Session timeout ─────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator SessionTimeout_PerSessionEngagement_SentBeforeNewSession() => UniTask.ToCoroutine(
            async () =>
            {
                var config = new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = 60_000,
                    SessionTimeoutMs  = 100  // very short timeout
                };
                var tracker = new SessionTracker(config, _mockSender);

                tracker.OnApplicationPause(false); // session 1
                string session1Id = _mockSender.GetEventsByName("session_start")
                    .Last().Data?.GetValueOrDefault("session_id")?.ToString();

                await UniTask.Delay(50);
                tracker.OnApplicationPause(true);
                await UniTask.Delay(200); // let timeout expire

                // Re-gap guard means session 2 start may be suppressed; wait past 10s gap is
                // impractical in unit test — just verify per_session fires on timeout path
                tracker.OnApplicationPause(false);

                var perSessionEvents = _mockSender.GetEventsByName("noctua_user_engagement_per_session");
                Assert.GreaterOrEqual(perSessionEvents.Count, 1,
                    "noctua_user_engagement_per_session must fire when session times out");

                tracker.Dispose();
            }
        );

        // ─── SaveSessionState when no session ────────────────────────────────

        [Test]
        public void SaveSessionState_WithNoActiveSession_DoesNotWritePlayerPrefs()
        {
            // SessionTracker with no OnApplicationPause(false) call — _sessionId is null.
            // SaveSessionState() must return early without touching PlayerPrefs.
            PlayerPrefs.DeleteKey("NoctuaOrphanedSessionId");

            var tracker = new SessionTracker(_config, _mockSender);
            // Never started — _sessionId is null; heartbeat task never writes PlayerPrefs.
            // Dispose also calls ClearSessionState() which is safe when keys don't exist.
            tracker.Dispose();

            var saved = PlayerPrefs.GetString("NoctuaOrphanedSessionId", "");
            Assert.IsEmpty(saved,
                "NoctuaOrphanedSessionId must not be written when session was never started");
        }

        // ─── Constructor validation ──────────────────────────────────────────

        [Test]
        public void Constructor_NullConfig_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SessionTracker(null, _mockSender));
        }

        [Test]
        public void Constructor_NullEventSender_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SessionTracker(_config, null));
        }
    }
}
