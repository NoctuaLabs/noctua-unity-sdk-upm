using UnityEngine;
using System.Collections.Generic;
using com.noctuagames.sdk;
using UnityEngine.Scripting;
using ILogger = com.noctuagames.sdk.ILogger;

#if UNITY_ANDROID && !UNITY_EDITOR
public class GoogleBilling
{
    private readonly ILogger _log = new NoctuaLogger(typeof(GoogleBilling));
    private AndroidJavaObject _noctua;
    private AndroidJavaObject _activity;
    private bool _isPurchaseFlow;
    private System.Action<PurchaseResult> _pendingGetPurchasedCallback;
    private System.Action<ProductPurchaseStatus> _pendingPurchaseStatusCallback;

    // Signals
    public delegate void PurchaseDone(PurchaseResult result);
    public delegate void ProductDetailsDone(ProductDetailsResponse response);
    public delegate void QueryPurchasesDone(PurchaseResult[] results);
    public event PurchaseDone OnPurchaseDone;
    public event ProductDetailsDone OnProductDetailsDone;
    public event QueryPurchasesDone OnQueryPurchasesDone;

    [Preserve]
    public enum BillingErrorCode
    {
        OK = 0, // Success
        UserCanceled = 1, // Transaction was canceled by the user
        ServiceUnavailable = 2, // The service is currently unavailable
        BillingUnavailable = 3, // A user billing error occurred during processing
        ItemUnavailable = 4, // The requested product is not available for purchase
        DeveloperError = 5, // Error resulting from incorrect usage of the API
        Error = 6, // Fatal error during the API action
        ItemAlreadyOwned = 7, // The purchase failed because the item is already owned
        ItemNotOwned = 8, // Requested action on the item failed since it is not owned by the user
        NetworkError = 12, // A network error occurred during the operation
        FeatureNotSupported = -2, // The requested feature is not supported by the Play Store on the current device
        ServiceDisconnected = -1, // The app is not connected to the Play Store service via the Google Play Billing Library
        ServiceTimeout = -3 // Deprecated: See ServiceUnavailable
    }

    [Preserve]
    public enum PurchaseState
    {
        Unspecified = 0, // The purchase state of the order is unspecified
        Purchased = 1, // The order is purchased
        Pending = 2 // The order is pending
    }

    [Preserve]
    public class PurchaseResult
    {
        public bool Success;
        public BillingErrorCode ErrorCode;
        public PurchaseState PurchaseState;
        public string ProductId;
        public string ReceiptId;
        public string ReceiptData;
        public string Message;
    }

    [Preserve]
    public class ProductDetailsResponse
    {
        public string ProductId;
        public string Title;
        public string Description;
        public string Price;
        public string Currency;
    }

    private void InvokeOnPurchaseDone(PurchaseResult result)
    {
        OnPurchaseDone?.Invoke(result);
    }

    private void InvokeOnProductDetailsResponse(ProductDetailsResponse response)
    {
        OnProductDetailsDone?.Invoke(response);
    }

    private void InvokeOnQueryPurchasesDone(PurchaseResult[] results)
    {
        OnQueryPurchasesDone?.Invoke(results);
    }

