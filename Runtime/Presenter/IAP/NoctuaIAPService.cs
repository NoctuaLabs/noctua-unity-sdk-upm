using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
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


namespace com.noctuagames.sdk
{
    /// <summary>
    /// In-app purchase service: handles product listing, purchases, pending receipts, and verification.
    /// </summary>
    [Preserve]
    public class NoctuaIAPService : IIAPService
    {
        private readonly Config _config;
        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaIAPService));

        private TaskCompletionSource<string> _activeCurrencyTcs;
        // Serializes Android active-currency queries: _activeCurrencyTcs is a single
        // shared slot consumed by HandleGoogleProductDetails, so concurrent queries
        // would overwrite each other's completion source.
        private readonly SemaphoreSlim _activeCurrencyGate = new(1, 1);
        // Deadline for the native store query that resolves the active currency — Google Play's
        // product-details query on Android, StoreKit on iOS. Chosen to sit just under the 5s
        // IAP-ready guard in Noctua.Initialization.cs so a silent store cannot dominate startup.
        // Neither platform guarantees a response time for these calls, so owning the deadline is
        // the app's responsibility.
        private const int ActiveCurrencyTimeoutMs = 3000;
        // Whether Init() has driven the native billing connection at least once. Distinguishes
        // "never started" (needs Init) from "started but not connected" (needs a reconnect).
        private bool _billingInitCalled;

        /// <summary>
        /// Fired when a purchase flow completes and an OrderRequest should be processed by game.
        /// </summary>
        public event Action<OrderRequest> OnPurchaseDone;
        /// <summary>
        /// Fired when a purchase verification fails and the order is placed in a pending state for retry.
        /// </summary>
        public event Action<OrderRequest> OnPurchasePending;

        private readonly IEventSender _eventSender;
        private readonly AccessTokenProvider _accessTokenProvider;
        // Retry queue for orders awaiting verification. Mirrors the "NoctuaPendingPurchases"
        // PlayerPrefs entry, which is the persisted source of truth; loaded lazily so the first
        // write after launch does not overwrite orders persisted by an earlier session.
        private readonly Queue<InternalPurchaseItem> _waitingPendingPurchases = new();
        private bool _pendingPurchaseQueueLoaded;
        private bool _pendingPurchaseRetryLoopRunning;
        private readonly HashSet<int> _verifyingOrderIds = new();
        // Orders already reported through OnPurchasePending this session.
        private readonly HashSet<int> _pendingNotifiedOrderIds = new();
        // verify-order error: the receipt is attached to a different order that owns the payment.
        private const int ReceiptAlreadyUsedErrorCode = 2042;
        private static readonly TimeSpan PendingPurchaseIdlePollInterval = TimeSpan.FromSeconds(1);
        private readonly INativePlugin _nativePlugin;
        private readonly ProductList _usdProducts = new();
        private TaskCompletionSource<PaymentResult> _paymentTcs;
        // Serializes the user-facing payment flow. _paymentTcs is a single shared slot
        // (and doubles as the "payment flow running" flag for Google callbacks), so two
        // concurrent purchases would overwrite each other's completion source. Queued
        // callers wait their turn instead of racing.
        private readonly SemaphoreSlim _purchaseFlowGate = new(1, 1);
#if UNITY_IOS && !UNITY_EDITOR
        private const string UnpairedOrdersKey = "NoctuaUnpairedOrders";
#endif

#if UNITY_ANDROID && !UNITY_EDITOR
        private readonly GoogleBilling GoogleBillingInstance = new();
        // Purchase tokens HandleUnpairedPurchase is currently handling. Play delivers the same
        // purchases again on every QueryPurchases pass, and passes overlap at startup; without this
        // each pass minted its own order for the same token.
        private readonly HashSet<string> _unpairedPurchasesInFlight = new();
        private readonly object _unpairedPurchasesInFlightLock = new();
        // Tokens the server already rejected because another order owns the receipt. Re-pairing
        // them after a restart can only mint another dead-end duplicate order.
        private const string SettledUnpairedPurchaseTokensKey = "NoctuaSettledUnpairedPurchaseTokens";
        // By default all major payment types are enabled, then it will be overridden by the server config at SDK init
        private List<PaymentType> _enabledPaymentTypes = new()
            { PaymentType.playstore, PaymentType.noctuastore };
#elif UNITY_IOS && !UNITY_EDITOR
        // By default all major payment types are enabled, then it will be overridden by the server config at SDK init
        private List<PaymentType> _enabledPaymentTypes = new()
            { PaymentType.appstore, PaymentType.noctuastore };
#else
        private List<PaymentType> _enabledPaymentTypes = new()
            { PaymentType.editor, PaymentType.noctuastore };
#endif
        private readonly IPaymentUI _paymentUI;
        private readonly IAuthProvider _authProvider;
        private readonly ILocaleProvider _localeProvider;
        private readonly IConnectivityProvider _connectivity;
        private bool _enabled;
        private string _distributionPlaftorm;
        private IAPTaichiConfig _taichiConfig;
        // Cumulative IAP revenue in the product's own (local) currency — both the threshold decision
        // and the reported value use this amount.
        private const string KeyIAPTotalRevenue = "Noctua_Taichi_IAPTotalRevenue";
        // Currency of the amount stored in KeyIAPTotalRevenue. The accumulator sums raw local amounts,
        // so mixing currencies is meaningless — this lets us warn when a later purchase arrives in a
        // different currency than what is already accumulated.
        private const string KeyIAPRevenueCurrency = "Noctua_Taichi_IAPRevenueCurrency";

        /// <summary>
        /// Internal constructor for Noctua IAP service.
        /// </summary>
        /// <param name="config">IAP service configuration (clientId, flags).</param>
        /// <param name="accessTokenProvider">Provider used to attach access tokens.</param>
        /// <param name="uiFactory">UI factory for purchase dialogs.</param>
        /// <param name="nativePlugin">Platform native plugin for store integration.</param>
        /// <param name="eventSender">Optional event sender for telemetry.</param>
        public NoctuaIAPService(
            Config config,
            AccessTokenProvider accessTokenProvider,
            IPaymentUI paymentUI,
            INativePlugin nativePlugin,
            IEventSender eventSender = null,
            IAuthProvider authProvider = null,
            ILocaleProvider localeProvider = null,
            IConnectivityProvider connectivity = null
        )
        {
            _config = config;
            _accessTokenProvider = accessTokenProvider;
            _eventSender = eventSender;

            _paymentUI = paymentUI;
            _authProvider = authProvider;
            _localeProvider = localeProvider;
            _connectivity = connectivity;

#if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.OnProductDetailsDone += HandleGoogleProductDetails;
            GoogleBillingInstance.OnPurchaseDone += HandleGooglePurchaseDone;
            GoogleBillingInstance.OnQueryPurchasesDone += HandleGoogleQueryPurchasesDone;
#endif
            _nativePlugin = nativePlugin;

#if UNITY_IOS && !UNITY_EDITOR
            _nativePlugin?.SetUnsolicitedPurchaseHandler(HandleUnsolicitedAppStorePurchase);
#endif
        }
        
        /// <summary>
        /// Indicates whether the native IAP subsystem (e.g., Google Billing) is ready.
        /// </summary>
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

        /// <summary>
        /// Initialize the underlying native billing system (no-op on non-Android platforms).
        /// </summary>
        internal void Init()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.Init();
            _paymentTcs = null;
            _billingInitCalled = true;
#endif
        }

        /// <summary>
        /// Ensures the native billing client is connected, re-driving the Play connection when an
        /// earlier attempt failed. Safe to call repeatedly; does nothing once billing is ready.
        /// </summary>
        /// <remarks>
        /// Calling <see cref="Init"/> again cannot recover a client that never connected. The
        /// native BillingService sets <c>isInitialized = true</c> as soon as
        /// <c>startConnection()</c> is *called*, not when it succeeds, so every later
        /// <c>initialize()</c> returns early with "already initialized" even though the connection
        /// attempt failed. Google Play Billing's own auto-reconnection does not cover this case
        /// either: it re-establishes a connection that was made and later severed, not one that
        /// never succeeded. Reconnecting explicitly is the only way back.
        ///
        /// Without this, a session that starts while Google Play is unavailable (user not signed
        /// in, Play Store disabled or blocked — frequently observed on OPPO/ColorOS) can never
        /// complete a purchase for the rest of that session, even after the user signs in. It
        /// fails with "no payment types enabled" without ever reaching Google Play.
        /// </remarks>
        internal void EnsureBillingConnected()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (IsReady)
            {
                return;
            }

            // First attempt: start the connection. Reconnecting in the same breath would re-enter
            // startConnection() while the initial attempt is still in flight.
            if (!_billingInitCalled)
            {
                Init();

                return;
            }

            _log.Info("EnsureBillingConnected: billing client not ready, reconnecting to Google Play");
            GoogleBillingInstance.ReconnectBilling();
