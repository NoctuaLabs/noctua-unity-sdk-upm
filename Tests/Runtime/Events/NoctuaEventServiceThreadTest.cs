using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using com.noctuagames.sdk.Events;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Tests.Runtime.Events
{
    /// <summary>
    /// Thread-safety and survivability tests for <see cref="NoctuaEventService"/>.
    ///
    /// Group T — Thread-safety (5 tests)
    ///   T1. TrackCustomEvent from background thread does not throw
    ///   T2. TrackAdRevenue from background thread does not throw
    ///   T3. Concurrent TrackCustomEvent from 10 threads — all events delivered
    ///   T4. Concurrent SetProperties + TrackCustomEvent — no deadlock, events delivered
    ///   T5. SetCurrentFeature from background thread does not throw
    ///
    /// Group S — Survivability / edge cases (6 tests)
    ///   S1. NativeTracker throws during TrackAdRevenue — EventSender still receives event
    ///   S2. NativeTracker throws during TrackCustomEvent — EventSender still receives event
    ///   S3. NativeTracker throws during TrackCustomEventWithRevenue — EventSender still receives event
    ///   S4. TrackAdRevenue with zero revenue sends successfully
    ///   S5. TrackCustomEvent with null extraPayload does not throw
    ///   S6. TrackCustomEventWithRevenue with null extraPayload — revenue + currency injected
    /// </summary>
    [TestFixture]
    public class NoctuaEventServiceThreadTest
    {
        private MockNativeTrackerTs _nativeTracker;
        private MockEventSenderTs   _eventSender;

        [SetUp]
        public void SetUp()
        {
            _nativeTracker = new MockNativeTrackerTs();
            _eventSender   = new MockEventSenderTs();

            // Prevent ExperimentManager static state from leaking between tests
            ExperimentManager.Clear();
        }

        // ═══════════════════════════════════════════════════════════════════════
        // Group T — Thread-safety
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// T1 — TrackCustomEvent is safe to call from a Task.Run background thread.
        /// Ad event handlers fire on AppLovin / AdMob background threads, so this must
        /// not throw regardless of the calling thread.
        /// </summary>
        [UnityTest]
        public IEnumerator TrackCustomEvent_FromBackgroundThread_DoesNotThrow() =>
            UniTask.ToCoroutine(async () =>
            {
                var svc = new NoctuaEventService(_nativeTracker, _eventSender);
                Exception caught = null;

                await Task.Run(() =>
                {
                    try { svc.TrackCustomEvent("bg_custom_event", new Dictionary<string, IConvertible> { ["x"] = 1 }); }
                    catch (Exception e) { caught = e; }
                });

                Assert.IsNull(caught,
                    $"TrackCustomEvent from background thread must not throw. Got: {caught}");
                Assert.IsTrue(_eventSender.HasEvent("bg_custom_event"),
                    "TrackCustomEvent from background thread must still send the event");
            });

        /// <summary>
        /// T2 — TrackAdRevenue is safe to call from a Task.Run background thread.
        /// Revenue callbacks from AppLovin arrive on Unity's background render thread.
        /// </summary>
        [UnityTest]
        public IEnumerator TrackAdRevenue_FromBackgroundThread_DoesNotThrow() =>
            UniTask.ToCoroutine(async () =>
            {
                var svc = new NoctuaEventService(_nativeTracker, _eventSender);
                Exception caught = null;

                await Task.Run(() =>
                {
                    try { svc.TrackAdRevenue("applovin_max_sdk", 0.05, "USD"); }
                    catch (Exception e) { caught = e; }
                });

                Assert.IsNull(caught,
                    $"TrackAdRevenue from background thread must not throw. Got: {caught}");
                Assert.IsTrue(_eventSender.HasEvent("ad_revenue"),
                    "ad_revenue event must be sent even when called from background thread");
            });

        /// <summary>
        /// T3 — 10 concurrent Task.Run threads each call TrackCustomEvent once.
        /// All 10 events must arrive at the event sender without data corruption or loss.
        /// This exercises the payload-copy immutability guarantee and MockEventSenderTs
        /// ConcurrentBag safety.
        /// </summary>
        [UnityTest]
        public IEnumerator TrackCustomEvent_ConcurrentFromTenThreads_AllEventsDelivered() =>
            UniTask.ToCoroutine(async () =>
            {
                const int threadCount = 10;
                var svc  = new NoctuaEventService(_nativeTracker, _eventSender);
                var tasks = Enumerable.Range(0, threadCount)
                    .Select(i => Task.Run(() =>
                        svc.TrackCustomEvent("concurrent_event",
                            new Dictionary<string, IConvertible> { ["thread_id"] = i })))
                    .ToArray();

                await Task.WhenAll(tasks);

                Assert.AreEqual(threadCount, _eventSender.CountByName("concurrent_event"),
                    $"All {threadCount} concurrent TrackCustomEvent calls must reach the event sender");
            });

        /// <summary>
        /// T4 — Concurrent SetProperties (background) + TrackCustomEvent (multiple callers).
        /// SetProperties writes _country/_ipAddress/_isSandbox fields that AppendProperties reads.
        /// Verifies no deadlock, no throw, and all track events are delivered.
        /// </summary>
        [UnityTest]
        public IEnumerator SetProperties_ConcurrentWithTrackCustomEvent_NoDeadlockOrCorruption() =>
            UniTask.ToCoroutine(async () =>
            {
                const int trackCount = 20;
                var svc   = new NoctuaEventService(_nativeTracker, _eventSender);
                var cts   = new CancellationTokenSource();

                // Background: keep updating properties while Track calls are in-flight
                var propTask = Task.Run(async () =>
                {
                    int i = 0;
                    while (!cts.Token.IsCancellationRequested)
                    {
                        svc.SetProperties(country: i % 2 == 0 ? "ID" : "SG");
                        i++;
                        await Task.Delay(5, cts.Token).ContinueWith(_ => { }); // swallow cancellation
                    }
                });

                // Foreground: send 20 events from multiple threads simultaneously
                var trackTasks = Enumerable.Range(0, trackCount)
                    .Select(i => Task.Run(() => svc.TrackCustomEvent("prop_race_event",
                        new Dictionary<string, IConvertible> { ["seq"] = i })))
                    .ToArray();

                await Task.WhenAll(trackTasks);
                cts.Cancel();
                await propTask;

                Assert.AreEqual(trackCount, _eventSender.CountByName("prop_race_event"),
                    "All events must reach the sender despite concurrent SetProperties calls");
            });

        /// <summary>
        /// T5 — SetCurrentFeature from a background thread must not throw.
        /// The game may call SetCurrentFeature in a non-main-thread callback (e.g. game
        /// state machine driven by networking).
        /// </summary>
        [UnityTest]
        public IEnumerator SetCurrentFeature_FromBackgroundThread_DoesNotThrow() =>
            UniTask.ToCoroutine(async () =>
            {
                var svc   = new NoctuaEventService(_nativeTracker, _eventSender);
                Exception caught = null;

                await Task.Run(() =>
                {
                    try
                    {
                        svc.SetCurrentFeature("battle_screen");
                        svc.SetCurrentFeature(""); // clear
                    }
                    catch (Exception e)
                    {
                        caught = e;
                    }
                });

                Assert.IsNull(caught,
                    $"SetCurrentFeature from background thread must not throw. Got: {caught}");
            });

        // ═══════════════════════════════════════════════════════════════════════
        // Group S — Survivability / edge cases
        // ═══════════════════════════════════════════════════════════════════════

        /// <summary>
        /// S1 — If _nativeTracker.TrackAdRevenue() throws, the ad_revenue event must
        /// still be delivered to EventSender. Without this guarantee, a crashing Adjust
        /// or Facebook SDK silently drops revenue from the analytics pipeline.
        /// </summary>
        [Test]
        public void TrackAdRevenue_NativeTrackerThrows_EventSenderStillReceivesEvent()
        {
            var throwingTracker = new ThrowingNativeTracker();
            var svc = new NoctuaEventService(throwingTracker, _eventSender);

            // ThrowingNativeTracker.TrackAdRevenue() throws — must be caught internally
            Assert.DoesNotThrow(() => svc.TrackAdRevenue("admob", 0.02, "USD"),
                "TrackAdRevenue must not propagate native tracker exceptions");

            Assert.IsTrue(_eventSender.HasEvent("ad_revenue"),
                "ad_revenue event must still reach EventSender even when native tracker throws");

            var evt = _eventSender.GetAll("ad_revenue").First();
            Assert.AreEqual("admob", evt["source"],   "source field must be present");
            Assert.AreEqual(0.02,    evt["revenue"],  "revenue field must be present");
            Assert.AreEqual("USD",   evt["currency"], "currency field must be present");
        }

        /// <summary>
        /// S2 — If _nativeTracker.TrackCustomEvent() throws, the event must still reach
        /// EventSender. A broken Adjust integration must not block custom event analytics.
        /// </summary>
        [Test]
        public void TrackCustomEvent_NativeTrackerThrows_EventSenderStillReceivesEvent()
        {
            var throwingTracker = new ThrowingNativeTracker();
            var svc = new NoctuaEventService(throwingTracker, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackCustomEvent("level_complete"),
                "TrackCustomEvent must not propagate native tracker exceptions");

            Assert.IsTrue(_eventSender.HasEvent("level_complete"),
                "level_complete event must still reach EventSender even when native tracker throws");
        }

        /// <summary>
        /// S3 — If _nativeTracker.TrackCustomEventWithRevenue() throws, the event with
        /// revenue must still reach EventSender.
        /// </summary>
        [Test]
        public void TrackCustomEventWithRevenue_NativeTrackerThrows_EventSenderStillReceivesEvent()
        {
            var throwingTracker = new ThrowingNativeTracker();
            var svc = new NoctuaEventService(throwingTracker, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackCustomEventWithRevenue("special_offer", 1.99, "EUR"),
                "TrackCustomEventWithRevenue must not propagate native tracker exceptions");

            Assert.IsTrue(_eventSender.HasEvent("special_offer"),
                "special_offer event must still reach EventSender even when native tracker throws");

            var evt = _eventSender.GetAll("special_offer").First();
            Assert.AreEqual(1.99,  evt["revenue"],  "revenue must be injected even after tracker throw");
            Assert.AreEqual("EUR", evt["currency"], "currency must be injected even after tracker throw");
        }

        /// <summary>
        /// S4 — Zero revenue is a valid value (ad impression with $0 fill).
        /// Must send without any guard filtering it out.
        /// </summary>
        [Test]
        public void TrackAdRevenue_ZeroRevenue_SendsSuccessfully()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackAdRevenue("admob_sdk", 0.0, "USD"));
            Assert.IsTrue(_eventSender.HasEvent("ad_revenue"),
                "Zero-revenue ad_revenue event must still be sent");

            var evt = _eventSender.GetAll("ad_revenue").First();
            Assert.AreEqual(0.0, evt["revenue"], "revenue must be exactly 0.0");
        }

        /// <summary>
        /// S5 — TrackCustomEvent with null extraPayload must not throw and must send
        /// the event with no extra keys (only AppendProperties context if set).
        /// </summary>
        [Test]
        public void TrackCustomEvent_NullExtraPayload_DoesNotThrow()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackCustomEvent("null_payload_event", null),
                "null extraPayload must be treated as empty dictionary");
            Assert.IsTrue(_eventSender.HasEvent("null_payload_event"),
                "Event must still be sent with null extraPayload");
        }

        /// <summary>
        /// S6 — TrackCustomEventWithRevenue with null extraPayload must inject revenue
        /// and currency into the sent payload even when no extra keys are provided.
        /// </summary>
        [Test]
        public void TrackCustomEventWithRevenue_NullExtraPayload_InjectsRevenueAndCurrency()
        {
            var svc = new NoctuaEventService(_nativeTracker, _eventSender);

            Assert.DoesNotThrow(() => svc.TrackCustomEventWithRevenue("offer_purchase", 4.99, "USD", null));

            Assert.IsTrue(_eventSender.HasEvent("offer_purchase"),
                "offer_purchase must be sent with null extraPayload");

            var evt = _eventSender.GetAll("offer_purchase").First();
            Assert.IsTrue(evt.ContainsKey("revenue"),  "revenue must be injected");
            Assert.IsTrue(evt.ContainsKey("currency"), "currency must be injected");
            Assert.AreEqual(4.99,  evt["revenue"]);
            Assert.AreEqual("USD", evt["currency"]);
        }
    }

    // ─── Thread-safe mocks for NoctuaEventServiceThreadTest ──────────────────

    /// <summary>
    /// Thread-safe mock INativeTracker that records all calls via ConcurrentBag.
    /// Used exclusively by NoctuaEventServiceThreadTest.
    /// </summary>
    public class MockNativeTrackerTs : INativeTracker
    {
        private readonly ConcurrentBag<string> _calledMethods = new ConcurrentBag<string>();

        public IEnumerable<string> CalledMethods => _calledMethods;

        public void TrackAdRevenue(string source, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
            => _calledMethods.Add("TrackAdRevenue");

        public void TrackPurchase(string orderId, double amount, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
            => _calledMethods.Add("TrackPurchase");

        public void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
            => _calledMethods.Add("TrackCustomEvent");

        public void TrackCustomEventWithRevenue(string name, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
            => _calledMethods.Add("TrackCustomEventWithRevenue");

        public void OnOnline()  => _calledMethods.Add("OnOnline");
        public void OnOffline() => _calledMethods.Add("OnOffline");

        public bool WasCalled(string method) => _calledMethods.Contains(method);
    }

    /// <summary>
    /// Thread-safe mock IEventSender backed by a ConcurrentBag.
    /// All public members are safe to call from any thread.
    /// Used exclusively by NoctuaEventServiceThreadTest.
    /// </summary>
    public class MockEventSenderTs : IEventSender
    {
        private readonly ConcurrentBag<(string Name, Dictionary<string, IConvertible> Data)> _events
            = new ConcurrentBag<(string, Dictionary<string, IConvertible>)>();

        public string PseudoUserId => "mock-ts-pseudo-id";

        public void Send(string name, Dictionary<string, IConvertible> data = null)
        {
            // Snapshot the dictionary — the caller owns the reference and may modify it later.
            // An independent copy guarantees the recorded event is a stable snapshot.
            var snapshot = data != null
                ? new Dictionary<string, IConvertible>(data)
                : new Dictionary<string, IConvertible>();
            _events.Add((name, snapshot));
        }

        public void SetProperties(
            long? userId = 0, long? playerId = 0, long? credentialId = 0,
            string credentialProvider = "", long? gameId = 0, long? gamePlatformId = 0,
            string sessionId = "", string ipAddress = "", bool? isSandbox = null) { }

        public void Flush() { }

        public bool HasEvent(string name) =>
            _events.Any(e => e.Name == name);

        public int CountByName(string name) =>
            _events.Count(e => e.Name == name);

        public List<Dictionary<string, IConvertible>> GetAll(string name) =>
            _events.Where(e => e.Name == name).Select(e => e.Data).ToList();
    }

    /// <summary>
    /// INativeTracker that always throws InvalidOperationException.
    /// Used to verify that NoctuaEventService does not propagate native tracker failures
    /// and still delivers events to the EventSender (survivability S1/S2/S3).
    /// </summary>
    public class ThrowingNativeTracker : INativeTracker
    {
        public void TrackAdRevenue(string source, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
            => throw new InvalidOperationException("Simulated native tracker failure in TrackAdRevenue");

        public void TrackPurchase(string orderId, double amount, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
            => throw new InvalidOperationException("Simulated native tracker failure in TrackPurchase");

        public void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
            => throw new InvalidOperationException($"Simulated native tracker failure in TrackCustomEvent('{name}')");

        public void TrackCustomEventWithRevenue(string name, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
            => throw new InvalidOperationException($"Simulated native tracker failure in TrackCustomEventWithRevenue('{name}')");

        public void OnOnline()  => throw new InvalidOperationException("Simulated failure in OnOnline");
        public void OnOffline() => throw new InvalidOperationException("Simulated failure in OnOffline");
    }
}
