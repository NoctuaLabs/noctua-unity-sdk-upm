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

        [JsonProperty("allow_payment_type_override")]
        public bool AllowPaymentTypeOverride = true;
    }

    [Preserve]
    public class UnpairedPurchaseRequest
    {
        [JsonProperty("receipt_data")]
        public string ReceiptData;

        [JsonProperty("payment_type")]
        public PaymentType PaymentType;

        [JsonProperty("product_id")]
        public string ProductId;

        [JsonProperty("currency")]
        public string Currency;

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

        [JsonProperty("payment_type")]
        public PaymentType PaymentType;
    }

    [Preserve]
    public class UnpairedPurchaseResponse
    {
        [JsonProperty("id")]
        public int Id;
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
        PendingPurchaseOngoing,
        IapNotReady,
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

        [JsonProperty("trigger")]
        public string Trigger;
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
        voided,
        fallback_to_native_payment
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

    public enum VerifyOrderTrigger
    {
        payment_flow,
        manual_retry,
        client_automatic_retry,
    }

    
    [Preserve]
    public class NoctuaIAPService
    {
        private readonly Config _config;
        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaIAPService));

        private TaskCompletionSource<string> _activeCurrencyTcs;

        public event Action<OrderRequest> OnPurchaseDone;

        private readonly EventSender _eventSender;
        private readonly AccessTokenProvider _accessTokenProvider;
        private readonly Queue<PurchaseItem> _waitingPendingPurchases = new();
        private readonly INativePlugin _nativePlugin;
        private readonly ProductList _usdProducts = new();
        private readonly CustomPaymentCompleteDialogPresenter _customPaymentCompleteDialog;
        private readonly FailedPaymentDialogPresenter _failedPaymentDialog;
        private TaskCompletionSource<PaymentResult> _paymentTcs;

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
        private string _distributionPlaftorm;
        
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
            
            _uiFactory = uiFactory;
            _customPaymentCompleteDialog = _uiFactory.Create<CustomPaymentCompleteDialogPresenter, object>(new object());
            _failedPaymentDialog = _uiFactory.Create<FailedPaymentDialogPresenter, object>(new object());

#if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.OnProductDetailsDone += HandleGoogleProductDetails;
            GoogleBillingInstance.OnPurchaseDone += HandleGooglePurchaseDone;
            GoogleBillingInstance.OnQueryPurchasesDone += HandleGoogleQueryPurchasesDone;
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
            _paymentTcs = null;
