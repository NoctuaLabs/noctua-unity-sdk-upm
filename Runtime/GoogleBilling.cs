﻿using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Scripting;

#if UNITY_ANDROID && !UNITY_EDITOR
public class GoogleBilling
{
    private AndroidJavaObject billingClient;
    private AndroidJavaObject activity;

    // Signals
    public delegate void PurchaseDone(PurchaseResult result);
    public delegate void ProductDetailsDone(ProductDetailsResponse response);
    public event PurchaseDone OnPurchaseDone;
    public event ProductDetailsDone OnProductDetailsDone;

    [Preserve]
    public class PurchaseResult
    {
        public bool Success;
        public string PurchaseToken;
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

    public void Init()
    {
        Debug.Log("GoogleBilling.Start. Initialize billing client.");
        InitializeBilling();
    }

    private void InitializeBilling()
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
            Debug.Log("GoogleBilling.InitializeBilling");
            billingClient = billingClientClass.CallStatic<AndroidJavaObject>("newBuilder", activity)
                .Call<AndroidJavaObject>("setListener", new PurchasesUpdatedListener(this))
                .Call<AndroidJavaObject>("enablePendingPurchases")
                .Call<AndroidJavaObject>("build");
            Debug.Log("GoogleBilling.InitializeBilling done.");
        }

