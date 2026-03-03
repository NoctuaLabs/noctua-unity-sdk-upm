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
        void TrackAdRevenue(string source, double revenue, string currency,
            Dictionary<string, IConvertible> extraPayload = null);
    }
}
