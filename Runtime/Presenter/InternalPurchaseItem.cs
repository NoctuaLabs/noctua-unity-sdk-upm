using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Internal purchase record used by NoctuaIAPService for persistence and retry logic.
    /// Not to be confused with the PurchaseItem DTO in Model/DTOs/ which is the UI-facing type
    /// with serialized (string) OrderRequest/VerifyOrderRequest fields.
    /// </summary>
    [Preserve]
    internal class InternalPurchaseItem
    {
        public int OrderId;
        public OrderRequest OrderRequest;
        public VerifyOrderRequest VerifyOrderRequest;
        public string AccessToken;
        public string Status;
        public long? PlayerId;
    }
}
