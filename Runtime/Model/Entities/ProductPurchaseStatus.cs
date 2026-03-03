using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Represents the purchase status of a product from the native store (Google Play or App Store).
    /// </summary>
    [Preserve]
    public class ProductPurchaseStatus
    {
        /// <summary>Store product identifier.</summary>
        public string ProductId;
        /// <summary>Whether this product has been purchased.</summary>
        public bool IsPurchased;
        /// <summary>Whether the purchase has been acknowledged by the app.</summary>
        public bool IsAcknowledged;
        /// <summary>Whether this is an auto-renewing subscription.</summary>
        public bool IsAutoRenewing;
        /// <summary>Native purchase state: 0 = Unspecified, 1 = Purchased, 2 = Pending.</summary>
        public int PurchaseState;       // 0=Unspecified, 1=Purchased, 2=Pending
        /// <summary>Store-issued purchase token used for server-side verification.</summary>
        public string PurchaseToken;
        /// <summary>Purchase timestamp in milliseconds since Unix epoch.</summary>
        public long PurchaseTime;       // ms since epoch
        /// <summary>Expiry timestamp in milliseconds since Unix epoch (0 if not applicable; always 0 on Android).</summary>
        public long ExpiryTime;         // ms since epoch, 0 if N/A (always 0 on Android)
        /// <summary>Store-issued order identifier.</summary>
        public string OrderId;
        /// <summary>Raw JSON string of the original purchase receipt from the native store.</summary>
        public string OriginalJson;
    }
}
