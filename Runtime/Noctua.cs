using System;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    public class Noctua
    {
        private static readonly INoctuaNativePlugin Plugin = GetPlugin();
        
        public static void Init()
        {
            Plugin?.Init();
        }

        public static void OnApplicationPause(bool pause)
        {
            Plugin?.OnApplicationPause(pause);
        }

        public static void TrackAdRevenue(
            string source,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin?.TrackAdRevenue(source, revenue, currency, extraPayload);
        }

        public static void TrackPurchase(
            string orderId,
            double amount,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin?.TrackPurchase(orderId, amount, currency, extraPayload);
        }

        public static void TrackCustomEvent(
            string name,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin?.TrackCustomEvent(name, extraPayload);
        }
        
        private static INoctuaNativePlugin GetPlugin()
        {
            #if UNITY_ANDROID
                Debug.Log("Plugin is NoctuaAndroidPlugin");
                return new NoctuaAndroidPlugin();
            #elif UNITY_IOS
                Debug.Log("Plugin is NoctuaIPhonePlugin");
                return new NoctuaIPhonePlugin();
            #else
                Debug.Log("Plugin is null");
                return null;
            #endif
        }
    }
}