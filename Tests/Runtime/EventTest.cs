using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
        
        [TearDown]
        public void TearDown()
        {
            _server.Requests.Clear();
        }

        [Ignore("This test is ignored because it requires a real server.")]
        [UnityTest]
        public IEnumerator SendAndEventToRealServer_ExpectSuccess() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = NoctuaConfig.DefaultTrackerUrl,
                        ClientId = "test_client_id"
                    },
                    new NoctuaLocale()
                );
                
                eventSender.Flush();

                Assert.DoesNotThrow(() => eventSender.Send("test_event"));
            }
        );

        [UnityTest]
        public IEnumerator SendAnEvent_AllMandatoryFieldsAreIncluded() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1
                    },
                    new NoctuaLocale()
                );

                eventSender.SetProperties(gameId: 7, gamePlatformId: 17);

                eventSender.Send("test_event");

                await UniTask.WhenAny(UniTask.Delay(1000), UniTask.WaitUntil(() => _server.Requests.Count > 0));

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
                _server.Requests.Clear();
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
                        BatchSize = 1
                    },
                    new NoctuaLocale()
                );

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
                    UniTask.Delay(1000),
                    UniTask.WaitUntil(() => _server.Requests.Count > 0)
                );

                if (win == 0)
                {
                    Assert.Fail("No requests received.");
                }

                var sb = new StringBuilder();

                while (_server.Requests.TryDequeue(out var request))
                {
                    sb.AppendLine(request.Body);
                }

                var events = sb
                    .ToString()
                    .Trim()
                    .Split('\n')
                    .Select(JsonConvert.DeserializeObject<Dictionary<string, object>>)
                    .ToList();

                Assert.AreEqual(3, events.Count);

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
                _server.Requests.Clear();
            }
        );

        [UnityTest]
        public IEnumerator SendAnEvent_DontReachBatchingNumberThreshold_DontSendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 3
                    },
                    new NoctuaLocale()
                );

                eventSender.Send("test_event_1");

                eventSender.Send("test_event_1");

                await UniTask.WhenAny(UniTask.Delay(1000), UniTask.WaitUntil(() => _server.Requests.Count > 0));

                Assert.IsFalse(_server.Requests.TryDequeue(out _));
                
                eventSender.Dispose();
                _server.Requests.Clear();
            }
        );

        [UnityTest]
        public IEnumerator SendAnEvent_DontReachBatchingNumberThresholdButFlushed_SendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 3
                    },
                    new NoctuaLocale()
                );

                eventSender.Send("test_event_1");

                eventSender.Send("test_event_1");

                eventSender.Flush();

                var events = await GetEventsFromServerAsync();

                Assert.AreEqual(2, events.Count);
                
                eventSender.Dispose();
                _server.Requests.Clear();
            }
        );

        [UnityTest]
        public IEnumerator SendAnEvent_ReachBatchingNumberThreshold_SendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 3
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
                _server.Requests.Clear();
            }
        );

        [UnityTest]
        public IEnumerator SendAnEvent_ReachBatchingTimeout_SendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 3,
                        BatchPeriodMs = 500
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
                _server.Requests.Clear();
            }
        );

        [UnityTest]
        public IEnumerator LoginAsGuest_SendAuthenticatedEvent() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 1
                    },
                    new NoctuaLocale()
                );

                PlayerPrefs.DeleteKey("NoctuaAccountContainer");

                var authSvc = new NoctuaAuthenticationService(
                    baseUrl: "https://sdk-api-v2.noctuaprojects.com/api/v1",
                    clientId: "102-0abe09ca2ed8",
                    nativeAccountStore: new DefaultNativePlugin(),
                    eventSender: eventSender,
                    bundleId: Application.identifier
                );

                await authSvc.AuthenticateAsync();

                var events = await GetEventsFromServerAsync();

                var eventNames = events.Select(evt => evt.event_name).ToList();

                Assert.AreEqual(1, eventNames.Count(x => x == "account_authenticated"));

                foreach (var evt in events)
                {
                    Assert.AreNotEqual(0, evt.user_id);
                    Assert.AreNotEqual(0, evt.player_id);
                    Assert.AreNotEqual(0, evt.credential_id);
                }
                
                eventSender.Dispose();
                _server.Requests.Clear();
            }
        );

        [UnityTest]
        public IEnumerator LoginAsGuest_ThenSendCustomEvent_UserIdAndPlayerIdAttached() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchSize = 2
                    },
                    new NoctuaLocale()
                );

                PlayerPrefs.DeleteKey("NoctuaAccountContainer");

                var authSvc = new NoctuaAuthenticationService(
                    baseUrl: "https://sdk-api-v2.noctuaprojects.com/api/v1",
                    clientId: "102-0abe09ca2ed8",
                    nativeAccountStore: new DefaultNativePlugin(),
                    eventSender: eventSender,
                    bundleId: Application.identifier
                );

                await authSvc.AuthenticateAsync();

                eventSender.Send("test_event");

                var events = await GetEventsFromServerAsync();

                var eventNames = events.Select(evt => evt.event_name).ToList();

                Assert.AreEqual(1, eventNames.Count(x => x == "account_authenticated"));
                Assert.AreEqual(1, eventNames.Count(x => x == "test_event"));

                foreach (var evt in events)
                {
                    Assert.AreNotEqual(0, evt.user_id);
                    Assert.AreNotEqual(0, evt.player_id);
                    Assert.AreNotEqual(0, evt.credential_id);
                }
                
                eventSender.Dispose();
                _server.Requests.Clear();
            }
        );

        [UnityTest]
        public IEnumerator LoginWithEmailTwiceThenSwitch_SendThreeEvents() => UniTask.ToCoroutine(
            async () =>
            {
                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "102-0abe09ca2ed8",
                        BatchSize = 3
                    },
                    new NoctuaLocale()
                );

                PlayerPrefs.DeleteKey("NoctuaAccountContainer");

                var authSvc = new NoctuaAuthenticationService(
                    baseUrl: "https://sdk-api-v2.noctuaprojects.com/api/v1",
                    clientId: "102-0abe09ca2ed8",
                    nativeAccountStore: new DefaultNativePlugin(),
                    eventSender: eventSender,
                    bundleId: Application.identifier
                );

                // don't ask me how I got these accounts

                await authSvc.LoginWithEmailAsync("weteso6757@digopm.com", "aaaaaa");

                await authSvc.LoginWithEmailAsync("sefeg80041@digopm.com", "aaaaaa");

                await authSvc.SwitchAccountAsync(authSvc.AccountList.Last());

                var events = await GetEventsFromServerAsync();

                var eventNames = events.Select(evt => evt.event_name).ToList();

                Assert.AreEqual(2, eventNames.Count(x => x == "account_authenticated"));
                Assert.AreEqual(2, eventNames.Count(x => x == "account_authenticated_by_email"));
                Assert.AreEqual(1, eventNames.Count(x => x == "account_switched"));

                foreach (var evt in events)
                {
                    Assert.AreNotEqual(0, evt.user_id);
                    Assert.AreNotEqual(0, evt.player_id);
                    Assert.AreNotEqual(0, evt.credential_id);
                }
                
                eventSender.Dispose();
                _server.Requests.Clear();
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
                        BatchSize = 1
                    },
                    new NoctuaLocale()
                );

                var sessionTracker = new SessionTracker(new SessionTrackerConfig(), eventSender);

                sessionTracker.OnApplicationPause(false);
                sessionTracker.OnApplicationPause(true);
                sessionTracker.OnApplicationPause(false);

                var events = await GetEventsFromServerAsync();

                Assert.AreEqual("session_start", events[0].event_name);
                Assert.AreEqual("session_pause", events[1].event_name);
                Assert.AreEqual("session_continue", events[2].event_name);

                Assert.True(events.All(evt => evt.session_id != null));
                
                sessionTracker.Dispose();
                eventSender.Dispose();
                _server.Requests.Clear();
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
                        BatchSize = 1
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
                _server.Requests.Clear();
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
                        BatchSize = 1
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

                Assert.AreEqual("session_start", events[0].event_name);
                Assert.AreEqual("session_heartbeat", events[1].event_name);
                Assert.AreEqual("session_heartbeat", events[2].event_name);
                
                var sessionId = events[0].session_id;
                var sessionIds = events.Skip(1).Select(evt => evt.session_id).ToList();
                
                Assert.True(sessionIds.All(id => id == sessionId));
                
                sessionTracker.Dispose();
                eventSender.Dispose();
                _server.Requests.Clear();
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
                        BatchSize = 1
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
                sessionTracker.OnApplicationPause(true);
                
                await UniTask.Delay(2500);
                
                sessionTracker.OnApplicationPause(false);
        
                await UniTask.Delay(100);

                var events = await GetEventsFromServerAsync();
                
                Debug.Log(events.Select(evt => evt.event_name).Aggregate((a, b) => $"{a}\n{b}"));
                
                Assert.AreEqual("session_start", events[0].event_name);
                Assert.AreEqual("session_pause", events[1].event_name);
                Assert.AreEqual("session_start", events[2].event_name);
                
                Assert.True(events.All(evt => evt.session_id != null));
                
                Assert.True(events.Select(evt => evt.session_id).Distinct().Count() == 2);
                
                Assert.AreEqual(events[0].session_id, events[1].session_id);
                Assert.AreNotEqual(events[0].session_id, events[2].session_id);
                Assert.AreNotEqual(events[1].session_id, events[2].session_id);

                sessionTracker.Dispose();
                eventSender.Dispose();
                _server.Requests.Clear();
            }
        );

        private async Task<List<EventData>> GetEventsFromServerAsync()
        {
            var win = await UniTask.WhenAny(UniTask.Delay(1000), UniTask.WaitUntil(() => _server.Requests.Count > 0));

            if (win == 0)
            {
                return new List<EventData>();
            }

            var sb = new StringBuilder();

            while (_server.Requests.TryDequeue(out var request))
            {
                sb.AppendLine(request.Body);
            }

            return sb.ToString().Trim().Split('\n').Select(JsonConvert.DeserializeObject<EventData>).ToList();
        }
    }
}

