using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Contract for the IAP presenter. Used by the View layer to interact
    /// with purchase functionality without depending on the concrete NoctuaIAPService.
    /// </summary>
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

        /// <summary>Get all pending purchases awaiting retry.</summary>
        List<InternalPurchaseItem> GetPendingPurchases();

        /// <summary>Get a specific pending purchase by order ID.</summary>
        InternalPurchaseItem GetPendingPurchaseByOrderId(int orderId);

        /// <summary>Get the purchase history.</summary>
        List<InternalPurchaseItem> GetPurchaseHistory();

        /// <summary>Retry all pending purchases.</summary>
        UniTask RetryPendingPurchasesAsync();
    }
}
