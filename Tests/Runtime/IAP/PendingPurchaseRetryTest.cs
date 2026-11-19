using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using com.noctuagames.sdk;

namespace Tests.Runtime.IAP
{
    /// <summary>
    /// One pass of the pending-purchase retry (<c>RetryPendingPurchaseOnceAsync</c>) against a
    /// fake <c>/verify-order</c>, for every answer the backend can give: what stays queued, what
    /// is dropped, what is retried again, and when the game hears about it.
    /// </summary>
    [TestFixture]
    public class PendingPurchaseRetryTest
    {
        private const string BaseUrl = "http://localhost:7793/api/v1";
        private const string ServerUrl = "http://localhost:7793/api/v1/";
        private const string VerifyPath = "/verify-order";
        private const string PendingKey = "NoctuaPendingPurchases";
        private const string HistoryKey = "NoctuaPurchaseHistory";
        private const string AccessTokenKey = "NoctuaAccessToken";
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        private HttpMockServer _server;
        private readonly Dictionary<string, string> _savedPrefs = new();

        private class StubAuthProvider : IAuthProvider
        {
            public long? PlayerId => 42;
            public UserBundle RecentAccount => new() { Player = new Player { Id = 42, GameId = 1 } };
            public UniTask<UserBundle> AuthenticateAsync() => UniTask.FromResult(RecentAccount);
            public UniTask UpdatePlayerAccountAsync(PlayerAccountData data) => UniTask.CompletedTask;
        }

        private class StubAccountEvents : IAccountEvents
        {
            public event Action<UserBundle> OnAccountChanged { add { } remove { } }
            public event Action<Player> OnAccountDeleted { add { } remove { } }
        }

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            _server = new HttpMockServer(ServerUrl);
            _server.Start();
        }

        [OneTimeTearDown]
        public void OneTimeTearDown() => _server.Dispose();

        [SetUp]
        public void SetUp()
        {
            foreach (var key in new[] { PendingKey, HistoryKey, AccessTokenKey })
            {
                _savedPrefs[key] = PlayerPrefs.GetString(key, null);
                PlayerPrefs.DeleteKey(key);
            }
            PlayerPrefs.SetString(AccessTokenKey, "test-access-token");
            PlayerPrefs.Save();

            _server.RemoveHandler(VerifyPath);
            while (_server.Requests.TryDequeue(out _)) { }
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var saved in _savedPrefs)
            {
                if (string.IsNullOrEmpty(saved.Value)) PlayerPrefs.DeleteKey(saved.Key);
                else PlayerPrefs.SetString(saved.Key, saved.Value);
            }
            PlayerPrefs.Save();
            LogAssert.ignoreFailingMessages = false;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static InternalPurchaseItem StoreOrder(int orderId, string receipt = "play-token",
            PaymentType paymentType = PaymentType.playstore)
        {
            return new InternalPurchaseItem
            {
                OrderId = orderId,
                OrderRequest = new OrderRequest
                {
                    Id = orderId,
                    ProductId = "pack" + orderId,
                    PaymentType = paymentType,
                    Type = IapProductType.consumable,
                },
                VerifyOrderRequest = new VerifyOrderRequest { Id = orderId, ReceiptData = receipt },
                AccessToken = "order-access-token",
                Status = "pending",
                PlayerId = 42,
            };
        }

        /// <summary>A service whose retry queue was persisted by an earlier session.</summary>
        private static NoctuaIAPService ServiceWithQueue(params InternalPurchaseItem[] queued)
        {
            // A rejected verify logs an error on purpose. Set here, inside the test body: the flag
            // set in SetUp does not reach a [UnityTest] once the SDK logger is live.
            LogAssert.ignoreFailingMessages = true;

            PlayerPrefs.SetString(PendingKey, JsonConvert.SerializeObject(queued));
            PlayerPrefs.Save();

            var service = new NoctuaIAPService(
                config: new NoctuaIAPService.Config { BaseUrl = BaseUrl, ClientId = "test-client-id" },
                accessTokenProvider: new AccessTokenProvider(new StubAccountEvents()),
                paymentUI: null,
                nativePlugin: null,
                authProvider: new StubAuthProvider());
            service.Enable();

            return service;
        }

