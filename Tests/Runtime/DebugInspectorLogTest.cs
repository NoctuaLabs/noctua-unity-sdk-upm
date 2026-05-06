using System;
using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for the Inspector debug log ring buffers:
    ///   * <see cref="LogInspectorLedger"/>  — OnLog/Pump/Snapshot/Clear/overflow/OnEntry event
    ///   * <see cref="HttpInspectorLog"/>    — OnRequestStart/OnStateChange/OnRequestEnd/Pump/
    ///                                         Snapshot/Clear/overflow/OnExchange event
    ///   * <see cref="HttpExchange"/>        — property round-trip
    ///   * <see cref="HttpExchangeState"/>   — enum ordinals (ABI contract)
    ///
    /// Both ring buffers use a drain-on-demand Pump() model: writes go into a
    /// ConcurrentQueue; Pump() processes them on the "main thread". In EditMode
    /// we call Pump() directly after enqueuing — no MonoBehaviour required.
    /// </summary>
    [TestFixture]
    public class DebugInspectorLogTest
    {
        private static readonly DateTime _ts = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static LogEntry MakeEntry(string msg = "hello", LogLevel level = LogLevel.Info)
            => new LogEntry(_ts, level, "Unity", "tag", msg);

        private static HttpExchange MakeExchange(string url = "https://api.example.com/v1/test")
            => new HttpExchange
            {
                Id        = Guid.NewGuid(),
                Method    = "GET",
                Url       = url,
                StartUtc  = _ts,
                State     = HttpExchangeState.Building
            };

        // ═══════════════════════════════════════════════════════════════════
        // LogInspectorLedger — construction
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void LogLedger_DefaultCapacity_Is5000()
        {
            Assert.AreEqual(5000, LogInspectorLedger.DefaultCapacity);
        }

        [Test]
        public void LogLedger_CustomCapacity_RespectedOnOverflow()
        {
            // Capacity 2 → adding 3 entries drops the oldest
            var ledger = new LogInspectorLedger(capacity: 2);

            ledger.OnLog(MakeEntry("a"));
            ledger.OnLog(MakeEntry("b"));
            ledger.OnLog(MakeEntry("c")); // overflows → "a" dropped

            ledger.Pump();

            var snapshot = ledger.Snapshot();
            Assert.AreEqual(2, snapshot.Count);
            Assert.AreEqual("b", snapshot[0].Message);
            Assert.AreEqual("c", snapshot[1].Message);
        }

        // ═══════════════════════════════════════════════════════════════════
        // LogInspectorLedger — OnLog + Pump + Snapshot
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void LogLedger_Snapshot_EmptyBeforePump()
        {
            var ledger = new LogInspectorLedger();
            ledger.OnLog(MakeEntry("queued"));

            // Not yet pumped → should be empty (still in ConcurrentQueue)
            Assert.AreEqual(0, ledger.Snapshot().Count,
                "Entries must not appear in Snapshot before Pump() drains the queue");
        }

        [Test]
        public void LogLedger_AfterPump_EntryAppearsInSnapshot()
        {
            var ledger = new LogInspectorLedger();
            ledger.OnLog(MakeEntry("msg1"));
            ledger.Pump();

            var snapshot = ledger.Snapshot();
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual("msg1", snapshot[0].Message);
        }

        [Test]
        public void LogLedger_MultipleEntries_AllInSnapshot()
        {
            var ledger = new LogInspectorLedger();
            for (int i = 1; i <= 5; i++) ledger.OnLog(MakeEntry($"m{i}"));
            ledger.Pump();

            Assert.AreEqual(5, ledger.Snapshot().Count);
        }

        [Test]
        public void LogLedger_NullEntry_DoesNotThrow()
        {
            var ledger = new LogInspectorLedger();

            Assert.DoesNotThrow(() =>
            {
                ledger.OnLog(null);
                ledger.Pump();
            });

            Assert.AreEqual(0, ledger.Snapshot().Count,
                "Null entry must not appear in snapshot");
        }

        // ═══════════════════════════════════════════════════════════════════
        // LogInspectorLedger — Clear
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void LogLedger_Clear_EmptiesSnapshot()
        {
            var ledger = new LogInspectorLedger();
            ledger.OnLog(MakeEntry("x"));
            ledger.Pump();

            ledger.Clear();

            Assert.AreEqual(0, ledger.Snapshot().Count);
        }

        // ═══════════════════════════════════════════════════════════════════
        // LogInspectorLedger — OnEntry event
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void LogLedger_OnEntry_FiredOnPump()
        {
            var ledger = new LogInspectorLedger();
            LogEntry received = null;
            ledger.OnEntry += e => received = e;

            ledger.OnLog(MakeEntry("event-test"));
            ledger.Pump();

            Assert.IsNotNull(received, "OnEntry must fire during Pump()");
            Assert.AreEqual("event-test", received.Message);
        }

        [Test]
        public void LogLedger_OnEntry_NotFiredBeforePump()
        {
            var ledger = new LogInspectorLedger();
            bool fired = false;
            ledger.OnEntry += _ => fired = true;

            ledger.OnLog(MakeEntry());
            // No Pump call

            Assert.IsFalse(fired, "OnEntry must not fire before Pump()");
        }

        // ═══════════════════════════════════════════════════════════════════
        // HttpInspectorLog — OnRequestStart + Pump + Snapshot
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void HttpLog_Snapshot_EmptyBeforePump()
        {
            var log = new HttpInspectorLog();
            log.OnRequestStart(MakeExchange());

            Assert.AreEqual(0, log.Snapshot().Count,
                "Exchanges must not appear in Snapshot before Pump()");
        }

        [Test]
        public void HttpLog_AfterPump_ExchangeAppearsInSnapshot()
        {
            var log = new HttpInspectorLog();
            var ex  = MakeExchange();
            log.OnRequestStart(ex);
            log.Pump();

            var snapshot = log.Snapshot();
            Assert.AreEqual(1, snapshot.Count);
            Assert.AreEqual(ex.Id, snapshot[0].Id);
        }

        [Test]
        public void HttpLog_MultipleExchanges_AllInSnapshot()
        {
            var log = new HttpInspectorLog();
            for (int i = 0; i < 5; i++)
            {
                log.OnRequestStart(MakeExchange($"https://api.example.com/v{i}"));
            }
            log.Pump();

            Assert.AreEqual(5, log.Snapshot().Count);
        }

        // ═══════════════════════════════════════════════════════════════════
        // HttpInspectorLog — OnStateChange
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void HttpLog_OnStateChange_UpdatesStateInSnapshot()
        {
            var log = new HttpInspectorLog();
            var ex  = MakeExchange();
            ex.State = HttpExchangeState.Sending;
            log.OnRequestStart(ex);
            log.Pump();

            log.OnStateChange(ex.Id, HttpExchangeState.Complete);
            log.Pump();

            Assert.AreEqual(HttpExchangeState.Complete, log.Snapshot()[0].State);
        }

        [Test]
        public void HttpLog_OnStateChange_UnknownId_DoesNotThrow()
        {
            var log = new HttpInspectorLog();

            Assert.DoesNotThrow(() =>
            {
                log.OnStateChange(Guid.NewGuid(), HttpExchangeState.Failed);
                log.Pump();
            });
        }

        // ═══════════════════════════════════════════════════════════════════
        // HttpInspectorLog — OnRequestEnd / upsert
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void HttpLog_OnRequestEnd_UpdatesExistingExchange()
        {
            var log = new HttpInspectorLog();
            var ex  = MakeExchange();
            log.OnRequestStart(ex);
            log.Pump();

            ex.Status       = 200;
            ex.ResponseBody = "OK";
            ex.State        = HttpExchangeState.Complete;
            log.OnRequestEnd(ex);
            log.Pump();

            var snapshot = log.Snapshot();
            Assert.AreEqual(1, snapshot.Count, "End must not duplicate the exchange");
            Assert.AreEqual(200, snapshot[0].Status);
            Assert.AreEqual(HttpExchangeState.Complete, snapshot[0].State);
        }

        // ═══════════════════════════════════════════════════════════════════
        // HttpInspectorLog — overflow / capacity cap
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void HttpLog_OverflowCapacity_OldestDropped()
        {
            var log   = new HttpInspectorLog();
            var first = MakeExchange("https://first.com");
            log.OnRequestStart(first);

            // Fill remaining 99 slots + 1 overflow
            for (int i = 0; i < HttpInspectorLog.Capacity; i++) // +1 over cap
                log.OnRequestStart(MakeExchange($"https://n{i}.com"));

            log.Pump();

            var snapshot = log.Snapshot();
            Assert.AreEqual(HttpInspectorLog.Capacity, snapshot.Count,
                "Snapshot must be capped at Capacity");
            Assert.IsFalse(snapshot[0].Id == first.Id,
                "Oldest exchange must have been evicted on overflow");
        }

        // ═══════════════════════════════════════════════════════════════════
        // HttpInspectorLog — Clear
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void HttpLog_Clear_EmptiesSnapshot()
        {
            var log = new HttpInspectorLog();
            log.OnRequestStart(MakeExchange());
            log.Pump();

            log.Clear();

            Assert.AreEqual(0, log.Snapshot().Count);
        }

        // ═══════════════════════════════════════════════════════════════════
        // HttpInspectorLog — OnExchange event
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void HttpLog_OnExchange_FiredOnPump()
        {
            var log = new HttpInspectorLog();
            HttpExchange received = null;
            log.OnExchange += e => received = e;

            var ex = MakeExchange();
            log.OnRequestStart(ex);
            log.Pump();

            Assert.IsNotNull(received, "OnExchange must fire during Pump()");
            Assert.AreEqual(ex.Id, received.Id);
        }

        // ═══════════════════════════════════════════════════════════════════
        // HttpExchange — property round-trip
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void HttpExchange_PropertyRoundTrip()
        {
            var id = Guid.NewGuid();
            var ex = new HttpExchange
            {
                Id              = id,
                Method          = "POST",
                Url             = "https://api.example.com/event",
                RequestBody     = @"{""event"":""purchase""}",
                Status          = 201,
                ResponseBody    = @"{""ok"":true}",
                StartUtc        = _ts,
                ElapsedMs       = 312L,
                Error           = null,
                State           = HttpExchangeState.Complete
            };

            Assert.AreEqual(id,                           ex.Id);
            Assert.AreEqual("POST",                       ex.Method);
            Assert.AreEqual("https://api.example.com/event", ex.Url);
            Assert.AreEqual(201,                          ex.Status);
            Assert.AreEqual(312L,                         ex.ElapsedMs);
            Assert.IsNull(ex.Error);
            Assert.AreEqual(HttpExchangeState.Complete,   ex.State);
        }

        // ═══════════════════════════════════════════════════════════════════
        // HttpExchangeState ordinals — ABI contract
        // ═══════════════════════════════════════════════════════════════════

        [Test] public void State_Building_OrdinalIsZero()   => Assert.AreEqual(0, (int)HttpExchangeState.Building);
        [Test] public void State_Sending_OrdinalIsOne()     => Assert.AreEqual(1, (int)HttpExchangeState.Sending);
        [Test] public void State_Receiving_OrdinalIsTwo()   => Assert.AreEqual(2, (int)HttpExchangeState.Receiving);
        [Test] public void State_Complete_OrdinalIsThree()  => Assert.AreEqual(3, (int)HttpExchangeState.Complete);
        [Test] public void State_Failed_OrdinalIsFour()     => Assert.AreEqual(4, (int)HttpExchangeState.Failed);
        [Test] public void State_Aborted_OrdinalIsFive()    => Assert.AreEqual(5, (int)HttpExchangeState.Aborted);
    }
}
