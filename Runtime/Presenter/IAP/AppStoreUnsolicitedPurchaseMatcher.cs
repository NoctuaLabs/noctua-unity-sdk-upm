using System.Collections.Generic;
using System.Linq;

namespace com.noctuagames.sdk
{
    /// <summary>What to do with an App Store transaction no in-flight purchase claimed.</summary>
    public enum UnsolicitedAppStorePurchaseAction
    {
        /// <summary>
        /// No order can be tied to it. Leave the transaction unfinished: StoreKit re-delivers it on the
        /// next launch, so it can still be paired later instead of being consumed without delivery.
        /// </summary>
        LeaveUnfinished,

        /// <summary>Its order was already verified and delivered; only the StoreKit transaction needs finishing.</summary>
        FinishAlreadyCompleted,

        /// <summary>A pending (retry-queue) order already owns this transaction; verify it now.</summary>
        VerifyPendingPurchase,

        /// <summary>An order was created for this product but never received a transaction; pair and verify.</summary>
        PairUnpairedOrder
    }

    /// <summary>Decision for an unsolicited App Store transaction.</summary>
    public sealed class UnsolicitedAppStorePurchaseDecision
    {
        /// <summary>The action to take.</summary>
        public UnsolicitedAppStorePurchaseAction Action { get; }

        /// <summary>
        /// For <see cref="UnsolicitedAppStorePurchaseAction.VerifyPendingPurchase"/> and
        /// <see cref="UnsolicitedAppStorePurchaseAction.PairUnpairedOrder"/>: a new purchase item carrying
        /// the transaction's receipt and token, ready to verify. The stored item is not modified.
        /// </summary>
        public InternalPurchaseItem Item { get; }

        /// <summary>Human-readable reason, for logs.</summary>
        public string Reason { get; }

        public UnsolicitedAppStorePurchaseDecision(UnsolicitedAppStorePurchaseAction action, InternalPurchaseItem item, string reason)
        {
            Action = action;
            Item = item;
            Reason = reason;
        }
    }

    /// <summary>
    /// Decides how to deliver an App Store transaction that arrived without a purchase waiting for it:
    /// a StoreKit replay of an unfinished transaction, or a purchase that completed after the purchase
    /// flow stopped waiting. Pure, so the pairing rules are unit-tested without StoreKit.
    ///
    /// Transactions are matched by StoreKit transaction id (the purchase token), which is unique per
    /// transaction. The iOS receipt is the whole app receipt and changes with every transaction, so it
    /// cannot identify one.
    /// </summary>
    public static class AppStoreUnsolicitedPurchaseMatcher
    {
        private const string CompletedStatus = "completed";

        public static UnsolicitedAppStorePurchaseDecision Decide(
            StoreKitTransaction transaction,
            IEnumerable<InternalPurchaseItem> pendingPurchases,
            IEnumerable<InternalPurchaseItem> purchaseHistory,
            IReadOnlyDictionary<string, InternalPurchaseItem> unpairedOrders)
        {
            var token = transaction?.PurchaseToken;
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(transaction.ProductId))
            {
                return Leave("transaction has no token or product id");
            }

            var pending = (pendingPurchases ?? Enumerable.Empty<InternalPurchaseItem>()).Where(p => p != null).ToList();
            var history = (purchaseHistory ?? Enumerable.Empty<InternalPurchaseItem>()).Where(p => p != null).ToList();

            if (history.Any(h => h.PurchaseToken == token && h.Status == CompletedStatus))
            {
                return new UnsolicitedAppStorePurchaseDecision(
                    UnsolicitedAppStorePurchaseAction.FinishAlreadyCompleted, null, "order already completed");
            }

            var owner = pending.FirstOrDefault(p => p.PurchaseToken == token);
            if (owner != null)
            {
                return new UnsolicitedAppStorePurchaseDecision(
                    UnsolicitedAppStorePurchaseAction.VerifyPendingPurchase,
                    WithTransaction(owner, transaction),
                    $"pending order {owner.OrderId} owns this transaction");
            }

            if (unpairedOrders == null ||
                !unpairedOrders.TryGetValue(transaction.ProductId, out var unpaired) ||
                unpaired?.OrderRequest == null)
            {
                return Leave("no pending or unpaired order for this product");
            }

            var pairedWithOther = pending.Any(p =>
                p.OrderId == unpaired.OrderId &&
                !string.IsNullOrEmpty(p.PurchaseToken) &&
                p.PurchaseToken != token);
            if (pairedWithOther)
            {
                return Leave($"unpaired order {unpaired.OrderId} is already paired with another transaction");
            }

            return new UnsolicitedAppStorePurchaseDecision(
                UnsolicitedAppStorePurchaseAction.PairUnpairedOrder,
                WithTransaction(unpaired, transaction),
                $"paired with unpaired order {unpaired.OrderId}");
        }

        private static UnsolicitedAppStorePurchaseDecision Leave(string reason) =>
            new(UnsolicitedAppStorePurchaseAction.LeaveUnfinished, null, reason);

        private static InternalPurchaseItem WithTransaction(InternalPurchaseItem item, StoreKitTransaction transaction)
        {
            var verify = item.VerifyOrderRequest;
            return new InternalPurchaseItem
            {
                OrderId = item.OrderId,
                OrderRequest = item.OrderRequest,
                VerifyOrderRequest = new VerifyOrderRequest
                {
                    Id = verify?.Id > 0 ? verify.Id : item.OrderId,
                    ReceiptId = verify?.ReceiptId,
                    ReceiptData = string.IsNullOrEmpty(transaction.Receipt) ? verify?.ReceiptData : transaction.Receipt,
                    Trigger = VerifyOrderTrigger.payment_flow.ToString()
                },
                AccessToken = item.AccessToken,
                Status = item.Status,
                PlayerId = item.PlayerId,
                PurchaseToken = transaction.PurchaseToken
            };
        }
    }
}
