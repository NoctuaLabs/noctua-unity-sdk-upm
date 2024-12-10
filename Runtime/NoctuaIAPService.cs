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

        [JsonProperty("display_price")]
        public string DisplayPrice;

        [JsonProperty("price_in_usd")]
        public string PriceInUsd;

        [JsonProperty("platform")]
        public string Platform;
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

        [JsonProperty("extra")]
        public Dictionary<string, string> Extra;
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

    [Preserve]
    public enum OrderStatus
    {
        unknown,
        pending,
        completed,
        failed,
        verification_failed,
        delivery_callback_failed,
        error,
        refunded,
        canceled,
        expired,
        invalid
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

        [JsonProperty("extra")]
        public Dictionary<string, string> Extra;
        
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
        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaIAPService));

        private TaskCompletionSource<string> _activeCurrencyTcs;

        private readonly EventSender _eventSender;
        private readonly AccessTokenProvider _accessTokenProvider;
        private readonly NoctuaWebPaymentService _noctuaPayment;
        private readonly Queue<RetryPendingPurchaseItem> _waitingPendingPurchases = new();
        private readonly UniTask _retryPendingPurchasesTask;
        private readonly INativePlugin _nativePlugin;
        private readonly ProductList _usdProducts = new();

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly GoogleBilling GoogleBillingInstance = new();
        // By default all major payment types are enabled, then it will be overridden by the server config at SDK init
        private List<PaymentType> _enabledPaymentTypes = new()
            { PaymentType.playstore, PaymentType.noctuastore };
#elif UNITY_IOS && !UNITY_EDITOR
        // By default all major payment types are enabled, then it will be overridden by the server config at SDK init
        private List<PaymentType> _enabledPaymentTypes = new()
            { PaymentType.appstore, PaymentType.noctuastore };
#else
        private List<PaymentType> _enabledPaymentTypes = new()
            { PaymentType.noctuastore };
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

        public async UniTask<ProductList> GetProductListAsync(string currency = null, string platformType = null)
        {
            var recentAccount = Noctua.Auth.GetRecentAccount();

            if (recentAccount?.Player?.GameId == null || recentAccount.Player.GameId <= 0)
            {
                throw new Exception("Game ID not found or invalid. Please authenticate first");
            }

            string gameId = recentAccount.Player.GameId.ToString();

            if (string.IsNullOrEmpty(currency))
            {
                currency = Noctua.Platform.Locale.GetCurrency();
            }
            
            string enabledPaymentTypes = string.Join(",", _enabledPaymentTypes).ToLower();

            _log.Debug(_config.BaseUrl);
            _log.Debug(_config.ClientId);
            _log.Debug(gameId);
            _log.Debug(currency);
            _log.Debug(enabledPaymentTypes);

            if (string.IsNullOrEmpty(platformType))
            {
#if UNITY_ANDROID
                platformType = "playstore";

                // TODO Handle more platforms
                // - "direct" platformType for Direct distribution.
#elif UNITY_IOS
                platformType = "appstore";
#else
                platformType = "unknown";
#endif
            }

            var url =
                $"{_config.BaseUrl}/products" +
                $"?game_id={gameId}" +
                $"&currency={currency}" +
                $"&enabled_payment_types={enabledPaymentTypes}" +
                $"&platform={platformType}";

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
                _log.Info("NoctuaIAPService.GetActiveCurrency callback");
                _log.Info("NoctuaIAPService.GetActiveCurrency callback success: " + success);
                _log.Info("NoctuaIAPService.GetActiveCurrency callback currency: " + currency);
                if (!success) {
                    _log.Info("NoctuaIAPService.GetActiveCurrency callback currency: " + currency);
                    tcs.TrySetException(NoctuaException.ActiveCurrencyFailure);
                    return;  
                }
                tcs.TrySetResult(currency);
            });

            var activeCurrency = await tcs.Task;
            tcs.TrySetCanceled();

            return activeCurrency;

#elif UNITY_ANDROID && !UNITY_EDITOR
            _log.Info("GetActiveCurrencyAsync: Android");
            _activeCurrencyTcs = new TaskCompletionSource<string>();
            GoogleBillingInstance.QueryProductDetails(productId);

            var activeCurrency = await _activeCurrencyTcs.Task;
            _activeCurrencyTcs.TrySetCanceled();
            _activeCurrencyTcs = null;

            return activeCurrency;

#else // TODO for Other platforms

            _log.Info("GetActiveCurrencyAsync: not found, return empty string");

            return "";

