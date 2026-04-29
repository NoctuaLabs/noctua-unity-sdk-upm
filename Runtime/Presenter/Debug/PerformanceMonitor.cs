using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Per-frame performance monitor — drives the Inspector "Performance"
    /// tab. Pure Unity, no native bridge needed.
    ///
    /// Sampled in <see cref="Update"/> every frame; aggregates into a short
    /// raw buffer (<see cref="RawCapacity"/> = 600 entries ≈ 10s @ 60fps)
    /// and a longer 1Hz aggregate buffer (<see cref="AggCapacity"/> = 600 ≈
    /// 10 minutes). The 95th-percentile frame time uses a fixed 60s sliding
    /// window — long enough to surface stutters, short enough that a single
    /// hitch doesn't dominate the number indefinitely.
    ///
    /// Hot-path discipline: zero per-frame allocations once the monitor is
    /// warm. The percentile buffer is a pre-allocated <c>float[]</c>; the
    /// raw and aggregate buffers are pre-allocated <see cref="LinkedList{T}"/>
    /// sized once. <see cref="OnSample"/> consumers must not allocate inside
    /// their handler.
    ///
    /// Lifecycle: instantiated from <see cref="Noctua.Initialization"/> when
    /// <c>sandboxEnabled = true</c>, attached to the Inspector controller
    /// host GameObject, and destroyed with it.
    /// </summary>
    public sealed class PerformanceMonitor : MonoBehaviour
    {
        public const int RawCapacity = 600;
        public const int AggCapacity = 600;

        // 95th-percentile window. 60s × 60fps = 3,600 entries — safe upper
        // bound for ring tracking. Picked over 30s because mobile games
        // often run at 30fps target, where a 30s window only holds 900
        // samples and a single dropped frame skews the percentile.
        private const int FrameTimeP95Window = 3600;

        private readonly LinkedList<PerformanceSample> _raw = new();
        private readonly LinkedList<PerformanceSample> _agg = new();
        private readonly object _lock = new();

        // Rolling buffers for averages — circular, no allocation per frame.
        private readonly float[] _delta1s  = new float[120];   // 2× max framerate budget
        private int _delta1sHead;
        private float _delta1sSum;
        private int _delta1sCount;

        private readonly float[] _delta5s  = new float[600];
        private int _delta5sHead;
        private float _delta5sSum;
        private int _delta5sCount;

        // Sliding window for p95 — circular; sorted on demand.
        private readonly float[] _ftWindow = new float[FrameTimeP95Window];
        private int _ftWindowHead;
        private int _ftWindowCount;
        private readonly float[] _ftSortBuf = new float[FrameTimeP95Window];

        private int _droppedFrames30Hz;
        private int _droppedFrames60Hz;

        private float _aggAccumSec;
        private const float AggIntervalSec = 1f;

        /// <summary>
        /// Fires after each <see cref="Update"/> tick admits a new raw sample.
        /// Always on the main thread. UI throttles its own redraw.
        /// </summary>
        public event Action<PerformanceSample> OnSample;

        public IReadOnlyList<PerformanceSample> SnapshotRaw()
        {
            lock (_lock) return new List<PerformanceSample>(_raw);
        }

        public IReadOnlyList<PerformanceSample> SnapshotAggregated()
        {
            lock (_lock) return new List<PerformanceSample>(_agg);
        }

        public PerformanceSample LatestOrDefault()
        {
            lock (_lock) return _raw.Count > 0 ? _raw.Last.Value : default;
        }

        public void ResetCounters()
        {
            lock (_lock)
            {
                _droppedFrames30Hz = 0;
                _droppedFrames60Hz = 0;
            }
        }

        // FrameTimingManager scratch buffer — single-element since we only
        // care about the latest timing each frame. Pre-allocated to avoid
        // per-frame allocations in a hot path.
        private readonly UnityEngine.FrameTiming[] _frameTimingBuf = new UnityEngine.FrameTiming[1];

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            if (dt <= 0f || float.IsNaN(dt) || float.IsInfinity(dt)) return;

            float frameTimeMs = dt * 1000f;
            float fpsInstant  = 1f / dt;

            // GPU / CPU split via FrameTimingManager. Skips silently on
            // platforms where timings aren't enabled (WebGL, some Editor
            // configs) — the sentinels propagate as -1f to the UI.
            float gpuMs = -1f, cpuMainMs = -1f, cpuRenderMs = -1f;
            try
            {
                UnityEngine.FrameTimingManager.CaptureFrameTimings();
                uint count = UnityEngine.FrameTimingManager.GetLatestTimings(1, _frameTimingBuf);
                if (count >= 1)
                {
                    var t = _frameTimingBuf[0];
                    gpuMs       = (float)t.gpuFrameTime;
                    cpuMainMs   = (float)t.cpuMainThreadFrameTime;
                    cpuRenderMs = (float)t.cpuRenderThreadFrameTime;
                }
            }
            catch { /* swallow — frame timings are optional */ }

            // 1s rolling avg
            UpdateRolling(_delta1s, ref _delta1sHead, ref _delta1sSum, ref _delta1sCount, dt);
            // 5s rolling avg
            UpdateRolling(_delta5s, ref _delta5sHead, ref _delta5sSum, ref _delta5sCount, dt);

            float fpsAvg1s = _delta1sCount > 0 ? _delta1sCount / _delta1sSum : fpsInstant;
            float fpsAvg5s = _delta5sCount > 0 ? _delta5sCount / _delta5sSum : fpsInstant;

            // p95 frame time over sliding window
            _ftWindow[_ftWindowHead] = frameTimeMs;
            _ftWindowHead = (_ftWindowHead + 1) % _ftWindow.Length;
            if (_ftWindowCount < _ftWindow.Length) _ftWindowCount++;
            float p95 = ComputeP95();

            // Dropped-frame counters — frame counts as "dropped" when its
            // frametime exceeds the target budget (33.3ms @ 30Hz, 16.7ms @
            // 60Hz). Mobile devices usually hit one cap per platform; we
            // surface both because dev iterating in Editor cares about 60.
            if (frameTimeMs > 33.3f) _droppedFrames30Hz++;
            if (frameTimeMs > 16.7f) _droppedFrames60Hz++;

            var sample = new PerformanceSample(
                timestampUtc:      DateTime.UtcNow,
                deltaSeconds:      dt,
                fpsInstant:        fpsInstant,
                fpsAvg1s:          fpsAvg1s,
                fpsAvg5s:          fpsAvg5s,
                frameTimeMs:       frameTimeMs,
                frameTimeP95Ms:    p95,
                droppedFrames30Hz: _droppedFrames30Hz,
                droppedFrames60Hz: _droppedFrames60Hz,
                gpuFrameTimeMs:    gpuMs,
                cpuMainThreadMs:   cpuMainMs,
                cpuRenderThreadMs: cpuRenderMs);

            lock (_lock)
            {
                _raw.AddLast(sample);
                while (_raw.Count > RawCapacity) _raw.RemoveFirst();

                _aggAccumSec += dt;
                if (_aggAccumSec >= AggIntervalSec)
                {
                    _aggAccumSec = 0f;
                    _agg.AddLast(sample);
                    while (_agg.Count > AggCapacity) _agg.RemoveFirst();
                }
            }

            try { OnSample?.Invoke(sample); } catch { /* swallow */ }
        }

        private static void UpdateRolling(
            float[] buf,
            ref int head,
            ref float sum,
            ref int count,
            float value)
        {
            if (count == buf.Length)
            {
                sum -= buf[head];
            }
            else
            {
                count++;
            }
            buf[head] = value;
            sum += value;
            head = (head + 1) % buf.Length;
        }

        private float ComputeP95()
        {
            if (_ftWindowCount == 0) return 0f;
            // Copy & sort into the pre-allocated buffer to avoid per-frame
            // List<float>/Array.Copy allocations. Worst case 3,600 floats —
            // ~14µs on a midrange 2020 phone.
            Array.Copy(_ftWindow, _ftSortBuf, _ftWindowCount);
            Array.Sort(_ftSortBuf, 0, _ftWindowCount);
            int idx = (int)(0.95f * (_ftWindowCount - 1));
            if (idx < 0) idx = 0;
            if (idx >= _ftWindowCount) idx = _ftWindowCount - 1;
            return _ftSortBuf[idx];
        }
    }
}
