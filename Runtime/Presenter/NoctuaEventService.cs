using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    public class NoctuaEventService : IAdRevenueTracker
    {
        private readonly INativeTracker _nativeTracker;
        private readonly IEventSender _eventSender;
        private readonly ILogger _log = new NoctuaLogger(typeof(NoctuaEventService));

        private string _country;
        private string _ipAddress;
        private bool _isSandbox;
        private readonly Stopwatch _featureStopwatch = new Stopwatch();
        private string _currentFeatureVisitId;

        /// <summary>
        /// Sets contextual properties (country, IP, sandbox flag) that are automatically appended to all tracked events.
        /// </summary>
        /// <param name="country">ISO country code (e.g., "US").</param>
        /// <param name="ipAddress">Client IP address.</param>
        /// <param name="isSandbox">Whether the current environment is a sandbox/test environment.</param>
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
        public NoctuaEventService(INativeTracker nativeTracker, IEventSender eventSender = null)
        {
            _nativeTracker = nativeTracker;
            _eventSender = eventSender;
        }

        /// <summary>
        /// Sets the current feature/screen the player is on. Automatically sends a
        /// <c>feature_engagement</c> event for the previous feature (if any) with the
        /// elapsed time and a unique visit ID.
        /// </summary>
        /// <param name="featureName">Feature or screen name. Pass empty string to clear.</param>
        public void SetCurrentFeature(string featureName)
        {
            var previousTag = ExperimentManager.GetCurrentFeature();

            if (!string.IsNullOrEmpty(previousTag) && _featureStopwatch.IsRunning)
            {
                var elapsedMs = _featureStopwatch.ElapsedMilliseconds;
                _log.Info($"[Feature] Leaving '{previousTag}' after {elapsedMs}ms (visit_id={_currentFeatureVisitId}), sending feature_engagement");

                _eventSender?.Send("feature_engagement", new Dictionary<string, IConvertible>
                {
                    { "feature_tag", previousTag },
                    { "feature_time_msec", elapsedMs },
                    { "feature_visit_id", _currentFeatureVisitId }
                });
            }

            ExperimentManager.SetCurrentFeature(featureName);

            if (!string.IsNullOrEmpty(featureName))
            {
                _currentFeatureVisitId = Guid.NewGuid().ToString();
                _featureStopwatch.Restart();
                _log.Info($"[Feature] Entering '{featureName}' (visit_id={_currentFeatureVisitId})");
            }
            else
            {
                _currentFeatureVisitId = null;
                _featureStopwatch.Reset();
                _log.Info("[Feature] Cleared current feature");
            }
        }

        /// <summary>
        /// Gets the currently active feature/screen name.
        /// </summary>
        /// <returns>Active feature name or empty string.</returns>
        public string GetCurrentFeature()
        {
            return ExperimentManager.GetCurrentFeature();
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
            // Copy to avoid mutating the caller's dictionary — the same dict reference is
            // sometimes passed to multiple tracking calls, and AppendProperties/Send enrich it.
            var payload = extraPayload != null
                ? new Dictionary<string, IConvertible>(extraPayload)
                : new Dictionary<string, IConvertible>();
            AppendProperties(payload);

            _nativeTracker?.TrackAdRevenue(source, revenue, currency, payload);

            payload["source"] = source;
            payload["revenue"] = revenue;
            payload["currency"] = currency;

            _eventSender?.Send("ad_revenue", payload);
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
            // Copy to avoid mutating the caller's dictionary.
            var payload = extraPayload != null
                ? new Dictionary<string, IConvertible>(extraPayload)
                : new Dictionary<string, IConvertible>();
            AppendProperties(payload);

            _nativeTracker?.TrackPurchase(orderId, amount, currency, payload);
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
            // Copy to avoid mutating the caller's dictionary — the same dict reference is
            // sometimes passed to multiple sequential TrackCustomEvent calls (e.g. from ad
            // event handlers), and AppendProperties enriches the dict in-place.
            var payload = extraPayload != null
                ? new Dictionary<string, IConvertible>(extraPayload)
                : new Dictionary<string, IConvertible>();
            AppendProperties(payload);

            string properties = "";
            foreach (var (key, value) in payload)
            {
                properties += $"{key}={value}, ";
            }
            _log.Debug($"Event name: {name}, Event properties: {properties}");

            _nativeTracker?.TrackCustomEvent(name, payload);
            _eventSender?.Send(name, payload);
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
            // Copy to avoid mutating the caller's dictionary.
            var payload = extraPayload != null
                ? new Dictionary<string, IConvertible>(extraPayload)
                : new Dictionary<string, IConvertible>();
            AppendProperties(payload);

            string properties = "";
            foreach (var (key, value) in payload)
            {
                properties += $"{key}={value}, ";
            }
            _log.Debug($"Event name: {name}, Event properties: {properties}");

            _nativeTracker?.TrackCustomEventWithRevenue(name, revenue, currency, payload);

            payload["revenue"] = revenue;
            payload["currency"] = currency;

            _eventSender?.Send(name, payload);
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
            // Copy to avoid mutating the caller's dictionary.
            var payload = extraPayload != null
                ? new Dictionary<string, IConvertible>(extraPayload)
                : new Dictionary<string, IConvertible>();
            AppendProperties(payload);

            string properties = "";
            foreach (var (key, value) in payload)
            {
                properties += $"{key}={value}, ";
            }

            _log.Debug($"Event name: {eventName}, Event properties: {properties}");

            _eventSender?.Send(eventName, payload);
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
                extraPayload["country"] = _country;
            }

            if (!string.IsNullOrEmpty(_ipAddress))
            {
                _log.Debug("Add ip_address to event's extra payload: " + _ipAddress);
                extraPayload["ip_address"] = _ipAddress;
            }

            if (_isSandbox)
            {
                _log.Debug("Add sandbox to event's extra payload: " + _isSandbox);
                extraPayload["is_sandbox"] = _isSandbox;
            }
        }
    }
}