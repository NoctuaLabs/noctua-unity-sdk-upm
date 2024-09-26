using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Threading;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine.Scripting;

namespace com.noctuagames.sdk
{

    [Preserve, JsonConverter(typeof(StringEnumConverter), typeof(SnakeCaseNamingStrategy))]
    public enum PaymentType
    {
        Unknown,
        Applestore,
        Playstore,
        Noctuawallet
    }
    
    [Preserve]
    public class Product
    {
        [JsonProperty("id")]
        public string Id;

        [JsonProperty("description")]
        public string Description;

        [JsonProperty("game_id")]
        public int GameId;

        [JsonProperty("vat_rate")]
        public double VatRate;

        [JsonProperty("enabled_payment_types")]
        public PaymentType[] EnabledPaymentTypes;

        [JsonProperty("price")]
        public double Price;

        [JsonProperty("price_vat")]
        public double PriceVat;

        [JsonProperty("currency")]
        public string Currency;
    }

    [JsonArray]
    public class ProductList : List<Product>
    {
    }

    [Preserve]
    public class OrderRequest
    {
        [JsonProperty("payment_type")] 
        public PaymentType PaymentType;

        [JsonProperty("product_id")]
        public string ProductId;

        [JsonProperty("price")]
        public decimal Price;

        [JsonProperty("currency")]
        public string Currency;

        [JsonProperty("role_id")]
        public string RoleId;

        [JsonProperty("server_id")]
        public string ServerId;

        [JsonProperty("ingame_item_id")]
        public string IngameItemId;

        [JsonProperty("ingame_item_name")]
        public string IngameItemName;
    }

