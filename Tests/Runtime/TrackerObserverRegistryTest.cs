using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for:
    ///   * <see cref="TrackerObserverRegistry"/> — Register, Unregister, HasObservers, Emit
    ///   * <see cref="TrackerEventPhaseEx"/>      — FromRaw, IsTerminal helpers
    ///   * <see cref="TrackerEventPhase"/>         — enum ordinals (native ABI contract)
    ///
    /// Because <see cref="TrackerObserverRegistry"/> is a static class the observer list
    /// persists across test methods. Each test creates its own <see cref="FakeObserver"/>
    /// and unregisters it in <c>[TearDown]</c> to avoid leaking state.
    /// </summary>
    [TestFixture]
    public class TrackerObserverRegistryTest
    {
        // ─── Fake observer ────────────────────────────────────────────────────

        private sealed class FakeObserver : ITrackerObserver
        {
            public int CallCount;
            public string LastProvider;
            public string LastEventName;
            public TrackerEventPhase LastPhase;
            public string LastError;

            public void OnEvent(
                string provider,
                string eventName,
                IReadOnlyDictionary<string, object> payload,
                IReadOnlyDictionary<string, object> extraParams,
                TrackerEventPhase phase,
                string error)
            {
                CallCount++;
                LastProvider  = provider;
                LastEventName = eventName;
                LastPhase     = phase;
                LastError     = error;
            }
        }

        private sealed class ThrowingObserver : ITrackerObserver
        {
            public void OnEvent(
                string provider,
                string eventName,
                IReadOnlyDictionary<string, object> payload,
                IReadOnlyDictionary<string, object> extraParams,
                TrackerEventPhase phase,
                string error)
            {
                throw new System.InvalidOperationException("observer kaboom");
            }
        }

        private FakeObserver _obs;

        [SetUp]
        public void SetUp()
        {
            _obs = new FakeObserver();
        }

        [TearDown]
        public void TearDown()
        {
            // Unregister our observer so it doesn't pollute subsequent tests.
            TrackerObserverRegistry.Unregister(_obs);
        }

        // ═══════════════════════════════════════════════════════════════════
        // Register
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Register_Observer_HasObserversIsTrue()
        {
            TrackerObserverRegistry.Register(_obs);

            Assert.IsTrue(TrackerObserverRegistry.HasObservers,
                "HasObservers must be true after registering an observer");
        }

        [Test]
        public void Register_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => TrackerObserverRegistry.Register(null));
        }

        [Test]
        public void Register_SameObserverTwice_EmitCalledOnce()
        {
            // Registering the same reference twice must be deduplicated.
            TrackerObserverRegistry.Register(_obs);
            TrackerObserverRegistry.Register(_obs);

            TrackerObserverRegistry.Emit("p", "e", null, null, TrackerEventPhase.Queued);

            Assert.AreEqual(1, _obs.CallCount,
                "Duplicate registration must not cause double dispatch");
        }

        // ═══════════════════════════════════════════════════════════════════
        // Unregister
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Unregister_RegisteredObserver_NoLongerReceivesEmit()
        {
            TrackerObserverRegistry.Register(_obs);
            TrackerObserverRegistry.Unregister(_obs);

            TrackerObserverRegistry.Emit("p", "e", null, null, TrackerEventPhase.Emitted);

            Assert.AreEqual(0, _obs.CallCount,
                "Unregistered observer must not receive subsequent Emit calls");
        }

        [Test]
        public void Unregister_Null_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => TrackerObserverRegistry.Unregister(null));
        }

        [Test]
        public void Unregister_NotRegistered_DoesNotThrow()
        {
            var stranger = new FakeObserver();

            Assert.DoesNotThrow(() => TrackerObserverRegistry.Unregister(stranger));
        }

        // ═══════════════════════════════════════════════════════════════════
        // Emit
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void Emit_CallsOnEventWithCorrectArguments()
        {
            TrackerObserverRegistry.Register(_obs);

            TrackerObserverRegistry.Emit(
                provider:    "Adjust",
                eventName:   "level_up",
                payload:     null,
                extraParams: null,
                phase:       TrackerEventPhase.Acknowledged,
                error:       null);

            Assert.AreEqual("Adjust",                        _obs.LastProvider);
            Assert.AreEqual("level_up",                      _obs.LastEventName);
            Assert.AreEqual(TrackerEventPhase.Acknowledged,  _obs.LastPhase);
            Assert.IsNull(_obs.LastError);
        }

        [Test]
        public void Emit_ErrorPropagatedToObserver()
        {
            TrackerObserverRegistry.Register(_obs);

            TrackerObserverRegistry.Emit("p", "e", null, null, TrackerEventPhase.Failed, "timeout");

            Assert.AreEqual("timeout", _obs.LastError);
            Assert.AreEqual(TrackerEventPhase.Failed, _obs.LastPhase);
        }

        [Test]
        public void Emit_TwoObservers_BothReceiveCall()
        {
            var obs2 = new FakeObserver();

            TrackerObserverRegistry.Register(_obs);
            TrackerObserverRegistry.Register(obs2);

            TrackerObserverRegistry.Emit("p", "e", null, null, TrackerEventPhase.Queued);

            Assert.AreEqual(1, _obs.CallCount,  "First observer must receive call");
            Assert.AreEqual(1, obs2.CallCount,  "Second observer must receive call");

            TrackerObserverRegistry.Unregister(obs2); // cleanup
        }

        [Test]
        public void Emit_ThrowingObserver_DoesNotPropagateException()
        {
            var thrower = new ThrowingObserver();
            TrackerObserverRegistry.Register(thrower);

            Assert.DoesNotThrow(
                () => TrackerObserverRegistry.Emit("p", "e", null, null, TrackerEventPhase.Failed),
                "Exceptions thrown by observers must be swallowed");

            TrackerObserverRegistry.Unregister(thrower); // cleanup
        }

        [Test]
        public void Emit_NullPayloadAndExtraParams_DoesNotThrow()
        {
            TrackerObserverRegistry.Register(_obs);

            Assert.DoesNotThrow(
                () => TrackerObserverRegistry.Emit("p", "e", null, null, TrackerEventPhase.Sending));
        }

        // ═══════════════════════════════════════════════════════════════════
        // TrackerEventPhase ordinals — native ABI contract
        // ═══════════════════════════════════════════════════════════════════

        [Test] public void Phase_Queued_OrdinalIsZero()       => Assert.AreEqual(0, (int)TrackerEventPhase.Queued);
        [Test] public void Phase_Sending_OrdinalIsOne()        => Assert.AreEqual(1, (int)TrackerEventPhase.Sending);
        [Test] public void Phase_Emitted_OrdinalIsTwo()        => Assert.AreEqual(2, (int)TrackerEventPhase.Emitted);
        [Test] public void Phase_Uploading_OrdinalIsThree()    => Assert.AreEqual(3, (int)TrackerEventPhase.Uploading);
        [Test] public void Phase_Acknowledged_OrdinalIsFour()  => Assert.AreEqual(4, (int)TrackerEventPhase.Acknowledged);
        [Test] public void Phase_Failed_OrdinalIsFive()        => Assert.AreEqual(5, (int)TrackerEventPhase.Failed);
        [Test] public void Phase_TimedOut_OrdinalIsSix()       => Assert.AreEqual(6, (int)TrackerEventPhase.TimedOut);

        // ═══════════════════════════════════════════════════════════════════
        // TrackerEventPhaseEx helpers
        // ═══════════════════════════════════════════════════════════════════

        [Test]
        public void FromRaw_ValidValues_RoundTrip()
        {
            for (int i = 0; i <= 6; i++)
            {
                Assert.AreEqual((TrackerEventPhase)i, TrackerEventPhaseEx.FromRaw(i),
                    $"FromRaw({i}) must return (TrackerEventPhase){i}");
            }
        }

        [Test]
        public void FromRaw_NegativeValue_ReturnsQueued()
        {
            Assert.AreEqual(TrackerEventPhase.Queued, TrackerEventPhaseEx.FromRaw(-1));
        }

        [Test]
        public void FromRaw_OutOfRangeHigh_ReturnsQueued()
        {
            Assert.AreEqual(TrackerEventPhase.Queued, TrackerEventPhaseEx.FromRaw(7));
        }

        [Test]
        public void IsTerminal_Acknowledged_IsTrue()   => Assert.IsTrue(TrackerEventPhase.Acknowledged.IsTerminal());
        [Test]
        public void IsTerminal_Failed_IsTrue()         => Assert.IsTrue(TrackerEventPhase.Failed.IsTerminal());
        [Test]
        public void IsTerminal_TimedOut_IsTrue()       => Assert.IsTrue(TrackerEventPhase.TimedOut.IsTerminal());
        [Test]
        public void IsTerminal_Queued_IsFalse()        => Assert.IsFalse(TrackerEventPhase.Queued.IsTerminal());
        [Test]
        public void IsTerminal_Sending_IsFalse()       => Assert.IsFalse(TrackerEventPhase.Sending.IsTerminal());
        [Test]
        public void IsTerminal_Emitted_IsFalse()       => Assert.IsFalse(TrackerEventPhase.Emitted.IsTerminal());
        [Test]
        public void IsTerminal_Uploading_IsFalse()     => Assert.IsFalse(TrackerEventPhase.Uploading.IsTerminal());
    }
}
