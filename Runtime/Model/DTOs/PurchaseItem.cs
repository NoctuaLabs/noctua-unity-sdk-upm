using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Represents a locally stored purchase record used for pending purchase retry and history display.
    /// </summary>
    [Preserve]
    public class PurchaseItem
    {
        /// <summary>Server-assigned order identifier.</summary>
        public int OrderId;
        /// <summary>Payment type used for this purchase (e.g., "playstore", "appstore").</summary>
        public string PaymentType;
        /// <summary>Current status of the purchase (e.g., "pending", "completed").</summary>
        public string Status;
        /// <summary>Display name of the purchased item.</summary>
        public string PurchaseItemName;
        /// <summary>ISO 8601 timestamp when the purchase was initiated.</summary>
        public string Timestamp;
        /// <summary>Serialized JSON of the original order request payload.</summary>
        public string OrderRequest;
        /// <summary>Serialized JSON of the verification request payload.</summary>
        public string VerifyOrderRequest;
        /// <summary>Player identifier that initiated the purchase (null if unknown).</summary>
        public long? PlayerId;
    }
}
