using System;
using System.Collections.Generic;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Phase of a tracker event through its dispatch lifecycle. Integer values
    /// are stable across the C ABI (iOS P/Invoke) and JNI (Android proxy);
    /// must match <c>NoctuaTrackerEventPhase</c> in the native SDKs.
    /// </summary>
    public enum TrackerEventPhase
    {
        Queued       = 0,
        Sending      = 1,
        Emitted      = 2,
        Uploading    = 3,
        Acknowledged = 4,
        Failed       = 5,
        TimedOut     = 6,
    }

    /// <summary>
    /// Convenience helper used by Inspector UI to render phase badges.
    /// </summary>
    public static class TrackerEventPhaseEx
    {
        public static TrackerEventPhase FromRaw(int raw) =>
            raw >= 0 && raw <= 6 ? (TrackerEventPhase)raw : TrackerEventPhase.Queued;

        public static bool IsTerminal(this TrackerEventPhase p) =>
            p == TrackerEventPhase.Acknowledged ||
            p == TrackerEventPhase.Failed       ||
            p == TrackerEventPhase.TimedOut;
    }

    /// <summary>
    /// Single tracker event entry shown on the Inspector "Trackers" tab.
    /// Each row accumulates a <see cref="History"/> of phase transitions
    /// (Queued → Sending → Emitted → Uploading → Acknowledged) with
    /// timestamps, so the expanded row can render a lifecycle timeline.
    /// </summary>
    public class TrackerEmission
    {
        public Guid Id { get; set; }
        public string Provider { get; set; }
        public string EventName { get; set; }
        public DateTime CreatedUtc { get; set; }
        public TrackerEventPhase Phase { get; set; }
        public string Error { get; set; }

        public IReadOnlyDictionary<string, object> Payload { get; set; }
        public IReadOnlyDictionary<string, object> ExtraParams { get; set; }

        public List<TrackerPhaseTransition> History { get; set; } = new();
    }

    public struct TrackerPhaseTransition
    {
        public TrackerEventPhase Phase;
        public DateTime AtUtc;
    }
}
