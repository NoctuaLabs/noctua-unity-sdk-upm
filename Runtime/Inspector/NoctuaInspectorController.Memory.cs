using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// "Memory" tab — Mono heap, Unity native, native footprint, plus
    /// destructive actions (Force GC, Unload Unused Assets, Clear caches,
    /// Wipe PlayerPrefs). Each destructive action prompts a confirmation
    /// before invoking — wipe-PlayerPrefs additionally requires a press
    /// counter to guard against fat-fingering.
    /// </summary>
    public partial class NoctuaInspectorController
    {
        private int _wipePrefsConfirmCount; // 0 = idle, 1 = first tap, 2 = committed
        private int _clearAssetCacheConfirm; // 0 = idle, 1 = pending confirmation
        private int _clearNativeCacheConfirm; // 0 = idle, 1 = pending confirmation
        // Wipe PlayerPrefs hold-to-confirm — uses UI Toolkit's schedule API
        // to fire at 3s once a PointerDown is received, cancelled on PointerUp.
        private const float WipePrefsHoldSeconds = 3f;

        private void RenderMemory(ref int ok, ref int failing, ref int inflight)
        {
            if (_memMonitor == null)
            {
                _listContainer.Add(MakeMutedLabel("Memory monitor not available."));
                return;
            }

            var s = _memMonitor.LatestOrDefault();
            _listContainer.Add(BuildMemReadout(s));
            _listContainer.Add(BuildMemChart());
            _listContainer.Add(BuildMemActions());

            ok++; // memory tab is informational
        }

        /// <summary>
        /// 10-minute time-series chart driven by <see cref="MemoryMonitor"/>'s
        /// 1Hz aggregate buffer. Three lines overlaid:
        ///   * Mono used (yellow)        — managed heap in use
        ///   * Unity allocated (orange)  — engine-side native allocations
        ///   * Native phys footprint (red) — when bridge available; iOS
        ///     phys_footprint or Android PSS
        /// Drawn via UI Toolkit's <c>generateVisualContent</c> + Painter2D —
        /// no per-frame allocations once the panel is realised.
        /// </summary>
        private VisualElement BuildMemChart()
        {
            var wrap = new VisualElement();
            wrap.style.paddingLeft = 12; wrap.style.paddingRight = 12;
            wrap.style.paddingTop = 12; wrap.style.paddingBottom = 4;

            var caption = new Label("Memory — last 10 min (1Hz)");
            caption.style.color = TextLo; caption.style.fontSize = 10;
            wrap.Add(caption);

            var canvas = new VisualElement();
            canvas.style.height = 80;
            canvas.style.backgroundColor = Bg2;
            canvas.generateVisualContent += DrawMemChart;
            wrap.Add(canvas);

            var legend = new VisualElement();
            legend.style.flexDirection = FlexDirection.Row;
            legend.style.paddingTop = 4;
            legend.Add(MakeLegendDot(Color.yellow,    "Mono"));
            legend.Add(MakeLegendDot(Warn,            "Unity"));
            legend.Add(MakeLegendDot(Err,             "Native"));
            wrap.Add(legend);

            return wrap;
        }

        private static VisualElement MakeLegendDot(Color color, string label)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginRight = 12;
            var dot = new VisualElement();
            dot.style.width = 8; dot.style.height = 8;
            dot.style.backgroundColor = color;
            dot.style.borderTopLeftRadius = 4; dot.style.borderTopRightRadius = 4;
            dot.style.borderBottomLeftRadius = 4; dot.style.borderBottomRightRadius = 4;
            dot.style.marginRight = 4;
            row.Add(dot);
            var lbl = new Label(label);
            lbl.style.color = new Color(0xA0 / 255f, 0xA3 / 255f, 0xA7 / 255f, 1f);
            lbl.style.fontSize = 10;
            row.Add(lbl);
            return row;
        }

        private void DrawMemChart(MeshGenerationContext mgc)
        {
            if (_memMonitor == null) return;
            var samples = _memMonitor.Snapshot();
            if (samples.Count < 2) return;

            var ve = mgc.visualElement;
            float w = ve.contentRect.width;
            float h = ve.contentRect.height;
            if (w <= 0 || h <= 0) return;

            // Compute the chart's max so all 3 series share scale. Use the
            // max of (UnityReserved, native phys) — UnityReserved is the
            // platform's high-water mark for engine memory, and native phys
            // covers iOS/Android process footprint.
            long maxBytes = 1;
            foreach (var s in samples)
            {
                if (s.UnityReservedBytes > maxBytes) maxBytes = s.UnityReservedBytes;
                if (s.Native.PhysFootprintBytes > maxBytes) maxBytes = s.Native.PhysFootprintBytes;
            }
            float maxF = (float)maxBytes;

            var painter = mgc.painter2D;
            painter.lineWidth = 1.5f;
            painter.lineJoin = LineJoin.Round;
            painter.lineCap = LineCap.Butt;

            // Plot helper closure — strokes one series.
            void Stroke(System.Func<MemorySample, long> selector, Color color)
            {
                painter.BeginPath();
                painter.strokeColor = color;
                bool first = true;
                int n = samples.Count;
                for (int i = 0; i < n; i++)
                {
                    var v = selector(samples[i]);
                    if (v < 0) continue; // sentinel — skip native lines on platforms that don't expose
                    float x = (n == 1) ? w : ((float)i / (n - 1)) * w;
                    float y = h - (((float)v / maxF) * h);
                    if (first) { painter.MoveTo(new Vector2(x, y)); first = false; }
                    else painter.LineTo(new Vector2(x, y));
                }
                painter.Stroke();
            }

            Stroke(s => s.MonoUsedBytes,         Color.yellow);
            Stroke(s => s.UnityAllocatedBytes,   Warn);
            Stroke(s => s.Native.PhysFootprintBytes, Err);
        }

        private VisualElement BuildMemReadout(MemorySample s)
        {
            var box = new VisualElement();
            box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.paddingTop = 12; box.style.paddingBottom = 8;

            void AddRow(string label, string value, Color color)
            {
                var r = new VisualElement();
                r.style.flexDirection = FlexDirection.Row;
                r.style.paddingTop = 3; r.style.paddingBottom = 3;
                var l = new Label(label);
                l.style.color = TextMid; l.style.fontSize = 11;
                l.style.flexGrow = 1;
                var v = new Label(value);
                v.style.color = color; v.style.fontSize = 12;
                v.style.unityFontStyleAndWeight = FontStyle.Bold;
                r.Add(l); r.Add(v);
                box.Add(r);
            }

            AddRow("Mono used",          FormatBytes(s.MonoUsedBytes),       TextHi);
            AddRow("Mono heap",          FormatBytes(s.MonoHeapBytes),       TextHi);
            AddRow("Unity allocated",    FormatBytes(s.UnityAllocatedBytes), TextHi);
            AddRow("Unity reserved",     FormatBytes(s.UnityReservedBytes),  TextHi);
            AddRow("GC.GetTotalMemory",  FormatBytes(s.GcTotalBytes),        TextHi);
            AddRow("Asset cache",        FormatBytes(s.AssetCacheBytes),     TextMid);

            // Native section
            var divider = new Label("Native");
            divider.style.color = TextLo; divider.style.fontSize = 10;
            divider.style.paddingTop = 8; divider.style.paddingBottom = 4;
            box.Add(divider);

            AddRow("Phys footprint",     s.Native.PhysFootprintBytes >= 0 ? FormatBytes(s.Native.PhysFootprintBytes) : "—", TextHi);
            AddRow("Available",          s.Native.AvailableBytes >= 0 ? FormatBytes(s.Native.AvailableBytes) : "—", TextHi);
            AddRow("System total",       s.Native.SystemTotalBytes >= 0 ? FormatBytes(s.Native.SystemTotalBytes) : "—", TextMid);
            AddRow("Low memory",         s.Native.LowMemory ? "YES" : "no", s.Native.LowMemory ? Err : TextMid);
            AddRow("Thermal",            s.Native.Thermal.ToString(), ThermalColor(s.Native.Thermal));
            return box;
        }

        private VisualElement BuildMemActions()
        {
            var wrap = new VisualElement();
            wrap.style.paddingLeft = 12; wrap.style.paddingRight = 12;
            wrap.style.paddingTop = 12; wrap.style.paddingBottom = 12;

            var head = new Label("Actions");
            head.style.color = TextMid; head.style.fontSize = 10;
            head.style.paddingBottom = 6;
            wrap.Add(head);

            // Buttons in a flex row that wraps on narrow viewports.
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.flexWrap = Wrap.Wrap;

            // Non-destructive — instant tap.
            row.Add(MakeMemActionButton("Force GC",             () => MemoryMonitor.ForceGC()));
            row.Add(MakeMemActionButton("Unload Unused Assets", () => MemoryMonitor.UnloadUnusedAssets()));

            // Two-tap confirm — first tap arms ("Tap again to confirm"),
            // second tap commits. Idle state on every render.
            row.Add(MakeConfirmButton(
                idleLabel:  "Clear Asset Cache",
                pendingRef: () => _clearAssetCacheConfirm,
                setPending: v => { _clearAssetCacheConfirm = v; _dirty = true; },
                commit: () => MemoryMonitor.ClearAssetBundleCache()));

            row.Add(MakeConfirmButton(
                idleLabel:  "Clear Native HTTP Cache",
                pendingRef: () => _clearNativeCacheConfirm,
                setPending: v => { _clearNativeCacheConfirm = v; _dirty = true; },
                commit: () => _memMonitor?.ClearNativeHttpCache()));

            // Hold-to-confirm for the most destructive action — tap-and-hold
            // the button for 3s to commit. Released early cancels the timer.
            row.Add(MakeWipePlayerPrefsButton());

            wrap.Add(row);
            return wrap;
        }

        /// <summary>
        /// Two-tap confirm pattern. Idle button shows the label; first tap
        /// arms it (red, "Tap again to confirm: …"); second tap commits +
        /// returns to idle. If the user navigates away before tap 2, the
        /// next render reverts to idle since `pendingRef` is in-memory only.
        /// </summary>
        private VisualElement MakeConfirmButton(
            string idleLabel,
            System.Func<int> pendingRef,
            System.Action<int> setPending,
            System.Action commit)
        {
            int pending = pendingRef();
            var label = pending == 0 ? idleLabel : $"Tap again: {idleLabel}";
            var color = pending == 0 ? Bg2 : Warn;
            return MakeMemActionButton(label, () =>
            {
                if (pendingRef() == 0)
                {
                    setPending(1);
                }
                else
                {
                    try { commit(); } catch { /* swallow — actions are best-effort */ }
                    setPending(0);
                }
            }, accent: color);
        }

        /// <summary>
        /// Hold-to-confirm Wipe PlayerPrefs. Uses UI Toolkit's
        /// <c>PointerDownEvent</c> / <c>PointerUpEvent</c> + the
        /// VisualElement scheduler so the timer is panel-driven (no
        /// MonoBehaviour Update needed). Releasing before the 3s mark
        /// cancels.
        /// </summary>
        private VisualElement MakeWipePlayerPrefsButton()
        {
            var labelText = _wipePrefsConfirmCount == 0
                ? $"Hold {WipePrefsHoldSeconds:0}s: Wipe PlayerPrefs"
                : _wipePrefsConfirmCount == 1
                    ? "…holding…"
                    : "Wiping…";
            var color = _wipePrefsConfirmCount == 0 ? Err : Err;

            var btn = new Label(labelText);
            btn.style.color = TextHi;
            btn.style.backgroundColor = color;
            btn.style.paddingLeft = 12; btn.style.paddingRight = 12;
            btn.style.paddingTop = 8; btn.style.paddingBottom = 8;
            btn.style.marginRight = 6; btn.style.marginBottom = 6;
            btn.style.fontSize = 11;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            btn.style.borderTopLeftRadius = 4; btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4; btn.style.borderBottomRightRadius = 4;

            IVisualElementScheduledItem scheduled = null;

            btn.RegisterCallback<PointerDownEvent>(_ =>
            {
                if (_wipePrefsConfirmCount != 0) return;
                _wipePrefsConfirmCount = 1;
                _dirty = true;
                scheduled = btn.schedule.Execute(() =>
                {
                    if (_wipePrefsConfirmCount != 1) return;
                    _wipePrefsConfirmCount = 2;
                    MemoryMonitor.WipePlayerPrefs();
                    _wipePrefsConfirmCount = 0;
                    _dirty = true;
                }).StartingIn((long)(WipePrefsHoldSeconds * 1000));
            });

            // Either pointer release or pointer-out cancels the pending hold.
            void Cancel()
            {
                scheduled?.Pause();
                scheduled = null;
                if (_wipePrefsConfirmCount == 1)
                {
                    _wipePrefsConfirmCount = 0;
                    _dirty = true;
                }
            }
            btn.RegisterCallback<PointerUpEvent>(_ => Cancel());
            btn.RegisterCallback<PointerLeaveEvent>(_ => Cancel());
            btn.RegisterCallback<PointerCancelEvent>(_ => Cancel());
            return btn;
        }

        private VisualElement MakeMemActionButton(string text, System.Action onClick, Color? accent = null)
        {
            var c = accent ?? Bg2;
            var btn = new Label(text);
            btn.style.color = TextHi;
            btn.style.backgroundColor = c;
            btn.style.paddingLeft = 12; btn.style.paddingRight = 12;
            btn.style.paddingTop = 8; btn.style.paddingBottom = 8;
            btn.style.marginRight = 6; btn.style.marginBottom = 6;
            btn.style.fontSize = 11;
            btn.style.borderTopLeftRadius = 4; btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4; btn.style.borderBottomRightRadius = 4;
            btn.RegisterCallback<ClickEvent>(_ => onClick());
            return btn;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 0) return "—";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024L * 1024) return $"{bytes / 1024f:F1} KB";
            if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024f * 1024f):F1} MB";
            return $"{bytes / (1024f * 1024f * 1024f):F2} GB";
        }

        private Color ThermalColor(ThermalState t) => t switch
        {
            ThermalState.Critical => Err,
            ThermalState.Serious  => Err,
            ThermalState.Fair     => Warn,
            ThermalState.Nominal  => Ok,
            _                     => TextMid,
        };
    }
}
