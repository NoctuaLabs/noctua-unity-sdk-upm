using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#if UNITY_ANDROID && !UNITY_EDITOR
namespace com.noctuagames.sdk
{
    internal class AndroidPlugin : INativePlugin
    {
        public void Init()
        {
            using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var unityContext = unityActivity.Call<AndroidJavaObject>("getApplicationContext");

            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
            noctua.Call("init", unityContext);
            noctua.Call("onResume");
        }

        public void OnApplicationPause(bool pause)
        {
            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
            noctua.Call(pause ? "onPause" : "onResume");
        }

        public void TrackAdRevenue(
            string source,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            using AndroidJavaObject javaPayload = ConvertToJavaHashMap(extraPayload);
            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");

            noctua.Call("trackAdRevenue", source, revenue, currency, javaPayload);
        }

        public void TrackPurchase(
            string orderId,
            double amount,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            using AndroidJavaObject javaPayload = ConvertToJavaHashMap(extraPayload);
            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");

            noctua.Call("trackPurchase", orderId, amount, currency, javaPayload);
        }

        public void TrackCustomEvent(
            string name,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            using AndroidJavaObject javaPayload = ConvertToJavaHashMap(extraPayload);
            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");

            noctua.Call("trackCustomEvent", name, javaPayload);
        }
        
        private static AndroidJavaObject ConvertToJavaHashMap(Dictionary<string, IConvertible> dictionary)
        {
            var hashMap = new AndroidJavaObject("java.util.HashMap");

            if (dictionary == null)
            {
                return hashMap;
            }
            
            foreach (var pair in dictionary)
            {
                var boxValue = pair.Value switch
                {
                    sbyte sbyteValue => new AndroidJavaObject("java.lang.Byte", sbyteValue),
                    short shortValue => new AndroidJavaObject("java.lang.Short", shortValue),
                    int intValue => new AndroidJavaObject("java.lang.Integer", intValue),
                    long longValue => new AndroidJavaObject("java.lang.Long", longValue),
                    float floatValue => new AndroidJavaObject("java.lang.Float", floatValue),
                    double doubleValue => new AndroidJavaObject("java.lang.Double", doubleValue),
                    char charValue => new AndroidJavaObject("java.lang.Character", charValue),
                    bool boolValue => new AndroidJavaObject("java.lang.Boolean", boolValue),
                    string stringValue => new AndroidJavaObject("java.lang.String", stringValue),
                    DateTime time => new AndroidJavaObject("java.lang.String", time.ToString("o")),
                    _ => new AndroidJavaObject("java.lang.String", pair.Value.ToString(CultureInfo.InvariantCulture))  
                };
                
                hashMap.Call<AndroidJavaObject>("put", pair.Key, boxValue);
            }

            return hashMap;
        }

        public void PurchaseItem(string productId, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }

        public void GetActiveCurrency(string productId, Action<bool, string> callback)
        {
            throw new NotImplementedException();
        }

    }
}
#endif
