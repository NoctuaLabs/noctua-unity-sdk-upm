using System;
using System.Threading;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime.Inspector
{
    /// <summary>
    /// Unit tests for the verbose-log ledger that backs the Inspector
    /// "Logs" tab. Coverage focus:
    ///   * Pump correctly drains queued entries on the calling thread
    ///   * Capacity cap drops oldest entries (FIFO)
    ///   * <see cref="LogInspectorHooks"/> static fan-out works for
    ///     multiple registered observers
    ///   * Off-thread emissions are safe (TrackerDebugMonitor pattern)
    /// </summary>
    public class LogInspectorLedgerTest
    {
        [SetUp]
        public void Setup()
        {
            // Defensive — other tests may have registered observers.
            // No public Reset, so we assert state from a fresh ledger only.
        }

        [Test]
        public void Pump_drains_queued_entries_in_order()
        {
            var ledger = new LogInspectorLedger();
            var e1 = MakeEntry(LogLevel.Info, "first");
            var e2 = MakeEntry(LogLevel.Warning, "second");

            ledger.OnLog(e1);
            ledger.OnLog(e2);
            // Before Pump, snapshot is empty — entries sit in the queue.
            Assert.AreEqual(0, ledger.Snapshot().Count);

            ledger.Pump();

            var snap = ledger.Snapshot();
            Assert.AreEqual(2, snap.Count);
            Assert.AreEqual("first",  snap[0].Message);
            Assert.AreEqual("second", snap[1].Message);
        }

        [Test]
        public void Capacity_cap_drops_oldest_first()
        {
            var ledger = new LogInspectorLedger(capacity: 3);
            for (int i = 0; i < 5; i++)
            {
                ledger.OnLog(MakeEntry(LogLevel.Info, $"msg{i}"));
            }
            ledger.Pump();

            var snap = ledger.Snapshot();
            Assert.AreEqual(3, snap.Count, "capacity cap not enforced");
            Assert.AreEqual("msg2", snap[0].Message, "oldest two entries should be evicted");
            Assert.AreEqual("msg4", snap[2].Message);
        }

        [Test]
        public void OnEntry_fires_for_each_admitted_row()
        {
            var ledger = new LogInspectorLedger();
            int seen = 0;
            ledger.OnEntry += _ => seen++;

            ledger.OnLog(MakeEntry(LogLevel.Info, "a"));
            ledger.OnLog(MakeEntry(LogLevel.Info, "b"));
            ledger.Pump();

            Assert.AreEqual(2, seen);
        }

        [Test]
        public void Hooks_fanout_to_multiple_observers()
        {
            var a = new LogInspectorLedger();
            var b = new LogInspectorLedger();
            LogInspectorHooks.RegisterObserver(a);
            LogInspectorHooks.RegisterObserver(b);
            try
            {
                LogInspectorHooks.Emit(MakeEntry(LogLevel.Info, "fanout"));
                a.Pump();
                b.Pump();
                Assert.AreEqual(1, a.Snapshot().Count);
                Assert.AreEqual(1, b.Snapshot().Count);
            }
            finally
            {
                LogInspectorHooks.UnregisterObserver(a);
                LogInspectorHooks.UnregisterObserver(b);
            }
        }

        [Test]
        public void Hooks_HasObservers_reflects_registration()
        {
            var ledger = new LogInspectorLedger();
            // We can't assert false initially — other tests may leak observers.
            LogInspectorHooks.RegisterObserver(ledger);
            try
            {
                Assert.IsTrue(LogInspectorHooks.HasObservers);
            }
            finally
            {
                LogInspectorHooks.UnregisterObserver(ledger);
            }
        }

        [Test]
        public void OnLog_from_background_thread_is_safe()
        {
            var ledger = new LogInspectorLedger(capacity: 1000);
            var threads = new Thread[4];
            const int perThread = 100;
            for (int t = 0; t < threads.Length; t++)
            {
                int tid = t;
                threads[t] = new Thread(() =>
                {
                    for (int i = 0; i < perThread; i++)
                        ledger.OnLog(MakeEntry(LogLevel.Info, $"t{tid}-i{i}"));
                });
                threads[t].Start();
            }
            foreach (var th in threads) th.Join();

            ledger.Pump();
            Assert.AreEqual(threads.Length * perThread, ledger.Snapshot().Count);
        }

        [Test]
        public void Clear_empties_the_ring_buffer()
        {
            var ledger = new LogInspectorLedger();
            ledger.OnLog(MakeEntry(LogLevel.Info, "x"));
            ledger.Pump();
            Assert.AreEqual(1, ledger.Snapshot().Count);

            ledger.Clear();
            Assert.AreEqual(0, ledger.Snapshot().Count);
        }

        private static LogEntry MakeEntry(LogLevel level, string msg) =>
            new LogEntry(DateTime.UtcNow, level, "Unity", "TestTag", msg);
    }
}
