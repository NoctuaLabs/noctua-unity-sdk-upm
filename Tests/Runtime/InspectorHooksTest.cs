using System;
using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for the static inspector hook registries:
    ///   * <see cref="HttpInspectorHooks"/> — observer registration, fan-out (FireStart/FireEnd/FireStateChange)
    ///   * <see cref="LogInspectorHooks"/> — observer registration, Emit fan-out
    ///
    /// Both classes are static — each test uses try/finally to guarantee cleanup even on failure.
    /// </summary>
    [TestFixture]
    public class InspectorHooksTest
    {
        // ─── HttpInspectorHooks ───────────────────────────────────────────────

        [Test]
        public void HttpInspectorHooks_RegisterNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => HttpInspectorHooks.RegisterObserver(null));
        }

        [Test]
        public void HttpInspectorHooks_UnregisterNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => HttpInspectorHooks.UnregisterObserver(null));
        }

        [Test]
        public void HttpInspectorHooks_Register_HasObserversTrue()
        {
            var obs = new FakeHttpObserver();
            HttpInspectorHooks.RegisterObserver(obs);
            try
            {
                Assert.IsTrue(HttpInspectorHooks.HasObservers);
            }
            finally
            {
                HttpInspectorHooks.UnregisterObserver(obs);
            }
        }

        [Test]
        public void HttpInspectorHooks_DuplicateRegister_ObserverCalledOnce()
        {
            var obs = new FakeHttpObserver();
            HttpInspectorHooks.RegisterObserver(obs);
            HttpInspectorHooks.RegisterObserver(obs);
            try
            {
                var ex = new HttpExchange { Id = Guid.NewGuid(), Method = "GET", Url = "https://api.test" };
                HttpInspectorHooks.FireStart(ex);
                Assert.AreEqual(1, obs.StartCount, "Observer registered twice should still be called once");
            }
            finally
            {
                HttpInspectorHooks.UnregisterObserver(obs);
            }
        }

        [Test]
        public void HttpInspectorHooks_FireStart_NotifiesObserver()
        {
            var obs = new FakeHttpObserver();
            HttpInspectorHooks.RegisterObserver(obs);
            try
            {
                var ex = new HttpExchange { Id = Guid.NewGuid(), Method = "POST", Url = "https://track.test" };
                HttpInspectorHooks.FireStart(ex);

                Assert.AreEqual(1, obs.StartCount);
                Assert.AreEqual(ex.Id, obs.LastStartExchange.Id);
            }
            finally
            {
                HttpInspectorHooks.UnregisterObserver(obs);
            }
        }

        [Test]
        public void HttpInspectorHooks_FireEnd_NotifiesObserver()
        {
            var obs = new FakeHttpObserver();
            HttpInspectorHooks.RegisterObserver(obs);
            try
            {
                var ex = new HttpExchange { Id = Guid.NewGuid(), Status = 200 };
                HttpInspectorHooks.FireEnd(ex);

                Assert.AreEqual(1, obs.EndCount);
                Assert.AreEqual(200, obs.LastEndExchange.Status);
            }
            finally
            {
                HttpInspectorHooks.UnregisterObserver(obs);
            }
        }

        [Test]
        public void HttpInspectorHooks_FireStateChange_NotifiesObserver()
        {
            var obs = new FakeHttpObserver();
            HttpInspectorHooks.RegisterObserver(obs);
            try
            {
                var id = Guid.NewGuid();
                HttpInspectorHooks.FireStateChange(id, HttpExchangeState.Complete);

                Assert.AreEqual(1, obs.StateChangeCount);
                Assert.AreEqual(id, obs.LastStateChangeId);
                Assert.AreEqual(HttpExchangeState.Complete, obs.LastState);
            }
            finally
            {
                HttpInspectorHooks.UnregisterObserver(obs);
            }
        }

        [Test]
        public void HttpInspectorHooks_FireMultiple_FansOutToAllObservers()
        {
            var obs1 = new FakeHttpObserver();
            var obs2 = new FakeHttpObserver();
            HttpInspectorHooks.RegisterObserver(obs1);
            HttpInspectorHooks.RegisterObserver(obs2);
            try
            {
                HttpInspectorHooks.FireStart(new HttpExchange { Id = Guid.NewGuid() });

                Assert.AreEqual(1, obs1.StartCount);
                Assert.AreEqual(1, obs2.StartCount);
            }
            finally
            {
                HttpInspectorHooks.UnregisterObserver(obs1);
                HttpInspectorHooks.UnregisterObserver(obs2);
            }
        }

        [Test]
        public void HttpInspectorHooks_AfterUnregister_ObserverNotCalled()
        {
            var obs = new FakeHttpObserver();
            HttpInspectorHooks.RegisterObserver(obs);
            HttpInspectorHooks.UnregisterObserver(obs);

            HttpInspectorHooks.FireStart(new HttpExchange { Id = Guid.NewGuid() });

            Assert.AreEqual(0, obs.StartCount, "Unregistered observer should not be called");
        }

        [Test]
        public void HttpInspectorHooks_FireWhenNoObservers_DoesNotThrow()
        {
            // Ensure clean state
            var tmp = new FakeHttpObserver();
            HttpInspectorHooks.RegisterObserver(tmp);
            HttpInspectorHooks.UnregisterObserver(tmp);

            Assert.DoesNotThrow(() =>
            {
                var ex = new HttpExchange { Id = Guid.NewGuid() };
                HttpInspectorHooks.FireStart(ex);
                HttpInspectorHooks.FireEnd(ex);
                HttpInspectorHooks.FireStateChange(ex.Id, HttpExchangeState.Failed);
            });
        }

        // ─── LogInspectorHooks ────────────────────────────────────────────────

        [Test]
        public void LogInspectorHooks_RegisterNull_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LogInspectorHooks.RegisterObserver(null));
        }

        [Test]
        public void LogInspectorHooks_Register_HasObserversTrue()
        {
            var obs = new FakeLogObserver();
            LogInspectorHooks.RegisterObserver(obs);
            try
            {
                Assert.IsTrue(LogInspectorHooks.HasObservers);
            }
            finally
            {
                LogInspectorHooks.UnregisterObserver(obs);
            }
        }

        [Test]
        public void LogInspectorHooks_DuplicateRegister_EmitCalledOnce()
        {
            var obs = new FakeLogObserver();
            LogInspectorHooks.RegisterObserver(obs);
            LogInspectorHooks.RegisterObserver(obs);
            try
            {
                var entry = MakeLogEntry();
                LogInspectorHooks.Emit(entry);
                Assert.AreEqual(1, obs.CallCount, "Duplicate registration should not double-fire");
            }
            finally
            {
                LogInspectorHooks.UnregisterObserver(obs);
            }
        }

        [Test]
        public void LogInspectorHooks_Emit_FansOutToRegisteredObserver()
        {
            var obs = new FakeLogObserver();
            LogInspectorHooks.RegisterObserver(obs);
            try
            {
                var entry = MakeLogEntry(LogLevel.Warning, "NoctuaAuth", "token expired");
                LogInspectorHooks.Emit(entry);

                Assert.AreEqual(1, obs.CallCount);
                Assert.AreEqual(entry.Id,        obs.LastEntry.Id);
                Assert.AreEqual(LogLevel.Warning, obs.LastEntry.Level);
                Assert.AreEqual("NoctuaAuth",    obs.LastEntry.Tag);
            }
            finally
            {
                LogInspectorHooks.UnregisterObserver(obs);
            }
        }

        [Test]
        public void LogInspectorHooks_EmitNull_DoesNotThrow()
        {
            var obs = new FakeLogObserver();
            LogInspectorHooks.RegisterObserver(obs);
            try
            {
                Assert.DoesNotThrow(() => LogInspectorHooks.Emit(null));
                Assert.AreEqual(0, obs.CallCount, "Null entry should not be forwarded to observer");
            }
            finally
            {
                LogInspectorHooks.UnregisterObserver(obs);
            }
        }

        [Test]
        public void LogInspectorHooks_EmitMultiple_FansOutToAllObservers()
        {
            var obs1 = new FakeLogObserver();
            var obs2 = new FakeLogObserver();
            LogInspectorHooks.RegisterObserver(obs1);
            LogInspectorHooks.RegisterObserver(obs2);
            try
            {
                LogInspectorHooks.Emit(MakeLogEntry());

                Assert.AreEqual(1, obs1.CallCount);
                Assert.AreEqual(1, obs2.CallCount);
            }
            finally
            {
                LogInspectorHooks.UnregisterObserver(obs1);
                LogInspectorHooks.UnregisterObserver(obs2);
            }
        }

        [Test]
        public void LogInspectorHooks_AfterUnregister_ObserverNotCalled()
        {
            var obs = new FakeLogObserver();
            LogInspectorHooks.RegisterObserver(obs);
            LogInspectorHooks.UnregisterObserver(obs);

            LogInspectorHooks.Emit(MakeLogEntry());

            Assert.AreEqual(0, obs.CallCount, "Unregistered observer should not be called");
        }

        [Test]
        public void LogInspectorHooks_EmitWhenNoObservers_DoesNotThrow()
        {
            var tmp = new FakeLogObserver();
            LogInspectorHooks.RegisterObserver(tmp);
            LogInspectorHooks.UnregisterObserver(tmp);

            Assert.DoesNotThrow(() => LogInspectorHooks.Emit(MakeLogEntry()));
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private static LogEntry MakeLogEntry(
            LogLevel level   = LogLevel.Info,
            string tag       = "TestTag",
            string message   = "test message")
        {
            return new LogEntry(DateTime.UtcNow, level, "Unity", tag, message);
        }

        // ─── Fakes ────────────────────────────────────────────────────────────

        private class FakeHttpObserver : IHttpObserver
        {
            public int          StartCount       { get; private set; }
            public int          EndCount         { get; private set; }
            public int          StateChangeCount { get; private set; }
            public HttpExchange LastStartExchange { get; private set; }
            public HttpExchange LastEndExchange   { get; private set; }
            public Guid         LastStateChangeId { get; private set; }
            public HttpExchangeState LastState   { get; private set; }

            public void OnRequestStart(HttpExchange exchange)
            {
                StartCount++;
                LastStartExchange = exchange;
            }

            public void OnRequestEnd(HttpExchange exchange)
            {
                EndCount++;
                LastEndExchange = exchange;
            }

            public void OnStateChange(Guid exchangeId, HttpExchangeState state)
            {
                StateChangeCount++;
                LastStateChangeId = exchangeId;
                LastState         = state;
            }
        }

        private class FakeLogObserver : ILogObserver
        {
            public int      CallCount { get; private set; }
            public LogEntry LastEntry { get; private set; }

            public void OnLog(LogEntry entry)
            {
                CallCount++;
                LastEntry = entry;
            }
        }
    }
}
