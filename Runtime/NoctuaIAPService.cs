using System;
using System.Collections.Concurrent;
using Cysharp.Threading.Tasks;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using com.noctuagames.sdk.Events;
using Newtonsoft.Json;
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

        [JsonProperty("enabled_payment_types")]
        public PaymentType[] EnabledPaymentTypes;

        [JsonProperty("price")]
        public decimal Price;

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
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("payment_type")] 
        public PaymentType PaymentType;

        [JsonProperty("product_id")]
        public string ProductId;

        [JsonProperty("price")]
        public decimal Price;

        [JsonProperty("currency")]
        public string Currency;

        [JsonProperty("price_in_usd")]
        public decimal PriceInUSD;

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

        [JsonProperty("timestamp")]
        public string Timestamp;
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
        Confirmed,
        ItemAlreadyOwned,
        Pending,
        InvalidPurchaseObject,
        PendingPurchaseOngoing
    }
    
    public class PaymentResult
    {
        public PaymentStatus Status;
        public string ReceiptId;
        public string ReceiptData;
        public string Message;
    }

    [Preserve]
    public class VerifyOrderRequest
    {
        [JsonProperty("id")]
        public int Id;

        [JsonProperty("receipt_id")]
        public string ReceiptId;

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
        invalid,
        voided
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
    public class NoctuaGoldData
    {
        [JsonProperty("vip_level")]
        public double VipLevel;

        [JsonProperty("gold_amount")]
        public double GoldAmount;

        [JsonProperty("bound_gold_amount")]
        public double BoundGoldAmount;
        
        [JsonProperty("total_gold_amount")]
        public double TotalGoldAmount; 
        
        [JsonProperty("eligible_gold_amount")]
        public double EligibleGoldAmount;
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
        private readonly INativePlugin _nativePlugin;
        private readonly ProductList _usdProducts = new();
        private readonly CustomPaymentCompleteDialogPresenter _customPaymentCompleteDialog;
        private readonly FailedPaymentDialogPresenter _failedPaymentDialog;

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
        private bool _enabled;

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
            _customPaymentCompleteDialog = _uiFactory.Create<CustomPaymentCompleteDialogPresenter, object>(new object());
            _failedPaymentDialog = _uiFactory.Create<FailedPaymentDialogPresenter, object>(new object());

#if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.OnProductDetailsDone += HandleGoogleProductDetails;
#endif
            _nativePlugin = nativePlugin;
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
            EnsureEnabled();
            
            _log.Debug("calling API");
            
            var recentAccount = Noctua.Auth.RecentAccount;

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

        private async UniTask<VerifyOrderResponse> VerifyOrderImplAsync(
            OrderRequest orderRequest,
            VerifyOrderRequest verifyOrderRequest,
            string token
        )
        {
                if (orderRequest.Id == 0)
                {
                    throw new NoctuaException(NoctuaErrorCode.Payment, $": Invalid order ID: 0");
                }

                var verifyOrderResponse = await VerifyOrderAsync(verifyOrderRequest, token);

                switch (verifyOrderResponse.Status)
                {
                case OrderStatus.completed:
                    _log.Debug("remove from pending queue because it has been completed");
                    RemoveFromRetryPendingPurchasesByOrderID(verifyOrderRequest.Id);

                    _eventSender?.Send(
                        "purchase_completed",
                        new()
                        {
                            { "product_id", orderRequest.ProductId },
                            { "amount", orderRequest.PriceInUSD },
                            { "currency", "USD" },
                            { "order_id", orderRequest.Id },
                            { "orig_amount", orderRequest.Price },
                            { "orig_currency", orderRequest.Currency }
                        }
                    );
                    _nativePlugin?.TrackPurchase(
                        verifyOrderRequest.Id.ToString(),
                        (double)orderRequest.Price,
                        orderRequest.Currency
                    );
                    break;
                case OrderStatus.canceled:
                    _eventSender?.Send(
                        "purchase_canceled",
                        new()
                        {
                            { "product_id", orderRequest.ProductId },
                            { "amount", orderRequest.PriceInUSD },
                            { "currency", "USD" },
                            { "order_id", orderRequest.Id },
                            { "orig_amount", orderRequest.Price },
                            { "orig_currency", orderRequest.Currency }
                        }
                    );

                    EnqueueToRetryPendingPurchases(
                        new RetryPendingPurchaseItem
                        {
                            OrderId = verifyOrderRequest.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "canceled",
                        }
                    );
                    break;
                case OrderStatus.refunded:
                    _eventSender?.Send(
                        "purchase_refunded",
                        new()
                        {
                            { "product_id", orderRequest.ProductId },
                            { "amount", orderRequest.PriceInUSD },
                            { "currency", "USD" },
                            { "order_id", orderRequest.Id },
                            { "orig_amount", orderRequest.Price },
                            { "orig_currency", orderRequest.Currency }
                        }
                    );
                    EnqueueToRetryPendingPurchases(
                        new RetryPendingPurchaseItem
                        {
                            OrderId = verifyOrderRequest.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "refunded",
                        }
                    );
                    break;
                case OrderStatus.voided:
                    _log.Debug("remove from pending queue because it has been voided");
                    RemoveFromRetryPendingPurchasesByOrderID(verifyOrderRequest.Id);

                    _eventSender?.Send(
                        "purchase_voided",
                        new()
                        {
                            { "product_id", orderRequest.ProductId },
                            { "amount", orderRequest.PriceInUSD },
                            { "currency", "USD" },
                            { "order_id", orderRequest.Id },
                            { "orig_amount", orderRequest.Price },
                            { "orig_currency", orderRequest.Currency }
                        }
                    );
                    break;
                }

                if (verifyOrderResponse.Status != OrderStatus.completed &&
                verifyOrderResponse.Status != OrderStatus.canceled &&
                verifyOrderResponse.Status != OrderStatus.refunded &&
                verifyOrderResponse.Status != OrderStatus.voided)
                {
                    _eventSender?.Send(
                        "purchase_verify_order_failed",
                        new()
                        {
                            { "product_id", orderRequest.ProductId },
                            { "amount", orderRequest.PriceInUSD },
                            { "currency", "USD" },
                            { "order_id", orderRequest.Id },
                            { "orig_amount", orderRequest.Price },
                            { "orig_currency", orderRequest.Currency }
                        }
                    );

                    EnqueueToRetryPendingPurchases(
                        new RetryPendingPurchaseItem
                        {
                            OrderId = verifyOrderRequest.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "verification_failed",
                        }
                    );

                    var message = Utility.GetTranslation("CustomPaymentCompleteDialogPresenter.OrderVerificationFailedMessage",  Utility.LoadTranslations(Noctua.Platform.Locale.GetLanguage()));
                    if (message == "" || message == "CustomPaymentCompleteDialogPresenter.OrderVerificationFailedMessage")
                    {
                        message = "Your payment couldn’t be verified. Please retry later.";
                    }

                    throw new NoctuaException(NoctuaErrorCode.Payment, $"{message} Status: {verifyOrderResponse.Status.ToString()}");
                }

                return verifyOrderResponse;
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

        public async UniTask<PurchaseResponse> PurchaseItemAsync(PurchaseRequest purchaseRequest, bool tryToUseSecondaryPayment = false)
        {
            EnsureEnabled();
            
            _log.Debug("calling API");

            if (!_accessTokenProvider.IsAuthenticated)
            {
                _uiFactory.ShowError(LocaleTextKey.IAPRequiresAuthentication);

                throw new NoctuaException(NoctuaErrorCode.Authentication, "Purchase requires user authentication");
            }
            
            if (_enabledPaymentTypes.Count == 0)
            {
                _uiFactory.ShowError(LocaleTextKey.IAPPaymentDisabled);

                throw new NoctuaException(NoctuaErrorCode.Payment, "no payment types enabled");
            }

            // The payment types are prioritized in backend
            // and filtered by runtime platform in InitAsync()
            var paymentType = _enabledPaymentTypes.First();
            if (tryToUseSecondaryPayment && _enabledPaymentTypes.Count > 1)
            {
                paymentType = _enabledPaymentTypes[1];
            }

            _uiFactory.ShowLoadingProgress(true);
            
            Product usdProduct;
            OrderRequest orderRequest;
            OrderResponse orderResponse;

            try
            {
                if (_usdProducts.Count == 0)
                {
                    _usdProducts.AddRange(await GetProductListAsync(currency: "USD"));
                }
                
                var playerData = new PlayerAccountData
                {
                    IngameServerId = purchaseRequest.ServerId,
                    IngameRoleId = purchaseRequest.RoleId,
                    Extra = (purchaseRequest.Extra != null && purchaseRequest.Extra.Count > 0) 
                    ? purchaseRequest.Extra 
                    : new Dictionary<string, string> {{ "", "" }}

                };

                await Noctua.Auth.UpdatePlayerAccountAsync(playerData);
                
                _log.Debug($"updated player role: '{playerData.IngameRoleId}', server: '{playerData.IngameServerId}'");
                
                orderRequest = new OrderRequest
                {
                    PaymentType = paymentType,
                    ProductId = purchaseRequest.ProductId,
                    Price = purchaseRequest.Price,
                    Currency = purchaseRequest.Currency,
                    RoleId = purchaseRequest.RoleId,
                    ServerId = purchaseRequest.ServerId,
                    IngameItemId = purchaseRequest.IngameItemId,
                    IngameItemName = purchaseRequest.IngameItemName,
                    Extra = purchaseRequest.Extra
                };

                if (string.IsNullOrEmpty(orderRequest.Currency))
                {
                    orderRequest.Currency = Noctua.Platform.Locale.GetCurrency();
                }
                
                usdProduct = _usdProducts.FirstOrDefault(p => p.Id == orderRequest.ProductId);
                
                if (usdProduct == null)
                {
                    throw new NoctuaException(NoctuaErrorCode.Payment, $"USD price not found for product '{orderRequest.ProductId}'");
                }

                orderRequest.PriceInUSD = usdProduct.Price;
                orderRequest.Timestamp = DateTime.Now.ToString();
            
                _log.Debug("creating order");

                orderResponse = await RetryAsync(() => CreateOrderAsync(orderRequest));

                orderRequest.Id = orderResponse.Id;
                
                _eventSender?.Send(
                    "purchase_opened",
                    new()
                    {
                        { "product_id", orderRequest.ProductId },
                        { "amount", orderRequest.PriceInUSD },
                        { "currency", "USD" },
                        { "order_id", orderRequest.Id },
                        { "orig_amount", orderRequest.Price },
                        { "orig_currency", orderRequest.Currency }
                    }
                );

                _log.Debug("orderResponse.Id: "         + orderResponse.Id);
                _log.Debug("orderResponse.ProductId: " + orderResponse.ProductId);

                _uiFactory.ShowLoadingProgress(false);
            }
            catch (Exception e)
            {
                _uiFactory.ShowError(e.Message);
                _log.Exception(e);
                _uiFactory.ShowLoadingProgress(false);

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
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Apptore payment is not supported on this platform");
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

                    if (string.IsNullOrEmpty(orderResponse.PaymentUrl)) {
                        throw new NoctuaException(NoctuaErrorCode.Payment, "Payment URL is empty.");
                    }

                    _log.Debug(orderResponse.PaymentUrl);
                    Application.OpenURL(orderResponse.PaymentUrl);

                    paymentResult = new PaymentResult{Status = PaymentStatus.Confirmed};

                    var continueToVerify = await _customPaymentCompleteDialog.Show();
                    if (!continueToVerify) // Custom payment get canceled.
                    {
                        var verifiedAtCancelation = false;
                        var verifyReq = new VerifyOrderRequest
                            {
                                Id = orderResponse.Id,
                                ReceiptData = orderResponse.Id.ToString(),
                            };
                        try
                        {
                            // At least try to verify before fallback to secondary paymennt.
                            var verifyResponseAtCancel = await VerifyOrderImplAsync(
                                orderRequest,
                                verifyReq,
                                _accessTokenProvider.AccessToken
                            );

                            // If verified, cancel the falback to secondary payment.
                            if (verifyResponseAtCancel.Status == OrderStatus.completed)
                            {
                                _log.Debug("remove from pending queue because it has been completed");
                                RemoveFromRetryPendingPurchasesByOrderID(orderResponse.Id);
                                verifiedAtCancelation = true;
                            }
                        }
                        catch (Exception e) 
                        {
                            // TODO Do we really need to retry the canceled purchase?
                            // What if the user is accidentally tap the close button instead of complete?
                            _log.Exception(e);
                            EnqueueToRetryPendingPurchases(
                                new RetryPendingPurchaseItem
                                {
                                    OrderId = orderResponse.Id,
                                    OrderRequest = orderRequest,
                                    VerifyOrderRequest = verifyReq,
                                    AccessToken = _accessTokenProvider.AccessToken
                                }
                            );
                        }

                        if (_enabledPaymentTypes.Count > 1 && !verifiedAtCancelation)
                        {
                            // Fallback to secondary payment option.
                            return await PurchaseItemAsync(purchaseRequest, true);
                        } else if (verifiedAtCancelation) {
                            // Verified at cancelation, set the paymentResult to confirmed
                            // to allow this to be processed as successful purchase/payment.
                            // Double verify will be happened but it's ok.
                            paymentResult = new PaymentResult{
                                Status = PaymentStatus.Confirmed
                            };
                        } else {
                            // Custom payment is actually get canceled
                            // but there is no secondary payment.
                            _log.Debug("custom payment is actually get canceled, but there is no secondary payment. Remove from pending queue.");
                            RemoveFromRetryPendingPurchasesByOrderID(orderResponse.Id);
                            paymentResult = new PaymentResult{
                                Status = PaymentStatus.Canceled,
                                Message = "Purchase canceled"
                            };
                            // TODO Do we really need to retry the canceled purchase?
                            // What if the user is accidentally tap the close button instead of complete?
                            EnqueueToRetryPendingPurchases(
                                new RetryPendingPurchaseItem
                                {
                                    OrderId = orderResponse.Id,
                                    OrderRequest = orderRequest,
                                    VerifyOrderRequest = verifyReq,
                                    AccessToken = _accessTokenProvider.AccessToken
                                }
                            );
                        }
                    }

                    // Native browser custom payment is using OrderId as ReceiptData
                    paymentResult.ReceiptData = orderResponse.Id.ToString();
                    
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
            
            switch (paymentResult.Status)
            {
                case PaymentStatus.Confirmed:
                case PaymentStatus.Successful:
                case PaymentStatus.Pending: // will attempt to verify directly
                    break;
                case PaymentStatus.PendingPurchaseOngoing:
                case PaymentStatus.ItemAlreadyOwned:
                    await _failedPaymentDialog.Show(paymentResult.Status);
                    
                    throw new NoctuaException(NoctuaErrorCode.Payment, paymentResult.Message);
                case PaymentStatus.Canceled:
                    _eventSender?.Send(
                        "purchase_cancelled",
                        new()
                        {
                            { "product_id", orderResponse.ProductId },
                            { "amount", usdProduct.Price },
                            { "currency", usdProduct.Currency },
                            { "order_id", orderResponse.Id },
                            { "orig_amount", orderRequest.Price },
                            { "orig_currency", orderRequest.Currency }
                        }
                    );

                    _uiFactory.ShowError(LocaleTextKey.IAPCanceled);
                
                    throw new NoctuaException(NoctuaErrorCode.Payment, $"payment status: {paymentResult.Status}, Message: {paymentResult.Message}");

                default:
                    _uiFactory.ShowError(LocaleTextKey.IAPFailed);
                
                    throw new NoctuaException(NoctuaErrorCode.Payment, $"payment status: {paymentResult.Status}, Message: {paymentResult.Message}");
            }

            var orderId = orderResponse.Id;
            _log.Info($"Purchase was successful. Verifying order ID: {orderId}");

            var verifyOrderRequest = new VerifyOrderRequest
            {
                Id = orderId,
                ReceiptId = paymentResult.ReceiptId,
                ReceiptData = paymentResult.ReceiptData
            };

            // Store early for Negative Payment Cases no #4
            EnqueueToRetryPendingPurchases(
                new RetryPendingPurchaseItem
                {
                    OrderId = orderResponse.Id,
                    OrderRequest = orderRequest,
                    VerifyOrderRequest = verifyOrderRequest,
                    AccessToken = _accessTokenProvider.AccessToken
                }
            );

            _log.Info($"Verifying order: {verifyOrderRequest.Id} with receipt data: {verifyOrderRequest.ReceiptData}");
            
            VerifyOrderResponse verifyOrderResponse;

            try {
                _uiFactory.ShowLoadingProgress(true);

                verifyOrderResponse = await RetryAsync(() => VerifyOrderImplAsync(
                        orderRequest,
                        verifyOrderRequest,
                        _accessTokenProvider.AccessToken
                    )
                );

                _uiFactory.ShowLoadingProgress(false);
            }
            catch (NoctuaException e)
            {
                if ((NoctuaErrorCode)e.ErrorCode == NoctuaErrorCode.Networking)
                {
                    EnqueueToRetryPendingPurchases(
                        new RetryPendingPurchaseItem
                        {
                            OrderId = orderResponse.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "Network error"
                        }
                    );
                }
                
                _uiFactory.ShowLoadingProgress(false);
                _log.Exception(e);
                _uiFactory.ShowError(e.Message);

                throw;
            }
            catch (Exception e) 
            {
                _uiFactory.ShowLoadingProgress(false);
                _log.Exception(e);
                
                throw;
            }

            switch (verifyOrderResponse.Status)
            {
                case OrderStatus.canceled:
                    _uiFactory.ShowGeneralNotification(
                        "Your purchase has been canceled. Please contact customer support for more details.",
                        false
                    );
                    break;
                case OrderStatus.refunded:
                    _uiFactory.ShowGeneralNotification(
                        "Your purchase has been refunded. Please contact customer support for more details.",
                        false
                    );
                    break;
                case OrderStatus.voided:
                    _uiFactory.ShowGeneralNotification(
                        "Your purchase has been voided. Please contact customer support for more details.",
                        false
                    );
                    break;
                default:
                    _uiFactory.ShowGeneralNotification("Purchase successful!", true);
                    break;
            }


            return new PurchaseResponse
            {
                OrderId = verifyOrderResponse.Id,
                Status = verifyOrderResponse.Status,
                Message = "Purchase " + verifyOrderResponse.Status.ToString(),
            };
        }

        public async UniTask<OrderStatus>  RetryPendingPurchaseByOrderId(int orderId)
        {
            var item = GetPendingPurchaseByOrderId(orderId);
            try
            {
                _log.Info(
                    $"Retrying Order ID: {item.OrderId}, " +
                    $"Receipt Data: {item.VerifyOrderRequest.ReceiptData}"
                );

                if (item.OrderRequest.Id == 0) {
                item.OrderRequest.Id = item.OrderId;
                }

                var verifyOrderResponse = await VerifyOrderImplAsync(
                    item.OrderRequest,
                    item.VerifyOrderRequest,
                    item.AccessToken
                );

                if (verifyOrderResponse.Status != OrderStatus.completed &&
                verifyOrderResponse.Status != OrderStatus.canceled &&
                verifyOrderResponse.Status != OrderStatus.voided)
                {
                    // Enqueue to player prefs for future read
                    item.Status = verifyOrderResponse.Status.ToString();
                    EnqueueToRetryPendingPurchases(item);
                    _uiFactory.ShowGeneralNotification("Failed to verify the purchase. Status: " + verifyOrderResponse.Status.ToString(), false);
                }

                return verifyOrderResponse.Status;
            }
            catch (NoctuaException e)
            {
                // Track failure
                _eventSender?.Send(
                    "purchase_verify_order_failed",
                    new()
                    {
                        { "product_id", item.OrderRequest.ProductId },
                        { "amount", item.OrderRequest.PriceInUSD },
                        { "currency", "USD" },
                        { "order_id", item.OrderRequest.Id },
                        { "orig_amount", item.OrderRequest.Price },
                        { "orig_currency", item.OrderRequest.Currency }
                    }
                );

                EnqueueToRetryPendingPurchases(
                    new RetryPendingPurchaseItem
                    {
                        OrderId = item.OrderId,
                        OrderRequest = item.OrderRequest,
                        VerifyOrderRequest = item.VerifyOrderRequest,
                        AccessToken = item.AccessToken,
                        Status = "verification_failed",
                    }
                );

                _log.Error("NoctuaException: " + e.ErrorCode + " : " + e.Message);
                return OrderStatus.error;
            }
            catch (Exception e)
            {
                _log.Error("Exception: " + e);
                return OrderStatus.error;
            }

            return OrderStatus.completed;
        }

        private async UniTask<T> RetryAsync<T>(Func<UniTask<T>> action)
        {
            while (true)
            {
                try
                {
                    _uiFactory.ShowLoadingProgress(true);
                    return await action();
                }
                catch (NoctuaException e)
                {
                    _uiFactory.ShowLoadingProgress(false);
                    var errorCode = (NoctuaErrorCode)e.ErrorCode;

                    bool shouldRetry = false;
                    switch (errorCode)
                    {
                        case NoctuaErrorCode.Networking:
                            _log.Exception(e);
                            if (e.Message.Contains("HTTP error"))
                            {
                                shouldRetry = await _uiFactory.ShowRetryDialog($"{e.Message}. Please try again later.");
                            } else {
                                shouldRetry = await _uiFactory.ShowRetryDialog("Please check your internet connection.");
                            }
                            break;
                        default:
                            _log.Exception(e);
                            shouldRetry = await _uiFactory.ShowRetryDialog(e.Message);
                            break;
                    }

                    if (!shouldRetry)
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _uiFactory.ShowLoadingProgress(false);
                    _log.Exception(ex);

                    bool shouldRetry = await _uiFactory.ShowRetryDialog(ex.Message);
                    if (!shouldRetry)
                    {
                        throw;
                    }
                }
            }
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
            if (result is null)
            {
                _log.Error("Purchase result is null");

                return new PaymentResult
                {
                    Status = PaymentStatus.InvalidPurchaseObject,
                    Message = "Purchase result is null"
                };
            }

            _log.Debug(
                $"Playstore purchase:\n"           +
                $"token: {result.PurchaseToken}, " +
                $"success: {result.Success}, "     +
                $"errorCode: {result.ErrorCode}, " +
                $"purchaseState={result.PurchaseState}"
            );

            if (result.ErrorCode == GoogleBilling.BillingErrorCode.UserCanceled)
            {
                return new PaymentResult
                {
                    Status = PaymentStatus.Canceled,
                    Message = result.Message,
                    ReceiptId = result.ReceiptId,
                    ReceiptData = result.ReceiptData,
                };
            }

            if (result.ErrorCode == GoogleBilling.BillingErrorCode.DeveloperError && 
                result.Message.Contains("There is already a pending purchase for the requested item."))
            {
                return new PaymentResult
                {
                    Status = PaymentStatus.PendingPurchaseOngoing,
                    Message = result.Message,
                    ReceiptId = result.ReceiptId,
                    ReceiptData = result.ReceiptData,
                };
            }

            if (result.ErrorCode == GoogleBilling.BillingErrorCode.ItemAlreadyOwned)
            {
                return new PaymentResult
                {
                    Status = PaymentStatus.ItemAlreadyOwned,
                    Message = $"Item with purchase token '{result.PurchaseToken}' already owned",
                    ReceiptId = result.ReceiptId,
                    ReceiptData = result.ReceiptData,
                };
            }

            if (result.ErrorCode != GoogleBilling.BillingErrorCode.OK)
            {
                return new PaymentResult
                {
                    Status = PaymentStatus.Failed,
                    Message = $"Purchase failed with error '{result.ErrorCode}'",
                    ReceiptId = result.ReceiptId,
                    ReceiptData = result.ReceiptData,
                };
            }

            if (result.PurchaseState == GoogleBilling.PurchaseState.Pending)
            {
                return new PaymentResult
                {
                    Status = PaymentStatus.Pending,
                    Message = $"Purchase with purchase token '{result.PurchaseToken}' is pending",
                    ReceiptId = result.ReceiptId,
                    ReceiptData = result.ReceiptData,
                };
            }

            return new PaymentResult
            {
                Status = PaymentStatus.Successful,
                Message = $"Purchase '{result.ReceiptId}' successful",
                ReceiptId = result.ReceiptId,
                ReceiptData = result.ReceiptData,
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
            _log.Debug("save pending purchases to player prefs");
            var updatedJson = JsonConvert.SerializeObject(orders);
            PlayerPrefs.SetString("NoctuaPendingPurchases", updatedJson);
            PlayerPrefs.Save();

            // Leave it here for debuggin purpose.
            //GetPendingPurchases();
        }

        public List<RetryPendingPurchaseItem> GetPendingPurchases()
        {
            _log.Info("Noctua.GetPendingPurchases");
            var json = PlayerPrefs.GetString("NoctuaPendingPurchases", string.Empty);
            _log.Info($"Pending purchases data: {json}");

            if (string.IsNullOrEmpty(json))
            {
                return new List<RetryPendingPurchaseItem>();
            }

            try
            {
                var pendingPurchases = JsonConvert.DeserializeObject<List<RetryPendingPurchaseItem>>(json);
                
                var list = pendingPurchases
                    .Where(p => p.VerifyOrderRequest != null && p.AccessToken != null)
                    .ToList();
                list.Sort((p1, p2) => p1.OrderId.CompareTo(p2.OrderId));

                return list;
            }
            catch (Exception e)
            {
                _log.Error("Failed to parse pending purchases: " + e);

                PlayerPrefs.DeleteKey("NoctuaPendingPurchases");
                
                return new List<RetryPendingPurchaseItem>();
            }
        }

        public RetryPendingPurchaseItem GetPendingPurchaseByOrderId(int orderId)
        {
            _log.Info("Noctua.GetPendingPurchases");
            var json = PlayerPrefs.GetString("NoctuaPendingPurchases", string.Empty);
            _log.Info($"Pending purchases data: {json}");

            if (string.IsNullOrEmpty(json))
            {
                throw new Exception($"No pending purchase with such ID {orderId}");
            }

            try
            {
                var pendingPurchases = JsonConvert.DeserializeObject<List<RetryPendingPurchaseItem>>(json);

                var list = pendingPurchases
                    .Where(p => p.VerifyOrderRequest != null && p.AccessToken != null)
                    .ToList();

                var result = new RetryPendingPurchaseItem();
                var found = false;
                foreach (var item in list)
                {
                    if (item.OrderId == orderId)
                    {
                        result = item;
                        found = true;
                        break;
                    }
                }
                if (found)
                {
                    return result;
                } else {
                    throw new Exception($"No pending purchase with such ID {orderId}");
                }
            }
            catch (Exception e)
            {
                _log.Error("Failed to parse pending purchases: " + e);
                throw e;
            }
        }

        public async UniTask RetryPendingPurchasesAsync()
        {
            _log.Info("Starting pending purchases retry loop.");
            
            var random = new Random();
            
            var runningPendingPurchases = GetPendingPurchases().ToList();

            _log.Info("Queue count: " + runningPendingPurchases.Count);
            CancellationTokenSource cts = new();
            var quitting = false;

            Application.quitting += () =>
            {
                _log.Info("Quitting pending purchases retry loop.");
                quitting = true;
                cts.Cancel();
            };

            var retryCount = 0;
            
            if (_enabledPaymentTypes.Count == 0)
            {
                _log.Error("no payment types enabled, quitting");
                
                return;
            }

            while (!quitting)
            {
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
                    
                    _log.Info("Draining pending purchase: " + pendingPurchase.OrderId);
                }
                
                // Retry pending purchases
                var failedPendingPurchases = new List<RetryPendingPurchaseItem>();
                
                foreach (var item in runningPendingPurchases)
                {

                    if (item.Status == OrderStatus.refunded.ToString() ||
                    item.Status == OrderStatus.canceled.ToString())
                    {
                        // We want to keep these items remains in the pending list
                        EnqueueToRetryPendingPurchases(item);
                        failedPendingPurchases.Add(item);

                        continue;
                    }

                    try
                    {
                        _log.Info(
                            $"Retrying Order ID: {item.OrderId}, " +
                            $"Receipt Data: {item.VerifyOrderRequest.ReceiptData}"
                        );

                        if (item.OrderRequest.Id == 0) {
                            item.OrderRequest.Id = item.OrderId;
                        }

                        var verifyOrderResponse = await VerifyOrderImplAsync(
                            item.OrderRequest,
                            item.VerifyOrderRequest,
                            item.AccessToken
                        );

                        if (verifyOrderResponse.Status != OrderStatus.completed &&
                        verifyOrderResponse.Status != OrderStatus.voided)
                        {
                            // Enqueue to player prefs for future read
                            item.Status = verifyOrderResponse.Status.ToString();
                            EnqueueToRetryPendingPurchases(item);
                            // Enqueue to running queue
                            failedPendingPurchases.Add(item);
                        }
                    }
                    catch (NoctuaException e)
                    {
                        // Track failure
                        _eventSender?.Send(
                            "purchase_verify_order_failed",
                            new()
                            {
                                { "product_id", item.OrderRequest.ProductId },
                                { "amount", item.OrderRequest.PriceInUSD },
                                { "currency", "USD" },
                                { "order_id", item.OrderRequest.Id },
                                { "orig_amount", item.OrderRequest.Price },
                                { "orig_currency", item.OrderRequest.Currency }
                            }
                        );

                        EnqueueToRetryPendingPurchases(
                            new RetryPendingPurchaseItem
                            {
                                OrderId = item.OrderId,
                                OrderRequest = item.OrderRequest,
                                VerifyOrderRequest = item.VerifyOrderRequest,
                                AccessToken = item.AccessToken,
                                Status = "verification_failed",
                            }
                        );

                        if ((NoctuaErrorCode)e.ErrorCode == NoctuaErrorCode.Networking)
                        {
                            failedPendingPurchases.Add(item);
                        
                            _log.Info("Adding pending purchase back to running queue: " + item.OrderId);
                        }

                        _log.Error("NoctuaException: " + e.ErrorCode + " : " + e.Message);
                    }
                    catch (Exception e)
                    {
                        _log.Error("Exception: " + e);
                    }
                }

                // At this point, the successful ones are already removed from the PlayerPrefs. 
                if (failedPendingPurchases.Count > 0)
                {
                    _log.Info("Saving failed pending purchases: " + failedPendingPurchases.Count);

                    // Merge with existing _waitingPendingPurchases instead of overwrite
                    foreach (var item in failedPendingPurchases)
                    {
                        EnqueueToRetryPendingPurchases(item);
                    }
                    SavePendingPurchases(_waitingPendingPurchases.ToList());
                }

                // Continue the current failed retry. 
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

        public async UniTask<NoctuaGoldData> GetNoctuaGold()
        {
            var request = new HttpRequest(HttpMethod.Get, $"{_config.BaseUrl}/noctuastore/wallet")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken);

            return await request.Send<NoctuaGoldData>();
        }

        private TimeSpan GetBackoffDelay(Random random, int retryCount)
        {
            var baseDelay = TimeSpan.FromSeconds(5); // Base delay of 1 second
            var maxDelay = TimeSpan.FromHours(3); // Maximum delay of 1 hour
            var randomFactor = (random.NextDouble() * 0.5) + 0.75; // Random factor between 0.75 and 1.25

            var delay = TimeSpan.FromMinutes(baseDelay.TotalMinutes * Math.Pow(2, retryCount - 1) * randomFactor);
            return delay > maxDelay ? maxDelay : delay;
        }

        private void EnqueueToRetryPendingPurchases(RetryPendingPurchaseItem item)
        {
            // Remove the existing if any.
            RemoveFromRetryPendingPurchasesByOrderID(item.OrderId);

            _log.Info($"Enqueue to retry pending purchase: {item.OrderId}");
            _waitingPendingPurchases.Enqueue(item);
            SavePendingPurchases(_waitingPendingPurchases.ToList());
        }

        public void RemoveFromRetryPendingPurchasesByOrderID(int orderId)
        {
            _log.Info($"Remove from retry pending purchase: {orderId}");

            // Rebuild the queue excluding the item with the specified OrderID
            var updatedQueue = new Queue<RetryPendingPurchaseItem>(
                _waitingPendingPurchases.Where(item => item.OrderId != orderId));

            _waitingPendingPurchases.Clear();
            foreach (var item in updatedQueue)
            {
                _waitingPendingPurchases.Enqueue(item);
            }
            
            SavePendingPurchases(_waitingPendingPurchases.ToList());
        }

        [Preserve]
        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
            public string WebPaymentBaseUrl;
        }

        [Preserve]
        public class RetryPendingPurchaseItem
        {
            public int OrderId;
            public OrderRequest OrderRequest;
            public VerifyOrderRequest VerifyOrderRequest;
            public string AccessToken;
            public string Status;
        }
        
        private void EnsureEnabled()
        {
            if (_enabled) return;

            _log.Error("Noctua IAP is not enabled due to initialization failure.");
                
            throw new NoctuaException(NoctuaErrorCode.Application, "Noctua IAP is not enabled due to initialization failure.");
        }

        public void Enable()
        {
            _enabled = true;
        }
    }
}
