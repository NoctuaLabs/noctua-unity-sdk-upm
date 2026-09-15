using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using com.noctuagames.sdk;
using Newtonsoft.Json;
using NUnit.Framework;

namespace Tests.Runtime.IAP
{
    /// <summary>
    /// Unit tests for <see cref="StoreKitRequestRouter"/> — the matcher between iOS StoreKit bridge
    /// events and the calls waiting for them. Each test reproduces a race the old single-slot
    /// native bridge got wrong (dropped or cross-wired callbacks, hijacked purchases).
    /// </summary>
    [TestFixture]
    public class StoreKitRequestRouterTest
    {
        private const long Now = 1_800_000_000_000;

        private FakeNative _native;
        private ManualScheduler _scheduler;
        private long _nowMs;
        private StoreKitRequestRouter _router;

        [SetUp]
        public void SetUp()
        {
            _native = new FakeNative();
            _scheduler = new ManualScheduler();
            _nowMs = Now;
            _router = new StoreKitRequestRouter(_native, () => _nowMs, _scheduler.Schedule);
        }

        // MARK: - Purchase status

        [Test]
        public void RapidStatusChecks_SameProduct_CoalesceIntoAtMostTwoNativeQueries_AndAllComplete()
        {
            var results = new List<bool>();

            for (var i = 0; i < 5; i++)
            {
                _router.RequestPurchaseStatus("gem", s => results.Add(s.IsPurchased));
            }

            Assert.AreEqual(new[] { "gem" }, _native.StatusQueries.ToArray(), "Checks while a query is in flight must not each hit native");

            _router.OnPurchaseStatus(Status("gem", purchased: true));
            Assert.AreEqual(1, results.Count, "Only the check that issued the query gets its answer");
            Assert.AreEqual(new[] { "gem", "gem" }, _native.StatusQueries.ToArray(), "The other four share one follow-up query");

            _router.OnPurchaseStatus(Status("gem", purchased: true));
            Assert.AreEqual(Enumerable.Repeat(true, 5).ToList(), results);
            Assert.AreEqual(2, _native.StatusQueries.Count);
        }

        [Test]
        public void StatusResult_ForOtherProduct_DoesNotCompleteWaiter()
        {
            ProductPurchaseStatus received = null;
            _router.RequestPurchaseStatus("gem", s => received = s);

            _router.OnPurchaseStatus(Status("coin", purchased: true));

            Assert.IsNull(received);
        }

        [Test]
        public void StatusCheck_WhileQueryInFlight_GetsFollowUpQuery_NotTheEarlierAnswer()
        {
            var first = new List<bool>();
            var second = new List<bool>();

            _router.RequestPurchaseStatus("sub", s => first.Add(s.IsPurchased));
            // Native query for the first check is already out; the product gets bought meanwhile.
            _router.RequestPurchaseStatus("sub", s => second.Add(s.IsPurchased));

            _router.OnPurchaseStatus(Status("sub", purchased: false));

            Assert.AreEqual(new List<bool> { false }, first);
            Assert.IsEmpty(second, "The second check asked after the query went out — it must not get that stale answer");
            Assert.AreEqual(2, _native.StatusQueries.Count, "A follow-up native query must be issued");

            _router.OnPurchaseStatus(Status("sub", purchased: true));

            Assert.AreEqual(new List<bool> { true }, second);
            Assert.AreEqual(new List<bool> { false }, first, "The first check completes exactly once");
        }

        [Test]
        public void ParallelChecks_ForDifferentProducts_ResultsOutOfOrder_EachGetsItsOwn()
        {
            var results = new Dictionary<string, bool>();
            var productIds = new[] { "a", "b", "c", "d" };

            foreach (var id in productIds)
            {
                _router.RequestPurchaseStatus(id, s => results[id] = s.IsPurchased && s.ProductId == id);
            }
            foreach (var id in productIds.Reverse())
            {
                _router.OnPurchaseStatus(Status(id, purchased: true));
            }

            Assert.AreEqual(4, results.Count);
            Assert.IsTrue(results.Values.All(v => v), "Every check must receive its own product's status");
        }