#endif
        }

        public void SetEnabledPaymentTypes(List<PaymentType> enabledPaymentTypes)
        {
            // The sequence represent the priority.
            _enabledPaymentTypes = enabledPaymentTypes;
        }

        public void SetDistributionPlatform(string platform)
        {
            // The sequence represent the priority.
            _distributionPlaftorm = platform;
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

        private async UniTask<UnpairedPurchaseResponse> CreateUnpairedPurchaseAsync(UnpairedPurchaseRequest purchase)
        {
            var url = $"{_config.BaseUrl}/unpaired-purchases";

            var request = new HttpRequest(HttpMethod.Post, url)
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken)
                .WithJsonBody(purchase);

            var response = await request.Send<UnpairedPurchaseResponse>();

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
            string token,
            long? playerId,
            bool isTriggeredByIAP
        )
        {
                _log.Debug($"Attempt to verify orderID {verifyOrderRequest.Id}, triggered by IAP: ${isTriggeredByIAP}");

                if (orderRequest.Id == 0)
                {
                    throw new NoctuaException(NoctuaErrorCode.Payment, $": Invalid order ID: 0");
                }

                var verifyOrderResponse = new VerifyOrderResponse();
                verifyOrderResponse.Id = verifyOrderRequest.Id;
                try {
                verifyOrderResponse = await VerifyOrderAsync(verifyOrderRequest, token);
                }
                catch(Exception e)
                {
                    verifyOrderResponse = new VerifyOrderResponse();
                    verifyOrderResponse.Id = verifyOrderRequest.Id;
                    if (e is NoctuaException noctuaEx)
                    {
                        switch (noctuaEx.ErrorCode)
                        {
                        case 2043:
                            verifyOrderResponse.Status = OrderStatus.pending;
                            break;
                        case 2044:
                            verifyOrderResponse.Status = OrderStatus.verification_failed;
                            break;
                        case 2045:
                            verifyOrderResponse.Status = OrderStatus.delivery_callback_failed;
                            break;
                        case 2046:
                            verifyOrderResponse.Status = OrderStatus.canceled;
                            break;
                        case 2047:
                            verifyOrderResponse.Status = OrderStatus.refunded;
                            break;
                        case 2048:
                            verifyOrderResponse.Status = OrderStatus.voided;
                            break;
                        default:
                            break;
                        }
                    } else {
                        throw e;
                    }
                }

                if (verifyOrderResponse == null) // Guard it
                {
                    verifyOrderResponse = new VerifyOrderResponse();
                    verifyOrderResponse.Id = verifyOrderRequest.Id;
                    verifyOrderResponse.Status = OrderStatus.unknown;
                }

                switch (verifyOrderResponse.Status)
                {
                case OrderStatus.completed:
                    _log.Debug("remove from pending queue because it has been completed");
                    RemoveFromRetryPendingPurchasesByOrderID(verifyOrderRequest.Id);
                    _log.Debug("add to purchase history");
                    AddToPurchaseHistory(
                        new PurchaseItem
                        {
                            OrderId = verifyOrderRequest.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "refunded",
                            PlayerId = Noctua.Auth.RecentAccount?.Player?.Id,
                        }
                    );

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

                    OnPurchaseDone?.Invoke(orderRequest);

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
                        new PurchaseItem
                        {
                            OrderId = verifyOrderRequest.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "canceled",
                            PlayerId = Noctua.Auth.RecentAccount?.Player?.Id,
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
                        new PurchaseItem
                        {
                            OrderId = verifyOrderRequest.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "refunded",
                            PlayerId = Noctua.Auth.RecentAccount?.Player?.Id,
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
                    // Only send this event on IAP flow so retry/worker will not flood our data.
                    if (isTriggeredByIAP)
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
                    }

                    EnqueueToRetryPendingPurchases(
                        new PurchaseItem
                        {
                            OrderId = verifyOrderRequest.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "verification_failed",
                            PlayerId = Noctua.Auth.RecentAccount?.Player?.Id,
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

        public void QueryPurchasesAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.QueryPurchasesAsync();
#endif
        }

        private async Task HandlePurchaseRetryPopUpMessageAsync(string offlineModeMessage, PurchaseRequest purchaseRequest, bool tryToUseSecondaryPayment = false, PaymentType enforcedPaymentType = PaymentType.unknown) {
            bool isRetry = await _uiFactory.ShowRetryDialog(offlineModeMessage, "offlineMode");
            if(isRetry)
            {
                await PurchaseItemAsync(purchaseRequest, tryToUseSecondaryPayment, enforcedPaymentType);
            }
        }

        public async UniTask<PurchaseResponse> PurchaseItemAsync(PurchaseRequest purchaseRequest, bool tryToUseSecondaryPayment = false, PaymentType enforcedPaymentType = PaymentType.unknown)
        {

            if (_config.isIAPDisabled)
            {
                _uiFactory.ShowError(LocaleTextKey.IAPDisabled);

                _log.Warning($"IAP is being disabled by config");
                throw new NoctuaException(NoctuaErrorCode.Unknown, "IAP is being disabled by config");

            }


            // Offline-first handler
            _uiFactory.ShowLoadingProgress(true);
            
            var offlineModeMessage = Noctua.Platform.Locale.GetTranslation(LocaleTextKey.OfflineModeMessage) + " [IAP]";
            var isOffline = await Noctua.IsOfflineAsync();

            if(!isOffline && !Noctua.IsInitialized())
            {
                try
                {
                    await Noctua.InitAsync();

                    await Noctua.Auth.AuthenticateAsync();

                } catch(Exception e)
                {
                    _uiFactory.ShowLoadingProgress(false);

                    await HandlePurchaseRetryPopUpMessageAsync(offlineModeMessage, purchaseRequest, tryToUseSecondaryPayment, enforcedPaymentType);

                    throw new NoctuaException(NoctuaErrorCode.Authentication, $"{e.Message}");
                }
            }

            if (isOffline)
            {
                _uiFactory.ShowLoadingProgress(false);

                await HandlePurchaseRetryPopUpMessageAsync(offlineModeMessage, purchaseRequest, tryToUseSecondaryPayment, enforcedPaymentType);

                throw new NoctuaException(NoctuaErrorCode.Authentication, offlineModeMessage);
            }

            _uiFactory.ShowLoadingProgress(false);

            var iapReadyTimeout = DateTime.UtcNow.AddSeconds(5);
            while (!IsReady && DateTime.UtcNow < iapReadyTimeout)
            {
                Init();

                var completedTask = await UniTask.WhenAny(
                    UniTask.WaitUntil(() => IsReady),
                    UniTask.Delay(1000)
                );

                if (completedTask == 0)
                {
                    break;
                }
            }

            var result = await PurchaseItemImplAsync(purchaseRequest, tryToUseSecondaryPayment, enforcedPaymentType);

            if (result.Status == OrderStatus.fallback_to_native_payment)
            {
#if UNITY_ANDROID
                enforcedPaymentType = PaymentType.playstore;
                _log.Debug($"Fallback to native payment: {enforcedPaymentType}");
                result = await PurchaseItemImplAsync(purchaseRequest, false, enforcedPaymentType);
#elif UNITY_IOS
                enforcedPaymentType = PaymentType.appstore;
                _log.Debug($"Fallback to native payment: {enforcedPaymentType}");
                result = await PurchaseItemImplAsync(purchaseRequest, false, enforcedPaymentType);
#endif
                return result;
            } else {
                return result;
            }
        }

        public async UniTask<PurchaseResponse> PurchaseItemImplAsync(PurchaseRequest purchaseRequest, bool tryToUseSecondaryPayment = false, PaymentType enforcedPaymentType = PaymentType.unknown)
        {
            EnsureEnabled();
            
            _log.Debug("calling API");

            if (!_accessTokenProvider.IsAuthenticated)
            {
                _uiFactory.ShowError(LocaleTextKey.IAPRequiresAuthentication);

                _log.Warning($"Purchase requires user authentication");
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Purchase requires user authentication");
            }
            
            if (_enabledPaymentTypes.Count == 0)
            {
                _uiFactory.ShowError(LocaleTextKey.IAPPaymentDisabled);

                _log.Warning($"No payment types enabled");
                throw new NoctuaException(NoctuaErrorCode.Payment, "no payment types enabled");
            }

            // The payment types are prioritized in backend
            // and filtered by runtime platform in InitAsync()
            // This payment type could be override by
            // the response of create order.
            var paymentType = _enabledPaymentTypes.First();
            if (tryToUseSecondaryPayment && _enabledPaymentTypes.Count > 1)
            {
                paymentType = _enabledPaymentTypes[1];
                _log.Info($"Fallback to secondary payment type: {paymentType}");
            }

            // Enforce particular payment type if available.
            // This could be triggered from CustomPaymentCompleteDialogPresenter.cs.
            if (enforcedPaymentType != PaymentType.unknown)
            {
                paymentType = enforcedPaymentType;
                _log.Info($"Fallback to enforced payment type: {paymentType}");
            }

            _uiFactory.ShowLoadingProgress(true);
            
            Product usdProduct;
            OrderRequest orderRequest;
            OrderResponse orderResponse;

            var unpairedOrders = new Dictionary<string, PurchaseItem>();
            var orderId = 0;
            var pendingPurchaseItem = new PurchaseItem();
            var verifyOrderRequest = new VerifyOrderRequest();

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
                
                _log.Info($"updated player role: '{playerData.IngameRoleId}', server: '{playerData.IngameServerId}'");
                
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
                    _log.Warning($"USD price not found for product {orderRequest.ProductId}");
                    throw new NoctuaException(NoctuaErrorCode.Payment, $"USD price not found for product '{orderRequest.ProductId}'");
                }

                orderRequest.PriceInUSD = usdProduct.Price;
                orderRequest.Timestamp = DateTime.Now.ToString();
                orderRequest.AllowPaymentTypeOverride = true;
            
                _log.Info("creating order");

                if (enforcedPaymentType != PaymentType.unknown)
                {
                    orderRequest.AllowPaymentTypeOverride = false;
                }

                orderResponse = await RetryAsync(() => CreateOrderAsync(orderRequest));
                orderRequest.Id = orderResponse.Id;

                // Override the payment type in case this get altered from backend.
                // TODO this will cause payment loop if both of these conditions meet:
                // 1. Noctuastore is prioritized above Playstore
                // 2. The user have enough noctua gold
                if (enforcedPaymentType == PaymentType.unknown
                ) // It means that there is no enforce on payment type
                {
                    paymentType = orderResponse.PaymentType;
                    _log.Info($"payment type get overrided from backend: {paymentType}");
                }

                // Declare structs early so we can use it for multipurposes
                orderId = orderResponse.Id;
                verifyOrderRequest = new VerifyOrderRequest
                {
                    Id = orderId,
                    // But no receipt id or receipt data at this point
                };
                pendingPurchaseItem = new PurchaseItem
                {
                    OrderId = orderResponse.Id,
                    OrderRequest = orderRequest,
                    VerifyOrderRequest = verifyOrderRequest,
                    AccessToken = _accessTokenProvider.AccessToken,
                    PlayerId = Noctua.Auth.RecentAccount?.Player?.Id,
                };

                // Store unpaired order.
                _log.Info($"NoctuaIAPService.HandleUnpairedPurchase store unpaired order for order ID: {JsonConvert.SerializeObject(orderRequest)}");
                _log.Info($"NoctuaIAPService.HandleUnpairedPurchase pending purchase item for unpaired order: {JsonConvert.SerializeObject(pendingPurchaseItem)}");
                unpairedOrders = new Dictionary<string, PurchaseItem>();
                var unpairedOrdersJson = PlayerPrefs.GetString("NoctuaUnpairedOrders", "{}");
                try
                {
                    unpairedOrders = JsonConvert.DeserializeObject<Dictionary<string, PurchaseItem>>(unpairedOrdersJson);
                    if (unpairedOrders == null)
                    {
                        unpairedOrders = new Dictionary<string, PurchaseItem>();
                    }
                }
                catch (Exception e)
                {
                    _log.Warning($"Failed to parse existing unpaired orders: {e}");
                    unpairedOrders = new Dictionary<string, PurchaseItem>();
                }

                unpairedOrders[orderRequest.ProductId] = pendingPurchaseItem;
                var serializedUnpairedOrders = JsonConvert.SerializeObject(unpairedOrders);
                _log.Info($"NoctuaIAPService.HandleUnpairedPurchase unpaired orders to be save: {serializedUnpairedOrders}");
                PlayerPrefs.SetString("NoctuaUnpairedOrders", serializedUnpairedOrders);
                PlayerPrefs.Save();

                // Store early for Negative Payment Cases no #4
                EnqueueToRetryPendingPurchases(pendingPurchaseItem);
                
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

                _log.Info("orderResponse.Id: "         + orderResponse.Id);
                _log.Info("orderResponse.ProductId: " + orderResponse.ProductId);

                _uiFactory.ShowLoadingProgress(false);
            }
            catch (Exception e)
            {
                _log.Warning($"Failed to prepare purchase: {e.Message}");
                _uiFactory.ShowError(e.Message);
                _log.Exception(e);
                _uiFactory.ShowLoadingProgress(false);

                throw;
            }

            _paymentTcs = new TaskCompletionSource<PaymentResult>();
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
                        
                        _paymentTcs.TrySetResult(GetAppstorePaymentResult(orderResponse.Id, success, message));
                    });

                    var task = await _paymentTcs.Task;
                    _log.Info("NoctuaIAPService.PurchaseItemAsync user side payment flow completed, clear up _paymentTcs then continue the payment flow.");
                    
                    paymentResult = _paymentTcs.Task.Result;
                    
                    _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback response: " + paymentResult);
                    break;
#else
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Apptore payment is not supported on this platform");
#endif
                case PaymentType.playstore:
#if UNITY_ANDROID && !UNITY_EDITOR
                    _log.Info("NoctuaIAPService.PurchaseItemAsync purchase on playstore: " + orderResponse.ProductId);
                    
                    GoogleBillingInstance.PurchaseItem(orderResponse.ProductId);

                    var task = await _paymentTcs.Task;
                    _log.Info("NoctuaIAPService.PurchaseItemAsync user side payment flow completed, clear up _paymentTcs then continue the payment flow.");
                    
                    paymentResult = _paymentTcs.Task.Result;
                    
                    _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback response: " + paymentResult);
                    break;
#else
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Playstore payment is not supported on this platform");
#endif

                case PaymentType.noctuastore:
                    // Noctua store payment is not using _paymentTcs but rather have a different mechanism to wait
                    // for user payment to be completed and directly set paymentResult.
                    // Please see CustomPaymentCompleteDialog.

                    if (string.IsNullOrEmpty(orderResponse.PaymentUrl)) {
                        _log.Warning($"Payment URL is empty");
                        throw new NoctuaException(NoctuaErrorCode.Payment, "Payment URL is empty.");
                    }

                    _log.Debug(orderResponse.PaymentUrl);
                    Application.OpenURL(orderResponse.PaymentUrl);

                    paymentResult = new PaymentResult{Status = PaymentStatus.Confirmed};

                    var nativePaymentButtonEnabled = _distributionPlaftorm != "direct";

                    var completeDialogResult = await _customPaymentCompleteDialog.Show(nativePaymentButtonEnabled);
                    _log.Info("NoctuaIAPService.PurchaseItemAsync user side payment flow completed (custom payment complete dialog), clear up _paymentTcs then continue the payment flow.");

                    if (completeDialogResult == "cancel") // Custom payment get canceled.
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
                            verifyReq.Trigger = VerifyOrderTrigger.payment_flow.ToString();
                            var verifyResponseAtCancel = await VerifyOrderImplAsync(
                                orderRequest,
                                verifyReq,
                                _accessTokenProvider.AccessToken,
                                Noctua.Auth.RecentAccount?.Player?.Id,
                                true
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
                            _log.Warning($"Failed to verify order at cancelation: {e.Message}");
                            // TODO Do we really need to retry the canceled purchase?
                            // What if the user is accidentally tap the close button instead of complete?
                            _log.Exception(e);
                            EnqueueToRetryPendingPurchases(
                                new PurchaseItem
                                {
                                    OrderId = orderResponse.Id,
                                    OrderRequest = orderRequest,
                                    VerifyOrderRequest = verifyReq,
                                    AccessToken = _accessTokenProvider.AccessToken,
                                    PlayerId = Noctua.Auth.RecentAccount?.Player?.Id,
                                }
                            );
                        }

                        if (_enabledPaymentTypes.Count > 1 &&
                        !verifiedAtCancelation &&
                        enforcedPaymentType == PaymentType.unknown &&
                        _enabledPaymentTypes[1] != paymentType
                        )
                        {
                            // Fallback to secondary payment option.
                            return await PurchaseItemImplAsync(purchaseRequest, true);
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
                            _log.Info("custom payment is actually get canceled, but there is no secondary payment. Remove from pending queue.");
                            RemoveFromRetryPendingPurchasesByOrderID(orderResponse.Id);
                            paymentResult = new PaymentResult{
                                Status = PaymentStatus.Canceled,
                                Message = "Purchase canceled"
                            };
                            // TODO Do we really need to retry the canceled purchase?
                            // What if the user is accidentally tap the close button instead of complete?
                            EnqueueToRetryPendingPurchases(
                                new PurchaseItem
                                {
                                    OrderId = orderResponse.Id,
                                    OrderRequest = orderRequest,
                                    VerifyOrderRequest = verifyReq,
                                    AccessToken = _accessTokenProvider.AccessToken,
                                    PlayerId = Noctua.Auth.RecentAccount?.Player?.Id,
                                }
                            );
                        }
                    }
                    else if (completeDialogResult == "native_payment")
                    {
#if UNITY_ANDROID || UNITY_IOS
                        return new PurchaseResponse
                        {
                            OrderId = 0,
                            Status = OrderStatus.fallback_to_native_payment,
                            Message = "Payment method changed",
                        };
#endif
                    }

                    // Native browser custom payment is using OrderId as ReceiptData
                    paymentResult.ReceiptData = orderResponse.Id.ToString();
                    
                    break;
                case PaymentType.unknown:
                    _log.Warning($"Unknown payment type");
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Unknown payment type");
                default:
                    _log.Warning($"Unsupported payment type");
                    throw new NoctuaException(NoctuaErrorCode.Payment, "Unsupported payment type " + paymentType);
            }

            // Clear up the payment flow instance
            _paymentTcs = null;

            // Assign the update value
            verifyOrderRequest.ReceiptId = paymentResult.ReceiptId;
            verifyOrderRequest.ReceiptData = paymentResult.ReceiptData;

            pendingPurchaseItem.OrderId = orderResponse.Id;
            pendingPurchaseItem.OrderRequest = orderRequest;
            pendingPurchaseItem.VerifyOrderRequest = verifyOrderRequest;
            pendingPurchaseItem.AccessToken = _accessTokenProvider.AccessToken;
            pendingPurchaseItem.PlayerId = Noctua.Auth.RecentAccount?.Player?.Id;

            _log.Info($"Purchase process was done, whatever the status. Store the data to pending purchase early before verifying.  Order ID: {orderId}");

            // Store early for Negative Payment Cases no #4
            EnqueueToRetryPendingPurchases(pendingPurchaseItem);

            _log.Info($"Check payment result status: {paymentResult.Status}");
            switch (paymentResult.Status)
            {
                case PaymentStatus.Confirmed:
                case PaymentStatus.Successful:
                case PaymentStatus.Pending:
                    _log.Warning($"Purchase status pending, will attempt to verify directly");
                    break;
                case PaymentStatus.PendingPurchaseOngoing:
                case PaymentStatus.ItemAlreadyOwned:
                    _log.Warning($"Purchase status ItemAlreadyOwned: {paymentResult.Status}, Message: {paymentResult.Message}");
                    await _failedPaymentDialog.Show(paymentResult.Status);
                    
                    throw new NoctuaException(NoctuaErrorCode.PaymentStatusItemAlreadyOwned, paymentResult.Message);
                case PaymentStatus.Canceled:
                    _log.Warning($"Purchase status Canceled: {paymentResult.Status}, Message: {paymentResult.Message}");
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
                
                    throw new NoctuaException(NoctuaErrorCode.PaymentStatusCanceled, $"payment status: {paymentResult.Status}, Message: {paymentResult.Message}");
                case PaymentStatus.IapNotReady:
                    _log.Warning($"Purchase status IAPNotReady: {paymentResult.Status}, Message: {paymentResult.Message}");
                    _uiFactory.ShowError(LocaleTextKey.IAPNotReady);

                    throw new NoctuaException(NoctuaErrorCode.PaymentStatusIapNotReady, $"payment status: {paymentResult.Status}, Message: {paymentResult.Message}");
                default:
                    _log.Warning($"Purchase status IAPFailed: {paymentResult.Status}, Message: {paymentResult.Message}");
                    _uiFactory.ShowError(LocaleTextKey.IAPFailed);
                
                    throw new NoctuaException(NoctuaErrorCode.Payment, $"payment status: {paymentResult.Status}, Message: {paymentResult.Message}");
            }

            _log.Info($"Verifying order: {verifyOrderRequest.Id} with receipt data: {verifyOrderRequest.ReceiptData}");
            
            VerifyOrderResponse verifyOrderResponse;

            try {
                _uiFactory.ShowLoadingProgress(true);

                verifyOrderRequest.Trigger = VerifyOrderTrigger.payment_flow.ToString();
                verifyOrderResponse = await RetryAsync(() => VerifyOrderImplAsync(
                        orderRequest,
                        verifyOrderRequest,
                        _accessTokenProvider.AccessToken,
                        Noctua.Auth.RecentAccount?.Player?.Id,
                        true
                    )
                );

                _uiFactory.ShowLoadingProgress(false);
            }
            catch (NoctuaException e)
            {
                _log.Warning($"Failed to verify order: {e.Message}");
                if ((NoctuaErrorCode)e.ErrorCode == NoctuaErrorCode.Networking)
                {
                    EnqueueToRetryPendingPurchases(
                        new PurchaseItem
                        {
                            OrderId = orderResponse.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "Network error",
                            PlayerId = Noctua.Auth.RecentAccount?.Player?.Id
                        }
                    );
                }

                // At this point the unpaired order is already paired with receipt data.
                if (!string.IsNullOrEmpty(verifyOrderRequest.ReceiptData))
                {
                    _log.Info($"Remove from unpaired orders since we have receipt data now: {orderRequest.ProductId}");
                    unpairedOrders.Remove(orderRequest.ProductId);
                    PlayerPrefs.SetString("NoctuaUnpairedOrders", JsonConvert.SerializeObject(unpairedOrders));
                    PlayerPrefs.Save();
                    _log.Info($"NoctuaUnpairedOrders: {JsonConvert.SerializeObject(unpairedOrders)}");
                }
                
                _uiFactory.ShowLoadingProgress(false);
                _log.Exception(e);
                _uiFactory.ShowError(e.Message);

                throw;
            }
            catch (Exception e) 
            {
                _log.Warning($"Failed to verify order: {e.Message}");
                _uiFactory.ShowLoadingProgress(false);
                _log.Exception(e);
                
                throw;
            }

            _log.Info($"Verify order status: {verifyOrderResponse.Status}");
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

                item.VerifyOrderRequest.Trigger = VerifyOrderTrigger.manual_retry.ToString();
                var verifyOrderResponse = await VerifyOrderImplAsync(
                    item.OrderRequest,
                    item.VerifyOrderRequest,
                    item.AccessToken,
                    item.PlayerId,
                    false
                );

                if (verifyOrderResponse.Status != OrderStatus.completed &&
                verifyOrderResponse.Status != OrderStatus.canceled &&
                verifyOrderResponse.Status != OrderStatus.refunded &&
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
                // Do not track purchase_verify_order_failed
                // as we don't want it to flood our data.
                EnqueueToRetryPendingPurchases(
                    new PurchaseItem
                    {
                        OrderId = item.OrderId,
                        OrderRequest = item.OrderRequest,
                        VerifyOrderRequest = item.VerifyOrderRequest,
                        AccessToken = item.AccessToken,
                        Status = "verification_failed",
                        PlayerId = Noctua.Auth.RecentAccount?.Player?.Id
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
                                shouldRetry = await _uiFactory.ShowRetryDialog($"{e.Message}. Please try again later.", "payment");
                            } else {
                                shouldRetry = await _uiFactory.ShowRetryDialog("Please check your internet connection.", "payment");
                            }
                            break;
                        default:
                            _log.Exception(e);
                            shouldRetry = await _uiFactory.ShowRetryDialog(e.Message, "payment");
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

                    bool shouldRetry = await _uiFactory.ShowRetryDialog(ex.Message, "payment");
                    if (!shouldRetry)
                    {
                        throw;
                    }
                }
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void HandleGooglePurchaseDone(GoogleBilling.PurchaseResult result)
        {
            _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback");

            if (_paymentTcs == null)
            {
                _log.Info("NoctuaIAPService.PurchaseItemAsync Find out the order ID pair...");

                HandleUnpairedPurchase(result);
            } else
            {
                _log.Info("NoctuaIAPService.PurchaseItemAsync paymentTcs (payment flow instance) is still exist, try to continue the payment flow");
                _paymentTcs.TrySetResult(GetPlaystorePaymentResult(result));
            }
        }

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

        private void HandleUnpairedPurchase(GoogleBilling.PurchaseResult result)
        {
            var productId = result.ProductId;
            _log.Info($"NoctuaIAPService.HandleUnpairedPurchase Try to find the purchase token in pending purchase first to avoid duplicate token {result.ReceiptData}.");
            var foundInPendingPurchases = false;
            var foundInPurchaseHistory = false;
            var foundUnpairedOrder = false;

            var pendingPurchases = GetPendingPurchases().ToList();
            foreach (var pendingPurchase in pendingPurchases)
            {
                _log.Debug($"pending purchase receipt data for order ID {pendingPurchase.VerifyOrderRequest.Id} : {pendingPurchase.VerifyOrderRequest.ReceiptData}");
                _log.Debug($"play billing purchase update receipt data: {result.ReceiptData}");
                if (pendingPurchase.VerifyOrderRequest != null &&
                    !string.IsNullOrEmpty(pendingPurchase.VerifyOrderRequest.ReceiptData) &&
                    pendingPurchase.VerifyOrderRequest.ReceiptData == result.ReceiptData
                )
                {
                    _log.Info($"NoctuaIAPService.HandleUnpairedPurchase Found pending purchase with the same receipt data: {pendingPurchase.VerifyOrderRequest.ReceiptData}");
                    foundInPendingPurchases = true;
                    // Verify right now
                    pendingPurchase.VerifyOrderRequest.Trigger = VerifyOrderTrigger.payment_flow.ToString();
                    VerifyOrderImplAsync(
                        pendingPurchase.OrderRequest,
                        pendingPurchase.VerifyOrderRequest,
                        pendingPurchase.AccessToken,
                        pendingPurchase.PlayerId,
                        false
                    ).Forget();

                    break;
                }
            }
            if (foundInPendingPurchases)
            {
                return;
            }

            var purchaseHistory = GetPurchaseHistory().ToList();
            foreach (var purchaseItem in purchaseHistory)
            {
                _log.Debug($"purchase history receipt data for order ID {purchaseItem.VerifyOrderRequest.Id} : {purchaseItem.VerifyOrderRequest.ReceiptData}");
                _log.Debug($"play billing purchase update receipt data: {result.ReceiptData}");
                if (purchaseItem.VerifyOrderRequest != null &&
                    !string.IsNullOrEmpty(purchaseItem.VerifyOrderRequest.ReceiptData) &&
                    purchaseItem.VerifyOrderRequest.ReceiptData == result.ReceiptData
                )
                {
                    _log.Info($"NoctuaIAPService.HandleUnpairedPurchase Found purchase history with the same receipt data: {purchaseItem.VerifyOrderRequest.ReceiptData}");
                    foundInPurchaseHistory = true;
                    // Verify right now
                    purchaseItem.VerifyOrderRequest.Trigger = VerifyOrderTrigger.payment_flow.ToString();
                    VerifyOrderImplAsync(
                        purchaseItem.OrderRequest,
                        purchaseItem.VerifyOrderRequest,
                        purchaseItem.AccessToken,
                        purchaseItem.PlayerId,
                        false
                    ).Forget();

                    break;
                }
            }
            if (foundInPurchaseHistory)
            {
                return;
            }


            _log.Info($"NoctuaIAPService.HandleUnpairedPurchase Not found in pending purchase and purchase history, continue to try to find out the order ID pair for {productId}");
            var unpairedOrdersJson = PlayerPrefs.GetString("NoctuaUnpairedOrders", "{}");
            _log.Info($"NoctuaIAPService.HandleUnpairedPurchase unpaired orders: {unpairedOrdersJson}");
            Dictionary<string, PurchaseItem> unpairedOrders;
            try
            {
                unpairedOrders = JsonConvert.DeserializeObject<Dictionary<string, PurchaseItem>>(unpairedOrdersJson);
                if (unpairedOrders == null)
                {
                    unpairedOrders = new Dictionary<string, PurchaseItem>();
                }
            }
            catch (Exception e)
            {
                _log.Error($"NoctuaIAPService.HandleUnpairedPurchase Failed to parse unpaired orders: {e}");
                _log.Info($"NoctuaIAPService.HandleUnpairedPurchase Create empty unpairedOrders array");
                unpairedOrders = new Dictionary<string, PurchaseItem>();
            }

            if (unpairedOrders.TryGetValue(productId, out var pendingPurchaseItem))
            {

                pendingPurchaseItem.VerifyOrderRequest.ReceiptData = result.ReceiptData;

                _log.Info($"NoctuaIAPService.HandleUnpairedPurchase Found unpaired order for product ID: {productId}, Order ID: {pendingPurchaseItem.OrderId}, ReceiptData: {pendingPurchaseItem.VerifyOrderRequest.ReceiptData}");
                foundUnpairedOrder = true;
                EnqueueToRetryPendingPurchases(pendingPurchaseItem);

                // Remove from unpaired orders since we have receipt data now
                unpairedOrders.Remove(productId);
                PlayerPrefs.SetString("NoctuaUnpairedOrders", JsonConvert.SerializeObject(unpairedOrders));
                PlayerPrefs.Save();
                _log.Info($"NoctuaIAPService.HandleUnpairedPurchase NoctuaUnpairedOrders: {JsonConvert.SerializeObject(unpairedOrders)}");

                // Verify right now, don't wait
                pendingPurchaseItem.VerifyOrderRequest.Trigger = VerifyOrderTrigger.payment_flow.ToString();
                VerifyOrderImplAsync(
                    pendingPurchaseItem.OrderRequest,
                    pendingPurchaseItem.VerifyOrderRequest,
                    pendingPurchaseItem.AccessToken,
                    pendingPurchaseItem.PlayerId,
                    false
                ).Forget();
            }

            if (!foundInPurchaseHistory && !foundInPurchaseHistory && !foundUnpairedOrder) {
                _log.Warning($"NoctuaIAPService.HandleUnpairedPurchase No unpaired order or pending purchase found for receipt data {result.ReceiptData}. Store to backend.");
                var unpairedPurchaseRequest = new UnpairedPurchaseRequest
                {
                    ReceiptData = result.ReceiptData,
                    PaymentType = PaymentType.playstore, // This is always about playstore
                    ProductId = result.ProductId,
                    Currency = Noctua.Platform.Locale.GetCurrency(),
                };

                try
                {
                    CreateUnpairedPurchaseAsync(unpairedPurchaseRequest); // Async, but don't wait.
                }
                catch (Exception e)
                {
                    _log.Error("NoctuaIAPService.HandleUnpairedPurchase failed to create unpaired purchase: " + e);
                }
            }
        }

        private void HandleGoogleQueryPurchasesDone(GoogleBilling.PurchaseResult[] results)
        {
            _log.Info("NoctuaIAPService.QueryPurchasesAsync callback");

            if (results == null)
            {
                _log.Info("NoctuaIAPService.QueryPurchasesAsync callback result is null, do nothing.");
                return;
            }

            if (_paymentTcs != null)
            {
                _log.Info("NoctuaIAPService.QueryPurchasesAsync payment flow still running, abort to avoid race condition.");
                return;
            }

            _log.Info("NoctuaIAPService.QueryPurchasesAsync No payment task is running, try to find out the order ID pair...");

            foreach (var result in results)
            {
                HandleUnpairedPurchase(result);
            }
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
                $"token: {result.ReceiptData}, " +
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
                    Message = $"Item with purchase token '{result.ReceiptData}' already owned",
                    ReceiptId = result.ReceiptId,
                    ReceiptData = result.ReceiptData,
                };
            }

            if (result.ErrorCode != GoogleBilling.BillingErrorCode.OK)
            {

                var paymentStatus = PaymentStatus.Failed;
                if (result.ErrorCode == GoogleBilling.BillingErrorCode.ServiceDisconnected)
                {
                    paymentStatus = PaymentStatus.IapNotReady;
                }

                return new PaymentResult
                {
                    Status = paymentStatus,
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
                    Message = $"Purchase with purchase token '{result.ReceiptData}' is pending",
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

        private void SavePendingPurchases(List<PurchaseItem> orders)
        {
            _log.Debug("save pending purchases to player prefs");
            var updatedJson = JsonConvert.SerializeObject(orders);
            PlayerPrefs.SetString("NoctuaPendingPurchases", updatedJson);
            PlayerPrefs.Save();

            // Leave it here for debuggin purpose.
            //GetPendingPurchases();
        }

        public List<PurchaseItem> GetPendingPurchases()
        {
            _log.Info("Noctua.GetPendingPurchases");
            var json = PlayerPrefs.GetString("NoctuaPendingPurchases", string.Empty);
            _log.Info($"Pending purchases data: {json}");

            if (string.IsNullOrEmpty(json))
            {
                return new List<PurchaseItem>();
            }

            try
            {
                var pendingPurchases = JsonConvert.DeserializeObject<List<PurchaseItem>>(json);
                if (pendingPurchases == null)
                {
                    pendingPurchases = new List<PurchaseItem>();
                }
                
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
                
                return new List<PurchaseItem>();
            }
        }

        public PurchaseItem GetPendingPurchaseByOrderId(int orderId)
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
                var pendingPurchases = JsonConvert.DeserializeObject<List<PurchaseItem>>(json);
                if (pendingPurchases == null)
                {
                    pendingPurchases = new List<PurchaseItem>();
                }

                var list = pendingPurchases
                    .Where(p => p.VerifyOrderRequest != null && p.AccessToken != null)
                    .ToList();

                var result = new PurchaseItem();
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
            
            if (_enabledPaymentTypes == null || _enabledPaymentTypes.Count == 0)
            {
                _log.Warning("no payment types enabled, quitting");
                
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
                var failedPendingPurchases = new List<PurchaseItem>();
                
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

                        item.VerifyOrderRequest.Trigger = VerifyOrderTrigger.client_automatic_retry.ToString();
                        var verifyOrderResponse = await VerifyOrderImplAsync(
                            item.OrderRequest,
                            item.VerifyOrderRequest,
                            item.AccessToken,
                            item.PlayerId,
                            false
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
                        // Do not track purchase_verify_order_failed
                        // as we don't want it to flood our data.

                        EnqueueToRetryPendingPurchases(
                            new PurchaseItem
                            {
                                OrderId = item.OrderId,
                                OrderRequest = item.OrderRequest,
                                VerifyOrderRequest = item.VerifyOrderRequest,
                                AccessToken = item.AccessToken,
                                Status = "verification_failed",
                                PlayerId = Noctua.Auth.RecentAccount?.Player?.Id,
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

        private void EnqueueToRetryPendingPurchases(PurchaseItem item)
        {

            if (item.OrderId == 0)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Missing parameter when enqueue retry pending purchase item: orderId");
            }
            if (item.OrderRequest is null)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Missing parameter when enqueue retry pending purchase item: orderRequest");
            }
            if (item.VerifyOrderRequest is null)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Missing parameter when enqueue retry pending purchase item: verifyOrderRequest");
            }
            if (string.IsNullOrEmpty(item.AccessToken))
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Missing parameter when enqueue retry pending purchase item: accessToken");
            }
            if (item.PlayerId is null)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Missing parameter when enqueue retry pending purchase item: playerId");
            }

            // Remove the existing if any.
            var oldItem = GetThenRemoveFromRetryPendingPurchasesByOrderID(item.OrderId);

            if (oldItem != null && !string.IsNullOrEmpty(oldItem.VerifyOrderRequest?.ReceiptData))
            {
                item.VerifyOrderRequest.ReceiptData = oldItem.VerifyOrderRequest.ReceiptData;
                _log.Info($"Preserved ReceiptData for {item.OrderId}: {item.VerifyOrderRequest.ReceiptData}");
            }

            _log.Info($"Enqueue to retry pending purchase: {item.OrderId}");
            _waitingPendingPurchases.Enqueue(item);
            SavePendingPurchases(_waitingPendingPurchases.ToList());
        }

        public PurchaseItem GetThenRemoveFromRetryPendingPurchasesByOrderID(int orderId)
        {
            _log.Info($"Remove from retry pending purchase: {orderId}");

            var oldItem = _waitingPendingPurchases.FirstOrDefault(item => item.OrderId == orderId);
            if (oldItem == null)
            {
                _log.Warning($"No pending purchase found with order ID: {orderId}");
                return new PurchaseItem();
            } else {
                // Rebuild the queue excluding the item with the specified OrderID
                var updatedQueue = new Queue<PurchaseItem>(
                    _waitingPendingPurchases.Where(item => item.OrderId != orderId));
                _waitingPendingPurchases.Clear();
                foreach (var item in updatedQueue)
                {
                    _waitingPendingPurchases.Enqueue(item);
                }
                SavePendingPurchases(_waitingPendingPurchases.ToList());
            }

            return oldItem;
        }

        public void RemoveFromRetryPendingPurchasesByOrderID(int orderId)
        {
            _log.Info($"Remove from retry pending purchase: {orderId}");

            // Rebuild the queue excluding the item with the specified OrderID
            var updatedQueue = new Queue<PurchaseItem>(
                _waitingPendingPurchases.Where(item => item.OrderId != orderId));

            _waitingPendingPurchases.Clear();
            foreach (var item in updatedQueue)
            {
                _waitingPendingPurchases.Enqueue(item);
            }
            
            SavePendingPurchases(_waitingPendingPurchases.ToList());
        }

        public List<PurchaseItem> GetPurchaseHistory()
        {
            _log.Info("Noctua.GetPurchaseHistory");
            var json = PlayerPrefs.GetString("NoctuaPurchaseHistory", string.Empty);
            _log.Info($"PurchaseHistory data: {json}");

            if (string.IsNullOrEmpty(json))
            {
                return new List<PurchaseItem>();
            }

            try
            {
                var purchaseHistory = JsonConvert.DeserializeObject<List<PurchaseItem>>(json);
                if (purchaseHistory == null)
                {
                    purchaseHistory = new List<PurchaseItem>();
                }
                var list = purchaseHistory
                    .Where(p => p.VerifyOrderRequest != null && p.AccessToken != null)
                    .ToList();
                list.Sort((p1, p2) => p1.OrderId.CompareTo(p2.OrderId));

                return list;
            }
            catch (Exception e)
            {
                _log.Error("Failed to parse purchase history: " + e);
                PlayerPrefs.DeleteKey("NoctuaPurchaseHistory");
                return new List<PurchaseItem>();
            }
        }

        private void AddToPurchaseHistory(PurchaseItem item)
        {

            if (item.OrderId == 0)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Missing parameter when add new purchase history item: orderId");
            }
            if (item.OrderRequest is null)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Missing parameter when add new purchase history item: orderRequest");
            }
            if (item.VerifyOrderRequest is null)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Missing parameter when add new purchase history item: verifyOrderRequest");
            }
            if (string.IsNullOrEmpty(item.AccessToken))
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Missing parameter when add new purchase history item: accessToken");
            }
            if (item.PlayerId is null)
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Missing parameter when add new purchase history item: playerId");
            }

            // Remove the existing if any.
            RemoveFromPurchaseHistoryByOrderID(item.OrderId);
            var list = GetPurchaseHistory();
            list.Add(item);
            SavePurchaseHistory(list);
        }

        private void SavePurchaseHistory(List<PurchaseItem> orders)
        {
            _log.Debug("save pending purchases to player prefs");
            var updatedJson = JsonConvert.SerializeObject(orders);
            PlayerPrefs.SetString("NoctuaPurchaseHistory", updatedJson);
            PlayerPrefs.Save();

            // Leave it here for debuggin purpose.
            //GetPurchaseHistory();
        }

        public void RemoveFromPurchaseHistoryByOrderID(int orderId)
        {
            _log.Info($"Remove from purchase history: {orderId}");

            var oldList = GetPurchaseHistory();
            var newList = oldList.Where(item => item.OrderId != orderId);
            SavePurchaseHistory(newList.ToList());
        }

        public async Task<bool> GetPurchaseStatusAsync(string productId)
        {
            try
            {
                bool result = await CheckIfProductPurchasedAsync(productId);
                return result;
            }
            catch (NoctuaException e)
            {
                _log.Error("NoctuaIAPService.GetPurchaseStatusAsync failed: " + e);
                return false;
            }
        }

        // Get purchase status for a batch of product IDs
        public async Task<List<string>> GetPurchasedProductsAsync(List<string> productIds)
        {
            var tasks = productIds.ToDictionary(
                productId => productId,
                productId => GetPurchaseStatusAsync(productId)
            );

            await Task.WhenAll(tasks.Values);

            var purchased = new List<string>();
            foreach (var kvp in tasks)
            {
                if (await kvp.Value) // true = purchased
                {
                    purchased.Add(kvp.Key);
                }
            }

            return purchased;
        }

        private Task<bool> CheckIfProductPurchasedAsync(string productId)
        {
            var tcs = new TaskCompletionSource<bool>();

            CheckIfProductPurchased(productId, (result) =>
            {
                Debug.Log("CheckIfProductPurchased result: " + result);
                tcs.SetResult(result);
            });

            return tcs.Task;
        }
        
        //Keep this method public for compatibility backwards
        public void CheckIfProductPurchased(string productId, System.Action<bool> callback)
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.GetPurchasedProductById(productId, (purchase) =>
            {
                if (purchase != null && purchase.Success)
                {
                    Debug.Log($"[GoogleBilling] Product purchased: {purchase.ProductId}, State: {purchase.PurchaseState}");
                    callback?.Invoke(true);
                }
                else
                {
                    Debug.Log($"[GoogleBilling] Product '{productId}' is not purchased or not found.");
                    callback?.Invoke(false);
                }
            });
            #elif UNITY_IOS && !UNITY_EDITOR
            _nativePlugin.GetProductPurchasedById(productId, (hasPurchased) =>
            {
                Debug.Log($"[IosPlugin] Product '{productId}' purchased: {hasPurchased}");
                callback?.Invoke(hasPurchased);
            });
            #else
            Debug.LogWarning("CheckIfProductPurchased is not supported on this platform.");
            callback?.Invoke(false);
            #endif
        }

        [Preserve]
        internal class Config
        {
            public string BaseUrl;
            public string ClientId;
            public bool isIAPDisabled;
        }

        [Preserve]
        public class PurchaseItem
        {
            public int OrderId;
            public OrderRequest OrderRequest;
            public VerifyOrderRequest VerifyOrderRequest;
            public string AccessToken;
            public string Status;
            public long? PlayerId;
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
