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
        /// <param name="isSandbox">Whether the current environment is a sandbox/test environment; null keeps the current value.</param>
        public void SetProperties(string country = "", string ipAddress = "", bool? isSandbox = null)
        {
            if (country != "")
            {
                _country = country;
            }

            if (ipAddress != "")
            {
                _ipAddress = ipAddress;
            }

            // Nullable so the flag can also be cleared: the old `if (isSandbox)` guard
            // made a true value sticky for the rest of the session.
            if (isSandbox.HasValue)
            {
                _isSandbox = isSandbox.Value;
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

            // Native tracker is best-effort: if it throws (SDK not initialized, ad network error,
            // etc.) we must still send the ad_revenue event to EventSender so revenue is not lost.
            try
            {
                _nativeTracker?.TrackAdRevenue(source, revenue, currency, payload);
            }
            catch (Exception e)
            {
                _log.Warning($"[Event] Native tracker threw during TrackAdRevenue: {e.Message}");
            }

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

            // Native tracker is best-effort: a throw must not prevent EventSender from
            // receiving the event (e.g. Adjust SDK not initialized, network-level error).
            try
            {
                _nativeTracker?.TrackCustomEvent(name, payload);
            }
            catch (Exception e)
            {
                _log.Warning($"[Event] Native tracker threw during TrackCustomEvent('{name}'): {e.Message}");
            }

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

            // Native tracker is best-effort: a throw must not prevent EventSender from
            // receiving the event so revenue reporting is not silently dropped.
            try
            {
                _nativeTracker?.TrackCustomEventWithRevenue(name, revenue, currency, payload);
            }
            catch (Exception e)
            {
                _log.Warning($"[Event] Native tracker threw during TrackCustomEventWithRevenue('{name}'): {e.Message}");
            }

            payload["revenue"] = revenue;
            payload["currency"] = currency;

            _eventSender?.Send(name, payload);
        }

        /// <summary>
        /// Reports an error to the Noctua error pipeline as a <c>client_error</c> event.
        /// Use this to explicitly surface handled failures from game logic or SDK integration
        /// code in the Noctua dashboard. Auto-caught unhandled exceptions are forwarded
        /// automatically by the SDK — only call this for errors you want to report intentionally.
        /// </summary>
        /// <param name="message">
        /// Human-readable description of the error. Keep under 500 characters.
        /// Example: <c>"Player save failed: disk full"</c>
        /// </param>
        /// <param name="errorType">
        /// Short identifier for the error category used for grouping in the dashboard.
        /// Use PascalCase with no spaces. Example: <c>"SaveError"</c>, <c>"NetworkTimeout"</c>.
        /// Pass <c>null</c> to omit the field.
        /// </param>
        /// <param name="severity">
        /// How severe the error is. Defaults to <see cref="ClientErrorSeverity.Error"/>.
        /// </param>
        /// <param name="source">
        /// Which part of the stack produced the error. Defaults to <see cref="ClientErrorSource.Game"/>.
        /// Use <see cref="ClientErrorSource.NoctuaSdk"/> when reporting errors from SDK wrapper or
        /// integration code rather than pure game logic.
        /// </param>
        /// <example>
        /// <code>
        /// // Simple game-side error
        /// Noctua.Event.ReportError("Player save failed", "SaveError");
        ///
        /// // SDK integration error with explicit severity
        /// Noctua.Event.ReportError("Ad network init failed", "AdInitError",
        ///     ClientErrorSeverity.Warning, ClientErrorSource.NoctuaSdk);
        /// </code>
        /// </example>
        public void ReportError(
            string message,
            string errorType             = null,
            ClientErrorSeverity severity = ClientErrorSeverity.Error,
            ClientErrorSource source     = ClientErrorSource.Game)
        {
            var payload = new Dictionary<string, IConvertible>
            {
                { "source",      source.ToString().ToLower() },
                { "severity",    severity.ToString().ToLower() },
                { "message",     message ?? "" },
                { "app_version", Application.version },
                { "platform",    Application.platform.ToString() },
                { "timestamp_utc", DateTime.UtcNow.ToString("o") }
            };

            if (!string.IsNullOrEmpty(errorType))
                payload["error_type"] = errorType;

            AppendProperties(payload);

            _log.Debug($"[Event] ReportError source={source} severity={severity} type={errorType ?? "null"} msg={message}");

            _eventSender?.Send("client_error", payload);
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