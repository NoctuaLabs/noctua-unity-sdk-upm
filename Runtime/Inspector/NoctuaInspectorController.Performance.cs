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

            // Reset button
            var resetBtn = MakeButton("Reset dropped-frame counters", () =>
            {
                _perfMonitor.ResetCounters();
                _dirty = true;
            });
            var btnRow = new VisualElement();
            btnRow.style.flexDirection = FlexDirection.Row;
            btnRow.style.paddingLeft = 12; btnRow.style.paddingTop = 8;
            btnRow.Add(resetBtn);
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
                l.style.color = TextMid; l.style.fontSize = 10;
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
            caption.style.color = TextLo; caption.style.fontSize = 10;
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
                r.style.paddingTop = 2; r.style.paddingBottom = 2;
                var l = new Label(label);
                l.style.color = TextMid; l.style.fontSize = 11;
                l.style.flexGrow = 1;
                var v = new Label(value);
                v.style.color = TextHi; v.style.fontSize = 11;
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
    }
}
