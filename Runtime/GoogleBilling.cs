using UnityEngine;
using System.Collections.Generic;
using com.noctuagames.sdk;
using UnityEngine.Scripting;
using ILogger = com.noctuagames.sdk.ILogger;

#if UNITY_ANDROID && !UNITY_EDITOR
public class GoogleBilling
{
    private readonly ILogger _log = new NoctuaLogger(typeof(GoogleBilling));
    private AndroidJavaObject billingClient;
    private AndroidJavaObject activity;

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
            activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
        }

        // Initialize the BillingClient
        using (AndroidJavaClass billingClientClass = new AndroidJavaClass(
                "com.android.billingclient.api.BillingClient"
            ))
        {
            _log.Debug("start");
            billingClient = billingClientClass.CallStatic<AndroidJavaObject>("newBuilder", activity)
                .Call<AndroidJavaObject>("setListener", new PurchasesUpdatedListener(this))
                .Call<AndroidJavaObject>("enablePendingPurchases")
                .Call<AndroidJavaObject>("build");
            _log.Debug("done");
        }
    }

    public void Init()
    {
        _log.Debug("GoogleBilling.Start. Initialize billing client.");
        billingClient.Call("startConnection", new BillingClientStateListener());
    }
    
    public bool IsReady => billingClient.Call<bool>("isReady");
    
    public void PurchaseItem(string productId)
    {
        // ProductDetails object can't be created by hand.
        // We need to get the ProductDetails object first by quering against Google Server.
         // Create a list of products to query
        using (AndroidJavaClass productClass = new AndroidJavaClass(
            "com.android.billingclient.api.QueryProductDetailsParams$Product"
        ))
        {
            AndroidJavaObject product = productClass.CallStatic<AndroidJavaObject>("newBuilder")
                                                     .Call<AndroidJavaObject>("setProductId", productId)
                                                     .Call<AndroidJavaObject>("setProductType", "inapp") 
                                                     .Call<AndroidJavaObject>("build");

            AndroidJavaClass immutableListClass = new AndroidJavaClass("com.google.common.collect.ImmutableList");
            AndroidJavaObject productList = immutableListClass.CallStatic<AndroidJavaObject>("of", product);

            // Create QueryProductDetailsParams
            AndroidJavaClass queryProductDetailsParamsClass = new AndroidJavaClass(
                "com.android.billingclient.api.QueryProductDetailsParams"
            );
            AndroidJavaObject queryProductDetailsParams = queryProductDetailsParamsClass
                .CallStatic<AndroidJavaObject>("newBuilder")
                .Call<AndroidJavaObject>("setProductList", productList)
                .Call<AndroidJavaObject>("build");

            // Call queryProductDetailsAsync
            billingClient.Call(
                "queryProductDetailsAsync",
                queryProductDetailsParams,
                new ContinueToBillingFlow(billingClient, activity, this)
            );
        }
    }

    // This is actually a ProductDetailsResponseListener but we make it as a continuation flow for PurcahseItem
    // because we need to get the ProductDetails object first.
    private class ContinueToBillingFlow : AndroidJavaProxy
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(ContinueToBillingFlow));
        private GoogleBilling googleBilling;
        private AndroidJavaObject billingClient;
        private AndroidJavaObject activity;
        public ContinueToBillingFlow(
            AndroidJavaObject parentBillingClient,
            AndroidJavaObject parentActivity,
            GoogleBilling parentGoogleBilling
        ) : base("com.android.billingclient.api.ProductDetailsResponseListener") {
            billingClient = parentBillingClient;
            activity = parentActivity;
            googleBilling = parentGoogleBilling;
        }

        void onProductDetailsResponse(AndroidJavaObject billingResult, AndroidJavaObject productDetailsList)
        {
            _log.Debug("GoogleBilling.ProductDetailsResponseListener");
            int responseCode = billingResult.Call<int>("getResponseCode");
            _log.Debug($"billingResult responseCode: '{(BillingErrorCode)responseCode}'");

            if (responseCode == 0) // BillingResponseCode.OK
            {
                _log.Debug("Product details query successful");

                int size = productDetailsList.Call<int>("size");
                _log.Debug(size.ToString());

                if (size < 1) {
                    _log.Error("No product details found");
                    var result = new PurchaseResult{
                        Success = false,
                        Message = "product_not_found",
                        ReceiptData = "",
                    };
                    _log.Debug("InvokeOnPurchaseDone with product_not_found");
                    googleBilling.InvokeOnPurchaseDone(result);
                    return;
                }

                AndroidJavaObject productDetails = productDetailsList.Call<AndroidJavaObject>("get", 0);
                string productId = productDetails.Call<string>("getProductId");
                _log.Debug("Product ID: " + productId);
                _log.Debug("Continue to launchBillingFlow");


                _log.Debug("Create ProductDetailsParams object");
                // Process other details as needed
                AndroidJavaClass productDetailsParamsClass = new AndroidJavaClass(
                    "com.android.billingclient.api.BillingFlowParams$ProductDetailsParams"
                );
                // Get the er class from ProductDetailsParams
                _log.Debug("Assign productDetails object into the productDetailsParams object");
                AndroidJavaObject productDetailsParams = productDetailsParamsClass
                    .CallStatic<AndroidJavaObject>("newBuilder")
                    .Call<AndroidJavaObject>("setProductDetails", productDetails)
                    .Call<AndroidJavaObject>("build");

                // Get the ImmutableList class
                _log.Debug("Create immutable list");
                AndroidJavaClass immutableListClass = new AndroidJavaClass("com.google.common.collect.ImmutableList");

                // Get the ImmutableList Builder
                _log.Debug("Assign productDetailsParams object into the immutable list");
                AndroidJavaObject productDetailsParamsList = immutableListClass.CallStatic<AndroidJavaObject>("builder")
                                        .Call<AndroidJavaObject>("add", productDetailsParams)
                                        .Call<AndroidJavaObject>("build");


                _log.Debug("Create billingFlowParams object");
                AndroidJavaClass billingFlowParamsClass = new AndroidJavaClass(
                    "com.android.billingclient.api.BillingFlowParams"
                );
                _log.Debug("Assign productDetailsParamsList into the billingFlowParams object");
                AndroidJavaObject billingFlowParams = billingFlowParamsClass.CallStatic<AndroidJavaObject>("newBuilder")
                    .Call<AndroidJavaObject>("setProductDetailsParamsList", productDetailsParamsList)
                    .Call<AndroidJavaObject>("build");
                _log.Debug("Call launchBillingFlow with billingFlowParams as param");
                billingClient.Call<AndroidJavaObject>("launchBillingFlow", activity, billingFlowParams);
            }
            else
            {
                string errorMessage = billingResult.Call<string>("getDebugMessage");
                _log.Error("Failed to query product details: " + errorMessage);
                
                var result = new PurchaseResult{
                    Success = false,
                    ErrorCode = (BillingErrorCode)responseCode,
                    PurchaseState = PurchaseState.Unspecified,
                    Message = errorMessage,
                    ReceiptData = "",
                };
                
                googleBilling.InvokeOnPurchaseDone(result);
            }
        }
    }

    // Inner classes to handle purchase callbacks
    private class PurchasesUpdatedListener : AndroidJavaProxy
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(PurchasesUpdatedListener));
        private GoogleBilling googleBilling;
        public PurchasesUpdatedListener(GoogleBilling parent) : base(
            "com.android.billingclient.api.PurchasesUpdatedListener"
        ) {
            googleBilling = parent;
        }

        void onPurchasesUpdated(AndroidJavaObject billingResult, AndroidJavaObject purchases)
        {
            var responseCode = billingResult.Call<int>("getResponseCode");
            var debugMessage = billingResult.Call<string>("getDebugMessage");
            _log.Debug($"billingResult code: '{(BillingErrorCode)responseCode}', message: '{debugMessage}'");

            if (responseCode != 0)
            {
                _log.Error($"purchase failed, code: {(BillingErrorCode)responseCode}");
            }

            if (purchases == null)
            {
                _log.Debug("purchases is null");
                
                googleBilling.InvokeOnPurchaseDone(new PurchaseResult{
                    Success = false,
                    ErrorCode = (BillingErrorCode)responseCode,
                    PurchaseState = PurchaseState.Unspecified,
                    Message = debugMessage,
                    ReceiptData = "",
                });
                
                return;
            }

            var purchaseSize = purchases.Call<int>("size");

            if (purchaseSize < 1)
            {
                _log.Debug($"purchaseSize is 0");

                googleBilling.InvokeOnPurchaseDone(new PurchaseResult{
                    Success = false,
                    ErrorCode = (BillingErrorCode)responseCode,
                    PurchaseState = PurchaseState.Unspecified,
                    Message = debugMessage,
                    ReceiptData = "",
                });

                return;
            }
            
            //Assuming only one purchase is made at a time
            AndroidJavaObject purchase = purchases.Call<AndroidJavaObject>("get", 0);
            _log.Debug($"originalJson: '{purchase.Call<string>("getOriginalJson")}'");

            var purchaseState = purchase.Call<int>("getPurchaseState");
            var orderId = purchase.Call<string>("getOrderId");
            var purchaseToken = purchase.Call<string>("getPurchaseToken");
            // getProducts() returns a List<String> of product IDs
            var productList = purchase.Call<AndroidJavaObject>("getProducts");
            var productId = productList.Call<string>("get", 0); // Get first SKU

            _log.Debug($"orderId: '{orderId}', purchaseToken: '{purchaseToken}', purchaseState: '{(PurchaseState)purchaseState}'");

            googleBilling.InvokeOnPurchaseDone(new PurchaseResult{
                Success = true,
                ErrorCode = (BillingErrorCode)responseCode,
                PurchaseState = (PurchaseState)purchaseState,
                Message = debugMessage,
                ReceiptId = orderId,
                ReceiptData = purchaseToken,
                ProductId = productId,
            });
        }
    }

    private class BillingClientStateListener : AndroidJavaProxy
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(BillingClientStateListener));
        public BillingClientStateListener() : base("com.android.billingclient.api.BillingClientStateListener") { }

        void onBillingSetupFinished(AndroidJavaObject billingResult)
        {
            int responseCode = billingResult.Call<int>("getResponseCode");
            if (responseCode == 0) // BillingResponseCode.OK
            {
                _log.Debug("Billing setup finished successfully");
            }
            else
            {
                string errorMessage = billingResult.Call<string>("getDebugMessage");
                _log.Error("Billing setup failed: " + errorMessage);
            }
        }

        void onBillingServiceDisconnected()
        {
            _log.Error("Billing service disconnected");
        }
    }

    public void QueryProductDetails(string productId)
    {
        _log.Debug("GoogleBilling.QueryProductDetails: " + productId);
        using (AndroidJavaClass productClass = new AndroidJavaClass(
            "com.android.billingclient.api.QueryProductDetailsParams$Product"
            ))
        {
            AndroidJavaObject product = productClass.CallStatic<AndroidJavaObject>("newBuilder")
                .Call<AndroidJavaObject>("setProductId", productId)
                .Call<AndroidJavaObject>("setProductType", "inapp")
                .Call<AndroidJavaObject>("build");

            // Use ImmutableList instead of ArrayList
            AndroidJavaClass immutableListClass = new AndroidJavaClass("com.google.common.collect.ImmutableList");
            AndroidJavaObject productList = immutableListClass.CallStatic<AndroidJavaObject>("of", product);

            using (AndroidJavaClass queryProductDetailsParamsClass = new AndroidJavaClass(
                "com.android.billingclient.api.QueryProductDetailsParams"
            ))
            {
                AndroidJavaObject queryProductDetailsParams = queryProductDetailsParamsClass
                    .CallStatic<AndroidJavaObject>("newBuilder")
                    .Call<AndroidJavaObject>("setProductList", productList)
                    .Call<AndroidJavaObject>("build");

                billingClient.Call(
                    "queryProductDetailsAsync",
                    queryProductDetailsParams,
                    new ProductDetailsResponseListener(this)
                );
            }
        }
    }

    private class ProductDetailsResponseListener : AndroidJavaProxy
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(ProductDetailsResponseListener));
        private GoogleBilling googleBilling;

        public ProductDetailsResponseListener(GoogleBilling parent) : base("com.android.billingclient.api.ProductDetailsResponseListener")
        {
            googleBilling = parent;
        }

        void onProductDetailsResponse(AndroidJavaObject billingResult, AndroidJavaObject productDetailsList)
        {
            _log.Debug("GoogleBilling.ProductDetailsResponseListener");
            int responseCode = billingResult.Call<int>("getResponseCode");
            if (responseCode == 0) // BillingResponseCode.OK
            {
                int size = productDetailsList.Call<int>("size");
                _log.Debug("Product details list length: " + size);

                if (size > 0)
                {
                    AndroidJavaObject productDetails = productDetailsList.Call<AndroidJavaObject>("get", 0);
                    string productId = productDetails.Call<string>("getProductId");
                    string title = productDetails.Call<string>("getTitle");
                    string description = productDetails.Call<string>("getDescription");

                    // Get the first pricing phase for the product
                    AndroidJavaObject oneTimePurchaseOfferDetails = productDetails.Call<AndroidJavaObject>("getOneTimePurchaseOfferDetails");
                    string formattedPrice = oneTimePurchaseOfferDetails.Call<string>("getFormattedPrice");
                    string priceCurrencyCode = oneTimePurchaseOfferDetails.Call<string>("getPriceCurrencyCode");

                    googleBilling.InvokeOnProductDetailsResponse(new ProductDetailsResponse
                    {
                        ProductId = productId,
                        Title = title,
                        Description = description,
                        Price = formattedPrice,
                        Currency = priceCurrencyCode,
                    });
                }
                else
                {
                    _log.Error("No product details found");
                    
                    googleBilling.InvokeOnProductDetailsResponse(null);
                }
            }
            else
            {
                string errorMessage = billingResult.Call<string>("getDebugMessage");
                _log.Error("Failed to query product details: " + errorMessage + ": returning empty strings");

                googleBilling.InvokeOnProductDetailsResponse(null);
            }
        }
    }

    public void QueryPurchasesAsync()
    {
        _log.Debug("GoogleBilling.QueryPurchasesAsync: ");
        // https://developer.android.com/reference/com/android/billingclient/api/QueryPurchasesParams
        using (AndroidJavaClass queryPurchasesParamsClass = new AndroidJavaClass(
            "com.android.billingclient.api.QueryPurchasesParams"
            ))
        {
            AndroidJavaObject queryParams = queryPurchasesParamsClass
                .CallStatic<AndroidJavaObject>("newBuilder")
                .Call<AndroidJavaObject>("setProductType", "inapp")
                .Call<AndroidJavaObject>("build");

            billingClient.Call(
                "queryPurchasesAsync",
                queryParams,
                new QueryPurchasesResponseListener(this)
            );
        }
    }

    private class QueryPurchasesResponseListener : AndroidJavaProxy
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(QueryPurchasesResponseListener));
        private GoogleBilling googleBilling;

        public QueryPurchasesResponseListener(GoogleBilling parent) : base("com.android.billingclient.api.PurchasesResponseListener")
        {
            googleBilling = parent;
        }

        void onQueryPurchasesResponse(AndroidJavaObject billingResult, AndroidJavaObject purchaseList)
        {
            _log.Debug("GoogleBilling.PurchasesResponseListener");
            int responseCode = billingResult.Call<int>("getResponseCode");
            if (responseCode == 0) // BillingResponseCode.OK
            {
                int size = purchaseList.Call<int>("size");
                _log.Debug("Purchase list length: " + size);

                if (size > 0)
                {
                    PurchaseResult[] results = new PurchaseResult[size];
                    for (int i = 0; i < size; i++)
                    {
                        AndroidJavaObject purchase = purchaseList.Call<AndroidJavaObject>("get", i);
                        var productList = purchase.Call<AndroidJavaObject>("getProducts");
                        // Get first SKU since we don't support
                        // multiple product purchase
                        string productId = productList.Call<string>("get", 0);
                        string purchaseToken = purchase.Call<string>("getPurchaseToken");
                        int purchaseState = purchase.Call<int>("getPurchaseState");
                        string orderId = purchase.Call<string>("getOrderId");
                        string originalJson = purchase.Call<string>("getOriginalJson");

                        results[i] = new PurchaseResult
                        {
                            Success = true,
                            ProductId = productId,
                            PurchaseState = (PurchaseState)purchaseState,
                            ReceiptId = orderId,
                            ReceiptData = purchaseToken
                        };
                    }

                    googleBilling.InvokeOnQueryPurchasesDone(results);
                }
                else
                {
                    _log.Error("No purchase found");
                    googleBilling.InvokeOnQueryPurchasesDone(null);
                }
            }
            else
            {
                string errorMessage = billingResult.Call<string>("getDebugMessage");
                _log.Error("Failed to query product details: " + errorMessage + ": returning empty strings");

                googleBilling.InvokeOnQueryPurchasesDone(null);
            }
        }
    }
}
#endif

