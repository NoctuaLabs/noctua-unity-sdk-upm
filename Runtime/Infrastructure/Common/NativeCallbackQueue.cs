using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// FIFO queue of pending callbacks for a native call that funnels every response through a
    /// single event/trampoline, keyed only by call order (no per-call request id available from
    /// the native side).
    ///
    /// This exists to fix a real deadlock class: a single-slot callback field (<c>Action&lt;T&gt;
    /// _pending;</c>) gets silently overwritten when a second call goes out before the first
    /// call's native response arrives — e.g. two concurrent purchase-status checks, one from a
    /// real purchase flow and one from the SDK's own background retry worker. The first caller's
    /// <c>TaskCompletionSource</c> then never completes and hangs forever, with no exception.
    ///
    /// A FIFO queue removes the overwrite: every call enqueues its own callback, and every native
    /// response dequeues and invokes the oldest one. This is correct exactly when the native side
    /// answers calls in the order they were made (true for both the Android JNI bridge and the
    /// iOS P/Invoke trampolines this class backs — see <c>GoogleBilling.cs</c> and
    /// <c>IosPlugin.cs</c>), and is a strict improvement over the single-slot field either way:
    /// callbacks always eventually fire instead of some of them hanging forever.
    ///
    /// Platform-agnostic and dependency-free by design (no UnityEngine / native plugin
    /// references) so the FIFO behavior itself is unit-testable in EditMode, unlike the
    /// P/Invoke/JNI call sites that use it (see
    /// <c>Packages/com.noctuagames.sdk/CLAUDE.md</c>'s EditMode coverage exclusions for
    /// <c>Runtime/Platform/iOS/**</c> and <c>Runtime/Platform/Android/**</c>).
    /// </summary>
    public class NativeCallbackQueue<T>
    {
        private readonly Queue<Action<T>> _pending = new();

        /// <summary>Number of callbacks currently waiting for a native response.</summary>
        public int Count => _pending.Count;

        /// <summary>Enqueues a callback to be invoked by the next matching native response.</summary>
        public void Enqueue(Action<T> callback)
        {
            _pending.Enqueue(callback);
        }

        /// <summary>
        /// Dequeues the oldest pending callback, if any. Returns <c>false</c> (and a <c>null</c>
        /// callback) when the queue is empty — e.g. an unsolicited/unmatched native response.
        /// </summary>
        public bool TryDequeue(out Action<T> callback)
        {
            return _pending.TryDequeue(out callback);
        }

        /// <summary>
        /// Dequeues the oldest pending callback and invokes it with <paramref name="value"/>.
        /// No-op if the queue is empty (an unsolicited/unmatched native response) or if the
        /// dequeued callback is itself null.
        /// </summary>
        public void InvokeNext(T value)
        {
            if (_pending.TryDequeue(out var callback))
            {
                callback?.Invoke(value);
            }
        }
    }
}