        [Test]
        public void StatusCheck_TimesOut_AnswersNotPurchased_AndLateResultIsIgnored()
        {
            var results = new List<ProductPurchaseStatus>();
            _router.RequestPurchaseStatus("gem", s => results.Add(s));
            Assert.AreEqual(StoreKitRequestRouter.StatusTimeoutMs, _scheduler.Delays.Single());

            _scheduler.RunAll();
            _router.OnPurchaseStatus(Status("gem", purchased: true));

            Assert.AreEqual(1, results.Count);
            Assert.IsFalse(results[0].IsPurchased);
            Assert.AreEqual("gem", results[0].ProductId);
        }

        [Test]
        public void StatusCheck_AfterTimeout_IssuesFreshQuery()
        {
            _router.RequestPurchaseStatus("gem", _ => { });
            _scheduler.RunAll();

            var received = false;
            _router.RequestPurchaseStatus("gem", s => received = s.IsPurchased);
            _router.OnPurchaseStatus(Status("gem", purchased: true));

            Assert.AreEqual(2, _native.StatusQueries.Count);
            Assert.IsTrue(received);
        }

        [Test]
        public void ConcurrentStatusChecks_FromManyThreads_AllCompleteExactlyOnce()
        {
            var native = new AnsweringNative();
            var router = new StoreKitRequestRouter(native, () => Now, (_, _) => { });
            native.Router = router;

            const int callers = 200;
            var completions = new ConcurrentDictionary<int, int>();
            var wrongProduct = 0;

            Parallel.For(0, callers, i =>
            {
                var productId = "product-" + (i % 7);
                router.RequestPurchaseStatus(productId, s =>
                {
                    if (s.ProductId != productId) Interlocked.Increment(ref wrongProduct);
                    completions.AddOrUpdate(i, 1, (_, count) => count + 1);
                });
            });

            Assert.IsTrue(SpinWait.SpinUntil(() => completions.Count == callers, TimeSpan.FromSeconds(5)),
                $"Only {completions.Count}/{callers} status checks completed");
            Assert.IsTrue(completions.Values.All(count => count == 1), "A check completed more than once");
            Assert.AreEqual(0, wrongProduct, "A check received another product's status");
        }

        [Test]
        public void StatusCallbackThatThrows_DoesNotBlockOtherWaiters()
        {
            var otherProductCalled = false;
            _router.RequestPurchaseStatus("gem", _ => throw new InvalidOperationException("boom"));
            _router.RequestPurchaseStatus("coin", _ => otherProductCalled = true);

            Assert.DoesNotThrow(() => _router.OnPurchaseStatus(Status("gem", purchased: true)));

            // The router must still be usable after a throwing callback.
            _router.OnPurchaseStatus(Status("coin", purchased: true));
            Assert.IsTrue(otherProductCalled);

            var retried = false;
            _router.RequestPurchaseStatus("gem", _ => retried = true);
            _router.OnPurchaseStatus(Status("gem", purchased: true));
            Assert.IsTrue(retried);
        }

        // MARK: - Active currency

        [Test]
        public void CurrencyQueries_AreSerialized()
        {
            string first = null, second = null;

            _router.RequestActiveCurrency("gem", (_, c) => first = c);
            _router.RequestActiveCurrency("coin", (_, c) => second = c);

            Assert.AreEqual(new[] { "gem" }, _native.CurrencyQueries.ToArray());

            _router.OnProductDetails(new[] { Currency("gem", "IDR") });

            Assert.AreEqual("IDR", first);
            Assert.AreEqual(new[] { "gem", "coin" }, _native.CurrencyQueries.ToArray());

            _router.OnProductDetails(new[] { Currency("coin", "USD") });
            Assert.AreEqual("USD", second);
        }

        [Test]
        public void CurrencyQuery_SameProductWhileInFlight_JoinsQuery()
        {
            var results = new List<string>();
            _router.RequestActiveCurrency("gem", (_, c) => results.Add(c));
            _router.RequestActiveCurrency("gem", (_, c) => results.Add(c));

            _router.OnProductDetails(new[] { Currency("gem", "IDR") });

            Assert.AreEqual(1, _native.CurrencyQueries.Count);
            Assert.AreEqual(new List<string> { "IDR", "IDR" }, results);
        }