    [Preserve]
    public class OrderResponse
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("product_id")]
        public string ProductId;

        [JsonProperty("payment_url")]
        public string PaymentUrl;
    }
    
    public enum PaymentStatus
    {
        Successful,
        Canceled,
        Failed,
        Confirmed
    }
    
    public class PaymentResult
    {
        public PaymentStatus Status;
        public string ReceiptData;
        public string Message;
    }

    [Preserve]
    public class VerifyOrderRequest
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("receipt_data")]
        public string ReceiptData;
    }

    [Preserve, JsonConverter(typeof(StringEnumConverter), typeof(SnakeCaseNamingStrategy))]
    public enum OrderStatus
    {
        Unknown,
        Pending,
        Completed,
        Failed,
        Refunded,
        Canceled,
        Expired,
        VerificationFailed,
    }

    [Preserve]
    public class VerifyOrderResponse
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("status")]
        public OrderStatus Status;
    }

    [Preserve]
    public class PurchaseRequest
    {
        [JsonProperty("product_id")]
        public string ProductId;

        [JsonProperty("price")]
        public decimal Price;

        [JsonProperty("currency")]
        public string Currency;

        [JsonProperty("role_id")]
        public string RoleId;

        [JsonProperty("server_id")]
        public string ServerId;

        [JsonProperty("ingame_item_id")]
        public string IngameItemId;

        [JsonProperty("ingame_item_name")]
        public string IngameItemName;
    }
    
    [Preserve]
    public class PurchaseResponse
    {
        [JsonProperty("order_id")]
        public int OrderId;
        
        [JsonProperty("status")]
        public OrderStatus Status;

        [JsonProperty("message")] 
        public string Message;
    }

    [Preserve]
    public class NoctuaIAPService
    {
        private readonly Config _config;

        private UniTaskCompletionSource<string> _activeCurrencyTcs;

        // By default all major payment types are enabled, then it will be overridden by the server config at SDK init
        private List<PaymentType> _enabledPaymentTypes = new()
            { PaymentType.Playstore, PaymentType.Applestore, PaymentType.Noctuawallet };

        private readonly AccessTokenProvider _accessTokenProvider;
        private readonly NoctuaWebPaymentService _noctuaPayment;

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly GoogleBilling GoogleBillingInstance = new();
#elif UNITY_IOS && !UNITY_EDITOR
        private readonly IosPlugin IosPluginInstance = new IosPlugin();
#endif


        internal NoctuaIAPService(Config config, AccessTokenProvider accessTokenProvider)
        {
            _config = config;
            _accessTokenProvider = accessTokenProvider;
            _noctuaPayment = new NoctuaWebPaymentService(config.WebPaymentBaseUrl);

#if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.OnProductDetailsDone += HandleGoogleProductDetails;
            GoogleBillingInstance?.Init();
#elif UNITY_IOS && !UNITY_EDITOR
            IosPluginInstance?.Init();
#endif
        }

        public void SetEnabledPaymentTypes(List<PaymentType> enabledPaymentTypes)
        {
            _enabledPaymentTypes = enabledPaymentTypes;
        }

        public async UniTask<ProductList> GetProductListAsync()
        {
            Debug.Log("NoctuaIAPService.GetProductListAsync");

            var recentAccount = Noctua.Auth.GetRecentAccount();

            if (recentAccount?.Player?.GameId == null || recentAccount.Player.GameId <= 0)
            {
                throw new Exception("Game ID not found or invalid. Please authenticate first");
            }

            string gameId = recentAccount.Player.GameId.ToString();
            string currency = Noctua.Locale.GetCurrency();
            string enabledPaymentTypes = string.Join(",", _enabledPaymentTypes).ToLower();

            Debug.Log("NoctuaIAPService.GetProductListAsync");
            Debug.Log(_config.BaseUrl);
            Debug.Log(_config.ClientId);
            Debug.Log(gameId);
            Debug.Log(currency);
            Debug.Log(enabledPaymentTypes);

            var url =
                $"{_config.BaseUrl}/products?game_id={gameId}&currency={currency}&enabled_payment_types={enabledPaymentTypes}";

            Debug.Log(url);

            var request = new HttpRequest(HttpMethod.Get, url)
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken);

            var response = await request.Send<ProductList>();

            return response;
        }

        private async UniTask<OrderResponse> CreateOrderAsync(OrderRequest order)
        {
            var url = $"{_config.BaseUrl}/orders";

            var request = new HttpRequest(HttpMethod.Post, url)
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken)
                .WithJsonBody(order);

            var response = await request.Send<OrderResponse>();

            return response;
        }

        private async UniTask<VerifyOrderResponse> VerifyOrderAsync(VerifyOrderRequest order)
        {
            var url = $"{_config.BaseUrl}/verify-order";

            var request = new HttpRequest(HttpMethod.Post, url)
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken)
                .WithJsonBody(order);

            var response = await request.Send<VerifyOrderResponse>();

            return response;
        }

        public async UniTask<string> GetActiveCurrencyAsync(string productId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            var tcs = new UniTaskCompletionSource<string>();
            IosPluginInstance.GetActiveCurrency(productId, (success, currency) => {
                Debug.Log("NoctuaIAPService.GetActiveCurrency callback");
                Debug.Log("NoctuaIAPService.GetActiveCurrency callback success: " + success);
                Debug.Log("NoctuaIAPService.GetActiveCurrency callback currency: " + currency);
                if (!success) {
                    Debug.Log("NoctuaIAPService.GetActiveCurrency callback currency: " + currency);
                    tcs.TrySetException(NoctuaException.ActiveCurrencyFailure);
                    return;  
                }
                tcs.TrySetResult(currency);
            });
            var activeCurrency = await tcs.Task;
            tcs.TrySetCanceled();

            return activeCurrency;

#elif UNITY_ANDROID && !UNITY_EDITOR
            Debug.Log("GetActiveCurrencyAsync: Android");
            _activeCurrencyTcs = new UniTaskCompletionSource<string>();
            GoogleBillingInstance.QueryProductDetails(productId);

            var activeCurrency = await _activeCurrencyTcs.Task;
            _activeCurrencyTcs.TrySetCanceled();
            _activeCurrencyTcs = null;

            return activeCurrency;

#else // TODO for Other platforms

            Debug.Log("GetActiveCurrencyAsync: not found, default to IDR");

            return "IDR";

#endif
        }

        public async UniTask<PurchaseResponse> PurchaseItemAsync(PurchaseRequest purchaseRequest)
        {
            Debug.Log("NoctuaIAPService.PurchaseItemAsync");

            // TODO payment method selector. For now, it's hardcoded according to the platform

            var paymentType = Application.platform switch
            {
                RuntimePlatform.WindowsPlayer or RuntimePlatform.OSXPlayer => PaymentType.Noctuawallet,
                RuntimePlatform.Android => PaymentType.Playstore,
                RuntimePlatform.IPhonePlayer => PaymentType.Applestore,
                _ => throw new NoctuaException(NoctuaErrorCode.Payment, "Unsupported payment type")
            };

            var orderRequest = new OrderRequest
            {
                PaymentType = paymentType,
                ProductId = purchaseRequest.ProductId,
                Price = purchaseRequest.Price,
                Currency = purchaseRequest.Currency,
                RoleId = purchaseRequest.RoleId,
                ServerId = purchaseRequest.ServerId,
                IngameItemId = purchaseRequest.IngameItemId,
                IngameItemName = purchaseRequest.IngameItemName
            };

            if (string.IsNullOrEmpty(orderRequest.Currency))
            {
                orderRequest.Currency = Noctua.Locale.GetCurrency();
            }

            OrderResponse orderResponse;

            try
            {
                Debug.Log("NoctuaIAPService.PurchaseItemAsync try to CreateOrderAsync");

                orderResponse = await CreateOrderAsync(orderRequest);
            }
            catch (Exception e)
            {
                Debug.Log("NoctuaIAPService.PurchaseItemAsync CreateOrderAsync failed");

                if (e is NoctuaException exception)
                {
                    Debug.Log("NoctuaException: " + exception.ErrorCode + " : " + exception.Message);
                }
                else
                {
                    Debug.Log("Exception: " + e);
                }

                throw;
            }

            Debug.Log("NoctuaIAPService.PurchaseItemAsync _currentOrderId: "         + orderResponse.Id);
            Debug.Log("NoctuaIAPService.PurchaseItemAsync orderResponse.ProductId: " + orderResponse.ProductId);

            var timeoutTask = UniTask.Delay(TimeSpan.FromSeconds(300), DelayType.UnscaledDeltaTime);
            var paymentTcs = new UniTaskCompletionSource<PaymentResult>();
            var hasResult = true;
            PaymentResult paymentResult;

            switch (paymentType)
            {
                case PaymentType.Applestore:
#if UNITY_IOS && !UNITY_EDITOR
                    Debug.Log("NoctuaIAPService.PurchaseItemAsync purchase on ios: " + orderResponse.ProductId);
                    orderResponse.ProductId = purchaseRequest.ProductId;
                    IosPluginInstance.PurchaseItem(orderResponse.ProductId, (success, message) => {
                        Debug.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback");
                        Debug.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback success: " + success);
                        Debug.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback message: " + message);
                        
                        paymentTcs.TrySetResult(GetAppstorePaymentResult(orderResponse.Id, success, message));
                    });

                    (hasResult, paymentResult) = await UniTask.WhenAny(paymentTcs.Task, timeoutTask);
                    
                    Debug.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback response: " + paymentResult);
                    break;
#else
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Applestore payment is not supported on this platform");
#endif
                case PaymentType.Playstore:
#if UNITY_ANDROID && !UNITY_EDITOR
                    Debug.Log("NoctuaIAPService.PurchaseItemAsync purchase on playstore: " + orderResponse.ProductId);
                    
                    void PurchaseDone(GoogleBilling.PurchaseResult result)
                    {
                        Debug.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback");
                        
                        paymentTcs.TrySetResult(GetPlaystorePaymentResult(result));
                    }
                    
                    GoogleBillingInstance.OnPurchaseDone += PurchaseDone;
                    GoogleBillingInstance.PurchaseItem(orderResponse.ProductId);

                    (hasResult, paymentResult) = await UniTask.WhenAny(paymentTcs.Task, timeoutTask);

                    GoogleBillingInstance.OnPurchaseDone -= PurchaseDone;
                    
                    Debug.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback response: " + paymentResult);
                    break;
#else
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Playstore payment is not supported on this platform");
#endif

                case PaymentType.Noctuawallet:
                    hasResult = true;
                    paymentResult = await _noctuaPayment.PayAsync(orderResponse.PaymentUrl);
                    
                    break;
                case PaymentType.Unknown:
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Unknown payment type");
                default:
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Unsupported payment type " + paymentType);
            }
            
            if (!hasResult)
            {
                paymentTcs.TrySetCanceled();
                
                throw new NoctuaException(NoctuaErrorCode.Payment, "Payment timeout");
            }
            
            if (paymentResult.Status is not (PaymentStatus.Successful or PaymentStatus.Confirmed))
            {
                throw new NoctuaException(NoctuaErrorCode.Payment, $"OrderStatus: {paymentResult.Status}, Message: {paymentResult.Message}");
            }

            var orderId = orderResponse.Id;
            Debug.Log($"Purchase was successful. Verifying order ID: {orderId}");
            Debug.Log(orderId);
            var verifyOrderRequest = new VerifyOrderRequest
            {
                Id = orderId,
                ReceiptData = paymentResult.ReceiptData
            };

            Debug.Log(verifyOrderRequest.Id);
            Debug.Log(verifyOrderRequest.ReceiptData);
            
            VerifyOrderResponse verifyOrderResponse;

            try {
                verifyOrderResponse = await VerifyOrderAsync(verifyOrderRequest);
                if (verifyOrderResponse.Status != OrderStatus.Completed)
                {
                    SavePendingPurchase(verifyOrderRequest); // For retry later
                }
            } catch (Exception e) {
                SavePendingPurchase(verifyOrderRequest); // For retry later
                if (e is NoctuaException noctuaEx)
                {
                    Debug.Log("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                }
                
                throw;
            }

            return new PurchaseResponse
            {
                OrderId = verifyOrderResponse.Id,
                Status = verifyOrderResponse.Status,
                Message = "Purchase completed"
            };
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void HandleGoogleProductDetails(GoogleBilling.ProductDetailsResponse response)
        {
            Debug.Log("NoctuaIAPService.HandleGoogleProductDetails");
            Debug.Log("NoctuaIAPService.HandleGoogleProductDetails currency: " + response.Currency);
            if (_activeCurrencyTcs != null) {
                _activeCurrencyTcs.TrySetResult(response.Currency);
            } else {
                throw NoctuaException.MissingCompletionHandler;
            }
        }

        private PaymentResult GetPlaystorePaymentResult(GoogleBilling.PurchaseResult result)
        {
            Debug.Log("Noctua.HandleGooglePurchaseDone");
            
            if (result == null || !result.Success)
            {
                Debug.Log("Noctua.HandleGooglePurchaseDone result.Message: " + result.Message);
                if (string.IsNullOrEmpty(result.Message)) { // Empty message means canceled
                    Debug.LogError("Purchase canceled: empty message means canceled");

                    return new PaymentResult
                    {
                        Status = PaymentStatus.Canceled,
                        Message = "Purchase canceled"
                    };
                }

                Debug.LogError("Purchase failed: " + result.Message);
                
                return new PaymentResult{
                    Status = PaymentStatus.Failed,
                    Message = result.Message
                };
            }

            return new PaymentResult
            {
                Status = PaymentStatus.Successful,
                ReceiptData = result.ReceiptData
            };
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        private PaymentResult GetAppstorePaymentResult(int orderId, bool success, string message)
        {
            Debug.Log("Noctua.HandleIosPurchaseDone");
            Debug.Log("Noctua.HandleIosPurchaseDone orderId: " + orderId);
            Debug.Log("Noctua.HandleIosPurchaseDone success: " + success);
            Debug.Log("Noctua.HandleIosPurchaseDone message: " + message);

            if (!success)
            {
                // Check if message contains cancel keyword
                if (message.Contains("cancel")) {
                    Debug.LogError("Purchase canceled: ");

                    return new PaymentResult
                    {
                        Status = PaymentStatus.Canceled,
                        Message = "Purchase canceled"
                    };
                }

                Debug.LogError("Purchase failed: " + message);
                
                return new PaymentResult
                {
                    Status = PaymentStatus.Failed,
                    Message = message
                };
            }

            return new PaymentResult
            {
                Status = PaymentStatus.Successful,
                ReceiptData = message
            };
        }
#endif

        private void SavePendingPurchase(VerifyOrderRequest newOrder)
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
                    if (verifyOrderResponse.Status != OrderStatus.Completed)
                    {
                        SavePendingPurchase(verifyOrderRequest); // For retry later
                    }
                } catch (Exception e) {
                    SavePendingPurchase(verifyOrderRequest); // For retry later
                    if (e is NoctuaException noctuaEx)
                    {
                        Debug.Log("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                    }
                    throw;
                }
            }
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
            public string WebPaymentBaseUrl;
        }
    }
}