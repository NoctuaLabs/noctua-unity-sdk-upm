using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Polls Unity Profiler counters + native device metrics on a fixed
    /// 1Hz cadence and exposes a ring buffer of <see cref="MemorySample"/>
    /// for the Inspector "Memory" tab. Also surfaces the destructive
    /// reset/clear actions that the tab's action panel calls into.
    ///
    /// Native metrics are optional — the constructor accepts an
    /// <see cref="IDeviceMetricsProvider"/> that returns
    /// <see cref="DeviceMetricsSnapshot.Empty"/> when no native bridge is
    /// available (e.g. Editor PlayMode without the iOS plugin). This keeps
    /// the Memory tab functional in-Editor without a stub.
    /// </summary>
    public sealed class MemoryMonitor : MonoBehaviour
    {
        public const int DefaultCapacity = 600; // 10 minutes @ 1Hz

        private readonly LinkedList<MemorySample> _samples = new();
        private readonly object _lock = new();
        private float _accumSec;
        private const float SampleIntervalSec = 1f;

        private IDeviceMetricsProvider _nativeMetrics;

        // Optional bridge to the native maintenance API (clear HTTP cache,
        // future: clear cookies, etc). Composition root injects this when
        // sandbox is enabled; null otherwise so the action button no-ops.
        private System.Action _clearNativeHttpCacheAction;

        /// <summary>
        /// Fires after each sample is admitted. Always main-thread.
        /// </summary>
        public event Action<MemorySample> OnSample;

        /// <summary>
        /// Inject the native bridge after construction. Called from the
        /// composition root once <see cref="INativePlugin"/> is wired up.
        /// May be null — the monitor will fall back to empty native metrics.
        /// </summary>
        public void SetNativeMetricsProvider(IDeviceMetricsProvider provider)
        {
            _nativeMetrics = provider;
        }

        /// <summary>
        /// Inject the "clear native HTTP cache" hook from the composition
        /// root (kept as a delegate so MemoryMonitor stays in the Presenter
        /// layer without referencing INativeMaintenance from Platform).
        /// </summary>
        public void SetClearNativeHttpCacheAction(System.Action action)
        {
            _clearNativeHttpCacheAction = action;
        }

        /// <summary>
        /// Wipes platform-managed HTTP caches (URLCache + WKWebView on
        /// iOS, WebView + cacheDir on Android). No-op when no native
        /// bridge is installed (Editor / production).
        /// </summary>
        public void ClearNativeHttpCache()
        {
            try { _clearNativeHttpCacheAction?.Invoke(); }
            catch { /* swallow — action panel surfaces no error UI */ }
        }

        public IReadOnlyList<MemorySample> Snapshot()
        {
            lock (_lock) return new List<MemorySample>(_samples);
        }

        public MemorySample LatestOrDefault()
        {
            lock (_lock) return _samples.Count > 0 ? _samples.Last.Value : default;
        }

        public void Clear()
        {
            lock (_lock) _samples.Clear();
        }

        // --- Destructive actions (called from Inspector UI; gated by confirm dialogs there) ---

        /// <summary>
        /// Force a full GC pass and wait for finalizers. Cheap to call but
        /// pauses the game thread for tens of milliseconds — only invoke
        /// from the Inspector "Force GC" button, never on a timer.
        /// </summary>
        public static void ForceGC()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        /// <summary>
        /// Asks Unity to unload assets no longer referenced by any scene
        /// or live object. Returns the underlying <see cref="AsyncOperation"/>
        /// so the UI can show progress.
        /// </summary>
        public static AsyncOperation UnloadUnusedAssets() =>
            Resources.UnloadUnusedAssets();

        /// <summary>
        /// Wipes asset bundle cache. <see cref="Caching.ClearCache"/> is
        /// safe to call even if no bundles were cached.
        /// </summary>
        public static bool ClearAssetBundleCache() => Caching.ClearCache();

        /// <summary>
        /// Wipes <see cref="PlayerPrefs"/>. Destructive — must be guarded
        /// by a hold-to-confirm interaction in the Inspector tab.
        /// </summary>
        public static void WipePlayerPrefs()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();
        }

        private void Update()
        {
            _accumSec += Time.unscaledDeltaTime;
            if (_accumSec < SampleIntervalSec) return;
            _accumSec = 0f;

            var now = DateTime.UtcNow;
            DeviceMetricsSnapshot native;
            try
            {
                native = _nativeMetrics?.Snapshot() ?? DeviceMetricsSnapshot.Empty(now);
            }
            catch
            {
                native = DeviceMetricsSnapshot.Empty(now);
            }

            var sample = new MemorySample(
                timestampUtc:        now,
                monoUsedBytes:       Profiler.GetMonoUsedSizeLong(),
                monoHeapBytes:       Profiler.GetMonoHeapSizeLong(),
                unityAllocatedBytes: Profiler.GetTotalAllocatedMemoryLong(),
                unityReservedBytes:  Profiler.GetTotalReservedMemoryLong(),
                gcTotalBytes:        GC.GetTotalMemory(false),
                assetCacheBytes:     Caching.cacheCount > 0 ? SafeCacheBytes() : 0,
                native:              native);

            lock (_lock)
            {
                _samples.AddLast(sample);
                while (_samples.Count > DefaultCapacity) _samples.RemoveFirst();
            }
            try { OnSample?.Invoke(sample); } catch { /* swallow */ }
        }

        private static long SafeCacheBytes()
        {
            // Caching.spaceOccupied throws if no cache is initialized in
            // certain Unity versions; guard defensively.
            try { return Caching.defaultCache.spaceOccupied; }
            catch { return 0; }
        }
    }

    /// <summary>
    /// Minimal interface so <see cref="MemoryMonitor"/> can read native
    /// metrics without holding a direct reference to <see cref="INativePlugin"/>
    /// (which lives at the Platform layer; Presenter must not depend on it).
    /// The composition root constructs an adapter that delegates to the
    /// active <c>INativeDeviceMetrics</c> implementation.
    /// </summary>
    public interface IDeviceMetricsProvider
    {
        DeviceMetricsSnapshot Snapshot();
    }
}