        [Test]
        public void EmptyProductDetails_FailsInFlightCurrencyQuery()
        {
            bool? success = null;
            _router.RequestActiveCurrency("unknown", (ok, _) => success = ok);

            _router.OnProductDetails(Array.Empty<StoreKitProductCurrency>());

            Assert.AreEqual(false, success);
        }

        [Test]
        public void ProductDetails_ForAnotherProduct_AreIgnored()
        {
            bool? success = null;
            _router.RequestActiveCurrency("gem", (ok, _) => success = ok);

            _router.OnProductDetails(new[] { Currency("coin", "USD") });

            Assert.IsNull(success);
        }

        [Test]
        public void ProductDetailsQueryError_FailsCurrency_ButNotPendingPurchase()
        {
            bool? currencySuccess = null;
            StoreKitPurchaseOutcome purchase = null;
            _router.RequestActiveCurrency("gem", (ok, _) => currencySuccess = ok);
            _router.BeginPurchase("coin", o => purchase = o);

            _router.OnStoreKitError(6, "Failed to query product details: The Internet connection appears to be offline.");

            Assert.AreEqual(false, currencySuccess);
            Assert.IsNull(purchase, "An unrelated query error must not fail the purchase");
        }

        [Test]
        public void CurrencyTimeout_FailsWaiter_AndStartsNextQuery()
        {
            bool? first = null;
            string second = null;
            _router.RequestActiveCurrency("gem", (ok, _) => first = ok);
            _router.RequestActiveCurrency("coin", (_, c) => second = c);

            _scheduler.RunFirst();

            Assert.AreEqual(false, first);
            Assert.AreEqual(new[] { "gem", "coin" }, _native.CurrencyQueries.ToArray());

            _router.OnProductDetails(new[] { Currency("coin", "USD") });
            Assert.AreEqual("USD", second);
        }

        // MARK: - Purchases

        [Test]
        public void ServerVerification_CompletesMatchingPurchase()
        {
            StoreKitPurchaseOutcome outcome = null;
            _router.BeginPurchase("gem", o => outcome = o);

            _router.OnServerVerificationRequired(Purchased("gem", "tx-1", Now + 5_000));

            Assert.AreEqual(new[] { "gem" }, _native.Purchases.ToArray());
            Assert.IsTrue(outcome.Success);
            Assert.AreEqual("tx-1", outcome.Transaction.PurchaseToken);
        }

        [Test]
        public void ReplayedOldTransaction_DuringPurchase_IsUnsolicited_AndPurchaseWaitsForItsOwn()
        {
            StoreKitPurchaseOutcome outcome = null;
            var unsolicited = new List<StoreKitTransaction>();
            _router.SetUnsolicitedPurchaseHandler(unsolicited.Add);
            _router.BeginPurchase("gem", o => outcome = o);

            // An unfinished transaction for the same product from three days ago is re-delivered.
            _router.OnServerVerificationRequired(Purchased("gem", "tx-old", Now - 3 * 24 * 60 * 60 * 1000L));

            Assert.IsNull(outcome, "The old transaction must not complete the new purchase");
            Assert.AreEqual("tx-old", unsolicited.Single().PurchaseToken);

            _router.OnServerVerificationRequired(Purchased("gem", "tx-new", Now + 8_000));

            Assert.AreEqual("tx-new", outcome.Transaction.PurchaseToken);
        }

        [Test]
        public void PurchasedTransaction_WithNoPendingPurchase_IsBufferedUntilHandlerRegistered()
        {
            _router.OnServerVerificationRequired(Purchased("gem", "tx-1", Now));

            var unsolicited = new List<StoreKitTransaction>();
            _router.SetUnsolicitedPurchaseHandler(unsolicited.Add);

            Assert.AreEqual("tx-1", unsolicited.Single().PurchaseToken);
        }

        [Test]
        public void RepeatedTransactionEvent_IsNeverDropped_SoItCanBeFinished()
        {
            var unsolicited = new List<StoreKitTransaction>();
            _router.SetUnsolicitedPurchaseHandler(unsolicited.Add);

            _router.OnServerVerificationRequired(Purchased("gem", "tx-1", Now));
            _router.OnServerVerificationRequired(Purchased("gem", "tx-1", Now));

            Assert.AreEqual(2, unsolicited.Count, "A repeat must reach the handler so the transaction gets finished");
        }

