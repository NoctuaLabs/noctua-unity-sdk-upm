using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace com.noctuagames.sdk
{
#if UNITY_IOS
    /// <summary>
    /// iOS implementation of <see cref="INativePlugin"/> that bridges to the native Swift SDK via P/Invoke.
    /// Uses <c>[DllImport("__Internal")]</c> for calling Objective-C/Swift interop functions and
    /// <c>[MonoPInvokeCallback]</c> static delegates for receiving native callbacks.
    /// </summary>
    internal class IosPlugin : INativePlugin
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(IosPlugin));
        private static readonly ILogger _sLog = new NoctuaLogger(typeof(IosPlugin));

        [DllImport("__Internal")]
        private static extern void noctuaInitialize(bool verifyPurchasesOnServer, bool useStoreKit1);

        [DllImport("__Internal")]
        private static extern void noctuaTrackAdRevenue(string source, double revenue, string currency, string extraPayloadJson);

        [DllImport("__Internal")]
        private static extern void noctuaTrackPurchase(string orderId, double amount, string currency, string extraPayloadJson);

        [DllImport("__Internal")]
        private static extern void noctuaTrackCustomEvent(string eventName, string payloadJson);

        [DllImport("__Internal")]
        private static extern void noctuaTrackCustomEventWithRevenue(string eventName, double amount, string currency, string payloadJson);

        [DllImport("__Internal")]
        private static extern void noctuaPurchaseItem(string productId, CompletionDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetProductPurchasedById(string productId, CompletionProductPurchasedDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetReceiptProductPurchasedStoreKit1(string productId, CompletionGetReceiptDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetActiveCurrency(string productId, CompletionDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetProductPurchaseStatusDetail(string productId, ProductPurchaseStatusDetailDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaPutAccount(long gameId, long playerId, string rawData);

        [DllImport("__Internal")]
        private static extern void noctuaGetAllAccounts(StringDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetSingleAccount(long gameId, long playerId, StringDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaDeleteAccount(long gameId, long playerId);

        [DllImport("__Internal")]
        private static extern void noctuaOnOnline();

        [DllImport("__Internal")]
        private static extern void noctuaOnOffline();

        [DllImport("__Internal")]
        private static extern void _TAG_ShowDatePicker(int mode, double unix, int pickerId);

        [DllImport("__Internal")]
        private static extern void DismissDatePicker();

        [DllImport("__Internal")]
        private static extern void noctuaGetFirebaseInstallationID(GetFirebaseIDCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetFirebaseAnalyticsSessionID(GetFirebaseSessionIDCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetFirebaseMessagingToken(GetFirebaseMessagingTokenCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetFirebaseRemoteConfigString(string key, GetFirebaseRemoteConfigStringCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetFirebaseRemoteConfigBoolean(string key, GetFirebaseRemoteConfigBooleanCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetFirebaseRemoteConfigDouble(string key, GetFirebaseRemoteConfigDoubleCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetFirebaseRemoteConfigLong(string key, GetFirebaseRemoteConfigLongCallbackDelegate callback);
        
        [DllImport("__Internal")]
        private static extern void noctuaGetAdjustAttribution(GetAdjustAttributionJsonCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaSaveEvents(string eventsJson);

        [DllImport("__Internal")]
        private static extern void noctuaGetEvents(GetEventsCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaDeleteEvents();

        // Additional StoreKit functions
        [DllImport("__Internal")]
        private static extern void noctuaRegisterProduct(string productId, int consumableType);

        [DllImport("__Internal")]
        private static extern void noctuaCompletePurchaseProcessing(string purchaseToken, int consumableType, bool verified, BoolCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaRestorePurchases();

        [DllImport("__Internal")]
        private static extern void noctuaDisposeStoreKit();

        [DllImport("__Internal")]
        private static extern bool noctuaIsStoreKitReady();

        // Per-row event storage
        [DllImport("__Internal")]
        private static extern void noctuaInsertEvent(string eventJson);

        [DllImport("__Internal")]
        private static extern void noctuaGetEventsBatch(int limit, int offset, GetEventsBatchCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaDeleteEventsByIds(string idsJson, DeleteEventsByIdsCallbackDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetEventCount(GetEventCountCallbackDelegate callback);

        // In-App Review
        [DllImport("__Internal")]
        private static extern void noctuaRequestInAppReview();

        // Native Lifecycle Callback
        [DllImport("__Internal")]
        private static extern void noctuaRegisterLifecycleCallback(NativeLifecycleCallbackDelegate callback);

        // Store the callback to be used in the static methods
        private static Action<string> storedLifecycleCallback;
        private static Action<bool, string> storedCompletion;
        private static Action<bool> storedHasPurchasedCompletion;
        private static Action<string> storedGetReceiptCompletion;
        private static Action<string> storedFirebaseInstallationIdCompletion;
        private static Action<string> storedFirebaseSessionIdCompletion;
        private static Action<string> storedFirebaseMessagingTokenCompletion;
        private static Action<string> storedFirebaseRemoteConfigStringCompletion;
        private static Action<bool> storedFirebaseRemoteConfigBooleanCompletion;
        private static Action<double> storedFirebaseRemoteConfigDoubleCompletion;
        private static Action<long> storedFirebaseRemoteConfigLongCompletion;
        private static Action<string> storedAdjustAttributionCallback;
        private static Action<List<string>> storedGetEventsCompletion;
        private static Action<List<NativeEvent>> storedGetEventsBatchCompletion;
        private static Action<int> storedDeleteEventsByIdsCompletion;
        private static Action<int> storedGetEventCountCompletion;
        private static Action<bool> storedCompletePurchaseProcessingCompletion;
        private static Action<ProductPurchaseStatus> storedPurchaseStatusDetailCompletion;

        // Define delegates for the native callbacks
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CompletionDelegate(bool success, IntPtr messagePtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CompletionProductPurchasedDelegate(bool hasPurchased);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CompletionGetReceiptDelegate(string receipt);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetFirebaseIDCallbackDelegate(string installationId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetFirebaseSessionIDCallbackDelegate(string sessionId);

        private delegate void GetFirebaseMessagingTokenCallbackDelegate(string token);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetFirebaseRemoteConfigStringCallbackDelegate(string value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetFirebaseRemoteConfigBooleanCallbackDelegate(bool value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetFirebaseRemoteConfigDoubleCallbackDelegate(double value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetFirebaseRemoteConfigLongCallbackDelegate(long value);
        
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetAdjustAttributionJsonCallbackDelegate(string json);


        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void NativeLifecycleCallbackDelegate(IntPtr lifecycleEventPtr);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetEventsCallbackDelegate(IntPtr eventsJson);

        // Bool callback delegate (for completePurchaseProcessing, etc.)
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void BoolCallbackDelegate(bool success);

        // Per-row event delegates
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetEventsBatchCallbackDelegate(IntPtr eventsJson);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void DeleteEventsByIdsCallbackDelegate(int deletedCount);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void GetEventCountCallbackDelegate(int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void ProductPurchaseStatusDetailDelegate(IntPtr statusJsonPtr);

        //Delegate for methods returning string values
        [AOT.MonoPInvokeCallback(typeof(CompletionDelegate))]
        private static void CompletionCallback(bool success, IntPtr messagePtr)
        {
            string message = messagePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(messagePtr) : "Unknown error";
            storedCompletion?.Invoke(success, message);
        }

        [AOT.MonoPInvokeCallback(typeof(CompletionProductPurchasedDelegate))]
        private static void CompletionHasPurchasedCallback(bool hasPurchased)
        {
            storedHasPurchasedCompletion?.Invoke(hasPurchased);
        }

        [AOT.MonoPInvokeCallback(typeof(CompletionGetReceiptDelegate))]
        private static void CompletionGetReceiptCallback(string receipt)
        {
            storedGetReceiptCompletion?.Invoke(receipt);
        } 

        [AOT.MonoPInvokeCallback(typeof(GetFirebaseIDCallbackDelegate))]
        private static void GetFirebaseInstallationIDCallback(string installationId) 
        {
            storedFirebaseInstallationIdCompletion?.Invoke(installationId);
        }

        [AOT.MonoPInvokeCallback(typeof(GetFirebaseSessionIDCallbackDelegate))]
        private static void GetFirebaseSessionIDCallback(string sessionId)
        {
            storedFirebaseSessionIdCompletion?.Invoke(sessionId);
        }

        [AOT.MonoPInvokeCallback(typeof(GetFirebaseMessagingTokenCallbackDelegate))]
        private static void GetFirebaseMessagingTokenCallback(string token)
        {
            storedFirebaseMessagingTokenCompletion?.Invoke(token ?? string.Empty);
        }

        [AOT.MonoPInvokeCallback(typeof(GetFirebaseRemoteConfigStringCallbackDelegate))]
        private static void GetFirebaseRemoteConfigStringCallback(string value)
        {
            storedFirebaseRemoteConfigStringCompletion?.Invoke(value);
        }

        [AOT.MonoPInvokeCallback(typeof(GetFirebaseRemoteConfigBooleanCallbackDelegate))]
        private static void GetFirebaseRemoteConfigBooleanCallback(bool value)
        {
            storedFirebaseRemoteConfigBooleanCompletion?.Invoke(value);
        }

        [AOT.MonoPInvokeCallback(typeof(GetFirebaseRemoteConfigDoubleCallbackDelegate))]
        private static void GetFirebaseRemoteConfigDoubleCallback(double value)
        {
            storedFirebaseRemoteConfigDoubleCompletion?.Invoke(value);
        }

        [AOT.MonoPInvokeCallback(typeof(GetFirebaseRemoteConfigLongCallbackDelegate))]
        private static void GetFirebaseRemoteConfigLongCallback(long value)
        {
            storedFirebaseRemoteConfigLongCompletion?.Invoke(value);
        }

        [AOT.MonoPInvokeCallback(typeof(GetAdjustAttributionJsonCallbackDelegate))]
        private static void GetAdjustAttributionJsonCallback(string json)
        {
            storedAdjustAttributionCallback?.Invoke(json);
            storedAdjustAttributionCallback = null;
        }

        [AOT.MonoPInvokeCallback(typeof(GetEventsCallbackDelegate))]
        private static void GetEventsCallback(IntPtr eventsJsonPtr)
        {
            try
            {
                if (eventsJsonPtr == IntPtr.Zero)
                {
                    storedGetEventsCompletion?.Invoke(new List<string>());
                    return;
                }

                string json = Marshal.PtrToStringUTF8(eventsJsonPtr);

                if (string.IsNullOrEmpty(json))
                {
                    storedGetEventsCompletion?.Invoke(new List<string>());
                    return;
                }

                var list = JsonUtilityHelper.FromJsonArray<string>(json);
                storedGetEventsCompletion?.Invoke(list);
            }
            catch (Exception e)
            {
                _sLog.Warning($"[Noctua] GetEvents callback failed: {e.Message}");
                storedGetEventsCompletion?.Invoke(new List<string>());
            }
        }

        [AOT.MonoPInvokeCallback(typeof(BoolCallbackDelegate))]
        private static void CompletePurchaseProcessingCallback(bool success)
        {
            storedCompletePurchaseProcessingCompletion?.Invoke(success);
        }

        [AOT.MonoPInvokeCallback(typeof(ProductPurchaseStatusDetailDelegate))]
        private static void ProductPurchaseStatusDetailCallback(IntPtr statusJsonPtr)
        {
            try
            {
                if (statusJsonPtr == IntPtr.Zero)
                {
                    storedPurchaseStatusDetailCompletion?.Invoke(new ProductPurchaseStatus());
                    return;
                }

                string json = Marshal.PtrToStringUTF8(statusJsonPtr);

                if (string.IsNullOrEmpty(json) || json == "{}")
                {
                    storedPurchaseStatusDetailCompletion?.Invoke(new ProductPurchaseStatus());
                    return;
                }

                var status = JsonConvert.DeserializeObject<ProductPurchaseStatus>(json) ?? new ProductPurchaseStatus();
                storedPurchaseStatusDetailCompletion?.Invoke(status);
            }
            catch (Exception e)
            {
                _sLog.Warning($"[Noctua] ProductPurchaseStatusDetail callback failed: {e.Message}");
                storedPurchaseStatusDetailCompletion?.Invoke(new ProductPurchaseStatus());
            }
        }

        /// <inheritdoc />
        public void Init(List<string> activeBundleIds)
        {
            noctuaInitialize(true, true);
        }

        /// <inheritdoc />
        public void OnApplicationPause(bool pause)
        {
        }

        /// <inheritdoc />
        public void TrackAdRevenue(string source, double revenue, string currency, Dictionary<string, IConvertible> extraPayload)
        {
            noctuaTrackAdRevenue(source, revenue, currency, JsonConvert.SerializeObject(extraPayload));
        }

        /// <inheritdoc />
        public void TrackPurchase(string orderId, double amount, string currency, Dictionary<string, IConvertible> extraPayload)
        {
            noctuaTrackPurchase(orderId, amount, currency, JsonConvert.SerializeObject(extraPayload));
        }

        /// <inheritdoc />
        public void TrackCustomEvent(string eventName, Dictionary<string, IConvertible> payload)
        {
            noctuaTrackCustomEvent(eventName, JsonConvert.SerializeObject(payload));

            _log.Info($"forwarded event '{eventName}' to native tracker");
        }

        /// <inheritdoc />
        public void TrackCustomEventWithRevenue(string eventName, double revenue, string currency, Dictionary<string, IConvertible> payload)
        {
            noctuaTrackCustomEventWithRevenue(eventName, revenue, currency, JsonConvert.SerializeObject(payload));
        }

        /// <inheritdoc />
        public void PurchaseItem(string productId, Action<bool, string> completion)
        {
            if (string.IsNullOrEmpty(productId))
            {
                _log.Error("Product ID is null or empty");
                completion?.Invoke(false, "Product ID is null or empty");
                return;
            }

            storedCompletion = completion;
            noctuaPurchaseItem(productId, new CompletionDelegate(CompletionCallback));

            _log.Debug("noctuaPurchaseItem called");
        }

        /// <inheritdoc />
        public void GetProductPurchasedById(string productId, Action<bool> completion)
        {
            if (string.IsNullOrEmpty(productId))
            {
                _log.Error("Product ID is null or empty");
                completion?.Invoke(false);
                return;
            }


            storedHasPurchasedCompletion = completion;
            noctuaGetProductPurchasedById(productId, new CompletionProductPurchasedDelegate(CompletionHasPurchasedCallback));

            _log.Debug("noctuaGetProductPurchasedById called");
        }

        /// <inheritdoc />
        public void GetProductPurchaseStatusDetail(string productId, Action<ProductPurchaseStatus> callback)
        {
            if (string.IsNullOrEmpty(productId))
            {
                _log.Error("Product ID is null or empty");
                callback?.Invoke(new ProductPurchaseStatus());
                return;
            }

            storedPurchaseStatusDetailCompletion = callback;
            noctuaGetProductPurchaseStatusDetail(productId, new ProductPurchaseStatusDetailDelegate(ProductPurchaseStatusDetailCallback));

            _log.Debug("noctuaGetProductPurchaseStatusDetail called");
        }

        /// <inheritdoc />
        public void GetReceiptProductPurchasedStoreKit1(string productId, Action<string> completion)
        {
            if (string.IsNullOrEmpty(productId))
            {
                _log.Error("Product ID is null or empty");
                completion?.Invoke(string.Empty);
                return;
            }

            storedGetReceiptCompletion = completion;
            noctuaGetReceiptProductPurchasedStoreKit1(productId, new CompletionGetReceiptDelegate(CompletionGetReceiptCallback));

            _log.Debug("noctuaGetReceiptProductPurchasedStoreKit1 called");
        }

        /// <inheritdoc />
        public void GetActiveCurrency(string productId, Action<bool, string> completion)
        {
            if (string.IsNullOrEmpty(productId))
            {
                _log.Debug("Product ID is null or empty");
                completion?.Invoke(false, "Product ID is null or empty");
                return;
            }

            storedCompletion = completion;
            noctuaGetActiveCurrency(productId, new CompletionDelegate(CompletionCallback));

            _log.Debug("noctuaGetActiveCurrency called");
        }

        /// <inheritdoc />
        public void ShowDatePicker(int year, int month, int day, int id)
        {
            DateTime dateTime = new DateTime(year, month, day);
            double unix = (TimeZoneInfo.ConvertTimeToUtc(dateTime) - new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
            _TAG_ShowDatePicker(2, unix, id);
        }

        /// <inheritdoc />
        public void CloseDatePicker()
        {
            // Calls the method that contains the logic to hide or remove the date picker.
            DismissDatePicker();
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void StringDelegate(IntPtr resultPtr);

        private static NativeAccount _nativeAccount;

        [AOT.MonoPInvokeCallback(typeof(StringDelegate))]
        private static void OnGetAccount(IntPtr resultPtr)
        {
            if (resultPtr == IntPtr.Zero)
            {
                return;
            }

            string rawAccount = Marshal.PtrToStringAnsi(resultPtr);
            _nativeAccount = JsonConvert.DeserializeObject<NativeAccount>(rawAccount);
        }

        /// <inheritdoc />
        public NativeAccount GetAccount(long playerId, long gameId)
        {
            noctuaGetSingleAccount(gameId, playerId, OnGetAccount);

            var account = _nativeAccount;
            _nativeAccount = null;

            return account;
        }

        private static List<NativeAccount> _nativeAccounts;

        [AOT.MonoPInvokeCallback(typeof(StringDelegate))]
        private static void OnGetAccounts(IntPtr resultPtr)
        {
            if (resultPtr == IntPtr.Zero)
            {
                return;
            }

            string rawAccounts = Marshal.PtrToStringAnsi(resultPtr);

            _nativeAccounts = JsonConvert.DeserializeObject<List<NativeAccount>>(rawAccounts) ?? new();
        }

        /// <inheritdoc />
        public List<NativeAccount> GetAccounts()
        {
            noctuaGetAllAccounts(OnGetAccounts);

            var accounts = _nativeAccounts;
            _nativeAccounts = null;

            foreach (var account in accounts)
            {
                _log.Info($"Account: |{account.LastUpdated}|{account.PlayerId}");
            }

            return accounts;
        }

        /// <inheritdoc />
        public void PutAccount(NativeAccount account)
        {
            noctuaPutAccount(account.GameId, account.PlayerId, account.RawData);
        }

        /// <inheritdoc />
        public int DeleteAccount(NativeAccount account)
        {
            noctuaDeleteAccount(account.GameId, account.PlayerId);
            
            return 1;
        }

        /// <inheritdoc />
        public void OnOnline()
        {
            noctuaOnOnline();
            _log.Info($"trigger online mode to native plugin");
        }

        /// <inheritdoc />
        public void OnOffline()
        {
            noctuaOnOffline();
            _log.Info($"trigger offline mode to native plugin");
        }

        /// <inheritdoc />
        public void GetFirebaseInstallationID(Action<string> callback)
        {
            try
            {
                storedFirebaseInstallationIdCompletion = callback;

                noctuaGetFirebaseInstallationID(new GetFirebaseIDCallbackDelegate(GetFirebaseInstallationIDCallback));
                _log.Debug("noctuaGetFirebaseInstallationID called");
            }
            catch (Exception e)
            {
                _log.Warning($"GetFirebaseInstallationID failed: {e.Message}");
                callback?.Invoke(string.Empty);
            }

        }

        /// <inheritdoc />
        public void GetFirebaseAnalyticsSessionID(Action<string> callback)
        {
            try
            {
                storedFirebaseSessionIdCompletion = callback;

                noctuaGetFirebaseAnalyticsSessionID(new GetFirebaseSessionIDCallbackDelegate(GetFirebaseSessionIDCallback));
                _log.Debug("noctuaGetFirebaseAnalyticsSessionID called");
            }
            catch (Exception e)
            {
                _log.Warning($"GetFirebaseAnalyticsSessionID failed: {e.Message}");
                callback?.Invoke(string.Empty);
            }
        }

        /// <inheritdoc />
        public void GetFirebaseMessagingToken(Action<string> callback)
        {
            try
            {
                storedFirebaseMessagingTokenCompletion = callback;

                noctuaGetFirebaseMessagingToken(new GetFirebaseMessagingTokenCallbackDelegate(GetFirebaseMessagingTokenCallback));
                _log.Debug("noctuaGetFirebaseMessagingToken called");
            }
            catch (Exception e)
            {
                _log.Warning($"GetFirebaseMessagingToken failed: {e.Message}");
                callback?.Invoke(string.Empty);
            }
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigString(string key, Action<string> callback)
        {
            try
            {
                storedFirebaseRemoteConfigStringCompletion = callback;

                noctuaGetFirebaseRemoteConfigString(key, new GetFirebaseRemoteConfigStringCallbackDelegate(GetFirebaseRemoteConfigStringCallback));
                _log.Debug($"noctuaGetFirebaseRemoteConfigString called for key: {key}");
            }
            catch (Exception e)
            {
                _log.Warning($"GetFirebaseRemoteConfigString failed for key '{key}': {e.Message}");
                callback?.Invoke(string.Empty);
            }
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigBoolean(string key, Action<bool> callback)
        {
            try
            {
                storedFirebaseRemoteConfigBooleanCompletion = callback;

                noctuaGetFirebaseRemoteConfigBoolean(key, new GetFirebaseRemoteConfigBooleanCallbackDelegate(GetFirebaseRemoteConfigBooleanCallback));
                _log.Debug($"noctuaGetFirebaseRemoteConfigBoolean called for key: {key}");
            }
            catch (Exception e)
            {
                _log.Warning($"GetFirebaseRemoteConfigBoolean failed for key '{key}': {e.Message}");
                callback?.Invoke(false);
            }
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigDouble(string key, Action<double> callback)
        {
            try
            {
                storedFirebaseRemoteConfigDoubleCompletion = callback;

                noctuaGetFirebaseRemoteConfigDouble(key, new GetFirebaseRemoteConfigDoubleCallbackDelegate(GetFirebaseRemoteConfigDoubleCallback));
                _log.Debug($"noctuaGetFirebaseRemoteConfigDouble called for key: {key}");
            }
            catch (Exception e)
            {
                _log.Warning($"GetFirebaseRemoteConfigDouble failed for key '{key}': {e.Message}");
                callback?.Invoke(0.0);
            }
        }

        /// <inheritdoc />
        public void GetFirebaseRemoteConfigLong(string key, Action<long> callback)
        {
            try
            {
                storedFirebaseRemoteConfigLongCompletion = callback;

                noctuaGetFirebaseRemoteConfigLong(key, new GetFirebaseRemoteConfigLongCallbackDelegate(GetFirebaseRemoteConfigLongCallback));
                _log.Debug($"noctuaGetFirebaseRemoteConfigLong called for key: {key}");
            }
            catch (Exception e)
            {
                _log.Warning($"GetFirebaseRemoteConfigLong failed for key '{key}': {e.Message}");
                callback?.Invoke(0L);
            }
        }

        /// <inheritdoc />
        public void GetAdjustAttribution(Action<string> callback)
        {
            try
            {
                storedAdjustAttributionCallback = callback;
                noctuaGetAdjustAttribution(GetAdjustAttributionJsonCallback);
                
                _log.Debug("noctuaGetAdjustAttribution called");
            }
            catch (Exception e)
            {
                _log.Warning($"GetAdjustAttribution failed: {e.Message}");
                callback?.Invoke(string.Empty);
            }
        }
        
        /// <inheritdoc />
        public void SaveEvents(string eventsJson)
        {
            noctuaSaveEvents(eventsJson);
        }

        /// <inheritdoc />
        public void GetEvents(Action<List<string>> callback)
        {
            try
            {
                storedGetEventsCompletion = callback;

                noctuaGetEvents(GetEventsCallback);
                _log.Debug("noctuaGetEvents called");
            }
            catch (Exception e)
            {
                callback?.Invoke(new List<string>());
                
                if(e.Message == null) return;
                _log.Warning($"GetEvents failed: {e.Message}");
            }
        }

        /// <inheritdoc />
        public void DeleteEvents()
        {
            noctuaDeleteEvents();
        }

        [AOT.MonoPInvokeCallback(typeof(GetEventsBatchCallbackDelegate))]
        private static void GetEventsBatchCallback(IntPtr eventsJsonPtr)
        {
            string json = null;

            try
            {
                if (eventsJsonPtr == IntPtr.Zero)
                {
                    storedGetEventsBatchCompletion?.Invoke(new List<NativeEvent>());
                    return;
                }

                json = Marshal.PtrToStringUTF8(eventsJsonPtr);

                if (string.IsNullOrEmpty(json))
                {
                    storedGetEventsBatchCompletion?.Invoke(new List<NativeEvent>());
                    return;
                }

                var events = JsonConvert.DeserializeObject<List<NativeEvent>>(json) ?? new List<NativeEvent>();
                storedGetEventsBatchCompletion?.Invoke(events);
            }
            catch (Exception e)
            {
                _sLog.Warning($"[Noctua] Failed to parse events batch, attempting element-wise recovery: {e.Message}");
                storedGetEventsBatchCompletion?.Invoke(ParseEventsBatchSafe(json));
            }
        }

        /// Attempts to parse a JSON array of <see cref="NativeEvent"/> element by element,
        /// skipping any entries whose <c>eventJson</c> field is malformed.
        /// This prevents a single corrupted row from blocking the entire flush batch.
        private static List<NativeEvent> ParseEventsBatchSafe(string json)
        {
            var result = new List<NativeEvent>();

            try
            {
                var array = JArray.Parse(json);

                foreach (var token in array)
                {
                    try
                    {
                        var evt = token.ToObject<NativeEvent>(JsonSerializer.CreateDefault());

                        if (evt != null)
                            result.Add(evt);
                    }
                    catch (Exception elemEx)
                    {
                        var id = token["id"]?.Value<long>() ?? -1;
                        _sLog.Warning($"[Noctua] Skipping corrupted event id={id} in batch: {elemEx.Message}");
                    }
                }
            }
            catch (Exception parseEx)
            {
                _sLog.Warning($"[Noctua] Could not parse batch as JSON array during recovery: {parseEx.Message}");
            }

            _sLog.Info($"[Noctua] Element-wise recovery completed: {result.Count} events recovered");
            return result;
        }

        [AOT.MonoPInvokeCallback(typeof(DeleteEventsByIdsCallbackDelegate))]
        private static void DeleteEventsByIdsCallback(int deletedCount)
        {
            storedDeleteEventsByIdsCompletion?.Invoke(deletedCount);
        }

        [AOT.MonoPInvokeCallback(typeof(GetEventCountCallbackDelegate))]
        private static void GetEventCountCallback(int count)
        {
            storedGetEventCountCompletion?.Invoke(count);
        }

        /// <inheritdoc />
        public void InsertEvent(string eventJson)
        {
            noctuaInsertEvent(eventJson);
        }

        /// <inheritdoc />
        public void GetEventsBatch(int limit, int offset, Action<List<NativeEvent>> callback)
        {
            try
            {
                storedGetEventsBatchCompletion = callback;
                noctuaGetEventsBatch(limit, offset, GetEventsBatchCallback);
            }
            catch (Exception e)
            {
                callback?.Invoke(new List<NativeEvent>());
                if (e.Message == null) return;
                _log.Warning($"GetEventsBatch failed: {e.Message}");
            }
        }

        /// <inheritdoc />
        public void DeleteEventsByIds(long[] ids, Action<int> callback)
        {
            try
            {
                storedDeleteEventsByIdsCompletion = callback;
                var idsJson = "[" + string.Join(",", ids) + "]";
                noctuaDeleteEventsByIds(idsJson, DeleteEventsByIdsCallback);
            }
            catch (Exception e)
            {
                callback?.Invoke(0);
                if (e.Message == null) return;
                _log.Warning($"DeleteEventsByIds failed: {e.Message}");
            }
        }

        /// <inheritdoc />
        public void GetEventCount(Action<int> callback)
        {
            try
            {
                storedGetEventCountCompletion = callback;
                noctuaGetEventCount(GetEventCountCallback);
            }
            catch (Exception e)
            {
                callback?.Invoke(0);
                if (e.Message == null) return;
                _log.Warning($"GetEventCount failed: {e.Message}");
            }
        }

        /// <summary>
        /// Registers a product with its consumable type in the native StoreKit layer.
        /// </summary>
        /// <param name="productId">The App Store product identifier.</param>
        /// <param name="consumableType">The consumable type of the product.</param>
        public void RegisterProduct(string productId, NoctuaConsumableType consumableType)
        {
            _log.Debug($"IosPlugin.RegisterProduct: {productId}, type={consumableType}");
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
            storedCompletePurchaseProcessingCompletion = callback;
            noctuaCompletePurchaseProcessing(purchaseToken, (int)consumableType, verified, CompletePurchaseProcessingCallback);
        }

        /// <summary>
        /// Restores all previously completed purchases via the native StoreKit layer.
        /// </summary>
        public void RestorePurchases()
        {
            _log.Debug("IosPlugin.RestorePurchases");
            noctuaRestorePurchases();
        }

        /// <summary>
        /// Disposes the native StoreKit service and releases resources.
        /// </summary>
        public void DisposeStoreKit()
        {
            _log.Debug("IosPlugin.DisposeStoreKit");
            noctuaDisposeStoreKit();
        }

        /// <summary>
        /// Returns whether the native StoreKit service is initialized and ready for operations.
        /// </summary>
        /// <returns>True if StoreKit is ready, false otherwise.</returns>
        public bool IsStoreKitReady()
        {
            return noctuaIsStoreKitReady();
        }

        // ------------------------------------
        // Native Lifecycle Callback
        // ------------------------------------

        [AOT.MonoPInvokeCallback(typeof(NativeLifecycleCallbackDelegate))]
        private static void NativeLifecycleCallback(IntPtr lifecycleEventPtr)
        {
            if (lifecycleEventPtr == IntPtr.Zero) return;
            string lifecycleEvent = Marshal.PtrToStringAnsi(lifecycleEventPtr);
            storedLifecycleCallback?.Invoke(lifecycleEvent);
        }

        /// <inheritdoc />
        public void RegisterNativeLifecycleCallback(Action<string> callback)
        {
            storedLifecycleCallback = callback;
            if (callback != null)
            {
                noctuaRegisterLifecycleCallback(NativeLifecycleCallback);
            }
            else
            {
                noctuaRegisterLifecycleCallback(null);
            }
        }

        // ------------------------------------
        // INativeAppManagement
        // ------------------------------------

        public void RequestInAppReview(Action<bool> callback)
        {
            try
            {
                noctuaRequestInAppReview();
                // Fire-and-forget — iOS doesn't report whether the dialog was shown
                callback?.Invoke(true);
            }
            catch (Exception e)
            {
                _log.Warning($"[Noctua] Failed to request in-app review: {e.Message}");
                callback?.Invoke(false);
            }
        }

        // In-App Updates not supported on iOS
        public void CheckForUpdate(Action<string> callback) => callback?.Invoke("{}");
        public void StartImmediateUpdate(Action<int> callback) => callback?.Invoke(3); // NotAvailable
        public void StartFlexibleUpdate(Action<float> onProgress, Action<int> onResult) => onResult?.Invoke(3);
        public void CompleteUpdate() { }
    }
#endif
}

/// <summary>
/// Helper for deserializing JSON arrays using Unity's <see cref="JsonUtility"/>,
/// which does not natively support top-level arrays.
/// </summary>
public static class JsonUtilityHelper
{
    [Serializable]
    private class Wrapper<T>
    {
        public List<T> items;
    }

    /// <summary>
    /// Deserializes a JSON array string into a <see cref="List{T}"/> by wrapping it in an object.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="json">A JSON array string (e.g., <c>["a","b"]</c>).</param>
    /// <returns>The deserialized list, or an empty list if parsing fails.</returns>
    public static List<T> FromJsonArray<T>(string json)
    {
        var wrapped = $"{{\"items\":{json}}}";
        return JsonUtility.FromJson<Wrapper<T>>(wrapped)?.items ?? new List<T>();
    }
}
