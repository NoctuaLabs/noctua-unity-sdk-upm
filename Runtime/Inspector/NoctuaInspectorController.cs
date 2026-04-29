using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// Auto-spawned Inspector overlay host. Creates a DontDestroyOnLoad
    /// <c>__NoctuaInspector</c> GameObject with a fullscreen <c>UIDocument</c>
    /// and binds UI to <see cref="Noctua.HttpLog"/> and <see cref="Noctua.DebugMonitor"/>.
    ///
    /// Only spawned when <see cref="Noctua.IsSandbox"/> — never referenced
    /// in production. All UI built programmatically (no UXML/USS resources
    /// required), so the SDK ships as-is without extra asset files.
    /// </summary>
    public partial class NoctuaInspectorController : MonoBehaviour
    {
        // ----- theme tokens (kept centrally; matches plan's USS palette) -----
        private static readonly Color Bg0 = new(0x0F / 255f, 0x11 / 255f, 0x15 / 255f, 0.97f);
        private static readonly Color Bg1 = new(0x1F / 255f, 0x22 / 255f, 0x27 / 255f, 1f);
        private static readonly Color Bg2 = new(0x25 / 255f, 0x28 / 255f, 0x2E / 255f, 1f);
        private static readonly Color Stroke = new(0x35 / 255f, 0x3A / 255f, 0x42 / 255f, 1f);
        private static readonly Color TextHi = new(0xEA / 255f, 0xEA / 255f, 0xEA / 255f, 1f);
        private static readonly Color TextMid = new(0xA0 / 255f, 0xA3 / 255f, 0xA7 / 255f, 1f);
        private static readonly Color TextLo = new(0x7D / 255f, 0x7E / 255f, 0x81 / 255f, 1f);
        private static readonly Color Ok = new(0x17 / 255f, 0xA3 / 255f, 0x4A / 255f, 1f);
        private static readonly Color Warn = new(0xFB / 255f, 0x92 / 255f, 0x3C / 255f, 1f);
        private static readonly Color Err = new(0xAF / 255f, 0x1C / 255f, 0x36 / 255f, 1f);
        private static readonly Color AccentHttp = new(0x3B / 255f, 0x82 / 255f, 0xF6 / 255f, 1f);
        private static readonly Color AccentTracker = new(0xA8 / 255f, 0x5F / 255f, 0xF7 / 255f, 1f);

        private enum Tab { Timeline, Http, Trackers, Logs, Perf, Memory, Build }

        private UIDocument _doc;
        private PanelSettings _panelSettings;
        private InspectorTrigger _trigger;
        private HttpInspectorLog _httpLog;
        private TrackerDebugMonitor _monitor;
        private LogInspectorLedger _logLedger;
        private UnityLogStream _unityLogStream;
        private PerformanceMonitor _perfMonitor;
        private MemoryMonitor _memMonitor;
        private bool _visible;
        private bool _dirty;
        private bool _editorBannerDismissed;
        private Tab _tab = Tab.Timeline;
        private string _trackerFilter = "All";

        private VisualElement _root;
        private VisualElement _listContainer;
        private VisualElement _filterBar;
        private readonly Dictionary<string, Label> _filterChips = new();
        private Label _tabTimelineBtn, _tabHttpBtn, _tabTrackersBtn, _tabLogsBtn, _tabPerfBtn, _tabMemoryBtn, _tabBuildBtn;
        private Label _statusBar;

        /// <summary>Exposed for the composition root to wire the native metrics provider after init.</summary>
        public MemoryMonitor MemoryMonitorComponent => _memMonitor;

        /// <summary>Exposed for the composition root and tests.</summary>
        public LogInspectorLedger LogLedger => _logLedger;
        /// <summary>Exposed for the composition root and tests.</summary>
        public PerformanceMonitor PerformanceMonitorComponent => _perfMonitor;

        /// <summary>
        /// Optional hook for "Logs → Native: ON/off" toggle. Composition
        /// root injects this so the controller doesn't reach into the
        /// View layer or hold a Platform-layer reference. Safe to call
        /// when null — toggle still updates the Unity-side flag, the
        /// native stream simply stays where it is.
        /// </summary>
        public System.Action<bool> NativeLogStreamToggle;

        /// <summary>
        /// Install the Inspector onto a freshly-spawned GameObject.
        /// Called by the composition root after SDK init.
        /// </summary>
        public static NoctuaInspectorController Install(HttpInspectorLog httpLog, TrackerDebugMonitor monitor)
        {
            return Install(httpLog, monitor, logLedger: null);
        }

        /// <summary>
        /// Extended <see cref="Install(HttpInspectorLog, TrackerDebugMonitor)"/>
        /// that also wires the Logs/Performance/Memory monitors. Performance
        /// and Memory monitors are <see cref="MonoBehaviour"/>s and are
        /// AddComponent'd onto the Inspector host GameObject so they share
        /// its lifecycle (DontDestroyOnLoad, OnDestroy cleanup).
        ///
        /// The composition root constructs the Unity log stream + ledger and
        /// passes them in so this method stays free of <see cref="Noctua"/>
        /// references (Inspector lives in the SDK; static View facade does
        /// not flow into MonoBehaviour land).
        /// </summary>
        public static NoctuaInspectorController Install(
            HttpInspectorLog httpLog,
            TrackerDebugMonitor monitor,
            LogInspectorLedger logLedger)
        {
            var go = new GameObject("__NoctuaInspector");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var ctrl = go.AddComponent<NoctuaInspectorController>();
            ctrl._httpLog = httpLog;
            ctrl._monitor = monitor;
            ctrl._logLedger = logLedger;
            ctrl._perfMonitor = go.AddComponent<PerformanceMonitor>();
            ctrl._memMonitor = go.AddComponent<MemoryMonitor>();
            ctrl._trigger = go.AddComponent<InspectorTrigger>();
            ctrl._trigger.OnTrigger += ctrl.Toggle;
            // Mark dirty on every new entry so RenderList refreshes on
            // change instead of every frame.
            if (httpLog != null) httpLog.OnExchange += _ => ctrl._dirty = true;
            if (monitor != null) monitor.OnEmission += _ => ctrl._dirty = true;
            if (logLedger != null) logLedger.OnEntry += _ => ctrl._dirty = true;
            // Performance fires per-frame — only flip dirty when the perf
            // tab is visible to avoid pointless re-render of HTTP / Tracker.
            // Memory fires at 1Hz — always dirty (cheap).
            if (ctrl._perfMonitor != null) ctrl._perfMonitor.OnSample += _ =>
            {
                if (ctrl._tab == Tab.Perf) ctrl._dirty = true;
            };
            if (ctrl._memMonitor != null) ctrl._memMonitor.OnSample += _ =>
            {
                if (ctrl._tab == Tab.Memory) ctrl._dirty = true;
            };
            return ctrl;
        }

        private void Awake()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.sortingOrder = 32767;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPhysicalSize;

            _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = _panelSettings;
            _doc.visualTreeAsset = null;
            BuildRoot();
            _root.style.display = DisplayStyle.None;
        }

        private void Update()
        {
            _httpLog?.Pump();
            _monitor?.Pump();
            _logLedger?.Pump();
            if (_visible && _dirty)
            {
                RenderList();
                _dirty = false;
            }
        }

        private void OnDestroy()
        {
            if (_trigger != null) _trigger.OnTrigger -= Toggle;
            if (_panelSettings != null) Destroy(_panelSettings);
        }

        /// <summary>Whether the overlay is currently displayed.</summary>
        public bool IsVisible => _visible;

        /// <summary>Show the overlay. No-op if already visible.</summary>
        public void Show()
        {
            if (_visible) return;
            Toggle();
        }

        /// <summary>Hide the overlay. No-op if already hidden.</summary>
        public void Hide()
        {
            if (!_visible) return;
            Toggle();
        }

        public void Toggle()
        {
            _visible = !_visible;
            _root.style.display = _visible ? DisplayStyle.Flex : DisplayStyle.None;
            if (_visible) { _dirty = true; RenderList(); _dirty = false; }
        }

        // -------- UI construction --------

        private void BuildRoot()
        {
            _root = _doc.rootVisualElement;
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0; _root.style.top = 0; _root.style.bottom = 0;
            _root.style.backgroundColor = Bg0;
            // Explicit column layout + never shrink; prevents header/tabs/filter
            // collapsing on top of each other on tall/short aspect ratios.
            _root.style.flexDirection = FlexDirection.Column;
            _root.style.flexShrink = 0;

            // PanelSettings created via ScriptableObject.CreateInstance has no
            // default theme assigned, so text glyphs render as empty. Assign
            // Unity's built-in runtime font (LegacyRuntime.ttf on 2022.2+,
            // falls back to Arial on older versions) as the root-inherited
            // font so every descendant Label renders.
            var font = LoadBuiltinFont();
            if (font != null) _root.style.unityFont = font;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.flexShrink = 0;
            header.style.paddingLeft = 12; header.style.paddingRight = 12;
            header.style.paddingTop = 8; header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Stroke;
            header.style.backgroundColor = Bg1;

            var logo = new VisualElement();
            logo.style.width = 18; logo.style.height = 18;
            logo.style.marginRight = 8;
            var logoTex = Resources.Load<Texture2D>("NoctuaLogo");
            if (logoTex != null)
            {
                logo.style.backgroundImage = new StyleBackground(logoTex);
                logo.style.unityBackgroundScaleMode = ScaleMode.ScaleToFit;
            }
            header.Add(logo);

            var title = new Label("Noctua Inspector");
            title.style.color = TextHi;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.fontSize = 14;
            title.style.flexGrow = 1;
            header.Add(title);

            // Use a real Button instead of a clickable Label so touch hit
            // area is reliable on mobile. Plain "X" glyph (not ✕) so it
            // renders in every fallback font.
            var close = new Button(() => Toggle()) { text = "Close" };
            close.style.color = TextHi;
            close.style.backgroundColor = Bg2;
            close.style.borderTopWidth = 0; close.style.borderBottomWidth = 0;
            close.style.borderLeftWidth = 0; close.style.borderRightWidth = 0;
            close.style.paddingLeft = 14; close.style.paddingRight = 14;
            close.style.paddingTop = 6; close.style.paddingBottom = 6;
            close.style.marginLeft = 4; close.style.marginRight = 0;
            close.style.fontSize = 12;
            close.style.minWidth = 56;
            close.style.borderTopLeftRadius = 4; close.style.borderTopRightRadius = 4;
            close.style.borderBottomLeftRadius = 4; close.style.borderBottomRightRadius = 4;
            header.Add(close);
            _root.Add(header);

            // Tab strip
            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.flexShrink = 0;
            tabs.style.backgroundColor = Bg1;
            tabs.style.borderBottomWidth = 1;
            tabs.style.borderBottomColor = Stroke;

            _tabTimelineBtn = MakeTab("Timeline", () => { _tab = Tab.Timeline; UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabHttpBtn     = MakeTab("HTTP",     () => { _tab = Tab.Http;     UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabTrackersBtn = MakeTab("Trackers", () => { _tab = Tab.Trackers; UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabLogsBtn     = MakeTab("Logs",     () => { _tab = Tab.Logs;     UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabPerfBtn     = MakeTab("Perf",     () => { _tab = Tab.Perf;     UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabMemoryBtn   = MakeTab("Memory",   () => { _tab = Tab.Memory;   UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabBuildBtn    = MakeTab("Build",    () => { _tab = Tab.Build;    UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            tabs.Add(_tabTimelineBtn);
            tabs.Add(_tabHttpBtn);
            tabs.Add(_tabTrackersBtn);
            tabs.Add(_tabLogsBtn);
            tabs.Add(_tabPerfBtn);
            tabs.Add(_tabMemoryBtn);
            tabs.Add(_tabBuildBtn);
            _root.Add(tabs);
            UpdateTabChrome();

            // Filter bar (tracker tab only)
            _filterBar = MakeFilterBar();
            _root.Add(_filterBar);
            UpdateFilterBarVisibility();

            // List container
            _listContainer = new ScrollView(ScrollViewMode.Vertical);
            _listContainer.style.flexGrow = 1;
            _listContainer.style.flexShrink = 1;
            _listContainer.style.minHeight = 0;        // crucial — lets flex-grow shrink children
            _listContainer.style.paddingTop = 4;
            _root.Add(_listContainer);

            // Status bar
            _statusBar = new Label("0 events");
            _statusBar.style.color = TextMid;
            _statusBar.style.flexShrink = 0;
            _statusBar.style.paddingLeft = 12; _statusBar.style.paddingRight = 12;
            _statusBar.style.paddingTop = 6; _statusBar.style.paddingBottom = 6;
            _statusBar.style.borderTopWidth = 1;
            _statusBar.style.borderTopColor = Stroke;
            _statusBar.style.backgroundColor = Bg1;
            _statusBar.style.fontSize = 11;
            _root.Add(_statusBar);
        }

        private Label MakeTab(string label, Action onClick)
        {
            var lbl = new Label(label);
            lbl.style.color = TextMid;
            lbl.style.paddingLeft = 14; lbl.style.paddingRight = 14;
            lbl.style.paddingTop = 10; lbl.style.paddingBottom = 10;
            lbl.style.fontSize = 13;
            lbl.RegisterCallback<ClickEvent>(_ => onClick());
            return lbl;
        }

        private Label MakeButton(string text, Action onClick)
        {
            var lbl = new Label(text);
            lbl.style.color = TextHi;
            lbl.style.paddingLeft = 6; lbl.style.paddingRight = 6;
            lbl.style.paddingTop = 2; lbl.style.paddingBottom = 2;
            lbl.RegisterCallback<ClickEvent>(_ => onClick());
            return lbl;
        }

        /// <summary>
        /// Apply the "selected" look to whichever chip matches
        /// <see cref="_trackerFilter"/> and dim the rest. Each provider gets
        /// its own accent so the selection is obvious at a glance.
        /// </summary>
        private void UpdateFilterChipStyles()
        {
            foreach (var pair in _filterChips)
            {
                var key = pair.Key;
                var chip = pair.Value;
                bool active = key == _trackerFilter;
                var accent = ProviderAccent(key);

                if (active)
                {
                    chip.style.backgroundColor = accent;
                    chip.style.color = Color.white;
                    chip.style.borderTopColor = accent;
                    chip.style.borderBottomColor = accent;
                    chip.style.borderLeftColor = accent;
                    chip.style.borderRightColor = accent;
                    chip.style.unityFontStyleAndWeight = FontStyle.Bold;
                }
                else
                {
                    chip.style.backgroundColor = Bg2;
                    chip.style.color = TextMid;
                    chip.style.borderTopColor = Stroke;
                    chip.style.borderBottomColor = Stroke;
                    chip.style.borderLeftColor = Stroke;
                    chip.style.borderRightColor = Stroke;
                    chip.style.unityFontStyleAndWeight = FontStyle.Normal;
                }
            }
        }

        private static Color ProviderAccent(string key)
        {
            switch (key)
            {
                case "All":      return AccentTracker;                                        // purple
                case "Noctua":   return Warn;                                                 // amber
                case "Firebase": return new Color(0xFF / 255f, 0xA0 / 255f, 0x00 / 255f, 1f); // Firebase orange
                case "Adjust":   return Ok;                                                   // green
                case "Facebook": return AccentHttp;                                           // Facebook blue
                default:         return AccentTracker;
            }
        }

        /// <summary>Filter chips (All / Noctua / Firebase / …) and the
        /// Clear / Export / adb buttons are only relevant on the Trackers
        /// tab, so hide the whole strip on Timeline and HTTP to reduce
        /// visual noise.</summary>
        private void UpdateFilterBarVisibility()
        {
            if (_filterBar == null) return;
            _filterBar.style.display = _tab == Tab.Trackers
                ? DisplayStyle.Flex
                : DisplayStyle.None;
        }

        private void UpdateTabChrome()
        {
            void SetActive(Label btn, bool active)
            {
                btn.style.color = active ? TextHi : TextMid;
                btn.style.unityFontStyleAndWeight = active ? FontStyle.Bold : FontStyle.Normal;
                btn.style.borderBottomWidth = active ? 2 : 0;
                btn.style.borderBottomColor = active ? AccentHttp : Stroke;
            }
            SetActive(_tabTimelineBtn, _tab == Tab.Timeline);
            SetActive(_tabHttpBtn,     _tab == Tab.Http);
            SetActive(_tabTrackersBtn, _tab == Tab.Trackers);
            SetActive(_tabLogsBtn,     _tab == Tab.Logs);
            SetActive(_tabPerfBtn,     _tab == Tab.Perf);
            SetActive(_tabMemoryBtn,   _tab == Tab.Memory);
            SetActive(_tabBuildBtn,    _tab == Tab.Build);
        }

        private VisualElement MakeFilterBar()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.flexShrink = 0;
            bar.style.paddingLeft = 8; bar.style.paddingRight = 8;
            bar.style.paddingTop = 6; bar.style.paddingBottom = 6;
            bar.style.backgroundColor = Bg1;

            _filterChips.Clear();
            foreach (var key in new[] { "All", "Noctua", "Firebase", "Adjust", "Facebook" })
            {
                var chip = new Label(key);
                chip.style.paddingLeft = 10; chip.style.paddingRight = 10;
                chip.style.paddingTop = 4; chip.style.paddingBottom = 4;
                chip.style.marginRight = 4; chip.style.marginBottom = 4;
                chip.style.borderTopLeftRadius = 12; chip.style.borderTopRightRadius = 12;
                chip.style.borderBottomLeftRadius = 12; chip.style.borderBottomRightRadius = 12;
                chip.style.borderTopWidth = 1; chip.style.borderBottomWidth = 1;
                chip.style.borderLeftWidth = 1; chip.style.borderRightWidth = 1;
                chip.style.fontSize = 11;
                var captured = key;
                chip.RegisterCallback<ClickEvent>(_ =>
                {
                    _trackerFilter = captured;
                    UpdateFilterChipStyles();
                    RenderList();
                });
                _filterChips[key] = chip;
                bar.Add(chip);
            }
            UpdateFilterChipStyles();

            var clear = MakeTextButton("Clear", Err);
            clear.RegisterCallback<ClickEvent>(_ =>
            {
                _httpLog?.Clear();
                _monitor?.Clear();
                RenderList();
            });
            bar.Add(clear);

            var export = MakeTextButton("Export", AccentTracker);
            export.RegisterCallback<ClickEvent>(_ => OnExportAll());
            bar.Add(export);

#if UNITY_ANDROID || UNITY_EDITOR
            var adb = MakeTextButton("Copy adb debug cmd", AccentTracker);
            adb.RegisterCallback<ClickEvent>(_ => OnCopyAdbSnippet());
            bar.Add(adb);
#endif

            return bar;
        }

        private Label MakeTextButton(string text, Color fg)
        {
            var lbl = new Label(text);
            lbl.style.color = fg;
            lbl.style.marginLeft = 8;
            lbl.style.paddingTop = 3; lbl.style.paddingBottom = 3;
            lbl.style.fontSize = 11;
            return lbl;
        }

        // -------- toolbar actions --------

        private void OnExportAll()
        {
            var http = _httpLog?.Snapshot() ?? new List<HttpExchange>();
            var tr = _monitor?.Snapshot() ?? new List<TrackerEmission>();
            var json = InspectorExporter.ToJson(http, tr);
            GUIUtility.systemCopyBuffer = json;
            FlashStatus($"Exported to clipboard ({json.Length} chars)");
        }

        private void OnOpenDebugView()
        {
            var projectId = FirebaseProjectLookup.GetProjectId();
            if (string.IsNullOrEmpty(projectId))
            {
                FlashStatus("Firebase project id not found — is GoogleService-Info.plist / google-services.json in StreamingAssets?");
                return;
            }
            var url = $"https://console.firebase.google.com/project/{projectId}/analytics/debugview";
            Application.OpenURL(url);
            FlashStatus("Opening Firebase DebugView…");
        }

        private void OnCopyAdbSnippet()
        {
            var cmd = $"adb shell setprop log.tag.FA VERBOSE && adb shell setprop debug.firebase.analytics.app {Application.identifier}";
            GUIUtility.systemCopyBuffer = cmd;
            FlashStatus("adb command copied — run in terminal to enable Firebase verbose logs");
        }

        private void FlashStatus(string message)
        {
            if (_statusBar == null) return;
            _statusBar.text = message;
            // Next frame the normal RenderList() will overwrite it; that's
            // fine — the flash is intentionally ephemeral.
        }

        // -------- Rendering --------

        private void RenderList()
        {
            if (!_visible || _listContainer == null) return;
            _listContainer.Clear();

            int ok = 0, failing = 0, inflight = 0;

            switch (_tab)
            {
                case Tab.Timeline:
                    RenderTimeline(ref ok, ref failing, ref inflight);
                    break;
                case Tab.Http:
                    RenderHttp(ref ok, ref failing, ref inflight);
                    break;
                case Tab.Trackers:
                    RenderTrackers(ref ok, ref failing, ref inflight);
                    break;
                case Tab.Logs:
                    RenderLogs(ref ok, ref failing, ref inflight);
                    break;
                case Tab.Perf:
                    RenderPerformance(ref ok, ref failing, ref inflight);
                    break;
                case Tab.Memory:
                    RenderMemory(ref ok, ref failing, ref inflight);
                    break;
                case Tab.Build:
                    RenderBuild(ref ok, ref failing, ref inflight);
                    break;
            }
            _statusBar.text = $"{ok} ok  ·  {failing} failing  ·  {inflight} in-flight";
        }

        private void RenderTimeline(ref int ok, ref int failing, ref int inflight)
        {
#if UNITY_EDITOR
            if (!_editorBannerDismissed) _listContainer.Add(EditorOnlyBanner());
#endif

            var rows = new List<(DateTime ts, VisualElement row, bool fail, bool flight)>();
            if (_httpLog != null)
            {
                foreach (var ex in _httpLog.Snapshot())
                {
                    bool fail = ex.State == HttpExchangeState.Failed;
                    bool flight = ex.State == HttpExchangeState.Building || ex.State == HttpExchangeState.Sending || ex.State == HttpExchangeState.Receiving;
                    rows.Add((ex.StartUtc, BuildHttpRow(ex), fail, flight));
                }
            }
            if (_monitor != null)
            {
                foreach (var em in _monitor.Snapshot())
                {
                    bool fail = em.Phase == TrackerEventPhase.Failed || em.Phase == TrackerEventPhase.TimedOut;
                    bool flight = !em.Phase.IsTerminal() && em.Phase != TrackerEventPhase.Queued;
                    rows.Add((em.CreatedUtc, BuildTrackerRow(em), fail, flight));
                }
            }
            foreach (var entry in rows.OrderByDescending(r => r.ts).Take(200))
            {
                _listContainer.Add(entry.row);
                if (entry.fail) failing++; else if (entry.flight) inflight++; else ok++;
            }
        }

        private void RenderHttp(ref int ok, ref int failing, ref int inflight)
        {
            if (_httpLog == null)
            {
                _listContainer.Add(Placeholder("No requests yet. Network calls made by the game will appear here."));
                return;
            }
            var snap = _httpLog.Snapshot();
            if (snap.Count == 0)
            {
                _listContainer.Add(Placeholder("No requests yet. Network calls made by the game will appear here."));
                return;
            }
            foreach (var ex in snap.Reverse())
            {
                _listContainer.Add(BuildHttpRow(ex));
                if (ex.State == HttpExchangeState.Failed) failing++;
                else if (ex.State == HttpExchangeState.Complete) ok++;
                else inflight++;
            }
        }

        private void RenderTrackers(ref int ok, ref int failing, ref int inflight)
        {
            if (_monitor == null)
            {
                _listContainer.Add(Placeholder("Tracker monitor unavailable."));
                return;
            }

#if UNITY_EDITOR
            if (!_editorBannerDismissed) _listContainer.Add(EditorOnlyBanner());
#endif

            var filter = _trackerFilter == "All" ? null : _trackerFilter;
            var snap = _monitor.Snapshot(filter);
            if (snap.Count == 0)
            {
                _listContainer.Add(Placeholder("No tracker events yet. Play the game or send a test event to see activity here."));
                return;
            }
            foreach (var em in snap.Reverse())
            {
                _listContainer.Add(BuildTrackerRow(em));
                if (em.Phase == TrackerEventPhase.Failed || em.Phase == TrackerEventPhase.TimedOut) failing++;
                else if (em.Phase == TrackerEventPhase.Acknowledged) ok++;
                else inflight++;
            }
        }

        private VisualElement Placeholder(string text)
        {
            var lbl = new Label(text);
            lbl.style.color = TextLo;
            lbl.style.paddingLeft = 16; lbl.style.paddingRight = 16;
            lbl.style.paddingTop = 20; lbl.style.paddingBottom = 20;
            lbl.style.whiteSpace = WhiteSpace.Normal;
            return lbl;
        }

        /// <summary>
        /// Banner shown only when running in the Unity Editor. Native log
        /// tailers (Firebase / Adjust / Facebook) attach to iOS OSLogStore
        /// and Android logcat — neither exists in the Editor, so tracker
        /// rows will stay at <c>Queued</c> and never progress to
        /// <c>Emitted</c> / <c>Acknowledged</c>. Deploy to a physical
        /// device or emulator to see the full lifecycle.
        /// </summary>
        private VisualElement EditorOnlyBanner()
        {
            // Compact single-row banner with a dismiss X so it doesn't
            // dominate the Inspector when scrolled into view repeatedly.
            var box = new VisualElement();
            box.style.flexDirection = FlexDirection.Row;
            box.style.alignItems = Align.Center;
            box.style.flexShrink = 0;
            box.style.flexGrow = 0;
            box.style.backgroundColor = Bg1;
            box.style.borderLeftWidth = 3;
            box.style.borderLeftColor = Warn;
            box.style.marginLeft = 8; box.style.marginRight = 8;
            box.style.marginTop = 6; box.style.marginBottom = 6;
            box.style.paddingLeft = 10; box.style.paddingRight = 6;
            box.style.paddingTop = 6; box.style.paddingBottom = 6;
            box.style.borderTopLeftRadius = 4; box.style.borderTopRightRadius = 4;
            box.style.borderBottomLeftRadius = 4; box.style.borderBottomRightRadius = 4;

            var msg = new Label(
                "You are previewing in the Unity Editor. Firebase, Adjust and Facebook " +
                "tracking status will only show up when the game runs on an Android or iOS device.");
            msg.style.color = TextMid;
            msg.style.fontSize = 11;
            msg.style.whiteSpace = WhiteSpace.Normal;
            msg.style.flexShrink = 1;
            msg.style.flexGrow = 1;
            box.Add(msg);

            var dismiss = new Button(() =>
            {
                _editorBannerDismissed = true;
                _dirty = true;
                RenderList();
            }) { text = "X" };
            dismiss.style.color = TextMid;
            dismiss.style.backgroundColor = Bg2;
            dismiss.style.borderTopWidth = 0; dismiss.style.borderBottomWidth = 0;
            dismiss.style.borderLeftWidth = 0; dismiss.style.borderRightWidth = 0;
            dismiss.style.minWidth = 28; dismiss.style.minHeight = 22;
            dismiss.style.marginLeft = 8;
            dismiss.style.paddingLeft = 4; dismiss.style.paddingRight = 4;
            dismiss.style.paddingTop = 2; dismiss.style.paddingBottom = 2;
            dismiss.style.borderTopLeftRadius = 4; dismiss.style.borderTopRightRadius = 4;
            dismiss.style.borderBottomLeftRadius = 4; dismiss.style.borderBottomRightRadius = 4;
            dismiss.style.flexShrink = 0;
            box.Add(dismiss);

            return box;
        }

        private VisualElement BuildHttpRow(HttpExchange ex)
        {
            // Outer wrapper: column so header line + expandable detail stack vertically
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.flexShrink = 0;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Stroke;

            var headerLine = new VisualElement();
            headerLine.style.flexDirection = FlexDirection.Row;
            headerLine.style.alignItems = Align.FlexStart;
            headerLine.style.flexShrink = 0;
            headerLine.style.paddingLeft = 10; headerLine.style.paddingRight = 10;
            headerLine.style.paddingTop = 6; headerLine.style.paddingBottom = 6;
            row.Add(headerLine);

            var dot = new VisualElement();
            dot.style.width = 8; dot.style.height = 8;
            dot.style.marginRight = 8; dot.style.marginTop = 4;
            dot.style.flexShrink = 0;
            dot.style.borderTopLeftRadius = 4; dot.style.borderTopRightRadius = 4;
            dot.style.borderBottomLeftRadius = 4; dot.style.borderBottomRightRadius = 4;
            dot.style.backgroundColor = ex.State == HttpExchangeState.Failed ? Err
                                     : ex.State == HttpExchangeState.Complete ? Ok
                                     : Warn;
            headerLine.Add(dot);

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.flexDirection = FlexDirection.Column;
            var head = new Label($"↗ {ex.Method}  {Shorten(ex.Url, 50)}");
            head.style.color = TextHi;
            head.style.fontSize = 12;
            body.Add(head);

            var sub = new Label(ex.State == HttpExchangeState.Complete || ex.State == HttpExchangeState.Failed
                ? $"{ex.Status}  ·  {ex.ElapsedMs}ms  ·  {ex.StartUtc.ToLocalTime():HH:mm:ss.fff}"
                : $"{StateLabel(ex.State)}  ·  {ex.StartUtc.ToLocalTime():HH:mm:ss.fff}");
            sub.style.color = TextMid;
            sub.style.fontSize = 10;
            body.Add(sub);

            headerLine.Add(body);

            // Tap-to-expand — stacks BELOW headerLine because row is Column-direction
            bool expanded = false;
            var detail = new VisualElement();
            detail.style.display = DisplayStyle.None;
            detail.style.backgroundColor = Bg1;
            detail.style.flexShrink = 0;
            detail.style.paddingLeft = 12; detail.style.paddingRight = 12;
            detail.style.paddingTop = 6; detail.style.paddingBottom = 6;
            detail.Add(MakeMono($"=> {ex.Method} {ex.Url}\n{HeadersToString(ex.RequestHeaders)}\n\n{Shorten(ex.RequestBody, 1000)}"));
            detail.Add(MakeMono($"<= {ex.Status}\n{HeadersToString(ex.ResponseHeaders)}\n\n{Shorten(ex.ResponseBody, 1000)}"));
            if (!string.IsNullOrEmpty(ex.Error))
            {
                var err = new Label($"error: {ex.Error}");
                err.style.color = Err;
                detail.Add(err);
            }

            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.flexShrink = 0;
            toolbar.style.marginTop = 4;

            var copyCurl = MakeTextButton("Copy as cURL", AccentHttp);
            copyCurl.RegisterCallback<ClickEvent>(evt =>
            {
                GUIUtility.systemCopyBuffer = CurlExporter.ToCurl(ex);
                FlashStatus("cURL copied to clipboard");
                evt.StopPropagation();
            });
            toolbar.Add(copyCurl);

            var copyJson = MakeTextButton("Copy JSON", AccentTracker);
            copyJson.RegisterCallback<ClickEvent>(evt =>
            {
                GUIUtility.systemCopyBuffer = InspectorExporter.ToJson(
                    new List<HttpExchange> { ex }, new List<TrackerEmission>());
                FlashStatus("Exchange JSON copied");
                evt.StopPropagation();
            });
            toolbar.Add(copyJson);

            detail.Add(toolbar);
            row.Add(detail);
            row.RegisterCallback<ClickEvent>(_ =>
            {
                expanded = !expanded;
                detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            });
            return row;
        }

        private VisualElement BuildTrackerRow(TrackerEmission em)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Column;
            row.style.flexShrink = 0;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Stroke;

            var headerLine = new VisualElement();
            headerLine.style.flexDirection = FlexDirection.Row;
            headerLine.style.alignItems = Align.FlexStart;
            headerLine.style.flexShrink = 0;
            headerLine.style.paddingLeft = 10; headerLine.style.paddingRight = 10;
            headerLine.style.paddingTop = 6; headerLine.style.paddingBottom = 6;
            row.Add(headerLine);

            var dot = new VisualElement();
            dot.style.width = 8; dot.style.height = 8;
            dot.style.marginRight = 8; dot.style.marginTop = 4;
            dot.style.flexShrink = 0;
            dot.style.borderTopLeftRadius = 4; dot.style.borderTopRightRadius = 4;
            dot.style.borderBottomLeftRadius = 4; dot.style.borderBottomRightRadius = 4;
            dot.style.backgroundColor = PhaseColor(em.Phase);
            headerLine.Add(dot);

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.flexDirection = FlexDirection.Column;
            var head = new Label($"◉ {em.Provider}  ·  {em.EventName}");
            head.style.color = TextHi;
            head.style.fontSize = 12;
            body.Add(head);

            var sub = new Label($"{em.Phase}  ·  {em.CreatedUtc.ToLocalTime():HH:mm:ss.fff}");
            sub.style.color = TextMid;
            sub.style.fontSize = 10;
            body.Add(sub);
            headerLine.Add(body);

            bool expanded = false;
            var detail = new VisualElement();
            detail.style.display = DisplayStyle.None;
            detail.style.backgroundColor = Bg1;
            detail.style.flexShrink = 0;
            detail.style.paddingLeft = 12; detail.style.paddingRight = 12;
            detail.style.paddingTop = 6; detail.style.paddingBottom = 6;
            if (em.History != null)
            {
                foreach (var tr in em.History)
                {
                    var h = new Label($"{tr.Phase,-13}  {tr.AtUtc.ToLocalTime():HH:mm:ss.fff}");
                    h.style.color = TextMid;
                    h.style.fontSize = 10;
                    detail.Add(h);
                }
            }
            if (em.Payload != null && em.Payload.Count > 0)
            {
                detail.Add(MakeMono("payload:\n" + DictToMono(em.Payload)));
            }
            if (em.ExtraParams != null && em.ExtraParams.Count > 0)
            {
                detail.Add(MakeMono("extra:\n" + DictToMono(em.ExtraParams)));
            }
            if (!string.IsNullOrEmpty(em.Error))
            {
                var err = new Label($"error: {em.Error}");
                err.style.color = Err;
                detail.Add(err);
            }

            var trackerToolbar = new VisualElement();
            trackerToolbar.style.flexDirection = FlexDirection.Row;
            trackerToolbar.style.flexShrink = 0;
            trackerToolbar.style.marginTop = 4;

            var copyJson = MakeTextButton("Copy JSON", AccentTracker);
            copyJson.RegisterCallback<ClickEvent>(evt =>
            {
                GUIUtility.systemCopyBuffer = InspectorExporter.ToJson(
                    new List<HttpExchange>(), new List<TrackerEmission> { em });
                FlashStatus("Emission JSON copied");
                evt.StopPropagation();
            });
            trackerToolbar.Add(copyJson);

            var copySummary = MakeTextButton("Copy summary", AccentHttp);
            copySummary.RegisterCallback<ClickEvent>(evt =>
            {
                GUIUtility.systemCopyBuffer =
                    $"{em.Provider} · {em.EventName} · {em.Phase} · {em.CreatedUtc.ToLocalTime():HH:mm:ss.fff}";
                FlashStatus("Summary copied");
                evt.StopPropagation();
            });
            trackerToolbar.Add(copySummary);

            detail.Add(trackerToolbar);

            row.Add(detail);
            row.RegisterCallback<ClickEvent>(_ =>
            {
                expanded = !expanded;
                detail.style.display = expanded ? DisplayStyle.Flex : DisplayStyle.None;
            });
            return row;
        }

        private VisualElement MakeMono(string text)
        {
            var l = new Label(text);
            l.style.color = TextHi;
            l.style.fontSize = 10;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = 6;
            return l;
        }

        // -------- helpers --------

        private static Color PhaseColor(TrackerEventPhase p)
        {
            return p switch
            {
                TrackerEventPhase.Acknowledged => Ok,
                TrackerEventPhase.Failed => Err,
                TrackerEventPhase.TimedOut => Err,
                TrackerEventPhase.Queued => TextLo,
                _ => Warn,
            };
        }

        private static string StateLabel(HttpExchangeState s) => s switch
        {
            HttpExchangeState.Building => "building",
            HttpExchangeState.Sending => "sending",
            HttpExchangeState.Receiving => "receiving",
            HttpExchangeState.Complete => "complete",
            HttpExchangeState.Failed => "failed",
            _ => s.ToString().ToLowerInvariant(),
        };

        private static string HeadersToString(Dictionary<string, string> h)
        {
            if (h == null || h.Count == 0) return "";
            return string.Join("\n", h.Select(kv => $"{kv.Key}: {kv.Value}"));
        }

        private static string DictToMono(IReadOnlyDictionary<string, object> d)
        {
            if (d == null || d.Count == 0) return "";
            var sb = new System.Text.StringBuilder();
            foreach (var kv in d)
            {
                sb.Append(kv.Key).Append(": ").Append(kv.Value?.ToString() ?? "null").Append('\n');
            }
            return sb.ToString();
        }

        private static string Shorten(string s, int max)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Length <= max) return s;
            return s.Substring(0, max) + "…";
        }

        private static Font LoadBuiltinFont()
        {
            // Unity 2022.2+ ships `LegacyRuntime.ttf`; older versions ship `Arial.ttf`.
            // `GetBuiltinResource<Font>` returns null for unknown names, so we
            // walk the candidates silently and keep the first that resolves.
            foreach (var name in new[] { "LegacyRuntime.ttf", "Arial.ttf" })
            {
                try
                {
                    var f = Resources.GetBuiltinResource<Font>(name);
                    if (f != null) return f;
                }
                catch { /* swallow — try next */ }
            }
            return null;
        }
    }
}
