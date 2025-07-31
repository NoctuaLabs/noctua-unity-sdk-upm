using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine;

namespace com.noctuagames.sdk
{
#if UNITY_IOS
    internal class IosPlugin : INativePlugin
    {
        private readonly ILogger _log = new NoctuaLogger(typeof(IosPlugin));

        [DllImport("__Internal")]
        private static extern void noctuaInitialize();

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
        private static extern void noctuaGetActiveCurrency(string productId, CompletionDelegate callback);

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

        public void Init(List<string> activeBundleIds)
        {
            noctuaInitialize();
        }

        public void OnApplicationPause(bool pause)
        {
        }

        public void TrackAdRevenue(string source, double revenue, string currency, Dictionary<string, IConvertible> extraPayload)
        {
            noctuaTrackAdRevenue(source, revenue, currency, JsonConvert.SerializeObject(extraPayload));
        }

        public void TrackPurchase(string orderId, double amount, string currency, Dictionary<string, IConvertible> extraPayload)
        {
            noctuaTrackPurchase(orderId, amount, currency, JsonConvert.SerializeObject(extraPayload));
        }

        public void TrackCustomEvent(string eventName, Dictionary<string, IConvertible> payload)
        {
            noctuaTrackCustomEvent(eventName, JsonConvert.SerializeObject(payload));

            _log.Info($"forwarded event '{eventName}' to native tracker");
        }

        public void TrackCustomEventWithRevenue(string eventName, double revenue, string currency, Dictionary<string, IConvertible> payload)
        {
            noctuaTrackCustomEventWithRevenue(eventName, revenue, currency, JsonConvert.SerializeObject(payload));
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void CompletionDelegate(bool success, IntPtr messagePtr);

        [AOT.MonoPInvokeCallback(typeof(CompletionDelegate))]
        private static void CompletionCallback(bool success, IntPtr messagePtr)
        {
            string message = messagePtr != IntPtr.Zero ? Marshal.PtrToStringAnsi(messagePtr) : "Unknown error";
            storedCompletion?.Invoke(success, message);
        }

        private static Action<bool, string> storedCompletion;

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

        public void ShowDatePicker(int year, int month, int day, int id)
        {
            DateTime dateTime = new DateTime(year, month, day);
            double unix = (TimeZoneInfo.ConvertTimeToUtc(dateTime) - new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds;
            _TAG_ShowDatePicker(2, unix, id);
        }

        // Closes the date picker by dismissing it from the user interface.
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

        public void PutAccount(NativeAccount account)
        {
            noctuaPutAccount(account.GameId, account.PlayerId, account.RawData);
        }

        public int DeleteAccount(NativeAccount account)
        {
            noctuaDeleteAccount(account.GameId, account.PlayerId);
            
            return 1;
        }

        public void OnOnline()
        {
            noctuaOnOnline();
            _log.Info($"trigger online mode to native plugin");
        }

        public void OnOffline()
        {
            noctuaOnOffline();
            _log.Info($"trigger offline mode to native plugin");
        }
    }
#endif
}