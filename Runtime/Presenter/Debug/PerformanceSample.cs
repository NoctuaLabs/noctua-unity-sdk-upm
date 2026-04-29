using System;

namespace com.noctuagames.sdk
{
    /// <summary>
    /// Per-frame snapshot of rendering performance. One sample per Unity
    /// frame — the monitor keeps a short raw buffer plus a longer aggregate
    /// buffer so the Inspector can show "this frame" detail and "last 10
    /// minutes" trends without retaining 60×60×10 = 36,000 raw entries.
    ///
    /// Pure data; immutable after construction.
    /// </summary>
    public readonly struct PerformanceSample
    {
        public DateTime TimestampUtc  { get; }
        public float    DeltaSeconds  { get; }   // unscaled delta time, this frame
        public float    FpsInstant    { get; }   // 1f / DeltaSeconds
        public float    FpsAvg1s      { get; }   // rolling 1s average
        public float    FpsAvg5s      { get; }   // rolling 5s average
        public float    FrameTimeMs   { get; }   // DeltaSeconds * 1000
        public float    FrameTimeP95Ms { get; }  // 95th percentile, 60s window
        public int      DroppedFrames30Hz { get; } // cumulative since session
        public int      DroppedFrames60Hz { get; }

        public PerformanceSample(
            DateTime timestampUtc,
            float deltaSeconds,
            float fpsInstant,
            float fpsAvg1s,
            float fpsAvg5s,
            float frameTimeMs,
            float frameTimeP95Ms,
            int droppedFrames30Hz,
            int droppedFrames60Hz)
        {
            TimestampUtc      = timestampUtc;
            DeltaSeconds      = deltaSeconds;
            FpsInstant        = fpsInstant;
            FpsAvg1s          = fpsAvg1s;
            FpsAvg5s          = fpsAvg5s;
            FrameTimeMs       = frameTimeMs;
            FrameTimeP95Ms    = frameTimeP95Ms;
            DroppedFrames30Hz = droppedFrames30Hz;
            DroppedFrames60Hz = droppedFrames60Hz;
        }
    }
}
