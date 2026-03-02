using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{
    [Preserve]
    public class ProductPurchaseStatus
    {
        public string ProductId;
        public bool IsPurchased;
        public bool IsAcknowledged;
        public bool IsAutoRenewing;
        public int PurchaseState;       // 0=Unspecified, 1=Purchased, 2=Pending
        public string PurchaseToken;
        public long PurchaseTime;       // ms since epoch
        public long ExpiryTime;         // ms since epoch, 0 if N/A (always 0 on Android)
        public string OrderId;
        public string OriginalJson;
    }
}
