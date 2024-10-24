using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk.Events
{
    public class NoctuaEventService
    {
        private readonly INativeTracker _nativeTracker;
        private readonly EventSender _eventSender;
        
        internal NoctuaEventService(INativeTracker nativeTracker, EventSender eventSender = null)
        {
            _nativeTracker = nativeTracker;
            _eventSender = eventSender;
        }

        public void TrackAdRevenue(string source, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
        {
            _nativeTracker?.TrackAdRevenue(source, revenue, currency, extraPayload);
        }

        public void TrackPurchase(string orderId, double amount, string currency,
            Dictionary<string, IConvertible> extraPayload = null)
        {
            _nativeTracker?.TrackPurchase(orderId, amount, currency, extraPayload);
        }

        public void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
        {
            _nativeTracker?.TrackCustomEvent(name, extraPayload);
            _eventSender?.Send(name, extraPayload);
        }
    }
}