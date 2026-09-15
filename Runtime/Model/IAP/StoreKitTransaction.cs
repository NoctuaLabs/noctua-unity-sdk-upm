using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// A StoreKit transaction event forwarded by the iOS native bridge (purchase completed,
    /// purchase updated, or server verification required). Field names match the JSON the
    /// bridge (<c>NoctuaInterop.m</c>) serializes.
    /// </summary>
    [Preserve]
    public class StoreKitTransaction
    {
        /// <summary>Store product identifier. Always set — used to match the event to its caller.</summary>
        public string ProductId;

        /// <summary>Whether the transaction succeeded.</summary>
        public bool Success;

        /// <summary>Native <c>StoreKitErrorCode</c> (0 = ok, 1 = user canceled, 4 = item unavailable, 6 = error).</summary>
        public int ErrorCode;

        /// <summary>0 = unspecified, 1 = purchased, 2 = pending (Ask to Buy).</summary>
        public int PurchaseState;

        /// <summary>StoreKit transaction identifier. Empty for failed transactions.</summary>
        public string PurchaseToken;

        /// <summary>Transaction date in milliseconds since epoch, or 0 when unknown.</summary>
        public long PurchaseTimeMs;

        /// <summary>Base64 app receipt at the time of the transaction.</summary>
        public string Receipt;

        /// <summary>Native <c>ConsumableType</c> (0 = consumable, 1 = non-consumable, 2 = subscription).</summary>
        public int ConsumableType;

        /// <summary>Human-readable failure reason, empty on success.</summary>
        public string Message;
    }

    /// <summary>
    /// Result of an App Store purchase request, as delivered by <see cref="StoreKitRequestRouter"/>.
    /// </summary>
    [Preserve]
    public class StoreKitPurchaseOutcome
    {
        /// <summary>Whether the purchase succeeded.</summary>
        public bool Success;

        /// <summary>Native <c>StoreKitErrorCode</c>; 0 on success.</summary>
        public int ErrorCode;

        /// <summary>Failure reason; empty on success.</summary>
        public string Message;

        /// <summary>The matched transaction on success; null on failure.</summary>
        public StoreKitTransaction Transaction;
    }

    /// <summary>
    /// Product currency reported by a StoreKit product details response.
    /// </summary>
    [Preserve]
    public class StoreKitProductCurrency
    {
        /// <summary>Store product identifier.</summary>
        public string ProductId;

        /// <summary>ISO 4217 currency code of the storefront price.</summary>
        public string Currency;
    }
}