        private void ServerAnswers(string orderStatus) => _server.AddHandler(VerifyPath,
            _ => "{\"success\":true,\"data\":{\"id\":0,\"order_status\":\"" + orderStatus + "\"}}");

        private void ServerRejects(int httpStatus, int errorCode, string message) => _server.AddHandler(VerifyPath,
            _ => (httpStatus, "{\"success\":false,\"error_code\":" + errorCode + ",\"error_message\":\"" + message + "\"}"));

        private int VerifyRequestCount =>
            _server.Requests.Count(r => r.Path.EndsWith(VerifyPath, StringComparison.Ordinal));

        /// <summary>The orders the retry loop would send on its next pass.</summary>
        private static List<InternalPurchaseItem> RetryBatch(NoctuaIAPService service) =>
            (List<InternalPurchaseItem>)typeof(NoctuaIAPService)
                .GetMethod("GetRetryablePendingPurchases", Private).Invoke(service, null);

        /// <summary>One retry pass: every order in the batch is verified once, like the loop does.</summary>
        private static async UniTask RetryPassAsync(NoctuaIAPService service)
        {
            var retryOnce = typeof(NoctuaIAPService).GetMethod("RetryPendingPurchaseOnceAsync", Private);
            foreach (var item in RetryBatch(service))
            {
                await (UniTask)retryOnce.Invoke(service, new object[] { item });
            }
        }

        private static void Enqueue(NoctuaIAPService service, InternalPurchaseItem item) =>
            typeof(NoctuaIAPService).GetMethod("EnqueueToRetryPendingPurchases", Private)
                .Invoke(service, new object[] { item });

        private static List<int> QueuedIds(NoctuaIAPService service) =>
            service.GetPendingPurchases().Select(item => item.OrderId).ToList();

        // ── Completed ─────────────────────────────────────────────────────────

        [UnityTest]
        public IEnumerator Completed_DeliversOnce_AndLeavesTheQueue() => UniTask.ToCoroutine(async () =>
        {
            var service = ServiceWithQueue(StoreOrder(5));
            var delivered = new List<int>();
            service.OnPurchaseDone += order => delivered.Add(order.Id);
            ServerAnswers("completed");

            await RetryPassAsync(service);

            CollectionAssert.AreEqual(new[] { 5 }, delivered, "the game gets the order once");
            CollectionAssert.IsEmpty(QueuedIds(service), "a completed order leaves the retry queue");
            Assert.IsTrue(service.GetPurchaseHistory().Any(item => item.OrderId == 5 && item.Status == "completed"));
            CollectionAssert.IsEmpty(RetryBatch(service), "nothing left to retry");
        });

        [UnityTest]
        public IEnumerator CompletedOrder_VerifiedAgain_IsNotDeliveredTwice() => UniTask.ToCoroutine(async () =>
        {
            var service = ServiceWithQueue(StoreOrder(5));
            var delivered = new List<int>();
            service.OnPurchaseDone += order => delivered.Add(order.Id);
            ServerAnswers("completed");
            await RetryPassAsync(service);

            // The store reports the same purchase again (e.g. not consumed yet) and it is re-queued.
            Enqueue(service, StoreOrder(5));
            await RetryPassAsync(service);

            CollectionAssert.AreEqual(new[] { 5 }, delivered, "OnPurchaseDone fires once per order");
            CollectionAssert.IsEmpty(QueuedIds(service));
        });

