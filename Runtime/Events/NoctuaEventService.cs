using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using System.Collections;
using UnityEngine;

namespace com.noctuagames.sdk.Events
{
    public class NoctuaEventService : MonoBehaviour
    {
        private readonly INativeTracker _nativeTracker;
        private readonly EventSender _eventSender;
        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaEventService));

        private string _country;

        private string _ipAddress;

        public void SetProperties(string country = "", string ipAddress = "")
        {
            if (country != "")
            {
                _country = country;
            }

            if (ipAddress != "")
            {
                _ipAddress = ipAddress;
            }
        }

        internal NoctuaEventService(INativeTracker nativeTracker, EventSender eventSender = null)
        {
            _nativeTracker = nativeTracker;
            _eventSender = eventSender;
        }

        public void TrackAdRevenue(
            string source,
            double revenue,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            AppendProperties(extraPayload);

            _nativeTracker?.TrackAdRevenue(source, revenue, currency, extraPayload);
        }

        public void TrackPurchase(
            string orderId,
            double amount,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            AppendProperties(extraPayload);

            _nativeTracker?.TrackPurchase(orderId, amount, currency, extraPayload);
        }

        public void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
        {
            AppendProperties(extraPayload);


            string properties = "";
            foreach (var (key, value) in extraPayload)
            {
                properties += $"{key}={value}, ";
            }
            _log.Debug($"Event name: {name}, Event properties: {properties}");

            _nativeTracker?.TrackCustomEvent(name, extraPayload);
            _eventSender?.Send(name, extraPayload);
        }

        private void AppendProperties(Dictionary<string, IConvertible> extraPayload)
        {
            if (string.IsNullOrEmpty(_country) && string.IsNullOrEmpty(_ipAddress))
            {
                return;
            }

            extraPayload ??= new Dictionary<string, IConvertible>();

            if (!string.IsNullOrEmpty(_country))
            {
                _log.Debug("Add country to event's extra payload: " + _country);
                extraPayload.Add("country", _country);
            }

            if (!string.IsNullOrEmpty(_ipAddress))
            {
                _log.Debug("Add ip_address to event's extra payload: " + _ipAddress);
                extraPayload.Add("ip_address", _ipAddress);
            }
        }
    }
}