    public GoogleBilling()
    {
        using (AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        {
            _activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }

        _noctua = new AndroidJavaClass("com.noctuagames.sdk.Noctua")
            .GetStatic<AndroidJavaObject>("INSTANCE");

        _log.Debug("GoogleBilling constructed with native SDK delegation");
    }

    public void Init()
    {
        _log.Debug("GoogleBilling.Init via native SDK");

        // onPurchaseCompleted callback
        var onPurchaseCompleted = new AndroidCallback<AndroidJavaObject>(javaResult =>
        {
            _log.Debug("onPurchaseCompleted callback received");
            InvokeOnPurchaseDone(MapNoctuaPurchaseResult(javaResult));
        });

        // onPurchaseUpdated callback
        var onPurchaseUpdated = new AndroidCallback<AndroidJavaObject>(javaResult =>
        {
            _log.Debug("onPurchaseUpdated callback received");
            InvokeOnPurchaseDone(MapNoctuaPurchaseResult(javaResult));
        });

        // onProductDetailsLoaded callback
        var onProductDetailsLoaded = new AndroidCallback<AndroidJavaObject>(javaList =>
        {
            _log.Debug("onProductDetailsLoaded callback received");
            int size = javaList.Call<int>("size");

            if (_isPurchaseFlow)
            {
                _isPurchaseFlow = false;

                if (size > 0)
                {
                    var javaDetails = javaList.Call<AndroidJavaObject>("get", 0);
                    _log.Debug("Continuing purchase flow with launchBillingFlow");
                    _noctua.Call("launchBillingFlow", _activity, javaDetails);
                }
                else
                {
                    _log.Error("No product details found for purchase flow");
                    InvokeOnPurchaseDone(new PurchaseResult
                    {
                        Success = false,
                        Message = "product_not_found",
                        ReceiptData = "",
                    });
                }
            }
            else
            {
                // Regular product details query (e.g., for GetActiveCurrency)
                if (size > 0)
                {
                    var javaDetails = javaList.Call<AndroidJavaObject>("get", 0);
                    InvokeOnProductDetailsResponse(MapNoctuaProductDetails(javaDetails));
                }
                else
                {
                    _log.Error("No product details found");
                    InvokeOnProductDetailsResponse(null);
                }
            }
        });

        // onQueryPurchasesCompleted callback
        var onQueryPurchasesCompleted = new AndroidCallback<AndroidJavaObject>(javaList =>
        {
            _log.Debug("onQueryPurchasesCompleted callback received");
            int size = javaList.Call<int>("size");

            if (size > 0)
            {
                var results = new PurchaseResult[size];
                for (int i = 0; i < size; i++)
                {
                    using var javaPurchase = javaList.Call<AndroidJavaObject>("get", i);
                    results[i] = MapNoctuaPurchaseResult(javaPurchase);
                }
                InvokeOnQueryPurchasesDone(results);
            }
            else
            {
                InvokeOnQueryPurchasesDone(null);
            }
        });

        // onRestorePurchasesCompleted (not used, no-op)
        var onRestorePurchasesCompleted = new AndroidCallback<AndroidJavaObject>(_ => { });

        // onProductPurchaseStatusResult callback
        var onProductPurchaseStatusResult = new AndroidCallback<AndroidJavaObject>(javaStatus =>
        {
            _log.Debug("onProductPurchaseStatusResult callback received");

            bool isPurchased = javaStatus.Call<bool>("getIsPurchased");
            string productId = javaStatus.Call<string>("getProductId");
            string purchaseToken = javaStatus.Call<string>("getPurchaseToken");
            string orderId = javaStatus.Call<string>("getOrderId");
            var javaPurchaseState = javaStatus.Call<AndroidJavaObject>("getPurchaseState");
            int purchaseStateInt = javaPurchaseState.Call<int>("getState");
            bool isAcknowledged = javaStatus.Call<bool>("getIsAcknowledged");
            bool isAutoRenewing = javaStatus.Call<bool>("getIsAutoRenewing");
            long purchaseTime = javaStatus.Call<long>("getPurchaseTime");
            long expiryTime = javaStatus.Call<long>("getExpiryTime");
            string originalJson = javaStatus.Call<string>("getOriginalJson");

            // Full status callback (new API)
            if (_pendingPurchaseStatusCallback != null)
            {
                _pendingPurchaseStatusCallback.Invoke(new ProductPurchaseStatus
                {
                    ProductId = productId,
                    IsPurchased = isPurchased,
                    IsAcknowledged = isAcknowledged,
                    IsAutoRenewing = isAutoRenewing,
                    PurchaseState = purchaseStateInt,
                    PurchaseToken = purchaseToken,
                    PurchaseTime = purchaseTime,
                    ExpiryTime = expiryTime,
                    OrderId = orderId ?? "",
                    OriginalJson = originalJson ?? "",
                });
                _pendingPurchaseStatusCallback = null;
            }

            // Legacy bool callback (backward compat)
            if (_pendingGetPurchasedCallback != null)
            {
                if (isPurchased)
                {
                    _pendingGetPurchasedCallback.Invoke(new PurchaseResult
                    {
                        Success = true,
                        ProductId = productId,
                        PurchaseState = (PurchaseState)purchaseStateInt,
                        ReceiptId = orderId ?? "",
                        ReceiptData = purchaseToken,
                    });
                }
                else
                {
                    _pendingGetPurchasedCallback.Invoke(null);
                }
                _pendingGetPurchasedCallback = null;
            }
        });

        // onServerVerificationRequired — forward purchase result so Unity can run its own VerifyOrderAsync
        var onServerVerificationRequired = new AndroidCallback2((javaPurchaseResult, javaConsumableType) =>
        {
            _log.Debug("onServerVerificationRequired callback received");
            var result = MapNoctuaPurchaseResult(javaPurchaseResult);
            InvokeOnPurchaseDone(result);
        });

        // onBillingError callback
        var onBillingError = new AndroidCallback2((javaErrorCode, javaMessage) =>
        {
            _log.Debug("onBillingError callback received");
            int errorCodeInt = javaErrorCode.Call<int>("getCode");
            string message = javaMessage.Call<string>("toString");
            InvokeOnPurchaseDone(new PurchaseResult
            {
                Success = false,
                ErrorCode = (BillingErrorCode)errorCodeInt,
                PurchaseState = PurchaseState.Unspecified,
                Message = message,
                ReceiptData = "",
            });
        });

        _noctua.Call("initializeBilling",
            onPurchaseCompleted,
            onPurchaseUpdated,
            onProductDetailsLoaded,
            onQueryPurchasesCompleted,
            onRestorePurchasesCompleted,
            onProductPurchaseStatusResult,
            onServerVerificationRequired,
            onBillingError
        );

        _log.Debug("GoogleBilling.Init complete");
    }

    public bool IsReady => _noctua.Call<bool>("isBillingReady");

    public void PurchaseItem(string productId)
    {
        _log.Debug("GoogleBilling.PurchaseItem via native SDK: " + productId);
        _isPurchaseFlow = true;

        using var javaList = new AndroidJavaObject("java.util.ArrayList");
        javaList.Call<bool>("add", productId);

        // Pass ProductType.INAPP explicitly — Kotlin default params don't generate JVM overloads
        using var productTypeClass = new AndroidJavaClass("com.noctuagames.sdk.models.ProductType");
        var inapp = productTypeClass.GetStatic<AndroidJavaObject>("INAPP");
        _noctua.Call("queryProductDetails", javaList, inapp);
    }

    public void QueryProductDetails(string productId)
    {
        _log.Debug("GoogleBilling.QueryProductDetails via native SDK: " + productId);
        _isPurchaseFlow = false;

        using var javaList = new AndroidJavaObject("java.util.ArrayList");
        javaList.Call<bool>("add", productId);

        using var productTypeClass = new AndroidJavaClass("com.noctuagames.sdk.models.ProductType");
        var inapp = productTypeClass.GetStatic<AndroidJavaObject>("INAPP");
        _noctua.Call("queryProductDetails", javaList, inapp);
    }

    public void QueryPurchasesAsync()
    {
        _log.Debug("GoogleBilling.QueryPurchasesAsync via native SDK");
        using var productTypeClass = new AndroidJavaClass("com.noctuagames.sdk.models.ProductType");
        var inapp = productTypeClass.GetStatic<AndroidJavaObject>("INAPP");
        _noctua.Call("queryPurchases", inapp);
    }

    public void GetPurchasedProductById(string productId, System.Action<PurchaseResult> callback)
    {
        _log.Debug($"GetPurchasedProductById via native SDK: {productId}");
        _pendingGetPurchasedCallback = callback;
        _noctua.Call("getProductPurchaseStatus", productId);
    }

    public void GetProductPurchaseStatusDetail(string productId, System.Action<ProductPurchaseStatus> callback)
    {
        _log.Debug($"GetProductPurchaseStatusDetail via native SDK: {productId}");
        _pendingPurchaseStatusCallback = callback;
        _noctua.Call("getProductPurchaseStatus", productId);
    }

    /// <summary>
    /// Registers a product with its consumable type.
    /// Must be called before purchasing to let the native SDK know how to handle the product.
    /// </summary>
    /// <param name="productId">The product ID from Google Play Console.</param>
    /// <param name="consumableType">The consumable type of the product.</param>
    public void RegisterProduct(string productId, NoctuaConsumableType consumableType)
    {
        _log.Debug($"GoogleBilling.RegisterProduct: {productId}, type={consumableType}");
        using var javaConsumableType = new AndroidJavaClass("com.noctuagames.sdk.models.ConsumableType");
        var values = javaConsumableType.CallStatic<AndroidJavaObject[]>("values");
        var typeEnum = values[(int)consumableType];
        _noctua.Call("registerProduct", productId, typeEnum);
    }

    /// <summary>
    /// Acknowledges a purchase. Required for non-consumable purchases.
    /// </summary>
    /// <param name="purchaseToken">The purchase token to acknowledge.</param>
    /// <param name="callback">Callback with success status.</param>
    public void AcknowledgePurchase(string purchaseToken, System.Action<bool> callback = null)
    {
        _log.Debug($"GoogleBilling.AcknowledgePurchase: {purchaseToken}");
        if (callback != null)
        {
            var javaCallback = new AndroidCallback<bool>(callback);
            _noctua.Call("acknowledgePurchase", purchaseToken, javaCallback);
        }
        else
        {
            _noctua.Call("acknowledgePurchase", purchaseToken, null);
        }
    }

    /// <summary>
    /// Consumes a purchase. Required for consumable products so they can be purchased again.
    /// </summary>
    /// <param name="purchaseToken">The purchase token to consume.</param>
    /// <param name="callback">Callback with success status.</param>
    public void ConsumePurchase(string purchaseToken, System.Action<bool> callback = null)
    {
        _log.Debug($"GoogleBilling.ConsumePurchase: {purchaseToken}");
        if (callback != null)
        {
            var javaCallback = new AndroidCallback<bool>(callback);
            _noctua.Call("consumePurchase", purchaseToken, javaCallback);
        }
        else
        {
            _noctua.Call("consumePurchase", purchaseToken, null);
        }
    }

    /// <summary>
    /// Completes purchase processing after server verification.
    /// For consumables: consumes the purchase client-side.
    /// For non-consumables/subscriptions: no client-side action needed.
    /// </summary>
    /// <param name="purchaseToken">The purchase token that was verified.</param>
    /// <param name="consumableType">The consumable type of the product.</param>
    /// <param name="verified">Whether the server verification succeeded.</param>
    /// <param name="callback">Optional callback with success status.</param>
    public void CompletePurchaseProcessing(string purchaseToken, NoctuaConsumableType consumableType, bool verified, System.Action<bool> callback = null)
    {
        _log.Debug($"GoogleBilling.CompletePurchaseProcessing: token={purchaseToken}, type={consumableType}, verified={verified}");
        using var javaConsumableType = new AndroidJavaClass("com.noctuagames.sdk.models.ConsumableType");
        var values = javaConsumableType.CallStatic<AndroidJavaObject[]>("values");
        var typeEnum = values[(int)consumableType];

        if (callback != null)
        {
            var javaCallback = new AndroidCallback<bool>(callback);
            _noctua.Call("completePurchaseProcessing", purchaseToken, typeEnum, verified, javaCallback);
        }
        else
        {
            _noctua.Call("completePurchaseProcessing", purchaseToken, typeEnum, verified, null);
        }
    }

    /// <summary>
    /// Restores all purchases by querying both INAPP and SUBS purchases.
    /// Results come through the onRestorePurchasesCompleted callback registered in Init.
    /// </summary>
    public void RestorePurchases()
    {
        _log.Debug("GoogleBilling.RestorePurchases via native SDK");
        _noctua.Call("restorePurchases");
    }

    /// <summary>
    /// Reconnects the billing client if it was disconnected.
    /// </summary>
    public void ReconnectBilling()
    {
        _log.Debug("GoogleBilling.ReconnectBilling via native SDK");
        _noctua.Call("reconnectBilling");
    }

    /// <summary>
    /// Disposes the billing service and releases resources.
    /// </summary>
    public void DisposeBilling()
    {
        _log.Debug("GoogleBilling.DisposeBilling via native SDK");
        _noctua.Call("disposeBilling");
    }

    /// <summary>
    /// Maps a native NoctuaPurchaseResult AndroidJavaObject to GoogleBilling.PurchaseResult
    /// </summary>
    private PurchaseResult MapNoctuaPurchaseResult(AndroidJavaObject javaResult)
    {
        try
        {
            bool success = javaResult.Call<bool>("getSuccess");

            var javaErrorCode = javaResult.Call<AndroidJavaObject>("getErrorCode");
            int errorCodeInt = javaErrorCode.Call<int>("getCode");

            var javaPurchaseState = javaResult.Call<AndroidJavaObject>("getPurchaseState");
            int purchaseStateInt = javaPurchaseState.Call<int>("getState");

            string productId = javaResult.Call<string>("getProductId");
            string orderId = javaResult.Call<string>("getOrderId");
            string purchaseToken = javaResult.Call<string>("getPurchaseToken");
            string message = javaResult.Call<string>("getMessage");

            return new PurchaseResult
            {
                Success = success,
                ErrorCode = (BillingErrorCode)errorCodeInt,
                PurchaseState = (PurchaseState)purchaseStateInt,
                ProductId = productId,
                ReceiptId = orderId ?? "",
                ReceiptData = purchaseToken,
                Message = message,
            };
        }
        catch (System.Exception e)
        {
            _log.Error($"Failed to map NoctuaPurchaseResult: {e.Message}");
            return new PurchaseResult
            {
                Success = false,
                ErrorCode = BillingErrorCode.Error,
                PurchaseState = PurchaseState.Unspecified,
                Message = $"Mapping error: {e.Message}",
                ReceiptData = "",
            };
        }
    }

    /// <summary>
    /// Maps a native NoctuaProductDetails AndroidJavaObject to GoogleBilling.ProductDetailsResponse
    /// </summary>
    private ProductDetailsResponse MapNoctuaProductDetails(AndroidJavaObject javaDetails)
    {
        try
        {
            return new ProductDetailsResponse
            {
                ProductId = javaDetails.Call<string>("getProductId"),
                Title = javaDetails.Call<string>("getTitle"),
                Description = javaDetails.Call<string>("getDescription"),
                Price = javaDetails.Call<string>("getFormattedPrice"),
                Currency = javaDetails.Call<string>("getPriceCurrencyCode"),
            };
        }
        catch (System.Exception e)
        {
            _log.Error($"Failed to map NoctuaProductDetails: {e.Message}");
            return null;
        }
    }
}
#endif
