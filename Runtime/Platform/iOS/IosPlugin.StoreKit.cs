using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace com.noctuagames.sdk
{
#if UNITY_IOS
    /// <summary>
    /// StoreKit half of <see cref="IosPlugin"/>. Native calls carry no callback; every StoreKit result
    /// arrives as one event (kind + JSON with the product id) and is matched to its caller by
    /// <see cref="StoreKitRequestRouter"/>. See <c>NoctuaInterop.m</c> for why the old per-operation
    /// callback slots were replaced.
    /// </summary>
    internal partial class IosPlugin
    {
        // Event kinds — keep in sync with NoctuaStoreKitEventKind in NoctuaInterop.m.
        private const int StoreKitEventPurchaseCompleted = 1;
        private const int StoreKitEventPurchaseUpdated = 2;
        private const int StoreKitEventServerVerificationRequired = 3;
        private const int StoreKitEventPurchaseStatus = 4;
        private const int StoreKitEventProductDetails = 5;
        private const int StoreKitEventError = 6;

        [DllImport("__Internal")]
        private static extern void noctuaSetStoreKitEventCallback(StoreKitEventDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaPurchaseItem(string productId);

        [DllImport("__Internal")]
        private static extern void noctuaQueryPurchaseStatus(string productId);

        [DllImport("__Internal")]
        private static extern void noctuaQueryActiveCurrency(string productId);

        [DllImport("__Internal")]
        private static extern void noctuaRegisterProduct(string productId, int consumableType);

        [DllImport("__Internal")]
        private static extern void noctuaCompletePurchaseProcessing(string purchaseToken, int consumableType, bool verified, int requestId, CompletePurchaseProcessingDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaRestorePurchases();

        [DllImport("__Internal")]
        private static extern void noctuaDisposeStoreKit();

        [DllImport("__Internal")]
        private static extern bool noctuaIsStoreKitReady();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void StoreKitEventDelegate(int kind, IntPtr jsonPtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CompletePurchaseProcessingDelegate(int requestId, bool success);

        // Delegates handed to native code are kept in static fields so they are never collected.
        private static readonly StoreKitEventDelegate StoreKitEventCallbackDelegate = StoreKitEventCallback;
        private static readonly CompletePurchaseProcessingDelegate CompletePurchaseProcessingCallbackDelegate = CompletePurchaseProcessingCallback;

        private static readonly StoreKitRequestRouter StoreKitRouter = new(
            new NativeStoreKitCommands(),
            () => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            (delayMs, action) => Task.Delay(delayMs).ContinueWith(_ => action(), TaskScheduler.Default),
            // Own logger: static initializers in another partial file (like _sLog) may not have run yet.
            new NoctuaLogger(typeof(StoreKitRequestRouter))
        );

        private static readonly object CompletePurchaseProcessingLock = new();
        private static readonly Dictionary<int, Action<bool>> CompletePurchaseProcessingCallbacks = new();
        private static int _nextCompletePurchaseProcessingId;
        private static int _storeKitEventCallbackRegistered;

        /// <summary>
        /// Registers the native StoreKit event callback once, before the first StoreKit call, so no
        /// event (including transactions StoreKit replays at startup) is dropped.
        /// </summary>
        private static void EnsureStoreKitEventCallbackRegistered()
        {
            if (Interlocked.Exchange(ref _storeKitEventCallbackRegistered, 1) == 0)
            {
                noctuaSetStoreKitEventCallback(StoreKitEventCallbackDelegate);
            }
        }

        /// <inheritdoc />
        public void PurchaseItem(string productId, Action<bool, string> completion)
        {
            EnsureStoreKitEventCallbackRegistered();
            StoreKitRouter.BeginPurchase(productId, outcome =>
            {
                if (outcome.Success)
                {
                    _sLog.Debug($"StoreKit purchase succeeded for '{productId}' (token {outcome.Transaction?.PurchaseToken})");
                }
                else
                {
                    _sLog.Warning($"StoreKit purchase failed for '{productId}' (errorCode={outcome.ErrorCode}): {outcome.Message}");
                }
                completion?.Invoke(outcome.Success, StoreKitRequestRouter.FormatPurchaseCallbackMessage(outcome));
            });
        }

        /// <inheritdoc />
        public void GetProductPurchasedById(string productId, Action<bool> completion)
        {
            EnsureStoreKitEventCallbackRegistered();
            StoreKitRouter.RequestPurchaseStatus(productId, status => completion?.Invoke(status.IsPurchased));
        }

        /// <inheritdoc />
        public void GetProductPurchaseStatusDetail(string productId, Action<ProductPurchaseStatus> callback)
        {
            EnsureStoreKitEventCallbackRegistered();
            StoreKitRouter.RequestPurchaseStatus(productId, status => callback?.Invoke(status));
        }

        /// <inheritdoc />
        public void GetReceiptProductPurchasedStoreKit1(string productId, Action<string> completion)
        {
            if (string.IsNullOrEmpty(productId))
            {
                completion?.Invoke(string.Empty);
                return;
            }

            EnsureStoreKitEventCallbackRegistered();
            StoreKitRouter.RequestPurchaseStatus(productId, status =>
                completion?.Invoke(status.IsPurchased ? status.PurchaseToken : null));
        }

        /// <inheritdoc />
        public void GetActiveCurrency(string productId, Action<bool, string> completion)
        {
            EnsureStoreKitEventCallbackRegistered();
            StoreKitRouter.RequestActiveCurrency(productId, completion);
        }

        /// <inheritdoc />
        public void SetUnsolicitedPurchaseHandler(Action<StoreKitTransaction> handler)
        {
            EnsureStoreKitEventCallbackRegistered();
            StoreKitRouter.SetUnsolicitedPurchaseHandler(handler);
        }

        /// <summary>
        /// Registers a product with its consumable type in the native StoreKit layer.
        /// </summary>
        /// <param name="productId">The App Store product identifier.</param>
        /// <param name="consumableType">The consumable type of the product.</param>
        public void RegisterProduct(string productId, NoctuaConsumableType consumableType)
        {
            _log.Debug($"IosPlugin.RegisterProduct: {productId}, type={consumableType}");
            EnsureStoreKitEventCallbackRegistered();
            noctuaRegisterProduct(productId, (int)consumableType);
        }

        /// <summary>
        /// Completes purchase processing in the native StoreKit layer after server verification.
        /// </summary>
        /// <param name="purchaseToken">The purchase token (transaction ID) to finalize.</param>
        /// <param name="consumableType">The consumable type of the product.</param>
        /// <param name="verified">Whether the server verification succeeded.</param>
        /// <param name="callback">Callback with success status.</param>
        public void CompletePurchaseProcessing(string purchaseToken, NoctuaConsumableType consumableType, bool verified, Action<bool> callback)
        {
            _log.Debug($"IosPlugin.CompletePurchaseProcessing: token={purchaseToken}, type={consumableType}, verified={verified}");

            // Each call gets its own id so concurrent completions (payment flow + retry worker) never
            // overwrite each other's callback.
            var requestId = Interlocked.Increment(ref _nextCompletePurchaseProcessingId);
            lock (CompletePurchaseProcessingLock)
            {
                CompletePurchaseProcessingCallbacks[requestId] = callback;
            }

            EnsureStoreKitEventCallbackRegistered();
            noctuaCompletePurchaseProcessing(purchaseToken, (int)consumableType, verified, requestId, CompletePurchaseProcessingCallbackDelegate);
        }

        /// <summary>
        /// Restores all previously completed purchases via the native StoreKit layer.
        /// </summary>
        public void RestorePurchases()
        {
            _log.Debug("IosPlugin.RestorePurchases");
            EnsureStoreKitEventCallbackRegistered();
            noctuaRestorePurchases();
        }

        /// <summary>
        /// Disposes the native StoreKit service and releases resources. Anything still waiting on
        /// StoreKit is completed with a failure so no caller hangs.
        /// </summary>
        public void DisposeStoreKit()
        {
            _log.Debug("IosPlugin.DisposeStoreKit");
            noctuaDisposeStoreKit();
            StoreKitRouter.FailAll("StoreKit disposed");
        }

        /// <summary>
        /// Returns whether the native StoreKit service is initialized and ready for operations.
        /// </summary>
        /// <returns>True if StoreKit is ready, false otherwise.</returns>
        public bool IsStoreKitReady()
        {
            return noctuaIsStoreKitReady();
        }

        [AOT.MonoPInvokeCallback(typeof(StoreKitEventDelegate))]
        private static void StoreKitEventCallback(int kind, IntPtr jsonPtr)
        {
            try
            {
                var json = jsonPtr != IntPtr.Zero ? Marshal.PtrToStringUTF8(jsonPtr) : null;
                if (string.IsNullOrEmpty(json))
                {
                    _sLog.Warning($"StoreKit event {kind} arrived without a payload");
                    return;
                }

                DispatchStoreKitEvent(kind, json);
            }
            catch (Exception e)
            {
                _sLog.Warning($"Failed to handle StoreKit event {kind}: {e}");
            }
        }

        private static void DispatchStoreKitEvent(int kind, string json)
        {
            switch (kind)
            {
            case StoreKitEventPurchaseCompleted:
                StoreKitRouter.OnPurchaseCompleted(JsonConvert.DeserializeObject<StoreKitTransaction>(json));
                break;
            case StoreKitEventPurchaseUpdated:
                StoreKitRouter.OnPurchaseUpdated(JsonConvert.DeserializeObject<StoreKitTransaction>(json));
                break;
            case StoreKitEventServerVerificationRequired:
                StoreKitRouter.OnServerVerificationRequired(JsonConvert.DeserializeObject<StoreKitTransaction>(json));
                break;
            case StoreKitEventPurchaseStatus:
                StoreKitRouter.OnPurchaseStatus(JsonConvert.DeserializeObject<ProductPurchaseStatus>(json));
                break;
            case StoreKitEventProductDetails:
                StoreKitRouter.OnProductDetails(JsonConvert.DeserializeObject<List<StoreKitProductCurrency>>(json));
                break;
            case StoreKitEventError:
                var error = JsonConvert.DeserializeObject<StoreKitErrorPayload>(json);
                StoreKitRouter.OnStoreKitError(error?.Code ?? 0, error?.Message);
                break;
            default:
                _sLog.Warning($"Unknown StoreKit event kind {kind}");
                break;
            }
        }

        [AOT.MonoPInvokeCallback(typeof(CompletePurchaseProcessingDelegate))]
        private static void CompletePurchaseProcessingCallback(int requestId, bool success)
        {
            Action<bool> callback;
            lock (CompletePurchaseProcessingLock)
            {
                if (!CompletePurchaseProcessingCallbacks.TryGetValue(requestId, out callback))
                {
                    _sLog.Warning($"CompletePurchaseProcessing callback for unknown request {requestId}");
                    return;
                }
                CompletePurchaseProcessingCallbacks.Remove(requestId);
            }

            try
            {
                callback?.Invoke(success);
            }
            catch (Exception e)
            {
                _sLog.Warning($"CompletePurchaseProcessing callback threw: {e}");
            }
        }

        private sealed class NativeStoreKitCommands : IStoreKitNativeCommands
        {
            public void Purchase(string productId) => noctuaPurchaseItem(productId);
            public void QueryPurchaseStatus(string productId) => noctuaQueryPurchaseStatus(productId);
            public void QueryActiveCurrency(string productId) => noctuaQueryActiveCurrency(productId);
        }

        private sealed class StoreKitErrorPayload
        {
            public int Code;
            public string Message;
        }
    }
#endif
}
