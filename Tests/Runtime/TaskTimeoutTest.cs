using System;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for <see cref="TaskTimeout"/> — the bounded-await helper that stops SDK
    /// initialization from hanging forever on a native callback that never arrives.
    ///
    /// The scenario these tests pin down: on Android, <c>GetActiveCurrencyAsync</c> awaits a
    /// TaskCompletionSource that is completed only by Google Play's <c>onProductDetailsLoaded</c>
    /// callback. When the Play billing client cannot connect (user not signed in to Google Play,
    /// Play Store disabled or blocked — frequently observed on OPPO/ColorOS), the native SDK
    /// reports the failure down a *different* channel (<c>onBillingError</c>) and that TCS is
    /// never completed, never faulted and never cancelled. A plain <c>await</c> on it blocks
    /// <c>Noctua.InitAsync()</c> permanently, so the game never leaves its loading screen.
    ///
    /// A task that never completes is not an exception, so try/catch cannot rescue it — only a
    /// timeout can. These tests use pure BCL tasks (no Unity main loop, no JNI), so the timeout
    /// behavior is verifiable in EditMode unlike the platform call sites that use it (see
    /// <c>Packages/com.noctuagames.sdk/CLAUDE.md</c>'s EditMode coverage exclusions for
    /// <c>Runtime/Platform/Android/**</c>).
    /// </summary>
    [TestFixture]
    public class TaskTimeoutTest
    {
        private const int ShortTimeoutMs = 100;
        private const int LongTimeoutMs = 10000;

        [Test]
        public void OrTimeout_TaskCompletesBeforeDeadline_ReturnsValueAndDoesNotTimeOut()
        {
            var tcs = new TaskCompletionSource<string>();
            tcs.SetResult("JPY");

            var (timedOut, value) = TaskTimeout.OrTimeoutAsync(tcs.Task, LongTimeoutMs)
                .GetAwaiter().GetResult();

            Assert.IsFalse(timedOut, "A task that already has a result must not report a timeout.");
            Assert.AreEqual("JPY", value);
        }

        [Test]
        public void OrTimeout_TaskNeverCompletes_ReportsTimeoutAndReturnsDefault()
        {
            // The exact production failure: Google Play never invokes onProductDetailsLoaded.
            var neverCompletes = new TaskCompletionSource<string>();

            var (timedOut, value) = TaskTimeout.OrTimeoutAsync(neverCompletes.Task, ShortTimeoutMs)
                .GetAwaiter().GetResult();

            Assert.IsTrue(timedOut, "A task that never completes must report a timeout, not hang.");
            Assert.IsNull(value, "On timeout the caller gets default(T) and applies its own fallback.");
        }

        [Test]
        public void OrTimeout_TaskCompletesLate_StillReturnsPromptlyWithTimeout()
        {
            var tcs = new TaskCompletionSource<string>();
            // Completes well after the deadline — the helper must not wait for it.
            _ = Task.Run(async () =>
            {
                await Task.Delay(ShortTimeoutMs * 8);
                tcs.TrySetResult("late");
            });

            var startedAt = DateTime.UtcNow;
            var (timedOut, _) = TaskTimeout.OrTimeoutAsync(tcs.Task, ShortTimeoutMs)
                .GetAwaiter().GetResult();
            var elapsed = DateTime.UtcNow - startedAt;

            Assert.IsTrue(timedOut);
            Assert.Less(
                elapsed.TotalMilliseconds,
                ShortTimeoutMs * 6,
                "The helper must return at its own deadline, not wait for the slow task."
            );
        }

        [Test]
        public void OrTimeout_LateCompletionAfterTimeout_DoesNotThrow()
        {
            // A callback arriving after we gave up must stay harmless — the abandoned task's
            // result is simply never observed.
            var tcs = new TaskCompletionSource<string>();

            var (timedOut, _) = TaskTimeout.OrTimeoutAsync(tcs.Task, ShortTimeoutMs)
                .GetAwaiter().GetResult();
            Assert.IsTrue(timedOut);

            Assert.DoesNotThrow(() => tcs.TrySetResult("arrived too late"));
        }

        [Test]
        public void OrTimeout_TaskFaults_PropagatesExceptionInsteadOfSwallowingIt()
        {
            // Billing reported a real failure. That must stay an exception so the caller's
            // existing catch path runs — the timeout is only for silence, not for errors.
            var tcs = new TaskCompletionSource<string>();
            tcs.SetException(NoctuaException.ActiveCurrencyFailure);

            var thrown = Assert.Throws<NoctuaException>(() =>
                TaskTimeout.OrTimeoutAsync(tcs.Task, LongTimeoutMs).GetAwaiter().GetResult()
            );

            Assert.AreEqual((int)NoctuaErrorCode.ActiveCurrencyFailure, thrown.ErrorCode);
        }

        [Test]
        public void OrTimeout_TaskCancelled_PropagatesCancellation()
        {
            var tcs = new TaskCompletionSource<string>();
            tcs.SetCanceled();

            Assert.Throws<TaskCanceledException>(() =>
                TaskTimeout.OrTimeoutAsync(tcs.Task, LongTimeoutMs).GetAwaiter().GetResult()
            );
        }

        [Test]
        public void OrTimeout_NullTask_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() =>
                TaskTimeout.OrTimeoutAsync<string>(null, ShortTimeoutMs).GetAwaiter().GetResult()
            );
        }

        [Test]
        public void OrTimeout_NonPositiveTimeout_ThrowsArgumentOutOfRangeException()
        {
            var tcs = new TaskCompletionSource<string>();

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                TaskTimeout.OrTimeoutAsync(tcs.Task, 0).GetAwaiter().GetResult()
            );
        }

        [Test]
        public void OrTimeout_ValueTypeResult_ReturnsDefaultOnTimeout()
        {
            var neverCompletes = new TaskCompletionSource<int>();

            var (timedOut, value) = TaskTimeout.OrTimeoutAsync(neverCompletes.Task, ShortTimeoutMs)
                .GetAwaiter().GetResult();

            Assert.IsTrue(timedOut);
            Assert.AreEqual(0, value);
        }
    }
}
