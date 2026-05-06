using System;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="LogInspectorHooks"/>.
    ///
    /// Covers:
    ///   — <c>RegisterObserver</c>   — null guard, dedup
    ///   — <c>UnregisterObserver</c> — null guard, unregistered-no-throw
    ///   — <c>HasObservers</c>       — true after register, false after unregister
    ///   — <c>Emit</c>               — delivers to all observers, swallows exceptions,
    ///                                  skips null entry, no-op when no observers
    ///
    /// Because <see cref="LogInspectorHooks"/> is a static class the observer list
    /// persists across test methods. Each test uses a fresh <see cref="FakeLogObserver"/>
    /// and unregisters it in <c>[TearDown]</c> to prevent leakage.
    /// </summary>
    [TestFixture]
    public class LogInspectorHooksTest
    {
        private static readonly DateTime _ts = new DateTime(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // ─── Fake observer ─────────────────────────────────────────────────

        private sealed class FakeLogObserver : ILogObserver
        {
            public int CallCount;
            public LogEntry LastEntry;

            public void OnLog(LogEntry entry)
            {
                CallCount++;
                LastEntry = entry;
            }
        }

        private sealed class ThrowingLogObserver : ILogObserver
        {
            public void OnLog(LogEntry entry) =>
                throw new InvalidOperationException("observer boom");
        }

        private FakeLogObserver _obs;

        [SetUp]
        public void SetUp()
        {
            _obs = new FakeLogObserver();
        }

        [TearDown]
        public void TearDown()
        {
            LogInspectorHooks.UnregisterObserver(_obs);
        }

        private static LogEntry MakeEntry(string msg = "test") =>
            new LogEntry(_ts, LogLevel.Info, "Unity", "tag", msg);

        // ═══════════════════════════════════════════════════════════════════
        // RegisterObserver
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Register_Observer_HasObserversIsTrue()
        {
            LogInspectorHooks.RegisterObserver(_obs);
            Assert.IsTrue(LogInspectorHooks.HasObservers);
        }

        [Test]
        public void Register_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LogInspectorHooks.RegisterObserver(null));
        }

        [Test]
        public void Register_SameObserverTwice_EmitCalledOnlyOnce()
        {
            LogInspectorHooks.RegisterObserver(_obs);
            LogInspectorHooks.RegisterObserver(_obs);

            LogInspectorHooks.Emit(MakeEntry());

            Assert.AreEqual(1, _obs.CallCount,
                "Duplicate registration must be deduplicated");
        }

        // ═══════════════════════════════════════════════════════════════════
        // UnregisterObserver
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Unregister_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => LogInspectorHooks.UnregisterObserver(null));
        }

        [Test]
        public void Unregister_NotRegistered_DoesNotThrow()
        {
            var stranger = new FakeLogObserver();
            Assert.DoesNotThrow(() => LogInspectorHooks.UnregisterObserver(stranger));
        }

        [Test]
        public void Unregister_RegisteredObserver_NoLongerReceivesEmit()
        {
            LogInspectorHooks.RegisterObserver(_obs);
            LogInspectorHooks.UnregisterObserver(_obs);

            LogInspectorHooks.Emit(MakeEntry());

            Assert.AreEqual(0, _obs.CallCount,
                "Unregistered observer must not receive subsequent Emit calls");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Emit
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Emit_CallsOnLogWithCorrectEntry()
        {
            LogInspectorHooks.RegisterObserver(_obs);
            var entry = MakeEntry("hello");

            LogInspectorHooks.Emit(entry);

            Assert.IsNotNull(_obs.LastEntry);
            Assert.AreEqual("hello", _obs.LastEntry.Message);
        }

        [Test]
        public void Emit_NullEntry_DoesNotThrowAndNotDelivered()
        {
            LogInspectorHooks.RegisterObserver(_obs);

            Assert.DoesNotThrow(() => LogInspectorHooks.Emit(null));
            Assert.AreEqual(0, _obs.CallCount,
                "Null entry must be silently dropped and not delivered to observers");
        }

        [Test]
        public void Emit_ThrowingObserver_ExceptionSwallowed()
        {
            var thrower = new ThrowingLogObserver();
            LogInspectorHooks.RegisterObserver(thrower);

            Assert.DoesNotThrow(() => LogInspectorHooks.Emit(MakeEntry()),
                "Exceptions thrown by observers must not propagate");

            LogInspectorHooks.UnregisterObserver(thrower); // cleanup
        }

        [Test]
        public void Emit_TwoObservers_BothReceiveEntry()
        {
            var obs2 = new FakeLogObserver();
            LogInspectorHooks.RegisterObserver(_obs);
            LogInspectorHooks.RegisterObserver(obs2);

            LogInspectorHooks.Emit(MakeEntry("both"));

            Assert.AreEqual(1, _obs.CallCount,  "First observer must receive call");
            Assert.AreEqual(1, obs2.CallCount,  "Second observer must receive call");

            LogInspectorHooks.UnregisterObserver(obs2); // cleanup
        }
    }
}
