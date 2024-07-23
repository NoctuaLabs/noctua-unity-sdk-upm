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
            if (Application.platform == RuntimePlatform.Android)
            {
                Plugin.Init();
            }
            else
            {
                Debug.LogError("Noctua is not supported on this platform");
                
                throw new PlatformNotSupportedException("Noctua is not supported on this platform");
            }
        }

        public static void OnApplicationPause(bool pause)
        {
            Plugin.OnApplicationPause(pause);
        }

        public static void TrackAdRevenue(
            string source,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin.TrackAdRevenue(source, revenue, currency, extraPayload);
        }

        public static void TrackPurchase(
            string orderId,
            double amount,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin.TrackPurchase(orderId, amount, currency, extraPayload);
        }

        public static void TrackCustomEvent(
            string name,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            Plugin.TrackCustomEvent(name, extraPayload);
        }
        
        private static INoctuaNativePlugin GetPlugin()
        {
            if (Application.platform == RuntimePlatform.Android)
            {
                return new NoctuaAndroidPlugin();
            }

            throw new PlatformNotSupportedException("Noctua is not supported on this platform");
        }
    }
}