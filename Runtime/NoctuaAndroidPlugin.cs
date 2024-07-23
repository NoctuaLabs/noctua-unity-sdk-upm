using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace com.noctuagames.sdk
{
    public class NoctuaAndroidPlugin : INoctuaNativePlugin
    {
        public void Init()
        {
            using AndroidJavaClass unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            var unityActivity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
            var unityContext = unityActivity.Call<AndroidJavaObject>("getApplicationContext");

            using AndroidJavaObject noctua = new AndroidJavaObject("com.noctuagames.sdk.Noctua$Companion");
            noctua.Call("init", unityContext);
            noctua.Call("onResume");

            Debug.Log("[Noctua] Starting");
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
                    sbyte sbyteValue => new AndroidJavaObject(AndroidJNIHelper.Box(sbyteValue)),
                    short shortValue => new AndroidJavaObject(AndroidJNIHelper.Box(shortValue)),
                    int intValue => new AndroidJavaObject(AndroidJNIHelper.Box(intValue)),
                    long longValue => new AndroidJavaObject(AndroidJNIHelper.Box(longValue)),
                    float floatValue => new AndroidJavaObject(AndroidJNIHelper.Box(floatValue)),
                    double doubleValue => new AndroidJavaObject(AndroidJNIHelper.Box(doubleValue)),
                    char charValue => new AndroidJavaObject(AndroidJNIHelper.Box(charValue)),
                    bool boolValue => new AndroidJavaObject(AndroidJNIHelper.Box(boolValue)),
                    string stringValue => new AndroidJavaObject("java.lang.String", stringValue),
                    DateTime time => new AndroidJavaObject("java.lang.String", time.ToString("o")),
                    _ => new AndroidJavaObject("java.lang.String", pair.Value.ToString(CultureInfo.InvariantCulture))
                };
                
                hashMap.Call<AndroidJavaObject>("put", pair.Key, boxValue);
            }

            return hashMap;
        }
    }
}