        [Test]
        public void BuySameProductAgain_AfterStoreKitRepeatsFinishedTransaction_DoesNotHang()
        {
            // Reproduces the sandbox log: purchase succeeds, StoreKit re-sends the finished transaction,
            // then answers the next purchase of the same product with that same transaction.
            var unsolicited = new List<StoreKitTransaction>();
            _router.SetUnsolicitedPurchaseHandler(unsolicited.Add);

            StoreKitPurchaseOutcome first = null;
            _router.BeginPurchase("pack2", o => first = o);
            _router.OnServerVerificationRequired(Purchased("pack2", "2000001236345651", Now));
            Assert.IsTrue(first.Success);

            // Re-delivered after it was finished, with no purchase waiting.
            _router.OnServerVerificationRequired(Purchased("pack2", "2000001236345651", Now));
            Assert.AreEqual(1, unsolicited.Count);

            // The player buys the same product again; StoreKit answers with the old transaction.
            StoreKitPurchaseOutcome second = null;
            _router.BeginPurchase("pack2", o => second = o);
            _router.OnServerVerificationRequired(Purchased("pack2", "2000001236345651", Now));

            Assert.IsNotNull(second, "The second purchase must complete instead of holding the purchase flow forever");
            Assert.IsFalse(second.Success);
            Assert.AreEqual(StoreKitRequestRouter.RepeatedTransactionMessage, second.Message);
            Assert.AreEqual(2, unsolicited.Count, "The old transaction is handed over again so it gets finished");

            // A third attempt gets its own new transaction normally.
            StoreKitPurchaseOutcome third = null;
            _router.BeginPurchase("pack2", o => third = o);
            _router.OnServerVerificationRequired(Purchased("pack2", "2000001236399999", Now));

            Assert.IsTrue(third.Success);
            Assert.AreEqual("2000001236399999", third.Transaction.PurchaseToken);
        }

        [Test]
        public void NewTransactionArrivingAfterRepeatFailedThePurchase_GoesToUnsolicited()
        {
            var unsolicited = new List<StoreKitTransaction>();
            _router.SetUnsolicitedPurchaseHandler(unsolicited.Add);
            _router.BeginPurchase("pack2", _ => { });
            _router.OnServerVerificationRequired(Purchased("pack2", "tx-1", Now));

            _router.BeginPurchase("pack2", _ => { });
            _router.OnServerVerificationRequired(Purchased("pack2", "tx-1", Now));

            // StoreKit later completes the real payment for the failed attempt.
            _router.OnServerVerificationRequired(Purchased("pack2", "tx-2", Now));

            Assert.AreEqual(new[] { "tx-1", "tx-2" }, unsolicited.Select(t => t.PurchaseToken).ToArray(),
                "The late real transaction must still be delivered through the unsolicited path");
        }

        [Test]
        public void UnscopedStoreKitError_DoesNotFailPurchase()
        {
            StoreKitPurchaseOutcome outcome = null;
            _router.BeginPurchase("gem", o => outcome = o);

            _router.OnStoreKitError(6, "Failed to restore purchases: offline");

            Assert.IsNull(outcome);
        }

        [Test]
        public void ProductNotFoundError_FailsOnlyThatProductsPurchase()
        {
            StoreKitPurchaseOutcome gem = null, coin = null;
            _router.BeginPurchase("gem", o => gem = o);
            _router.BeginPurchase("coin", o => coin = o);

            _router.OnStoreKitError(4, "Product not found: coin");

            Assert.IsNull(gem);
            Assert.IsFalse(coin.Success);
            Assert.AreEqual(4, coin.ErrorCode);
        }

        [Test]
        public void ScopedFailureReportedTwice_CompletesPurchaseOnce()
        {
            var outcomes = new List<StoreKitPurchaseOutcome>();
            _router.BeginPurchase("coin", outcomes.Add);

            // Newer native SDKs send a scoped purchase result and then the legacy error.
            _router.OnPurchaseCompleted(Failed("coin", 4, "Product not found: coin"));
            _router.OnStoreKitError(4, "Product not found: coin");

            Assert.AreEqual(1, outcomes.Count);
        }