        [UnityTest]
        public IEnumerator Completed_SurvivesRestart_AsNotQueued() => UniTask.ToCoroutine(async () =>
        {
            var first = ServiceWithQueue(StoreOrder(5), StoreOrder(6));
            _server.AddHandler(VerifyPath, request => "{\"success\":true,\"data\":{\"order_status\":\"completed\"}}");
            await RetryPassAsync(first);

            // Next launch: a new service reads what the last one saved.
            var second = new NoctuaIAPService(
                config: new NoctuaIAPService.Config { BaseUrl = BaseUrl, ClientId = "test-client-id" },
                accessTokenProvider: new AccessTokenProvider(new StubAccountEvents()),
                paymentUI: null, nativePlugin: null, authProvider: new StubAuthProvider());
            second.Enable();

            CollectionAssert.IsEmpty(QueuedIds(second), "completed orders are not retried after a restart");
        });

        // ── Dropped without delivery ──────────────────────────────────────────

        [UnityTest]
        public IEnumerator ReceiptUsedByAnotherOrder_IsDropped_WithoutDelivery() => UniTask.ToCoroutine(async () =>
        {
            var service = ServiceWithQueue(StoreOrder(5));
            var delivered = new List<int>();
            service.OnPurchaseDone += order => delivered.Add(order.Id);
            ServerRejects(400, 2042, "ReceiptData is already used.");

            await RetryPassAsync(service);

            CollectionAssert.IsEmpty(delivered, "the other order owns this payment");
            CollectionAssert.IsEmpty(QueuedIds(service), "a duplicate is dropped, not retried forever");
        });

        [UnityTest]
        public IEnumerator Voided_IsDropped_WithoutDelivery() => UniTask.ToCoroutine(async () =>
        {
            var service = ServiceWithQueue(StoreOrder(5));
            var delivered = new List<int>();
            service.OnPurchaseDone += order => delivered.Add(order.Id);
            ServerRejects(400, 2048, "voided");

            await RetryPassAsync(service);

            CollectionAssert.IsEmpty(delivered);
            CollectionAssert.IsEmpty(QueuedIds(service));
        });

        // ── Final, kept for the pending-purchases dialog, never retried ────────

        [UnityTest]
        public IEnumerator Canceled_StaysListed_ButIsNotRetried() => UniTask.ToCoroutine(async () =>
        {
            var service = ServiceWithQueue(StoreOrder(5));
            ServerRejects(400, 2046, "canceled");

            await RetryPassAsync(service);
            await RetryPassAsync(service);

            Assert.AreEqual(1, VerifyRequestCount, "a canceled order is verified once, then left alone");
            CollectionAssert.AreEqual(new[] { 5 }, QueuedIds(service), "still listed for the player");
            Assert.AreEqual("canceled", service.GetPendingPurchases()[0].Status);
        });

        [UnityTest]
        public IEnumerator Refunded_StaysListed_ButIsNotRetried() => UniTask.ToCoroutine(async () =>
        {
            var service = ServiceWithQueue(StoreOrder(5));
            ServerRejects(400, 2047, "refunded");

            await RetryPassAsync(service);
            await RetryPassAsync(service);

            Assert.AreEqual(1, VerifyRequestCount);
            Assert.AreEqual("refunded", service.GetPendingPurchases()[0].Status);
        });

        // ── Not final yet: retried on the next pass ───────────────────────────

        [UnityTest]
        public IEnumerator StillPending_IsRetried_AndTheGameIsToldOnce() => UniTask.ToCoroutine(async () =>
        {
            var service = ServiceWithQueue(StoreOrder(5));
            var pending = new List<int>();
            var delivered = new List<int>();
            service.OnPurchasePending += order => pending.Add(order.Id);
            service.OnPurchaseDone += order => delivered.Add(order.Id);
            ServerRejects(400, 2043, "pending");

            await RetryPassAsync(service);
            await RetryPassAsync(service);

            Assert.AreEqual(2, VerifyRequestCount, "a pending order is verified again on the next pass");
            CollectionAssert.AreEqual(new[] { 5 }, pending, "automatic retries raise OnPurchasePending once per order");
            CollectionAssert.IsEmpty(delivered);

            // The payment clears: the same order is now delivered.
            ServerAnswers("completed");
            await RetryPassAsync(service);

            CollectionAssert.AreEqual(new[] { 5 }, delivered);
            CollectionAssert.IsEmpty(QueuedIds(service));
        });

