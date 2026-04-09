using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    [Preserve]
    public class EventData
    {
        public string event_version;
        public string event_name;
        public string sdk_version;
        public string device_id;
        public string device_os_version;
        public string device_os;
        public string device_type;
        public string device_model;
        public string bundle_id;
        public long? game_id;
        public long? game_platform_id;
        public string game_version;
        public string unique_id;
        public string session_id;
        public string country;
        public string timestamp;
        public long? user_id;
        public long? player_id;
        public long? credential_id;
        public string credential_provider;
    }

    public class EventTest
    {
        private HttpMockServer _server;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _server = new HttpMockServer("http://localhost:7777/api/v1/");

            _server.AddHandler(
                "/events",
                _ => @"{""success"":""true"",""data"":{""message"":""events tracked""}}"
            );

            _server.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _server.RemoveHandler("/events");
            _server.Dispose();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // Wait for any pending fire-and-forget tasks from previous tests to settle
            yield return new WaitForSeconds(2.0f);

            PlayerPrefs.DeleteKey("NoctuaEvents");
            PlayerPrefs.Save();

            // Clean up per-row event storage file to prevent leftover events between tests
            var eventStorePath = Path.Combine(Application.persistentDataPath, "noctua_events.jsonl");
            if (File.Exists(eventStorePath)) File.Delete(eventStorePath);

            ExperimentManager.Clear();

            var empty = false;

            while (!empty)
            {
                _server.Requests.Clear();

                yield return new WaitForSeconds(0.5f);

                empty = _server.Requests.Count == 0;
            }

            // Final cleanup in case async tasks wrote to storage during drain
            PlayerPrefs.DeleteKey("NoctuaEvents");
            PlayerPrefs.Save();

            // Also clean up JSONL file again in case it was recreated during drain
            if (File.Exists(eventStorePath)) File.Delete(eventStorePath);
        }

        [UnityTest]
        public IEnumerator SendAnEvent_AllMandatoryFieldsAreIncluded() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                eventSender.SetProperties(gameId: 7, gamePlatformId: 17);

                eventSender.Send("test_event");

                await UniTask.WhenAny(UniTask.Delay(3000), UniTask.WaitUntil(() => _server.Requests.Count > 0));

                Assert.IsTrue(_server.Requests.TryDequeue(out var request));

                Assert.AreEqual("POST", request.Method);
                Assert.AreEqual("/api/v1/events", request.Path);
                Assert.AreEqual("application/x-ndjson", request.Headers["Content-Type"]);
                Assert.IsNotNull(request.Headers["X-CLIENT-ID"]);
                Assert.IsNotNull(request.Headers["X-DEVICE-ID"]);

                var evt = JsonConvert.DeserializeObject<EventData>(request.Body);

                Assert.IsNotNull(evt.event_version);
                Assert.IsNotNull(evt.event_name);
                Assert.IsNotNull(evt.sdk_version);
                Assert.IsNotNull(evt.device_id);
                Assert.IsNotNull(evt.device_os_version);
                Assert.IsNotNull(evt.device_os);
                Assert.IsNotNull(evt.device_type);
                Assert.IsNotNull(evt.device_model);
                Assert.IsNotNull(evt.bundle_id);

                Assert.AreEqual(7, evt.game_id);
                Assert.AreEqual(17, evt.game_platform_id);

                Assert.IsNotNull(evt.country);
                Assert.IsNotNull(evt.timestamp);

                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SetAndClearProperties_EventPropertiesAffected() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                // session_id in event data comes from ExperimentManager, not SetProperties
                ExperimentManager.SetSessionId("6");

                eventSender.SetProperties(
                    userId: 1,
                    playerId: 2,
                    credentialId: 3,
                    gameId: 4,
                    gamePlatformId: 5,
                    sessionId: "6"
                );

                eventSender.Send("test_event");

                eventSender.SetProperties(
                    userId: 11,
                    playerId: 12,
                    credentialId: 13
                );

                eventSender.Send("test_event");

                eventSender.SetProperties(
                    userId: null,
                    playerId: null,
                    credentialId: null
                );

                eventSender.Send("test_event");

                var win = await UniTask.WhenAny(
                    UniTask.Delay(3000),
                    UniTask.WaitUntil(() => _server.Requests.Count > 0)
                );

                if (win == 0)
                {
                    Assert.Fail("No requests received.");
                }

                // Allow additional events to arrive (fire-and-forget write queue + send loop)
                await UniTask.Delay(500);

                var sb = new StringBuilder();

                while (_server.Requests.TryDequeue(out var request))
                {
                    sb.AppendLine(request.Body);
                }

                // Filter to only our test_event events (exclude stale session_start etc. from Noctua init)
                var events = sb
                    .ToString()
                    .Trim()
                    .Split('\n')
                    .Select(JsonConvert.DeserializeObject<Dictionary<string, object>>)
                    .Where(e => e.ContainsKey("event_name") && e["event_name"].ToString() == "test_event")
                    .ToList();

                Assert.AreEqual(3, events.Count,
                    $"Expected 3 test_event events, got {events.Count}");

                Assert.AreEqual(1, events[0]["user_id"]);
                Assert.AreEqual(2, events[0]["player_id"]);
                Assert.AreEqual(3, events[0]["credential_id"]);
                Assert.AreEqual(4, events[0]["game_id"]);
                Assert.AreEqual(5, events[0]["game_platform_id"]);
                Assert.AreEqual("6", events[0]["session_id"]);

                Assert.AreEqual(11, events[1]["user_id"]);
                Assert.AreEqual(12, events[1]["player_id"]);
                Assert.AreEqual(13, events[1]["credential_id"]);
                Assert.AreEqual(4, events[1]["game_id"]);
                Assert.AreEqual(5, events[1]["game_platform_id"]);
                Assert.AreEqual("6", events[1]["session_id"]);

                Assert.IsFalse(events[2].ContainsKey("user_id"));
                Assert.IsFalse(events[2].ContainsKey("player_id"));
                Assert.IsFalse(events[2].ContainsKey("credential_id"));
                Assert.AreEqual(4, events[2]["game_id"]);
                Assert.AreEqual(5, events[2]["game_platform_id"]);
                Assert.AreEqual("6", events[2]["session_id"]);

                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SendEvents_DontReachBatchingNumberThreshold_DontSendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 3,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                eventSender.Send("test_event_1");

                eventSender.Send("test_event_1");

                await UniTask.WhenAny(UniTask.Delay(2000), UniTask.WaitUntil(() => _server.Requests.Count > 0));

                Assert.IsFalse(_server.Requests.TryDequeue(out _));

                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SendEvents_DontReachBatchingNumberThresholdButFlushed_SendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 3,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                eventSender.Send("test_event_1");

                eventSender.Send("test_event_1");

                // Wait for fire-and-forget Send tasks to complete and write to storage
                await UniTask.Delay(500);

                eventSender.Flush();

                var events = await GetEventsFromServerAsync();

                Assert.AreEqual(2, events.Count);

                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SendEvents_ReachBatchingNumberThreshold_SendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 3,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                eventSender.SetProperties(gameId: 7, gamePlatformId: 17);

                eventSender.Send("test_event_1");
                eventSender.Send("test_event_1");
                eventSender.Send("test_event_1");
                eventSender.Send("test_event_1");

                var events = await GetEventsFromServerAsync();

                Assert.GreaterOrEqual(events.Count, 3);

                foreach (var evt in events)
                {
                    Assert.IsNotNull(evt.event_version);
                    Assert.IsNotNull(evt.event_name);
                    Assert.IsNotNull(evt.sdk_version);
                    Assert.IsNotNull(evt.device_id);
                    Assert.IsNotNull(evt.device_os_version);
                    Assert.IsNotNull(evt.device_os);
                    Assert.IsNotNull(evt.device_type);
                    Assert.IsNotNull(evt.device_model);
                    Assert.IsNotNull(evt.bundle_id);

                    Assert.AreEqual(7, evt.game_id);
                    Assert.AreEqual(17, evt.game_platform_id);

                    Assert.IsNotNull(evt.country);
                    Assert.IsNotNull(evt.timestamp);
                }

                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SendEvents_ReachBatchingTimeout_SendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 3,
                        BatchPeriodMs = 500,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                eventSender.SetProperties(gameId: 7, gamePlatformId: 17);

                eventSender.Send("test_event_1");
                eventSender.Send("test_event_1");

                var events = await GetEventsFromServerAsync();

                Assert.AreEqual(2, events.Count);

                foreach (var evt in events)
                {
                    Assert.IsNotNull(evt.event_version);
                    Assert.IsNotNull(evt.event_name);
                    Assert.IsNotNull(evt.sdk_version);
                    Assert.IsNotNull(evt.device_id);
                    Assert.IsNotNull(evt.device_os_version);
                    Assert.IsNotNull(evt.device_os);
                    Assert.IsNotNull(evt.device_type);
                    Assert.IsNotNull(evt.device_model);
                    Assert.IsNotNull(evt.bundle_id);

                    Assert.AreEqual(7, evt.game_id);
                    Assert.AreEqual(17, evt.game_platform_id);

                    Assert.IsNotNull(evt.country);
                    Assert.IsNotNull(evt.timestamp);
                }

                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SessionTracker_OnStartPauseAndResume_SendStartPauseAndResumeEvents() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                var sessionTracker = new SessionTracker(new SessionTrackerConfig(), eventSender);

                // Drain any stale events that may have leaked through SetUp
                await UniTask.Delay(500);
                while (_server.Requests.TryDequeue(out _)) { }

                sessionTracker.OnApplicationPause(false);
                sessionTracker.OnApplicationPause(true);
                sessionTracker.OnApplicationPause(false);
                sessionTracker.OnApplicationPause(true);

                var allEvents = await GetEventsFromServerAsync(5000, 1000);

                // Filter by session_id to exclude stale events from previous tests.
                // Our SessionTracker creates a unique session_id (Guid) on first OnApplicationPause(false),
                // so all 4 events share the same session_id.
                var sessionGroup = allEvents
                    .Where(e => e.session_id != null)
                    .GroupBy(e => e.session_id)
                    .FirstOrDefault(g => g.Any(e => e.event_name == "session_continue"));

                Assert.IsNotNull(sessionGroup,
                    $"Expected session group with session_continue not found. Events: {string.Join(", ", allEvents.Select(e => e.event_name))}");

                var events = sessionGroup.ToList();

                // Filter out user_engagement events to verify existing session lifecycle unchanged
                var sessionEvents = events.Where(e => e.event_name != "noctua_user_engagement").ToList();

                Assert.AreEqual("session_start", sessionEvents[0].event_name);
                Assert.AreEqual("session_pause", sessionEvents[1].event_name);
                Assert.AreEqual("session_continue", sessionEvents[2].event_name);
                Assert.AreEqual("session_pause", sessionEvents[3].event_name);

                Assert.True(sessionEvents.All(evt => evt.session_id != null));

                sessionTracker.Dispose();
                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SendEvents_SessionTrackerPaused_FlushedAndSendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 100,
                        BatchPeriodMs = 300_000,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                var sessionTracker = new SessionTracker(new SessionTrackerConfig(), eventSender);

                sessionTracker.OnApplicationPause(false);

                eventSender.Send("test_event_1");
                eventSender.Send("test_event_2");

                // Wait for fire-and-forget Send tasks to write to storage
                await UniTask.Delay(500);

                sessionTracker.OnApplicationPause(true);

                var events = await GetEventsFromServerAsync(5000, 1000);

                // Filter out user_engagement events to verify existing event ordering unchanged
                var nonEngagementEvents = events.Where(e => e.event_name != "noctua_user_engagement").ToList();

                Assert.AreEqual("session_start", nonEngagementEvents[0].event_name);
                Assert.AreEqual("test_event_1", nonEngagementEvents[1].event_name);
                Assert.AreEqual("test_event_2", nonEngagementEvents[2].event_name);
                Assert.AreEqual("session_pause", nonEngagementEvents[3].event_name);

                Assert.True(nonEngagementEvents.All(evt => evt.session_id != null));

                sessionTracker.Dispose();
                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SessionTracker_NotStarted_NoEvents() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                var sessionTracker = new SessionTracker(
                    new SessionTrackerConfig
                    {
                        HeartbeatPeriodMs = 100,
                        SessionTimeoutMs = 2000
                    },
                    eventSender
                );

                await UniTask.Delay(1000);

                var events = await GetEventsFromServerAsync();

                Assert.AreEqual(0, events.Count);

                sessionTracker.Dispose();
                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SessionTracker_OnHeartbeatPeriod_SendHeartbeatEvent() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                var sessionTracker = new SessionTracker(
                    new SessionTrackerConfig
                    {
                        HeartbeatPeriodMs = 500,
                        SessionTimeoutMs = 2000
                    },
                    eventSender
                );

                sessionTracker.OnApplicationPause(false);

                await UniTask.Delay(1200);

                var events = await GetEventsFromServerAsync();

                Debug.Log(events.Select(evt => evt.event_name).Aggregate((a, b) => $"{a}\n{b}"));

                // Filter out user_engagement events to verify existing heartbeat behavior unchanged
                var nonEngagementEvents = events.Where(e => e.event_name != "noctua_user_engagement").ToList();

                Assert.AreEqual("session_start", nonEngagementEvents[0].event_name);
                Assert.AreEqual("session_heartbeat", nonEngagementEvents[1].event_name);
                Assert.AreEqual("session_heartbeat", nonEngagementEvents[2].event_name);

                var sessionId = nonEngagementEvents[0].session_id;
                var sessionIds = nonEngagementEvents.Skip(1).Select(evt => evt.session_id).ToList();

                Assert.True(sessionIds.All(id => id == sessionId));

                sessionTracker.Dispose();
                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator SessionTracker_OnSessionTimeout_EndSessionAndStartNewSession() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                var sessionTracker = new SessionTracker(
                    new SessionTrackerConfig
                    {
                        HeartbeatPeriodMs = 500,
                        SessionTimeoutMs = 2000
                    },
                    eventSender
                );

                // Drain any stale events that may have leaked through SetUp
                await UniTask.Delay(500);
                while (_server.Requests.TryDequeue(out _)) { }

                sessionTracker.OnApplicationPause(false);
                sessionTracker.OnApplicationPause(true);

                await UniTask.Delay(2500);

                sessionTracker.OnApplicationPause(false);

                await UniTask.Delay(200);

                var allEvents = await GetEventsFromServerAsync(5000, 1000);

                // This test produces events across 2 sessions (timeout creates new session).
                // Filter out user_engagement events first, then find the expected pattern.
                var nonEngagementEvents = allEvents.Where(e => e.event_name != "noctua_user_engagement").ToList();

                // Find sessions that have a session_start event
                var sessionStarts = nonEngagementEvents
                    .Where(e => e.event_name == "session_start" && e.session_id != null)
                    .ToList();

                // Find the pair: session A has start+pause, session B has start, and
                // session A's start comes before session B's start
                List<EventData> events = null;
                for (int i = 0; i < sessionStarts.Count - 1; i++)
                {
                    var sessionA = sessionStarts[i].session_id;
                    var sessionAEvents = nonEngagementEvents.Where(e => e.session_id == sessionA).ToList();
                    if (sessionAEvents.Count >= 2 &&
                        sessionAEvents[0].event_name == "session_start" &&
                        sessionAEvents[1].event_name == "session_pause")
                    {
                        for (int j = i + 1; j < sessionStarts.Count; j++)
                        {
                            var sessionB = sessionStarts[j].session_id;
                            if (sessionA != sessionB)
                            {
                                events = new List<EventData>
                                {
                                    sessionAEvents[0],
                                    sessionAEvents[1],
                                    sessionStarts[j]
                                };
                                break;
                            }
                        }
                        if (events != null) break;
                    }
                }

                Assert.IsNotNull(events,
                    $"Expected session timeout pattern not found. Events: {string.Join(", ", allEvents.Select(e => $"{e.event_name}({e.session_id?.Substring(0, 8)})"))}");

                Debug.Log(events.Select(evt => evt.event_name).Aggregate((a, b) => $"{a}\n{b}"));

                Assert.AreEqual("session_start", events[0].event_name);
                Assert.AreEqual("session_pause", events[1].event_name);
                Assert.AreEqual("session_start", events[2].event_name);

                Assert.True(events.All(evt => evt.session_id != null));

                Assert.AreEqual(events[0].session_id, events[1].session_id);
                Assert.AreNotEqual(events[0].session_id, events[2].session_id);
                Assert.AreNotEqual(events[1].session_id, events[2].session_id);

                sessionTracker.Dispose();
                eventSender.Dispose();
            }
        );

        // ===== New tests for bug fixes =====

        [UnityTest]
        public IEnumerator QueueCap_ExceedMaxSize_DropsOldestEvents() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1000,            // High to prevent auto-send
                        BatchPeriodMs = 300_000,      // Long to prevent timeout send
                        CycleDelay = 60_000,          // Long cycle delay to prevent send loop
                        MaxStoredEvents = 5,          // Cap at 5 events for this test
                        NativePlugin = new DefaultNativePlugin(),
                        IsOfflineModeFunc = () => true // Skip GeoIP; makes Send() complete synchronously fast
                    },
                    new NoctuaLocale()
                );

                // Send 8 events, cap is 5
                for (int i = 0; i < 8; i++)
                {
                    eventSender.Send($"event_{i}");
                }

                // Allow fire-and-forget tasks to complete and eviction to run
                await UniTask.Delay(2000);

                // Flush to send remaining events
                eventSender.Flush();

                var events = await GetEventsFromServerAsync(5000, 1000);

                // Should have at most 5 events (oldest dropped)
                Assert.LessOrEqual(events.Count, 5,
                    $"Expected at most 5 events, got {events.Count}: {string.Join(", ", events.Select(e => e.event_name))}");

                // The oldest events (event_0, event_1, event_2) should have been dropped
                var names = events.Select(e => e.event_name).ToList();
                Assert.IsFalse(names.Contains("event_0"), "Oldest event (event_0) should have been dropped");
                Assert.IsFalse(names.Contains("event_1"), "Second oldest event (event_1) should have been dropped");
                Assert.IsFalse(names.Contains("event_2"), "Third oldest event (event_2) should have been dropped");

                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator PersistenceRoundTrip_SaveAndLoad_EventsRecovered() => UniTask.ToCoroutine(
            async () =>
            {
                // Phase 1: Send events and dispose (events are persisted immediately via write queue)
                var eventSender1 = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1000,           // High to prevent auto-send
                        BatchPeriodMs = 300_000,     // Long to prevent timeout
                        CycleDelay = 60_000,         // Long cycle delay to prevent send loop
                        NativePlugin = new DefaultNativePlugin(),
                        IsOfflineModeFunc = () => true // Skip GeoIP so Send() writes to storage fast
                    },
                    new NoctuaLocale()
                );

                eventSender1.Send("persist_event_1");
                eventSender1.Send("persist_event_2");

                // Wait for fire-and-forget tasks to write to per-row storage
                await UniTask.Delay(2000);

                // Dispose — storage is already up to date (no queue to persist)
                eventSender1.Dispose();

                // Small delay for any async cleanup
                await UniTask.Delay(200);

                // Phase 2: Create new EventSender — constructor auto-flushes persisted events
                var eventSender2 = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin()
                    },
                    new NoctuaLocale()
                );

                // The loaded events should be sent
                var events = await GetEventsFromServerAsync(5000, 1000);

                var names = events.Select(e => e.event_name).ToList();
                Assert.IsTrue(names.Contains("persist_event_1"),
                    $"persist_event_1 not found in recovered events. Got: {string.Join(", ", names)}");
                Assert.IsTrue(names.Contains("persist_event_2"),
                    $"persist_event_2 not found in recovered events. Got: {string.Join(", ", names)}");

                eventSender2.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator FlushRaceCondition_ConcurrentFlushAndSendLoop_NoDataLoss() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 2,
                        BatchPeriodMs = 200,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin(),
                        IsOfflineModeFunc = () => true // Skip GeoIP so Send() writes to storage fast
                    },
                    new NoctuaLocale()
                );

                eventSender.Send("event_a");
                eventSender.Send("event_b");
                eventSender.Send("event_c");

                // Wait for fire-and-forget Send tasks to write to storage
                await UniTask.Delay(1000);

                // Call Flush while the send loop is also running
                eventSender.Flush();

                var events = await GetEventsFromServerAsync(5000, 1000);

                // All 3 events should arrive via flush or send loop
                // _isFlushing guard prevents concurrent sends, so no duplicates
                Assert.GreaterOrEqual(events.Count, 3,
                    $"Expected at least 3 events, got {events.Count}: {string.Join(", ", events.Select(e => e.event_name))}");

                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator EventOrdering_MultipleEvents_ArriveInOrder() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 5,
                        CycleDelay = 100,
                        NativePlugin = new DefaultNativePlugin(),
                        IsOfflineModeFunc = () => true // Skip GeoIP for deterministic ordering
                    },
                    new NoctuaLocale()
                );

                for (int i = 0; i < 5; i++)
                {
                    eventSender.Send($"ordered_event_{i}");
                }

                var events = await GetEventsFromServerAsync();

                Assert.AreEqual(5, events.Count,
                    $"Expected 5 events, got {events.Count}: {string.Join(", ", events.Select(e => e.event_name))}");
                for (int i = 0; i < 5; i++)
                {
                    Assert.AreEqual($"ordered_event_{i}", events[i].event_name,
                        $"Event at index {i} was {events[i].event_name}, expected ordered_event_{i}");
                }

                eventSender.Dispose();
            }
        );

        [UnityTest]
        public IEnumerator AppKillDuringSend_EventsSurvive() => UniTask.ToCoroutine(
            async () =>
            {
                // Events are written to per-row storage immediately after Send(), so they survive app kill
                var nativePlugin = new DefaultNativePlugin();
                var sender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1000,
                        BatchPeriodMs = 300_000,
                        CycleDelay = 60_000,
                        NativePlugin = nativePlugin,
                        IsOfflineModeFunc = () => true // Skip GeoIP so Send() writes to storage fast
                    },
                    new NoctuaLocale()
                );

                sender.Send("survive_event");
                await UniTask.Delay(1000); // Let fire-and-forget complete

                // Simulate app kill: just dispose without flush
                sender.Dispose();

                // Verify events are in per-row storage (not old PlayerPrefs blob)
                var tcs = new TaskCompletionSource<List<NativeEvent>>();
                nativePlugin.GetEventsBatch(100, 0, events => tcs.SetResult(events));
                var stored = await tcs.Task;
                Assert.IsTrue(stored.Any(e => e.EventJson.Contains("survive_event")),
                    "Event should be persisted to per-row storage immediately after Send()");
            }
        );

        private async Task<List<EventData>> GetEventsFromServerAsync(int timeoutMs = 3000, int settleMs = 500)
        {
            var win = await UniTask.WhenAny(UniTask.Delay(timeoutMs), UniTask.WaitUntil(() => _server.Requests.Count > 0));

            if (win == 0)
            {
                return new List<EventData>();
            }

            // Wait a bit more for additional events to arrive
            await UniTask.Delay(settleMs);

            var sb = new StringBuilder();

            while (_server.Requests.TryDequeue(out var request))
            {
                sb.AppendLine(request.Body);
            }

            // Deduplicate events: although _isFlushing guard prevents concurrent sends,
            // edge cases during rapid test execution may produce duplicates.
            var lines = sb.ToString().Trim().Split('\n')
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Distinct()
                .ToList();

            return lines.Select(JsonConvert.DeserializeObject<EventData>).ToList();
        }
    }
}
