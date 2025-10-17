using System;
using System.Collections.Generic;
using UnityEngine.Scripting;
using System.Collections;
using UnityEngine;

namespace com.noctuagames.sdk.Events
{
    /// <summary>
    /// Handles event tracking for Noctua SDK, bridging between native tracker implementations
    /// (e.g., Adjust, Firebase, Facebook SDKs) and Noctua’s analytics <see cref="EventSender"/>.
    /// </summary>
    /// <remarks>
    /// This service provides a unified interface for tracking ad revenues, purchases, and custom events.
    /// Events are automatically enriched with contextual data (country, IP address, sandbox flag).
    /// </remarks>
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

        /// <summary>
        /// Internal constructor for creating a <see cref="NoctuaEventService"/> instance.
        /// </summary>
        /// <param name="nativeTracker">The platform-specific native tracker (e.g., Adjust SDK).</param>
        /// <param name="eventSender">The Noctua event sender responsible for server communication.</param>
        internal NoctuaEventService(INativeTracker nativeTracker, EventSender eventSender = null)
        {
            _nativeTracker = nativeTracker;
            _eventSender = eventSender;
        }

        /// <summary>
        /// Tracks an ad revenue event across both native trackers and Noctua analytics.
        /// </summary>
        /// <param name="source">The ad network source (e.g. "admob_sdk", "applovin_max_sdk").</param>
        /// <param name="revenue">Revenue amount earned from the ad.</param>
        /// <param name="currency">ISO 4217 currency code (e.g. "USD").</param>
        /// <param name="extraPayload">Optional additional event properties.</param>
        /// <example>
        /// <code>
        /// Noctua.Event.TrackAdRevenue("applovin_max_sdk", 0.05, "USD");
        /// </code>
        /// </example>
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

        /// <summary>
        /// Tracks an in-app purchase event with associated metadata.
        /// </summary>
        /// <param name="orderId">Unique order identifier for the purchase.</param>
        /// <param name="amount">Monetary value of the purchase.</param>
        /// <param name="currency">ISO 4217 currency code.</param>
        /// <param name="extraPayload">Optional extra parameters to include with the event.</param>
        /// <example>
        /// <code>
        /// Noctua.Event.TrackPurchase("order_123", 4.99, "USD");
        /// </code>
        /// </example>
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

        /// <summary>
        /// Tracks a custom analytics event without revenue data.
        /// </summary>
        /// <param name="name">The event name (e.g., "level_complete", "tutorial_start").</param>
        /// <param name="extraPayload">Optional extra properties to include with the event.</param>
        /// <example>
        /// <code>
        /// Noctua.Event.TrackCustomEvent("level_complete", new Dictionary&lt;string, IConvertible&gt;
        /// {
        ///     { "level", 5 },
        ///     { "duration", 120 }
        /// });
        /// </code>
        /// </example>
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

        /// <summary>
        /// Tracks a custom analytics event with associated revenue and currency data.
        /// </summary>
        /// <param name="name">The event name (e.g., "special_offer_purchase").</param>
        /// <param name="revenue">Monetary value associated with the event.</param>
        /// <param name="currency">Currency code (e.g., "USD").</param>
        /// <param name="extraPayload">Optional extra parameters to include with the event.</param>
        /// <example>
        /// <code>
        /// Noctua.Event.TrackCustomEventWithRevenue("ad_impression", 1.99, "USD");
        /// </code>
        /// </example>
        public void TrackCustomEventWithRevenue(string name, double revenue, string currency, Dictionary<string, IConvertible> extraPayload = null)
        {
            extraPayload ??= new Dictionary<string, IConvertible>();
            AppendProperties(extraPayload);

            string properties = "";
            foreach (var (key, value) in extraPayload)
            {
                properties += $"{key}={value}, ";
            }
            _log.Debug($"Event name: {name}, Event properties: {properties}");

            _nativeTracker?.TrackCustomEventWithRevenue(name, revenue, currency, extraPayload);

            extraPayload.Add("revenue", revenue);
            extraPayload.Add("currency", currency);

            _eventSender?.Send(name, extraPayload);
        }

        /// <summary>
        /// Tracks internal SDK-level events that should not be logged to native trackers.
        /// </summary>
        /// <param name="eventName">Internal event name (e.g., "sdk_init_success").</param>
        /// <param name="extraPayload">Optional extra parameters to include with the event.</param>
        /// <remarks>
        /// Used internally by the Noctua SDK to report internal state changes, errors, or telemetry events.
        /// </remarks>
        public void InternalTrackEvent(string eventName, Dictionary<string, IConvertible> extraPayload = null)
        {
            extraPayload ??= new Dictionary<string, IConvertible>();
            AppendProperties(extraPayload);

            string properties = "";
            foreach (var (key, value) in extraPayload)
            {
                properties += $"{key}={value}, ";
            }

            _log.Debug($"Event name: {eventName}, Event properties: {properties}");

            _eventSender?.Send(eventName, extraPayload);
        }
        
        /// <summary>
        /// Appends contextual properties (country, IP address, sandbox state) to an event payload.
        /// </summary>
        /// <param name="extraPayload">Dictionary of properties to be enriched with context.</param>
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