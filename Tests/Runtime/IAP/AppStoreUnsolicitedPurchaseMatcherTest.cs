using System.Collections.Generic;
using com.noctuagames.sdk;
using NUnit.Framework;

namespace Tests.Runtime.IAP
{
    /// <summary>
    /// Unit tests for <see cref="AppStoreUnsolicitedPurchaseMatcher"/> — how an App Store transaction
    /// nobody was waiting for (a StoreKit replay, or a purchase that finished after the flow gave up)
    /// is tied back to an order so the player still receives the item.
    /// </summary>
    [TestFixture]
    public class AppStoreUnsolicitedPurchaseMatcherTest
    {
        private static readonly IReadOnlyDictionary<string, InternalPurchaseItem> NoUnpaired =
            new Dictionary<string, InternalPurchaseItem>();

        [Test]
        public void TransactionWithoutToken_IsLeftUnfinished()
        {
            var decision = AppStoreUnsolicitedPurchaseMatcher.Decide(
                Transaction("gem", token: ""), null, null, NoUnpaired);

            Assert.AreEqual(UnsolicitedAppStorePurchaseAction.LeaveUnfinished, decision.Action);
        }

        [Test]
        public void TransactionOfCompletedOrder_IsOnlyFinished()
        {
            var history = new[] { Item(10, "gem", token: "tx-1", status: "completed") };

            var decision = AppStoreUnsolicitedPurchaseMatcher.Decide(
                Transaction("gem", "tx-1"), null, history, Unpaired(Item(11, "gem")));

            Assert.AreEqual(UnsolicitedAppStorePurchaseAction.FinishAlreadyCompleted, decision.Action,
                "A delivered order must not be verified (and delivered) again");
            Assert.IsNull(decision.Item);
        }

        [Test]
        public void TransactionOwnedByPendingOrder_IsVerifiedWithFreshReceipt()
        {
            var pending = new[] { Item(10, "gem", token: "tx-1", receipt: "old-receipt") };

            var decision = AppStoreUnsolicitedPurchaseMatcher.Decide(
                Transaction("gem", "tx-1", receipt: "new-receipt"), pending, null, NoUnpaired);

            Assert.AreEqual(UnsolicitedAppStorePurchaseAction.VerifyPendingPurchase, decision.Action);
            Assert.AreEqual(10, decision.Item.OrderId);
            Assert.AreEqual("new-receipt", decision.Item.VerifyOrderRequest.ReceiptData);
            Assert.AreEqual("old-receipt", pending[0].VerifyOrderRequest.ReceiptData, "Stored item must not be mutated");
        }

        [Test]
        public void TransactionForProductWithUnpairedOrder_IsPaired()
        {
            var decision = AppStoreUnsolicitedPurchaseMatcher.Decide(
                Transaction("gem", "tx-9", receipt: "receipt-9"), null, null, Unpaired(Item(42, "gem")));

            Assert.AreEqual(UnsolicitedAppStorePurchaseAction.PairUnpairedOrder, decision.Action);
            Assert.AreEqual(42, decision.Item.OrderId);
            Assert.AreEqual(42, decision.Item.VerifyOrderRequest.Id);
            Assert.AreEqual("receipt-9", decision.Item.VerifyOrderRequest.ReceiptData);
            Assert.AreEqual("tx-9", decision.Item.PurchaseToken);
            Assert.AreEqual(VerifyOrderTrigger.payment_flow.ToString(), decision.Item.VerifyOrderRequest.Trigger);
        }

        [Test]
        public void UnpairedOrderAlreadyPairedWithAnotherTransaction_IsNotReused()
        {
            var pending = new[] { Item(42, "gem", token: "tx-other") };

            var decision = AppStoreUnsolicitedPurchaseMatcher.Decide(
                Transaction("gem", "tx-9"), pending, null, Unpaired(Item(42, "gem")));

            Assert.AreEqual(UnsolicitedAppStorePurchaseAction.LeaveUnfinished, decision.Action,
                "One order must never be verified with two different transactions");
        }

        [Test]
        public void UnpairedOrderForDifferentProduct_IsNotUsed()
        {
            var decision = AppStoreUnsolicitedPurchaseMatcher.Decide(
                Transaction("gem", "tx-9"), null, null, Unpaired(Item(42, "coin")));

            Assert.AreEqual(UnsolicitedAppStorePurchaseAction.LeaveUnfinished, decision.Action);
        }

        [Test]
        public void PendingOrderWithoutToken_DoesNotBlockPairing()
        {
            // A failed/timed-out purchase leaves its order in the retry queue without a token.
            var pending = new[] { Item(42, "gem") };

            var decision = AppStoreUnsolicitedPurchaseMatcher.Decide(
                Transaction("gem", "tx-9"), pending, null, Unpaired(Item(42, "gem")));

            Assert.AreEqual(UnsolicitedAppStorePurchaseAction.PairUnpairedOrder, decision.Action);
        }

        private static StoreKitTransaction Transaction(string productId, string token, string receipt = "receipt") =>
            new() { ProductId = productId, Success = true, PurchaseToken = token, Receipt = receipt };

        private static InternalPurchaseItem Item(int orderId, string productId, string token = null, string receipt = null, string status = null) =>
            new()
            {
                OrderId = orderId,
                OrderRequest = new OrderRequest { Id = orderId, ProductId = productId },
                VerifyOrderRequest = new VerifyOrderRequest { Id = orderId, ReceiptData = receipt },
                AccessToken = "token",
                Status = status,
                PurchaseToken = token
            };

        private static IReadOnlyDictionary<string, InternalPurchaseItem> Unpaired(InternalPurchaseItem item) =>
            new Dictionary<string, InternalPurchaseItem> { { item.OrderRequest.ProductId, item } };
    }
}
