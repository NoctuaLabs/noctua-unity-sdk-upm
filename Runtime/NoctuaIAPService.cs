using System;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using com.noctuagames.sdk.Events;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using UnityEngine.Scripting;
using Random = System.Random;
using com.noctuagames.sdk.UI;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk
{
    
    [Preserve]
    public class PaymentSettings
    {
        [JsonProperty("payment_type")]
        public PaymentType PaymentType;
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

        [JsonProperty("order_status")]
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
        private readonly ILogger _log = new NoctuaUnityDebugLogger();

        private TaskCompletionSource<string> _activeCurrencyTcs;

        private readonly EventSender _eventSender;
        private readonly AccessTokenProvider _accessTokenProvider;
        private readonly NoctuaWebPaymentService _noctuaPayment;
        private readonly Queue<RetryPendingPurchaseItem> _waitingPendingPurchases = new();
        private readonly UniTask _retryPendingPurchasesTask;
        private readonly INativePlugin _nativePlugin;

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly GoogleBilling GoogleBillingInstance = new();
        // By default all major payment types are enabled, then it will be overridden by the server config at SDK init
        private List<PaymentType> _enabledPaymentTypes = new()
            { PaymentType.Playstore, PaymentType.Noctuawallet };
#elif UNITY_IOS && !UNITY_EDITOR
        // By default all major payment types are enabled, then it will be overridden by the server config at SDK init
        private List<PaymentType> _enabledPaymentTypes = new()
            { PaymentType.Applestore, PaymentType.Noctuawallet };
#else
        private List<PaymentType> _enabledPaymentTypes = new()
            { PaymentType.Noctuawallet };
#endif
        private readonly UIFactory _uiFactory;

        internal NoctuaIAPService(
            Config config,
            AccessTokenProvider accessTokenProvider,
            UIFactory uiFactory,
            INativePlugin nativePlugin,
            EventSender eventSender = null
        )
        {
            _config = config;
            _accessTokenProvider = accessTokenProvider;
            _eventSender = eventSender;
            _noctuaPayment = new NoctuaWebPaymentService(config.WebPaymentBaseUrl);
            
            _uiFactory = uiFactory;

#if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.OnProductDetailsDone += HandleGoogleProductDetails;
            GoogleBillingInstance?.Init();
#endif
            _nativePlugin = nativePlugin;
            
            _retryPendingPurchasesTask = UniTask.Create(RetryPendingPurchases);
        }
        
        public bool IsReady
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                return GoogleBillingInstance.IsReady;
#else
                return true;
#endif
            }
        }
        
        public void Init()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.Init();
