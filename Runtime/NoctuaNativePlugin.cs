using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    public interface INoctuaNativePlugin
    {
        void Init();
        void OnApplicationPause(bool pause);
        void TrackAdRevenue(string source, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null);
        void TrackPurchase(string orderId, double amount, string currency, Dictionary<string, IConvertible> extraPayload = null);
        void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null);
    }
}