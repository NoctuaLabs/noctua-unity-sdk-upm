using System;
using System.Collections.Generic;
using System.Reflection;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="BugReportExporter"/>.
    ///
    /// Covers via reflection (private static methods):
    ///   — <c>FormatBuild</c>      — null guard, field inclusion
    ///   — <c>FormatLogs</c>       — null guard, empty ledger, populated ledger (level chars)
    ///   — <c>FormatEvents</c>     — null guard, empty monitor, populated monitor
    ///   — <c>FormatHttp</c>       — null guard, empty log, populated log
    ///   — <c>LevelChar</c>        — every LogLevel value
    ///   — <c>SafeSerialize</c>    — normal dict, null, empty dict
    /// Public constants <c>LogsCap</c>, <c>EventsCap</c>, <c>HttpCap</c> also verified.
    /// </summary>
    [TestFixture]
    public class BugReportExporterTest
    {
        // ─── Reflection helpers ────────────────────────────────────────────

        private static T Invoke<T>(string methodName, params object[] args)
        {
            var method = typeof(BugReportExporter).GetMethod(
                methodName,
                BindingFlags.NonPublic | BindingFlags.Static);

            Assert.IsNotNull(method, $"Private static method '{methodName}' not found on BugReportExporter");
            return (T)method.Invoke(null, args);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Public constants
        // ═══════════════════════════════════════════════════════════════════

        [Test] public void LogsCap_IsPositive()    => Assert.Greater(BugReportExporter.LogsCap,   0);
        [Test] public void EventsCap_IsPositive()  => Assert.Greater(BugReportExporter.EventsCap, 0);
        [Test] public void HttpCap_IsPositive()    => Assert.Greater(BugReportExporter.HttpCap,   0);

        // ═══════════════════════════════════════════════════════════════════
        // FormatBuild
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void FormatBuild_Null_ReturnsPlaceholder()
        {
            var result = Invoke<string>("FormatBuild", new object[] { null });

            Assert.AreEqual("(no build info)", result);
        }

        [Test]
        public void FormatBuild_PopulatedInfo_ContainsAllFields()
        {
            var build = new BuildSanityInfo
            {
                UnitySdkVersion        = "1.2.3",
                NativeSdkVersion       = "4.5.6",
                BundleId               = "com.test.app",
                AppVersion             = "7.8.9",
                UnityVersion           = "2021.3.0f1",
                IsSandbox              = true,
                Region                 = "us-east-1",
                ConfigChecksum         = "abc123",
                AdjustAppTokenMasked   = "XXXX",
                FirebaseProjectId      = "proj-123",
                GoogleServicesPresent  = true,
                SkAdNetworksCount      = 42,
                AndroidPermissionsCount = 5
            };

            var result = Invoke<string>("FormatBuild", build);

            StringAssert.Contains("1.2.3",        result, "UnitySdkVersion");
            StringAssert.Contains("4.5.6",        result, "NativeSdkVersion");
            StringAssert.Contains("com.test.app", result, "BundleId");
            StringAssert.Contains("7.8.9",        result, "AppVersion");
            StringAssert.Contains("ENABLED",      result, "IsSandbox=true → ENABLED");
            StringAssert.Contains("us-east-1",    result, "Region");
            StringAssert.Contains("abc123",       result, "ConfigChecksum");
            StringAssert.Contains("proj-123",     result, "FirebaseProjectId");
            StringAssert.Contains("42",           result, "SkAdNetworksCount");
        }

        [Test]
        public void FormatBuild_SandboxFalse_ContainsDisabled()
        {
            var build = new BuildSanityInfo { IsSandbox = false };
            var result = Invoke<string>("FormatBuild", build);

            StringAssert.Contains("disabled", result, "IsSandbox=false → disabled");
        }

        [Test]
        public void FormatBuild_NegativeCounts_ContainsNa()
        {
            var build = new BuildSanityInfo
            {
                SkAdNetworksCount       = -1,
                AndroidPermissionsCount = -1
            };

            var result = Invoke<string>("FormatBuild", build);

            // Both negative counts should render as "n/a"
            Assert.AreEqual(2, CountOccurrences(result, "n/a"),
                "Both negative count fields must render as 'n/a'");
        }

        // ═══════════════════════════════════════════════════════════════════
        // FormatLogs
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void FormatLogs_NullLedger_ReturnsPlaceholder()
        {
            var result = Invoke<string>("FormatLogs", new object[] { null });

            Assert.AreEqual("(logs ledger unavailable)", result);
        }

        [Test]
        public void FormatLogs_EmptyLedger_ReturnsEmptyString()
        {
            var ledger = new LogInspectorLedger();
            ledger.Pump(); // drain empty queue

            var result = Invoke<string>("FormatLogs", ledger);

            Assert.AreEqual(string.Empty, result,
                "Empty ledger must produce an empty string");
        }

        [Test]
        public void FormatLogs_WithEntries_ContainsMessageAndSource()
        {
            var ledger = new LogInspectorLedger();
            ledger.OnLog(new LogEntry(
                timestampUtc: DateTime.UtcNow,
                level:        LogLevel.Warning,
                source:       "MyClass",
                tag:          "MyTag",
                message:      "Hello world",
                stackTrace:   null));
            ledger.Pump();

            var result = Invoke<string>("FormatLogs", ledger);

            StringAssert.Contains("Hello world", result);
            StringAssert.Contains("MyClass",     result);
            StringAssert.Contains("MyTag",       result);
            StringAssert.Contains("W",           result, "Warning level char must appear");
        }

        [Test]
        public void FormatLogs_EntryWithStackTrace_ContainsStackTrace()
        {
            var ledger = new LogInspectorLedger();
            ledger.OnLog(new LogEntry(
                timestampUtc: DateTime.UtcNow,
                level:        LogLevel.Error,
                source:       "Src",
                tag:          "Tag",
                message:      "Crash",
                stackTrace:   "at Foo.Bar()"));
            ledger.Pump();

            var result = Invoke<string>("FormatLogs", ledger);

            StringAssert.Contains("at Foo.Bar()", result);
        }

        // ═══════════════════════════════════════════════════════════════════
        // FormatEvents
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void FormatEvents_NullMonitor_ReturnsPlaceholder()
        {
            var result = Invoke<string>("FormatEvents", new object[] { null });

            Assert.AreEqual("(tracker monitor unavailable)", result);
        }

        [Test]
        public void FormatEvents_EmptyMonitor_ReturnsEmptyString()
        {
            var monitor = new TrackerDebugMonitor();

            var result = Invoke<string>("FormatEvents", monitor);

            Assert.AreEqual(string.Empty, result,
                "Empty monitor must produce an empty string");
        }

        [Test]
        public void FormatEvents_WithEvent_ContainsProviderAndEventName()
        {
            var monitor = new TrackerDebugMonitor();
            monitor.OnEvent("Adjust", "level_up", null, null, TrackerEventPhase.Emitted, null);
            monitor.Pump();

            var result = Invoke<string>("FormatEvents", monitor);

            StringAssert.Contains("Adjust",   result);
            StringAssert.Contains("level_up", result);
        }

        [Test]
        public void FormatEvents_EventWithError_ContainsError()
        {
            var monitor = new TrackerDebugMonitor();
            monitor.OnEvent("FB", "purchase", null, null, TrackerEventPhase.Failed, "timeout");
            monitor.Pump();

            var result = Invoke<string>("FormatEvents", monitor);

            StringAssert.Contains("timeout", result);
        }

        // ═══════════════════════════════════════════════════════════════════
        // FormatHttp
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void FormatHttp_NullLog_ReturnsPlaceholder()
        {
            var result = Invoke<string>("FormatHttp", new object[] { null });

            Assert.AreEqual("(http log unavailable)", result);
        }

        [Test]
        public void FormatHttp_EmptyLog_ReturnsEmptyString()
        {
            var httpLog = new HttpInspectorLog();
            httpLog.Pump();

            var result = Invoke<string>("FormatHttp", httpLog);

            Assert.AreEqual(string.Empty, result,
                "Empty HttpInspectorLog must produce an empty string");
        }

        [Test]
        public void FormatHttp_WithExchange_ContainsMethodAndUrl()
        {
            var httpLog = new HttpInspectorLog();
            var ex = new HttpExchange
            {
                Id       = Guid.NewGuid(),
                Method   = "POST",
                Url      = "https://api.example.com/v1/events",
                Status   = 200,
                State    = HttpExchangeState.Complete,
                StartUtc = DateTime.UtcNow,
            };
            httpLog.OnRequestEnd(ex);
            httpLog.Pump();

            var result = Invoke<string>("FormatHttp", httpLog);

            StringAssert.Contains("POST",                              result);
            StringAssert.Contains("https://api.example.com/v1/events", result);
        }

        // ═══════════════════════════════════════════════════════════════════
        // LevelChar
        // ═══════════════════════════════════════════════════════════════════

        [Test] public void LevelChar_Verbose_IsV() => Assert.AreEqual('V', Invoke<char>("LevelChar", LogLevel.Verbose));
        [Test] public void LevelChar_Debug_IsD()   => Assert.AreEqual('D', Invoke<char>("LevelChar", LogLevel.Debug));
        [Test] public void LevelChar_Info_IsI()    => Assert.AreEqual('I', Invoke<char>("LevelChar", LogLevel.Info));
        [Test] public void LevelChar_Warning_IsW() => Assert.AreEqual('W', Invoke<char>("LevelChar", LogLevel.Warning));
        [Test] public void LevelChar_Error_IsE()   => Assert.AreEqual('E', Invoke<char>("LevelChar", LogLevel.Error));

        [Test]
        public void LevelChar_UnknownLevel_IsQuestionMark()
        {
            // Cast a value outside the defined enum range
            var result = Invoke<char>("LevelChar", (LogLevel)999);
            Assert.AreEqual('?', result);
        }

        // ═══════════════════════════════════════════════════════════════════
        // SafeSerialize
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void SafeSerialize_NullDict_ReturnsNullJson()
        {
            var result = Invoke<string>("SafeSerialize",
                new object[] { (IReadOnlyDictionary<string, object>)null });

            Assert.AreEqual("null", result);
        }

        [Test]
        public void SafeSerialize_EmptyDict_ReturnsEmptyJsonObject()
        {
            var dict   = new Dictionary<string, object>();
            var result = Invoke<string>("SafeSerialize", dict);

            Assert.AreEqual("{}", result);
        }

        [Test]
        public void SafeSerialize_SimpleDict_ContainsKeyValue()
        {
            var dict   = new Dictionary<string, object> { { "key", "value" } };
            var result = Invoke<string>("SafeSerialize", dict);

            StringAssert.Contains("key",   result);
            StringAssert.Contains("value", result);
        }

        [Test]
        public void SafeSerialize_NumericValue_SerializesCorrectly()
        {
            var dict   = new Dictionary<string, object> { { "score", 42 } };
            var result = Invoke<string>("SafeSerialize", dict);

            StringAssert.Contains("42", result);
        }

        // ─── Private helper ────────────────────────────────────────────────

        private static int CountOccurrences(string text, string pattern)
        {
            int count = 0;
            int idx   = 0;
            while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += pattern.Length;
            }
            return count;
        }
    }
}
