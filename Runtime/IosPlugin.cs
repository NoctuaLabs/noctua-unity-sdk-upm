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
        private static extern void noctuaPurchaseItem(string productId, PurchaseCompletionDelegate callback);

        public void Init()
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
        private delegate void PurchaseCompletionDelegate(bool success, IntPtr messagePtr);

        [AOT.MonoPInvokeCallback(typeof(PurchaseCompletionDelegate))]
        private static void PurchaseCompletionCallback(bool success, IntPtr messagePtr)
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
            noctuaPurchaseItem(productId, new PurchaseCompletionDelegate(PurchaseCompletionCallback));
            Debug.Log("noctuaPurchaseItem called");
        }

    }
#endif
}