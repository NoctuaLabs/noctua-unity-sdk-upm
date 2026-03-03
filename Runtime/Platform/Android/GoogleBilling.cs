using UnityEngine;
using System.Collections.Generic;
using com.noctuagames.sdk;
using UnityEngine.Scripting;
using ILogger = com.noctuagames.sdk.ILogger;

#if UNITY_ANDROID && !UNITY_EDITOR
/// <summary>
/// Wraps Google Play Billing via the Kotlin native SDK, handling purchases, product queries, and billing lifecycle.
/// </summary>
public class GoogleBilling
{
    private readonly ILogger _log = new NoctuaLogger(typeof(GoogleBilling));
    private AndroidJavaObject _noctua;
    private AndroidJavaObject _activity;
    private bool _isPurchaseFlow;
    private System.Action<PurchaseResult> _pendingGetPurchasedCallback;
    private System.Action<ProductPurchaseStatus> _pendingPurchaseStatusCallback;

    /// <summary>
    /// Delegate invoked when a purchase flow completes (success or failure).
    /// </summary>
    public delegate void PurchaseDone(PurchaseResult result);

    /// <summary>
    /// Delegate invoked when product details are loaded from Google Play.
    /// </summary>
    public delegate void ProductDetailsDone(ProductDetailsResponse response);

    /// <summary>
    /// Delegate invoked when a query for existing purchases completes.
    /// </summary>
    public delegate void QueryPurchasesDone(PurchaseResult[] results);

    /// <summary>
    /// Raised when a purchase flow completes or a billing error occurs.
    /// </summary>
    public event PurchaseDone OnPurchaseDone;

    /// <summary>
    /// Raised when product details are successfully loaded from Google Play.
    /// </summary>
    public event ProductDetailsDone OnProductDetailsDone;

    /// <summary>
    /// Raised when a query for existing purchases completes.
    /// </summary>
    public event QueryPurchasesDone OnQueryPurchasesDone;

    /// <summary>
    /// Maps to Google Play Billing Library BillingResponseCode values.
    /// </summary>
    [Preserve]
    public enum BillingErrorCode
    {
        /// <summary>Success.</summary>
        OK = 0,
        /// <summary>Transaction was canceled by the user.</summary>
        UserCanceled = 1,
        /// <summary>The service is currently unavailable.</summary>
        ServiceUnavailable = 2,
        /// <summary>A user billing error occurred during processing.</summary>
        BillingUnavailable = 3,
        /// <summary>The requested product is not available for purchase.</summary>
        ItemUnavailable = 4,
        /// <summary>Error resulting from incorrect usage of the API.</summary>
        DeveloperError = 5,
        /// <summary>Fatal error during the API action.</summary>
        Error = 6,
        /// <summary>The purchase failed because the item is already owned.</summary>
        ItemAlreadyOwned = 7,
        /// <summary>Requested action on the item failed since it is not owned by the user.</summary>
        ItemNotOwned = 8,
        /// <summary>A network error occurred during the operation.</summary>
        NetworkError = 12,
        /// <summary>The requested feature is not supported by the Play Store on the current device.</summary>
        FeatureNotSupported = -2,
        /// <summary>The app is not connected to the Play Store service via the Google Play Billing Library.</summary>
        ServiceDisconnected = -1,
        /// <summary>Deprecated. See <see cref="ServiceUnavailable"/>.</summary>
        ServiceTimeout = -3
    }

    /// <summary>
    /// Maps to Google Play Billing Library PurchaseState values.
    /// </summary>
    [Preserve]
    public enum PurchaseState
    {
        /// <summary>The purchase state of the order is unspecified.</summary>
        Unspecified = 0,
        /// <summary>The order is purchased.</summary>
        Purchased = 1,
        /// <summary>The order is pending.</summary>
        Pending = 2
    }

    /// <summary>
    /// Represents the result of a Google Play purchase or query operation.
    /// </summary>
    [Preserve]
    public class PurchaseResult
    {
        /// <summary>Whether the purchase completed successfully.</summary>
        public bool Success;
        /// <summary>The billing error code if the purchase failed.</summary>
        public BillingErrorCode ErrorCode;
        /// <summary>The current state of the purchase order.</summary>
        public PurchaseState PurchaseState;
        /// <summary>The Google Play product identifier.</summary>
        public string ProductId;
        /// <summary>The order ID (receipt identifier) from Google Play.</summary>
        public string ReceiptId;
        /// <summary>The purchase token used for server verification and consumption.</summary>
        public string ReceiptData;
        /// <summary>A human-readable message describing the result or error.</summary>
        public string Message;
    }

    /// <summary>
    /// Contains product details retrieved from Google Play for display or pricing purposes.
    /// </summary>
    [Preserve]
    public class ProductDetailsResponse
    {
        /// <summary>The Google Play product identifier.</summary>
        public string ProductId;
        /// <summary>The localized product title.</summary>
        public string Title;
        /// <summary>The localized product description.</summary>
        public string Description;
        /// <summary>The formatted price string including currency symbol.</summary>
        public string Price;
        /// <summary>The ISO 4217 currency code for the price.</summary>
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

    /// <summary>
    /// Constructs the GoogleBilling wrapper and obtains references to the Unity activity and native SDK singleton.
    /// </summary>
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

    /// <summary>
    /// Initializes the billing service by registering all native SDK callbacks for purchase, product details,
    /// query, error, and server verification events.
    /// </summary>
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

    /// <summary>
    /// Gets whether the billing client is connected and ready for operations.
    /// </summary>
    public bool IsReady => _noctua.Call<bool>("isBillingReady");

    /// <summary>
    /// Initiates a purchase flow for the specified product ID by first querying product details,
    /// then launching the billing flow.
    /// </summary>
    /// <param name="productId">The Google Play product identifier to purchase.</param>
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

    /// <summary>
    /// Queries product details from Google Play for the specified product ID without initiating a purchase.
    /// </summary>
    /// <param name="productId">The Google Play product identifier to query.</param>
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

    /// <summary>
    /// Queries all existing in-app purchases owned by the user from Google Play.
    /// Results are delivered via <see cref="OnQueryPurchasesDone"/>.
    /// </summary>
    public void QueryPurchasesAsync()
    {
        _log.Debug("GoogleBilling.QueryPurchasesAsync via native SDK");
        using var productTypeClass = new AndroidJavaClass("com.noctuagames.sdk.models.ProductType");
        var inapp = productTypeClass.GetStatic<AndroidJavaObject>("INAPP");
        _noctua.Call("queryPurchases", inapp);
    }

    /// <summary>
    /// Retrieves the purchase result for a specific product by its ID from Google Play.
    /// </summary>
    /// <param name="productId">The Google Play product identifier to look up.</param>
    /// <param name="callback">Callback with the <see cref="PurchaseResult"/>, or null if not purchased.</param>
    public void GetPurchasedProductById(string productId, System.Action<PurchaseResult> callback)
    {
        _log.Debug($"GetPurchasedProductById via native SDK: {productId}");
        _pendingGetPurchasedCallback = callback;
        _noctua.Call("getProductPurchaseStatus", productId);
    }

    /// <summary>
    /// Retrieves detailed purchase status for a product, including acknowledgment, renewal, and expiry state.
    /// </summary>
    /// <param name="productId">The Google Play product identifier to query.</param>
    /// <param name="callback">Callback with the full <see cref="ProductPurchaseStatus"/> details.</param>
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
