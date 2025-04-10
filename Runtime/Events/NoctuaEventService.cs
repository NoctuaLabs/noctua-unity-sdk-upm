using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using System.Collections;
using UnityEngine;

namespace com.noctuagames.sdk.Events
{
    public class NoctuaEventService
    {
        private readonly INativeTracker _nativeTracker;
        private readonly EventSender _eventSender;
        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaEventService));

        private string _country;

        private string _ipAddress;
        
        private bool _isSandbox;

        public void SetProperties(string country = "", string ipAddress = "", bool isSandbox = false)
        {
            if (country != "")
            {
                _country = country;
            }

            if (ipAddress != "")
            {
                _ipAddress = ipAddress;
            }
            
            if (isSandbox)
            {
                _isSandbox = true;
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
            extraPayload ??= new Dictionary<string, IConvertible>();
            AppendProperties(extraPayload);

            _nativeTracker?.TrackAdRevenue(source, revenue, currency, extraPayload);

            extraPayload.Add("source", source);
            extraPayload.Add("revenue", revenue);
            extraPayload.Add("currency", currency);

            _eventSender?.Send("ad_revenue", extraPayload);
        }

        public void TrackPurchase(
            string orderId,
            double amount,
            string currency,
            Dictionary<string, IConvertible> extraPayload = null
        )
        {
            extraPayload ??= new Dictionary<string, IConvertible>();
            AppendProperties(extraPayload);

            _nativeTracker?.TrackPurchase(orderId, amount, currency, extraPayload);
        }

        public void TrackCustomEvent(string name, Dictionary<string, IConvertible> extraPayload = null)
        {
            extraPayload ??= new Dictionary<string, IConvertible>();
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
            
            if (_isSandbox)
            {
                _log.Debug("Add sandbox to event's extra payload: " + _isSandbox);
                extraPayload.Add("is_sandbox", _isSandbox);
            }
        }
    }
}