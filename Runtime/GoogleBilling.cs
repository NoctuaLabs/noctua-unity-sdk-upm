using UnityEngine;
using System.Collections.Generic;

public class GoogleBilling : MonoBehaviour
{
    private AndroidJavaObject billingClient;
    private AndroidJavaObject activity;

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
        using (AndroidJavaClass billingClientClass = new AndroidJavaClass("com.android.billingclient.api.BillingClient"))
        {
            billingClient = billingClientClass.CallStatic<AndroidJavaObject>("newBuilder", activity)
                .Call<AndroidJavaObject>("setListener", new PurchasesUpdatedListener())
                .Call<AndroidJavaObject>("enablePendingPurchases")
                .Call<AndroidJavaObject>("build");
        }

        billingClient.Call("startConnection", new BillingClientStateListener());
    }

    public void GetProductList()
    {
        Debug.Log("=========================GoogleBilling.GetProductList");
        // Create the Product object
        using (AndroidJavaClass productClass = new AndroidJavaClass("com.android.billingclient.api.QueryProductDetailsParams$Product"))
        {
            AndroidJavaObject product = productClass.CallStatic<AndroidJavaObject>("newBuilder")
            .Call<AndroidJavaObject>("setProductId", "product_id_example")
            .Call<AndroidJavaObject>("setProductType", "subs") // "inapp" for in-app purchases or "subs" for subscriptions
            .Call<AndroidJavaObject>("build");

            // Create a Java ArrayList for the product
            using (AndroidJavaObject productList = new AndroidJavaObject("java.util.ArrayList"))
            {
                productList.Call<bool>("add", product); // Add the product to the list

                // Create QueryProductDetailsParams
                using (AndroidJavaClass queryProductDetailsParamsClass = new AndroidJavaClass("com.android.billingclient.api.QueryProductDetailsParams"))
                {
                    AndroidJavaObject queryProductDetailsParams = queryProductDetailsParamsClass.CallStatic<AndroidJavaObject>("newBuilder")
                    .Call<AndroidJavaObject>("setProductList", productList)
                    .Call<AndroidJavaObject>("build");

                    // Call queryProductDetailsAsync on the BillingClient
                    billingClient.Call("queryProductDetailsAsync", queryProductDetailsParams, new ProductDetailsResponseListener());
                }
            }
        }
    }

    public void PurchaseItem(string sku)
    {
        Debug.Log("=========================GoogleBilling.PurchaseItem");
        // Fetch the SKU details
        AndroidJavaObject skuDetails = GetSkuDetails(sku);
        Debug.Log("=========================GoogleBilling.PurchaseItem skuDetails");

        // Launch the billing flow
        if (skuDetails != null)
        {
            Debug.Log("=========================GoogleBilling.PurchaseItem launchBillingFlow");
            using (AndroidJavaClass billingFlowParamsClass = new AndroidJavaClass("com.android.billingclient.api.BillingFlowParams"))
            {
                AndroidJavaObject billingFlowParams = billingFlowParamsClass.CallStatic<AndroidJavaObject>("newBuilder")
                    .Call<AndroidJavaObject>("setSkuDetails", skuDetails)
                    .Call<AndroidJavaObject>("build");

                billingClient.Call<AndroidJavaObject>("launchBillingFlow", activity, billingFlowParams);
            }
        }
        else
        {
            Debug.LogError("SKU details not found for " + sku);
        }
    }

    private AndroidJavaObject GetSkuDetails(string sku)
    {
        Debug.Log("=========================GoogleBilling.PurchaseItem getSkuDetails");
        using (AndroidJavaClass skuDetailsParamsClass = new AndroidJavaClass("com.android.billingclient.api.SkuDetailsParams"))
        {
            List<string> skuList = new List<string> { sku };

            AndroidJavaObject skuDetailsParams = skuDetailsParamsClass.CallStatic<AndroidJavaObject>("newBuilder")
                .Call<AndroidJavaObject>("setSkusList", new AndroidJavaObject("java.util.ArrayList", skuList.ToArray()))
                .Call<AndroidJavaObject>("setType", "inapp") // or "subs" for subscriptions
                .Call<AndroidJavaObject>("build");

            AndroidJavaObject skuDetailsResult = billingClient.Call<AndroidJavaObject>("querySkuDetails", skuDetailsParams);

            AndroidJavaObject skuDetailsList = skuDetailsResult.Call<AndroidJavaObject>("getSkuDetailsList");

            if (skuDetailsList.Call<int>("size") > 0)
            {
                return skuDetailsList.Call<AndroidJavaObject>("get", 0);
            }
        }

        return null;
    }

    // Inner classes to handle callbacks
    private class PurchasesUpdatedListener : AndroidJavaProxy
    {
        public PurchasesUpdatedListener() : base("com.android.billingclient.api.PurchasesUpdatedListener") { }

        void onPurchasesUpdated(AndroidJavaObject billingResult, AndroidJavaObject purchases)
        {
            int responseCode = billingResult.Call<int>("getResponseCode");
            if (responseCode == 0 && purchases != null) // BillingResponseCode.OK
            {
                Debug.Log("Purchase successful");
            }
            else
            {
                string errorMessage = billingResult.Call<string>("getDebugMessage");
                Debug.LogError("Purchase failed: " + errorMessage);
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

    private class ProductDetailsResponseListener : AndroidJavaProxy
    {
        public ProductDetailsResponseListener() : base("com.android.billingclient.api.ProductDetailsResponseListener") { }

        void onProductDetailsResponse(AndroidJavaObject billingResult, AndroidJavaObject productDetailsList)
        {
            Debug.Log("GoogleBilling.ProductDetailsResponseListener");
            int responseCode = billingResult.Call<int>("getResponseCode");
            if (responseCode == 0) // BillingResponseCode.OK
            {
                Debug.Log("Product details query successful");

                int size = productDetailsList.Call<int>("size");
                Debug.Log(size);
                for (int i = 0; i < size; i++)
                {
                    AndroidJavaObject productDetails = productDetailsList.Call<AndroidJavaObject>("get", i);
                    string productId = productDetails.Call<string>("getProductId");
                    Debug.Log("Product ID: " + productId);
                    // Process other details as needed
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

