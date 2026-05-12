using System;
using Cysharp.Threading.Tasks;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// EditMode NUnit tests for <see cref="Utility.RetryAsyncTask{T}"/> covering the
    /// code paths that complete <em>without</em> any real async delay.
    ///
    /// All 6 tests in <c>UtilityTest</c> use <c>[UnityTest]</c> / <c>UniTask.ToCoroutine</c>.
    /// Four of those exercise timing-sensitive retry paths (<c>await UniTask.Delay(…)</c>)
    /// and must remain PlayMode tests.  Two paths are genuinely synchronous:
    ///
    ///   1. <b>Success on the first try</b> — the method performs a single <c>await task()</c>
    ///      that resolves immediately; no delay is introduced.
    ///   2. <b>Non-networking exception</b> — the catch-block <c>rethrow</c>s before
    ///      the <c>await UniTask.Delay(…)</c> call is reached.
    ///   3. <b>Non-<see cref="NoctuaException"/> exception</b> — the exception propagates
    ///      out of the <c>catch (NoctuaException)</c> block immediately.
    ///   4. <b>Zero max-retries</b> — the for-loop body never runs, so no delay is
    ///      incurred; the single final <c>await task()</c> throws immediately.
    ///
    /// All four cases use <c>.GetAwaiter().GetResult()</c>: when a <c>UniTask</c> is
    /// already complete (or faults synchronously), <c>GetResult()</c> returns or
    /// rethrows without entering the UniTask scheduler.
    /// </summary>
    [TestFixture]
    public class RetryAsyncTaskEditModeTest
    {
        // ─── Success on first try ──────────────────────────────────────────────

        [Test]
        public void RetryAsyncTask_SuccessOnFirstTry_ReturnsExpectedResult()
        {
            // task() returns a pre-completed UniTask — RetryAsyncTask does a single
            // await and returns without any delay.
            var result = Utility.RetryAsyncTask(
                async () => await UniTask.FromResult("hello")
            ).GetAwaiter().GetResult();

            Assert.AreEqual("hello", result);
        }

        [Test]
        public void RetryAsyncTask_SuccessOnFirstTry_IntResult()
        {
            var result = Utility.RetryAsyncTask(
                async () => await UniTask.FromResult(42)
            ).GetAwaiter().GetResult();

            Assert.AreEqual(42, result);
        }

        // ─── Non-networking NoctuaException — rethrown immediately ────────────

        [Test]
        public void RetryAsyncTask_ThrowsApplicationException_RethrowsWithoutRetrying()
        {
            // NoctuaErrorCode.Application (3002) is NOT NoctuaErrorCode.Networking (3001),
            // so the catch-block calls `throw` before reaching any delay.
            int callCount = 0;
            var ex = Assert.Throws<NoctuaException>(() =>
                Utility.RetryAsyncTask<object>(async () =>
                {
                    callCount++;
                    throw new NoctuaException(NoctuaErrorCode.Application, "app error");
                }).GetAwaiter().GetResult());

            Assert.AreEqual((int)NoctuaErrorCode.Application, ex.ErrorCode,
                "Exception code must be preserved on re-throw");
            Assert.AreEqual(1, callCount,
                "Task must be called exactly once — non-networking errors are not retried");
        }

        [Test]
        public void RetryAsyncTask_ThrowsAuthenticationException_RethrowsWithoutRetrying()
        {
            int callCount = 0;
            Assert.Throws<NoctuaException>(() =>
                Utility.RetryAsyncTask<string>(async () =>
                {
                    callCount++;
                    throw new NoctuaException(NoctuaErrorCode.Authentication, "auth error");
                }).GetAwaiter().GetResult());

            Assert.AreEqual(1, callCount);
        }

        // ─── Generic (non-NoctuaException) exception ──────────────────────────

        [Test]
        public void RetryAsyncTask_ThrowsGenericException_RethrowsImmediately()
        {
            // A plain Exception is not caught by catch (NoctuaException), so it
            // propagates immediately — no delay, task called exactly once.
            int callCount = 0;
            Assert.Throws<InvalidOperationException>(() =>
                Utility.RetryAsyncTask<object>(async () =>
                {
                    callCount++;
                    throw new InvalidOperationException("generic error");
                }).GetAwaiter().GetResult());

            Assert.AreEqual(1, callCount,
                "Generic exceptions must propagate without any retry attempt");
        }

        [Test]
        public void RetryAsyncTask_ThrowsArgumentException_RethrowsImmediately()
        {
            Assert.Throws<ArgumentException>(() =>
                Utility.RetryAsyncTask<object>(async () =>
                    throw new ArgumentException("bad arg")
                ).GetAwaiter().GetResult());
        }

        // ─── MaxRetries = 0 (zero-retry path — no loop body, no delay) ────────

        [Test]
        public void RetryAsyncTask_MaxRetries0_CallsTaskExactlyOnce_ThenThrows()
        {
            // When maxRetries=0 the for-loop never executes.
            // The single `return await task()` after the loop is the only call.
            // If that throws Networking, it propagates — no delay.
            int callCount = 0;
            Assert.Throws<NoctuaException>(() =>
                Utility.RetryAsyncTask<object>(
                    {
                        callCount++;
                        throw new NoctuaException(NoctuaErrorCode.Networking, "network error");
                    },
                    maxRetries: 0
                ).GetAwaiter().GetResult());

            // maxRetries=0 → loop executes 0 times → 1 attempt (the post-loop call)
            Assert.AreEqual(1, callCount,
                "With maxRetries=0, task must be called exactly once (the post-loop attempt)");
        }
    }
}
