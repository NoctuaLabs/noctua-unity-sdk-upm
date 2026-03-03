using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Abstraction for tracking ad revenue events.
    /// Used by MediationManager (Presenter) so it doesn't depend
    /// on the static Noctua.Event singleton.
    /// </summary>
    public interface IAdRevenueTracker
    {
        /// <summary>
        /// Tracks an ad revenue event from the specified ad network source.
        /// </summary>
        /// <param name="source">The ad network source name (e.g. "admob", "applovin").</param>
        /// <param name="revenue">The revenue amount.</param>
        /// <param name="currency">The ISO 4217 currency code (e.g. "USD").</param>
        /// <param name="extraPayload">Optional additional key-value pairs for the event.</param>
        void TrackAdRevenue(string source, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null);
    }
}
