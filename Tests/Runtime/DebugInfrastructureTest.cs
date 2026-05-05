using System;
using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for debug/inspector infrastructure types:
    ///   * <see cref="TrackerDebugMonitor"/> — ring buffer, phase correlation, Pump, Snapshot, Clear
    ///   * <see cref="TrackerObserverRegistry"/> — register/unregister/emit fan-out
    ///   * <see cref="TrackerEventPhaseEx"/> — IsTerminal, FromRaw
    ///   * <see cref="HttpInspectorLog"/> — ring buffer, Pump, Snapshot, Clear, state change
    ///   * <see cref="InspectorJson"/> — Deserialize edge cases
    /// </summary>
    [TestFixture]
    public class DebugInfrastructureTest
    {
        // ─── Helpers ──────────────────────────────────────────────────────────

        private static void Emit(
            TrackerDebugMonitor monitor,
            string provider,
            string eventName,
            TrackerEventPhase phase,
            string error = null)
        {
            monitor.OnEvent(provider, eventName, null, null, phase, error);
            monitor.Pump();   // drain main-thread work queue synchronously
        }

        // ─── TrackerEventPhaseEx ──────────────────────────────────────────────

        [Test]
        public void TrackerEventPhaseEx_IsTerminal_TrueForTerminalPhases()
        {
            Assert.IsTrue(TrackerEventPhase.Acknowledged.IsTerminal());
            Assert.IsTrue(TrackerEventPhase.Failed.IsTerminal());
            Assert.IsTrue(TrackerEventPhase.TimedOut.IsTerminal());
        }

        [Test]
        public void TrackerEventPhaseEx_IsTerminal_FalseForNonTerminalPhases()
        {
            Assert.IsFalse(TrackerEventPhase.Queued.IsTerminal());
            Assert.IsFalse(TrackerEventPhase.Sending.IsTerminal());
            Assert.IsFalse(TrackerEventPhase.Emitted.IsTerminal());
            Assert.IsFalse(TrackerEventPhase.Uploading.IsTerminal());
        }

        [Test]
        public void TrackerEventPhaseEx_FromRaw_ValidValues_MapCorrectly()
        {
            Assert.AreEqual(TrackerEventPhase.Queued,       TrackerEventPhaseEx.FromRaw(0));
            Assert.AreEqual(TrackerEventPhase.Sending,      TrackerEventPhaseEx.FromRaw(1));
            Assert.AreEqual(TrackerEventPhase.Emitted,      TrackerEventPhaseEx.FromRaw(2));
            Assert.AreEqual(TrackerEventPhase.Uploading,    TrackerEventPhaseEx.FromRaw(3));
            Assert.AreEqual(TrackerEventPhase.Acknowledged, TrackerEventPhaseEx.FromRaw(4));
            Assert.AreEqual(TrackerEventPhase.Failed,       TrackerEventPhaseEx.FromRaw(5));
            Assert.AreEqual(TrackerEventPhase.TimedOut,     TrackerEventPhaseEx.FromRaw(6));
        }

        [Test]
        public void TrackerEventPhaseEx_FromRaw_OutOfRange_ReturnsQueued()
        {
            Assert.AreEqual(TrackerEventPhase.Queued, TrackerEventPhaseEx.FromRaw(-1));
            Assert.AreEqual(TrackerEventPhase.Queued, TrackerEventPhaseEx.FromRaw(99));
        }

        // ─── TrackerEventPhase enum ordinals ──────────────────────────────────

        [Test]
        public void TrackerEventPhase_Ordinals_AreCorrect()
        {
            Assert.AreEqual(0, (int)TrackerEventPhase.Queued);
            Assert.AreEqual(1, (int)TrackerEventPhase.Sending);
            Assert.AreEqual(2, (int)TrackerEventPhase.Emitted);
            Assert.AreEqual(3, (int)TrackerEventPhase.Uploading);
            Assert.AreEqual(4, (int)TrackerEventPhase.Acknowledged);
            Assert.AreEqual(5, (int)TrackerEventPhase.Failed);
            Assert.AreEqual(6, (int)TrackerEventPhase.TimedOut);
        }

        // ─── TrackerDebugMonitor — basic ──────────────────────────────────────

        [Test]
        public void TrackerDebugMonitor_NewInstance_SnapshotIsEmpty()
        {
            var monitor = new TrackerDebugMonitor();
            Assert.AreEqual(0, monitor.Snapshot().Count);
        }

        [Test]
        public void TrackerDebugMonitor_Queued_AppearsInSnapshot()
        {
            var monitor = new TrackerDebugMonitor();
            Emit(monitor, "adjust", "level_up", TrackerEventPhase.Queued);

            var snap = monitor.Snapshot();
            Assert.AreEqual(1, snap.Count);
            Assert.AreEqual("adjust",   snap[0].Provider);
            Assert.AreEqual("level_up", snap[0].EventName);
            Assert.AreEqual(TrackerEventPhase.Queued, snap[0].Phase);
        }

        [Test]
        public void TrackerDebugMonitor_MultipleEvents_AppearInOrder()
        {
            var monitor = new TrackerDebugMonitor();
            Emit(monitor, "adjust",   "event_a", TrackerEventPhase.Queued);
            Emit(monitor, "firebase", "event_b", TrackerEventPhase.Queued);

            var snap = monitor.Snapshot();
            Assert.AreEqual(2,         snap.Count);
            Assert.AreEqual("event_a", snap[0].EventName);
            Assert.AreEqual("event_b", snap[1].EventName);
        }

        [Test]
        public void TrackerDebugMonitor_Clear_EmptiesBuffer()
        {
            var monitor = new TrackerDebugMonitor();
            Emit(monitor, "adjust", "level_up", TrackerEventPhase.Queued);

            monitor.Clear();

            Assert.AreEqual(0, monitor.Snapshot().Count);
        }

        // ─── TrackerDebugMonitor — phase correlation ──────────────────────────

        [Test]
        public void TrackerDebugMonitor_QueuedThenAcknowledged_UpdatesExistingEntry()
        {
            var monitor = new TrackerDebugMonitor();
            Emit(monitor, "adjust", "purchase", TrackerEventPhase.Queued);
            Emit(monitor, "adjust", "purchase", TrackerEventPhase.Acknowledged);

            var snap = monitor.Snapshot();
            // Still just one entry (the Acknowledged updated the same row)
            Assert.AreEqual(1, snap.Count);
            Assert.AreEqual(TrackerEventPhase.Acknowledged, snap[0].Phase);
        }

        [Test]
        public void TrackerDebugMonitor_QueuedThenFailed_RecordsError()
        {
            var monitor = new TrackerDebugMonitor();
            Emit(monitor, "adjust", "purchase", TrackerEventPhase.Queued);
            Emit(monitor, "adjust", "purchase", TrackerEventPhase.Failed, error: "timeout");

            var snap = monitor.Snapshot();
            Assert.AreEqual(1,        snap.Count);
            Assert.AreEqual(TrackerEventPhase.Failed, snap[0].Phase);
            Assert.AreEqual("timeout", snap[0].Error);
        }

        [Test]
        public void TrackerDebugMonitor_QueuedThenNonTerminal_RemainsInPending()
        {
            // Sending (non-terminal) should update the row but keep it pending
            // so a subsequent Acknowledged still correlates.
            var monitor = new TrackerDebugMonitor();
            Emit(monitor, "adjust", "event_x", TrackerEventPhase.Queued);
            Emit(monitor, "adjust", "event_x", TrackerEventPhase.Sending);
            Emit(monitor, "adjust", "event_x", TrackerEventPhase.Acknowledged);

            var snap = monitor.Snapshot();
            Assert.AreEqual(1, snap.Count);
            Assert.AreEqual(TrackerEventPhase.Acknowledged, snap[0].Phase);
        }

        [Test]
        public void TrackerDebugMonitor_AcknowledgedWithoutPriorQueued_AddsNewEntry()
        {
            // Standalone terminal signal — synthetic entry created
            var monitor = new TrackerDebugMonitor();
            Emit(monitor, "adjust", "orphan_event", TrackerEventPhase.Acknowledged);

            var snap = monitor.Snapshot();
            Assert.AreEqual(1, snap.Count);
            Assert.AreEqual(TrackerEventPhase.Acknowledged, snap[0].Phase);
        }

        [Test]
        public void TrackerDebugMonitor_TwoSameProviderEventName_CorrelateIndependently()
        {
            // Two separate queued events with same provider+eventName should stay separate.
            var monitor = new TrackerDebugMonitor();
            Emit(monitor, "adjust", "purchase", TrackerEventPhase.Queued);
            Emit(monitor, "adjust", "purchase", TrackerEventPhase.Queued);
            Emit(monitor, "adjust", "purchase", TrackerEventPhase.Acknowledged); // resolves first

            var snap = monitor.Snapshot();
            Assert.AreEqual(2, snap.Count);
        }

        // ─── TrackerDebugMonitor — snapshot filter ────────────────────────────

        [Test]
        public void TrackerDebugMonitor_Snapshot_FilterByProvider()
        {
            var monitor = new TrackerDebugMonitor();
            Emit(monitor, "adjust",   "event_a", TrackerEventPhase.Queued);
            Emit(monitor, "firebase", "event_b", TrackerEventPhase.Queued);

            var adjustOnly = monitor.Snapshot("adjust");
            Assert.AreEqual(1,         adjustOnly.Count);
            Assert.AreEqual("event_a", adjustOnly[0].EventName);

            var firebaseOnly = monitor.Snapshot("firebase");
            Assert.AreEqual(1,         firebaseOnly.Count);
            Assert.AreEqual("event_b", firebaseOnly[0].EventName);
        }

        [Test]
        public void TrackerDebugMonitor_Snapshot_NullFilter_ReturnsAll()
        {
            var monitor = new TrackerDebugMonitor();
            Emit(monitor, "adjust",   "event_a", TrackerEventPhase.Queued);
            Emit(monitor, "firebase", "event_b", TrackerEventPhase.Queued);

            Assert.AreEqual(2, monitor.Snapshot(null).Count);
        }

        // ─── TrackerDebugMonitor — ring buffer ────────────────────────────────

        [Test]
        public void TrackerDebugMonitor_CapacityEviction_DropsOldest()
        {
            const int cap = 5;
            var monitor = new TrackerDebugMonitor(capacity: cap);

            for (int i = 0; i < cap + 2; i++)
                Emit(monitor, "adjust", $"event_{i}", TrackerEventPhase.Queued);

            var snap = monitor.Snapshot();
            Assert.AreEqual(cap, snap.Count, "Buffer should cap at capacity");
            // Oldest 2 entries (event_0, event_1) were evicted
            Assert.AreEqual("event_2", snap[0].EventName);
            Assert.AreEqual("event_6", snap[cap - 1].EventName);
        }

        // ─── TrackerDebugMonitor — OnEmission event ───────────────────────────

        [Test]
        public void TrackerDebugMonitor_OnEmission_FiredAfterPump()
        {
            var monitor = new TrackerDebugMonitor();
            TrackerEmission received = null;
            monitor.OnEmission += em => received = em;

            monitor.OnEvent("adjust", "purchase", null, null, TrackerEventPhase.Queued, null);
            // Before Pump, nothing should have fired yet
            Assert.IsNull(received, "OnEmission should not fire before Pump");

            monitor.Pump();
            Assert.IsNotNull(received, "OnEmission should fire after Pump");
            Assert.AreEqual("purchase", received.EventName);
        }

        // ─── TrackerObserverRegistry ──────────────────────────────────────────
        // NOTE: TrackerObserverRegistry is a static class — use try/finally in
        // each test to guarantee cleanup even on test failure.

        [Test]
        public void TrackerObserverRegistry_Register_HasObservers_True()
        {
            var observer = new FakeTrackerObserver();
            TrackerObserverRegistry.Register(observer);
            try
            {
                Assert.IsTrue(TrackerObserverRegistry.HasObservers);
            }
            finally
            {
                TrackerObserverRegistry.Unregister(observer);
            }
        }

        [Test]
        public void TrackerObserverRegistry_DuplicateRegister_CountsOnce()
        {
            var observer = new FakeTrackerObserver();
            TrackerObserverRegistry.Register(observer);
            TrackerObserverRegistry.Register(observer); // duplicate — should be ignored
            try
            {
                TrackerObserverRegistry.Emit("test", "event", null, null, TrackerEventPhase.Queued);
                Assert.AreEqual(1, observer.CallCount, "Observer should be called once, not twice");
            }
            finally
            {
                TrackerObserverRegistry.Unregister(observer);
            }
        }

        [Test]
        public void TrackerObserverRegistry_RegisterNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => TrackerObserverRegistry.Register(null));
        }

        [Test]
        public void TrackerObserverRegistry_Emit_FansOutToRegisteredObservers()
        {
            var obs1 = new FakeTrackerObserver();
            var obs2 = new FakeTrackerObserver();
            TrackerObserverRegistry.Register(obs1);
            TrackerObserverRegistry.Register(obs2);
            try
            {
                TrackerObserverRegistry.Emit("adjust", "purchase", null, null, TrackerEventPhase.Queued);
                Assert.AreEqual(1, obs1.CallCount);
                Assert.AreEqual(1, obs2.CallCount);
            }
            finally
            {
                TrackerObserverRegistry.Unregister(obs1);
                TrackerObserverRegistry.Unregister(obs2);
            }
        }

        [Test]
        public void TrackerObserverRegistry_EmitAfterUnregister_ObserverNotCalled()
        {
            var observer = new FakeTrackerObserver();
            TrackerObserverRegistry.Register(observer);
            TrackerObserverRegistry.Unregister(observer);

            // Emit should not reach the observer after unregistration
            TrackerObserverRegistry.Emit("adjust", "event", null, null, TrackerEventPhase.Queued);
            Assert.AreEqual(0, observer.CallCount);
        }

        // ─── HttpInspectorLog — basic ─────────────────────────────────────────

        private static HttpExchange MakeExchange(string url = "https://api.example.com/test")
        {
            return new HttpExchange
            {
                Id      = Guid.NewGuid(),
                Method  = "POST",
                Url     = url,
                State   = HttpExchangeState.Building,
            };
        }

        [Test]
        public void HttpInspectorLog_NewInstance_SnapshotIsEmpty()
        {
            var log = new HttpInspectorLog();
            Assert.AreEqual(0, log.Snapshot().Count);
        }

        [Test]
        public void HttpInspectorLog_OnRequestStart_AfterPump_AppearsInSnapshot()
        {
            var log = new HttpInspectorLog();
            var ex  = MakeExchange();

            log.OnRequestStart(ex);
            log.Pump();

            var snap = log.Snapshot();
            Assert.AreEqual(1, snap.Count);
            Assert.AreEqual(ex.Id, snap[0].Id);
        }

        [Test]
        public void HttpInspectorLog_OnRequestEnd_UpdatesExistingEntry()
        {
            var log = new HttpInspectorLog();
            var ex  = MakeExchange();

            log.OnRequestStart(ex);
            log.Pump();

            ex.State  = HttpExchangeState.Complete;
            ex.Status = 200;
            log.OnRequestEnd(ex);
            log.Pump();

            var snap = log.Snapshot();
            // Same exchange ID — should still be one entry
            Assert.AreEqual(1, snap.Count);
            Assert.AreEqual(200,                       snap[0].Status);
            Assert.AreEqual(HttpExchangeState.Complete, snap[0].State);
        }

        [Test]
        public void HttpInspectorLog_OnStateChange_UpdatesState()
        {
            var log = new HttpInspectorLog();
            var ex  = MakeExchange();

            log.OnRequestStart(ex);
            log.Pump();

            log.OnStateChange(ex.Id, HttpExchangeState.Sending);
            log.Pump();

            var snap = log.Snapshot();
            Assert.AreEqual(HttpExchangeState.Sending, snap[0].State);
        }

        [Test]
        public void HttpInspectorLog_OnStateChange_UnknownId_NoException()
        {
            var log = new HttpInspectorLog();
            Assert.DoesNotThrow(() =>
            {
                log.OnStateChange(Guid.NewGuid(), HttpExchangeState.Failed);
                log.Pump();
            });
        }

        [Test]
        public void HttpInspectorLog_Clear_EmptiesBuffer()
        {
            var log = new HttpInspectorLog();
            log.OnRequestStart(MakeExchange());
            log.Pump();

            log.Clear();

            Assert.AreEqual(0, log.Snapshot().Count);
        }

        [Test]
        public void HttpInspectorLog_MultipleExchanges_AllAppear()
        {
            var log = new HttpInspectorLog();
            log.OnRequestStart(MakeExchange("https://a.example.com"));
            log.OnRequestStart(MakeExchange("https://b.example.com"));
            log.Pump();

            Assert.AreEqual(2, log.Snapshot().Count);
        }

        [Test]
        public void HttpInspectorLog_CapacityEviction_DropsOldest()
        {
            var log = new HttpInspectorLog();
            var firstId = Guid.Empty;

            for (int i = 0; i < HttpInspectorLog.Capacity + 3; i++)
            {
                var ex = MakeExchange($"https://api.example.com/e{i}");
                if (i == 0) firstId = ex.Id;
                log.OnRequestStart(ex);
            }
            log.Pump();

            var snap = log.Snapshot();
            Assert.AreEqual(HttpInspectorLog.Capacity, snap.Count);
            // Oldest entry should be gone
            foreach (var e in snap)
                Assert.AreNotEqual(firstId, e.Id, "Oldest entry should have been evicted");
        }

        [Test]
        public void HttpInspectorLog_OnExchangeEvent_FiredAfterPump()
        {
            var log = new HttpInspectorLog();
            HttpExchange received = null;
            log.OnExchange += ex => received = ex;

            var exchange = MakeExchange();
            log.OnRequestStart(exchange);

            Assert.IsNull(received, "OnExchange should not fire before Pump");
            log.Pump();
            Assert.IsNotNull(received, "OnExchange should fire after Pump");
            Assert.AreEqual(exchange.Id, received.Id);
        }

        // ─── InspectorJson ────────────────────────────────────────────────────

        [Test]
        public void InspectorJson_Null_ReturnsEmptyDictionary()
        {
            var result = InspectorJson.Deserialize(null);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void InspectorJson_EmptyString_ReturnsEmptyDictionary()
        {
            var result = InspectorJson.Deserialize("");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void InspectorJson_EmptyObject_ReturnsEmptyDictionary()
        {
            var result = InspectorJson.Deserialize("{}");
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void InspectorJson_ValidJson_ParsesAllTypes()
        {
            const string json = @"{
                ""str_val"": ""hello"",
                ""int_val"": 42,
                ""float_val"": 3.14,
                ""bool_val"": true,
                ""null_val"": null
            }";

            var result = InspectorJson.Deserialize(json);

            Assert.AreEqual("hello", result["str_val"]);
            Assert.AreEqual(42L,     result["int_val"]);       // integers → long
            Assert.AreEqual(3.14,    (double)result["float_val"], delta: 0.001);
            Assert.AreEqual(true,    result["bool_val"]);
            Assert.IsNull(result["null_val"]);
        }

        [Test]
        public void InspectorJson_InvalidJson_ReturnsEmptyDictionary()
        {
            var result = InspectorJson.Deserialize("not-json{{");
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void InspectorJson_NestedObject_ParsedAsInnerDictionary()
        {
            const string json = @"{""nested"": {""a"": 1}}";
            var result = InspectorJson.Deserialize(json);

            Assert.IsNotNull(result["nested"]);
            var inner = result["nested"] as Dictionary<string, object>;
            Assert.IsNotNull(inner);
            Assert.AreEqual(1L, inner["a"]);
        }

        [Test]
        public void InspectorJson_ArrayValue_ParsedAsList()
        {
            const string json = @"{""items"": [1, 2, 3]}";
            var result = InspectorJson.Deserialize(json);

            var list = result["items"] as List<object>;
            Assert.IsNotNull(list);
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual(1L, list[0]);
        }

        // ─── Fake observer ────────────────────────────────────────────────────

        private class FakeTrackerObserver : ITrackerObserver
        {
            public int CallCount { get; private set; }

            public void OnEvent(
                string provider,
                string eventName,
                IReadOnlyDictionary<string, object> payload,
                IReadOnlyDictionary<string, object> extraParams,
                TrackerEventPhase phase,
                string error)
            {
                CallCount++;
            }
        }
    }
}
