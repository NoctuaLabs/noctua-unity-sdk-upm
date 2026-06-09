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
    /// Hot-path discipline: zero per-frame allocations. The raw and aggregate
    /// stores are fixed <c>PerformanceSample[]</c> ring buffers (not a
    /// <see cref="LinkedList{T}"/>, whose <c>AddLast</c> allocates a node every
    /// frame); the percentile window is a pre-allocated <c>float[]</c> sorted at
    /// ~1Hz (not every frame). <see cref="OnSample"/> consumers must not allocate
    /// inside their handler.
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

        // Ring buffers — pre-allocated arrays, zero per-frame allocation.
        // (A LinkedList allocates a node on every AddLast; a fixed array does not.)
        private readonly PerformanceSample[] _raw = new PerformanceSample[RawCapacity];
        private int _rawHead;   // index of the next write slot
        private int _rawCount;

        private readonly PerformanceSample[] _agg = new PerformanceSample[AggCapacity];
        private int _aggHead;
        private int _aggCount;

        private readonly object _lock = new();

        // p95 is a 60s-window statistic — it barely moves frame-to-frame, so we sort the
        // window at ~1Hz and reuse the cached value for per-frame samples in between. This is
        // the single biggest saver: a 3,600-element sort goes from 60×/sec to 1×/sec.
        private float _lastP95;
        private float _p95AccumSec;
        private const float P95IntervalSec = 1f;

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
            lock (_lock) return CopyRing(_raw, _rawHead, _rawCount);
        }

        public IReadOnlyList<PerformanceSample> SnapshotAggregated()
        {
            lock (_lock) return CopyRing(_agg, _aggHead, _aggCount);
        }

        public PerformanceSample LatestOrDefault()
        {
            lock (_lock)
                return _rawCount > 0 ? _raw[(_rawHead - 1 + _raw.Length) % _raw.Length] : default;
        }

        // Reconstructs the ring's contents oldest-first into a new list. Only called by the UI
        // on redraw (a few times/sec), never on the per-frame hot path.
        private static List<PerformanceSample> CopyRing(PerformanceSample[] ring, int head, int count)
        {
            var list = new List<PerformanceSample>(count);
            int start = (head - count + ring.Length) % ring.Length;
            for (int i = 0; i < count; i++) list.Add(ring[(start + i) % ring.Length]);
            return list;
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

            // p95 frame time over sliding window. Append every frame (cheap ring write), but only
            // re-sort/recompute the percentile at ~1Hz — sorting 3,600 floats every frame was the
            // dominant cost. Between recomputes we reuse the cached value (a 60s p95 is stable
            // frame-to-frame anyway). First sample computes immediately so the UI isn't blank.
            _ftWindow[_ftWindowHead] = frameTimeMs;
            _ftWindowHead = (_ftWindowHead + 1) % _ftWindow.Length;
            if (_ftWindowCount < _ftWindow.Length) _ftWindowCount++;

            _p95AccumSec += dt;
            if (_p95AccumSec >= P95IntervalSec || _lastP95 == 0f)
            {
                _p95AccumSec = 0f;
                _lastP95 = ComputeP95();
            }
            float p95 = _lastP95;

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
                // Ring write — overwrites the oldest slot once full, no allocation.
                _raw[_rawHead] = sample;
                _rawHead = (_rawHead + 1) % RawCapacity;
                if (_rawCount < RawCapacity) _rawCount++;

                _aggAccumSec += dt;
                if (_aggAccumSec >= AggIntervalSec)
                {
                    _aggAccumSec = 0f;
                    _agg[_aggHead] = sample;
                    _aggHead = (_aggHead + 1) % AggCapacity;
                    if (_aggCount < AggCapacity) _aggCount++;
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
