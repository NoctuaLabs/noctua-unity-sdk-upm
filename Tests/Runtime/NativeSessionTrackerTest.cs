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
    public class NativeSessionTrackerTest
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
        public IEnumerator OnResumePause_SendsNativeUserEngagement() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new NativeSessionTracker(_config, _mockSender);

                tracker.OnNativeResume();
                await UniTask.Delay(200);
                tracker.OnNativePause();

                var eventNames = _mockSender.SentEvents.Select(e => e.Name).ToList();

                // native_user_engagement(start), native_user_engagement(pause)
                Assert.AreEqual("native_user_engagement", eventNames[0]);
                Assert.AreEqual("native_user_engagement", eventNames[1]);

                var startEvt = _mockSender.SentEvents[0];
                Assert.AreEqual("start", startEvt.Data["lifecycle"].ToString());
                Assert.AreEqual(0L, Convert.ToInt64(startEvt.Data["engagement_time_msec"]));

                var pauseEvt = _mockSender.SentEvents[1];
                Assert.AreEqual("pause", pauseEvt.Data["lifecycle"].ToString());
                var msec = Convert.ToInt64(pauseEvt.Data["engagement_time_msec"]);
                Assert.GreaterOrEqual(msec, 100);
                Assert.LessOrEqual(msec, 5000);

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator OnHeartbeat_SendsNativeUserEngagement() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new NativeSessionTracker(_config, _mockSender);

                tracker.OnNativeResume();
                await UniTask.Delay(800);

                var engagements = _mockSender.SentEvents
                    .Where(e => e.Name == "native_user_engagement")
                    .ToList();

                // start + at least one foreground
                Assert.GreaterOrEqual(engagements.Count, 2);
                Assert.AreEqual("start", engagements[0].Data["lifecycle"].ToString());

                var foregroundEvents = engagements
                    .Where(e => e.Data["lifecycle"].ToString() == "foreground")
                    .ToList();
                Assert.GreaterOrEqual(foregroundEvents.Count, 1);

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator OnDispose_SendsEndAndPerSession() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new NativeSessionTracker(_config, _mockSender);

                tracker.OnNativeResume();
                await UniTask.Delay(200);

                _mockSender.Clear();
                tracker.Dispose();

                var eventNames = _mockSender.SentEvents.Select(e => e.Name).ToList();

                Assert.AreEqual("native_user_engagement", eventNames[0]);
                Assert.AreEqual("native_user_engagement_per_session", eventNames[1]);

                Assert.AreEqual("end", _mockSender.SentEvents[0].Data["lifecycle"].ToString());
            }
        );

        [UnityTest]
        public IEnumerator EngagementTime_IsIncremental() => UniTask.ToCoroutine(
            async () =>
            {
                var config = new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = 60_000,
                    SessionTimeoutMs = 1000
                };
                var tracker = new NativeSessionTracker(config, _mockSender);

                tracker.OnNativeResume();
                await UniTask.Delay(300);
                tracker.OnNativePause();

                tracker.OnNativeResume();
                await UniTask.Delay(300);
                tracker.OnNativePause();

                var pauseEngagements = _mockSender.SentEvents
                    .Where(e => e.Name == "native_user_engagement" && e.Data["lifecycle"].ToString() == "pause")
                    .ToList();
                Assert.AreEqual(2, pauseEngagements.Count);

                var firstMs = Convert.ToInt64(pauseEngagements[0].Data["engagement_time_msec"]);
                var secondMs = Convert.ToInt64(pauseEngagements[1].Data["engagement_time_msec"]);

                Assert.GreaterOrEqual(firstMs, 200);
                Assert.LessOrEqual(firstMs, 5000);
                Assert.GreaterOrEqual(secondMs, 200);
                Assert.LessOrEqual(secondMs, 5000);

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SessionTimeout_ResetsAndSendsPerSession() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new NativeSessionTracker(_config, _mockSender);

                tracker.OnNativeResume();
                await UniTask.Delay(200);
                tracker.OnNativePause();

                await UniTask.Delay(1500);

                _mockSender.Clear();
                tracker.OnNativeResume();

                var eventNames = _mockSender.SentEvents.Select(e => e.Name).ToList();

                Assert.AreEqual("native_user_engagement_per_session", eventNames[0]);
                Assert.AreEqual("native_user_engagement", eventNames[1]);

                var startEvt = _mockSender.SentEvents[1];
                Assert.AreEqual("start", startEvt.Data["lifecycle"].ToString());
                Assert.AreEqual(0L, Convert.ToInt64(startEvt.Data["engagement_time_msec"]));

                tracker.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator PerSessionEngagement_CumulativeTotal() => UniTask.ToCoroutine(
            async () =>
            {
                var config = new SessionTrackerConfig
                {
                    HeartbeatPeriodMs = 60_000,
                    SessionTimeoutMs = 300_000
                };
                var tracker = new NativeSessionTracker(config, _mockSender);

                tracker.OnNativeResume();
                await UniTask.Delay(300);
                tracker.OnNativePause();

                tracker.OnNativeResume();
                await UniTask.Delay(300);

                tracker.Dispose();

                var perSession = _mockSender.SentEvents
                    .Where(e => e.Name == "native_user_engagement_per_session")
                    .ToList();
                Assert.AreEqual(1, perSession.Count);

                var cumulativeMs = Convert.ToInt64(perSession[0].Data["engagement_time_msec"]);
                Assert.GreaterOrEqual(cumulativeMs, 400);
                Assert.LessOrEqual(cumulativeMs, 10000);
            }
        );

        [UnityTest]
        public IEnumerator NoSessionEvents_AreSent() => UniTask.ToCoroutine(
            async () =>
            {
                var tracker = new NativeSessionTracker(_config, _mockSender);

                tracker.OnNativeResume();
                await UniTask.Delay(100);
                tracker.OnNativePause();
                tracker.OnNativeResume();
                await UniTask.Delay(100);
                tracker.Dispose();

                var sessionEvents = _mockSender.SentEvents
                    .Where(e => e.Name == "session_start" || e.Name == "session_pause" ||
                                e.Name == "session_continue" || e.Name == "session_end" ||
                                e.Name == "session_heartbeat")
                    .ToList();

                Assert.AreEqual(0, sessionEvents.Count, "NativeSessionTracker must not send session events");
            }
        );
    }
}