        [Test]
        public void FailedTransaction_FailsPendingPurchase_WithErrorCode()
        {
            StoreKitPurchaseOutcome outcome = null;
            _router.BeginPurchase("gem", o => outcome = o);

            _router.OnPurchaseCompleted(Failed("gem", 1, "The operation couldn’t be completed. (SKErrorDomain error 2.)"));

            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(StoreKitRequestRouter.UserCanceledErrorCode, outcome.ErrorCode);
        }

        [Test]
        public void FailedTransaction_ForOtherProduct_DoesNotFailPurchase()
        {
            StoreKitPurchaseOutcome outcome = null;
            _router.BeginPurchase("gem", o => outcome = o);

            _router.OnPurchaseCompleted(Failed("coin", 6, "An unknown error occurred"));

            Assert.IsNull(outcome);
        }

        [Test]
        public void SuccessfulPurchaseCompleted_IsIgnored_WhenServerVerificationDelivers()
        {
            var outcomes = new List<StoreKitPurchaseOutcome>();
            _router.BeginPurchase("gem", outcomes.Add);

            var transaction = Purchased("gem", "tx-1", Now);
            _router.OnServerVerificationRequired(transaction);
            _router.OnPurchaseCompleted(transaction);

            Assert.AreEqual(1, outcomes.Count);
        }

        [Test]
        public void RestoredTransaction_DoesNotCompletePurchase()
        {
            StoreKitPurchaseOutcome outcome = null;
            _router.BeginPurchase("gem", o => outcome = o);

            _router.OnPurchaseUpdated(Purchased("gem", "tx-restored", Now));

            Assert.IsNull(outcome);
        }

        [Test]
        public void SecondPurchaseForSameProduct_SupersedesFirst()
        {
            StoreKitPurchaseOutcome first = null, second = null;
            _router.BeginPurchase("gem", o => first = o);
            _router.BeginPurchase("gem", o => second = o);

            Assert.IsFalse(first.Success);

            _router.OnServerVerificationRequired(Purchased("gem", "tx-1", Now));
            Assert.IsTrue(second.Success);
        }

        [Test]
        public void FailAll_CompletesEveryWaiter()
        {
            StoreKitPurchaseOutcome purchase = null;
            ProductPurchaseStatus status = null;
            bool? currency = null;
            _router.BeginPurchase("gem", o => purchase = o);
            _router.RequestPurchaseStatus("gem", s => status = s);
            _router.RequestActiveCurrency("gem", (ok, _) => currency = ok);

            _router.FailAll("StoreKit disposed");

            Assert.IsFalse(purchase.Success);
            Assert.IsFalse(status.IsPurchased);
            Assert.AreEqual(false, currency);
        }

        // MARK: - StoreKit unavailable (events NoctuaInterop.m sends when StoreKit cannot initialize)

        private const string StoreKitUnavailableMessage =
            "StoreKit is not available (IAP disabled in config, or the native SDK is not initialized)";

        [Test]
        public void StoreKitUnavailable_PurchaseFailsImmediately()
        {
            StoreKitPurchaseOutcome outcome = null;
            _router.BeginPurchase("gem", o => outcome = o);

            _router.OnPurchaseCompleted(Failed("gem", 3, StoreKitUnavailableMessage));

            Assert.IsFalse(outcome.Success);
            Assert.AreEqual(3, outcome.ErrorCode);
        }

        [Test]
        public void StoreKitUnavailable_StatusAndCurrencyAnswerImmediately()
        {
            ProductPurchaseStatus status = null;
            bool? currency = null;
            _router.RequestPurchaseStatus("gem", s => status = s);
            _router.RequestActiveCurrency("gem", (ok, _) => currency = ok);

            _router.OnPurchaseStatus(new ProductPurchaseStatus { ProductId = "gem", IsPurchased = false });
            _router.OnStoreKitError(3, "Failed to query product details: " + StoreKitUnavailableMessage);

            Assert.IsFalse(status.IsPurchased);
            Assert.AreEqual(false, currency);
        }

