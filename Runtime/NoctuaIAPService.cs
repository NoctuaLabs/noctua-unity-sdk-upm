using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine.Device;
using UnityEngine;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    public partial class Product
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("game_id")]
        public int GameId { get; set; }

        [JsonProperty("vat_rate")]
        public double VatRate { get; set; }

        [JsonProperty("enabled_payment_types")]
        public object EnabledPaymentTypes { get; set; }

        [JsonProperty("price")]
        public double Price { get; set; }

        [JsonProperty("price_vat")]
        public double PriceVat { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }
    }

    [JsonArray]
    public class ProductList : List<Product>
    {
    }

    public class OrderRequest
    {
        [JsonProperty("payment_type")]
        public string PaymentType { get; set; }

        [JsonProperty("product_id")]
        public string ProductId { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("role_id")]
        public string RoleId { get; set; }

        [JsonProperty("server_id")]
        public string ServerId { get; set; }

        [JsonProperty("ingame_item_id")]
        public string IngameItemId { get; set; }

        [JsonProperty("ingame_item_name")]
        public string IngameItemName { get; set; }
    }

    public class OrderResponse
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("product_id")]
        public string ProductId { get; set; }
    }

    public class VerifyOrderRequest
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("receipt_data")]
        public string ReceiptData { get; set; }
    }

    public class VerifyOrderResponse
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("status")]
        public string Status { get; set; }
    }

    public class PurchaseRequest
    {
        [JsonProperty("product_id")]
        public string ProductId { get; set; }

        [JsonProperty("price")]
        public decimal Price { get; set; }

        [JsonProperty("currency")]
        public string Currency { get; set; }

        [JsonProperty("role_id")]
        public string RoleId { get; set; }

        [JsonProperty("server_id")]
        public string ServerId { get; set; }

        [JsonProperty("ingame_item_id")]
        public string IngameItemId { get; set; }

        [JsonProperty("ingame_item_name")]
        public string IngameItemName { get; set; }
    }

    public class PurchaseResponse
    {
        [JsonProperty("status")]
        public string Status { get; set; }
    }

    public class NoctuaIAPService
    {
        private static Config _config;

        private string _accessToken;

        private static int _currentOrderId;

        #if UNITY_ANDROID && !UNITY_EDITOR
        private static readonly GoogleBilling GoogleBillingInstance = new GoogleBilling();
        // Event to forward purchase results to the users of this class
        #endif
        public static event Action<PurchaseResponse> OnPurchaseDone;

        internal NoctuaIAPService(Config config)
        {
            _config = config;

            #if UNITY_ANDROID && !UNITY_EDITOR
            // Subscribe to the GoogleBillingInstance's OnPurchaseDone event
            GoogleBillingInstance.OnPurchaseDone += HandlePurchaseDone;
            GoogleBillingInstance?.Init();
            #endif
        }

        public async UniTask<ProductList> GetProductListAsync()
        {
            Debug.Log("NoctuaIAPService.GetProductListAsync");

            // TODO construct from token
            int gameId = 1;
            string currency = "USD";
            string enabledPaymentTypes = "playstore";

            Debug.Log("NoctuaIAPService.GetProductListAsync");
            Debug.Log(_config.BaseUrl);
            Debug.Log(_config.ClientId);
            Debug.Log(gameId);
            Debug.Log(currency);
            Debug.Log(enabledPaymentTypes);

            var url = $"{_config.BaseUrl}/products?game_id={gameId}&currency={currency}&enabled_payment_types={enabledPaymentTypes}";
            Debug.Log(url);

            var request = new HttpRequest(HttpMethod.Get, url)
                .WithHeader("X-CLIENT-ID", _config.ClientId);

            var response = await request.Send<ProductList>();

            return response;

        }

        private async UniTask<OrderResponse> CreateOrderAsync(OrderRequest order)
        {
            var url = $"{_config.BaseUrl}/orders";
            Debug.Log(url);

            var request = new HttpRequest(HttpMethod.Post, url)
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithJsonBody(order);

            var response = await request.Send<OrderResponse>();

            return response;
        }

        private static async UniTask<VerifyOrderResponse> VerifyOrderAsync(VerifyOrderRequest order)
        {
            var url = $"{_config.BaseUrl}/verify-order";
            Debug.Log(url);

            var request = new HttpRequest(HttpMethod.Post, url)
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithJsonBody(order);

            var response = await request.Send<VerifyOrderResponse>();

            return response;
        }

        public async void PurchaseItemAsync(PurchaseRequest purchaseRequest)
        {
            Debug.Log("NoctuaIAPService.PurchaseItemAsync");
            var orderRequest = new OrderRequest
            {
                PaymentType = "playstore", // TODO determine the payment type automatically
                ProductId = purchaseRequest.ProductId,
                Price = purchaseRequest.Price,
                Currency = purchaseRequest.Currency,
                RoleId = purchaseRequest.RoleId,
                ServerId = purchaseRequest.ServerId,
                IngameItemId = purchaseRequest.IngameItemId,
                IngameItemName = purchaseRequest.IngameItemName
            };

            _currentOrderId = 0; // Clear up first
            OrderResponse orderResponse = null;
            try {
                Debug.Log("NoctuaIAPService.PurchaseItemAsync try to CreateOrderAsync");
                orderResponse = await CreateOrderAsync(orderRequest);
            } catch (Exception e) {
                Debug.Log("NoctuaIAPService.PurchaseItemAsync CreateOrderAsync failed");
                if (e is NoctuaException) {
                    Debug.Log("NoctuaException: " + ((NoctuaException)e).ErrorCode + " : " + ((NoctuaException)e).Message);
                } else {
                    Debug.Log("Exception: " + e);
                }
                throw e;
            }

            _currentOrderId = orderResponse.Id;
            #if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance?.PurchaseItem(orderResponse.ProductId);
            #endif
        }

        #if UNITY_ANDROID && !UNITY_EDITOR
        private static async void HandlePurchaseDone(GoogleBilling.PurchaseResult result)
        {
            Debug.Log("Noctua.HandlePurchaseDone");
            // Forward the event to subscribers of Noctua's OnPurchaseDone even
            if (result == null || (result != null && !result.Success))
            {
                if (result.Message != null) { // Empty message means canceled
                    Debug.LogError("Purchase canceled: ");
                    OnPurchaseDone?.Invoke(new PurchaseResponse{
                        Status = "canceled"
                    });
                    return;
                }

                Debug.LogError("Purchase failed: " + result.Message);
                OnPurchaseDone?.Invoke(new PurchaseResponse{
                    Status = "failed"
                });
                return;
            }

            Debug.Log("Purchase was successful! Let's verify it");
            Debug.Log(result.ReceiptData);
            Debug.Log(_currentOrderId);
            var verifyOrderRequest = new VerifyOrderRequest
            {
                Id = _currentOrderId,
                ReceiptData = result.ReceiptData
            };

            try {
                var verifyOrderResponse = await VerifyOrderAsync(verifyOrderRequest);
                if (verifyOrderResponse.Status != "completed")
                {
                    SavePendingPurchase(verifyOrderRequest); // For retry later
                }
                OnPurchaseDone?.Invoke(new PurchaseResponse{
                    Status = "completed"
                });
            } catch (Exception e) {
                SavePendingPurchase(verifyOrderRequest); // For retry later
                if (e is NoctuaException noctuaEx)
                {
                    Debug.Log("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                }
                throw e;
            }
        }
        #endif

        private static void SavePendingPurchase(VerifyOrderRequest newOrder)
        {
            Debug.Log("Noctua.SavePendingPurchase");
            string json = PlayerPrefs.GetString("NoctuaPendingPurchases", string.Empty);

            List<VerifyOrderRequest> orders;

            if (string.IsNullOrEmpty(json))
            {
                orders = new List<VerifyOrderRequest>();
            }
            else
            {
                orders = JsonConvert.DeserializeObject<List<VerifyOrderRequest>>(json);
            }

            orders.Add(newOrder);

            string updatedJson = JsonConvert.SerializeObject(orders);

            PlayerPrefs.SetString("NoctuaPendingPurchases", updatedJson);
            PlayerPrefs.Save();
        }

        public List<VerifyOrderRequest> GetPendingPurchases()
        {
            Debug.Log("Noctua.GetPendingPurchases");
            string json = PlayerPrefs.GetString("NoctuaPendingPurchases", string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                return new List<VerifyOrderRequest>();
            }
            List<VerifyOrderRequest> orders = JsonConvert.DeserializeObject<List<VerifyOrderRequest>>(json);

            // Clear up
            PlayerPrefs.SetString("NoctuaPendingPurchases", "[]");
            PlayerPrefs.Save();

            return orders;
        }

        public async void RetryPendingPurchases()
        {
            Debug.Log("Noctua.RetryPendingPurchases");
            List<VerifyOrderRequest> orders = GetPendingPurchases();

            if (orders.Count == 0)
            {
                Debug.Log("No pending purchases to retry.");
                return;
            }

            foreach (VerifyOrderRequest verifyOrderRequest in orders)
            {
                try {
                    Debug.Log($"Retrying Order ID: {verifyOrderRequest.Id}, Receipt Data: {verifyOrderRequest.ReceiptData}");
                    var verifyOrderResponse = await VerifyOrderAsync(verifyOrderRequest);
                    if (verifyOrderResponse.Status != "completed")
                    {
                        SavePendingPurchase(verifyOrderRequest); // For retry later
                    }
                    OnPurchaseDone?.Invoke(new PurchaseResponse{
                        Status = "completed"
                    });
                } catch (Exception e) {
                    SavePendingPurchase(verifyOrderRequest); // For retry later
                    if (e is NoctuaException noctuaEx)
                    {
                        Debug.Log("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                    }
                    throw e;
                }
            }
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
        }
    }


}