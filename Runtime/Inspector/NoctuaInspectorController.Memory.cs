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

        private void RenderMemory(ref int ok, ref int failing, ref int inflight)
        {
            if (_memMonitor == null)
            {
                _listContainer.Add(MakeMutedLabel("Memory monitor not available."));
                return;
            }

            var s = _memMonitor.LatestOrDefault();
            _listContainer.Add(BuildMemReadout(s));
            _listContainer.Add(BuildMemActions());

            ok++; // memory tab is informational
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

            row.Add(MakeMemActionButton("Force GC",            () => MemoryMonitor.ForceGC()));
            row.Add(MakeMemActionButton("Unload Unused Assets",() => MemoryMonitor.UnloadUnusedAssets()));
            row.Add(MakeMemActionButton("Clear Asset Cache",   () => MemoryMonitor.ClearAssetBundleCache()));
            row.Add(MakeMemActionButton(
                _wipePrefsConfirmCount == 0 ? "Wipe PlayerPrefs"
                : _wipePrefsConfirmCount == 1 ? "Tap again to confirm" : "Wiping…",
                () =>
                {
                    if (_wipePrefsConfirmCount == 0)
                    {
                        _wipePrefsConfirmCount = 1;
                    }
                    else if (_wipePrefsConfirmCount == 1)
                    {
                        _wipePrefsConfirmCount = 2;
                        MemoryMonitor.WipePlayerPrefs();
                        _wipePrefsConfirmCount = 0;
                    }
                    _dirty = true;
                },
                accent: _wipePrefsConfirmCount == 0 ? Bg2 : Err));
            wrap.Add(row);
            return wrap;
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
