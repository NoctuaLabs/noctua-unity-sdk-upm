using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Internal purchase record used by NoctuaIAPService for persistence and retry logic.
    /// Not to be confused with the PurchaseItem DTO in Model/DTOs/ which is the UI-facing type
    /// with serialized (string) OrderRequest/VerifyOrderRequest fields.
    /// </summary>
    [Preserve]
    public class InternalPurchaseItem
    {
        public int OrderId;
        public OrderRequest OrderRequest;
        public VerifyOrderRequest VerifyOrderRequest;
        public string AccessToken;
        public string Status;
        public long? PlayerId;
        /// <summary>
        /// Native purchase token used to finalize the transaction after server verification.
        /// Persisted so retry flows can call CompletePurchaseProcessing.
        /// </summary>
        public string PurchaseToken;
    }
}
