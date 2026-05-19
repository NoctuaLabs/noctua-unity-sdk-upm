using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using com.noctuagames.sdk;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    /// <summary>
    /// Extended EditMode tests for <see cref="BugReportExporter"/>,
    /// <see cref="NativeCrashForwarder"/> lifecycle, and <see cref="AccountContainer"/>
    /// edge cases not covered by the primary test files.
    /// </summary>

    // ═══════════════════════════════════════════════════════════════════════
    // BugReportExporter — coverage gaps
    // ═══════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class BugReportExporterExtendedTest
    {
        private static T Invoke<T>(string methodName, params object[] args)
        {
            var method = typeof(BugReportExporter).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, $"Private static '{methodName}' not found on BugReportExporter");
            return (T)method.Invoke(null, args);
        }

        // ── FormatDevice ────────────────────────────────────────────────────

        [Test]
        public void FormatDevice_ReturnsNonEmptyString()
        {
            var result = Invoke<string>("FormatDevice");

            Assert.IsNotNull(result);
            Assert.IsNotEmpty(result);
        }

        [Test]
        public void FormatDevice_ContainsExpectedLabels()
        {
            var result = Invoke<string>("FormatDevice");

            // Check for static label text present regardless of device values.
            StringAssert.Contains("Device model",        result, "FormatDevice must include 'Device model' label");
            StringAssert.Contains("Operating system",    result, "FormatDevice must include 'Operating system' label");
            StringAssert.Contains("Captured at (UTC)",   result, "FormatDevice must include capture timestamp label");
        }

        // ── SafeSerialize — catch (fallback) path ──────────────────────────

        /// <summary>
        /// Newtonsoft.Json throws <see cref="Newtonsoft.Json.JsonSerializationException"/>
        /// for self-referential objects when <c>ReferenceLoopHandling</c> is Error (default).
        /// Wrapping the instance in a plain object value triggers the catch branch.
        /// </summary>
        private class SelfRef
        {
            public SelfRef Self { get; set; }
            public override string ToString() => "SelfRefValue";
        }

        [Test]
        public void SafeSerialize_CircularReference_FallsBackToToStringEncoding()
        {
            var loop = new SelfRef();
            loop.Self = loop; // creates reference cycle

            var dict = new Dictionary<string, object> { { "cycle", loop } };

            // The normal JsonConvert path should throw on the cycle;
            // SafeSerialize must catch it and return a manually-built JSON string.
            string result = null;
            Assert.DoesNotThrow(() =>
            {
                result = Invoke<string>("SafeSerialize", dict);
            }, "SafeSerialize must not throw when JSON serialization fails");

            Assert.IsNotNull(result, "Fallback result must not be null");
            StringAssert.StartsWith("{", result, "Fallback result must start with '{'");
            StringAssert.EndsWith("}",  result, "Fallback result must end with '}'");
            StringAssert.Contains("cycle",        result, "Fallback must include the key");
            StringAssert.Contains("SelfRefValue", result, "Fallback must use ToString() for the value");
        }

        [Test]
        public void SafeSerialize_FallbackWithNullValue_EncodesEmptyString()
        {
            var loop = new SelfRef();
            loop.Self = loop;

            var dict = new Dictionary<string, object> { { "key", loop }, { "nullkey", null } };

            string result = null;
            Assert.DoesNotThrow(() => result = Invoke<string>("SafeSerialize", dict));

            // null value should encode as empty string in the fallback path
            StringAssert.Contains("nullkey", result);
        }

        // ── FormatLogs — cap-exceeded branch ───────────────────────────────

        [Test]
        public void FormatLogs_WithCapExceeded_OnlyLastNLinesAppear()
        {
            // Add LogsCap + 5 entries with distinct messages.
            var ledger = new LogInspectorLedger();
            int total = BugReportExporter.LogsCap + 5;
            for (int i = 0; i < total; i++)
            {
                ledger.OnLog(new LogEntry(
                    timestampUtc: DateTime.UtcNow,
                    level:        LogLevel.Info,
                    source:       "Src",
                    tag:          "Tag",
                    message:      $"msg-{i:D5}",
                    stackTrace:   null));
            }
            ledger.Pump();

            var result = Invoke<string>("FormatLogs", ledger);

            // First 5 messages (0-4) should be dropped.
            StringAssert.DoesNotContain("msg-00000", result, "msg-0 is before the cap window");
            StringAssert.DoesNotContain("msg-00004", result, "msg-4 is before the cap window");
            // Last message should be present.
            StringAssert.Contains($"msg-{(total - 1):D5}", result, "Last message must appear");
        }

        // ── FormatEvents — with payload ─────────────────────────────────────

        [Test]
        public void FormatEvents_WithPayload_ContainsPayloadData()
        {
            var monitor = new TrackerDebugMonitor();
            var payload = new Dictionary<string, object>
            {
                { "level", 42 },
                { "currency", "gold" }
            };
            monitor.OnEvent("Adjust", "level_up", payload, null, TrackerEventPhase.Emitted, null);
            monitor.Pump();

            var result = Invoke<string>("FormatEvents", monitor);

            StringAssert.Contains("payload",  result, "FormatEvents must include 'payload' label");
            StringAssert.Contains("level",    result, "Payload key 'level' must appear");
            StringAssert.Contains("currency", result, "Payload key 'currency' must appear");
        }

        // ── FormatHttp — cap-exceeded branch ────────────────────────────────

        [Test]
        public void FormatHttp_WithMoreThanCapExchanges_OnlyLastHttpCapAppear()
        {
            var httpLog = new HttpInspectorLog();
            int total = BugReportExporter.HttpCap + 5;
            for (int i = 0; i < total; i++)
            {
                httpLog.OnRequestEnd(new HttpExchange
                {
                    Id       = Guid.NewGuid(),
                    Method   = "GET",
                    Url      = $"https://api.example.com/item/{i}",
                    Status   = 200,
                    State    = HttpExchangeState.Complete,
                    StartUtc = DateTime.UtcNow
                });
            }
            httpLog.Pump();

            var result = Invoke<string>("FormatHttp", httpLog);

            StringAssert.DoesNotContain("/item/0", result, "First exchange is before the cap window");
            StringAssert.Contains($"/item/{total - 1}", result, "Last exchange must appear");
        }

        [Test]
        public void FormatHttp_WithError_ContainsErrorText()
        {
            var httpLog = new HttpInspectorLog();
            httpLog.OnRequestEnd(new HttpExchange
            {
                Id       = Guid.NewGuid(),
                Method   = "POST",
                Url      = "https://api.example.com/fail",
                Status   = 503,
                State    = HttpExchangeState.Failed,
                Error    = "service unavailable",
                StartUtc = DateTime.UtcNow
            });
            httpLog.Pump();

            var result = Invoke<string>("FormatHttp", httpLog);

            StringAssert.Contains("service unavailable", result);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // NativeCrashForwarder — lifecycle gaps
    // ═══════════════════════════════════════════════════════════════════════

    [TestFixture]
    public class NativeCrashForwarderLifecycleTest
    {
        private MockEventSender _mock;
        private NativeCrashForwarder _forwarder;

        private const string PrefKeyIosSeenReportIds = "noctua.nativecrash.ios.seenIds";
        private const string PrefKeyAndroidLastTsMs  = "noctua.nativecrash.android.lastTsMs";

        [SetUp]
        public void SetUp()
        {
            _mock     = new MockEventSender();
            _forwarder = new NativeCrashForwarder(_mock);

            PlayerPrefs.DeleteKey(PrefKeyIosSeenReportIds);
            PlayerPrefs.DeleteKey(PrefKeyAndroidLastTsMs);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            // Best-effort stop to cancel any background tasks started by Start().
            try { _forwarder.Stop(); } catch { /* ignored */ }

            PlayerPrefs.DeleteKey(PrefKeyIosSeenReportIds);
            PlayerPrefs.DeleteKey(PrefKeyAndroidLastTsMs);
            PlayerPrefs.Save();
        }

        // ── Start() idempotency ──────────────────────────────────────────────

        [Test]
        public void Start_CalledTwice_IsIdempotent_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                _forwarder.Start();
                _forwarder.Start(); // second call must be a no-op
            }, "Calling Start() twice must not throw");
        }

        [Test]
        public void Start_CalledTwice_StartedFlagIsTrue()
        {
            _forwarder.Start();
            _forwarder.Start();

            var started = (bool)typeof(NativeCrashForwarder)
                .GetField("_started", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_forwarder);

            Assert.IsTrue(started, "_started must be true after Start()");
        }

        // ── Stop() lifecycle ─────────────────────────────────────────────────

        [Test]
        public void Stop_AfterStart_ClearsStartedFlag()
        {
            _forwarder.Start();
            _forwarder.Stop();

            var started = (bool)typeof(NativeCrashForwarder)
                .GetField("_started", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_forwarder);

            Assert.IsFalse(started, "_started must be false after Stop()");
        }

        [Test]
        public void Stop_AfterStart_StopCtsBecomeNull()
        {
            _forwarder.Start();
            _forwarder.Stop();

            var cts = typeof(NativeCrashForwarder)
                .GetField("_stopCts", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_forwarder);

            Assert.IsNull(cts, "_stopCts must be null after Stop()");
        }

        [Test]
        public void Stop_ThenStart_CanRestartSuccessfully()
        {
            _forwarder.Start();
            _forwarder.Stop();

            Assert.DoesNotThrow(() => _forwarder.Start(),
                "Should be able to Start() again after Stop()");

            var started = (bool)typeof(NativeCrashForwarder)
                .GetField("_started", BindingFlags.NonPublic | BindingFlags.Instance)
                .GetValue(_forwarder);

            Assert.IsTrue(started, "_started must be true after restarting");
        }

        // ── ForwardAndroidRecord — timestamp edge cases ──────────────────────

        [Test]
        public void ForwardAndroidRecord_ZeroTimestamp_TimestampUtcIsPresentAndNonEmpty()
        {
            // TimestampMs=0 → UnixMsToIso8601 falls back to DateTime.UtcNow.ToString("o")
            _forwarder.ForwardAndroidRecord(new AndroidNativeCrashReporter.NativeCrashRecord
            {
                ErrorType   = "ANR",
                TimestampMs = 0,
                OsReportId  = "zero-ts"
            });

            Assert.AreEqual(1, _mock.SentEvents.Count);
            var tsUtc = _mock.SentEvents[0].Data["timestamp_utc"].ToString();
            Assert.IsNotNull(tsUtc);
            Assert.IsNotEmpty(tsUtc, "timestamp_utc must not be empty for TimestampMs=0");
        }

        [Test]
        public void ForwardAndroidRecord_PositiveTimestamp_TimestampUtcMatchesEpoch()
        {
            // Known epoch: 2023-11-14T22:13:20.000Z
            long knownMs = 1_700_000_000_000L;
            _forwarder.ForwardAndroidRecord(new AndroidNativeCrashReporter.NativeCrashRecord
            {
                ErrorType   = "CRASH_NATIVE",
                TimestampMs = knownMs,
                OsReportId  = "known-ts"
            });

            var tsUtc = _mock.SentEvents[0].Data["timestamp_utc"].ToString();
            // The ISO-8601 string must start with "2023-11-14"
            StringAssert.StartsWith("2023-11-14", tsUtc,
                "Positive TimestampMs must produce a deterministic ISO-8601 date");
        }

        // ── OnIosCrashPayload — edge cases ───────────────────────────────────

        [Test]
        public void OnIosCrashPayload_EmptyString_DoesNotForward()
        {
            Assert.DoesNotThrow(() => _forwarder.OnIosCrashPayload(""));

            Assert.AreEqual(0, _mock.SentEvents.Count,
                "Empty JSON string must not produce a client_error event");
        }

        [Test]
        public void OnIosCrashPayload_NullString_DoesNotForward()
        {
            Assert.DoesNotThrow(() => _forwarder.OnIosCrashPayload(null));

            Assert.AreEqual(0, _mock.SentEvents.Count,
                "Null JSON string must not produce a client_error event");
        }
    }

}
