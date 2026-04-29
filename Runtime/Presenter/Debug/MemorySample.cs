using System;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// One memory snapshot — combines Unity-side Profiler reads with native
    /// device metrics in a single record so the Inspector renders a coherent
    /// view (Mono heap + Unity native + native footprint + thermal).
    ///
    /// Bytes everywhere — no KB/MB conversion at the data layer; UI formats
    /// for display.
    /// </summary>
    public readonly struct MemorySample
    {
        public DateTime TimestampUtc           { get; }
        public long     MonoUsedBytes          { get; } // Profiler.GetMonoUsedSizeLong
        public long     MonoHeapBytes          { get; } // Profiler.GetMonoHeapSizeLong
        public long     UnityAllocatedBytes    { get; } // Profiler.GetTotalAllocatedMemoryLong
        public long     UnityReservedBytes     { get; } // Profiler.GetTotalReservedMemoryLong
        public long     GcTotalBytes           { get; } // GC.GetTotalMemory(false)
        public long     AssetCacheBytes        { get; } // Caching.spaceOccupied
        public DeviceMetricsSnapshot Native    { get; } // bridge poll

        public MemorySample(
            DateTime timestampUtc,
            long monoUsedBytes,
            long monoHeapBytes,
            long unityAllocatedBytes,
            long unityReservedBytes,
            long gcTotalBytes,
            long assetCacheBytes,
            DeviceMetricsSnapshot native)
        {
            TimestampUtc        = timestampUtc;
            MonoUsedBytes       = monoUsedBytes;
            MonoHeapBytes       = monoHeapBytes;
            UnityAllocatedBytes = unityAllocatedBytes;
            UnityReservedBytes  = unityReservedBytes;
            GcTotalBytes        = gcTotalBytes;
            AssetCacheBytes     = assetCacheBytes;
            Native              = native;
        }
    }
}
