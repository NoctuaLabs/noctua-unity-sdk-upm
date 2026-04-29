namespace com.noctuagames.sdk
{
    /// <summary>
    /// Adapts <see cref="INativeDeviceMetrics"/> (Platform layer) to
    /// <see cref="IDeviceMetricsProvider"/> (Presenter layer) so that
    /// <see cref="MemoryMonitor"/> can consume native data without taking
    /// a dependency on the Platform layer. Constructed once in the
    /// composition root and handed to <see cref="MemoryMonitor.SetNativeMetricsProvider"/>.
    /// </summary>
    public sealed class NoctuaDeviceMetricsAdapter : IDeviceMetricsProvider
    {
        private readonly INativeDeviceMetrics _native;

        public NoctuaDeviceMetricsAdapter(INativeDeviceMetrics native)
        {
            _native = native;
        }

        public DeviceMetricsSnapshot Snapshot()
        {
            if (_native == null) return DeviceMetricsSnapshot.Empty(System.DateTime.UtcNow);
            return _native.SnapshotDeviceMetrics();
        }
    }
}
