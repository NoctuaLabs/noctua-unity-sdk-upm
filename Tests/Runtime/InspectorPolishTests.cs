using System;
using System.Collections.Generic;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Inspector;
using NUnit.Framework;

namespace Tests.Runtime
{
    public class CurlExporterTests
    {
        [Test]
        public void NullExchangeReturnsEmpty()
        {
            Assert.AreEqual("", CurlExporter.ToCurl(null));
        }

        [Test]
        public void GetRequestIsIncluded()
        {
            var ex = new HttpExchange { Method = "GET", Url = "https://api.noctua.gg/x" };
            var curl = CurlExporter.ToCurl(ex);
            StringAssert.StartsWith("curl -X GET 'https://api.noctua.gg/x'", curl);
        }

        [Test]
        public void PostWithHeadersAndBody()
        {
            var ex = new HttpExchange
            {
                Method = "POST",
                Url = "https://api.noctua.gg/events",
                RequestHeaders = new Dictionary<string, string>
                {
                    { "Content-Type", "application/json" },
                    { "Authorization", "\u2022\u2022\u2022\u2022" },
                },
                RequestBody = "{\"event_name\":\"x\"}",
            };
            var curl = CurlExporter.ToCurl(ex);
            StringAssert.Contains("curl -X POST", curl);
            StringAssert.Contains("-H 'Content-Type: application/json'", curl);
            StringAssert.Contains("-H 'Authorization:", curl);
            StringAssert.Contains("--data-raw '{\"event_name\":\"x\"}'", curl);
        }

        [Test]
        public void EmbeddedSingleQuoteIsShellSafe()
        {
            var ex = new HttpExchange
            {
                Method = "POST",
                Url = "https://x/y",
                RequestBody = "it's",
            };
            var curl = CurlExporter.ToCurl(ex);
            // Shell-safe single-quote escape: '…' → '\''
            StringAssert.Contains("'it'\\''s'", curl);
        }
    }

    public class InspectorExporterTests
    {
        [Test]
        public void ExportsSchemaEnvelope()
        {
            var json = InspectorExporter.ToJson(new List<HttpExchange>(), new List<TrackerEmission>());
            StringAssert.StartsWith("{\"schema\":1", json);
            StringAssert.Contains("\"http\":[]", json);
            StringAssert.Contains("\"trackers\":[]", json);
        }

        [Test]
        public void IncludesHttpExchange()
        {
            var exchanges = new List<HttpExchange>
            {
                new HttpExchange
                {
                    Id = Guid.NewGuid(),
                    Method = "POST",
                    Url = "https://api.noctua.gg/events",
                    Status = 200,
                    ElapsedMs = 312,
                    State = HttpExchangeState.Complete,
                    StartUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
                }
            };
            var json = InspectorExporter.ToJson(exchanges, new List<TrackerEmission>());
            StringAssert.Contains("\"method\":\"POST\"", json);
            StringAssert.Contains("\"url\":\"https://api.noctua.gg/events\"", json);
            StringAssert.Contains("\"status\":200", json);
            StringAssert.Contains("\"elapsedMs\":312", json);
            StringAssert.Contains("\"state\":\"Complete\"", json);
        }

        [Test]
        public void IncludesTrackerEmission()
        {
            var em = new TrackerEmission
            {
                Id = Guid.NewGuid(),
                Provider = "Firebase",
                EventName = "level_up",
                Phase = TrackerEventPhase.Acknowledged,
                CreatedUtc = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc),
            };
            em.History.Add(new TrackerPhaseTransition { Phase = TrackerEventPhase.Queued, AtUtc = em.CreatedUtc });
            em.History.Add(new TrackerPhaseTransition { Phase = TrackerEventPhase.Acknowledged, AtUtc = em.CreatedUtc });

            var json = InspectorExporter.ToJson(new List<HttpExchange>(), new List<TrackerEmission> { em });
            StringAssert.Contains("\"provider\":\"Firebase\"", json);
            StringAssert.Contains("\"eventName\":\"level_up\"", json);
            StringAssert.Contains("\"phase\":\"Acknowledged\"", json);
            StringAssert.Contains("\"history\":[", json);
        }

        [Test]
        public void EscapesControlCharactersInStrings()
        {
            var exchanges = new List<HttpExchange>
            {
                new HttpExchange { Method = "GET", Url = "x", RequestBody = "line1\nline2\t\"quoted\"" }
            };
            var json = InspectorExporter.ToJson(exchanges, new List<TrackerEmission>());
            StringAssert.Contains("\"reqBody\":\"line1\\nline2\\t\\\"quoted\\\"\"", json);
        }
    }
}