        // MARK: - Pure helpers

        [TestCase("Product not found: com.game.gem", ExpectedResult = "com.game.gem")]
        [TestCase("Failed to fetch product for purchase: com.game.gem: offline", ExpectedResult = "com.game.gem")]
        [TestCase("Failed to query product details: offline", ExpectedResult = null)]
        [TestCase("Failed to restore purchases: offline", ExpectedResult = null)]
        [TestCase("Product not found: ", ExpectedResult = null)]
        [TestCase(null, ExpectedResult = null)]
        public string TryParsePurchaseScopedProductId_ExtractsOnlyPurchaseScopedIds(string message)
        {
            return StoreKitRequestRouter.TryParsePurchaseScopedProductId(message);
        }

        [Test]
        public void IsStaleTransaction_ToleratesClockSkew()
        {
            const long hour = 60 * 60 * 1000L;
            Assert.IsFalse(StoreKitRequestRouter.IsStaleTransaction(Purchased("gem", "t", Now - 60_000), Now));
            Assert.IsFalse(StoreKitRequestRouter.IsStaleTransaction(Purchased("gem", "t", Now - 3 * hour), Now), "A device clock hours ahead must not look stale");
            Assert.IsTrue(StoreKitRequestRouter.IsStaleTransaction(Purchased("gem", "t", Now - 48 * hour), Now));
            Assert.IsFalse(StoreKitRequestRouter.IsStaleTransaction(Purchased("gem", "t", 0), Now), "Unknown date is not stale");
        }

        // MARK: - Parity with the previous single-slot bridge

        [Test]
        public void Parity_GenuinePurchase_WithDeviceClockHoursAhead_CompletesPurchase()
        {
            StoreKitPurchaseOutcome outcome = null;
            _router.BeginPurchase("gem", o => outcome = o);

            // Device clock is 5 hours ahead of the App Store's transaction date.
            _router.OnServerVerificationRequired(Purchased("gem", "tx-1", Now - 5 * 60 * 60 * 1000L));

            Assert.IsTrue(outcome?.Success);
        }

        [Test]
        public void Parity_UnsuccessfulServerVerificationEvent_FailsPurchase()
        {
            StoreKitPurchaseOutcome outcome = null;
            _router.BeginPurchase("gem", o => outcome = o);

            _router.OnServerVerificationRequired(new StoreKitTransaction { ProductId = "gem", Success = false, Message = null });

            Assert.IsFalse(outcome.Success);
            Assert.AreEqual("Server verification required", outcome.Message);
        }

        [Test]
        public void Parity_FailureMessage_IsPassedThroughUnchanged()
        {
            StoreKitPurchaseOutcome outcome = null;
            _router.BeginPurchase("gem", o => outcome = o);

            _router.OnPurchaseCompleted(Failed("gem", 6, "An unknown error occurred"));

            Assert.AreEqual("An unknown error occurred", StoreKitRequestRouter.FormatPurchaseCallbackMessage(outcome));
            Assert.AreEqual("", StoreKitRequestRouter.FormatPurchaseCallbackMessage(new StoreKitPurchaseOutcome { Success = false, Message = "" }));
        }

        [Test]
        public void Parity_DeferredPurchase_FailsWithPendingApprovalMessage()
        {
            StoreKitPurchaseOutcome outcome = null;
            _router.BeginPurchase("gem", o => outcome = o);

            _router.OnPurchaseCompleted(new StoreKitTransaction
            {
                ProductId = "gem", Success = false, PurchaseState = 2, Message = "Purchase is pending approval (Ask to Buy)"
            });

            Assert.IsFalse(outcome.Success);
            Assert.AreEqual("Purchase is pending approval (Ask to Buy)", StoreKitRequestRouter.FormatPurchaseCallbackMessage(outcome));
        }

        [Test]
        public void Parity_ProductFoundWithEmptyCurrency_IsStillSuccess()
        {
            bool? success = null;
            string currency = null;
            _router.RequestActiveCurrency("gem", (ok, c) => { success = ok; currency = c; });

            _router.OnProductDetails(new[] { Currency("gem", "") });

            Assert.AreEqual(true, success);
            Assert.AreEqual("", currency);
        }

