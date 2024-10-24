using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    public class RequestData
    {
        public string Method;
        public string Path;
        public NameValueCollection Headers;
        public string Body;
    }

    public class HttpMockServer : IDisposable
    {
        public readonly ConcurrentQueue<RequestData> Requests = new();

        private readonly HttpListener _listener;
        private readonly string _basePath;
        private readonly Dictionary<string, Func<HttpListenerRequest, string>> _handlers;

        public HttpMockServer(string prefix)
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add(prefix);
            _basePath = new Uri(prefix).AbsolutePath;
            _handlers = new Dictionary<string, Func<HttpListenerRequest, string>>();
        }

        public void AddHandler(string path, Func<HttpListenerRequest, string> handler)
        {
            _handlers[$"{_basePath}{path[1..]}"] = handler;
        }

        public void RemoveHandler(string path)
        {
            _handlers.Remove($"{_basePath}{path[1..]}");
        }

        public void Start()
        {
            if (_listener.IsListening) throw new InvalidOperationException("Server is already running.");

            _listener.Start();
            _ = Task.Run(HandleIncomingConnections);

            Debug.Log("HttpMockServer started.");
        }

        private async Task HandleIncomingConnections()
        {
            while (_listener.IsListening)
            {
                Debug.Log("HttpMockServer: Waiting for incoming connection...");
                
                var context = await _listener.GetContextAsync();
                var request = context.Request;
                using var response = context.Response;

                // Find the handler for the requested path
                if (_handlers.TryGetValue(request.Url.AbsolutePath, out var handler))
                {
                    var responseString = handler(request);

                    try
                    {
                        using var reader = new StreamReader(request.InputStream);
                        var requestString = await reader.ReadToEndAsync();

                        Requests.Enqueue(
                            new RequestData
                            {
                                Method = request.HttpMethod,
                                Path = request.Url.AbsolutePath,
                                Headers = request.Headers,
                                Body = requestString,
                            }
                        );

                        Debug.Log($"HttpMockServer: {request.HttpMethod} {request.Url} => {response.StatusCode}");
                        
                        var buffer = Encoding.UTF8.GetBytes(responseString);
                        response.ContentLength64 = buffer.Length;
                        
                        Debug.Log($"HttpMockServer: writing response to {request.Url.AbsolutePath}");
                        
                        response.StatusCode = (int)HttpStatusCode.OK;

                        await response.OutputStream.WriteAsync(buffer, 0, buffer.Length);
                        
                        response.OutputStream.Close();

                        Debug.Log($"HttpMockServer: response written to {request.Url.AbsolutePath}");
                    }
                    catch (Exception ex)
                    {
                        response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    }
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
        }

        // Stops the mock server
        public void Stop()
        {
            if (!_listener.IsListening) throw new InvalidOperationException("Server is not running.");

            _listener.Stop();
            _listener.Close();

            Debug.Log("HttpMockServer stopped.");
        }

        public void Dispose()
        {
            Stop();
            _listener.Close();
        }
    }

    public class EventTest
    {
        private HttpMockServer _server;

        [OneTimeSetUp]
        public void Setup()
        {
            _server = new HttpMockServer("http://localhost:7777/api/v1/");
            _server.AddHandler(
                "/events",
                _ => @"{""success"":""true"",""data"":{""message"":""events tracked""}}"
            );

            _server.Start();
        }

        [OneTimeTearDown]
        public void TearDown()
        {
            _server.RemoveHandler("/events");
            _server.Dispose();
        }
        
        [Ignore("This test is ignored because it requires a real server.")]
        [UnityTest, Order(0)]
        public IEnumerator SendAndEventToRealServer_ExpectSuccess() => UniTask.ToCoroutine(
            async () =>
            {
                _server.Requests.Clear();

                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = NoctuaConfig.DefaultTrackerUrl,
                        ClientId = "test_client_id"
                    },
                    new NoctuaLocale()
                );

                Assert.DoesNotThrow(() => eventSender.Send("test_event"));
            }
        );

        [UnityTest, Order(1)]
        public IEnumerator SendAnEvent_AllMandatoryFieldsAreIncluded() => UniTask.ToCoroutine(
            async () =>
            {
                _server.Requests.Clear();

                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchingNumberThreshold = 1
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

                var evt = JsonUtility.FromJson<EventData>(request.Body);

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
        );
        
        [UnityTest, Order(2)]
        public IEnumerator SendAnEvent_DontReachBatchingNumberThreshold_DontSendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                _server.Requests.Clear();

                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchingNumberThreshold = 3
                    },
                    new NoctuaLocale()
                );

                eventSender.Send("test_event_1");

                eventSender.Send("test_event_1");
                
                await UniTask.WhenAny(UniTask.Delay(1000), UniTask.WaitUntil(() => _server.Requests.Count > 0));

                Assert.IsFalse(_server.Requests.TryDequeue(out _));
            }
        );
        
        [UnityTest, Order(3)]
        public IEnumerator SendAnEvent_ReachBatchingNumberThreshold_SendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                _server.Requests.Clear();

                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchingNumberThreshold = 3
                    },
                    new NoctuaLocale()
                );

                eventSender.SetProperties(gameId: 7, gamePlatformId: 17);

                eventSender.Send("test_event_1");
                eventSender.Send("test_event_1");
                eventSender.Send("test_event_1");
                eventSender.Send("test_event_1");
                
                await UniTask.WhenAny(UniTask.Delay(1000), UniTask.WaitUntil(() => _server.Requests.Count > 0));

                Assert.IsTrue(_server.Requests.TryDequeue(out var request));
                
                var eventBodies = request.Body.Split('\n');
                
                Assert.GreaterOrEqual(eventBodies.Length, 3);
                
                foreach (var body in eventBodies)
                {
                    var evt = JsonUtility.FromJson<EventData>(body);
                    
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
            }
        );
        
        [UnityTest, Order(4)]
        public IEnumerator SendAnEvent_ReachBatchingTimeout_SendToServer() => UniTask.ToCoroutine(
            async () =>
            {
                _server.Requests.Clear();

                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchingNumberThreshold = 3,
                        BatchingTimoutMs = 500
                    },
                    new NoctuaLocale()
                );
                
                eventSender.SetProperties(gameId: 7, gamePlatformId: 17);

                eventSender.Send("test_event_1");
                eventSender.Send("test_event_1");
                
                await UniTask.WhenAny(UniTask.Delay(1000), UniTask.WaitUntil(() => _server.Requests.Count > 0));

                Assert.IsTrue(_server.Requests.TryDequeue(out var request));
                
                var eventBodies = request.Body.Split('\n');
                
                Assert.AreEqual(2, eventBodies.Length);
                
                foreach (var body in eventBodies)
                {
                    var evt = JsonUtility.FromJson<EventData>(body);
                    
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
            }
        );
        
        [UnityTest, Order(5)]
        public IEnumerator LoginAsGuest_SendAuthenticatedEvent() => UniTask.ToCoroutine(
            async () =>
            {
                _server.Requests.Clear();

                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchingNumberThreshold = 1
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
                
                await UniTask.WhenAny(UniTask.Delay(1000), UniTask.WaitUntil(() => _server.Requests.Count > 0));
                
                var sb = new StringBuilder();
                
                while (_server.Requests.TryDequeue(out var request))
                {
                    sb.AppendLine(request.Body);
                }

                var events = sb.ToString().Trim().Split('\n').Select(JsonUtility.FromJson<EventData>).ToArray();
                var eventNames = events.Select(evt => evt.event_name).ToList();
                
                Assert.AreEqual(1, eventNames.Count(x => x == "account_authenticated"));

                foreach (var evt in events)
                {
                    Assert.AreNotEqual(0, evt.user_id);
                    Assert.AreNotEqual(0, evt.player_id);
                    Assert.AreNotEqual(0, evt.credential_id);
                }
            }
        );
        
        [UnityTest, Order(6)]
        public IEnumerator LoginAsGuest_ThenSendCustomEvent_UserIdAndPlayerIdAttached() => UniTask.ToCoroutine(
            async () =>
            {
                _server.Requests.Clear();

                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "test_client_id",
                        BatchingNumberThreshold = 2
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
                
                await UniTask.WhenAny(UniTask.Delay(1000), UniTask.WaitUntil(() => _server.Requests.Count > 0));

                var sb = new StringBuilder();
                
                while (_server.Requests.TryDequeue(out var request))
                {
                    sb.AppendLine(request.Body);
                }

                var events = sb.ToString().Trim().Split('\n').Select(JsonUtility.FromJson<EventData>).ToArray();
                var eventNames = events.Select(evt => evt.event_name).ToList();
                
                Assert.AreEqual(1, eventNames.Count(x => x == "account_authenticated"));
                Assert.AreEqual(1, eventNames.Count(x => x == "test_event"));

                foreach (var evt in events)
                {
                    Assert.AreNotEqual(0, evt.user_id);
                    Assert.AreNotEqual(0, evt.player_id);
                    Assert.AreNotEqual(0, evt.credential_id);
                }
            }
        );
        
        [UnityTest, Order(7)]
        public IEnumerator LoginWithEmailTwiceThenSwitch_SendThreeEvents() => UniTask.ToCoroutine(
            async () =>
            {
                _server.Requests.Clear();

                var eventSender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl = "http://localhost:7777/api/v1",
                        ClientId = "102-0abe09ca2ed8",
                        BatchingNumberThreshold = 3
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

                await UniTask.WhenAny(UniTask.Delay(1000), UniTask.WaitUntil(() => _server.Requests.Count > 0));
                
                var sb = new StringBuilder();
                
                while (_server.Requests.TryDequeue(out var request))
                {
                    sb.AppendLine(request.Body);
                }

                var events = sb.ToString().Trim().Split('\n').Select(JsonUtility.FromJson<EventData>).ToArray();
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
            }
        );

        [Preserve]
        public class EventData
        {
            public string event_version = "1.0";
            public string event_name;
            public string sdk_version;
            public string device_id;
            public string device_os_version;
            public string device_os;
            public string device_type;
            public string device_model;
            public string bundle_id;
            public int game_id;
            public int game_platform_id;
            public string game_version;
            public string unique_id;
            public string session_id;
            public string country;
            public string timestamp;
            public long user_id;
            public long player_id;
            public long credential_id;
            public string credential_provider;
        }
    }
}