#endif
        }

        /// <summary>
        /// The configured payment types that are actually usable right now, in priority order.
        /// </summary>
        /// <remarks>
        /// Native store payment (Play / App Store) needs a live billing connection, so that check
        /// belongs at the moment of use rather than once at startup. Evaluating it at init made a
        /// transient condition permanent: if Google Play was unreachable at launch, the store
        /// payment type was removed from the session's list for good, and the user could not
        /// purchase for the rest of that session even after Play recovered — the purchase failed
        /// with "no payment types enabled" without ever reaching Google Play.
        ///
        /// Returns a new list rather than mutating the configured one, so the server-provided
        /// configuration stays intact and a later call sees the recovered state.
        /// </remarks>
        private List<PaymentType> GetAvailablePaymentTypes()
        {
            if (IsReady)
            {
                return _enabledPaymentTypes;
            }

            return _enabledPaymentTypes
                .Where(pt => pt != PaymentType.playstore && pt != PaymentType.appstore)
                .ToList();
        }

        /// <summary>
        /// Sets enabled payment types with order representing priority.
        /// </summary>
        /// <param name="enabledPaymentTypes">List of allowed payment types (priority first).</param>
        public void SetEnabledPaymentTypes(List<PaymentType> enabledPaymentTypes)
        {
            // The sequence represent the priority.
            _enabledPaymentTypes = enabledPaymentTypes;
        }

        /// <summary>
        /// Set distribution platform identifier (e.g., "google_play" or "direct").
        /// </summary>
        /// <param name="platform">Distribution platform string.</param>
        public void SetDistributionPlatform(string platform)
        {
            // The sequence represent the priority.
            _distributionPlaftorm = platform;
        }

        public void SetIAPTaichiConfig(IAPTaichiConfig config) => _taichiConfig = config;

        /// <summary>
        /// Fetch list of products available for purchase from server.
        /// </summary>
        /// <param name="currency">Optional currency to filter products. If null, uses platform locale currency.</param>
        /// <param name="platformType">Optional platform type override (e.g., "playstore").</param>
        /// <returns>List of products returned by server.</returns>
        /// <exception cref="Exception">Thrown when player is not authenticated or game id missing.</exception>
        public async UniTask<ProductList> GetProductListAsync(string currency = null, string platformType = null)
        {
            EnsureEnabled();

            _log.Debug("calling API");

            var recentAccount = _authProvider?.RecentAccount;

            if (recentAccount?.Player?.GameId == null || recentAccount.Player.GameId <= 0)
            {
                throw new Exception("Game ID not found or invalid. Please authenticate first");
            }

            string gameId = recentAccount.Player.GameId.ToString();

            if (string.IsNullOrEmpty(currency))
            {
                currency = _localeProvider?.GetCurrency() ?? "USD";
            }

            // Filter out 'editor' — it's a local-only mock type, not recognized by the server
            string enabledPaymentTypes = string.Join(",",
                GetAvailablePaymentTypes().Where(pt => pt != PaymentType.editor)).ToLower();

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
                .WithHeader("X-GAME-VERSION", Application.version)
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

        private async UniTask<RedeemOrderResponse> CreateRedeemOrderAsync(RedeemOrderRequest purchase)
        {
            var url = $"{_config.BaseUrl}/redeems";

            var request = new HttpRequest(HttpMethod.Post, url)
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken)
                .WithJsonBody(purchase);

            var response = await request.Send<RedeemOrderResponse>();

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

        /// <summary>
        /// Maps a verify-order backend error code to the <see cref="OrderStatus"/> that should
        /// drive client-side handling. Returns <c>null</c> for unrecognized codes, which leaves
        /// the response status at its default (<see cref="OrderStatus.unknown"/>) so the order
        /// falls into the generic "verification failed, retry later" path.
        ///
        /// Pure/static so the mapping can be unit-tested directly without standing up HTTP or
        /// native-plugin dependencies — see <c>NoctuaIAPServiceTest.cs</c>.
        /// </summary>
        public static OrderStatus? MapVerifyOrderErrorCodeToStatus(int errorCode)
        {
            switch (errorCode)
            {
            case ReceiptAlreadyUsedErrorCode:
                // Backend's IsReceiptDataAlreadyUsed check excludes the order being verified
                // (WHERE id != $1 AND receipt_data = $2), so this does NOT mean "this order was
                // already completed" — it means this receipt string is attached to a *different*
                // order id, which already owns (and likely already completed with) it. This
                // order is a dead-end duplicate — usually created by HandleUnpairedPurchase
                // re-pairing the same receipt across an app restart — that can never itself be
                // verified. Map to voided (not completed) so it's dropped from the retry queue
                // without firing OnPurchaseDone/completion side effects, which would risk
                // double-crediting the player for a purchase the *other* order already delivered.
                return OrderStatus.voided;
            case 2043:
                return OrderStatus.pending;
            case 2044:
                return OrderStatus.verification_failed;
            case 2045:
                return OrderStatus.delivery_callback_failed;
            case 2046:
                return OrderStatus.canceled;
            case 2047:
                return OrderStatus.refunded;
            case 2048:
                return OrderStatus.voided;
            default:
                return null;
            }
        }

        /// <summary>
        /// Decides whether an unpairable Google Play purchase is allowed to be treated as a
        /// promo-code redemption by <see cref="HandleUnpairedPurchase"/>.
        ///
        /// Google Play leaves <c>Purchase.getOrderId()</c> empty for purchases acquired with a
        /// promo code; only paid purchases carry an order id (the "GPA.xxxx-xxxx-xxxx-xxxxx"
        /// string surfaced as <see cref="GoogleBilling.PurchaseResult.ReceiptId"/>). So a
        /// non-empty receipt id is positive proof the purchase was actually paid for, and
        /// minting a $0 redeem order for it would attach a second order to a real payment —
        /// the player can then be credited twice, once by the paid order's S2S delivery and
        /// once by the redeem order's local OnPurchaseDone.
        ///
        /// Failing to pair such a purchase locally means local state was lost (reinstall,
        /// cleared app data, new device), not that the purchase was a redemption. Those belong
        /// in the unpaired-purchase reconciliation queue instead.
        ///
        /// Pure/static so the rule can be unit-tested without HTTP or native-plugin
        /// dependencies — see <c>NoctuaIAPServiceTest.cs</c>.
        /// </summary>
        /// <param name="receiptId">The Google Play order id, or null/empty when absent.</param>
        /// <returns><c>true</c> only when the purchase carries no order id.</returns>
        public static bool CanTreatUnpairedPurchaseAsRedeem(string receiptId)
        {
            return string.IsNullOrWhiteSpace(receiptId);
        }

        // Google Play's Purchase.PurchaseState.PENDING. GoogleBilling's own enum is Android-only,
        // so the pure rule below takes the raw value to stay unit-testable in the Editor.
        private const int GooglePlayPurchaseStatePending = 2;

        /// <summary>
        /// Most recent settled unpaired purchase tokens kept on device: filed for reconciliation or
        /// rejected by the server. Settled tokens are never turned into another order or report.
        /// </summary>
        public const int MaxSettledUnpairedPurchaseTokens = 100;

        /// <summary>
        /// Whether an unpaired Google Play purchase may be turned into an order (redeem order or
        /// unpaired-purchase report).
        ///
        /// Google documents <c>Purchase.getOrderId()</c> as null while a purchase is PENDING (for
        /// example a QRIS pay-later purchase awaiting payment), so a pending purchase whose local
        /// pairing state was lost passes <see cref="CanTreatUnpairedPurchaseAsRedeem"/> and would be
        /// minted as a $0 redeem order for a purchase nobody has paid for. It is handled instead
        /// when Play re-delivers it in the PURCHASED state.
        ///
        /// UNSPECIFIED is allowed so a bridge that does not report the state keeps the existing
        /// behaviour.
        /// </summary>
        /// <param name="purchaseState">Google Play purchase state: 0 unspecified, 1 purchased, 2 pending.</param>
        /// <returns><c>false</c> only for a PENDING purchase.</returns>
        public static bool CanCreateOrderForUnpairedPurchase(int purchaseState)
        {
            return purchaseState != GooglePlayPurchaseStatePending;
        }

        /// <summary>
        /// Returns a new token list with <paramref name="token"/> recorded as the most recent entry:
        /// de-duplicated and capped to the <paramref name="max"/> most recent tokens. The input is
        /// never modified.
        /// </summary>
        /// <param name="tokens">Previously recorded tokens, oldest first. May be null.</param>
        /// <param name="token">Token to record. Null or empty is ignored.</param>
        /// <param name="max">Maximum number of tokens to keep.</param>
        /// <returns>A new list, oldest first.</returns>
        public static List<string> WithSettledUnpairedPurchaseToken(
            IReadOnlyList<string> tokens,
            string token,
            int max = MaxSettledUnpairedPurchaseTokens)
        {
            var result = tokens == null ? new List<string>() : new List<string>(tokens);

            if (string.IsNullOrEmpty(token))
            {
                return result;
            }

            result.Remove(token);
            result.Add(token);

            return result.Count > max ? result.GetRange(result.Count - max, max) : result;
        }

        private async UniTask<VerifyOrderResponse> VerifyOrderImplAsync(
            OrderRequest orderRequest,
            VerifyOrderRequest verifyOrderRequest,
            string token,
            long? playerId,
            bool isTriggeredByIAP,
            string purchaseToken = null,
            [CallerMemberName] string callerMember = ""
        )
        {
            // Track in-flight verifications so the retry loop skips orders another caller
            // (usually the purchase flow) is verifying right now.
            var orderId = verifyOrderRequest?.Id ?? 0;
            var tracked = orderId != 0 && _verifyingOrderIds.Add(orderId);
            try
            {
                return await VerifyOrderCoreAsync(
                    orderRequest, verifyOrderRequest, token, playerId, isTriggeredByIAP, purchaseToken, callerMember);
            }
            finally
            {
                if (tracked)
                {
                    _verifyingOrderIds.Remove(orderId);
                }
            }
        }

        private async UniTask<VerifyOrderResponse> VerifyOrderCoreAsync(
            OrderRequest orderRequest,
            VerifyOrderRequest verifyOrderRequest,
            string token,
            long? playerId,
            bool isTriggeredByIAP,
            string purchaseToken,
            string callerMember
        )
        {
                _log.Debug($"Attempt to verify orderID {verifyOrderRequest.Id}, triggered by IAP: {isTriggeredByIAP}, caller: {callerMember}, trigger: {verifyOrderRequest?.Trigger}");

                if (orderRequest.Id == 0)
                {
                    throw new NoctuaException(NoctuaErrorCode.Payment, $": Invalid order ID: 0");
                }

                var verifyOrderResponse = new VerifyOrderResponse();
                verifyOrderResponse.Id = verifyOrderRequest.Id;
                var verifyOrderErrorMessage = "";
                var isDuplicateReceipt = false;
                try {
                verifyOrderResponse = await VerifyOrderAsync(verifyOrderRequest, token);
                }
                catch(Exception e)
                {
                    verifyOrderResponse = new VerifyOrderResponse();
                    verifyOrderResponse.Id = verifyOrderRequest.Id;
                    if (e is NoctuaException noctuaEx)
                    {
                        var mappedStatus = MapVerifyOrderErrorCodeToStatus(noctuaEx.ErrorCode);
                        if (mappedStatus.HasValue)
                        {
                            verifyOrderResponse.Status = mappedStatus.Value;
                            verifyOrderErrorMessage = e.Message;
                            isDuplicateReceipt = noctuaEx.ErrorCode == ReceiptAlreadyUsedErrorCode;
                        }
                        else if ((NoctuaErrorCode)noctuaEx.ErrorCode == NoctuaErrorCode.Networking)
                        {
                            // The server was not reached, so nothing is known about the payment.
                            // Keep the order queued and rethrow as Networking: converting it into a
                            // Payment error below made callers treat it as a failed payment.
                            _log.Warning($"VerifyOrderImplAsync network error, keeping order queued. orderID={verifyOrderRequest.Id}, caller={callerMember}, trigger={verifyOrderRequest?.Trigger}, error={e.Message}");
                            EnqueueToRetryPendingPurchases(
                                new InternalPurchaseItem
                                {
                                    OrderId = verifyOrderRequest.Id,
                                    OrderRequest = orderRequest,
                                    VerifyOrderRequest = verifyOrderRequest,
                                    AccessToken = _accessTokenProvider.AccessToken,
                                    Status = "network_error",
                                    PlayerId = _authProvider?.PlayerId,
                                    PurchaseToken = purchaseToken,
                                }
                            );
                            throw;
                        }
                    } else {
                        _log.Warning($"VerifyOrderImplAsync failed. orderID={verifyOrderRequest.Id}, caller={callerMember}, trigger={verifyOrderRequest?.Trigger}, isTriggeredByIAP={isTriggeredByIAP}, error={e.Message}");
                        throw;
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

                    var existingCompleted = GetPurchaseHistory()
                        .Any(item => item.OrderId == verifyOrderRequest.Id && item.Status == OrderStatus.completed.ToString());
                    if (existingCompleted)
                    {
                        _log.Debug($"orderID {verifyOrderRequest.Id} already exists in purchase history with completed status. Skipping duplicate handling.");

                        break;
                    }

                    AddToPurchaseHistory(
                        new InternalPurchaseItem
                        {
                            OrderId = verifyOrderRequest.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "completed",
                            PlayerId = _authProvider?.PlayerId,
                            // Lets a replayed StoreKit transaction be recognized as already delivered.
                            PurchaseToken = purchaseToken,
                        }
                    );

                    // Refund-tracking probe: GetPurchaseStatusAsync returns true only for
                    // non-consumables, so we use it to auto-detect product type. Consumables
                    // never make it into NoctuaRefundTracking and are never flagged refunded.
                    //
                    // Defense in depth: wrapped with a timeout. The native purchase-status
                    // check can, in rare cases, never call back (see the queue-based fix in
                    // GoogleBilling.cs / IosPlugin.cs for the primary fix to that). If it still
                    // doesn't return in time, skip refund tracking for this order rather than
                    // hanging the whole purchase-completion flow (and the caller's loading UI)
                    // forever.
                    if (orderRequest != null && !string.IsNullOrEmpty(orderRequest.ProductId))
                    {
                        try
                        {
                            var probeTask = GetPurchaseStatusAsync(orderRequest.ProductId);
                            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5));

                            if (await Task.WhenAny(probeTask, timeoutTask) != probeTask)
                            {
                                _log.Warning($"Refund-tracking probe timed out for '{orderRequest.ProductId}' after 5s; skipping.");
                            }
                            else
                            {
                                bool isNonConsumable = await probeTask;
                                if (isNonConsumable)
                                {
                                    SaveRefundTrackingEntry(orderRequest.ProductId, orderRequest.PaymentType);
                                }
                                else
                                {
                                    _log.Debug($"Refund-tracking probe: GetPurchaseStatusAsync('{orderRequest.ProductId}') returned false; treating as consumable, not tracking.");
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            _log.Warning($"Refund-tracking probe failed for '{orderRequest.ProductId}': {e.Message}");
                        }
                    }

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

                    SendFirstPurchaseEventIfFirstTime(orderRequest);

                    _nativePlugin?.TrackPurchase(
                        verifyOrderRequest.Id.ToString(),
                        (double)orderRequest.Price,
                        orderRequest.Currency
                    );

                    TrackTaichiIAP(orderRequest);

                    // Assign store pricing
                    orderRequest.StoreAmount = verifyOrderResponse.StoreAmount;
                    orderRequest.StoreCurrency = verifyOrderResponse.StoreCurrency;

                    _log.Debug($"Invoking OnPurchaseDone for orderID {orderRequest.Id} with store amount {orderRequest.StoreAmount} {orderRequest.StoreCurrency}");

                    OnPurchaseDone?.Invoke(orderRequest);

                    // Complete purchase processing on native iOS side
                    // (finishes SK1 transaction via finishTransaction)
                    // Android purchase completion is handled server-side, no client call needed.
#if UNITY_IOS && !UNITY_EDITOR
                    if (!string.IsNullOrEmpty(purchaseToken))
                    {
                        try
                        {
                            _log.Debug($"Calling CompletePurchaseProcessing for purchaseToken={purchaseToken}");
                            _nativePlugin?.CompletePurchaseProcessing(
                                purchaseToken,
                                NoctuaConsumableType.Consumable,
                                true,
                                success => _log.Debug($"CompletePurchaseProcessing result: {success}")
                            );
                        }
                        catch (Exception e)
                        {
                            _log.Warning($"CompletePurchaseProcessing failed: {e.Message}");
                        }
                    }
#endif

                    break;
                case OrderStatus.canceled:
                    _eventSender?.Send(
                        "purchase_cancelled",
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
                        new InternalPurchaseItem
                        {
                            OrderId = verifyOrderRequest.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "canceled",
                            PlayerId = _authProvider?.PlayerId,
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
                        new InternalPurchaseItem
                        {
                            OrderId = verifyOrderRequest.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "refunded",
                            PlayerId = _authProvider?.PlayerId,
                        }
                    );
                    break;
                case OrderStatus.voided:
                    _log.Debug("remove from pending queue because it has been voided");
                    RemoveFromRetryPendingPurchasesByOrderID(verifyOrderRequest.Id);

                    if (isDuplicateReceipt)
                    {
                        // The payment belongs to the other order that owns this receipt; this order
                        // is only a duplicate. Reporting purchase_voided counted paid purchases as voided.
                        _log.Info($"orderID {verifyOrderRequest.Id} dropped from pending queue: receipt already used by another order.");
                        break;
                    }

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
                        new InternalPurchaseItem
                        {
                            OrderId = verifyOrderRequest.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "verification_failed",
                            PlayerId = _authProvider?.PlayerId,
                        }
                    );

                    var message = Utility.GetTranslation("CustomPaymentCompleteDialogPresenter.OrderVerificationFailedMessage",  Utility.LoadTranslations(_localeProvider?.GetLanguage() ?? "en"));
                    if (message == "" || message == "CustomPaymentCompleteDialogPresenter.OrderVerificationFailedMessage")
                    {
                        message = "Your payment couldn’t be verified. Please retry later.";
                    }


                    if (!string.IsNullOrEmpty(verifyOrderErrorMessage) && 
                        verifyOrderErrorMessage.Contains("Message: \""))
                    {
                        var splitted = verifyOrderErrorMessage.Split("Message: \"");
                        if (splitted.Length > 1)
                        {
                            var messageParts = splitted[1].Split('"');
                            if (messageParts.Length > 0)
                            {
                                message = messageParts[0];
                            }
                        }
                    }

                    if (ShouldNotifyPurchasePending(verifyOrderRequest.Id, verifyOrderRequest.Trigger))
                    {
                        OnPurchasePending?.Invoke(orderRequest);
                    }
                    _log.Warning($"VerifyOrderImplAsync throwing Payment error. orderID={verifyOrderRequest.Id}, caller={callerMember}, trigger={verifyOrderRequest?.Trigger}, isTriggeredByIAP={isTriggeredByIAP}, status={verifyOrderResponse.Status}, message={message}");
                    throw new NoctuaException(
                        NoctuaErrorCode.Payment,
                        $"{message}",
                        verifyOrderRequest.Id.ToString()
                    );

                }

                return verifyOrderResponse;
        }
        
        /// <summary>
        /// Get the active currency asynchronously. Will complete when active currency is available.
        /// </summary>
        /// <param name="productId">Product id to infer active currency from server.</param>
        /// <returns>Active currency ISO code.</returns>
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

            // Bounded for the same reason as Android: StoreKit going silent would otherwise block
            // Noctua.InitAsync() forever, and a task that never completes cannot be caught. The
            // failure path above faults the TCS, so only genuine silence reaches the deadline —
            // for example when a concurrent native call overwrites this one's pending callback
            // (IosPlugin keeps single static callback fields).
            var (timedOut, activeCurrency) = await TaskTimeout.OrTimeoutAsync(
                tcs.Task,
                ActiveCurrencyTimeoutMs
            );

            // The native callback completes the TCS off the Unity main thread; callers set locale
            // state, so come back to the main thread before returning.
            await UniTask.SwitchToMainThread();

            if (timedOut)
            {
                _log.Warning(
                    $"GetActiveCurrencyAsync: StoreKit did not respond within {ActiveCurrencyTimeoutMs}ms " +
                    $"for '{productId}'. Continuing without store currency; caller falls back to " +
                    "the country-to-currency map."
                );

                return "";
            }

            tcs.TrySetCanceled();

            return activeCurrency;

#elif UNITY_ANDROID && !UNITY_EDITOR
            _log.Info("GetActiveCurrencyAsync: Android");
            if (_activeCurrencyGate.CurrentCount == 0)
            {
                _log.Debug($"GetActiveCurrencyAsync: another product-details query is in progress, queuing '{productId}'");
            }
            await _activeCurrencyGate.WaitAsync();
            try
            {
                _activeCurrencyTcs = new TaskCompletionSource<string>();
                GoogleBillingInstance.QueryProductDetails(productId);

                // Bounded await: Google Play may never call back at all when the billing client
                // cannot connect (user not signed in to Google Play, Play Store disabled or
                // blocked — common on OPPO/ColorOS). An unbounded await here blocks
                // Noctua.InitAsync() forever and the game never leaves its loading screen.
                var (timedOut, activeCurrency) = await TaskTimeout.OrTimeoutAsync(
                    _activeCurrencyTcs.Task,
                    ActiveCurrencyTimeoutMs
                );

                // Native callbacks complete the TCS off the Unity main thread; callers set locale
                // state, so come back to the main thread before returning.
                await UniTask.SwitchToMainThread();

                if (timedOut)
                {
                    _log.Warning(
                        $"GetActiveCurrencyAsync: Google Play did not respond within {ActiveCurrencyTimeoutMs}ms " +
                        $"for '{productId}'. Continuing without store currency; caller falls back to " +
                        "the country-to-currency map."
                    );

                    return "";
                }

                _activeCurrencyTcs.TrySetCanceled();

                return activeCurrency;
            }
            finally
            {
                _activeCurrencyTcs = null;
                _activeCurrencyGate.Release();
            }

#else // TODO for Other platforms

            _log.Info("GetActiveCurrencyAsync: not found, return empty string");

            return "";

#endif
        }

        /// <summary>
        /// Queries the native store for existing purchases (Android only). Used to detect unpaired or pending purchases.
        /// </summary>
        public void QueryPurchasesAsync()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.QueryPurchasesAsync();
#endif
        }

        private async Task HandlePurchaseRetryPopUpMessageAsync(string offlineModeMessage, PurchaseRequest purchaseRequest, bool tryToUseSecondaryPayment = false, PaymentType enforcedPaymentType = PaymentType.unknown) {
            bool isRetry = await _paymentUI.ShowRetryDialog(offlineModeMessage, "offlineMode");
            if(isRetry)
            {
                await PurchaseItemAsync(purchaseRequest, tryToUseSecondaryPayment, enforcedPaymentType);
            }
        }

        /// <summary>
        /// Debug helper to simulate an unpaired purchase on Android for testing purposes.
        /// </summary>
        /// <param name="productId">Product ID to simulate.</param>
        /// <param name="receiptData">Receipt data (purchase token) to simulate.</param>
        /// <param name="receiptId">
        /// Google Play order ID ("GPA.xxxx-xxxx-xxxx-xxxxx") to simulate. Leave null/empty to
        /// simulate a promo-code redemption, which is the only case allowed to become a redeem
        /// order; pass a value to simulate an unpairable PAID purchase.
        /// </param>
        public async UniTask HandleUnpairedPurchaseDebugAsync(
            string productId,
            string receiptData,
            string receiptId = null
        )
        {
            await UniTask.SwitchToMainThread();

#if UNITY_ANDROID && !UNITY_EDITOR
            var result = new GoogleBilling.PurchaseResult
            {
                ProductId = productId,
                ReceiptData = receiptData,
                ReceiptId = receiptId ?? "",
            };
            HandleUnpairedPurchase(result);
#endif
        }


        /// <summary>
        /// Initiates a purchase flow for the given product, handling connectivity checks, store readiness, and payment fallback.
        /// </summary>
        /// <param name="purchaseRequest">The purchase request containing product ID, price, and metadata.</param>
        /// <param name="tryToUseSecondaryPayment">Whether to attempt the secondary payment type.</param>
        /// <param name="enforcedPaymentType">Force a specific payment type, bypassing server override.</param>
        /// <returns>The purchase response with order ID and status.</returns>
        /// <exception cref="NoctuaException">Thrown when IAP is disabled, user is offline, or authentication fails.</exception>
        public async UniTask<PurchaseResponse> PurchaseItemAsync(PurchaseRequest purchaseRequest, bool tryToUseSecondaryPayment = false, PaymentType enforcedPaymentType = PaymentType.unknown)
        {
            if (_config.isIAPDisabled)
            {
                _paymentUI.ShowError(LocaleTextKey.IAPDisabled);

                _log.Warning($"IAP is being disabled by config");
                throw new NoctuaException(NoctuaErrorCode.Unknown, "IAP is being disabled by config");

            }


            // Offline-first handler
            _paymentUI.ShowLoadingProgress(true);
            
            var offlineModeMessage = _localeProvider.GetTranslation(LocaleTextKey.OfflineModeMessage) + " [IAP]";
            var isOffline = await _connectivity.IsOfflineAsync();

            if(!isOffline && !_connectivity.IsInitialized())
            {
                try
                {
                    await _connectivity.InitAsync();

                    await _authProvider.AuthenticateAsync();

                } catch(Exception e)
                {
                    _paymentUI.ShowLoadingProgress(false);

                    await HandlePurchaseRetryPopUpMessageAsync(offlineModeMessage, purchaseRequest, tryToUseSecondaryPayment, enforcedPaymentType);

                    throw new NoctuaException(NoctuaErrorCode.Authentication, $"{e.Message}");
                }
            }

            if (isOffline)
            {
                _paymentUI.ShowLoadingProgress(false);

                await HandlePurchaseRetryPopUpMessageAsync(offlineModeMessage, purchaseRequest, tryToUseSecondaryPayment, enforcedPaymentType);

                throw new NoctuaException(NoctuaErrorCode.Authentication, offlineModeMessage);
            }

            _paymentUI.ShowLoadingProgress(false);

            var iapReadyTimeout = DateTime.UtcNow.AddSeconds(5);
            while (!IsReady && DateTime.UtcNow < iapReadyTimeout)
            {
                // Reconnects rather than re-initializing: if billing failed to connect at SDK
                // init (Google Play unavailable at launch), Init() is a no-op natively and the
                // purchase would fail with "no payment types enabled" without ever reaching Play.
                EnsureBillingConnected();

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

        /// <summary>
        /// Internal purchase implementation that creates orders, handles payment flows, and verifies receipts.
        /// </summary>
        /// <param name="purchaseRequest">The purchase request containing product ID, price, and metadata.</param>
        /// <param name="tryToUseSecondaryPayment">Whether to attempt the secondary payment type.</param>
        /// <param name="enforcedPaymentType">Force a specific payment type, bypassing server override.</param>
        /// <returns>The purchase response with order ID and verification status.</returns>
        /// <exception cref="NoctuaException">Thrown on authentication, payment, or verification failure.</exception>
        public async UniTask<PurchaseResponse> PurchaseItemImplAsync(PurchaseRequest purchaseRequest, bool tryToUseSecondaryPayment = false, PaymentType enforcedPaymentType = PaymentType.unknown)
        {
            EnsureEnabled();
            
            _log.Debug("calling API");

            if (!_accessTokenProvider.IsAuthenticated)
            {
                _paymentUI.ShowError(LocaleTextKey.IAPRequiresAuthentication);

                _log.Warning($"Purchase requires user authentication");
                throw new NoctuaException(NoctuaErrorCode.Authentication, "Purchase requires user authentication");
            }
            
            // Resolved once per purchase against live billing readiness, so a billing client that
            // reconnected after a failed startup is usable again in this session.
            var availablePaymentTypes = GetAvailablePaymentTypes();

            if (availablePaymentTypes.Count == 0)
            {
                _paymentUI.ShowError(LocaleTextKey.IAPPaymentDisabled);

                _log.Warning($"No payment types enabled");
                throw new NoctuaException(NoctuaErrorCode.Payment, "no payment types enabled");
            }

            // The payment types are prioritized in backend
            // and filtered by runtime platform in InitAsync()
            // This payment type could be override by
            // the response of create order.
            var paymentType = availablePaymentTypes.First();
            if (tryToUseSecondaryPayment && availablePaymentTypes.Count > 1)
            {
                paymentType = availablePaymentTypes[1];
                _log.Info($"Fallback to secondary payment type: {paymentType}");
            }

            // Enforce particular payment type if available.
            // This could be triggered from CustomPaymentCompleteDialogPresenter.cs.
            if (enforcedPaymentType != PaymentType.unknown)
            {
                paymentType = enforcedPaymentType;
                _log.Info($"Fallback to enforced payment type: {paymentType}");
            }

            // --- EDITOR MOCK: skip create order, show UI directly ---
#if UNITY_EDITOR
            if (paymentType == PaymentType.editor)
            {
                _paymentUI.ShowLoadingProgress(true);

                // Still fetch USD products for price display
                if (_usdProducts.Count == 0)
                {
                    _usdProducts.AddRange(await GetProductListAsync(currency: "USD"));
                }

                var usdProductEditor = _usdProducts.FirstOrDefault(p => p.Id == purchaseRequest.ProductId);

                var editorCurrency = purchaseRequest.Currency;
                if (string.IsNullOrEmpty(editorCurrency))
                {
                    editorCurrency = _localeProvider?.GetCurrency() ?? "USD";
                }

                _paymentUI.ShowLoadingProgress(false);

                _log.Info("[Editor Mock] Showing editor payment sheet");

                var purchased = await _paymentUI.ShowEditorPaymentSheet(
                    purchaseRequest.ProductId,
                    purchaseRequest.Price.ToString("F2"),
                    editorCurrency
                );

                if (purchased)
                {
                    _log.Info("[Editor Mock] Purchase confirmed");

                    // --- CREATE ORDER (uncomment when server is ready) ---
                    // var serverPaymentType = PaymentType.direct;
                    // var orderRequest = new OrderRequest
                    // {
                    //     PaymentType = serverPaymentType,
                    //     ProductId = purchaseRequest.ProductId,
                    //     Price = purchaseRequest.Price,
                    //     Currency = editorCurrency,
                    //     RoleId = purchaseRequest.RoleId,
                    //     ServerId = purchaseRequest.ServerId,
                    //     IngameItemId = purchaseRequest.IngameItemId,
                    //     IngameItemName = purchaseRequest.IngameItemName,
                    //     Extra = purchaseRequest.Extra,
                    //     PriceInUSD = usdProductEditor?.Price ?? 0,
                    //     Timestamp = DateTime.Now.ToString(),
                    //     AllowPaymentTypeOverride = false,
                    // };
                    // var orderResponse = await RetryAsync(() => CreateOrderAsync(orderRequest));
                    // orderRequest.Id = orderResponse.Id;
                    // --- END CREATE ORDER ---

                    // --- VERIFY ORDER (uncomment when server is ready) ---
                    // var verifyOrderRequest = new VerifyOrderRequest { Id = orderResponse.Id };
                    // verifyOrderRequest.ReceiptData = orderResponse.Id.ToString();
                    // verifyOrderRequest.Trigger = VerifyOrderTrigger.payment_flow.ToString();
                    // var verifyResponse = await RetryAsync(() => VerifyOrderImplAsync(
                    //     orderRequest,
                    //     verifyOrderRequest,
                    //     _accessTokenProvider.AccessToken,
                    //     _authProvider?.PlayerId,
                    //     true
                    // ));
                    // return new PurchaseResponse
                    // {
                    //     OrderId = verifyResponse.Id,
                    //     Status = verifyResponse.Status,
                    //     Message = "Purchase " + verifyResponse.Status.ToString(),
                    // };
                    // --- END VERIFY ORDER ---

                    // Direct completion (skip create order & verify — server in progress)
                    var editorOrderRequest = new OrderRequest
                    {
                        ProductId = purchaseRequest.ProductId,
                        Price = purchaseRequest.Price,
                        Currency = editorCurrency,
                        PriceInUSD = usdProductEditor?.Price ?? 0,
                    };

                    _eventSender?.Send("purchase_completed", new()
                    {
                        { "product_id", purchaseRequest.ProductId },
                        { "amount", usdProductEditor?.Price ?? 0 },
                        { "currency", "USD" },
                        { "order_id", 0 },
                        { "orig_amount", purchaseRequest.Price },
                        { "orig_currency", editorCurrency }
                    });

                    TrackTaichiIAP(editorOrderRequest);

                    SendFirstPurchaseEventIfFirstTime(editorOrderRequest);

                    OnPurchaseDone?.Invoke(editorOrderRequest);

                    return new PurchaseResponse
                    {
                        OrderId = 0,
                        Status = OrderStatus.completed,
                        Message = "Purchase completed (Editor Mock)"
                    };
                }
                else
                {
                    _log.Info("[Editor Mock] Purchase canceled");

                    return new PurchaseResponse
                    {
                        OrderId = 0,
                        Status = OrderStatus.canceled,
                        Message = "Purchase canceled"
                    };
                }
            }
#endif
            // --- END EDITOR MOCK ---

            _paymentUI.ShowLoadingProgress(true);

            Product usdProduct;
            OrderRequest orderRequest;
            OrderResponse orderResponse;

            var unpairedOrders = new Dictionary<string, InternalPurchaseItem>();
            var orderId = 0;
            var pendingPurchaseItem = new InternalPurchaseItem();
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

                await _authProvider.UpdatePlayerAccountAsync(playerData);
                
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
                    CurrentStageLevel = PlayerPrefs.GetString("NoctuaCurrentStageLevel", ""),
                    CurrencyToUsdRate = purchaseRequest.CurrencyToUsdRate,
                    LocalPriceInUsd = purchaseRequest.LocalPriceInUsd,
                    Extra = purchaseRequest.Extra
                };

                if (string.IsNullOrEmpty(orderRequest.CurrentStageLevel))
                {
                    orderRequest.CurrentStageLevel = null;
                }

                if (string.IsNullOrEmpty(orderRequest.Currency))
                {
                    orderRequest.Currency = _localeProvider?.GetCurrency() ?? "USD";
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
                orderRequest.CurrencyToUsdRate = orderResponse.CurrencyToUsdRate;
                orderRequest.LocalPriceInUsd  = orderResponse.LocalPriceInUsd;

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
                pendingPurchaseItem = new InternalPurchaseItem
                {
                    OrderId = orderResponse.Id,
                    OrderRequest = orderRequest,
                    VerifyOrderRequest = verifyOrderRequest,
                    AccessToken = _accessTokenProvider.AccessToken,
                    PlayerId = _authProvider?.PlayerId,
                };

                // Store unpaired order.
                _log.Info($"NoctuaIAPService.HandleUnpairedPurchase store unpaired order for order ID: {JsonConvert.SerializeObject(orderRequest)}");
                _log.Info($"NoctuaIAPService.HandleUnpairedPurchase pending purchase item for unpaired order: {JsonConvert.SerializeObject(pendingPurchaseItem)}");
                unpairedOrders = new Dictionary<string, InternalPurchaseItem>();
                var unpairedOrdersJson = PlayerPrefs.GetString("NoctuaUnpairedOrders", "{}");
                try
                {
                    unpairedOrders = JsonConvert.DeserializeObject<Dictionary<string, InternalPurchaseItem>>(unpairedOrdersJson);
                    if (unpairedOrders == null)
                    {
                        unpairedOrders = new Dictionary<string, InternalPurchaseItem>();
                    }
                }
                catch (Exception e)
                {
                    _log.Warning($"Failed to parse existing unpaired orders: {e}");
                    unpairedOrders = new Dictionary<string, InternalPurchaseItem>();
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

                _paymentUI.ShowLoadingProgress(false);
            }
            catch (Exception e)
            {
                _log.Warning($"Failed to prepare purchase: {e.Message}");
                _paymentUI.ShowError(e.Message);
                _log.Exception(e);
                _paymentUI.ShowLoadingProgress(false);

                throw;
            }

            // Gate the section that owns _paymentTcs. Released in the finally below,
            // or early before the secondary-payment fallback recursion.
            if (_purchaseFlowGate.CurrentCount == 0)
            {
                _log.Warning($"PurchaseItemImplAsync: another purchase flow is in progress, queuing order {orderId}");
            }
            await _purchaseFlowGate.WaitAsync();
            _log.Debug($"PurchaseItemImplAsync: purchase flow gate acquired for order {orderId}");
            var purchaseFlowGateReleased = false;

            PaymentResult paymentResult;

            try
            {
            _paymentTcs = new TaskCompletionSource<PaymentResult>();

            switch (paymentType)
            {
                case PaymentType.appstore:
#if UNITY_IOS && !UNITY_EDITOR
                    _log.Info("NoctuaIAPService.PurchaseItemAsync purchase on ios: " + orderResponse.ProductId);
                    orderResponse.ProductId = purchaseRequest.ProductId;
                    // Captured locally: the callback may run after this flow has finished and the
                    // shared _paymentTcs field belongs to a later purchase (or is null).
                    var appStorePaymentTcs = _paymentTcs;
                    var appStoreProductId = orderResponse.ProductId;
                    _nativePlugin.PurchaseItem(appStoreProductId, (success, message) => {
                        _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback");
                        _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback success: " + success);
                        _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback message: " + message);

                        var appStoreResult = GetAppstorePaymentResult(orderResponse.Id, success, message);
                        if (!appStorePaymentTcs.TrySetResult(appStoreResult) && appStoreResult.Status == PaymentStatus.Successful)
                        {
                            // Defensive: this flow already has a result, yet the user was charged.
                            // Deliver the transaction instead of dropping it.
                            _log.Warning($"App Store purchase for '{appStoreProductId}' completed after its payment flow had a result; handling as unsolicited");
                            HandleUnsolicitedAppStorePurchase(new StoreKitTransaction
                            {
                                ProductId = appStoreProductId,
                                Success = true,
                                PurchaseToken = appStoreResult.PurchaseToken,
                                Receipt = appStoreResult.ReceiptData
                            });
                        }
                    });

                    // No timeout on purpose: releasing _purchaseFlowGate while the SKPayment may still be
                    // live would let a retry queue a second payment for the same product (double charge).
                    // StoreKit always ends a payment as purchased, failed or deferred.
                    var task = await appStorePaymentTcs.Task;
                    _log.Info("NoctuaIAPService.PurchaseItemAsync user side payment flow completed, clear up _paymentTcs then continue the payment flow.");
                    
                    paymentResult = _paymentTcs.Task.Result;

                    _log.Info("[iOS DEBUG] Payment result ReceiptData: " + paymentResult.ReceiptData);
                    
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

                    var completeDialogResult = await _paymentUI.ShowCustomPaymentCompleteDialog(nativePaymentButtonEnabled);
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
                                _authProvider?.PlayerId,
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
                                new InternalPurchaseItem
                                {
                                    OrderId = orderResponse.Id,
                                    OrderRequest = orderRequest,
                                    VerifyOrderRequest = verifyReq,
                                    AccessToken = _accessTokenProvider.AccessToken,
                                    PlayerId = _authProvider?.PlayerId,
                                }
                            );
                        }

                        if (availablePaymentTypes.Count > 1 &&
                        !verifiedAtCancelation &&
                        enforcedPaymentType == PaymentType.unknown &&
                        availablePaymentTypes[1] != paymentType
                        )
                        {
                            // Fallback to secondary payment option.
                            // Release the flow gate first — the recursive call below
                            // re-enters this method and must be able to acquire it.
                            _log.Debug($"PurchaseItemImplAsync: releasing purchase flow gate before secondary-payment fallback for order {orderId}");
                            _paymentTcs = null;
                            purchaseFlowGateReleased = true;
                            _purchaseFlowGate.Release();
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
                                new InternalPurchaseItem
                                {
                                    OrderId = orderResponse.Id,
                                    OrderRequest = orderRequest,
                                    VerifyOrderRequest = verifyReq,
                                    AccessToken = _accessTokenProvider.AccessToken,
                                    PlayerId = _authProvider?.PlayerId,
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
            }
            finally
            {
                // Clear up the payment flow instance and let the next queued purchase in.
                if (!purchaseFlowGateReleased)
                {
                    _paymentTcs = null;
                    _purchaseFlowGate.Release();
                    _log.Debug($"PurchaseItemImplAsync: purchase flow gate released for order {orderId}");
                }
            }

            // Assign the update value
            verifyOrderRequest.ReceiptId = paymentResult.ReceiptId;
            verifyOrderRequest.ReceiptData = paymentResult.ReceiptData;

            pendingPurchaseItem.OrderId = orderResponse.Id;
            pendingPurchaseItem.OrderRequest = orderRequest;
            pendingPurchaseItem.VerifyOrderRequest = verifyOrderRequest;
            pendingPurchaseItem.AccessToken = _accessTokenProvider.AccessToken;
            pendingPurchaseItem.PlayerId = _authProvider?.PlayerId;
            pendingPurchaseItem.PurchaseToken = paymentResult.PurchaseToken;

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
                    await _paymentUI.ShowFailedPaymentDialog(paymentResult.Status);
                    
                    throw new NoctuaException(NoctuaErrorCode.PaymentStatusItemAlreadyOwned, paymentResult.Message, orderId.ToString());
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

                    if (paymentType == PaymentType.playstore)
                    {
                        // Google Billing reported UserCanceled: no purchase was made, so the order
                        // enqueued above can never be verified. App Store cancels are matched by
                        // message text and custom (web) payments may have been paid before the page
                        // was closed, so those stay queued.
                        RemoveFromRetryPendingPurchasesByOrderID(orderResponse.Id);
                    }

                    _paymentUI.ShowError(LocaleTextKey.IAPCanceled);
                
                    throw new NoctuaException(NoctuaErrorCode.PaymentStatusCanceled, $"payment status: {paymentResult.Status}, Message: {paymentResult.Message}", orderId.ToString());
                case PaymentStatus.IapNotReady:
                    _log.Warning($"Purchase status IAPNotReady: {paymentResult.Status}, Message: {paymentResult.Message}");
                    _paymentUI.ShowError(LocaleTextKey.IAPNotReady);

                    throw new NoctuaException(NoctuaErrorCode.PaymentStatusIapNotReady, $"payment status: {paymentResult.Status}, Message: {paymentResult.Message}", orderId.ToString());
                default:
                    _log.Warning($"Purchase status IAPFailed: {paymentResult.Status}, Message: {paymentResult.Message}");
                    _paymentUI.ShowError(LocaleTextKey.IAPFailed);
                
                    throw new NoctuaException(NoctuaErrorCode.Payment, $"payment status: {paymentResult.Status}, Message: {paymentResult.Message}", orderId.ToString());
            }

            _log.Info($"Verifying order: {verifyOrderRequest.Id} with receipt data: {verifyOrderRequest.ReceiptData}");
            
            VerifyOrderResponse verifyOrderResponse;

            try {
                _paymentUI.ShowLoadingProgress(true);

                verifyOrderRequest.Trigger = VerifyOrderTrigger.payment_flow.ToString();
                verifyOrderResponse = await RetryAsync(() => VerifyOrderImplAsync(
                        orderRequest,
                        verifyOrderRequest,
                        _accessTokenProvider.AccessToken,
                        _authProvider?.PlayerId,
                        true,
                        paymentResult.PurchaseToken
                    )
                );

                _paymentUI.ShowLoadingProgress(false);
            }
            catch (NoctuaException e)
            {
                _log.Warning($"Failed to verify order: {e.Message}");
                if ((NoctuaErrorCode)e.ErrorCode == NoctuaErrorCode.Networking)
                {
                    EnqueueToRetryPendingPurchases(
                        new InternalPurchaseItem
                        {
                            OrderId = orderResponse.Id,
                            OrderRequest = orderRequest,
                            VerifyOrderRequest = verifyOrderRequest,
                            AccessToken = _accessTokenProvider.AccessToken,
                            Status = "Network error",
                            PlayerId = _authProvider?.PlayerId
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
                
                _paymentUI.ShowLoadingProgress(false);
                _log.Exception(e);
                _paymentUI.ShowError(e.Message);

                throw;
            }
            catch (Exception e) 
            {
                _log.Warning($"Failed to verify order: {e.Message}");
                _paymentUI.ShowLoadingProgress(false);
                _log.Exception(e);
                
                throw;
            }

            _log.Info($"Verify order status: {verifyOrderResponse.Status}");
            switch (verifyOrderResponse.Status)
            {
                case OrderStatus.canceled:
                    _paymentUI.ShowGeneralNotification(
                        "Your purchase has been canceled. Please contact customer support for more details.",
                        false
                    );
                    break;
                case OrderStatus.refunded:
                    _paymentUI.ShowGeneralNotification(
                        "Your purchase has been refunded. Please contact customer support for more details.",
                        false
                    );
                    break;
                case OrderStatus.voided:
                    _paymentUI.ShowGeneralNotification(
                        "Your purchase has been voided. Please contact customer support for more details.",
                        false
                    );
                    break;
                default:
                    _paymentUI.ShowGeneralNotification("Purchase successful!", true);
                    break;
            }


            return new PurchaseResponse
            {
                OrderId = verifyOrderResponse.Id,
                Status = verifyOrderResponse.Status,
                Message = "Purchase " + verifyOrderResponse.Status.ToString(),
            };
        }

        /// <summary>
        /// Manually retries verification for a single pending purchase identified by order ID.
        /// </summary>
        /// <param name="orderId">The order ID of the pending purchase to retry.</param>
        /// <returns>The resulting order status after retry.</returns>
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
                    false,
                    item.PurchaseToken
                );

                if (verifyOrderResponse.Status != OrderStatus.completed &&
                verifyOrderResponse.Status != OrderStatus.canceled &&
                verifyOrderResponse.Status != OrderStatus.refunded &&
                verifyOrderResponse.Status != OrderStatus.voided)
                {
                    // Enqueue to player prefs for future read
                    item.Status = verifyOrderResponse.Status.ToString();
                    EnqueueToRetryPendingPurchases(item);
                    _paymentUI.ShowGeneralNotification("Failed to verify the purchase. Status: " + verifyOrderResponse.Status.ToString(), false);
                }

                return verifyOrderResponse.Status;
            }
            catch (NoctuaException e)
            {
                // Do not track purchase_verify_order_failed
                // as we don't want it to flood our data.
                EnqueueToRetryPendingPurchases(
                    new InternalPurchaseItem
                    {
                        OrderId = item.OrderId,
                        OrderRequest = item.OrderRequest,
                        VerifyOrderRequest = item.VerifyOrderRequest,
                        AccessToken = item.AccessToken,
                        Status = "verification_failed",
                        PlayerId = _authProvider?.PlayerId
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
        }

        private async UniTask<T> RetryAsync<T>(Func<UniTask<T>> action)
        {
            while (true)
            {
                try
                {
                    _paymentUI.ShowLoadingProgress(true);
                    return await action();
                }
                catch (NoctuaException e)
                {
                    _paymentUI.ShowLoadingProgress(false);
                    var errorCode = (NoctuaErrorCode)e.ErrorCode;

                    bool shouldRetry = false;
                    switch (errorCode)
                    {
                        case NoctuaErrorCode.Networking:
                            _log.Exception(e);
                            if (e.Message.Contains("HTTP error"))
                            {
                                shouldRetry = await _paymentUI.ShowRetryDialog($"{e.Message}. Please try again later.", "payment");
                            }
                            else
                            {
                                shouldRetry = await _paymentUI.ShowRetryDialog("Please check your internet connection.", "payment");
                            }
                            break;
                        default:
                            _log.Exception(e);
                            shouldRetry = await _paymentUI.ShowRetryDialog(e.Message, "payment");
                            break;
                    }

                    if (!shouldRetry)
                    {
                        throw;
                    }
                }
                catch (Exception ex)
                {
                    _paymentUI.ShowLoadingProgress(false);
                    _log.Exception(ex);

                    bool shouldRetry = await _paymentUI.ShowRetryDialog(ex.Message, "payment");
                    if (!shouldRetry)
                    {
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Handles query purchases result from Google Billing.
        /// </summary>
#if UNITY_ANDROID && !UNITY_EDITOR
        private void HandleGooglePurchaseDone(GoogleBilling.PurchaseResult result)
        {
            _log.Info("NoctuaIAPService.PurchaseItemAsync PurchaseItem callback");

            if (_paymentTcs == null)
            {
                _log.Info("NoctuaIAPService.PurchaseItemAsync Find out the order ID pair...");

                UniTask.Void(async () =>
                {
                    HandleUnpairedPurchase(result);

                });
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
                // Expected when the query already timed out: the waiter gave up and cleared the
                // slot, and this is Google Play answering late. Throwing here would escape onto a
                // JNI callback thread where nothing can catch it.
                _log.Warning("NoctuaIAPService.HandleGoogleProductDetails: no pending active-currency request, ignoring late response");

                return;
            }
            
            if (response is null)
            {
                _activeCurrencyTcs.TrySetException(NoctuaException.ActiveCurrencyFailure);
            
                return;
            }

            _log.Info("NoctuaIAPService.HandleGoogleProductDetails currency: " + response.Currency);

            _activeCurrencyTcs.TrySetResult(response.Currency);
        }

        /// <summary>
        /// Files a Google Play purchase the client could not pair to a local order so it can be
        /// reconciled server-side. On success the token is settled, so later QueryPurchases passes
        /// and launches do not file the same purchase again (the server creates a new record per
        /// call). A failure is logged and left unsettled so the next pass retries it.
        /// </summary>
        private async UniTask ReportUnpairedPurchaseAsync(GoogleBilling.PurchaseResult result)
        {
            var unpairedPurchaseRequest = new UnpairedPurchaseRequest
            {
                ReceiptData = result.ReceiptData,
                PaymentType = PaymentType.playstore, // This is always about playstore
                ProductId = result.ProductId,
                Currency = _localeProvider?.GetCurrency() ?? "USD",
            };

            try
            {
                var response = await CreateUnpairedPurchaseAsync(unpairedPurchaseRequest);

                await UniTask.SwitchToMainThread();
                RememberSettledUnpairedPurchaseToken(result.ReceiptData);
                _log.Info(
                    $"NoctuaIAPService.ReportUnpairedPurchase Filed unpaired purchase {response?.Id} for product " +
                    $"{result.ProductId}; token settled so it is not filed again."
                );
            }
            catch (Exception e)
            {
                _log.Error("NoctuaIAPService.ReportUnpairedPurchase failed to create unpaired purchase: " + e);
            }
        }

        private async void HandleUnpairedPurchase(GoogleBilling.PurchaseResult result)
        {
            await UniTask.SwitchToMainThread();

            if (string.IsNullOrEmpty(result.ReceiptData)) {
                _log.Warning($"NoctuaIAPService.HandleUnpairedPurchase Receipt data is empty for productId: {result.ProductId}. Skip it.");

                return;
            }

            if (!TryBeginUnpairedPurchaseInFlight(result.ReceiptData))
            {
                _log.Info(
                    "NoctuaIAPService.HandleUnpairedPurchase Already handling this purchase token for product " +
                    $"{result.ProductId}; skipping the duplicate delivery from an overlapping QueryPurchases pass."
                );

                return;
            }

            try
            {
                await HandleUnpairedPurchaseCoreAsync(result);
            }
            catch (Exception e)
            {
                _log.Error($"NoctuaIAPService.HandleUnpairedPurchase failed for product {result.ProductId}: {e}");
            }
            finally
            {
                EndUnpairedPurchaseInFlight(result.ReceiptData);
            }
        }

        /// <summary>
        /// Marks a purchase token as being handled in this session. In memory only, unlike the
        /// persisted settled tokens.
        /// </summary>
        /// <returns><c>false</c> when the token is already in flight (overlapping QueryPurchases pass).</returns>
        private bool TryBeginUnpairedPurchaseInFlight(string purchaseToken)
        {
            lock (_unpairedPurchasesInFlightLock)
            {
                return _unpairedPurchasesInFlight.Add(purchaseToken);
            }
        }

        /// <summary>Releases a token marked by <see cref="TryBeginUnpairedPurchaseInFlight"/>.</summary>
        private void EndUnpairedPurchaseInFlight(string purchaseToken)
        {
            lock (_unpairedPurchasesInFlightLock)
            {
                _unpairedPurchasesInFlight.Remove(purchaseToken);
            }
        }

        private List<string> LoadSettledUnpairedPurchaseTokens()
        {
            var json = PlayerPrefs.GetString(SettledUnpairedPurchaseTokensKey, "[]");

            try
            {
                return JsonConvert.DeserializeObject<List<string>>(json) ?? new List<string>();
            }
            catch (Exception e)
            {
                _log.Error($"NoctuaIAPService failed to parse settled unpaired purchase tokens, resetting: {e.Message}");

                return new List<string>();
            }
        }

        private void RememberSettledUnpairedPurchaseToken(string purchaseToken)
        {
            var updated = WithSettledUnpairedPurchaseToken(LoadSettledUnpairedPurchaseTokens(), purchaseToken);
            PlayerPrefs.SetString(SettledUnpairedPurchaseTokensKey, JsonConvert.SerializeObject(updated));
            PlayerPrefs.Save();
        }

        private async UniTask HandleUnpairedPurchaseCoreAsync(GoogleBilling.PurchaseResult result)
        {
            var productId = result.ProductId;
            _log.Info(
                "NoctuaIAPService.HandleUnpairedPurchase Try to find the purchase token in pending purchase first " +
                $"to avoid duplicate token {result.ReceiptData} for product {productId} " +
                $"(purchaseState={result.PurchaseState}, orderId={result.ReceiptId})."
            );
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
                        false,
                        pendingPurchase.PurchaseToken
                    ).Forget(e => _log.Warning($"HandleUnpairedPurchase(pending) verify failed: {e.Message}"));

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
                        false,
                        purchaseItem.PurchaseToken
                    ).Forget(e => _log.Warning($"HandleUnpairedPurchase(history) verify failed: {e.Message}"));

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
            Dictionary<string, InternalPurchaseItem> unpairedOrders;
            try
            {
                unpairedOrders = JsonConvert.DeserializeObject<Dictionary<string, InternalPurchaseItem>>(unpairedOrdersJson);
                if (unpairedOrders == null)
                {
                    unpairedOrders = new Dictionary<string, InternalPurchaseItem>();
                }
            }
            catch (Exception e)
            {
                _log.Error($"NoctuaIAPService.HandleUnpairedPurchase Failed to parse unpaired orders: {e}");
                _log.Info($"NoctuaIAPService.HandleUnpairedPurchase Create empty unpairedOrders array");
                unpairedOrders = new Dictionary<string, InternalPurchaseItem>();
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
                    false,
                    pendingPurchaseItem.PurchaseToken
                ).Forget(e => _log.Warning($"HandleUnpairedPurchase(unpaired) verify failed: {e.Message}"));
            }

            if (!foundInPendingPurchases && !foundInPurchaseHistory && !foundUnpairedOrder) {
                if (LoadSettledUnpairedPurchaseTokens().Contains(result.ReceiptData))
                {
                    _log.Info(
                        "NoctuaIAPService.HandleUnpairedPurchase This purchase token for product " +
                        $"{productId} was already settled (filed for reconciliation, or rejected because another " +
                        "order owns the receipt); not creating another order or report."
                    );

                    return;
                }

                if (!CanCreateOrderForUnpairedPurchase((int)result.PurchaseState))
                {
                    _log.Warning(
                        $"NoctuaIAPService.HandleUnpairedPurchase Purchase for product {productId} is still PENDING " +
                        "(not paid yet, no Google Play order id); not creating any order. It is handled when Play " +
                        "re-delivers it as PURCHASED."
                    );

                    return;
                }

                // A purchase that carries a Google Play order id was PAID for, so it must never be
                // re-minted as a $0 redeem order — that attaches a second order to a real payment and
                // risks double-crediting the player. Being unable to pair it locally only means local
                // state was lost (reinstall, cleared data, new device), so hand it to the
                // unpaired-purchase reconciliation queue instead.
                // See CanTreatUnpairedPurchaseAsRedeem for the full rationale.
                if (!CanTreatUnpairedPurchaseAsRedeem(result.ReceiptId))
                {
                    _log.Warning(
                        "NoctuaIAPService.HandleUnpairedPurchase Unpairable PAID purchase for product " +
                        $"{productId} (Google Play order ID {result.ReceiptId}); local pairing state was lost. " +
                        "Filing it as an unpaired purchase for reconciliation instead of creating a redeem order."
                    );

                    await ReportUnpairedPurchaseAsync(result);

                    return;
                }

                _log.Warning($"NoctuaIAPService.HandleUnpairedPurchase No unpaired order or pending purchase found for receipt data {result.ReceiptData}. Treat it as redeem.");

                var redeemOrderRequest = new RedeemOrderRequest
                {
                    ProductId = result.ProductId,
                };

                try
                {
                    var orderResponse = await CreateRedeemOrderAsync(redeemOrderRequest);
                    var redeemOrderId = orderResponse.Id;

                    _log.Info($"NoctuaIAPService.HandleUnpairedPurchase redeem order ID: {redeemOrderId}");

                    var orderRequest = new OrderRequest
                    {
                        Id = redeemOrderId,
                        ProductId = productId,
                        PriceInUSD = 0,
                        Price = 0,
                        Currency = "USD"
                    };

                    var verifyOrderRequest = new VerifyOrderRequest
                    {
                        Id = orderRequest.Id,
                        ReceiptId = result.ReceiptId,
                        ReceiptData = result.ReceiptData,
                    };

                    var verifyOrderResponse = await VerifyOrderImplAsync(
                        orderRequest,
                        verifyOrderRequest,
                        _accessTokenProvider.AccessToken,
                        _authProvider?.PlayerId,
                        false,
                        result.ReceiptData // On Android, ReceiptData IS the purchase token
                    );

                    // Voided here means the receipt already belongs to another order (verify error
                    // 2042), so every later re-pairing of this token would mint another dead-end
                    // order. Remember it so later launches skip it.
                    if (verifyOrderResponse?.Status == OrderStatus.voided)
                    {
                        await UniTask.SwitchToMainThread();
                        RememberSettledUnpairedPurchaseToken(result.ReceiptData);
                        _log.Info(
                            $"NoctuaIAPService.HandleUnpairedPurchase Redeem order {redeemOrderId} for product " +
                            $"{productId} was rejected; token settled so it is not re-minted on later launches."
                        );
                    }
                }
                catch (Exception e)
                {
                    _log.Error("NoctuaIAPService.HandleUnpairedPurchase failed to verify redeem data: " + e);

                    await ReportUnpairedPurchaseAsync(result);
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
                UniTask.Void(async () =>
                {
                    HandleUnpairedPurchase(result);

                });
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
                PurchaseToken = result.ReceiptData, // On Android, ReceiptData IS the purchase token
            };
        }
#endif

#if UNITY_IOS && !UNITY_EDITOR
        /// <summary>
        /// Delivers an App Store transaction no in-flight purchase claimed (e.g. a StoreKit replay of an
        /// unfinished transaction whose purchase flow never received it).
        /// </summary>
        private void HandleUnsolicitedAppStorePurchase(StoreKitTransaction transaction)
        {
            UniTask.Void(async () =>
            {
                await UniTask.SwitchToMainThread();
                try
                {
                    ProcessUnsolicitedAppStorePurchase(transaction);
                }
                catch (Exception e)
                {
                    _log.Warning($"Failed to handle unsolicited App Store purchase for '{transaction?.ProductId}': {e.Message}");
                }
            });
        }

        private void ProcessUnsolicitedAppStorePurchase(StoreKitTransaction transaction)
        {
            var unpairedOrders = LoadUnpairedOrders();
            var decision = AppStoreUnsolicitedPurchaseMatcher.Decide(
                transaction, GetPendingPurchases(), GetPurchaseHistory(), unpairedOrders);

            var decisionLog = $"Unsolicited App Store purchase '{transaction.ProductId}' (token {transaction.PurchaseToken}): {decision.Action} — {decision.Reason}";
            if (decision.Action == UnsolicitedAppStorePurchaseAction.LeaveUnfinished)
            {
                // Paid but not delivered yet; StoreKit re-delivers it next launch.
                _log.Warning(decisionLog);
            }
            else
            {
                _log.Info(decisionLog);
            }

            switch (decision.Action)
            {
            case UnsolicitedAppStorePurchaseAction.FinishAlreadyCompleted:
                _nativePlugin?.CompletePurchaseProcessing(
                    transaction.PurchaseToken,
                    NoctuaConsumableType.Consumable,
                    true,
                    success => _log.Debug($"Finished already-completed transaction {transaction.PurchaseToken}: {success}")
                );
                break;
            case UnsolicitedAppStorePurchaseAction.PairUnpairedOrder:
                var remainingUnpaired = unpairedOrders
                    .Where(entry => entry.Key != transaction.ProductId)
                    .ToDictionary(entry => entry.Key, entry => entry.Value);
                PlayerPrefs.SetString(UnpairedOrdersKey, JsonConvert.SerializeObject(remainingUnpaired));
                PlayerPrefs.Save();
                VerifyUnsolicitedAppStorePurchase(decision.Item);
                break;
            case UnsolicitedAppStorePurchaseAction.VerifyPendingPurchase:
                VerifyUnsolicitedAppStorePurchase(decision.Item);
                break;
            default:
                // Left unfinished on purpose: StoreKit re-delivers it on the next launch.
                break;
            }
        }

        private void VerifyUnsolicitedAppStorePurchase(InternalPurchaseItem item)
        {
            EnqueueToRetryPendingPurchases(item);
            VerifyOrderImplAsync(
                item.OrderRequest,
                item.VerifyOrderRequest,
                item.AccessToken,
                item.PlayerId,
                false,
                item.PurchaseToken
            ).Forget(e => _log.Warning($"Unsolicited App Store purchase verify failed for order {item.OrderId}: {e.Message}"));
        }

        private Dictionary<string, InternalPurchaseItem> LoadUnpairedOrders()
        {
            try
            {
                return JsonConvert.DeserializeObject<Dictionary<string, InternalPurchaseItem>>(
                    PlayerPrefs.GetString(UnpairedOrdersKey, "{}")
                ) ?? new Dictionary<string, InternalPurchaseItem>();
            }
            catch (Exception e)
            {
                _log.Warning($"Failed to parse unpaired orders: {e.Message}");
                return new Dictionary<string, InternalPurchaseItem>();
            }
        }

        private PaymentResult GetAppstorePaymentResult(int orderId, bool success, string message)
        {
            _log.Info("Noctua.HandleIosPurchaseDone");
            _log.Info("Noctua.HandleIosPurchaseDone orderId: " + orderId);
            _log.Info("Noctua.HandleIosPurchaseDone success: " + success);
            _log.Info("Noctua.HandleIosPurchaseDone message: " + message);

            message ??= string.Empty;

            if (!success)
            {
                // The bridge labels StoreKit user cancellations "User cancelled"
                if (message.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0) {
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

            // Try to parse JSON format from new native SDK (contains receipt + purchaseToken + consumableType)
            try
            {
                if (!string.IsNullOrEmpty(message) && message.TrimStart().StartsWith("{"))
                {
                    var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(message);
                    if (data != null)
                    {
                        var result = new PaymentResult
                        {
                            Status = PaymentStatus.Successful,
                            ReceiptData = data.ContainsKey("receipt") ? data["receipt"]?.ToString() : message,
                            PurchaseToken = data.ContainsKey("purchaseToken") ? data["purchaseToken"]?.ToString() : null
                        };

                        _log.Info($"Parsed iOS purchase JSON: ReceiptData.length={result.ReceiptData?.Length}, PurchaseToken={result.PurchaseToken}");

                        return result;
                    }
                }
            }
            catch (Exception e)
            {
                _log.Warning($"Failed to parse iOS purchase JSON, falling back to legacy format: {e.Message}");
            }

            // Legacy format: message is raw receipt data
            return new PaymentResult
            {
                Status = PaymentStatus.Successful,
                ReceiptData = message
            };
        }
#endif
        /// <summary>
        /// Save pending purchases list into PlayerPrefs.
        /// </summary>
        /// <param name="orders">List of pending purchases to save.</param>
        private void SavePendingPurchases(List<InternalPurchaseItem> orders)
        {
            _log.Debug("save pending purchases to player prefs");
            var updatedJson = JsonConvert.SerializeObject(orders);
            PlayerPrefs.SetString("NoctuaPendingPurchases", updatedJson);
            PlayerPrefs.Save();

            // Leave it here for debuggin purpose.
            //GetPendingPurchases();
        }

        /// <summary>
        /// Retrieves pending purchases persisted locally.
        /// </summary>
        /// <returns>List of pending purchase items.</returns>
        public List<InternalPurchaseItem> GetPendingPurchases()
        {
            _log.Info("Noctua.GetPendingPurchases");
            var json = PlayerPrefs.GetString("NoctuaPendingPurchases", string.Empty);
            _log.Info($"Pending purchases data: {json}");

            if (string.IsNullOrEmpty(json))
            {
                return new List<InternalPurchaseItem>();
            }

            try
            {
                var pendingPurchases = JsonConvert.DeserializeObject<List<InternalPurchaseItem>>(json);
                if (pendingPurchases == null)
                {
                    pendingPurchases = new List<InternalPurchaseItem>();
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

                return new List<InternalPurchaseItem>();
            }
        }

        /// <summary>
        /// Retrieves a single pending purchase by order id.
        /// </summary>
        /// <param name="orderId">Order id to search for.</param>
        /// <returns>Found pending purchase item.</returns>
        /// <exception cref="Exception">Throws if not found or malformed storage.</exception>
        public InternalPurchaseItem GetPendingPurchaseByOrderId(int orderId)
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
                var pendingPurchases = JsonConvert.DeserializeObject<List<InternalPurchaseItem>>(json);
                if (pendingPurchases == null)
                {
                    pendingPurchases = new List<InternalPurchaseItem>();
                }

                var list = pendingPurchases
                    .Where(p => p.VerifyOrderRequest != null && p.AccessToken != null)
                    .ToList();

                var result = new InternalPurchaseItem();
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
        
        /// <summary>
        /// Retry mechanism for pending purchases. Each pass re-reads the persisted retry queue, so
        /// orders enqueued after the loop started (new purchases, failed verifications) are picked
        /// up, and verifies every retryable order. VerifyOrderImplAsync keeps the queue in sync:
        /// completed and voided orders leave it, everything else stays queued. Respects application
        /// quitting.
        /// </summary>
        public async UniTask RetryPendingPurchasesAsync()
        {
            if (_pendingPurchaseRetryLoopRunning)
            {
                _log.Info("Pending purchases retry loop already running.");
                return;
            }

            if (_enabledPaymentTypes == null || _enabledPaymentTypes.Count == 0)
            {
                _log.Warning("no payment types enabled, quitting");

                return;
            }

            _log.Info("Starting pending purchases retry loop.");
            _pendingPurchaseRetryLoopRunning = true;

            var random = new Random();
            var cts = new CancellationTokenSource();

            // Named handler so it can be unsubscribed when the loop exits — a lambda
            // added per call would leak one closure on every invocation.
            Action quitHandler = () =>
            {
                _log.Info("Quitting pending purchases retry loop.");
                cts.Cancel();
            };
            Application.quitting += quitHandler;

            try
            {
                EnsurePendingPurchaseQueueLoaded();
                _log.Info("Queue count: " + _waitingPendingPurchases.Count);

                var retryCount = 0;

                while (!cts.IsCancellationRequested)
                {
                    var batch = GetRetryablePendingPurchases();

                    // The purchase flow verifies its own order; do not race it.
                    if (batch.Count == 0 || IsPurchaseFlowActive())
                    {
                        retryCount = 0;
                        await UniTask.Delay(PendingPurchaseIdlePollInterval, cancellationToken: cts.Token);

                        continue;
                    }

                    _log.Info("Retrying pending purchases: " + batch.Count);

                    foreach (var item in batch)
                    {
                        if (cts.IsCancellationRequested)
                        {
                            break;
                        }

                        await RetryPendingPurchaseOnceAsync(item);
                    }

                    var remaining = GetRetryablePendingPurchases();
                    if (remaining.Count == 0)
                    {
                        retryCount = 0;
                        continue;
                    }

                    // Exponential backoff with randomization, so we don't hammer the server.
                    // A newly enqueued order ends the wait early and restarts the backoff, so it
                    // does not inherit a long delay built up by older orders.
                    retryCount++;
                    var delay = GetBackoffDelay(random, retryCount);
                    _log.Info($"Retrying {remaining.Count} pending purchase(s) in {delay.TotalSeconds} seconds...");

                    var knownOrderIds = new HashSet<int>(remaining.Select(item => item.OrderId));
                    if (await WaitForBackoffOrNewPendingPurchaseAsync(delay, knownOrderIds, cts.Token))
                    {
                        retryCount = 0;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _log.Info("Pending purchases retry loop canceled.");
            }
            finally
            {
                Application.quitting -= quitHandler;
                _pendingPurchaseRetryLoopRunning = false;
                cts.Dispose();

                _log.Info("Quitting, saving pending purchases: " + _waitingPendingPurchases.Count);
                SavePendingPurchases(_waitingPendingPurchases.ToList());
            }
        }

        private async UniTask RetryPendingPurchaseOnceAsync(InternalPurchaseItem item)
        {
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
                    false,
                    item.PurchaseToken
                );

                _log.Info($"Retried Order ID: {item.OrderId}, status: {verifyOrderResponse.Status}");
            }
            catch (NoctuaException e)
            {
                // Do not track purchase_verify_order_failed
                // as we don't want it to flood our data.
                // The order is still in the queue; it is retried on the next pass.
                _log.Warning($"Pending purchase {item.OrderId} not verified yet: {(NoctuaErrorCode)e.ErrorCode} : {e.Message}");
            }
            catch (Exception e)
            {
                _log.Error($"Pending purchase {item.OrderId} retry failed: {e}");
            }
        }

        /// <returns>True when a new order ended the wait early.</returns>
        private async UniTask<bool> WaitForBackoffOrNewPendingPurchaseAsync(
            TimeSpan delay,
            HashSet<int> knownOrderIds,
            CancellationToken cancellationToken)
        {
            var deadline = DateTime.UtcNow + delay;
            while (DateTime.UtcNow < deadline)
            {
                if (GetRetryablePendingPurchases().Any(item => !knownOrderIds.Contains(item.OrderId)))
                {
                    _log.Info("New pending purchase enqueued, ending retry backoff early.");
                    return true;
                }

                await UniTask.Delay(PendingPurchaseIdlePollInterval, cancellationToken: cancellationToken);
            }

            return false;
        }

        private List<InternalPurchaseItem> GetRetryablePendingPurchases()
        {
            EnsurePendingPurchaseQueueLoaded();

            return _waitingPendingPurchases
                .Where(item => IsRetryablePendingPurchase(item) && !_verifyingOrderIds.Contains(item.OrderId))
                .ToList();
        }

        /// <summary>
        /// Whether a queued order should be re-verified. Canceled and refunded orders are final:
        /// they stay persisted so the pending purchases dialog can show them, but are not retried.
        /// </summary>
        /// <param name="item">Queued pending purchase.</param>
        /// <returns>True when the order should be verified again.</returns>
        public static bool IsRetryablePendingPurchase(InternalPurchaseItem item)
        {
            if (item?.OrderRequest == null || item.VerifyOrderRequest == null || item.OrderId == 0)
            {
                return false;
            }

            return item.Status != OrderStatus.refunded.ToString() &&
                   item.Status != OrderStatus.canceled.ToString();
        }

        private bool IsPurchaseFlowActive() => _purchaseFlowGate.CurrentCount == 0;

        /// <summary>
        /// Loads the persisted retry queue into memory once. Without this, the first enqueue after
        /// launch saved only the in-memory queue and dropped orders persisted by earlier sessions.
        /// </summary>
        private void EnsurePendingPurchaseQueueLoaded()
        {
            if (_pendingPurchaseQueueLoaded)
            {
                return;
            }

            _pendingPurchaseQueueLoaded = true;

            var queuedOrderIds = new HashSet<int>(_waitingPendingPurchases.Select(item => item.OrderId));
            foreach (var item in GetPendingPurchases())
            {
                if (queuedOrderIds.Add(item.OrderId))
                {
                    _waitingPendingPurchases.Enqueue(item);
                }
            }
        }

        /// <summary>
        /// Automatic retries re-verify pending orders on a backoff; report each order through
        /// <see cref="OnPurchasePending"/> once per session instead of on every pass. Other
        /// triggers (purchase flow, manual retry) always notify.
        /// </summary>
        private bool ShouldNotifyPurchasePending(int orderId, string trigger)
        {
            var firstNotification = _pendingNotifiedOrderIds.Add(orderId);

            return firstNotification || trigger != VerifyOrderTrigger.client_automatic_retry.ToString();
        }

        /// <summary>
        /// Retrieves the current user's Noctua Gold wallet balance from the server.
        /// </summary>
        /// <returns>The Noctua Gold balance data.</returns>
        public async UniTask<NoctuaGoldData> GetNoctuaGold()
        {
            var request = new HttpRequest(HttpMethod.Get, $"{_config.BaseUrl}/noctuastore/wallet")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken);

            return await request.Send<NoctuaGoldData>();
        }

        /// <summary>
        /// Fetches pending deliverables (e.g., Noctua redeem orders) from the server that need to be delivered to the player.
        /// </summary>
        /// <returns>An array of pending deliverable items.</returns>
        public async UniTask<PendingDeliverables[]> GetPendingDeliverables()
        {
            var request = new HttpRequest(HttpMethod.Get, $"{_config.BaseUrl}/pending-deliverables")
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken);

            PendingDeliverablesData response = await request.Send<PendingDeliverablesData>();

            return response?.PendingNoctuaRedeemOrders ?? new PendingDeliverables[0];
        }

        /// <summary>
        /// Processes all pending deliverables by verifying each order and invoking <see cref="OnPurchaseDone"/> for completed ones.
        /// </summary>
        public async UniTask DeliverPendingDeliverablesAsync()
        {
            try
            {
                _log.Info("DeliverPendingDeliverablesAsync: Fetching pending deliverables from server");

                var pendingDeliverables = await GetPendingDeliverables();

                if (pendingDeliverables == null || pendingDeliverables.Length == 0)
                {
                    _log.Info("DeliverPendingDeliverablesAsync: No pending deliverables found");
                    return;
                }

                _log.Info($"DeliverPendingDeliverablesAsync: Found {pendingDeliverables.Length} pending deliverables");

                foreach (var deliverable in pendingDeliverables)
                {
                    _log.Info($"DeliverPendingDeliverablesAsync: Processing order {deliverable.OrderId} for product {deliverable.ProductId}");

                    try
                    {
                        var orderRequest = new OrderRequest
                        {
                            Id = deliverable.OrderId,
                            PaymentType = deliverable.PaymentType,
                            ProductId = deliverable.ProductId,
                            Currency = "USD",
                            Price = 0,
                            PriceInUSD = 0
                        };

                        var verifyOrderRequest = new VerifyOrderRequest
                        {
                            Id = deliverable.OrderId,
                            Trigger = VerifyOrderTrigger.pending_deliverable.ToString()
                        };

                        await VerifyOrderImplAsync(
                            orderRequest,
                            verifyOrderRequest,
                            _accessTokenProvider.AccessToken,
                            _authProvider?.PlayerId,
                            false
                        );

                        _log.Info($"DeliverPendingDeliverablesAsync: Successfully processed order {deliverable.OrderId}");
                    }
                    catch (Exception ex)
                    {
                        _log.Error($"DeliverPendingDeliverablesAsync: Error processing order {deliverable.OrderId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _log.Error($"DeliverPendingDeliverablesAsync: Error fetching pending deliverables: {ex.Message}");
            }
        }

        private TimeSpan GetBackoffDelay(Random random, int retryCount)
        {
            var baseDelay = TimeSpan.FromSeconds(5); // Base delay of 5 seconds
            var maxDelay = TimeSpan.FromHours(3); // Maximum delay of 3 hours
            var randomFactor = (random.NextDouble() * 0.5) + 0.75; // Random factor between 0.75 and 1.25

            var delay = TimeSpan.FromSeconds(baseDelay.TotalSeconds * Math.Pow(2, retryCount - 1) * randomFactor);
            return delay > maxDelay ? maxDelay : delay;
        }

        private void EnqueueToRetryPendingPurchases(InternalPurchaseItem item)
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
                item.PlayerId = _authProvider?.PlayerId;
            }

            // Remove the existing if any.
            var oldItem = GetThenRemoveFromRetryPendingPurchasesByOrderID(item.OrderId);

            if (oldItem != null && !string.IsNullOrEmpty(oldItem.VerifyOrderRequest?.ReceiptData))
            {
                item.VerifyOrderRequest.ReceiptData = oldItem.VerifyOrderRequest.ReceiptData;
                _log.Info($"Preserved ReceiptData for {item.OrderId}: {item.VerifyOrderRequest.ReceiptData}");
            }

            // Re-enqueues from failure paths do not carry the store purchase token; keep it so a
            // later successful retry can still finish the store transaction.
            if (string.IsNullOrEmpty(item.PurchaseToken) && !string.IsNullOrEmpty(oldItem?.PurchaseToken))
            {
                item.PurchaseToken = oldItem.PurchaseToken;
            }

            _log.Info($"Enqueue to retry pending purchase: {item.OrderId}");
            _waitingPendingPurchases.Enqueue(item);
            SavePendingPurchases(_waitingPendingPurchases.ToList());
        }

        /// <summary>
        /// Finds and removes a pending purchase item by order ID, returning the removed item (or empty if not found).
        /// </summary>
        /// <param name="orderId">The order ID to find and remove.</param>
        /// <returns>The removed purchase item, or an empty item if not found.</returns>
        public InternalPurchaseItem GetThenRemoveFromRetryPendingPurchasesByOrderID(int orderId)
        {
            _log.Info($"Remove from retry pending purchase: {orderId}");
            EnsurePendingPurchaseQueueLoaded();

            var oldItem = _waitingPendingPurchases.FirstOrDefault(item => item.OrderId == orderId);
            if (oldItem == null)
            {
                _log.Warning($"No pending purchase found with order ID: {orderId}");
                return new InternalPurchaseItem();
            } else {
                // Rebuild the queue excluding the item with the specified OrderID
                var updatedQueue = new Queue<InternalPurchaseItem>(
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

        /// <summary>
        /// Removes a pending purchase from the retry queue by order ID and persists the updated list.
        /// </summary>
        /// <param name="orderId">The order ID to remove.</param>
        public void RemoveFromRetryPendingPurchasesByOrderID(int orderId)
        {
            _log.Info($"Remove from retry pending purchase: {orderId}");
            EnsurePendingPurchaseQueueLoaded();

            // Rebuild the queue excluding the item with the specified OrderID
            var updatedQueue = new Queue<InternalPurchaseItem>(
                _waitingPendingPurchases.Where(item => item.OrderId != orderId));

            _waitingPendingPurchases.Clear();
            foreach (var item in updatedQueue)
            {
                _waitingPendingPurchases.Enqueue(item);
            }
            
            SavePendingPurchases(_waitingPendingPurchases.ToList());
        }

        // PlayerPrefs flag: 0 = no completed purchase recorded yet, 1 = first purchase already reported.
        // Scoped per device/install (matches NoctuaFirstOpen). Survives reinstalls via BackupPlayerPrefs.
        private const string FirstPurchasePrefKey = "NoctuaFirstPurchase";

        /// <summary>
        /// Fires the <c>first_purchase</c> event exactly once per device, the first time a
        /// purchase completes. Idempotent — no-op on subsequent completions. Game devs do not
        /// need to implement this themselves.
        /// </summary>
        /// <remarks>
        /// Forwards to <see cref="INativeTracker.TrackCustomEvent"/> so native third-party trackers
        /// (Adjust, Firebase, Facebook, AppsFlyer, etc.) receive the event. Amount/currency travel
        /// inside the payload dictionary.
        /// </remarks>
        /// <param name="orderRequest">The completed order. Payload mirrors <c>purchase_completed</c>.</param>
        private void SendFirstPurchaseEventIfFirstTime(OrderRequest orderRequest)
        {
            // Every log line in this method carries the [first_purchase] tag so the whole flow
            // (skips, send, failures) can be found with one grep.
            if (orderRequest == null)
            {
                _log.Debug("[first_purchase] skipped: orderRequest is null");
                return;
            }

            try
            {
                if (PlayerPrefs.GetInt(FirstPurchasePrefKey, 0) == 1)
                {
                    _log.Debug($"[first_purchase] skipped: already sent previously (FirstPurchasePrefKey set), orderID {orderRequest.Id}, productID {orderRequest.ProductId}");
                    return;
                }

                _log.Debug($"[first_purchase] first-time purchase detected, preparing event for orderID {orderRequest.Id}, productID {orderRequest.ProductId}");

                var payload = new Dictionary<string, IConvertible>
                {
                    { "product_id", orderRequest.ProductId },
                    { "amount", orderRequest.PriceInUSD },
                    { "currency", "USD" },
                    { "order_id", orderRequest.Id },
                    { "orig_amount", orderRequest.Price },
                    { "orig_currency", orderRequest.Currency }
                };

                // Third-party native trackers (Adjust, Firebase, Facebook, AppsFlyer, etc.).
                // Amount/currency travel inside the payload dictionary.
                _nativePlugin?.TrackCustomEvent("first_purchase", payload);

                PlayerPrefs.SetInt(FirstPurchasePrefKey, 1);
                PlayerPrefs.Save();

                _log.Info($"[first_purchase] event sent for orderID {orderRequest.Id}, productID {orderRequest.ProductId}");
            }
            catch (Exception e)
            {
                // Never let first_purchase tracking disrupt the purchase flow.
                _log.Warning($"[first_purchase] failed to send event: {e.Message}");
            }
        }

        private void TrackTaichiIAP(OrderRequest order)
        {
            // All log lines carry the [taichi] tag so the IAP-revenue path can be found alongside
            // the ad-impression taichi steps with one grep.
            if (order == null)
            {
                _log.Warning("[taichi] iap skipped: order is null");
                return;
            }

            if (_taichiConfig == null)
            {
                _log.Warning("[taichi] iap config is null, skipping revenue tracking");
                return;
            }

            try
            {
                var localPriceInUsd = (double)order.LocalPriceInUsd;
                var rate            = (double)order.CurrencyToUsdRate;

                // Dump every input the decision below depends on, so a single [taichi] grep
                // shows exactly what the backend returned for this order.
                _log.Debug(
                    $"[taichi] iap tracking start — orderId={order.Id}, productId={order.ProductId}, " +
                    $"price={order.Price:G} {order.Currency}, priceInUsd={order.PriceInUSD:G}, " +
                    $"local_price_in_usd={localPriceInUsd:G}, currency_to_usd_rate={rate:G}, " +
                    $"revenueThreshold={_taichiConfig.RevenueThreshold:G} USD");

                // No exchange rate from backend -> cannot value this in USD on device.
                // Track directly (per purchase, no accumulator) so the backend converts manually.
                if (localPriceInUsd <= 0)
                {
                    _log.Debug(
                        $"[taichi] no usd rate (local_price_in_usd={localPriceInUsd:G}) — routing to " +
                        $"taichi_iap_revenue_unconverted; accumulator left untouched");

                    var unconverted = new Dictionary<string, IConvertible>
                    {
                        { "value",                order.Price },                                                      // raw local amount
                        { "currency",             string.IsNullOrWhiteSpace(order.Currency) ? "USD" : order.Currency },
                        { "currency_to_usd_rate", rate },                                                             // 0
                        { "local_price_in_usd",   localPriceInUsd }                                                  // 0
                    };
                    _log.Debug(
                        $"[taichi] taichi_iap_revenue_unconverted fired — payload: value={order.Price:G}, " +
                        $"currency={(string.IsNullOrWhiteSpace(order.Currency) ? "USD" : order.Currency)}, " +
                        $"currency_to_usd_rate={rate:G}, local_price_in_usd={localPriceInUsd:G}");
                    if (_nativePlugin == null)
                        _log.Warning("[taichi] native plugin is null — taichi_iap_revenue_unconverted NOT delivered to native tracker");
                    _nativePlugin?.TrackCustomEvent("taichi_iap_revenue_unconverted", unconverted);
                    return;
                }

                // Rate available: accumulate in USD against the USD threshold.
                var stored = double.TryParse(PlayerPrefs.GetString(KeyIAPTotalRevenue, "0"), out var prev) ? prev : 0.0;
                var totalRevenue = stored + localPriceInUsd;
                PlayerPrefs.SetString(KeyIAPTotalRevenue, totalRevenue.ToString("G"));
                PlayerPrefs.SetString(KeyIAPRevenueCurrency, "USD");

                _log.Debug(
                    $"[taichi] iap accumulate — stored={stored:G} + thisOrder={localPriceInUsd:G} = " +
                    $"total={totalRevenue:G} USD (threshold={_taichiConfig.RevenueThreshold:G})");
                _log.Debug($"[taichi] iap revenue progress: {totalRevenue:G} / {_taichiConfig.RevenueThreshold:G} USD");

                if (totalRevenue >= _taichiConfig.RevenueThreshold)
                {
                    var payload = new Dictionary<string, IConvertible>
                    {
                        { "value",                totalRevenue },     // cumulative USD
                        { "currency",             "USD" },
                        { "currency_to_usd_rate", rate },
                        { "local_price_in_usd",   localPriceInUsd }  // this order's USD
                    };
                    _log.Debug(
                        $"[taichi] taichi_iap_revenue fired — threshold crossed ({totalRevenue:G} >= " +
                        $"{_taichiConfig.RevenueThreshold:G}); payload: value={totalRevenue:G}, currency=USD, " +
                        $"currency_to_usd_rate={rate:G}, local_price_in_usd={localPriceInUsd:G}");
                    if (_nativePlugin == null)
                        _log.Warning("[taichi] native plugin is null — taichi_iap_revenue NOT delivered to native tracker");
                    _nativePlugin?.TrackCustomEvent("taichi_iap_revenue", payload);
                    PlayerPrefs.SetString(KeyIAPTotalRevenue, "0");
                    PlayerPrefs.DeleteKey(KeyIAPRevenueCurrency);
                    PlayerPrefs.Save();
                    _log.Info("[taichi] iap accumulator reset to 0 after firing taichi_iap_revenue");
                }
                else
                {
                    _log.Debug(
                        $"[taichi] taichi_iap_revenue NOT fired — below threshold " +
                        $"({totalRevenue:G} < {_taichiConfig.RevenueThreshold:G}); accumulator persisted");
                }
            }
            catch (Exception e)
            {
                _log.Warning($"[taichi] iap revenue tracking failed: {e.Message}\n{e.StackTrace}");
            }
        }

        /// <summary>
        /// Retrieves the list of completed purchases from local storage.
        /// </summary>
        /// <returns>A sorted list of completed purchase items.</returns>
        public List<InternalPurchaseItem> GetPurchaseHistory()
        {
            _log.Info("Noctua.GetPurchaseHistory");
            var json = PlayerPrefs.GetString("NoctuaPurchaseHistory", string.Empty);
            _log.Info($"PurchaseHistory data: {json}");

            if (string.IsNullOrEmpty(json))
            {
                return new List<InternalPurchaseItem>();
            }

            try
            {
                var purchaseHistory = JsonConvert.DeserializeObject<List<InternalPurchaseItem>>(json);
                if (purchaseHistory == null)
                {
                    purchaseHistory = new List<InternalPurchaseItem>();
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
                return new List<InternalPurchaseItem>();
            }
        }

        private void AddToPurchaseHistory(InternalPurchaseItem item)
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
                item.PlayerId = _authProvider?.PlayerId;
            }

            // Remove the existing if any.
            RemoveFromPurchaseHistoryByOrderID(item.OrderId);
            var list = GetPurchaseHistory();
            list.Add(item);
            SavePurchaseHistory(list);
        }

        private void SavePurchaseHistory(List<InternalPurchaseItem> orders)
        {
            _log.Debug("save pending purchases to player prefs");
            var updatedJson = JsonConvert.SerializeObject(orders);
            PlayerPrefs.SetString("NoctuaPurchaseHistory", updatedJson);
            PlayerPrefs.Save();

            // Leave it here for debuggin purpose.
            //GetPurchaseHistory();
        }

        /// <summary>
        /// Removes a purchase record from the local purchase history by order ID.
        /// </summary>
        /// <param name="orderId">The order ID to remove from history.</param>
        public void RemoveFromPurchaseHistoryByOrderID(int orderId)
        {
            _log.Info($"Remove from purchase history: {orderId}");

            var oldList = GetPurchaseHistory();
            var newList = oldList.Where(item => item.OrderId != orderId);
            SavePurchaseHistory(newList.ToList());
        }

        /// <summary>
        /// Restore purchased products by checking their purchase status.
        /// </summary>
        /// <param name="productIds">List of product ids to restore.</param>
        /// <returns>List of purchased product ids.</returns>
        public async Task<List<string>> RestorePurchasedProducts(List<string> productIds)
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

        /// <summary>
        /// Get whether a product is purchased using native billing or server verification.
        /// </summary>
        /// <param name="productId">Product identifier.</param>
        /// <returns>True when purchased; false otherwise.</returns>
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

        /// <summary>
        /// Get full purchase status detail for a product, including subscription info.
        /// Returns a <see cref="ProductPurchaseStatus"/> with fields like ExpiryTime, IsAutoRenewing, etc.
        /// On iOS, ExpiryTime is populated from StoreKit 2 Transaction.expirationDate for auto-renewable subscriptions.
        /// On Android, ExpiryTime is always 0 (not available client-side).
        /// </summary>
        /// <param name="productId">Product identifier.</param>
        /// <returns>Full purchase status detail.</returns>
        public Task<ProductPurchaseStatus> GetProductPurchaseStatusDetailAsync(string productId)
        {
            var tcs = new TaskCompletionSource<ProductPurchaseStatus>();

            #if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.GetProductPurchaseStatusDetail(productId, (status) =>
            {
                _log.Debug($"[GoogleBilling] ProductPurchaseStatusDetail: {status.ProductId}, Purchased: {status.IsPurchased}, ExpiryTime: {status.ExpiryTime}");
                tcs.SetResult(status);
            });
            #elif UNITY_IOS && !UNITY_EDITOR
            _nativePlugin.GetProductPurchaseStatusDetail(productId, (status) =>
            {
                _log.Debug($"[IosPlugin] ProductPurchaseStatusDetail: {status.ProductId}, Purchased: {status.IsPurchased}, ExpiryTime: {status.ExpiryTime}");
                tcs.SetResult(status);
            });
            #else
            _log.Warning("GetProductPurchaseStatusDetailAsync is not supported on this platform.");
            tcs.SetResult(new ProductPurchaseStatus());
            #endif

            return tcs.Task;
        }

        /// <summary>
        /// Batch check purchase state for multiple product ids.
        /// </summary>
        /// <param name="productIds">List of product ids to check.</param>
        /// <returns>List of purchased product ids.</returns>
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

        // PlayerPrefs key for the refund-tracking store (separate from NoctuaPurchaseHistory).
        // Each entry is a RefundTrackingEntry persisted on every successful purchase.
        private const string RefundTrackingPrefsKey = "NoctuaRefundTracking";

        /// <summary>
        /// Reads the refund-tracking store from PlayerPrefs. Returns an empty list on first run
        /// or when the persisted JSON is malformed. Public so tests and tooling can inspect the
        /// store without going through reflection.
        /// </summary>
        public List<RefundTrackingEntry> GetRefundTrackingEntries()
        {
            var json = PlayerPrefs.GetString(RefundTrackingPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json)) return new List<RefundTrackingEntry>();

            try
            {
                return JsonConvert.DeserializeObject<List<RefundTrackingEntry>>(json)
                    ?? new List<RefundTrackingEntry>();
            }
            catch (Exception e)
            {
                _log.Error("Failed to parse refund tracking store: " + e);
                PlayerPrefs.DeleteKey(RefundTrackingPrefsKey);
                return new List<RefundTrackingEntry>();
            }
        }

        /// <summary>
        /// Persists a confirmed non-consumable product into the refund-tracking store. Replaces
        /// any existing entry for the same productId (newest write wins). Called from the
        /// successful-purchase path after a positive <see cref="GetPurchaseStatusAsync"/> probe.
        /// </summary>
        private void SaveRefundTrackingEntry(string productId, PaymentType paymentType)
        {
            if (string.IsNullOrEmpty(productId)) return;

            var entries = GetRefundTrackingEntries();
            entries.RemoveAll(e => e?.ProductId == productId);
            entries.Add(new RefundTrackingEntry
            {
                ProductId = productId,
                PaymentType = paymentType,
                Timestamp = DateTime.UtcNow,
            });

            try
            {
                PlayerPrefs.SetString(RefundTrackingPrefsKey, JsonConvert.SerializeObject(entries));
                PlayerPrefs.Save();
            }
            catch (Exception e)
            {
                _log.Error("Failed to persist refund tracking store: " + e);
            }
        }

        /// <summary>
        /// Returns true when a non-consumable product the player previously purchased is no longer
        /// reported as purchased by the native store and is therefore eligible to be removed from
        /// the player's inventory due to a refund. The SDK looks up the most recent matching
        /// <see cref="RefundTrackingEntry"/> in <c>NoctuaRefundTracking</c> (PlayerPrefs) and
        /// applies the three remaining conditions:
        ///   - the stored payment type is "playstore" or "appstore"
        ///   - the stored timestamp is older than <paramref name="minAgeDays"/> days
        ///   - <see cref="GetPurchaseStatusAsync"/> reports the product is no longer purchased
        ///
        /// Consumables never enter the tracking store (they always return <c>false</c> from
        /// <see cref="GetPurchaseStatusAsync"/> right after purchase, which is how the SDK
        /// auto-detects them) so the game does not need to declare a consumable type — just call
        /// <c>Noctua.IAP.CheckRefundEligibilityAsync(productId)</c>.
        ///
        /// Returns <c>false</c> conservatively when no matching record is found (legacy purchases,
        /// fresh installs, products never bought through the SDK) so the SDK never tells the game
        /// to remove an item it has no record of.
        /// </summary>
        /// <param name="productId">Product identifier to check.</param>
        /// <param name="minAgeDays">Minimum age in days before a missing purchase is considered refunded. Default 2.</param>
        /// <returns>True when the item is eligible to be removed from the player's inventory.</returns>
        public async Task<bool> CheckRefundEligibilityAsync(string productId, int minAgeDays = 2)
        {
            if (string.IsNullOrEmpty(productId)) return false;

            var entry = GetRefundTrackingEntries()
                .Where(e => e != null && e.ProductId == productId)
                .OrderByDescending(e => e.Timestamp)
                .FirstOrDefault();

            if (entry == null)
            {
                _log.Debug($"CheckRefundEligibilityAsync: no refund-tracking entry for '{productId}', returning false");
                return false;
            }

            if (entry.PaymentType != PaymentType.playstore && entry.PaymentType != PaymentType.appstore) return false;

            var ts = entry.Timestamp.Kind == DateTimeKind.Utc ? entry.Timestamp : entry.Timestamp.ToUniversalTime();
            if (ts >= DateTime.UtcNow.AddDays(-minAgeDays)) return false;

            bool isStillPurchased = await GetPurchaseStatusAsync(productId);
            if (isStillPurchased) return false;

            _log.Debug($"CheckRefundEligibilityAsync: '{productId}' flagged refunded (paymentType={entry.PaymentType}, purchasedAt={ts:o})");
            return true;
        }

        /// <summary>
        /// Internal wrapper turning callback-based purchase check into Task.
        /// </summary>
        /// <param name="productId">Product id to check.</param>
        /// <returns>Task that completes with boolean purchased state.</returns>
        private Task<bool> CheckIfProductPurchasedAsync(string productId)
        {
            var tcs = new TaskCompletionSource<bool>();

            CheckIfProductPurchased(productId, (result) =>
            {
                _log.Debug("CheckIfProductPurchased result: " + result);
                tcs.SetResult(result);
            });

            return tcs.Task;
        }
        
        /// <summary>
        /// Backwards-compatible API: checks if product is purchased and invokes callback.
        /// </summary>
        /// <param name="productId">Product id to check.</param>
        /// <param name="callback">Callback invoked with result.</param>
        public void CheckIfProductPurchased(string productId, System.Action<bool> callback)
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            GoogleBillingInstance.GetPurchasedProductById(productId, (purchase) =>
            {
                if (purchase != null && purchase.Success)
                {
                    _log.Debug($"[GoogleBilling] Product purchased: {purchase.ProductId}, State: {purchase.PurchaseState}");
                    callback?.Invoke(true);
                }
                else
                {
                    _log.Debug($"[GoogleBilling] Product '{productId}' is not purchased or not found.");
                    callback?.Invoke(false);
                }
            });
            #elif UNITY_IOS && !UNITY_EDITOR
            _nativePlugin.GetProductPurchasedById(productId, (hasPurchased) =>
            {
                _log.Debug($"[IosPlugin] Product '{productId}' purchased: {hasPurchased}");
                callback?.Invoke(hasPurchased);
            });
            #else
            _log.Warning("CheckIfProductPurchased is not supported on this platform.");
            callback?.Invoke(false);
            #endif
        }

        [Preserve]
        public class Config
        {
            public string BaseUrl;
            public string ClientId;
            public bool isIAPDisabled;
        }

        
        /// <summary>
        /// Claims a redeem code for the current authenticated user.
        /// </summary>
        /// <param name="code">The redeem code to claim (e.g. "ABCD-EFGH-IJKL-MNOP").</param>
        /// <returns>Response containing order IDs and a message.</returns>
        public async UniTask<ClaimRedeemCodeResponse> ClaimRedeemAsync(string code)
        {
            EnsureEnabled();

            if (string.IsNullOrWhiteSpace(code))
            {
                throw new NoctuaException(NoctuaErrorCode.Application, "Redeem code must not be empty.");
            }

            var recentAccount = _authProvider?.RecentAccount;

            if (recentAccount?.User?.Id == null || recentAccount.User.Id <= 0)
            {
                throw new NoctuaException(NoctuaErrorCode.Authentication, "User not authenticated. Please authenticate first.");
            }

            var url = $"{_config.BaseUrl}/redeem-codes/claim";

            var requestBody = new ClaimRedeemCodeRequest
            {
                Code = code,
                UserId = recentAccount.User.Id
            };

            var request = new HttpRequest(HttpMethod.Post, url)
                .WithHeader("X-CLIENT-ID", _config.ClientId)
                .WithHeader("X-BUNDLE-ID", Application.identifier)
                .WithHeader("Authorization", "Bearer " + _accessTokenProvider.AccessToken)
                .WithJsonBody(requestBody);

            try
            {
                return await request.Send<ClaimRedeemCodeResponse>();
            }
            catch (NoctuaException ex) when (ex.ErrorCode == (int)NoctuaErrorCode.Networking)
            {
                var match = System.Text.RegularExpressions.Regex.Match(ex.Message, @"Response:\s*'(.+)'");

                if (match.Success)
                {
                    try
                    {
                        var errorResponse = JsonConvert.DeserializeObject<ErrorResponse>(match.Groups[1].Value);

                        if (errorResponse?.ErrorCode > 0 && !string.IsNullOrEmpty(errorResponse.ErrorMessage))
                        {
                            throw new NoctuaException(
                                (NoctuaErrorCode)errorResponse.ErrorCode,
                                errorResponse.ErrorMessage
                            );
                        }
                    }
                    catch (NoctuaException)
                    {
                        throw;
                    }
                    catch (Exception)
                    {
                        // JSON parsing failed, fall through to rethrow original
                    }
                }

                throw;
            }
        }

        private void EnsureEnabled()
        {
            if (_enabled) return;

            _log.Error("Noctua IAP is not enabled due to initialization failure.");
                
            throw new NoctuaException(NoctuaErrorCode.Application, "Noctua IAP is not enabled due to initialization failure.");
        }

        internal void Enable()
        {
            _enabled = true;
        }
    }
}