        [Test]
        public void Parity_EmptyProductId_AnswersImmediately_WithoutNativeCall()
        {
            StoreKitPurchaseOutcome purchase = null;
            ProductPurchaseStatus status = null;
            bool? currency = null;

            _router.BeginPurchase("", o => purchase = o);
            _router.RequestPurchaseStatus(null, s => status = s);
            _router.RequestActiveCurrency("", (ok, _) => currency = ok);

            Assert.AreEqual("Product ID is null or empty", purchase.Message);
            Assert.IsFalse(status.IsPurchased);
            Assert.AreEqual(false, currency);
            Assert.IsEmpty(_native.Purchases);
            Assert.IsEmpty(_native.StatusQueries);
            Assert.IsEmpty(_native.CurrencyQueries);
        }

        [Test]
        public void FormatPurchaseCallbackMessage_Success_IsReceiptJson()
        {
            var message = StoreKitRequestRouter.FormatPurchaseCallbackMessage(new StoreKitPurchaseOutcome
            {
                Success = true,
                Transaction = new StoreKitTransaction { Receipt = "MIIU", PurchaseToken = "tx-1", ConsumableType = 0 }
            });

            var parsed = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
            Assert.AreEqual("MIIU", parsed["receipt"]);
            Assert.AreEqual("tx-1", parsed["purchaseToken"]);
        }

        [Test]
        public void FormatPurchaseCallbackMessage_UserCancel_MentionsCancel()
        {
            var message = StoreKitRequestRouter.FormatPurchaseCallbackMessage(new StoreKitPurchaseOutcome
            {
                Success = false,
                ErrorCode = StoreKitRequestRouter.UserCanceledErrorCode,
                Message = "The operation couldn’t be completed. (SKErrorDomain error 2.)"
            });

            StringAssert.Contains("cancel", message);
        }

        // MARK: - Helpers

        private static ProductPurchaseStatus Status(string productId, bool purchased) =>
            new() { ProductId = productId, IsPurchased = purchased };

        private static StoreKitProductCurrency Currency(string productId, string currency) =>
            new() { ProductId = productId, Currency = currency };

        private static StoreKitTransaction Purchased(string productId, string token, long timeMs) =>
            new() { ProductId = productId, Success = true, PurchaseState = 1, PurchaseToken = token, PurchaseTimeMs = timeMs, Receipt = "MIIU" };

        private static StoreKitTransaction Failed(string productId, int errorCode, string message) =>
            new() { ProductId = productId, Success = false, ErrorCode = errorCode, Message = message };

        private sealed class FakeNative : IStoreKitNativeCommands
        {
            public readonly List<string> Purchases = new();
            public readonly List<string> StatusQueries = new();
            public readonly List<string> CurrencyQueries = new();

            public void Purchase(string productId) => Purchases.Add(productId);
            public void QueryPurchaseStatus(string productId) => StatusQueries.Add(productId);
            public void QueryActiveCurrency(string productId) => CurrencyQueries.Add(productId);
        }

        /// <summary>Answers every status query asynchronously on the thread pool, like the native bridge.</summary>
        private sealed class AnsweringNative : IStoreKitNativeCommands
        {
            public StoreKitRequestRouter Router;

            public void Purchase(string productId) { }
            public void QueryActiveCurrency(string productId) { }

            public void QueryPurchaseStatus(string productId)
            {
                Task.Run(() => Router.OnPurchaseStatus(new ProductPurchaseStatus { ProductId = productId, IsPurchased = true }));
            }
        }

        private sealed class ManualScheduler
        {
            private readonly List<(int Delay, Action Action)> _scheduled = new();

            public IEnumerable<int> Delays => _scheduled.Select(s => s.Delay);

            public void Schedule(int delayMs, Action action) => _scheduled.Add((delayMs, action));

            public void RunFirst()
            {
                var first = _scheduled[0];
                _scheduled.RemoveAt(0);
                first.Action();
            }

            public void RunAll()
            {
                var pending = _scheduled.ToList();
                _scheduled.Clear();
                pending.ForEach(s => s.Action());
            }
        }
    }
}
