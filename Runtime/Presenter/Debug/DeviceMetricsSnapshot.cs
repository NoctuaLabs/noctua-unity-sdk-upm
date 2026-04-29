using System;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// OS-level metrics that Unity cannot read directly — sourced from the
    /// native bridge via <c>INativeDeviceMetrics.Snapshot()</c>. All numeric
    /// fields use bytes for memory and a normalized 0..3 thermal scale.
    ///
    /// Pure data; constructed once per poll tick. Fields default to -1 when
    /// the platform does not expose a particular metric (e.g. iOS doesn't
    /// expose system-wide PSS the way Android does).
    /// </summary>
    public readonly struct DeviceMetricsSnapshot
    {
        public DateTime TimestampUtc       { get; }
        public long     PhysFootprintBytes { get; } // iOS phys_footprint, Android Debug.MemoryInfo.totalPss
        public long     AvailableBytes     { get; } // iOS os_proc_available_memory(), Android availMem
        public long     SystemTotalBytes   { get; } // Android only — system total RAM
        public bool     LowMemory          { get; } // Android only — ActivityManager.MemoryInfo.lowMemory
        public ThermalState Thermal        { get; }

        public DeviceMetricsSnapshot(
            DateTime timestampUtc,
            long physFootprintBytes,
            long availableBytes,
            long systemTotalBytes,
            bool lowMemory,
            ThermalState thermal)
        {
            TimestampUtc       = timestampUtc;
            PhysFootprintBytes = physFootprintBytes;
            AvailableBytes     = availableBytes;
            SystemTotalBytes   = systemTotalBytes;
            LowMemory          = lowMemory;
            Thermal            = thermal;
        }

        public static DeviceMetricsSnapshot Empty(DateTime now) =>
            new DeviceMetricsSnapshot(now, -1, -1, -1, false, ThermalState.Unknown);
    }

    /// <summary>
    /// Normalized thermal pressure level. Maps to:
    ///   * iOS <c>ProcessInfo.thermalState</c> (nominal=0, fair=1, serious=2, critical=3)
    ///   * Android <c>PowerManager.getCurrentThermalStatus()</c> (NONE=0, LIGHT=1, MODERATE=2, SEVERE/+=3)
    /// </summary>
    public enum ThermalState
    {
        Unknown   = -1,
        Nominal   = 0,
        Fair      = 1,
        Serious   = 2,
        Critical  = 3,
    }
}