        [UnityTest]
        public IEnumerator ServerError_KeepsTheOrder_ForTheNextPass() => UniTask.ToCoroutine(async () =>
        {
            var service = ServiceWithQueue(StoreOrder(5));
            _server.AddHandler(VerifyPath, _ => (500, "{}"));

            await RetryPassAsync(service);

            CollectionAssert.AreEqual(new[] { 5 }, QueuedIds(service), "nothing is known about the payment: keep it");
            Assert.AreEqual(1, RetryBatch(service).Count, "and retry it");
            Assert.AreEqual("play-token", service.GetPendingPurchases()[0].VerifyOrderRequest.ReceiptData,
                "the receipt survives a failed retry");
        });

        [UnityTest]
        public IEnumerator OneFailingOrder_DoesNotBlockTheOthers() => UniTask.ToCoroutine(async () =>
        {
            var service = ServiceWithQueue(StoreOrder(5), StoreOrder(6));
            var delivered = new List<int>();
            service.OnPurchaseDone += order => delivered.Add(order.Id);
            _server.AddHandler(VerifyPath, request =>
            {
                // leaveOpen: the mock server reads (and closes) the stream itself afterwards.
                var reader = new System.IO.StreamReader(request.InputStream, System.Text.Encoding.UTF8, false, 1024, leaveOpen: true);
                return System.Text.RegularExpressions.Regex.IsMatch(reader.ReadToEnd(), "\"id\":\\s*5\\b")
                    ? (400, "{\"success\":false,\"error_code\":2043,\"error_message\":\"pending\"}")
                    : (200, "{\"success\":true,\"data\":{\"order_status\":\"completed\"}}");
            });

            await RetryPassAsync(service);

            CollectionAssert.AreEqual(new[] { 6 }, delivered);
            CollectionAssert.AreEqual(new[] { 5 }, QueuedIds(service));
        });

        // ── Store order with no receipt ───────────────────────────────────────

        [UnityTest]
        public IEnumerator StoreOrderWithoutReceipt_IsNotSent_UntilItsReceiptArrives() => UniTask.ToCoroutine(async () =>
        {
            // Item already owned / Play purchase still pending: the order was created but the
            // store gave no purchase token. The backend can only answer 400 for it.
            var service = ServiceWithQueue(StoreOrder(5, receipt: ""));
            var delivered = new List<int>();
            service.OnPurchaseDone += order => delivered.Add(order.Id);
            ServerRejects(400, 2001, "Bad request / insufficient parameter: receipt_data field is required");

            await RetryPassAsync(service);
            await RetryPassAsync(service);

            Assert.AreEqual(0, VerifyRequestCount, "no request is sent for an order that cannot verify");
            CollectionAssert.AreEqual(new[] { 5 }, QueuedIds(service), "the order is kept, waiting for its receipt");

            // The store delivers the purchase: HandleUnpairedPurchase queues the order with its token.
            Enqueue(service, StoreOrder(5, receipt: "play-token"));
            ServerAnswers("completed");
            await RetryPassAsync(service);

            Assert.AreEqual(1, VerifyRequestCount);
            CollectionAssert.AreEqual(new[] { 5 }, delivered);
            CollectionAssert.IsEmpty(QueuedIds(service));
        });

        [UnityTest]
        public IEnumerator NoctuaStoreOrderWithoutReceipt_IsStillRetried() => UniTask.ToCoroutine(async () =>
        {
            // Noctua Store orders are verified by order id alone; they never carry a receipt.
            var service = ServiceWithQueue(StoreOrder(5, receipt: "", paymentType: PaymentType.noctuastore));
            var delivered = new List<int>();
            service.OnPurchaseDone += order => delivered.Add(order.Id);
            ServerAnswers("completed");

            await RetryPassAsync(service);

            Assert.AreEqual(1, VerifyRequestCount);
            CollectionAssert.AreEqual(new[] { 5 }, delivered);
        });
    }
}
