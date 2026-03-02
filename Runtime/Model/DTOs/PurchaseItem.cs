using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class PurchaseItem
    {
        public int OrderId;
        public string PaymentType;
        public string Status;
        public string PurchaseItemName;
        public string Timestamp;
        public string OrderRequest;
        public string VerifyOrderRequest;
        public long? PlayerId;
    }
}
