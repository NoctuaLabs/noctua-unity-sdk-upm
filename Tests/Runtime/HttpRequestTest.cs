using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using com.noctuagames.sdk;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Tests.Runtime;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    [TestFixture]
    public class HttpRequestTest
    {
        private const string BaseUrl = "http://localhost:7788/api/v1/";
        private HttpMockServer _server;

        [SetUp]
        public void SetUp()
        {
            _server = new HttpMockServer(BaseUrl);
            _server.Start();
        }

        [TearDown]
        public void TearDown()
        {
            _server.Dispose();
            HttpRequest.SetLocaleProvider(null);
        }

        private class FakeLocale : ILocaleProvider
        {
            public string GetLanguage() => "th";
            public string GetCountry() => "TH";
            public string GetCurrency() => "THB";
            public string GetTranslation(LocaleTextKey textKey) => textKey.ToString();
        }

        private class SamplePayload
        {
            public string FirstName;
            public int TotalCount;
        }

        private class SampleResponse
        {
            public string Status;
        }

        // ─── JSON / NDJSON / Raw body serialization ───────────────────────────

        [UnityTest]
        public IEnumerator WithJsonBody_SerializesSnakeCase() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/echo", _ => "{\"data\":{\"status\":\"ok\"}}");

            using var req = new HttpRequest(HttpMethod.Post, BaseUrl + "echo");
            req.WithJsonBody(new SamplePayload { FirstName = "Alice", TotalCount = 3 }).NoVerboseLog();
            await req.Send<SampleResponse>();

            _server.Requests.TryDequeue(out var r);
            var body = JsonConvert.DeserializeObject<Dictionary<string, object>>(r.Body);
            Assert.IsTrue(body.ContainsKey("first_name"), "JSON should use snake_case 'first_name'");
            Assert.IsTrue(body.ContainsKey("total_count"), "JSON should use snake_case 'total_count'");
            Assert.AreEqual("Alice", body["first_name"].ToString());
        });

        [UnityTest]
        public IEnumerator WithNdjsonBody_ProducesNewlineDelimitedLines() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/ndjson", _ => "{\"data\":{\"status\":\"ok\"}}");

            var items = new List<SamplePayload>
            {
                new SamplePayload { FirstName = "Alice", TotalCount = 1 },
                new SamplePayload { FirstName = "Bob",   TotalCount = 2 },
            };

            using var req = new HttpRequest(HttpMethod.Post, BaseUrl + "ndjson");
            req.WithNdjsonBody(items).NoVerboseLog();
            await req.Send<SampleResponse>();

            _server.Requests.TryDequeue(out var r);
            var lines = r.Body.Split('\n');
            Assert.AreEqual(2, lines.Length, "NDJSON body should have exactly 2 lines for 2 items");
            foreach (var line in lines)
                Assert.DoesNotThrow(() => JsonConvert.DeserializeObject(line), "Each NDJSON line must be valid JSON");
        });

        [UnityTest]
        public IEnumerator WithRawBody_SetsOctetStreamContentType() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/raw", _ => "{\"data\":{\"status\":\"ok\"}}");

            using var req = new HttpRequest(HttpMethod.Post, BaseUrl + "raw");
            req.WithRawBody(new byte[] { 0x01, 0x02, 0x03 }).NoVerboseLog();
            await req.Send<SampleResponse>();

            _server.Requests.TryDequeue(out var r);
            Assert.AreEqual("application/octet-stream", r.Headers["Content-Type"]);
        });

        // ─── Headers ─────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator WithHeader_AddsCustomHeader() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/headers", _ => "{\"data\":{\"status\":\"ok\"}}");

            using var req = new HttpRequest(HttpMethod.Get, BaseUrl + "headers");
            req.WithHeader("X-Test-Custom", "test-value-123").NoVerboseLog();
            await req.Send<SampleResponse>();

            _server.Requests.TryDequeue(out var r);
            Assert.AreEqual("test-value-123", r.Headers["X-Test-Custom"]);
        });

        [UnityTest]
        public IEnumerator WithAuth_BearerAuth_InjectsAuthorizationHeader() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth-bearer", _ => "{\"data\":{\"status\":\"ok\"}}");

            using var req = new HttpRequest(HttpMethod.Get, BaseUrl + "auth-bearer");
            req.WithAuth(new BearerAuth("my-token-xyz")).NoVerboseLog();
            await req.Send<SampleResponse>();

            _server.Requests.TryDequeue(out var r);
            Assert.AreEqual("Bearer my-token-xyz", r.Headers["Authorization"]);
        });

        [UnityTest]
        public IEnumerator WithAuth_BasicAuth_InjectsAuthorizationHeader() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/auth-basic", _ => "{\"data\":{\"status\":\"ok\"}}");

            using var req = new HttpRequest(HttpMethod.Get, BaseUrl + "auth-basic");
            req.WithAuth(new BasicAuth("alice", "s3cret")).NoVerboseLog();
            await req.Send<SampleResponse>();

            _server.Requests.TryDequeue(out var r);
            var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("alice:s3cret"));
            Assert.AreEqual(expected, r.Headers["Authorization"]);
        });

        [UnityTest]
        public IEnumerator SetLocaleProvider_InjectsLocaleHeaders() => UniTask.ToCoroutine(async () =>
        {
            HttpRequest.SetLocaleProvider(new FakeLocale());
            _server.AddHandler("/locale", _ => "{\"data\":{\"status\":\"ok\"}}");

            using var req = new HttpRequest(HttpMethod.Get, BaseUrl + "locale");
            req.NoVerboseLog();
            await req.Send<SampleResponse>();

            _server.Requests.TryDequeue(out var r);
            Assert.AreEqual("th", r.Headers["X-LANGUAGE"]);
            Assert.AreEqual("TH", r.Headers["X-COUNTRY"]);
            Assert.AreEqual("THB", r.Headers["X-CURRENCY"]);
        });

        // ─── Path parameters ──────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator WithPathParam_ReplacesPlaceholderInUrl() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/items/42", _ => "{\"data\":{\"status\":\"ok\"}}");

            using var req = new HttpRequest(HttpMethod.Get, BaseUrl + "items/{id}");
            req.WithPathParam("id", "42").NoVerboseLog();
            await req.Send<SampleResponse>();

            _server.Requests.TryDequeue(out var r);
            Assert.IsTrue(r.Path.EndsWith("/items/42"), $"Path should end with /items/42 but was: {r.Path}");
        });

        [UnityTest]
        public IEnumerator Send_UnreplacedPathParam_ThrowsNoctuaException() => UniTask.ToCoroutine(async () =>
        {
            using var req = new HttpRequest(HttpMethod.Get, BaseUrl + "items/{id}");
            NoctuaException caught = null;
            try { await req.Send<SampleResponse>(); }
            catch (NoctuaException e) { caught = e; }

            Assert.IsNotNull(caught, "Should throw NoctuaException for unreplaced path param");
            Assert.AreEqual((int)NoctuaErrorCode.Application, caught.ErrorCode);
        });

        // ─── HTTP error response handling ─────────────────────────────────────

        [UnityTest]
        public IEnumerator Send_4xxResponse_ThrowsNoctuaExceptionWithErrorCode() => UniTask.ToCoroutine(async () =>
        {
            const string errorJson = "{\"success\":false,\"error_code\":1001,\"error_message\":\"not found\"}";
            _server.AddHandlerWithStatus("/error4xx", _ => (400, errorJson));

            using var req = new HttpRequest(HttpMethod.Get, BaseUrl + "error4xx");
            req.NoVerboseLog();
            NoctuaException caught = null;
            try { await req.Send<SampleResponse>(); }
            catch (NoctuaException e) { caught = e; }

            Assert.IsNotNull(caught, "Should throw NoctuaException on 4xx response");
            Assert.AreEqual(1001, caught.ErrorCode);
        });

        [UnityTest]
        public IEnumerator Send_5xxResponse_ThrowsNoctuaExceptionNetworking() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandlerWithStatus("/error5xx", _ => (500, "Internal Server Error"));

            using var req = new HttpRequest(HttpMethod.Get, BaseUrl + "error5xx");
            req.NoVerboseLog();
            NoctuaException caught = null;
            try { await req.Send<SampleResponse>(); }
            catch (NoctuaException e) { caught = e; }

            Assert.IsNotNull(caught, "Should throw NoctuaException on 5xx response");
            Assert.AreEqual((int)NoctuaErrorCode.Networking, caught.ErrorCode);
        });

        [UnityTest]
        public IEnumerator Send_SuccessResponse_DeserializesPayload() => UniTask.ToCoroutine(async () =>
        {
            _server.AddHandler("/success", _ => "{\"data\":{\"status\":\"found\"}}");

            using var req = new HttpRequest(HttpMethod.Get, BaseUrl + "success");
            req.NoVerboseLog();
            var result = await req.Send<SampleResponse>();

            Assert.AreEqual("found", result.Status);
        });
    }
}
