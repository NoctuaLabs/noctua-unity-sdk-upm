using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Native StoreKit operations the router triggers. Implemented by <c>IosPlugin</c> via P/Invoke;
    /// replaced by a fake in tests.
    /// </summary>
    public interface IStoreKitNativeCommands
    {
        /// <summary>Starts an App Store purchase for the product.</summary>
        void Purchase(string productId);

        /// <summary>Queries the purchase status of the product.</summary>
        void QueryPurchaseStatus(string productId);

        /// <summary>Queries product details (used for the storefront currency).</summary>
        void QueryActiveCurrency(string productId);
    }

    /// <summary>
    /// Matches iOS StoreKit bridge events to the calls that asked for them.
    ///
    /// The native bridge used to keep one global callback slot per operation and complete whatever
    /// happened to be waiting when any event arrived. That broke under fast or overlapping calls:
    /// a second status check overwrote (and later dropped) the first, one status result answered
    /// checks for other products, an unrelated StoreKit error failed an in-flight purchase, and a
    /// replayed old transaction completed a brand-new purchase — leaving the real one unfinished.
    ///
    /// This router instead matches every event by product id (and transaction time / id for
    /// purchases), coalesces duplicate native queries, bounds every non-interactive wait with a
    /// timeout, and hands transactions nobody asked for to an unsolicited-purchase handler so
    /// they can still be verified and delivered. All state is guarded by one lock and every
    /// callback runs outside it, so calls and native events may arrive on any thread.
    /// </summary>
    public sealed class StoreKitRequestRouter
    {
        /// <summary>Native <c>StoreKitErrorCode.userCanceled</c>.</summary>
        public const int UserCanceledErrorCode = 1;

        /// <summary>Max wait for a purchase status query before answering "not purchased".</summary>
        public const int StatusTimeoutMs = 10_000;

        /// <summary>Max wait for a currency query before failing it.</summary>
        public const int CurrencyTimeoutMs = 15_000;

        /// <summary>
        /// A transaction dated more than this before its purchase request started is a replay of an
        /// older, unfinished transaction — not the result of that request. Deliberately large: the
        /// transaction date comes from the App Store but the start time from the device clock, and a
        /// device clock set ahead must never make a genuine new purchase look stale (that purchase
        /// would then only be delivered through the unsolicited path, after the payment flow gave up).
        /// </summary>
        public const long StaleTransactionToleranceMs = 24 * 60 * 60 * 1000L;

        /// <summary>
        /// Failure message when StoreKit answers a new purchase with a transaction that was already
        /// delivered. That transaction is handed to the unsolicited handler to be finished; the purchase
        /// fails so the caller can try again instead of waiting forever.
        /// </summary>
        public const string RepeatedTransactionMessage =
            "The App Store returned a previous transaction for this product that was still being finished. Please try the purchase again.";

        /// <summary>Native <c>StoreKitErrorCode.error</c>.</summary>
        public const int GenericErrorCode = 6;

        private const string NoProductDetailsMessage = "No product details found";
        private const int MaxBufferedUnsolicited = 32;
        private const int MaxRememberedTokens = 256;

        // Messages the native SDK (<= 0.40.0) emits via onStoreKitError for failures that belong to
        // one purchase. Newer native versions also report them as a scoped purchase result.
        private const string ProductNotFoundPrefix = "Product not found: ";
        private const string FetchProductForPurchasePrefix = "Failed to fetch product for purchase: ";
        private const string QueryProductDetailsFailedPrefix = "Failed to query product details";

        private readonly object _lock = new();
        private readonly IStoreKitNativeCommands _native;
        private readonly Func<long> _nowMs;
        private readonly Action<int, Action> _schedule;
        private readonly ILogger _log;
        private readonly bool _verifyPurchasesOnServer;

        private readonly Dictionary<string, PendingPurchase> _purchases = new();
        private readonly Dictionary<string, StatusQuery> _statusQueries = new();
        private readonly List<CurrencyWaiter> _currencyWaiters = new();
        private string _currencyInFlightProductId;

        private readonly HashSet<string> _deliveredTokens = new();
        private readonly Queue<string> _deliveredTokenOrder = new();
        private readonly Queue<StoreKitTransaction> _bufferedUnsolicited = new();
        private Action<StoreKitTransaction> _unsolicitedHandler;

        private int _nextId;

        /// <param name="native">Native StoreKit commands.</param>
        /// <param name="nowMs">Wall clock in milliseconds since epoch (same clock as transaction dates).</param>
        /// <param name="schedule">Runs the action after the given delay in milliseconds.</param>
        /// <param name="log">Optional logger.</param>
        /// <param name="verifyPurchasesOnServer">
        /// When true, a successful purchase is delivered from the server-verification event and the
        /// plain purchase-completed success event is ignored (it carries the same transaction).
        /// </param>
        public StoreKitRequestRouter(
            IStoreKitNativeCommands native,
            Func<long> nowMs,
            Action<int, Action> schedule,
            ILogger log = null,
            bool verifyPurchasesOnServer = true)
        {
            _native = native ?? throw new ArgumentNullException(nameof(native));
            _nowMs = nowMs ?? throw new ArgumentNullException(nameof(nowMs));
            _schedule = schedule ?? throw new ArgumentNullException(nameof(schedule));
            _log = log;
            _verifyPurchasesOnServer = verifyPurchasesOnServer;
        }

        // MARK: - Purchases

        /// <summary>
        /// Starts a purchase. The callback fires exactly once with the matched result. Purchases have
        /// no timeout here — the user may sit on the payment sheet — so callers bound the wait.
        /// </summary>
        public void BeginPurchase(string productId, Action<StoreKitPurchaseOutcome> callback)
        {
            if (string.IsNullOrEmpty(productId))
            {
                Invoke(callback, Failure(0, "Product ID is null or empty"));
                return;
            }

            PendingPurchase superseded;
            lock (_lock)
            {
                _purchases.TryGetValue(productId, out superseded);
                _purchases[productId] = new PendingPurchase(callback, _nowMs());
            }

            if (superseded != null)
            {
                _log?.Warning($"Purchase for '{productId}' superseded by a newer purchase request");
                Invoke(superseded.Callback, Failure(0, "Superseded by a newer purchase request"));
            }

            _native.Purchase(productId);
        }

        /// <summary>Native <c>onServerVerificationRequired</c>: a purchased transaction to verify.</summary>
        public void OnServerVerificationRequired(StoreKitTransaction transaction)
        {
            if (transaction == null) return;

            if (!transaction.Success)
            {
                TryFailPurchase(transaction.ProductId, transaction.ErrorCode, transaction.Message ?? "Server verification required");
                return;
            }

            RoutePurchasedTransaction(transaction);
        }

        /// <summary>Native <c>onPurchaseCompleted</c>: a purchased, failed, or deferred transaction.</summary>
        public void OnPurchaseCompleted(StoreKitTransaction transaction)
        {
            if (transaction == null) return;

            if (transaction.Success)
            {
                if (!_verifyPurchasesOnServer)
                {
                    RoutePurchasedTransaction(transaction);
                }
                return;
            }

            var message = transaction.Message ?? "Purchase failed";
            if (!TryFailPurchase(transaction.ProductId, transaction.ErrorCode, message))
            {
                _log?.Warning($"Failed transaction for '{transaction.ProductId}' with no pending purchase (errorCode={transaction.ErrorCode}): {message}");
            }
        }

        /// <summary>
        /// Native <c>onPurchaseUpdated</c> (restored transactions). Never completes a purchase: a
        /// restore is not the answer to a purchase request.
        /// </summary>
        public void OnPurchaseUpdated(StoreKitTransaction transaction)
        {
            _log?.Debug($"Purchase updated for '{transaction?.ProductId}' (not routed to a pending purchase)");
        }

        /// <summary>
        /// Native <c>onStoreKitError</c>. Only errors that name a product are routed; an unscoped error
        /// must never fail an unrelated in-flight purchase.
        /// </summary>
        public void OnStoreKitError(int errorCode, string message)
        {
            var productId = TryParsePurchaseScopedProductId(message);
            if (productId != null)
            {
                if (!TryFailPurchase(productId, errorCode, message))
                {
                    _log?.Debug($"StoreKit error for '{productId}' already handled or not pending: {message}");
                }
                return;
            }

            if (message != null && message.StartsWith(QueryProductDetailsFailedPrefix, StringComparison.Ordinal))
            {
                CompleteInFlightCurrency(null, message);
                return;
            }

            _log?.Warning($"Unrouted StoreKit error (code={errorCode}): {message}");
        }

        /// <summary>
        /// Registers the handler for purchased transactions no pending purchase claimed (replays of
        /// unfinished transactions, purchases finished after the caller gave up). Transactions that
        /// arrived before a handler existed are delivered immediately.
        /// </summary>
        public void SetUnsolicitedPurchaseHandler(Action<StoreKitTransaction> handler)
        {
            List<StoreKitTransaction> buffered;
            lock (_lock)
            {
                _unsolicitedHandler = handler;
                if (handler == null) return;

                buffered = _bufferedUnsolicited.ToList();
                _bufferedUnsolicited.Clear();
            }

            foreach (var transaction in buffered)
            {
                Invoke(handler, transaction);
            }
        }

        // MARK: - Purchase status

        /// <summary>
        /// Queries purchase status. Concurrent checks for the same product share one native query;
        /// a check that arrives while a query is in flight gets a fresh follow-up query, so it never
        /// sees a status read before it asked. Checks for different products never cross.
        /// </summary>
        public void RequestPurchaseStatus(string productId, Action<ProductPurchaseStatus> callback)
        {
            if (string.IsNullOrEmpty(productId))
            {
                Invoke(callback, new ProductPurchaseStatus());
                return;
            }

            var id = Interlocked.Increment(ref _nextId);
            bool issueQuery;
            lock (_lock)
            {
                if (!_statusQueries.TryGetValue(productId, out var query))
                {
                    query = new StatusQuery();
                    _statusQueries[productId] = query;
                }

                query.Waiters.Add(new StatusWaiter(id, callback));
                issueQuery = !query.InFlight;
                if (issueQuery)
                {
                    query.InFlight = true;
                    query.CutoffId = id;
                }
            }

            _schedule(StatusTimeoutMs, () => TimeOutStatusWaiter(productId, id));

            if (issueQuery)
            {
                _native.QueryPurchaseStatus(productId);
            }
        }

        /// <summary>Native <c>onProductPurchaseStatusResult</c>.</summary>
        public void OnPurchaseStatus(ProductPurchaseStatus status)
        {
            if (string.IsNullOrEmpty(status?.ProductId)) return;

            List<StatusWaiter> answered;
            bool reissue;
            lock (_lock)
            {
                if (!_statusQueries.TryGetValue(status.ProductId, out var query) || !query.InFlight)
                {
                    return;
                }

                // Only waiters registered before this query was issued get its answer.
                answered = query.Waiters.Where(w => w.Id <= query.CutoffId).ToList();
                query.Waiters.RemoveAll(w => w.Id <= query.CutoffId);

                reissue = query.Waiters.Count > 0;
                if (reissue)
                {
                    query.CutoffId = query.Waiters.Max(w => w.Id);
                }
                else
                {
                    _statusQueries.Remove(status.ProductId);
                }
            }

            foreach (var waiter in answered)
            {
                Invoke(waiter.Callback, status);
            }

            if (reissue)
            {
                _native.QueryPurchaseStatus(status.ProductId);
            }
        }

        private void TimeOutStatusWaiter(string productId, int id)
        {
            StatusWaiter waiter;
            lock (_lock)
            {
                if (!_statusQueries.TryGetValue(productId, out var query)) return;

                waiter = query.Waiters.FirstOrDefault(w => w.Id == id);
                if (waiter == null) return;

                query.Waiters.Remove(waiter);
                if (query.Waiters.Count == 0)
                {
                    _statusQueries.Remove(productId);
                }
            }

            _log?.Warning($"Purchase status query for '{productId}' timed out after {StatusTimeoutMs}ms");
            Invoke(waiter.Callback, new ProductPurchaseStatus { ProductId = productId });
        }

        // MARK: - Active currency

        /// <summary>
        /// Queries the storefront currency for a product. Queries run one at a time because product
        /// detail responses for an unknown product are empty and cannot be matched by id otherwise.
        /// </summary>
        public void RequestActiveCurrency(string productId, Action<bool, string> callback)
        {
            if (string.IsNullOrEmpty(productId))
            {
                InvokeCurrency(callback, false, "Product ID is null or empty");
                return;
            }

            var id = Interlocked.Increment(ref _nextId);
            bool issueQuery;
            lock (_lock)
            {
                _currencyWaiters.Add(new CurrencyWaiter(id, productId, callback));
                issueQuery = _currencyInFlightProductId == null;
                if (issueQuery)
                {
                    _currencyInFlightProductId = productId;
                }
            }

            _schedule(CurrencyTimeoutMs, () => TimeOutCurrencyWaiter(id));

            if (issueQuery)
            {
                _native.QueryActiveCurrency(productId);
            }
        }

        /// <summary>Native <c>onProductDetailsLoaded</c>.</summary>
        public void OnProductDetails(IReadOnlyList<StoreKitProductCurrency> products)
        {
            CompleteInFlightCurrency(products ?? Array.Empty<StoreKitProductCurrency>(), NoProductDetailsMessage);
        }

        /// <param name="products">
        /// Product details to answer with, or null for a query failure. A non-empty list that does not
        /// contain the in-flight product is details for some other call and is ignored.
        /// </param>
        private void CompleteInFlightCurrency(IReadOnlyList<StoreKitProductCurrency> products, string failureMessage)
        {
            List<CurrencyWaiter> answered;
            StoreKitProductCurrency match;
            string next;
            lock (_lock)
            {
                var productId = _currencyInFlightProductId;
                if (productId == null) return;

                match = products?.FirstOrDefault(p => p.ProductId == productId);
                if (match == null && products != null && products.Count > 0) return;

                answered = _currencyWaiters.Where(w => w.ProductId == productId).ToList();
                _currencyWaiters.RemoveAll(w => w.ProductId == productId);
                next = AdvanceCurrencyQueue();
            }

            var success = match != null;
            foreach (var waiter in answered)
            {
                InvokeCurrency(waiter.Callback, success, success ? match.Currency ?? string.Empty : failureMessage);
            }

            if (next != null)
            {
                _native.QueryActiveCurrency(next);
            }
        }

        private void TimeOutCurrencyWaiter(int id)
        {
            CurrencyWaiter waiter;
            string next = null;
            lock (_lock)
            {
                waiter = _currencyWaiters.FirstOrDefault(w => w.Id == id);
                if (waiter == null) return;

                _currencyWaiters.Remove(waiter);
                var stillWaitingForInFlight = _currencyWaiters.Any(w => w.ProductId == _currencyInFlightProductId);
                if (waiter.ProductId == _currencyInFlightProductId && !stillWaitingForInFlight)
                {
                    next = AdvanceCurrencyQueue();
                }
            }

            _log?.Warning($"Active currency query for '{waiter.ProductId}' timed out after {CurrencyTimeoutMs}ms");
            InvokeCurrency(waiter.Callback, false, "Active currency query timed out");

            if (next != null)
            {
                _native.QueryActiveCurrency(next);
            }
        }

        // Must hold _lock. Returns the product id whose query should be issued next, if any.
        private string AdvanceCurrencyQueue()
        {
            _currencyInFlightProductId = _currencyWaiters.Count > 0 ? _currencyWaiters[0].ProductId : null;
            return _currencyInFlightProductId;
        }

        // MARK: - Lifecycle

        /// <summary>
        /// Fails every pending purchase, status check and currency query (e.g. StoreKit disposed), so no
        /// caller waits forever.
        /// </summary>
        public void FailAll(string reason)
        {
            List<PendingPurchase> purchases;
            List<(string ProductId, StatusWaiter Waiter)> statusWaiters;
            List<CurrencyWaiter> currencyWaiters;
            lock (_lock)
            {
                purchases = _purchases.Values.ToList();
                statusWaiters = _statusQueries
                    .SelectMany(q => q.Value.Waiters.Select(w => (q.Key, w)))
                    .ToList();
                currencyWaiters = _currencyWaiters.ToList();

                _purchases.Clear();
                _statusQueries.Clear();
                _currencyWaiters.Clear();
                _currencyInFlightProductId = null;
            }

            foreach (var purchase in purchases)
            {
                Invoke(purchase.Callback, Failure(0, reason));
            }
            foreach (var (productId, waiter) in statusWaiters)
            {
                Invoke(waiter.Callback, new ProductPurchaseStatus { ProductId = productId });
            }
            foreach (var waiter in currencyWaiters)
            {
                InvokeCurrency(waiter.Callback, false, reason);
            }
        }

        // MARK: - Pure helpers

        /// <summary>
        /// Extracts the product id from a native StoreKit error that belongs to one purchase, or null
        /// when the error is not purchase-scoped.
        /// </summary>
        public static string TryParsePurchaseScopedProductId(string message)
        {
            if (string.IsNullOrEmpty(message)) return null;

            if (message.StartsWith(ProductNotFoundPrefix, StringComparison.Ordinal))
            {
                var productId = message.Substring(ProductNotFoundPrefix.Length).Trim();
                return productId.Length > 0 ? productId : null;
            }

            if (message.StartsWith(FetchProductForPurchasePrefix, StringComparison.Ordinal))
            {
                var rest = message.Substring(FetchProductForPurchasePrefix.Length);
                var separator = rest.IndexOf(": ", StringComparison.Ordinal);
                var productId = (separator >= 0 ? rest.Substring(0, separator) : rest).Trim();
                return productId.Length > 0 ? productId : null;
            }

            return null;
        }

        /// <summary>
        /// Converts an outcome to the <c>(success, message)</c> pair the purchase flow has always
        /// consumed: on success a JSON object with the receipt, transaction token and consumable type;
        /// on failure the reason, labelled "User cancelled" when StoreKit reports a user cancellation
        /// (StoreKit 1's own text for that case, "SKErrorDomain error 2", never says "cancel").
        /// </summary>
        public static string FormatPurchaseCallbackMessage(StoreKitPurchaseOutcome outcome)
        {
            if (outcome == null) return "Purchase failed";

            if (outcome.Success)
            {
                var transaction = outcome.Transaction ?? new StoreKitTransaction();
                return JsonConvert.SerializeObject(new Dictionary<string, object>
                {
                    { "receipt", transaction.Receipt ?? string.Empty },
                    { "purchaseToken", transaction.PurchaseToken ?? string.Empty },
                    { "consumableType", transaction.ConsumableType }
                });
            }

            var reason = outcome.Message ?? "Purchase failed";
            return outcome.ErrorCode == UserCanceledErrorCode ? $"User cancelled: {reason}" : reason;
        }

        /// <summary>
        /// Whether a purchased transaction is a replay of an older unfinished transaction rather than
        /// the result of a purchase request started at <paramref name="purchaseStartedAtMs"/>.
        /// </summary>
        public static bool IsStaleTransaction(StoreKitTransaction transaction, long purchaseStartedAtMs)
        {
            return transaction.PurchaseTimeMs > 0 &&
                   transaction.PurchaseTimeMs < purchaseStartedAtMs - StaleTransactionToleranceMs;
        }

        // MARK: - Internals

        private void RoutePurchasedTransaction(StoreKitTransaction transaction)
        {
            if (string.IsNullOrEmpty(transaction?.ProductId)) return;

            PendingPurchase matched = null;
            PendingPurchase answeredWithRepeat = null;
            Action<StoreKitTransaction> unsolicitedHandler = null;
            lock (_lock)
            {
                var token = transaction.PurchaseToken;
                // StoreKit 1 can re-deliver a transaction that was already delivered and finished, and
                // then answer the next purchase of the same product with that same transaction. A repeat
                // is never the answer to a pending purchase, and it must never be dropped: it goes to the
                // unsolicited handler, which finishes it (or verifies it) so StoreKit stops re-sending it.
                var isRepeat = !string.IsNullOrEmpty(token) && _deliveredTokens.Contains(token);
                if (!string.IsNullOrEmpty(token) && !isRepeat)
                {
                    RememberToken(token);
                }

                _purchases.TryGetValue(transaction.ProductId, out var pending);
                if (pending != null && !isRepeat && !IsStaleTransaction(transaction, pending.StartedAtMs))
                {
                    _purchases.Remove(transaction.ProductId);
                    matched = pending;
                }
                else
                {
                    if (pending != null && isRepeat)
                    {
                        // Release the caller instead of letting it wait for a new transaction StoreKit may
                        // never send while the old one is unfinished.
                        _purchases.Remove(transaction.ProductId);
                        answeredWithRepeat = pending;
                    }

                    if (_unsolicitedHandler != null)
                    {
                        unsolicitedHandler = _unsolicitedHandler;
                    }
                    else
                    {
                        if (_bufferedUnsolicited.Count >= MaxBufferedUnsolicited)
                        {
                            _bufferedUnsolicited.Dequeue();
                        }
                        _bufferedUnsolicited.Enqueue(transaction);
                    }
                }
            }

            if (matched != null)
            {
                Invoke(matched.Callback, new StoreKitPurchaseOutcome
                {
                    Success = true,
                    ErrorCode = 0,
                    Message = string.Empty,
                    Transaction = transaction
                });
                return;
            }

            if (answeredWithRepeat != null)
            {
                _log?.Warning($"Purchase for '{transaction.ProductId}' was answered with already-delivered transaction {transaction.PurchaseToken}; failing it so it can be retried");
                Invoke(answeredWithRepeat.Callback, Failure(GenericErrorCode, RepeatedTransactionMessage));
            }

            _log?.Info($"Unsolicited purchased transaction for '{transaction.ProductId}' (token {transaction.PurchaseToken})");
            if (unsolicitedHandler != null)
            {
                Invoke(unsolicitedHandler, transaction);
            }
        }

        private bool TryFailPurchase(string productId, int errorCode, string message)
        {
            if (string.IsNullOrEmpty(productId)) return false;

            PendingPurchase pending;
            lock (_lock)
            {
                if (!_purchases.TryGetValue(productId, out pending)) return false;
                _purchases.Remove(productId);
            }

            Invoke(pending.Callback, Failure(errorCode, message));
            return true;
        }

        // Must hold _lock.
        private void RememberToken(string token)
        {
            _deliveredTokens.Add(token);
            _deliveredTokenOrder.Enqueue(token);
            while (_deliveredTokenOrder.Count > MaxRememberedTokens)
            {
                _deliveredTokens.Remove(_deliveredTokenOrder.Dequeue());
            }
        }

        private static StoreKitPurchaseOutcome Failure(int errorCode, string message)
        {
            return new StoreKitPurchaseOutcome { Success = false, ErrorCode = errorCode, Message = message };
        }

        private void Invoke<T>(Action<T> callback, T value)
        {
            try
            {
                callback?.Invoke(value);
            }
            catch (Exception e)
            {
                _log?.Warning($"StoreKit callback threw: {e}");
            }
        }

        private void InvokeCurrency(Action<bool, string> callback, bool success, string value)
        {
            try
            {
                callback?.Invoke(success, value);
            }
            catch (Exception e)
            {
                _log?.Warning($"StoreKit currency callback threw: {e}");
            }
        }

        private sealed class PendingPurchase
        {
            public readonly Action<StoreKitPurchaseOutcome> Callback;
            public readonly long StartedAtMs;

            public PendingPurchase(Action<StoreKitPurchaseOutcome> callback, long startedAtMs)
            {
                Callback = callback;
                StartedAtMs = startedAtMs;
            }
        }

        private sealed class StatusQuery
        {
            public readonly List<StatusWaiter> Waiters = new();
            public bool InFlight;
            public int CutoffId;
        }

        private sealed class StatusWaiter
        {
            public readonly int Id;
            public readonly Action<ProductPurchaseStatus> Callback;

            public StatusWaiter(int id, Action<ProductPurchaseStatus> callback)
            {
                Id = id;
                Callback = callback;
            }
        }

        private sealed class CurrencyWaiter
        {
            public readonly int Id;
            public readonly string ProductId;
            public readonly Action<bool, string> Callback;

            public CurrencyWaiter(int id, string productId, Action<bool, string> callback)
            {
                Id = id;
                ProductId = productId;
                Callback = callback;
            }
        }
    }
}
