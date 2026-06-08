using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Picks Noctua Inspector ring-buffer capacities based on the device's total physical RAM.
    /// High-RAM QA devices keep much more history (logs / tracker events / HTTP exchanges) while
    /// low-RAM devices stay at the conservative defaults — so raising the limits never risks OOM
    /// on the weakest hardware. These buffers are sandbox-only debug state; production never
    /// allocates them.
    ///
    /// Rough memory cost at the top tier: ~40k log entries (~12&#8211;20 MB), ~1.5k tracker
    /// emissions (~2&#8211;3 MB), ~500 HTTP exchanges (~2&#8211;5 MB) — negligible on a 6 GB+ device.
    /// </summary>
    public static class InspectorBufferLimits
    {
        /// <summary>Chosen capacities for the three Inspector buffers.</summary>
        public readonly struct Limits
        {
            public readonly int Logs;
            public readonly int Trackers;
            public readonly int Http;

            public Limits(int logs, int trackers, int http)
            {
                Logs = logs;
                Trackers = trackers;
                Http = http;
            }
        }

        /// <summary>
        /// Tiers the capacities by total physical RAM (in MB). The lowest tier matches the historical
        /// defaults (logs 5000 / trackers 200 / http 100) so behaviour never regresses on low-end
        /// devices; higher tiers scale up.
        /// </summary>
        public static Limits ForDevice(int systemMemoryMb)
        {
            if (systemMemoryMb >= 6144) return new Limits(logs: 40000, trackers: 1500, http: 500); // >= 6 GB
            if (systemMemoryMb >= 4096) return new Limits(logs: 20000, trackers: 1000, http: 300); // >= 4 GB
            if (systemMemoryMb >= 3072) return new Limits(logs: 10000, trackers:  500, http: 200); // >= 3 GB
            return new Limits(logs: 5000, trackers: 200, http: 100);                                // < 3 GB (defaults)
        }

        /// <summary>Convenience wrapper reading <see cref="SystemInfo.systemMemorySize"/> at runtime.</summary>
        public static Limits ForCurrentDevice() => ForDevice(SystemInfo.systemMemorySize);
    }
}
