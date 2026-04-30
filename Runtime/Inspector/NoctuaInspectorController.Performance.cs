using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// "Perf" tab — FPS, frame time, dropped-frame counters. Numbers come
    /// from <see cref="PerformanceMonitor"/>, sampled every frame; the tab
    /// re-renders at the controller's existing throttle (dirty flag set
    /// when the monitor's <c>OnSample</c> fires — see Install wiring).
    ///
    /// Sparkline rendering is intentionally minimal — Unity UIElements
    /// doesn't expose a native sparkline; we use a row of thin
    /// <see cref="VisualElement"/> bars whose height scales by FPS. Cheap,
    /// allocation-free per frame, and good enough for a 1Hz update.
    /// </summary>
    public partial class NoctuaInspectorController
    {
        private const int PerfSparklineBars = 60;
        private bool _perfHudVisible;
        private VisualElement _perfHud;
        private Label _perfHudLabel;

        private void RenderPerformance(ref int ok, ref int failing, ref int inflight)
        {
            if (_perfMonitor == null)
            {
                _listContainer.Add(MakeMutedLabel("Performance monitor not available."));
                return;
            }

            var latest = _perfMonitor.LatestOrDefault();
            // Big readout
            _listContainer.Add(BuildPerfReadout(latest));
            // Sparkline
            _listContainer.Add(BuildPerfSparkline());
            // Counters
            _listContainer.Add(BuildPerfCounters(latest));

            // GPU / CPU split — only render when FrameTimingManager
            // returned real numbers (sentinels are <0).
            if (latest.GpuFrameTimeMs >= 0f || latest.CpuMainThreadMs >= 0f)
            {
                _listContainer.Add(BuildPerfFrameTimings(latest));
            }

            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.flexWrap = Wrap.Wrap;
            btnRow.style.paddingLeft = 12; btnRow.style.paddingTop = 8;
            btnRow.Add(MakeButton("Reset dropped-frame counters", () =>
            {
                _perfMonitor.ResetCounters();
                _dirty = true;
            }));
            // Permanent HUD overlay — visible even when the Inspector is
            // closed, so devs can watch FPS during free-roam testing
            // without re-opening the panel.
            btnRow.Add(MakeButton(_perfHudVisible ? "Hide HUD" : "Show HUD", () =>
            {
                _perfHudVisible = !_perfHudVisible;
                EnsurePerfHud();
                if (_perfHud != null)
                    _perfHud.style.display = _perfHudVisible ? DisplayStyle.Flex : DisplayStyle.None;
                _dirty = true;
            }));
            _listContainer.Add(btnRow);

            // Status bar contribution: treat <55fps as "failing" hint.
            if (latest.FpsAvg1s < 55f && latest.FpsAvg1s > 0f) failing++;
            else ok++;
        }

        private VisualElement BuildPerfReadout(PerformanceSample s)
        {
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.paddingTop = 12; box.style.paddingBottom = 8;

            void AddCol(string label, string value, Color valueColor)
            {
                var col = new VisualElement();
                col.style.flexDirection = FlexDirection.Column;
                col.style.flexGrow = 1;

                var l = new Label(label);
                l.style.color = TextMid; l.style.fontSize = 12;
                col.Add(l);

                var v = new Label(value);
                v.style.color = valueColor;
                v.style.fontSize = 22;
                v.style.unityFontStyleAndWeight = FontStyle.Bold;
                col.Add(v);
                box.Add(col);
            }

            AddCol("FPS (1s)", s.FpsAvg1s.ToString("F0"), FpsColor(s.FpsAvg1s));
            AddCol("FPS (5s)", s.FpsAvg5s.ToString("F0"), FpsColor(s.FpsAvg5s));
            AddCol("Frame ms", s.FrameTimeMs.ToString("F1"), FrameColor(s.FrameTimeMs));
            AddCol("p95 ms",   s.FrameTimeP95Ms.ToString("F1"), FrameColor(s.FrameTimeP95Ms));
            return box;
        }

        private VisualElement BuildPerfSparkline()
        {
            var wrap = new VisualElement();
            wrap.style.paddingLeft = 12; wrap.style.paddingRight = 12;
            wrap.style.paddingTop = 8; wrap.style.paddingBottom = 8;

            var caption = new Label("FPS — last 60 samples");
            caption.style.color = TextLo; caption.style.fontSize = 12;
            wrap.Add(caption);

            var bars = new VisualElement();
            bars.style.flexDirection = FlexDirection.Row;
            bars.style.height = 40;
            bars.style.alignItems = Align.FlexEnd;
            bars.style.backgroundColor = Bg2;

            var samples = _perfMonitor.SnapshotRaw();
            int start = Mathf.Max(0, samples.Count - PerfSparklineBars);
            for (int i = start; i < samples.Count; i++)
            {
                var s = samples[i];
                float h = Mathf.Clamp01(s.FpsInstant / 60f) * 40f;
                var bar = new VisualElement();
                bar.style.width = 4;
                bar.style.marginRight = 1;
                bar.style.height = Mathf.Max(1f, h);
                bar.style.backgroundColor = FpsColor(s.FpsInstant);
                bars.Add(bar);
            }
            wrap.Add(bars);
            return wrap;
        }

        private VisualElement BuildPerfCounters(PerformanceSample s)
        {
            var box = new VisualElement();
            box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.paddingTop = 8; box.style.paddingBottom = 8;

            void AddRow(string label, string value)
            {
                var r = new VisualElement();
                r.style.flexDirection = FlexDirection.Row;
                r.style.paddingTop = 5; r.style.paddingBottom = 5;
                var l = new Label(label);
                l.style.color = TextMid; l.style.fontSize = 13;
                l.style.flexGrow = 1;
                var v = new Label(value);
                v.style.color = TextHi; v.style.fontSize = 13;
                v.style.unityFontStyleAndWeight = FontStyle.Bold;
                r.Add(l); r.Add(v);
                box.Add(r);
            }

            AddRow("Dropped frames > 16.7ms (60Hz)", s.DroppedFrames60Hz.ToString());
            AddRow("Dropped frames > 33.3ms (30Hz)", s.DroppedFrames30Hz.ToString());
            return box;
        }

        private Color FpsColor(float fps)
        {
            if (fps >= 55f) return Ok;
            if (fps >= 28f) return Warn;
            return Err;
        }

        private Color FrameColor(float ms)
        {
            if (ms <= 17f)  return Ok;
            if (ms <= 33.4f) return Warn;
            return Err;
        }

        private VisualElement BuildPerfFrameTimings(PerformanceSample s)
        {
            var box = new VisualElement();
            box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.paddingTop = 8; box.style.paddingBottom = 8;

            var caption = new Label("Frame timings (FrameTimingManager)");
            caption.style.color = TextLo; caption.style.fontSize = 12;
            box.Add(caption);

            void AddRow(string label, float ms)
            {
                var r = new VisualElement();
                r.style.flexDirection = FlexDirection.Row;
                r.style.paddingTop = 5; r.style.paddingBottom = 5;
                var l = new Label(label);
                l.style.color = TextMid; l.style.fontSize = 13;
                l.style.flexGrow = 1;
                var v = new Label(ms < 0f ? "—" : $"{ms:F2} ms");
                v.style.color = ms < 0f ? TextMid : FrameColor(ms);
                v.style.fontSize = 13;
                v.style.unityFontStyleAndWeight = FontStyle.Bold;
                r.Add(l); r.Add(v);
                box.Add(r);
            }

            AddRow("GPU frame time",       s.GpuFrameTimeMs);
            AddRow("CPU main thread",      s.CpuMainThreadMs);
            AddRow("CPU render thread",    s.CpuRenderThreadMs);
            return box;
        }

        /// <summary>
        /// Lazy-create a fixed HUD overlay attached to the root panel —
        /// visible regardless of whether the Inspector overlay is open.
        /// Updates each frame from <see cref="_perfMonitor.OnSample"/>.
        /// Cheap: a single Label re-text per frame, zero allocations
        /// once warm thanks to UI Toolkit's text-rendering buffer reuse.
        /// </summary>
        private void EnsurePerfHud()
        {
            if (_perfHud != null) return;
            // Attach to the document's rootVisualElement so the HUD lives
            // even when _root.style.display = None (Inspector closed).
            var docRoot = _doc?.rootVisualElement;
            if (docRoot == null) return;

            _perfHud = new VisualElement();
            _perfHud.style.position = Position.Absolute;
            _perfHud.style.top = 8; _perfHud.style.right = 8;
            _perfHud.style.paddingLeft = 8; _perfHud.style.paddingRight = 8;
            _perfHud.style.paddingTop = 4; _perfHud.style.paddingBottom = 4;
            _perfHud.style.backgroundColor = new Color(0, 0, 0, 0.6f);
            _perfHud.style.borderTopLeftRadius = 4; _perfHud.style.borderTopRightRadius = 4;
            _perfHud.style.borderBottomLeftRadius = 4; _perfHud.style.borderBottomRightRadius = 4;
            // pickingMode = Ignore so the HUD never absorbs game touches.
            _perfHud.pickingMode = PickingMode.Ignore;
            _perfHud.style.display = DisplayStyle.None;

            _perfHudLabel = new Label("--");
            _perfHudLabel.style.color = Color.white;
            _perfHudLabel.style.fontSize = 13;
            _perfHudLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _perfHudLabel.pickingMode = PickingMode.Ignore;
            _perfHud.Add(_perfHudLabel);

            docRoot.Add(_perfHud);

            if (_perfMonitor != null)
            {
                _perfMonitor.OnSample += UpdatePerfHud;
            }
        }

        private void UpdatePerfHud(PerformanceSample s)
        {
            if (_perfHudLabel == null || !_perfHudVisible) return;
            // Compact one-line readout. Format kept stable so devs can
            // grep screenshots / video recordings later.
            _perfHudLabel.text = s.GpuFrameTimeMs >= 0f
                ? $"FPS {s.FpsAvg1s:F0}  ms {s.FrameTimeMs:F1}  gpu {s.GpuFrameTimeMs:F1}"
                : $"FPS {s.FpsAvg1s:F0}  ms {s.FrameTimeMs:F1}";
        }
    }
}
