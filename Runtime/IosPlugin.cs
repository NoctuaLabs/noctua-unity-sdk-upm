using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEngine;

namespace com.noctuagames.sdk
{
#if UNITY_IOS
    internal class IosPlugin : INativePlugin {
        [DllImport("__Internal")]
        private static extern void noctuaInitialize();

        [DllImport("__Internal")]
        private static extern void noctuaTrackAdRevenue(string source, double revenue, string currency, string extraPayloadJson);

        [DllImport("__Internal")]
        private static extern void noctuaTrackPurchase(string orderId, double amount, string currency, string extraPayloadJson);

        [DllImport("__Internal")]
        private static extern void noctuaTrackCustomEvent(string eventName, string payloadJson);

        [DllImport("__Internal")]
        private static extern void noctuaPurchaseItem(string productId, CompletionDelegate callback);

        [DllImport("__Internal")]
        private static extern void noctuaGetActiveCurrency(string productId, CompletionDelegate callback);

        [DllImport ("__Internal")]
        private static extern void _TAG_ShowDatePicker(int mode, double unix, int pickerId);

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
                Debug.LogError("Product ID is null or empty");
                completion?.Invoke(false, "Product ID is null or empty");
                return;
            }

            storedCompletion = completion;
            noctuaPurchaseItem(productId, new CompletionDelegate(CompletionCallback));
            Debug.Log("noctuaPurchaseItem called");
        }

        public void GetActiveCurrency(string productId, Action<bool, string> completion)
        {
            if (string.IsNullOrEmpty(productId))
            {
                Debug.LogError("Product ID is null or empty");
                completion?.Invoke(false, "Product ID is null or empty");
                return;
            }

            storedCompletion = completion;
            noctuaGetActiveCurrency(productId, new CompletionDelegate(CompletionCallback));
            Debug.Log("noctuaGetActiveCurrency called");
        }

        public void ShowDatePicker(int year, int month, int day, int id)
        {
            DateTime dateTime = new DateTime(year, month, day);
            double unix = (TimeZoneInfo.ConvertTimeToUtc(dateTime) - new DateTime(1970, 1, 1, 0, 0, 0, 0, System.DateTimeKind.Utc)).TotalSeconds; 
            _TAG_ShowDatePicker(2, unix, id);
        }

        public NativeAccount GetAccount(long userId, long gameId)
        {
            var rawAccounts = PlayerPrefs.GetString("NoctuaAccountContainer");
            var accounts = JsonConvert.DeserializeObject<List<NativeAccount>>(rawAccounts);
            
            return accounts.Find(a => a.PlayerId == userId && a.GameId == gameId);
        }

        public List<NativeAccount> GetAccounts()
        {
            var rawAccounts = PlayerPrefs.GetString("NoctuaAccountContainer");

            try
            {
                return JsonConvert.DeserializeObject<List<NativeAccount>>(rawAccounts) ?? new List<NativeAccount>();
            }
            catch (Exception e)
            {
                return new List<NativeAccount>();
            }
        }

        public void PutAccount(NativeAccount account)
        {
            var accounts = GetAccounts();
            
            accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);
            account.LastUpdated = DateTime.UtcNow;
            accounts.Add(account);
            
            PlayerPrefs.SetString("NoctuaAccountContainer", JsonConvert.SerializeObject(accounts));
        }

        public int DeleteAccount(NativeAccount account)
        {
            var accounts = GetAccounts();
            
            accounts.RemoveAll(a => a.PlayerId == account.PlayerId && a.GameId == account.GameId);

            PlayerPrefs.SetString("NoctuaAccountContainer", JsonConvert.SerializeObject(accounts));
            return 1;
        }
    }
#endif
}