        billingClient.Call("startConnection", new BillingClientStateListener());
    }

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
            Debug.Log("GoogleBilling.ProductDetailsResponseListener");
            int responseCode = billingResult.Call<int>("getResponseCode");
            if (responseCode == 0) // BillingResponseCode.OK
            {
                Debug.Log("Product details query successful");

                int size = productDetailsList.Call<int>("size");
                Debug.Log(size);

                if (size < 1) {
                    Debug.LogError("No product details found");
                    var result = new PurchaseResult{
                        Success = false,
                        Message = "product_not_found",
                        ReceiptData = "",
                    };
                    Debug.Log("InvokeOnPurchaseDone with product_not_found");
                    googleBilling.InvokeOnPurchaseDone(result);
                    return;
                }

                AndroidJavaObject productDetails = productDetailsList.Call<AndroidJavaObject>("get", 0);
                string productId = productDetails.Call<string>("getProductId");
                Debug.Log("Product ID: " + productId);
                Debug.Log("Continue to launchBillingFlow");


                Debug.Log("Create ProductDetailsParams object");
                // Process other details as needed
                AndroidJavaClass productDetailsParamsClass = new AndroidJavaClass(
                    "com.android.billingclient.api.BillingFlowParams$ProductDetailsParams"
                );
                // Get the er class from ProductDetailsParams
                Debug.Log("Assign productDetails object into the productDetailsParams object");
                AndroidJavaObject productDetailsParams = productDetailsParamsClass
                    .CallStatic<AndroidJavaObject>("newBuilder")
                    .Call<AndroidJavaObject>("setProductDetails", productDetails)
                    .Call<AndroidJavaObject>("build");

                // Get the ImmutableList class
                Debug.Log("Create immutable list");
                AndroidJavaClass immutableListClass = new AndroidJavaClass("com.google.common.collect.ImmutableList");

                // Get the ImmutableList Builder
                Debug.Log("Assign productDetailsParams object into the immutable list");
                AndroidJavaObject productDetailsParamsList = immutableListClass.CallStatic<AndroidJavaObject>("builder")
                                        .Call<AndroidJavaObject>("add", productDetailsParams)
                                        .Call<AndroidJavaObject>("build");


                Debug.Log("Create billingFlowParams object");
                AndroidJavaClass billingFlowParamsClass = new AndroidJavaClass(
                    "com.android.billingclient.api.BillingFlowParams"
                );
                Debug.Log("Assign productDetailsParamsList into the billingFlowParams object");
                AndroidJavaObject billingFlowParams = billingFlowParamsClass.CallStatic<AndroidJavaObject>("newBuilder")
                    .Call<AndroidJavaObject>("setProductDetailsParamsList", productDetailsParamsList)
                    .Call<AndroidJavaObject>("build");
                Debug.Log("Call launchBillingFlow with billingFlowParams as param");
                billingClient.Call<AndroidJavaObject>("launchBillingFlow", activity, billingFlowParams);
            }
            else
            {
                string errorMessage = billingResult.Call<string>("getDebugMessage");
                Debug.LogError("Failed to query product details: " + errorMessage);
            }
        }
    }

    // Inner classes to handle purchase callbacks
    private class PurchasesUpdatedListener : AndroidJavaProxy
    {
        private GoogleBilling googleBilling;
        public PurchasesUpdatedListener(GoogleBilling parent) : base(
            "com.android.billingclient.api.PurchasesUpdatedListener"
        ) {
            googleBilling = parent;
        }

        void onPurchasesUpdated(AndroidJavaObject billingResult, AndroidJavaObject purchases)
        {
            Debug.Log("GoogleBilling.PurchasesUpdatedListener");
            int responseCode = billingResult.Call<int>("getResponseCode");
            if (responseCode == 0 && purchases != null)
            {
                Debug.Log("Purchase successful");
                for (int i = 0; i < purchases.Call<int>("size"); i++)
                {
                    AndroidJavaObject purchase = purchases.Call<AndroidJavaObject>("get", i);
                    Debug.Log("GoogleBilling.PurchasesUpdatedListener: purchase object found");
                    var originalJson = purchase.Call<string>("getOriginalJson");
                    Debug.Log("originalJson: " + originalJson);
                    var receiptData = purchase.Call<string>("getPurchaseToken");
                    Debug.Log("receiptData: " + receiptData);
                    googleBilling.InvokeOnPurchaseDone(new PurchaseResult{
                        Success = true,
                        Message = "success",
                        ReceiptData = receiptData,
                    });
                    break;
                }
            }
            else
            {
                string errorMessage = billingResult.Call<string>("getDebugMessage");
                Debug.LogError("Purchase failed: " + errorMessage);
                googleBilling.InvokeOnPurchaseDone(new PurchaseResult{
                    Success = false,
                    Message = errorMessage,
                    PurchaseToken = "",
                    ReceiptData = "",
                });
            }
        }
    }

    private class BillingClientStateListener : AndroidJavaProxy
    {
        public BillingClientStateListener() : base("com.android.billingclient.api.BillingClientStateListener") { }

        void onBillingSetupFinished(AndroidJavaObject billingResult)
        {
            int responseCode = billingResult.Call<int>("getResponseCode");
            if (responseCode == 0) // BillingResponseCode.OK
            {
                Debug.Log("Billing setup finished successfully");
            }
            else
            {
                string errorMessage = billingResult.Call<string>("getDebugMessage");
                Debug.LogError("Billing setup failed: " + errorMessage);
            }
        }

        void onBillingServiceDisconnected()
        {
            Debug.LogError("Billing service disconnected");
        }
    }

    public void QueryProductDetails(string productId)
    {
        Debug.Log("GoogleBilling.QueryProductDetails: " + productId);
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
        private GoogleBilling googleBilling;

        public ProductDetailsResponseListener(GoogleBilling parent) : base("com.android.billingclient.api.ProductDetailsResponseListener")
        {
            googleBilling = parent;
        }

        void onProductDetailsResponse(AndroidJavaObject billingResult, AndroidJavaObject productDetailsList)
        {
            Debug.Log("GoogleBilling.ProductDetailsResponseListener");
            int responseCode = billingResult.Call<int>("getResponseCode");
            if (responseCode == 0) // BillingResponseCode.OK
            {
                int size = productDetailsList.Call<int>("size");
                Debug.Log("Product details list length: " + size);

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
                    Debug.LogError("No product details found");
                }
            }
            else
            {
                string errorMessage = billingResult.Call<string>("getDebugMessage");
                Debug.LogError("Failed to query product details: " + errorMessage);
            }
        }
    }
}
#endif

