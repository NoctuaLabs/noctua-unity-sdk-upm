using System;
using System.Threading;
using System.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Bounds an await so a native callback that never arrives cannot hang the caller forever.
    /// </summary>
    /// <remarks>
    /// Native bridges (JNI on Android, P/Invoke on iOS) hand results back through callbacks that
    /// complete a <see cref="TaskCompletionSource{TResult}"/>. When the platform never invokes the
    /// callback — Google Play billing cannot connect, the store reports the failure down a
    /// different callback channel, the process is throttled — that TCS is never completed, never
    /// faulted and never cancelled. A bare <c>await</c> on it blocks forever, and no
    /// <c>try</c>/<c>catch</c> can rescue it: a task that never completes is not an exception.
    ///
    /// This helper separates the two failure modes deliberately:
    /// <list type="bullet">
    /// <item><description><b>Silence</b> is handled here — the caller gets
    /// <c>TimedOut == true</c> and applies its own fallback.</description></item>
    /// <item><description><b>Errors</b> are not swallowed — a faulted or cancelled task propagates
    /// its exception unchanged, so existing error handling still runs.</description></item>
    /// </list>
    ///
    /// Deliberately pure BCL (no UniTask, no Unity main loop, no platform types) so the behavior is
    /// unit-testable in EditMode, unlike the platform call sites that use it. See
    /// <see cref="NativeCallbackQueue{T}"/> for the related fix to callbacks that arrive but get
    /// delivered to the wrong waiter.
    /// </remarks>
    public static class TaskTimeout
    {
        /// <summary>
        /// Awaits <paramref name="task"/> until it settles or <paramref name="timeoutMs"/> elapses,
        /// whichever happens first.
        /// </summary>
        /// <typeparam name="T">The task's result type.</typeparam>
        /// <param name="task">The task to bound. Typically a native callback's TaskCompletionSource.</param>
        /// <param name="timeoutMs">Deadline in milliseconds. Must be greater than zero.</param>
        /// <returns>
        /// <c>(false, result)</c> when the task settled in time; <c>(true, default)</c> when the
        /// deadline elapsed first. On timeout the abandoned task is left unobserved — a callback
        /// arriving later completes it harmlessly.
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="task"/> is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeoutMs"/> is not positive.</exception>
        public static async Task<(bool TimedOut, T Value)> OrTimeoutAsync<T>(Task<T> task, int timeoutMs)
        {
            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (timeoutMs <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(timeoutMs),
                    timeoutMs,
                    "Timeout must be greater than zero milliseconds."
                );
            }

            // The CTS cancels the pending timer as soon as the task wins, so bounded awaits do not
            // accumulate live timers for the full deadline on every call.
            using var timeoutCts = new CancellationTokenSource();

            var timeoutTask = Task.Delay(timeoutMs, timeoutCts.Token);
            var firstSettled = await Task.WhenAny(task, timeoutTask).ConfigureAwait(false);

            if (!ReferenceEquals(firstSettled, task))
            {
                return (true, default(T));
            }

            timeoutCts.Cancel();

            // Awaited rather than read via .Result so a fault or cancellation propagates as the
            // original exception instead of an AggregateException.
            return (false, await task.ConfigureAwait(false));
        }
    }
}
