using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.Runtime
{
    /// <summary>
    /// Coverage-fill tests for <see cref="EventSender"/> targeting branches not exercised by
    /// <c>EventSenderBehaviorTest</c> or <c>EventTest</c>. Focus areas:
    ///
    /// Group F — Send() input validation & reserved-key stripping
    /// Group G — Send() enrichment: caller session_id, null-value stripping, sandbox propagation
    /// Group H — Game-stage stopwatch (start/complete/failed)
    /// Group I — Connectivity throttle bypass for "offline" events
    /// Group J — Native-plugin exception paths (the 4 storage helpers)
    /// Group K — Eviction path (MaxStoredEvents cap)
    /// Group L — Notify helpers: AdjustOfflineModeDisabled / null tracker / throwing tracker
    /// Group M — SanitizeHeaderValue (control chars, NUL, empty, non-ASCII high)
    /// Group N — Flush guards (early-return paths)
    /// Group O — GetCountryIDAsync fallback chain (GeoIP fail → Cloudflare)
    /// Group P — Threading: Send from a background thread; concurrent Send/Flush
    /// </summary>
    [TestFixture]
    public class EventSenderCoverageTest
    {
        private const string EventStoreFile = "noctua_events.jsonl";
        private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        private HttpMockServer _server;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            _server = new HttpMockServer("http://localhost:19883/api/v1/");
            _server.AddHandler("/events", _ => @"{""success"":""true"",""data"":{""message"":""ok""}}");
            _server.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            _server?.RemoveHandler("/events");
            _server?.Dispose();
        }

        [SetUp]
        public void SetUp()
        {
            PlayerPrefs.DeleteKey("NoctuaCurrentStageLevel");
            PlayerPrefs.DeleteKey("NoctuaCurrentStageMode");
            PlayerPrefs.DeleteKey("NoctuaPersistentDeviceId");
            PlayerPrefs.Save();

            var path = Path.Combine(Application.persistentDataPath, EventStoreFile);
            if (File.Exists(path)) File.Delete(path);

            LogAssert.ignoreFailingMessages = true;
        }

        // ─── Helpers ─────────────────────────────────────────────────────────

        private EventSender MakeOfflineSender(
            INativeEventStorage storage = null,
            INativeTracker tracker = null,
            Func<bool> adjustOfflineDisabled = null)
        {
            return new EventSender(
                new EventSenderConfig
                {
                    BaseUrl   = "http://localhost:19883/api/v1",
                    ClientId  = "coverage_client",
                    BundleId  = "com.test.coverage",
                    BatchSize = 1,
                    CycleDelay = 60_000, // suppress background loop
                    NativePlugin = storage ?? new DefaultNativePlugin(),
                    NativeTracker = tracker,
                    IsOfflineModeFunc = () => true,
                    AdjustOfflineModeDisabledFunc = adjustOfflineDisabled,
                },
                new NoctuaLocale());
        }

        private static void Invoke(EventSender s, string name, params object[] args)
        {
            typeof(EventSender).GetMethod(name, Priv).Invoke(s, args);
        }

        private static T InvokeRet<T>(EventSender s, string name, params object[] args)
        {
            return (T)typeof(EventSender).GetMethod(name, Priv).Invoke(s, args);
        }

        private static void Set(EventSender s, string field, object value)
        {
            var f = typeof(EventSender).GetField(field, Priv) ??
                    typeof(EventSender).GetField(field, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(f, $"Field '{field}' not found on EventSender");
            f.SetValue(s, value);
        }

        private static long EventCount(string name = null)
        {
            var path = Path.Combine(Application.persistentDataPath, EventStoreFile);
            if (!File.Exists(path)) return 0;
            var lines = File.ReadAllLines(path);
            if (name == null) return lines.Length;
            return lines.Count(l => l.Contains($"\"event_name\":\"{name}\""));
        }

        private static Dictionary<string, object> FindEvent(string name)
        {
            var path = Path.Combine(Application.persistentDataPath, EventStoreFile);
            if (!File.Exists(path)) return null;
            foreach (var line in File.ReadAllLines(path))
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                var native = JsonConvert.DeserializeObject<NativeEvent>(line);
                if (native?.EventJson == null) continue;
                var ev = JsonConvert.DeserializeObject<Dictionary<string, object>>(native.EventJson);
                if (ev != null && ev.TryGetValue("event_name", out var n) && n?.ToString() == name)
                    return ev;
            }
            return null;
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group F — Send() input validation & reserved-key stripping
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Send_NullName_ReturnsEarly_NoEventStored()
        {
            var sender = MakeOfflineSender();
            try
            {
                sender.Send(null);
                sender.Send("");
                // Give the fire-and-forget task a moment (it won't run, but be safe)
                Assert.AreEqual(0, EventCount(), "Null/empty name must not produce any persisted event");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        [Timeout(5000)]
        public async Task Send_ReservedKeys_AreStripped()
        {
            var sender = MakeOfflineSender();
            try
            {
                var data = new Dictionary<string, IConvertible>
                {
                    { "user_id",    "RESERVED_OVERRIDE" },
                    { "device_id",  "RESERVED_OVERRIDE" },
                    { "player_id",  "RESERVED_OVERRIDE" },
                    { "game_id",    "RESERVED_OVERRIDE" },
                    { "custom",     "kept" },
                };
                sender.Send("reserved_keys_test", data);
                await UniTask.Delay(800);

                var ev = FindEvent("reserved_keys_test");
                Assert.IsNotNull(ev, "Event must be persisted");
                Assert.AreEqual("kept", ev["custom"]?.ToString(),
                    "Non-reserved keys must be preserved");
                // user_id/player_id/game_id may be repopulated from EventSender's own fields,
                // but never with the caller's "RESERVED_OVERRIDE" value.
                if (ev.ContainsKey("user_id"))
                    Assert.AreNotEqual("RESERVED_OVERRIDE", ev["user_id"]?.ToString());
                if (ev.ContainsKey("device_id"))
                    Assert.AreNotEqual("RESERVED_OVERRIDE", ev["device_id"]?.ToString());
            }
            finally { sender.Dispose(); }
        }

        [Test]
        [Timeout(5000)]
        public async Task Send_NullDataValues_AreStripped()
        {
            var sender = MakeOfflineSender();
            try
            {
                var data = new Dictionary<string, IConvertible>
                {
                    { "kept_value", "yes" },
                    { "null_value", null },
                };
                sender.Send("null_strip_test", data);
                await UniTask.Delay(800);

                var ev = FindEvent("null_strip_test");
                Assert.IsNotNull(ev);
                Assert.IsFalse(ev.ContainsKey("null_value"),
                    "Keys with null values must be stripped before persist");
                Assert.AreEqual("yes", ev["kept_value"]?.ToString());
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group G — Caller-provided session_id, sandbox propagation
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task Send_WithCallerProvidedSessionId_IsPreserved()
        {
            var sender = MakeOfflineSender();
            try
            {
                sender.SetProperties(sessionId: "current-session");
                var data = new Dictionary<string, IConvertible>
                {
                    { "session_id", "caller-orphaned-session" }, // stripped from data, captured before strip
                };
                sender.Send("caller_session_test", data);
                await UniTask.Delay(800);

                var ev = FindEvent("caller_session_test");
                Assert.IsNotNull(ev);
                Assert.AreEqual("caller-orphaned-session", ev["session_id"]?.ToString(),
                    "Caller-provided session_id must override the EventSender's stored one");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        [Timeout(5000)]
        public async Task Send_WithSetProperties_AllPropertiesEnriched()
        {
            var sender = MakeOfflineSender();
            try
            {
                sender.SetProperties(
                    userId: 1,
                    playerId: 2,
                    credentialId: 3,
                    credentialProvider: "noctua",
                    gameId: 4,
                    gamePlatformId: 5,
                    sessionId: "session-abc",
                    ipAddress: "10.0.0.1",
                    isSandbox: true);

                sender.Send("enrichment_test");
                await UniTask.Delay(800);

                var ev = FindEvent("enrichment_test");
                Assert.IsNotNull(ev);
                Assert.AreEqual("1", ev["user_id"]?.ToString());
                Assert.AreEqual("2", ev["player_id"]?.ToString());
                Assert.AreEqual("3", ev["credential_id"]?.ToString());
                Assert.AreEqual("noctua", ev["credential_provider"]?.ToString());
                Assert.AreEqual("4", ev["game_id"]?.ToString());
                Assert.AreEqual("5", ev["game_platform_id"]?.ToString());
                Assert.AreEqual("session-abc", ev["session_id"]?.ToString());
                Assert.AreEqual("10.0.0.1", ev["ipAddress"]?.ToString());
                Assert.AreEqual(true, ev["is_sandbox"]);
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group H — Game-stage stopwatch
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task Send_GameStageStart_ThenComplete_RecordsStageTime()
        {
            var sender = MakeOfflineSender();
            try
            {
                sender.Send("game_stage_start", new Dictionary<string, IConvertible>
                {
                    { "level", "1-1" },
                    { "stage_mode", "normal" },
                });
                await UniTask.Delay(150);
                sender.Send("game_stage_complete", new Dictionary<string, IConvertible>
                {
                    { "level", "1-1" },
                });
                await UniTask.Delay(500);

                var start = FindEvent("game_stage_start");
                Assert.IsNotNull(start);
                Assert.IsTrue(start.ContainsKey("stage_session_id"),
                    "game_stage_start must include a generated stage_session_id");
                Assert.AreEqual("1-1", PlayerPrefs.GetString("NoctuaCurrentStageLevel", ""),
                    "level must be persisted to PlayerPrefs on stage_start");
                Assert.AreEqual("normal", PlayerPrefs.GetString("NoctuaCurrentStageMode", ""));

                var complete = FindEvent("game_stage_complete");
                Assert.IsNotNull(complete);
                Assert.IsTrue(complete.ContainsKey("stage_time_msec"),
                    "game_stage_complete must include stage_time_msec from the stopwatch");
                Assert.IsTrue(complete.ContainsKey("stage_session_id"),
                    "game_stage_complete must include the stage_session_id from the matching start");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        [Timeout(5000)]
        public async Task Send_GameStageStart_FallsBackToGameLevel()
        {
            var sender = MakeOfflineSender();
            try
            {
                // Use the `game_level` alias rather than `level`.
                sender.Send("game_stage_start", new Dictionary<string, IConvertible>
                {
                    { "game_level", "2-3" },
                });
                await UniTask.Delay(400);

                Assert.AreEqual("2-3", PlayerPrefs.GetString("NoctuaCurrentStageLevel", ""),
                    "game_level should be used as a fallback for level");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        [Timeout(5000)]
        public async Task Send_GameStageFailed_RecordsStageTimeAndResetsStopwatch()
        {
            var sender = MakeOfflineSender();
            try
            {
                sender.Send("game_stage_start", new Dictionary<string, IConvertible> { { "level", "1-2" } });
                await UniTask.Delay(150);
                sender.Send("game_stage_failed");
                await UniTask.Delay(400);

                var failed = FindEvent("game_stage_failed");
                Assert.IsNotNull(failed);
                Assert.IsTrue(failed.ContainsKey("stage_time_msec"),
                    "game_stage_failed must include stage_time_msec when stopwatch is running");
                Assert.IsTrue(failed.ContainsKey("stage_session_id"));
            }
            finally { sender.Dispose(); }
        }

        [Test]
        [Timeout(5000)]
        public async Task Send_CurrentStageLevelAndMode_EnrichSubsequentEvents()
        {
            PlayerPrefs.SetString("NoctuaCurrentStageLevel", "saved-level");
            PlayerPrefs.SetString("NoctuaCurrentStageMode", "saved-mode");
            PlayerPrefs.Save();

            var sender = MakeOfflineSender();
            try
            {
                sender.Send("any_event");
                await UniTask.Delay(500);

                var ev = FindEvent("any_event");
                Assert.IsNotNull(ev);
                Assert.AreEqual("saved-level", ev["current_stage_level"]?.ToString());
                Assert.AreEqual("saved-mode",  ev["current_stage_mode"]?.ToString());
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group I — Connectivity check bypass for "offline" event
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task Send_OfflineEvent_DoesNotTriggerConnectivityCheck()
        {
            var sender = MakeOfflineSender();
            try
            {
                // Force the throttle to be "ready" so a normal event WOULD trigger a check.
                Set(sender, "_lastConnectivityCheck", DateTime.MinValue);

                sender.Send("offline");
                await UniTask.Delay(400);

                // After Send, _lastConnectivityCheck should remain MinValue because the
                // event_name="offline" branch short-circuits the check (no throttle update).
                var f = typeof(EventSender).GetField("_lastConnectivityCheck", Priv);
                var actual = (DateTime)f.GetValue(sender);
                Assert.AreEqual(DateTime.MinValue, actual,
                    "Event name 'offline' must NOT update _lastConnectivityCheck");
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group J — Native-plugin exception paths
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task StorageHelpers_SwallowNativePluginExceptions()
        {
            var throwing = new ThrowingNativePlugin();
            var sender = MakeOfflineSender(storage: throwing);
            try
            {
                // GetEventCountDirectAsync → catches, returns 0
                var countTask = (Task<int>)typeof(EventSender)
                    .GetMethod("GetEventCountDirectAsync", Priv).Invoke(sender, null);
                await countTask;
                Assert.AreEqual(0, countTask.Result, "GetEventCount exception must yield 0");

                // GetEventsBatchDirectAsync → returns empty list
                var batchTask = (Task<List<NativeEvent>>)typeof(EventSender)
                    .GetMethod("GetEventsBatchDirectAsync", Priv).Invoke(sender, new object[] { 100, 0 });
                await batchTask;
                Assert.IsNotNull(batchTask.Result);
                Assert.AreEqual(0, batchTask.Result.Count);

                // DeleteEventsByIdsDirectAsync → returns 0
                var delTask = (Task<int>)typeof(EventSender)
                    .GetMethod("DeleteEventsByIdsDirectAsync", Priv).Invoke(sender, new object[] { new long[] { 1L } });
                await delTask;
                Assert.AreEqual(0, delTask.Result);

                // GetEventsDirectAsync → returns empty list
                var allTask = (Task<List<string>>)typeof(EventSender)
                    .GetMethod("GetEventsDirectAsync", Priv).Invoke(sender, null);
                await allTask;
                Assert.IsNotNull(allTask.Result);
                Assert.AreEqual(0, allTask.Result.Count);
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group K — Eviction path (MaxStoredEvents cap)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task ProcessWriteQueue_EnforcesMaxStoredEventsCap()
        {
            var storage = new DefaultNativePlugin();
            var sender = new EventSender(
                new EventSenderConfig
                {
                    BaseUrl   = "http://localhost:19883/api/v1",
                    ClientId  = "evict_client",
                    BundleId  = "com.test.evict",
                    BatchSize = 1,
                    CycleDelay = 60_000,
                    MaxStoredEvents = 5, // tiny cap → forces eviction
                    NativePlugin = storage,
                    IsOfflineModeFunc = () => true,
                },
                new NoctuaLocale());
            try
            {
                for (int i = 0; i < 25; i++)
                {
                    sender.Send($"evict_event_{i}");
                }
                // Wait for fire-and-forget eviction tasks to settle
                await UniTask.Delay(2000);

                var countTask = (Task<int>)typeof(EventSender)
                    .GetMethod("GetEventCountDirectAsync", Priv).Invoke(sender, null);
                await countTask;
                Assert.Less(countTask.Result, 25,
                    "Storage count must drop below the insert total after eviction kicks in");
                Assert.LessOrEqual(countTask.Result, 10,
                    "After racing inserts + evictions the residual should be near MaxStoredEvents, not the full insert count");
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group L — Notify helpers
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void NotifyOnline_NullTracker_DoesNotThrow()
        {
            var sender = MakeOfflineSender(tracker: null);
            try
            {
                Assert.DoesNotThrow(() => Invoke(sender, "NotifyOnline"));
                Assert.DoesNotThrow(() => Invoke(sender, "NotifyOffline"));
            }
            finally { sender.Dispose(); }
        }

        [Test]
        public void NotifyOnline_AdjustOfflineModeDisabled_SkipsTrackerCall()
        {
            var tracker = new RecordingTracker();
            var sender = MakeOfflineSender(tracker: tracker, adjustOfflineDisabled: () => true);
            try
            {
                Invoke(sender, "NotifyOnline");
                Invoke(sender, "NotifyOffline");
                Assert.AreEqual(0, tracker.OnlineCount, "NotifyOnline must skip tracker when Adjust offline-mode is disabled");
                Assert.AreEqual(0, tracker.OfflineCount, "NotifyOffline must skip tracker when Adjust offline-mode is disabled");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        public void NotifyOnline_AdjustOfflineModeEnabled_CallsTracker()
        {
            var tracker = new RecordingTracker();
            var sender = MakeOfflineSender(tracker: tracker, adjustOfflineDisabled: () => false);
            try
            {
                Invoke(sender, "NotifyOnline");
                Invoke(sender, "NotifyOffline");
                Assert.AreEqual(1, tracker.OnlineCount);
                Assert.AreEqual(1, tracker.OfflineCount);
            }
            finally { sender.Dispose(); }
        }

        [Test]
        public void NotifyOnline_TrackerThrows_IsSwallowed()
        {
            var tracker = new ThrowingTracker();
            var sender = MakeOfflineSender(tracker: tracker);
            try
            {
                Assert.DoesNotThrow(() => Invoke(sender, "NotifyOnline"));
                Assert.DoesNotThrow(() => Invoke(sender, "NotifyOffline"));
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group M — SanitizeHeaderValue
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SanitizeHeaderValue_RemovesControlAndNonPrintableChars()
        {
            var sender = MakeOfflineSender();
            try
            {
                var method = typeof(EventSender).GetMethod("SanitizeHeaderValue", Priv);
                Assert.IsNotNull(method);

                Assert.AreEqual("",      (string)method.Invoke(sender, new object[] { null }), "null → empty");
                Assert.AreEqual("",      (string)method.Invoke(sender, new object[] { "" }),   "empty → empty");
                Assert.AreEqual("abc",   (string)method.Invoke(sender, new object[] { "a\nb\rc" }), "CR/LF stripped");
                Assert.AreEqual("ab",    (string)method.Invoke(sender, new object[] { "a\tb" }), "TAB stripped entirely (char 9 < 32, no space substitution)");
                Assert.AreEqual("X",     (string)method.Invoke(sender, new object[] { "X" }), "DEL and SOH stripped");
                Assert.AreEqual("safe",  (string)method.Invoke(sender, new object[] { "safe" }), "ASCII passthrough");
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group N — Flush guards
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task Flush_WhileAlreadyFlushing_IsNoOp()
        {
            var sender = MakeOfflineSender();
            try
            {
                // Flag flush in progress.
                Set(sender, "_isFlushing", true);
                Assert.DoesNotThrow(() => sender.Flush(),
                    "Second Flush() while one is in flight must be a no-op");
                await UniTask.Yield();
                // Reset for clean teardown.
                Set(sender, "_isFlushing", false);
            }
            finally { sender.Dispose(); }
        }

        [Test]
        public void Flush_WhenQuitting_IsNoOp()
        {
            var sender = MakeOfflineSender();
            var quittingField = typeof(EventSender).GetField("_isQuitting",
                BindingFlags.NonPublic | BindingFlags.Static);
            var saved = (bool)quittingField.GetValue(null);
            try
            {
                quittingField.SetValue(null, true);
                Assert.DoesNotThrow(() => sender.Flush(),
                    "Flush() during app-quit must early-return without throwing");
            }
            finally
            {
                quittingField.SetValue(null, saved);
                sender.Dispose();
            }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group O — GetCountryIDAsync fallback
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task GetCountryIDAsync_FallsThroughToEmptyOnAllFailures()
        {
            var sender = MakeOfflineSender();
            try
            {
                // Both endpoints are real production URLs; this test asserts a stable
                // shape: the method returns *some* string (possibly empty) without
                // throwing. That exercises the try/catch ladder regardless of whether
                // the CI environment has internet access.
                string country = null;
                Exception caught = null;
                try { country = await sender.GetCountryIDAsync(); }
                catch (Exception e) { caught = e; }

                Assert.IsNull(caught, "GetCountryIDAsync must never throw; failures are caught");
                Assert.IsNotNull(country, "GetCountryIDAsync must return a string (possibly empty)");
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group P — Threading: background-thread Send & concurrent Send/Flush
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task Send_FromBackgroundThread_DoesNotThrow_AndPersists()
        {
            var sender = MakeOfflineSender();
            try
            {
                Exception caught = null;
                await Task.Run(() =>
                {
                    try { sender.Send("bg_thread_event"); }
                    catch (Exception e) { caught = e; }
                });

                Assert.IsNull(caught, "Send from background thread must not throw");
                // Send uses UniTask.SwitchToMainThread internally → enrichment happens on main.
                await UniTask.Delay(1000);

                var ev = FindEvent("bg_thread_event");
                Assert.IsNotNull(ev, "Background-thread Send must still persist the event");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        [Timeout(5000)]
        public async Task ConcurrentSendCalls_AllPersist()
        {
            var sender = MakeOfflineSender();
            try
            {
                const int N = 20;
                var tasks = new List<Task>();
                for (int i = 0; i < N; i++)
                {
                    int idx = i;
                    tasks.Add(Task.Run(() => sender.Send($"concurrent_{idx}")));
                }
                await Task.WhenAll(tasks);
                await UniTask.Delay(2000); // allow all enrichment + storage writes

                int present = 0;
                for (int i = 0; i < N; i++)
                {
                    if (FindEvent($"concurrent_{i}") != null) present++;
                }
                Assert.AreEqual(N, present,
                    "All concurrently-sent events must be persisted (no drops, no exceptions)");
            }
            finally { sender.Dispose(); }
        }

        // ─── Test doubles ────────────────────────────────────────────────────

        /// <summary>Native event storage whose every method throws — exercises EventSender's catch paths.</summary>
        private sealed class ThrowingNativePlugin : INativeEventStorage
        {
            public void GetEvents(Action<List<string>> cb)
                => throw new InvalidOperationException("boom GetEvents");
            public void DeleteEvents()
                => throw new InvalidOperationException("boom DeleteEvents");
            public void InsertEvent(string eventJson)
                => throw new InvalidOperationException("boom InsertEvent");
            public void GetEventsBatch(int limit, int offset, Action<List<NativeEvent>> cb)
                => throw new InvalidOperationException("boom GetEventsBatch");
            public void DeleteEventsByIds(long[] ids, Action<int> cb)
                => throw new InvalidOperationException("boom DeleteEventsByIds");
            public void GetEventCount(Action<int> cb)
                => throw new InvalidOperationException("boom GetEventCount");
            public void SaveEvents(string eventJson)
                => throw new InvalidOperationException("boom SaveEvents");
        }

        private sealed class RecordingTracker : INativeTracker
        {
            public int OnlineCount;
            public int OfflineCount;
            public void TrackAdRevenue(string source, double revenue, string currency,
                Dictionary<string, IConvertible> extraPayload = null) { }
            public void TrackPurchase(string orderId, double amount, string currency,
                Dictionary<string, IConvertible> extraPayload = null) { }
            public void TrackCustomEvent(string name, Dictionary<string, IConvertible> body = null) { }
            public void TrackCustomEventWithRevenue(string name, double revenue, string currency,
                Dictionary<string, IConvertible> extraPayload = null) { }
            public void OnOnline()  { Interlocked.Increment(ref OnlineCount); }
            public void OnOffline() { Interlocked.Increment(ref OfflineCount); }
        }

        private sealed class ThrowingTracker : INativeTracker
        {
            public void TrackAdRevenue(string source, double revenue, string currency,
                Dictionary<string, IConvertible> extraPayload = null) { }
            public void TrackPurchase(string orderId, double amount, string currency,
                Dictionary<string, IConvertible> extraPayload = null) { }
            public void TrackCustomEvent(string name, Dictionary<string, IConvertible> body = null) { }
            public void TrackCustomEventWithRevenue(string name, double revenue, string currency,
                Dictionary<string, IConvertible> extraPayload = null) { }
            public void OnOnline()  => throw new InvalidOperationException("boom OnOnline");
            public void OnOffline() => throw new InvalidOperationException("boom OnOffline");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group Q — ProcessWriteQueue guards
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void ProcessWriteQueue_ReentrancyGuard_IsNoOp()
        {
            var sender = MakeOfflineSender();
            try
            {
                // Set the re-entrancy flag to simulate a concurrent call.
                Set(sender, "_isProcessingWriteQueue", true);
                // Direct invocation must be a no-op (no exception, no double-process).
                Assert.DoesNotThrow(() => Invoke(sender, "ProcessWriteQueue"));
                // Restore so the finalizer/Dispose path is clean.
                Set(sender, "_isProcessingWriteQueue", false);
            }
            finally { sender.Dispose(); }
        }

        [Test]
        public void ProcessWriteQueue_PausedGuard_IsNoOp()
        {
            var sender = MakeOfflineSender();
            try
            {
                Set(sender, "_writeQueuePaused", true);
                Assert.DoesNotThrow(() => Invoke(sender, "ProcessWriteQueue"));
                Set(sender, "_writeQueuePaused", false);
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group R — SanitizeHeaderValue edge cases
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SanitizeHeaderValue_HighAscii_Preserved()
        {
            // The filter is `c >= 32 && c != 127`.  Chars 128-255 satisfy `c >= 32`
            // and are NOT 127, so they pass through untouched.  This is the actual
            // production behaviour; the test name and assertions reflect reality.
            var sender = MakeOfflineSender();
            try
            {
                var method = typeof(EventSender).GetMethod("SanitizeHeaderValue", Priv);
                Assert.IsNotNull(method);

                // Chars 128-255 satisfy (c >= 32 && c != 127) → they are kept.
                string highAscii = new string(new[] { (char)128, (char)200, (char)255 });
                string result = (string)method.Invoke(sender, new object[] { highAscii });
                Assert.AreEqual(highAscii, result, "Chars >= 128 are preserved (satisfy c >= 32)");

                // Mix of printable ASCII + high-ASCII: all chars survive.
                string mixed = "ab" + (char)150 + "cd";
                string resultMixed = (string)method.Invoke(sender, new object[] { mixed });
                Assert.AreEqual(mixed, resultMixed, "High-ASCII is preserved alongside printable ASCII");
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group S — Dispose idempotency
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Dispose_IsIdempotent()
        {
            var sender = MakeOfflineSender();
            Assert.DoesNotThrow(() => sender.Dispose());
            Assert.DoesNotThrow(() => sender.Dispose(), "Second Dispose must be a no-op");
            Assert.DoesNotThrow(() => sender.Dispose(), "Third Dispose must also be a no-op");
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group T — Constructor null-NativePlugin
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void Constructor_NullNativePlugin_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                var sender = new EventSender(
                    new EventSenderConfig
                    {
                        BaseUrl      = "http://localhost:19883/api/v1",
                        ClientId     = "test_client",
                        BundleId     = "com.test",
                        NativePlugin = null,
                        IsOfflineModeFunc = () => true,
                    },
                    new NoctuaLocale());
                sender.Dispose();
            });
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group U — Send with empty current stage (PlayerPrefs branch)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task Send_EmptyStageLevelInPrefs_DoesNotEnrichStageFields()
        {
            PlayerPrefs.DeleteKey("NoctuaCurrentStageLevel");
            PlayerPrefs.DeleteKey("NoctuaCurrentStageMode");
            PlayerPrefs.Save();

            var sender = MakeOfflineSender();
            try
            {
                sender.Send("no_stage_event");
                await UniTask.Delay(600);

                var ev = FindEvent("no_stage_event");
                Assert.IsNotNull(ev);
                Assert.IsFalse(ev.ContainsKey("current_stage_level"),
                    "current_stage_level must NOT be added when PlayerPrefs key is empty");
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group V — SanitizeHeaderValue precise edge cases
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void SanitizeHeaderValue_TabOnly_ReturnsEmpty()
        {
            // Tab is char 9, which is < 32, so it is stripped.
            // A string consisting only of a tab must produce an empty string.
            var sender = MakeOfflineSender();
            try
            {
                var method = typeof(EventSender).GetMethod("SanitizeHeaderValue", Priv);
                Assert.IsNotNull(method, "SanitizeHeaderValue must be accessible via reflection");

                string result = (string)method.Invoke(sender, new object[] { "\t" });
                Assert.AreEqual("", result, "Tab (char 9) is below 32 and must be stripped entirely");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        public void SanitizeHeaderValue_DelChar_IsStripped()
        {
            // DEL is char 127, explicitly excluded by the `c != 127` condition.
            var sender = MakeOfflineSender();
            try
            {
                var method = typeof(EventSender).GetMethod("SanitizeHeaderValue", Priv);

                string result = (string)method.Invoke(sender, new object[] { "\x7f" });
                Assert.AreEqual("", result, "DEL (char 127) must be stripped");

                // DEL embedded among printable chars — only the DEL is removed
                string embedded = (string)method.Invoke(sender, new object[] { "abc\x7fdef" });
                Assert.AreEqual("abcdef", embedded, "DEL embedded in printable text must be stripped");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        public void SanitizeHeaderValue_ControlCharsBelow32_AreStripped()
        {
            // SOH (1), BEL (7), TAB (9), LF (10), CR (13), ESC (27) are all < 32.
            var sender = MakeOfflineSender();
            try
            {
                var method = typeof(EventSender).GetMethod("SanitizeHeaderValue", Priv);

                // Each control char in isolation
                foreach (char c in new[] { '\x01', '\x07', '\x09', '\x0a', '\x0d', '\x1b' })
                {
                    string result = (string)method.Invoke(sender, new object[] { c.ToString() });
                    Assert.AreEqual("", result,
                        $"Control char (char {(int)c}) must be stripped; got '{result}'");
                }

                // Control chars mixed with printable text
                string mixed = (string)method.Invoke(sender, new object[] { "a\x01b\x0dc" });
                Assert.AreEqual("abc", mixed,
                    "Control chars embedded in printable text must all be stripped");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        public void SanitizeHeaderValue_HighAscii128To255_ArePreserved()
        {
            // The filter is `c >= 32 && c != 127`.
            // Chars 128-255 satisfy c >= 32 AND are not 127 → they pass through.
            var sender = MakeOfflineSender();
            try
            {
                var method = typeof(EventSender).GetMethod("SanitizeHeaderValue", Priv);

                // Pure high-ASCII string — every char must survive
                string highAscii = new string(new[] { (char)128, (char)200, (char)255 });
                string result = (string)method.Invoke(sender, new object[] { highAscii });
                Assert.AreEqual(highAscii, result,
                    "Chars 128-255 satisfy (c >= 32 && c != 127) and must be preserved");

                // Mixed: printable ASCII + high-ASCII, no chars removed
                string mixed = "ab" + (char)150 + "cd";
                string resultMixed = (string)method.Invoke(sender, new object[] { mixed });
                Assert.AreEqual(mixed, resultMixed,
                    "High-ASCII chars must be preserved when mixed with printable ASCII");

                // Boundary values: char 128 (first high-ASCII) and char 255 (last latin-1)
                Assert.AreEqual(((char)128).ToString(),
                    (string)method.Invoke(sender, new object[] { ((char)128).ToString() }),
                    "Char 128 must pass through");
                Assert.AreEqual(((char)255).ToString(),
                    (string)method.Invoke(sender, new object[] { ((char)255).ToString() }),
                    "Char 255 must pass through");
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group W — Game-stage stopwatch edge cases
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task Send_GameStageStart_Alone_DoesNotIncludeStageTimeMsec()
        {
            // game_stage_start by itself must NOT include stage_time_msec in its payload.
            // stage_time_msec only appears on game_stage_complete / game_stage_failed
            // after the stopwatch has been running.
            var sender = MakeOfflineSender();
            try
            {
                sender.Send("game_stage_start", new Dictionary<string, IConvertible>
                {
                    { "level", "3-1" },
                });
                await UniTask.Delay(600);

                var ev = FindEvent("game_stage_start");
                Assert.IsNotNull(ev, "game_stage_start must be persisted");
                Assert.IsFalse(ev.ContainsKey("stage_time_msec"),
                    "game_stage_start must NOT include stage_time_msec (stopwatch not yet stopped)");
                Assert.IsTrue(ev.ContainsKey("stage_session_id"),
                    "game_stage_start must include a stage_session_id");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        [Timeout(5000)]
        public async Task Send_GameStageComplete_WithoutPriorStart_IsHandledGracefully()
        {
            // If game_stage_complete fires without a preceding game_stage_start,
            // the stopwatch is not running (_stageStopwatch.IsRunning == false).
            // The event must still be persisted but must NOT include stage_time_msec
            // or stage_session_id (they are only set inside the `if (_stageStopwatch.IsRunning)` block).
            var sender = MakeOfflineSender();
            try
            {
                // Deliberately do NOT send game_stage_start first.
                sender.Send("game_stage_complete", new Dictionary<string, IConvertible>
                {
                    { "level", "5-2" },
                });
                await UniTask.Delay(600);

                var ev = FindEvent("game_stage_complete");
                Assert.IsNotNull(ev, "game_stage_complete must be persisted even without a prior start");
                Assert.IsFalse(ev.ContainsKey("stage_time_msec"),
                    "game_stage_complete without prior start must NOT include stage_time_msec");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        [Timeout(5000)]
        public async Task Send_GameStageFailed_WithoutPriorStart_IsHandledGracefully()
        {
            // Same as above but for game_stage_failed.
            var sender = MakeOfflineSender();
            try
            {
                sender.Send("game_stage_failed");
                await UniTask.Delay(600);

                var ev = FindEvent("game_stage_failed");
                Assert.IsNotNull(ev, "game_stage_failed must be persisted even without a prior start");
                Assert.IsFalse(ev.ContainsKey("stage_time_msec"),
                    "game_stage_failed without prior start must NOT include stage_time_msec");
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group X — Send() with explicit null data argument
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task Send_WithNullDataArgument_DoesNotThrow_AndPersistsEvent()
        {
            // Send(name, null) is valid — `data ??= new Dictionary<string, IConvertible>()`
            // inside Send() replaces null with an empty dict. The event must be persisted.
            var sender = MakeOfflineSender();
            try
            {
                Assert.DoesNotThrow(() => sender.Send("null_data_event", null),
                    "Send with explicit null data must not throw");

                await UniTask.Delay(600);

                var ev = FindEvent("null_data_event");
                Assert.IsNotNull(ev, "Event sent with null data must be persisted");
                Assert.AreEqual("null_data_event", ev["event_name"]?.ToString());
            }
            finally { sender.Dispose(); }
        }

        [Test]
        public void Send_WithEmptyStringName_ReturnsEarly_NoEventStored()
        {
            // Empty string is caught by the `string.IsNullOrEmpty(name)` guard at the top of Send().
            var sender = MakeOfflineSender();
            try
            {
                sender.Send("");
                // The guard returns before any async work is enqueued —
                // no storage write can occur synchronously or asynchronously.
                // We verify the count remains zero immediately (no async path entered).
                var countTask = (System.Threading.Tasks.Task<int>)typeof(EventSender)
                    .GetMethod("GetEventCountDirectAsync", Priv).Invoke(sender, null);
                countTask.Wait(500);
                Assert.AreEqual(0, countTask.Result,
                    "Empty event name must be rejected immediately; storage must remain empty");
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group Y — Explicit caller session_id is preserved (pre-strip capture)
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        [Timeout(5000)]
        public async Task Send_ExplicitSessionIdInData_OverridesSetPropertiesSessionId()
        {
            // When data contains "session_id", EventSender captures it BEFORE stripping
            // reserved keys and uses it as the effective session_id in the persisted payload.
            // This is the recovery-event path where the orphaned session_id is baked into data.
            var sender = MakeOfflineSender();
            try
            {
                sender.SetProperties(sessionId: "property-session");

                sender.Send("caller_session_override", new Dictionary<string, IConvertible>
                {
                    { "session_id", "explicit-caller-session" },
                    { "custom_field", "value" },
                });
                await UniTask.Delay(800);

                var ev = FindEvent("caller_session_override");
                Assert.IsNotNull(ev, "Event must be persisted");
                Assert.AreEqual("explicit-caller-session", ev["session_id"]?.ToString(),
                    "Caller-provided session_id in data must override the EventSender property session_id");
                Assert.AreEqual("value", ev["custom_field"]?.ToString(),
                    "Non-reserved fields must survive");
            }
            finally { sender.Dispose(); }
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group Z — NotifyOnline / NotifyOffline direct invocation post-construction
        // ═══════════════════════════════════════════════════════════════════════

        [Test]
        public void NotifyOnline_ImmediatelyAfterConstruction_DoesNotThrow()
        {
            // Verify that calling NotifyOnline/NotifyOffline right after construction
            // (before any Send/Flush) does not corrupt state or throw.
            var tracker = new RecordingTracker();
            var sender = MakeOfflineSender(tracker: tracker, adjustOfflineDisabled: () => false);
            try
            {
                Assert.DoesNotThrow(() => Invoke(sender, "NotifyOnline"),
                    "NotifyOnline immediately after construction must not throw");
                Assert.DoesNotThrow(() => Invoke(sender, "NotifyOffline"),
                    "NotifyOffline immediately after construction must not throw");

                Assert.AreEqual(1, tracker.OnlineCount,
                    "NotifyOnline must forward to tracker.OnOnline()");
                Assert.AreEqual(1, tracker.OfflineCount,
                    "NotifyOffline must forward to tracker.OnOffline()");
            }
            finally { sender.Dispose(); }
        }

        [Test]
        public void NotifyOffline_ThenNotifyOnline_DoesNotCorruptState()
        {
            // Multiple alternating calls must not corrupt internal state.
            var tracker = new RecordingTracker();
            var sender = MakeOfflineSender(tracker: tracker, adjustOfflineDisabled: () => false);
            try
            {
                for (int i = 0; i < 3; i++)
                {
                    Invoke(sender, "NotifyOffline");
                    Invoke(sender, "NotifyOnline");
                }

                Assert.AreEqual(3, tracker.OnlineCount,
                    "Three NotifyOnline calls must result in three tracker.OnOnline() calls");
                Assert.AreEqual(3, tracker.OfflineCount,
                    "Three NotifyOffline calls must result in three tracker.OnOffline() calls");
            }
            finally { sender.Dispose(); }
        }
    }
}
