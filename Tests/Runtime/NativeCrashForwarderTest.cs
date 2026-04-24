using System;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Runtime
{
    /// <summary>
    /// Tests for <see cref="NativeCrashForwarder"/> payload shaping and iOS
    /// PlayerPrefs-backed dedup. Platform-specific sources (MetricKit,
    /// ApplicationExitInfo) are not exercised — the tests call the forwarder's
    /// public entry points (<c>ForwardAndroidRecord</c>, <c>OnIosCrashPayload</c>)
    /// with hand-crafted data, which is the same contract the production code
    /// uses.
    /// </summary>
    public class NativeCrashForwarderTest
    {
        private MockEventSender _mock;
        private NativeCrashForwarder _forwarder;

        private const string PrefKeyIosSeenReportIds = "noctua.nativecrash.ios.seenIds";
        private const string PrefKeyAndroidLastTsMs = "noctua.nativecrash.android.lastTsMs";

        [SetUp]
        public void SetUp()
        {
            _mock = new MockEventSender();
            _forwarder = new NativeCrashForwarder(_mock);

            // PlayerPrefs is process-wide; clear our keys so tests are hermetic.
            PlayerPrefs.DeleteKey(PrefKeyIosSeenReportIds);
            PlayerPrefs.DeleteKey(PrefKeyAndroidLastTsMs);
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            PlayerPrefs.DeleteKey(PrefKeyIosSeenReportIds);
            PlayerPrefs.DeleteKey(PrefKeyAndroidLastTsMs);
            PlayerPrefs.Save();
        }

        private static IConvertible Get(Dictionary<string, IConvertible> d, string k)
        {
            return d != null && d.TryGetValue(k, out var v) ? v : null;
        }

        [Test]
        public void Ctor_NullEventSender_Throws()
        {
            Assert.Throws<ArgumentNullException>(() => new NativeCrashForwarder(null));
        }

        [Test]
        public void ForwardAndroidRecord_ShapesPayloadAsClientErrorNative()
        {
            var r = new AndroidNativeCrashReporter.NativeCrashRecord
            {
                ErrorType = "CRASH_NATIVE",
                Severity = "crash",
                Message = "segfault at 0x0",
                StackTrace = "#00 pc 00001234 libfoo.so",
                TimestampMs = 1_700_000_000_000L,
                Pid = 12345,
                ExitStatus = 11,
                ProcessName = "com.example.game",
                OsReportId = "1700000000000-12345-CRASH_NATIVE"
            };

            _forwarder.ForwardAndroidRecord(r);

            var events = _mock.GetEventsByName("client_error");
            Assert.AreEqual(1, events.Count);
            var d = events[0].Data;
            Assert.AreEqual("native", Get(d, "source").ToString());
            Assert.AreEqual("crash", Get(d, "severity").ToString());
            Assert.AreEqual("CRASH_NATIVE", Get(d, "error_type").ToString());
            Assert.AreEqual("segfault at 0x0", Get(d, "message").ToString());
            Assert.AreEqual("#00 pc 00001234 libfoo.so", Get(d, "stack_trace").ToString());
            Assert.AreEqual("native", Get(d, "thread").ToString());
            Assert.AreEqual(12345, Convert.ToInt32(Get(d, "native_pid")));
            Assert.AreEqual(11, Convert.ToInt32(Get(d, "native_exit_status")));
            Assert.AreEqual("com.example.game", Get(d, "native_process").ToString());
            Assert.AreEqual("1700000000000-12345-CRASH_NATIVE", Get(d, "os_report_id").ToString());
            Assert.IsTrue(Convert.ToBoolean(Get(d, "reported_at_launch")));
        }

        [Test]
        public void ForwardAndroidRecord_TruncatesMessageAndStack()
        {
            var r = new AndroidNativeCrashReporter.NativeCrashRecord
            {
                ErrorType = "ANR",
                Message = new string('m', 1000),
                StackTrace = new string('s', 10_000),
                TimestampMs = 1L,
                OsReportId = "anr-1"
            };

            _forwarder.ForwardAndroidRecord(r);

            var d = _mock.SentEvents[0].Data;
            Assert.AreEqual(500, Get(d, "message").ToString().Length);
            Assert.AreEqual(4000, Get(d, "stack_trace").ToString().Length);
        }

        [Test]
        public void ForwardAndroidRecord_NullFieldsGetSafeDefaults()
        {
            var r = new AndroidNativeCrashReporter.NativeCrashRecord
            {
                // All strings null — forwarder must not NPE
                TimestampMs = 1L
            };

            Assert.DoesNotThrow(() => _forwarder.ForwardAndroidRecord(r));

            var d = _mock.SentEvents[0].Data;
            Assert.AreEqual("UnknownCrash", Get(d, "error_type").ToString());
            Assert.AreEqual("", Get(d, "message").ToString());
            Assert.AreEqual("", Get(d, "stack_trace").ToString());
            Assert.AreEqual("", Get(d, "native_process").ToString());
            Assert.AreEqual("", Get(d, "os_report_id").ToString());
        }

        [Test]
        public void OnIosCrashPayload_FirstOccurrence_Forwards()
        {
            var json = BuildIosPayload(
                reportId: "ios-1",
                errorType: "SIGSEGV",
                message: "Segmentation fault",
                stack: "0 libsystem 0x0 foo",
                signal: 11);

            _forwarder.OnIosCrashPayload(json);

            var events = _mock.GetEventsByName("client_error");
            Assert.AreEqual(1, events.Count);
            var d = events[0].Data;
            Assert.AreEqual("native", Get(d, "source").ToString());
            Assert.AreEqual("crash", Get(d, "severity").ToString());
            Assert.AreEqual("SIGSEGV", Get(d, "error_type").ToString());
            Assert.AreEqual("ios-1", Get(d, "os_report_id").ToString());
            Assert.AreEqual(11, Convert.ToInt32(Get(d, "native_signal")));
        }

        [Test]
        public void OnIosCrashPayload_SameReportIdTwice_DedupsToOne()
        {
            var json = BuildIosPayload(reportId: "dup-1", errorType: "SIGABRT");

            _forwarder.OnIosCrashPayload(json);
            _forwarder.OnIosCrashPayload(json);

            Assert.AreEqual(1, _mock.GetEventsByName("client_error").Count,
                "the same os_report_id must not be re-forwarded");
        }

        [Test]
        public void OnIosCrashPayload_MissingReportId_GetsGuidFallback_AndIsNotDeduped()
        {
            // Each call without a report_id gets a fresh GUID, so both forward.
            var json = BuildIosPayload(reportId: null, errorType: "SIGILL");

            _forwarder.OnIosCrashPayload(json);
            _forwarder.OnIosCrashPayload(json);

            Assert.AreEqual(2, _mock.GetEventsByName("client_error").Count);
        }

        [Test]
        public void OnIosCrashPayload_InvalidJson_DoesNotThrow_DoesNotForward()
        {
            Assert.DoesNotThrow(() => _forwarder.OnIosCrashPayload("not-json"));
            Assert.DoesNotThrow(() => _forwarder.OnIosCrashPayload(""));
            Assert.DoesNotThrow(() => _forwarder.OnIosCrashPayload(null));

            Assert.AreEqual(0, _mock.GetEventsByName("client_error").Count);
        }

        [Test]
        public void OnIosCrashPayload_DedupCap_PrunesOldestAfter64Ids()
        {
            // Fire 70 distinct IDs. First 6 should be evicted from the stored
            // set, so re-firing them forwards again.
            for (int i = 0; i < 70; i++)
            {
                _forwarder.OnIosCrashPayload(BuildIosPayload($"id-{i}", "SIGSEGV"));
            }
            Assert.AreEqual(70, _mock.SentEvents.Count,
                "all 70 distinct IDs forward on first occurrence");

            _mock.Clear();

            // id-0 … id-5 should be evicted (70 - 64 = 6); id-6 … id-69 retained.
            _forwarder.OnIosCrashPayload(BuildIosPayload("id-0", "SIGSEGV"));
            _forwarder.OnIosCrashPayload(BuildIosPayload("id-69", "SIGSEGV"));

            var events = _mock.GetEventsByName("client_error");
            Assert.AreEqual(1, events.Count,
                "id-0 was evicted and re-forwards; id-69 is retained and is deduped");
            Assert.AreEqual("id-0", Get(events[0].Data, "os_report_id").ToString());
        }

        [Test]
        public void OnIosCrashPayload_PersistsAcrossInstances_ViaPlayerPrefs()
        {
            _forwarder.OnIosCrashPayload(BuildIosPayload("persist-1", "SIGSEGV"));
            Assert.AreEqual(1, _mock.SentEvents.Count);

            // Simulate next app launch: new forwarder, same process, same PlayerPrefs.
            var mock2 = new MockEventSender();
            var forwarder2 = new NativeCrashForwarder(mock2);

            forwarder2.OnIosCrashPayload(BuildIosPayload("persist-1", "SIGSEGV"));

            Assert.AreEqual(0, mock2.SentEvents.Count,
                "previously-seen iOS report_id must stay deduped across launches");
        }

        [Test]
        public void ForwardAndroidRecord_Source_IsAlwaysNative()
        {
            // Regression guard: even if a caller passes source="managed" in a
            // misused record, the forwarder hard-codes source="native".
            _forwarder.ForwardAndroidRecord(new AndroidNativeCrashReporter.NativeCrashRecord
            {
                ErrorType = "CRASH_NATIVE",
                TimestampMs = 1L,
                OsReportId = "x"
            });

            Assert.AreEqual("native", Get(_mock.SentEvents[0].Data, "source").ToString());
        }

        private static string BuildIosPayload(
            string reportId,
            string errorType,
            string message = "",
            string stack = "",
            int? signal = null)
        {
            var dict = new Dictionary<string, object>
            {
                { "error_type", errorType },
                { "message", message },
                { "stack_trace", stack },
                { "timestamp_utc", "2026-04-23T00:00:00Z" }
            };
            if (reportId != null) dict["os_report_id"] = reportId;
            if (signal.HasValue) dict["signal"] = signal.Value;

            return Newtonsoft.Json.JsonConvert.SerializeObject(dict);
        }
    }
}
