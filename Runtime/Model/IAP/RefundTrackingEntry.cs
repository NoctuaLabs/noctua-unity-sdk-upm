using System;
using Newtonsoft.Json;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Compact local record persisted to PlayerPrefs (key: <c>NoctuaRefundTracking</c>) when a
    /// non-consumable product is purchased successfully. The SDK detects non-consumability by
    /// probing <c>GetPurchaseStatusAsync</c> right after purchase — consumables always return
    /// <c>false</c>, so only confirmed non-consumables land in this store. Used exclusively by
    /// <see cref="NoctuaIAPService.IsRefundEligibleAsync"/>.
    /// Kept separate from <see cref="InternalPurchaseItem"/> so the refund-tracking store can
    /// evolve independently of the pending-purchase / retry pipeline.
    /// </summary>
    [Preserve]
    public class RefundTrackingEntry
    {
        /// <summary>Product identifier of the purchase.</summary>
        [JsonProperty("product_id")]
        public string ProductId;

        /// <summary>Payment type used for this purchase.</summary>
        [JsonProperty("payment_type")]
        public PaymentType PaymentType;

        /// <summary>UTC timestamp when the purchase was recorded by the SDK.</summary>
        [JsonProperty("timestamp")]
        public DateTime Timestamp;
    }
}
