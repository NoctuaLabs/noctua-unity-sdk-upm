using System;
using System.Collections.Generic;
using System.Linq;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    public class TrackerEventPhaseTests
    {
        [Test]
        public void RawValuesStableAcrossBoundary()
        {
            // Must match the enum in Noctua.swift / Noctua.kt — changing
            // these silently would break the native bridge contract.
            Assert.AreEqual(0, (int)TrackerEventPhase.Queued);
            Assert.AreEqual(1, (int)TrackerEventPhase.Sending);
            Assert.AreEqual(2, (int)TrackerEventPhase.Emitted);
            Assert.AreEqual(3, (int)TrackerEventPhase.Uploading);
            Assert.AreEqual(4, (int)TrackerEventPhase.Acknowledged);
            Assert.AreEqual(5, (int)TrackerEventPhase.Failed);
            Assert.AreEqual(6, (int)TrackerEventPhase.TimedOut);
        }

        [Test]
        public void FromRawClampsUnknownToQueued()
        {
            Assert.AreEqual(TrackerEventPhase.Acknowledged, TrackerEventPhaseEx.FromRaw(4));
            Assert.AreEqual(TrackerEventPhase.Queued,       TrackerEventPhaseEx.FromRaw(999));
            Assert.AreEqual(TrackerEventPhase.Queued,       TrackerEventPhaseEx.FromRaw(-1));
        }

        [Test]
        public void IsTerminalOnlyForTerminalPhases()
        {
            Assert.IsTrue(TrackerEventPhase.Acknowledged.IsTerminal());
            Assert.IsTrue(TrackerEventPhase.Failed.IsTerminal());
            Assert.IsTrue(TrackerEventPhase.TimedOut.IsTerminal());
            Assert.IsFalse(TrackerEventPhase.Queued.IsTerminal());
            Assert.IsFalse(TrackerEventPhase.Sending.IsTerminal());
            Assert.IsFalse(TrackerEventPhase.Emitted.IsTerminal());
            Assert.IsFalse(TrackerEventPhase.Uploading.IsTerminal());
        }
    }

    public class TrackerDebugMonitorTests
    {
        private static Dictionary<string, object> Payload(params (string k, object v)[] kv)
        {
            var d = new Dictionary<string, object>();
            foreach (var p in kv) d[p.k] = p.v;
            return d;
        }

        [Test]
        public void QueuedCreatesEntryAndFiresEvent()
        {
            var m = new TrackerDebugMonitor();
            TrackerEmission captured = null;
            m.OnEmission += e => captured = e;

            m.OnEvent("Firebase", "purchase", Payload(("v", 1)), null, TrackerEventPhase.Queued, null);
            m.Pump();

            Assert.NotNull(captured);
            Assert.AreEqual("Firebase", captured.Provider);
            Assert.AreEqual("purchase", captured.EventName);
            Assert.AreEqual(TrackerEventPhase.Queued, captured.Phase);
            Assert.AreEqual(1, m.Snapshot().Count);
        }

        [Test]
        public void TransitionsUpdateSameEntry()
        {
            var m = new TrackerDebugMonitor();
            m.OnEvent("Firebase", "level_up", Payload(), null, TrackerEventPhase.Queued, null);
            m.OnEvent("Firebase", "level_up", null, null, TrackerEventPhase.Emitted, null);
            m.OnEvent("Firebase", "level_up", null, null, TrackerEventPhase.Acknowledged, null);
            m.Pump();

            var list = m.Snapshot();
            Assert.AreEqual(1, list.Count, "terminal transitions must not create a second entry");
            Assert.AreEqual(TrackerEventPhase.Acknowledged, list[0].Phase);
            Assert.AreEqual(3, list[0].History.Count);
        }

        [Test]
        public void OutOfOrderEventsSynthesizeEntry()
        {
            var m = new TrackerDebugMonitor();
            m.OnEvent("Adjust", "purchase", null, Payload(("adid", "abc")), TrackerEventPhase.Acknowledged, null);
            m.Pump();

            var list = m.Snapshot();
            Assert.AreEqual(1, list.Count);
            Assert.AreEqual(TrackerEventPhase.Acknowledged, list[0].Phase);
        }

        [Test]
        public void ProviderFilterIsolatesRows()
        {
            var m = new TrackerDebugMonitor();
            m.OnEvent("Firebase", "a", null, null, TrackerEventPhase.Queued, null);
            m.OnEvent("Adjust",   "b", null, null, TrackerEventPhase.Queued, null);
            m.OnEvent("Facebook", "c", null, null, TrackerEventPhase.Queued, null);
            m.Pump();

            Assert.AreEqual(1, m.Snapshot("Firebase").Count);
            Assert.AreEqual(1, m.Snapshot("Adjust").Count);
            Assert.AreEqual(3, m.Snapshot().Count);
        }

        [Test]
        public void RingBufferEvictsOldest()
        {
            var m = new TrackerDebugMonitor(capacity: 3);
            for (int i = 0; i < 5; i++)
                m.OnEvent("Firebase", $"e{i}", null, null, TrackerEventPhase.Queued, null);
            m.Pump();

            var list = m.Snapshot();
            Assert.AreEqual(3, list.Count);
            // Oldest two dropped
            CollectionAssert.AreEqual(new[] { "e2", "e3", "e4" }, list.Select(e => e.EventName).ToArray());
        }

        [Test]
        public void ClearRemovesAllEntries()
        {
            var m = new TrackerDebugMonitor();
            m.OnEvent("Firebase", "e", null, null, TrackerEventPhase.Queued, null);
            m.Pump();

            m.Clear();

            Assert.AreEqual(0, m.Snapshot().Count);
        }
    }

    public class TrackerObserverRegistryTests
    {
        private class CountingObserver : ITrackerObserver
        {
            public int Calls;
            public void OnEvent(string provider, string eventName, IReadOnlyDictionary<string, object> p,
                IReadOnlyDictionary<string, object> x, TrackerEventPhase phase, string err) => Calls++;
        }

        [Test]
        public void EmitNoOpWithNoObservers()
        {
            // No observers registered from clean state (other tests may have registered).
            // We just verify no exception and no side-effects.
            TrackerObserverRegistry.Emit("X", "Y", null, null, TrackerEventPhase.Queued);
        }

        [Test]
        public void RegisteredObserverReceivesEmissions()
        {
            var o = new CountingObserver();
            TrackerObserverRegistry.Register(o);
            try
            {
                TrackerObserverRegistry.Emit("Firebase", "test", null, null, TrackerEventPhase.Queued);
                Assert.AreEqual(1, o.Calls);
            }
            finally
            {
                TrackerObserverRegistry.Unregister(o);
            }
        }

        [Test]
        public void RegisterIsIdempotent()
        {
            var o = new CountingObserver();
            TrackerObserverRegistry.Register(o);
            TrackerObserverRegistry.Register(o);
            try
            {
                TrackerObserverRegistry.Emit("Firebase", "test", null, null, TrackerEventPhase.Queued);
                Assert.AreEqual(1, o.Calls);
            }
            finally
            {
                TrackerObserverRegistry.Unregister(o);
            }
        }
    }

    public class HttpInspectorHooksTests
    {
        private class StubObs : IHttpObserver
        {
            public int Starts, Changes, Ends;
            public void OnRequestStart(HttpExchange e) => Starts++;
            public void OnStateChange(Guid id, HttpExchangeState s) => Changes++;
            public void OnRequestEnd(HttpExchange e) => Ends++;
        }

        [Test]
        public void HasObserversReflectsRegistration()
        {
            Assert.IsFalse(HttpInspectorHooks.HasObservers);
            var o = new StubObs();
            HttpInspectorHooks.RegisterObserver(o);
            try
            {
                Assert.IsTrue(HttpInspectorHooks.HasObservers);
            }
            finally
            {
                HttpInspectorHooks.UnregisterObserver(o);
            }
            Assert.IsFalse(HttpInspectorHooks.HasObservers);
        }

        [Test]
        public void ObserverReceivesStartStateAndEnd()
        {
            var o = new StubObs();
            HttpInspectorHooks.RegisterObserver(o);
            try
            {
                var ex = new HttpExchange { Id = Guid.NewGuid(), Method = "POST", Url = "http://x", State = HttpExchangeState.Building };
                HttpInspectorHooks.FireStart(ex);
                HttpInspectorHooks.FireStateChange(ex.Id, HttpExchangeState.Sending);
                HttpInspectorHooks.FireEnd(ex);
                Assert.AreEqual(1, o.Starts);
                Assert.AreEqual(1, o.Changes);
                Assert.AreEqual(1, o.Ends);
            }
            finally { HttpInspectorHooks.UnregisterObserver(o); }
        }
    }

    public class HttpInspectorLogTests
    {
        [Test]
        public void UpsertAndClear()
        {
            var log = new HttpInspectorLog();
            var ex = new HttpExchange { Id = Guid.NewGuid(), Method = "GET", Url = "http://x", State = HttpExchangeState.Building };
            log.OnRequestStart(ex);
            log.Pump();
            Assert.AreEqual(1, log.Snapshot().Count);

            log.OnRequestEnd(new HttpExchange { Id = ex.Id, State = HttpExchangeState.Complete });
            log.Pump();
            Assert.AreEqual(1, log.Snapshot().Count, "same id must not duplicate");

            log.Clear();
            Assert.AreEqual(0, log.Snapshot().Count);
        }

        [Test]
        public void RingBufferCapped()
        {
            var log = new HttpInspectorLog();
            for (int i = 0; i < HttpInspectorLog.Capacity + 10; i++)
            {
                log.OnRequestStart(new HttpExchange { Id = Guid.NewGuid(), Method = "GET" });
            }
            log.Pump();
            Assert.LessOrEqual(log.Snapshot().Count, HttpInspectorLog.Capacity);
        }
    }

    public class InspectorJsonTests
    {
        [Test]
        public void DeserializeBasicTypes()
        {
            var d = InspectorJson.Deserialize("{\"s\":\"v\",\"n\":42,\"b\":true,\"f\":1.5}");
            Assert.AreEqual("v", d["s"]);
            Assert.AreEqual(42L, d["n"]);
            Assert.AreEqual(true, d["b"]);
            Assert.AreEqual(1.5, d["f"]);
        }

        [Test]
        public void DeserializeEmptyReturnsEmptyDict()
        {
            Assert.AreEqual(0, InspectorJson.Deserialize("{}").Count);
            Assert.AreEqual(0, InspectorJson.Deserialize(null).Count);
            Assert.AreEqual(0, InspectorJson.Deserialize("").Count);
        }

        [Test]
        public void DeserializeInvalidJsonReturnsEmpty()
        {
            Assert.AreEqual(0, InspectorJson.Deserialize("not-json").Count);
            Assert.AreEqual(0, InspectorJson.Deserialize("{malformed").Count);
        }
    }
}
