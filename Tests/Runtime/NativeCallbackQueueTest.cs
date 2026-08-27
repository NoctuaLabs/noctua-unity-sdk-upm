using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for <see cref="NativeCallbackQueue{T}"/> — the FIFO queue that replaced the
    /// single-slot callback fields in GoogleBilling.cs (Android) and IosPlugin.cs (iOS).
    ///
    /// These tests exercise exactly the scenario that used to deadlock purchase completion: two
    /// calls going out before either native response arrives. With the old single-slot field,
    /// the second call's callback silently clobbered the first, so the first caller's
    /// TaskCompletionSource never completed and hung forever. With the queue, both calls'
    /// callbacks fire, in the order they were made.
    /// </summary>
    [TestFixture]
    public class NativeCallbackQueueTest
    {
        [Test]
        public void Enqueue_ThenInvokeNext_InvokesCallbackWithValue()
        {
            var queue = new NativeCallbackQueue<string>();
            string received = null;

            queue.Enqueue(value => received = value);
            queue.InvokeNext("hello");

            Assert.AreEqual("hello", received);
        }

        [Test]
        public void TwoOverlappingCalls_BothCallbacksFire_InFifoOrder()
        {
            // This is the exact race that caused the deadlock: call A enqueues, then call B
            // enqueues, before either native response has arrived. Both native responses then
            // arrive one after another. With a single-slot field, B's assignment would have
            // overwritten A's callback and A would never fire. With the queue, A fires first
            // (it was enqueued first), then B.
            var queue = new NativeCallbackQueue<int>();
            var invokedInOrder = new List<int>();

            queue.Enqueue(value => invokedInOrder.Add(value)); // "call A"
            queue.Enqueue(value => invokedInOrder.Add(value)); // "call B" — would have clobbered A's slot

            queue.InvokeNext(1); // native response for call A
            queue.InvokeNext(2); // native response for call B

            Assert.AreEqual(new List<int> { 1, 2 }, invokedInOrder);
        }

        [Test]
        public void InvokeNext_OnEmptyQueue_DoesNotThrow()
        {
            var queue = new NativeCallbackQueue<int>();

            Assert.DoesNotThrow(() => queue.InvokeNext(42));
        }

        [Test]
        public void InvokeNext_WithNullCallback_DoesNotThrow()
        {
            var queue = new NativeCallbackQueue<int>();
            queue.Enqueue(null);

            Assert.DoesNotThrow(() => queue.InvokeNext(42));
        }

        [Test]
        public void TryDequeue_OnEmptyQueue_ReturnsFalse()
        {
            var queue = new NativeCallbackQueue<int>();

            var found = queue.TryDequeue(out var callback);

            Assert.IsFalse(found);
            Assert.IsNull(callback);
        }

        [Test]
        public void TryDequeue_ReturnsOldestCallbackFirst()
        {
            var queue = new NativeCallbackQueue<int>();
            var invokedInOrder = new List<int>();

            queue.Enqueue(value => invokedInOrder.Add(value));
            queue.Enqueue(value => invokedInOrder.Add(value * 10));

            Assert.IsTrue(queue.TryDequeue(out var first));
            first(1);
            Assert.IsTrue(queue.TryDequeue(out var second));
            second(1);

            Assert.AreEqual(new List<int> { 1, 10 }, invokedInOrder);
        }

        [Test]
        public void Count_ReflectsPendingCallbacks()
        {
            var queue = new NativeCallbackQueue<int>();
            Assert.AreEqual(0, queue.Count);

            queue.Enqueue(_ => { });
            queue.Enqueue(_ => { });
            Assert.AreEqual(2, queue.Count);

            queue.InvokeNext(0);
            Assert.AreEqual(1, queue.Count);
        }
    }
}
