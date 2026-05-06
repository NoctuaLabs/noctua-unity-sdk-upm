using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="UnityLogStream"/>:
    ///   — <see cref="UnityLogStream.Start"/> and <see cref="UnityLogStream.Stop"/>
    ///     subscribe / unsubscribe from <see cref="UnityEngine.Application.logMessageReceivedThreaded"/>
    ///     without throwing, and are idempotent (calling each twice is safe).
    ///   — <see cref="UnityLogStream.Dispose"/> calls Stop without throwing.
    ///
    /// These tests verify the subscription lifecycle guards. They do NOT test
    /// that log messages actually flow through — that requires a real Unity log
    /// event which is non-deterministic in EditMode.
    /// </summary>
    [TestFixture]
    public class UnityLogStreamEditModeTest
    {
        private UnityLogStream _stream;

        [SetUp]
        public void SetUp()
        {
            _stream = new UnityLogStream();
        }

        [TearDown]
        public void TearDown()
        {
            // Always stop after each test to avoid leaking the subscription
            // into subsequent tests.
            try { _stream?.Stop(); } catch { /* best-effort */ }
        }

        // ─── Start ────────────────────────────────────────────────────────────

        [Test]
        public void Start_FirstCall_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _stream.Start());
        }

        [Test]
        public void Start_CalledTwice_DoesNotThrow()
        {
            // _started guard prevents double-subscription.
            _stream.Start();
            Assert.DoesNotThrow(() => _stream.Start());
        }

        // ─── Stop ─────────────────────────────────────────────────────────────

        [Test]
        public void Stop_WithoutStart_DoesNotThrow()
        {
            // _started is false initially → Stop() should be a no-op.
            Assert.DoesNotThrow(() => _stream.Stop());
        }

        [Test]
        public void Stop_AfterStart_DoesNotThrow()
        {
            _stream.Start();
            Assert.DoesNotThrow(() => _stream.Stop());
        }

        [Test]
        public void Stop_CalledTwice_DoesNotThrow()
        {
            _stream.Start();
            _stream.Stop();
            // Second Stop is a no-op because _started is already false.
            Assert.DoesNotThrow(() => _stream.Stop());
        }

        // ─── Start → Stop cycle ───────────────────────────────────────────────

        [Test]
        public void StartStop_Cycle_DoesNotThrow()
        {
            // Full subscribe / unsubscribe round-trip.
            Assert.DoesNotThrow(() =>
            {
                _stream.Start();
                _stream.Stop();
            });
        }

        [Test]
        public void StartStop_MultipleCycles_DoesNotThrow()
        {
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < 3; i++)
                {
                    _stream.Start();
                    _stream.Stop();
                }
            });
        }

        // ─── Dispose ──────────────────────────────────────────────────────────

        [Test]
        public void Dispose_WithoutStart_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => _stream.Dispose());
        }

        [Test]
        public void Dispose_AfterStart_DoesNotThrow()
        {
            _stream.Start();
            Assert.DoesNotThrow(() => _stream.Dispose());
        }

        [Test]
        public void Dispose_CalledTwice_DoesNotThrow()
        {
            _stream.Start();
            _stream.Dispose();
            // Dispose delegates to Stop which uses _started guard — safe to call again.
            Assert.DoesNotThrow(() => _stream.Dispose());
        }

        [Test]
        public void UsingStatement_DoesNotThrow()
        {
            // Verify that the IDisposable pattern works correctly.
            Assert.DoesNotThrow(() =>
            {
                using var stream = new UnityLogStream();
                stream.Start();
                // Dispose called automatically at end of using block.
            });
        }
    }
}
