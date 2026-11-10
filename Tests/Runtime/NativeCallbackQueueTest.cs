using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime
{
    /// <summary>
    /// Unit tests for <see cref="NativeCallbackQueue{T}"/> — the FIFO queue that replaced the
    /// single-slot callback fields in GoogleBilling.cs (Android). The iOS StoreKit bridge uses
    /// StoreKitRequestRouter instead (see StoreKitRequestRouterTest).
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

        // ─── Keyed (by product id) ─────────────────────────────────────────────

        [Test]
        public void Keyed_ResponsesInReverseOrder_EachReachesItsOwnCaller()
        {
            // The realme repro: Cat 1 (noads) and Cat 2 (pack4) checked together; Play answered
            // pack4 first. FIFO gave noads's caller pack4's answer and vice versa.
            var queue = new NativeCallbackQueue<bool>();
            bool? noadsOwned = null, pack4Owned = null;

            queue.Enqueue("noads", owned => noadsOwned = owned);
            queue.Enqueue("pack4", owned => pack4Owned = owned);

            queue.InvokeNext("pack4", false);
            queue.InvokeNext("noads", true);

            Assert.AreEqual(true, noadsOwned);
            Assert.AreEqual(false, pack4Owned);
            Assert.AreEqual(0, queue.Count);
        }

        [Test]
        public void Keyed_SameKeyTwice_CompletesInCallOrder()
        {
            var queue = new NativeCallbackQueue<string>();
            var order = new List<string>();

            queue.Enqueue("noads", v => order.Add("first:" + v));
            queue.Enqueue("noads", v => order.Add("second:" + v));

            queue.InvokeNext("noads", "a");
            queue.InvokeNext("noads", "b");

            CollectionAssert.AreEqual(new[] { "first:a", "second:b" }, order);
        }

        [Test]
        public void Keyed_ResponseForUnknownKey_IsDropped_AndOthersStillWait()
        {
            var queue = new NativeCallbackQueue<bool>();
            var fired = false;
            queue.Enqueue("noads", _ => fired = true);

            Assert.IsFalse(queue.InvokeNext("pack4", true));

            Assert.IsFalse(fired);
            Assert.AreEqual(1, queue.Count);
        }

        [Test]
        public void Keyed_ResponseWithoutKey_FallsBackToOldest()
        {
            // An older native build that leaves the id empty must still complete callers, not hang.
            var queue = new NativeCallbackQueue<bool>();
            bool? got = null;
            queue.Enqueue("noads", v => got = v);

            Assert.IsTrue(queue.InvokeNext(null, true));

            Assert.AreEqual(true, got);
        }

        [Test]
        public void Keyed_ResponseForUnmatchedKey_CompletesAnUnkeyedCaller()
        {
            var queue = new NativeCallbackQueue<bool>();
            bool? unkeyed = null, keyed = null;
            queue.Enqueue("noads", v => keyed = v);
            queue.Enqueue(v => unkeyed = v);

            queue.InvokeNext("pack4", true);

            Assert.AreEqual(true, unkeyed);
            Assert.IsNull(keyed);
        }

        [Test]
        public void UnkeyedApi_StillPlainFifoAcrossKeys()
        {
            var queue = new NativeCallbackQueue<int>();
            var order = new List<string>();
            queue.Enqueue("b", v => order.Add("b"));
            queue.Enqueue("a", v => order.Add("a"));

            queue.InvokeNext(0);
            queue.InvokeNext(0);

            CollectionAssert.AreEqual(new[] { "b", "a" }, order);
        }

        [Test]
        public void Keyed_NullCallback_IsConsumedWithoutThrowing()
        {
            var queue = new NativeCallbackQueue<int>();
            queue.Enqueue("noads", null);

            Assert.DoesNotThrow(() => queue.InvokeNext("noads", 1));
            Assert.AreEqual(0, queue.Count);
        }
    }
}
