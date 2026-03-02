using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Contract for the IAP presenter. Used by the View layer to interact
    /// with purchase functionality without depending on the concrete NoctuaIAPService.
    /// </summary>
    /// <remarks>
    /// Note: Methods that return <c>NoctuaIAPService.PurchaseItem</c> (nested type) are intentionally
    /// excluded from this interface. They will be added once the nested type is extracted to a
    /// standalone model class. In the meantime, consumers needing those methods (e.g., AuthUIController)
    /// continue to reference the concrete NoctuaIAPService.
    /// </remarks>
    public interface IIAPService
    {
        /// <summary>Fired when a purchase flow completes successfully.</summary>
        event Action<OrderRequest> OnPurchaseDone;

        /// <summary>Fired when a purchase enters pending state.</summary>
        event Action<OrderRequest> OnPurchasePending;

        /// <summary>Whether the native IAP subsystem (e.g. Google Billing) is ready.</summary>
        bool IsReady { get; }

        /// <summary>Fetch available products from the server.</summary>
        UniTask<ProductList> GetProductListAsync(string currency = null, string platformType = null);

        /// <summary>Retry a specific pending purchase by its order ID.</summary>
        UniTask<OrderStatus> RetryPendingPurchaseByOrderId(int orderId);
    }
}
