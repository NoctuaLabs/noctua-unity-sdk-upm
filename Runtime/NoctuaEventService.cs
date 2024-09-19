using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    public class NoctuaEventService
    {
        private readonly INativePlugin _nativePlugin;
        
        internal NoctuaEventService(INativePlugin nativePlugin)
        {
            _nativePlugin = nativePlugin;
        }

        public void TrackAdRevenue(string source, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
        {
            _nativePlugin?.TrackAdRevenue(source, revenue, currency, extraPayload);
        }

        public void TrackPurchase(string orderId, double amount, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
        {
            _nativePlugin?.TrackPurchase(orderId, amount, currency, extraPayload);
        }

        public void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
        {
            _nativePlugin?.TrackCustomEvent(name, extraPayload);
        }
    }
}