#endif
        }

        public void SetEnabledPaymentTypes(List<PaymentType> enabledPaymentTypes)
        {
            // The sequence represent the priority.
            _enabledPaymentTypes = enabledPaymentTypes;
        }

        public async UniTask<ProductList> GetProductListAsync()
        {
            _log.Log("NoctuaIAPService.GetProductListAsync");

            var recentAccount = Noctua.Auth.GetRecentAccount();

            if (recentAccount?.Player?.GameId == null || recentAccount.Player.GameId <= 0)
            {
                throw new Exception("Game ID not found or invalid. Please authenticate first");
            }

            string gameId = recentAccount.Player.GameId.ToString();
            string currency = Noctua.Platform.Locale.GetCurrency();
            string enabledPaymentTypes = string.Join(",", _enabledPaymentTypes).ToLower();

            _log.Log("NoctuaIAPService.GetProductListAsync");
            _log.Log(_config.BaseUrl);
            _log.Log(_config.ClientId);
            _log.Log(gameId);
            _log.Log(currency);
            _log.Log(enabledPaymentTypes);

            var url =
                $"{_config.BaseUrl}/products?game_id={gameId}&currency={currency}&enabled_payment_types={enabledPaymentTypes}";

            _log.Log(url);

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

        private async UniTask<VerifyOrderResponse> VerifyOrderAsync(VerifyOrderRequest order, string accessToken)
        {
            var url = $"{_config.BaseUrl}/verify-order";

            var request = new HttpRequest(HttpMethod.Post, url)
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + accessToken)
                .WithJsonBody(order);

            var response = await request.Send<VerifyOrderResponse>();

            return response;
        }

        public async UniTask<string> GetActiveCurrencyAsync(string productId)
        {
#if UNITY_IOS && !UNITY_EDITOR
            var tcs = new TaskCompletionSource<string>();
            _nativePlugin.GetActiveCurrency(productId, (success, currency) => {
                _log.Log("NoctuaIAPService.GetActiveCurrency callback");
                _log.Log("NoctuaIAPService.GetActiveCurrency callback success: " + success);
                _log.Log("NoctuaIAPService.GetActiveCurrency callback currency: " + currency);
                if (!success) {
                    _log.Log("NoctuaIAPService.GetActiveCurrency callback currency: " + currency);
                    tcs.TrySetException(NoctuaException.ActiveCurrencyFailure);
                    return;  
                }
                tcs.TrySetResult(currency);
            });

            var activeCurrency = await tcs.Task;
            tcs.TrySetCanceled();

            return activeCurrency;

#elif UNITY_ANDROID && !UNITY_EDITOR
            _log.Log("GetActiveCurrencyAsync: Android");
            _activeCurrencyTcs = new TaskCompletionSource<string>();
            GoogleBillingInstance.QueryProductDetails(productId);

            var activeCurrency = await _activeCurrencyTcs.Task;
            _activeCurrencyTcs.TrySetCanceled();
            _activeCurrencyTcs = null;

            return activeCurrency;

#else // TODO for Other platforms

            _log.Log("GetActiveCurrencyAsync: not found, default to IDR");

            return "IDR";

#endif
        }

        public async UniTask<PurchaseResponse> PurchaseItemAsync(PurchaseRequest purchaseRequest)
        {
            _log.Log("started");

            if (!_accessTokenProvider.IsAuthenticated)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Purchase requires user authentication");
            }

            _uiFactory.ShowLoadingProgress(true);
            
            var paymentType = await GetPaymentTypeAsync();

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
                orderRequest.Currency = Noctua.Platform.Locale.GetCurrency();
            }

            try
            {
                var playerData = new PlayerAccountData
                {
                    IngameUsername = "Player",
                    IngameServerId = purchaseRequest.ServerId,
                    IngameRoleId = purchaseRequest.RoleId,
                    Extra = new Dictionary<string, string>
                    {
                        { "", "" },
                    }
                };

                await Noctua.Auth.UpdatePlayerAccountAsync(playerData);
                Debug.Log($"Player account updated successfully");
            }
            catch (Exception e)
            {
                if (e is NoctuaException exception)
                {
                    Debug.Log("NoctuaException: " + exception.ErrorCode + " : " + exception.Message);
                }
                else
                {
                    Debug.Log("Exception: " + e);
                }
            }

            OrderResponse orderResponse;

            try
            {
                _log.Log("NoctuaIAPService.PurchaseItemAsync try to CreateOrderAsync");

                orderResponse = await CreateOrderAsync(orderRequest);
                
                _eventSender?.Send(
                    "purchase_opened",
                    new()
                    {
                        { "product_id", orderResponse.ProductId },
                        { "amount", orderRequest.Price },
                        { "currency", orderRequest.Currency },
                        { "order_id", orderResponse.Id }
                    }
                );

                _uiFactory.ShowLoadingProgress(false);
            }
            catch (Exception e)
            {
                _log.Log("NoctuaIAPService.PurchaseItemAsync CreateOrderAsync failed");

                if (e is NoctuaException exception)
                {
                    _log.Log("NoctuaException: " + exception.ErrorCode + " : " + exception.Message);
                    _uiFactory.ShowLoadingProgress(false);
                    _uiFactory.ShowGeneralNotification(exception.ErrorCode + " : " + exception.Message);
                }
                else
                {
                    _log.Log("Exception: " + e);
                    _uiFactory.ShowLoadingProgress(false);
                    _uiFactory.ShowGeneralNotification(e.Message);
                }

                throw;
            }

            _log.Log("NoctuaIAPService.PurchaseItemAsync _currentOrderId: "         + orderResponse.Id);
            _log.Log("NoctuaIAPService.PurchaseItemAsync orderResponse.ProductId: " + orderResponse.ProductId);

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(300));
            var paymentTcs = new TaskCompletionSource<PaymentResult>();
            bool hasResult;
            PaymentResult paymentResult;

            switch (paymentType)
            {
                case PaymentType.Applestore:
#if UNITY_IOS && !UNITY_EDITOR
                    _log.Log("NoctuaIAPService.PurchaseItemAsync purchase on ios: " + orderResponse.ProductId);
                    orderResponse.ProductId = purchaseRequest.ProductId;
                    _nativePlugin.PurchaseItem(orderResponse.ProductId, (success, message) => {
                        _log.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback");
                        _log.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback success: " + success);
                        _log.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback message: " + message);
                        
                        paymentTcs.TrySetResult(GetAppstorePaymentResult(orderResponse.Id, success, message));
                    });

                    var task = await Task.WhenAny(paymentTcs.Task, timeoutTask);
                    
                    hasResult = task == paymentTcs.Task;
                    paymentResult = paymentTcs.Task.Result;
                    
                    _log.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback response: " + paymentResult);
                    break;
#else
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Applestore payment is not supported on this platform");
#endif
                case PaymentType.Playstore:
#if UNITY_ANDROID && !UNITY_EDITOR
                    _log.Log("NoctuaIAPService.PurchaseItemAsync purchase on playstore: " + orderResponse.ProductId);
                    
                    void PurchaseDone(GoogleBilling.PurchaseResult result)
                    {
                        _log.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback");
                        
                        paymentTcs.TrySetResult(GetPlaystorePaymentResult(result));
                    }
                    
                    GoogleBillingInstance.OnPurchaseDone += PurchaseDone;
                    GoogleBillingInstance.PurchaseItem(orderResponse.ProductId);

                    var task = await Task.WhenAny(paymentTcs.Task, timeoutTask);
                    
                    hasResult = task == paymentTcs.Task;
                    paymentResult = paymentTcs.Task.Result;

                    GoogleBillingInstance.OnPurchaseDone -= PurchaseDone;
                    
                    _log.Log("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback response: " + paymentResult);
                    break;
#else
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Playstore payment is not supported on this platform");
#endif

                case PaymentType.Noctuawallet:
                    hasResult = true;
                    paymentResult = await _noctuaPayment.PayAsync(orderResponse.PaymentUrl);

                    var getReceipt = Utility.ParseQueryString(orderResponse.PaymentUrl);
                    paymentResult.ReceiptData = getReceipt["receiptId"];
                    
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
            
            if (paymentResult.Status == PaymentStatus.Canceled)
            {
                _eventSender?.Send(
                    "purchase_cancelled",
                    new()
                    {
                        { "product_id", orderResponse.ProductId },
                        { "amount", orderRequest.Price },
                        { "currency", orderRequest.Currency },
                        { "order_id", orderResponse.Id }
                    }
                );
            }

            if (paymentResult.Status is not (PaymentStatus.Successful or PaymentStatus.Confirmed))
            {
                throw new NoctuaException(NoctuaErrorCode.Payment, $"OrderStatus: {paymentResult.Status}, Message: {paymentResult.Message}");
            }

            var orderId = orderResponse.Id;
            _log.Log($"Purchase was successful. Verifying order ID: {orderId}");
            var verifyOrderRequest = new VerifyOrderRequest
            {
                Id = orderId,
                ReceiptData = paymentResult.ReceiptData
            };

            _log.Log($"Verifying order: {verifyOrderRequest.Id} with receipt data: {verifyOrderRequest.ReceiptData}");
            
            VerifyOrderResponse verifyOrderResponse;

            try {
                _uiFactory.ShowLoadingProgress(true);

                verifyOrderResponse = await VerifyOrderAsync(verifyOrderRequest, _accessTokenProvider.AccessToken);
                
                switch (verifyOrderResponse.Status)
                {
                case OrderStatus.Completed:
                    _eventSender?.Send(
                        "purchase_completed",
                        new()
                        {
                            { "product_id", orderResponse.ProductId },
                            { "amount", orderRequest.Price },
                            { "currency", orderRequest.Currency },
                            { "order_id", verifyOrderResponse.Id }
                        }
                    );
                    _nativePlugin?.TrackPurchase(
                        verifyOrderResponse.Id.ToString(),
                        (double)orderRequest.Price,
                        orderRequest.Currency
                    );
                    break;
                case OrderStatus.Canceled:
                    _eventSender?.Send(
                        "purchase_cancelled",
                        new()
                        {
                            { "product_id", orderResponse.ProductId },
                            { "amount", orderRequest.Price },
                            { "currency", orderRequest.Currency },
                            { "order_id", verifyOrderResponse.Id }
                        }
                    );
                    break;
                }

                if (verifyOrderResponse.Status == OrderStatus.Pending)
                {
                    _waitingPendingPurchases.Enqueue(
                        new RetryPendingPurchaseItem
                        {
                            VerifyOrder = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken
                        }
                    );
                }

                _uiFactory.ShowLoadingProgress(false);
            }
            catch (NoctuaException e)
            {
                if ((NoctuaErrorCode)e.ErrorCode == NoctuaErrorCode.Networking)
                {
                    _waitingPendingPurchases.Enqueue(
                        new RetryPendingPurchaseItem
                        {
                            Order = orderRequest,
                            VerifyOrder = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken
                        }
                    );
                }
                
                _log.Log("NoctuaException: " + e.ErrorCode + " : " + e.Message);
                _uiFactory.ShowLoadingProgress(false);
                _uiFactory.ShowGeneralNotification(e.ErrorCode + " : " + e.Message);

                throw;
            }
            catch (Exception e) 
            {
                if (e is NoctuaException noctuaEx)
                {
                    _log.Log("NoctuaException: " + noctuaEx.ErrorCode + " : " + noctuaEx.Message);
                    _uiFactory.ShowLoadingProgress(false);
                    _uiFactory.ShowGeneralNotification(noctuaEx.ErrorCode + " : " + noctuaEx.Message);
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

        private async UniTask<PaymentSettings> GetPaymentSettingsAsync()
        {
            var request = new HttpRequest(HttpMethod.Get, $"{_config.BaseUrl}/user/profile")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken);

            return await request.Send<PaymentSettings>();
        }
        
        private async UniTask<PaymentType> GetPaymentTypeAsync()
        {
            var paymentSettings = await GetPaymentSettingsAsync();
            _log.Log("Payment Type From Settings: " + paymentSettings.PaymentType);
            _log.Log("Enabled Payment Types: " + string.Join(", ", _enabledPaymentTypes));

            if (string.IsNullOrEmpty(paymentSettings.PaymentType.ToString()) ||
                paymentSettings.PaymentType.ToString() == "unknown")
            {
                if (_enabledPaymentTypes.Count == 0)
                {
                    throw new NoctuaException(NoctuaErrorCode.Payment, "No payment types are enabled");
                }

                _log.Log("Payment Type From Settings is empty, fallback to server remote config");
                paymentSettings.PaymentType = _enabledPaymentTypes.First();
            }
            else
            {
                if (!_enabledPaymentTypes.Contains(paymentSettings.PaymentType))
                {
                    _log.Log(
                        "Payment Type From Settings is not enabled from server side, fallback to server remote config"
                    );

                    // Fallback to _enabledPaymentTypes from SDK init
                    paymentSettings.PaymentType = _enabledPaymentTypes.First();
                }
            }
            
            _log.Log("Selected payment Type: " + paymentSettings.PaymentType);
            return paymentSettings.PaymentType;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void HandleGoogleProductDetails(GoogleBilling.ProductDetailsResponse response)
        {
            _log.Log("NoctuaIAPService.HandleGoogleProductDetails");
            _log.Log("NoctuaIAPService.HandleGoogleProductDetails currency: " + response.Currency);
            if (_activeCurrencyTcs != null) {
                _activeCurrencyTcs.TrySetResult(response.Currency);
            } else {
                throw NoctuaException.MissingCompletionHandler;
            }
        }

        private PaymentResult GetPlaystorePaymentResult(GoogleBilling.PurchaseResult result)
        {
            _log.Log("Noctua.HandleGooglePurchaseDone");
            
            if (result == null || !result.Success)
            {
                _log.Log("Noctua.HandleGooglePurchaseDone result.Message: " + result.Message);
                if (string.IsNullOrEmpty(result.Message)) { // Empty message means canceled
                    _log.Error("Purchase canceled: empty message means canceled");

                    return new PaymentResult
                    {
                        Status = PaymentStatus.Canceled,
                        Message = "Purchase canceled"
                    };
                }

                _log.Error("Purchase failed: " + result.Message);
                
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
            _log.Log("Noctua.HandleIosPurchaseDone");
            _log.Log("Noctua.HandleIosPurchaseDone orderId: " + orderId);
            _log.Log("Noctua.HandleIosPurchaseDone success: " + success);
            _log.Log("Noctua.HandleIosPurchaseDone message: " + message);

            if (!success)
            {
                // Check if message contains cancel keyword
                if (message.Contains("cancel")) {
                    _log.Error("Purchase canceled: ");

                    return new PaymentResult
                    {
                        Status = PaymentStatus.Canceled,
                        Message = "Purchase canceled"
                    };
                }

                _log.Error("Purchase failed: " + message);
                
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

        private void SavePendingPurchases(List<RetryPendingPurchaseItem> orders)
        {
            var updatedJson = JsonConvert.SerializeObject(orders);

            PlayerPrefs.SetString("NoctuaPendingPurchases", updatedJson);
            PlayerPrefs.Save();
        }

        private List<RetryPendingPurchaseItem> GetPendingPurchases()
        {
            _log.Log("Noctua.GetPendingPurchases");
            var json = PlayerPrefs.GetString("NoctuaPendingPurchases", string.Empty);

            if (string.IsNullOrEmpty(json))
            {
                return new List<RetryPendingPurchaseItem>();
            }

            try
            {
                var pendingPurchases = JsonConvert.DeserializeObject<List<RetryPendingPurchaseItem>>(json);
                
                return pendingPurchases
                    .Where(p => p.Order != null && p.VerifyOrder != null && p.AccessToken != null)
                    .ToList();
            }
            catch (Exception e)
            {
                _log.Error("Failed to parse pending purchases: " + e);
                
                return new List<RetryPendingPurchaseItem>();
            }
        }

        private async UniTask RetryPendingPurchases()
        {
            _log.Log("Starting pending purchases retry loop");
            
            var random = new Random();
            
            var runningPendingPurchases = GetPendingPurchases().ToList();
            CancellationTokenSource cts = new();
            var quitting = false;

            Application.quitting += () =>
            {
                quitting = true;
                cts.Cancel();
            };

            var retryCount = 0;

            while (!quitting)
            {
                // No pending purchases, just wait for new ones
                if (runningPendingPurchases.Count == 0)
                {
                    await UniTask.Delay(1000, cancellationToken: cts.Token);
                    
                    continue;
                }
                
                _log.Log("Retrying pending purchases: " + runningPendingPurchases.Count);
                
                // Drain the queue
                var newPendingPurchaseCount = 0;
                while (_waitingPendingPurchases.TryDequeue(out var pendingPurchase))
                {
                    runningPendingPurchases.Add(pendingPurchase);
                    newPendingPurchaseCount++;
                    
                    _log.Log("Draining pending purchase: " + pendingPurchase.VerifyOrder.Id);
                }
                
                // Retry pending purchases
                var failedPendingPurchases = new List<RetryPendingPurchaseItem>();
                
                foreach (var item in runningPendingPurchases)
                {
                    try
                    {
                        _log.Log(
                            $"Retrying Order ID: {item.VerifyOrder.Id}, " +
                            $"Receipt Data: {item.VerifyOrder.ReceiptData}"
                        );

                        var verifyOrderResponse = await VerifyOrderAsync(item.VerifyOrder, item.AccessToken);

                        switch (verifyOrderResponse.Status)
                        {
                        case OrderStatus.Completed:
                            _eventSender?.Send(
                                "purchase_completed",
                                new()
                                {
                                    { "product_id", item.Order.ProductId },
                                    { "amount", item.Order.Price },
                                    { "currency", item.Order.Currency },
                                    { "order_id", verifyOrderResponse.Id }
                                }
                            );

                            _nativePlugin?.TrackPurchase(
                                verifyOrderResponse.Id.ToString(),
                                (double)item.Order.Price,
                                item.Order.Currency
                            );

                            break;

                        case OrderStatus.Canceled:
                            _eventSender?.Send(
                                "purchase_cancelled",
                                new()
                                {
                                    { "product_id", item.Order.ProductId },
                                    { "amount", item.Order.Price },
                                    { "currency", item.Order.Currency },
                                    { "order_id", verifyOrderResponse.Id }
                                }
                            );

                            break;
                        }

                        if (verifyOrderResponse.Status == OrderStatus.Pending)
                        {
                            failedPendingPurchases.Add(item);
                        
                            _log.Log("Adding pending purchase back to queue: " + item.VerifyOrder.Id);
                        }
                    }
                    catch (NoctuaException e)
                    {
                        if ((NoctuaErrorCode)e.ErrorCode == NoctuaErrorCode.Networking)
                        {
                            failedPendingPurchases.Add(item);
                        
                            _log.Log("Adding pending purchase back to queue: " + item.VerifyOrder.Id);
                        }

                        _log.Error("NoctuaException: " + e.ErrorCode + " : " + e.Message);
                    }
                    catch (Exception e)
                    {
                        _log.Error("Exception: " + e);
                    }
                }
                
                // Save if running pending purchases changed
                if (newPendingPurchaseCount > 0 || runningPendingPurchases.Count != failedPendingPurchases.Count)
                {
                    _log.Log("Saving pending purchases: " + runningPendingPurchases.Count);
                    
                    SavePendingPurchases(runningPendingPurchases);
                }
                
                runningPendingPurchases = failedPendingPurchases;
                
                // No need to retry, just straight to next iteration waiting for new pending purchases
                if (runningPendingPurchases.Count == 0)
                {
                    retryCount = 0;
                    continue;
                }

                // Exponential backoff with randomization, so we don't hammer the server
                retryCount++;
                var delay = GetBackoffDelay(random, retryCount);
                _log.Log($"Retrying in {delay.TotalSeconds} seconds...");

                try
                {
                    await Task.Delay(delay, cancellationToken: cts.Token);
                }
                catch (Exception e)
                {
                    _log.Log("Operation canceled: " + e.Message);
                    break;
                }
            }
            
            _log.Log("Quitting, saving pending purchases: " + runningPendingPurchases.Count);
            
            SavePendingPurchases(_waitingPendingPurchases.ToList());
        }

        private TimeSpan GetBackoffDelay(Random random, int retryCount)
        {
            var baseDelay = TimeSpan.FromSeconds(5); // Base delay of 1 second
            var maxDelay = TimeSpan.FromHours(3); // Maximum delay of 1 hour
            var randomFactor = (random.NextDouble() * 0.5) + 0.75; // Random factor between 0.75 and 1.25

            var delay = TimeSpan.FromMinutes(baseDelay.TotalMinutes * Math.Pow(2, retryCount - 1) * randomFactor);
            return delay > maxDelay ? maxDelay : delay;
        }

        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
            public string WebPaymentBaseUrl;
        }
        
        private class RetryPendingPurchaseItem
        {
            public OrderRequest Order;
            public VerifyOrderRequest VerifyOrder;
            public string AccessToken;
        }
    }
}