#endif
        }

        public async UniTask<PurchaseResponse> PurchaseItemAsync(PurchaseRequest purchaseRequest)
        {
            _log.Info("started");

            if (!_accessTokenProvider.IsAuthenticated)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Purchase requires user authentication");
            }

            _uiFactory.ShowLoadingProgress(true);
            
            PaymentType paymentType;
            OrderRequest orderRequest;
            OrderResponse orderResponse;
            double price;
            string currency;

            try
            {
                paymentType = await GetPaymentTypeAsync();
                
                if (_usdProducts.Count == 0)
                {
                    _usdProducts.AddRange(await GetProductListAsync(currency: "USD"));
                }
                
                var playerData = new PlayerAccountData
                {
                    IngameUsername = "Player",
                    IngameServerId = purchaseRequest.ServerId,
                    IngameRoleId = purchaseRequest.RoleId,
                    Extra = (purchaseRequest.Extra != null && purchaseRequest.Extra.Count > 0) 
                    ? purchaseRequest.Extra 
                    : new Dictionary<string, string> {{ "", "" }}

                };

                await Noctua.Auth.UpdatePlayerAccountAsync(playerData);
                
                _log.Info($"Player account updated successfully");
                
                orderRequest = new OrderRequest
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

		if (purchaseRequest.Extra != null && purchaseRequest.Extra.Count > 0)
		{
		    foreach (var kvp in purchaseRequest.Extra)
		    {
		        orderRequest.Extra.Add(kvp.Key, kvp.Value);
		    }
		}

                if (string.IsNullOrEmpty(orderRequest.Currency))
                {
                    orderRequest.Currency = Noctua.Platform.Locale.GetCurrency();
                }
                
                var product = _usdProducts.FirstOrDefault(p => p.Id == orderRequest.ProductId);
            
                if (product == null)
                {
                    _log.Warning("Product not found in product list");
                
                    price = (double)orderRequest.Price;
                    currency = orderRequest.Currency;
                }
                else
                {
                    price = product.Price;
                    currency = product.Currency;
                }
                
                _log.Debug("creating order");

                orderResponse = await CreateOrderAsync(orderRequest);
                
                _eventSender?.Send(
                    "purchase_opened",
                    new()
                    {
                        { "product_id", orderResponse.ProductId },
                        { "amount", price },
                        { "currency", currency },
                        { "order_id", orderResponse.Id },
                        { "orig_amount", orderRequest.Price },
                        { "orig_currency", orderRequest.Currency }
                    }
                );

                _log.Debug("_currentOrderId: "         + orderResponse.Id);
                _log.Debug("orderResponse.ProductId: " + orderResponse.ProductId);

                _uiFactory.ShowLoadingProgress(false);
            }
            catch (Exception e)
            {
                _uiFactory.ShowError(e.Message);
                _log.Exception(e);
                _uiFactory.ShowLoadingProgress(false);
                _uiFactory.ShowError(e.Message);

                throw;
            }

            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(300));
            var paymentTcs = new TaskCompletionSource<PaymentResult>();
            bool hasResult;
            PaymentResult paymentResult;

            switch (paymentType)
            {
                case PaymentType.appstore:
#if UNITY_IOS && !UNITY_EDITOR
                    _log.Info("NoctuaIAPService.PurchaseItemAsync purchase on ios: " + orderResponse.ProductId);
                    orderResponse.ProductId = purchaseRequest.ProductId;
                    _nativePlugin.PurchaseItem(orderResponse.ProductId, (success, message) => {
                        _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback");
                        _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback success: " + success);
                        _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback message: " + message);
                        
                        paymentTcs.TrySetResult(GetAppstorePaymentResult(orderResponse.Id, success, message));
                    });

                    var task = await Task.WhenAny(paymentTcs.Task, timeoutTask);
                    
                    hasResult = task == paymentTcs.Task;
                    paymentResult = paymentTcs.Task.Result;
                    
                    _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback response: " + paymentResult);
                    break;
#else
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Applestore payment is not supported on this platform");
#endif
                case PaymentType.playstore:
#if UNITY_ANDROID && !UNITY_EDITOR
                    _log.Info("NoctuaIAPService.PurchaseItemAsync purchase on playstore: " + orderResponse.ProductId);
                    
                    void PurchaseDone(GoogleBilling.PurchaseResult result)
                    {
                        _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback");
                        
                        paymentTcs.TrySetResult(GetPlaystorePaymentResult(result));
                    }
                    
                    GoogleBillingInstance.OnPurchaseDone += PurchaseDone;
                    GoogleBillingInstance.PurchaseItem(orderResponse.ProductId);

                    var task = await Task.WhenAny(paymentTcs.Task, timeoutTask);
                    
                    hasResult = task == paymentTcs.Task;
                    paymentResult = paymentTcs.Task.Result;

                    GoogleBillingInstance.OnPurchaseDone -= PurchaseDone;
                    
                    _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback response: " + paymentResult);
                    break;
#else
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Playstore payment is not supported on this platform");
#endif

                case PaymentType.noctuastore:
                    hasResult = true;
                    paymentResult = await _noctuaPayment.PayAsync(orderResponse.PaymentUrl);

                    var getReceipt = Utility.ParseQueryString(orderResponse.PaymentUrl);
                    paymentResult.ReceiptData = getReceipt["receiptId"];
                    
                    break;
                case PaymentType.unknown:
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
                        { "amount", price },
                        { "currency", currency },
                        { "order_id", orderResponse.Id },
                        { "orig_amount", orderRequest.Price },
                        { "orig_currency", orderRequest.Currency }
                    }
                );
            }

            if (paymentResult.Status is not (PaymentStatus.Successful or PaymentStatus.Confirmed))
            {
                _uiFactory.ShowError(paymentResult.Message);
                
                throw new NoctuaException(NoctuaErrorCode.Payment, $"OrderStatus: {paymentResult.Status}, Message: {paymentResult.Message}");
            }

            var orderId = orderResponse.Id;
            _log.Info($"Purchase was successful. Verifying order ID: {orderId}");
            var verifyOrderRequest = new VerifyOrderRequest
            {
                Id = orderId,
                ReceiptData = paymentResult.ReceiptData
            };

            _log.Info($"Verifying order: {verifyOrderRequest.Id} with receipt data: {verifyOrderRequest.ReceiptData}");
            
            VerifyOrderResponse verifyOrderResponse;

            try {
                _uiFactory.ShowLoadingProgress(true);

                verifyOrderResponse = await VerifyOrderAsync(verifyOrderRequest, _accessTokenProvider.AccessToken);

                switch (verifyOrderResponse.Status)
                {
                case OrderStatus.completed:
                    _eventSender?.Send(
                        "purchase_completed",
                        new()
                        {
                            { "product_id", orderResponse.ProductId },
                            { "amount", price },
                            { "currency", currency },
                            { "order_id", verifyOrderResponse.Id },
                            { "orig_amount", orderRequest.Price },
                            { "orig_currency", orderRequest.Currency }
                        }
                    );
                    _nativePlugin?.TrackPurchase(
                        verifyOrderResponse.Id.ToString(),
                        (double)orderRequest.Price,
                        orderRequest.Currency
                    );
                    break;
                case OrderStatus.canceled:
                    _eventSender?.Send(
                        "purchase_cancelled",
                        new()
                        {
                            { "product_id", orderResponse.ProductId },
                            { "amount", price },
                            { "currency", currency },
                            { "order_id", verifyOrderResponse.Id },
                            { "orig_amount", orderRequest.Price },
                            { "orig_currency", orderRequest.Currency }
                        }
                    );
                    break;
                }

                if (verifyOrderResponse.Status == OrderStatus.pending)
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
                
                _uiFactory.ShowLoadingProgress(false);
                _log.Exception(e);
                _uiFactory.ShowError(e.ErrorCode + " : " + e.Message);

                throw;
            }
            catch (Exception e) 
            {
                _uiFactory.ShowLoadingProgress(false);
                _log.Exception(e);
                _uiFactory.ShowError(e.Message);
                
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
            _log.Info("Payment Type From Settings: " + paymentSettings.PaymentType);
            _log.Info("Enabled Payment Types: " + string.Join(", ", _enabledPaymentTypes));

            if (string.IsNullOrEmpty(paymentSettings.PaymentType.ToString()) ||
                paymentSettings.PaymentType.ToString() == "unknown")
            {
                if (_enabledPaymentTypes.Count == 0)
                {
                    throw new NoctuaException(NoctuaErrorCode.Payment, "No payment types are enabled");
                }

                _log.Info("Payment Type From Settings is empty, fallback to server remote config");
                paymentSettings.PaymentType = _enabledPaymentTypes.First();
            }
            else
            {
                if (!_enabledPaymentTypes.Contains(paymentSettings.PaymentType))
                {
                    _log.Info(
                        "Payment Type From Settings is not enabled from server side, fallback to server remote config"
                    );

                    // Fallback to _enabledPaymentTypes from SDK init
                    paymentSettings.PaymentType = _enabledPaymentTypes.First();
                }
            }
            
            _log.Info("Selected payment Type: " + paymentSettings.PaymentType);
            return paymentSettings.PaymentType;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void HandleGoogleProductDetails(GoogleBilling.ProductDetailsResponse response)
        {
            _log.Info("NoctuaIAPService.HandleGoogleProductDetails");

            if (_activeCurrencyTcs == null)
            {
                throw NoctuaException.MissingCompletionHandler;
            }
            
            if (response is null)
            {
                _activeCurrencyTcs.TrySetException(NoctuaException.ActiveCurrencyFailure);
            
                return;
            }

            _log.Info("NoctuaIAPService.HandleGoogleProductDetails currency: " + response.Currency);

            _activeCurrencyTcs.TrySetResult(response.Currency);
        }

        private PaymentResult GetPlaystorePaymentResult(GoogleBilling.PurchaseResult result)
        {
            _log.Info("Noctua.HandleGooglePurchaseDone");
            
            if (result == null || !result.Success)
            {
                _log.Info("Noctua.HandleGooglePurchaseDone result.Message: " + result.Message);
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
            _log.Info("Noctua.HandleIosPurchaseDone");
            _log.Info("Noctua.HandleIosPurchaseDone orderId: " + orderId);
            _log.Info("Noctua.HandleIosPurchaseDone success: " + success);
            _log.Info("Noctua.HandleIosPurchaseDone message: " + message);

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
            _log.Info("Noctua.GetPendingPurchases");
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
            _log.Info("Starting pending purchases retry loop");
            
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
            
            if (_usdProducts.Count == 0)
            {
                var paymentType = await GetPaymentTypeAsync();
                await GetProductListAsync(platformType: paymentType.ToString().ToLower());
            }

            while (!quitting)
            {
                // No pending purchases, just wait for new ones
                if (runningPendingPurchases.Count == 0)
                {
                    await UniTask.Delay(1000, cancellationToken: cts.Token);
                    
                    continue;
                }
                
                _log.Info("Retrying pending purchases: " + runningPendingPurchases.Count);
                
                // Drain the queue
                var newPendingPurchaseCount = 0;
                while (_waitingPendingPurchases.TryDequeue(out var pendingPurchase))
                {
                    runningPendingPurchases.Add(pendingPurchase);
                    newPendingPurchaseCount++;
                    
                    _log.Info("Draining pending purchase: " + pendingPurchase.VerifyOrder.Id);
                }
                
                // Retry pending purchases
                var failedPendingPurchases = new List<RetryPendingPurchaseItem>();
                
                foreach (var item in runningPendingPurchases)
                {
                    try
                    {
                        _log.Info(
                            $"Retrying Order ID: {item.VerifyOrder.Id}, " +
                            $"Receipt Data: {item.VerifyOrder.ReceiptData}"
                        );

                        var verifyOrderResponse = await VerifyOrderAsync(item.VerifyOrder, item.AccessToken);
                        
                        var product = _usdProducts.FirstOrDefault(p => p.Id == item.Order.ProductId && p.Currency == "USD");
                        double price;
                        string currency;
            
                        if (product == null)
                        {
                            _log.Warning("Product not found in product list");
                
                            price = (double)item.Order.Price;
                            currency = item.Order.Currency;
                        }
                        else
                        {
                            price = product.Price;
                            currency = product.Currency;
                        }
                        
                        switch (verifyOrderResponse.Status)
                        {
                        case OrderStatus.completed:
                            _eventSender?.Send(
                                "purchase_completed",
                                new()
                                {
                                    { "product_id", item.Order.ProductId },
                                    { "amount", price },
                                    { "currency", currency },
                                    { "order_id", verifyOrderResponse.Id },
                                    { "orig_amount", item.Order.Price },
                                    { "orig_currency", item.Order.Currency }
                                }
                            );

                            _nativePlugin?.TrackPurchase(
                                verifyOrderResponse.Id.ToString(),
                                (double)item.Order.Price,
                                item.Order.Currency
                            );

                            break;

                        case OrderStatus.canceled:
                            _eventSender?.Send(
                                "purchase_cancelled",
                                new()
                                {
                                    { "product_id", item.Order.ProductId },
                                    { "amount", price },
                                    { "currency", currency },
                                    { "order_id", verifyOrderResponse.Id },
                                    { "orig_amount", item.Order.Price },
                                    { "orig_currency", item.Order.Currency }
                                }
                            );

                            break;
                        }

                        if (verifyOrderResponse.Status == OrderStatus.pending)
                        {
                            failedPendingPurchases.Add(item);
                        
                            _log.Info("Adding pending purchase back to queue: " + item.VerifyOrder.Id);
                        }
                    }
                    catch (NoctuaException e)
                    {
                        if ((NoctuaErrorCode)e.ErrorCode == NoctuaErrorCode.Networking)
                        {
                            failedPendingPurchases.Add(item);
                        
                            _log.Info("Adding pending purchase back to queue: " + item.VerifyOrder.Id);
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
                    _log.Info("Saving pending purchases: " + runningPendingPurchases.Count);
                    
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
                _log.Info($"Retrying in {delay.TotalSeconds} seconds...");

                try
                {
                    await Task.Delay(delay, cancellationToken: cts.Token);
                }
                catch (Exception e)
                {
                    _log.Info("Operation canceled: " + e.Message);
                    break;
                }
            }
            
            _log.Info("Quitting, saving pending purchases: " + runningPendingPurchases.Count);
            
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
