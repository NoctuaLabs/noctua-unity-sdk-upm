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
    /// and binds UI to the HTTP log and tracker monitor it is given.
    ///
    /// Only spawned when the SDK runs in sandbox mode — never referenced
    /// in production. All UI built programmatically (no UXML/USS resources
    /// required), so the SDK ships as-is without extra asset files.
    /// </summary>
    public partial class NoctuaInspectorController : MonoBehaviour
    {
        // ----- theme tokens (kept centrally; matches plan's USS palette) -----
        // Fully opaque (alpha 1.0) — alpha 0.97 was letting the game's UI
        // bleed through the Inspector overlay, especially noticeable on
        // emulator + bright backgrounds where the 3% transparency was
        // clearly visible.
        private static readonly Color Bg0 = new(0x0F / 255f, 0x11 / 255f, 0x15 / 255f, 1f);
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

        private enum Tab { Timeline, Http, Trackers, Logs, Perf, Memory, Build, Inject, Campaigns }

        private UIDocument _doc;
        private PanelSettings _panelSettings;

        // UI density knob for the whole overlay. Under ConstantPhysicalSize the effective
        // px-per-point scale is Screen.dpi / referenceDpi, so a HIGHER referenceDpi renders the
        // entire inspector SMALLER (every font/padding shrinks uniformly). Raised from the old
        // 64/96 to make the overlay more compact. To resize globally, change only these two
        // numbers — do not hand-tune individual fontSize values.
        private const int ReferenceDpiEditor = 96;   // was 64 — ~33% smaller in the Editor/Game view
        private const int ReferenceDpiDevice = 128;  // was 96 — ~25% smaller on device
        private int _referenceDpi;
        private InspectorTrigger _trigger;
        private HttpInspectorLog _httpLog;
        private TrackerDebugMonitor _monitor;
        private LogInspectorLedger _logLedger;
        // Read by the Build and Campaigns tabs; supplied by the composition root.
        private Func<BuildSanityInfo> _buildSanity;
        private Func<GlobalConfig> _config;
        private Func<LiveOpsCampaign.NoctuaLiveOpsCampaign> _campaign;
        private UnityLogStream _unityLogStream;
        private PerformanceMonitor _perfMonitor;
        private MemoryMonitor _memMonitor;
        private bool _visible;
        private bool _dirty;
        private bool _editorBannerDismissed;
        private Tab _tab = Tab.Timeline;
        private string _trackerFilter = "All";
        private string _trackerSearch = "";                 // keyword filter for the Trackers tab
        private System.Text.RegularExpressions.Regex _trackerSearchRegex; // non-null when `re:` prefix is set

        // Single source of truth for the "Taichi" pseudo-filter. Unlike the other chips it is NOT a
        // provider — it matches events whose name starts with TaichiEventPrefix. Keeping these as
        // constants (used by the chip, ProviderAccent, and RenderTrackers) avoids the magic string
        // drifting out of sync across the three call sites.
        private const string TaichiFilterKey  = "Taichi";
        private const string TaichiEventPrefix = "taichi_";

        private VisualElement _root;
        private VisualElement _listContainer;
        private VisualElement _filterBar;
        private readonly Dictionary<string, Label> _filterChips = new();
        private Label _tabTimelineBtn, _tabHttpBtn, _tabTrackersBtn, _tabLogsBtn, _tabPerfBtn, _tabMemoryBtn, _tabBuildBtn, _tabInjectBtn, _tabCampaignsBtn;
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
        /// Optional hook for "Trackers → Re-fire" event replay.
        /// Composition root injects this with a closure over
        /// <c>_eventSender.Send</c> so the Inspector can replay an event
        /// without taking a hard reference on the View layer or
        /// <c>EventSender</c> directly. Null = replay button is hidden.
        /// </summary>
        public System.Action<string, System.Collections.Generic.IReadOnlyDictionary<string, object>> EventReplayHandler;

        /// <summary>
        /// Spawns the overlay and wires its feeds. The Performance and Memory monitors are
        /// <see cref="MonoBehaviour"/>s added to the Inspector host GameObject so they share its
        /// lifecycle. Everything the tabs read comes in through the arguments (the composition
        /// root passes them), so this class never touches the <c>Noctua</c> static facade.
        /// </summary>
        public static NoctuaInspectorController Install(
            HttpInspectorLog httpLog,
            TrackerDebugMonitor monitor,
            LogInspectorLedger logLedger,
            Func<BuildSanityInfo> buildSanity = null,
            Func<GlobalConfig> config = null,
            Func<LiveOpsCampaign.NoctuaLiveOpsCampaign> campaign = null)
        {
            var go = new GameObject("__NoctuaInspector");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
            var ctrl = go.AddComponent<NoctuaInspectorController>();
            ctrl._httpLog = httpLog;
            ctrl._monitor = monitor;
            ctrl._logLedger = logLedger;
            ctrl._buildSanity = buildSanity;
            ctrl._config = config;
            ctrl._campaign = campaign;
            ctrl._perfMonitor = go.AddComponent<PerformanceMonitor>();
            ctrl._perfMonitor.enabled = false; // default OFF — user starts it from the Perf tab
            ctrl._memMonitor = go.AddComponent<MemoryMonitor>();
            ctrl._trigger = go.AddComponent<InspectorTrigger>();
            ctrl._trigger.OnTrigger += ctrl.Toggle;
            // Mark dirty on every new entry so RenderList refreshes on
            // change instead of every frame. Skip while the Inject tab is
            // open — it shows none of these feeds, and a rebuild there would
            // steal focus from a text field the user is typing into.
            if (httpLog != null) httpLog.OnExchange += _ => { if (ctrl._tab != Tab.Inject) ctrl._dirty = true; };
            if (monitor != null) monitor.OnEmission += _ => { if (ctrl._tab != Tab.Inject) ctrl._dirty = true; };
            if (logLedger != null) logLedger.OnEntry += _ => { if (ctrl._tab != Tab.Inject) ctrl._dirty = true; };
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
            // Refresh the Inject tab when the game registers/unregisters a
            // debug action after the overlay has spawned.
            InspectorActionRegistry.Changed += ctrl.OnInspectorActionsChanged;
            return ctrl;
        }

        private void Awake()
        {
            _panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
            _panelSettings.sortingOrder = 32767;
            _panelSettings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
            // ConstantPhysicalSize divides Screen.dpi by referenceDpi to pick the px-per-pt scale.
            // HIGHER referenceDpi => smaller overlay. The Editor/Game view reports ~96-110dpi (so it
            // renders near 1:1), hence a separate, slightly lower reference there. See the
            // ReferenceDpi* constants to resize the whole inspector in one place.
            _referenceDpi = Application.isEditor ? ReferenceDpiEditor : ReferenceDpiDevice;
            _panelSettings.referenceDpi = _referenceDpi;
            _panelSettings.fallbackDpi  = _referenceDpi;

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
                // Reset _dirty BEFORE rendering: if RenderList throws, _dirty stays false so the
                // error can't re-throw every frame. Catch so a render bug never breaks the overlay.
                _dirty = false;
                try
                {
                    RenderList();
                }
                catch (Exception e)
                {
                    _listContainer?.Clear();
                    _listContainer?.Add(Placeholder($"Inspector render error: {e.Message}"));
                }
            }
        }

        private void OnDestroy()
        {
            if (_trigger != null) _trigger.OnTrigger -= Toggle;
            InspectorActionRegistry.Changed -= OnInspectorActionsChanged;
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

        /// <summary>
        /// Pads <paramref name="root"/> by <see cref="Screen.safeArea"/> so the
        /// header doesn't collide with the iOS notch / Dynamic Island and the
        /// status bar doesn't sit under the home-indicator bar. <c>safeArea</c>
        /// is in screen pixels; we convert to design pixels via the panel's
        /// reference DPI (96) to keep parity with <c>ConstantPhysicalSize</c>.
        ///
        /// Sampled once at <c>Awake</c>; rotation isn't re-applied because the
        /// Inspector is a one-off debug surface and re-laying-out the root on
        /// orientation change would cost more frames than it's worth. Devs
        /// expecting landscape/portrait flips should re-open the Inspector.
        /// </summary>
        private static void ApplySafeAreaInsets(VisualElement root, int referenceDpi)
        {
            try
            {
                var safe = Screen.safeArea;
                int screenW = Screen.width;
                int screenH = Screen.height;
                if (screenW <= 0 || screenH <= 0) return;

                // Distance from each screen edge to the safe-area rectangle,
                // in screen pixels. Unity's coordinate origin is bottom-left,
                // hence the inverted top calc.
                float leftPx   = safe.xMin;
                float rightPx  = screenW - safe.xMax;
                float bottomPx = safe.yMin;
                float topPx    = screenH - safe.yMax;

                // Convert screen px → design pt for ConstantPhysicalSize. Must use the SAME
                // referenceDpi the panel was configured with (_referenceDpi), or the notch padding
                // is over/under-sized. If Screen.dpi reports 0 (some editors / desktop builds) we
                // leave the values in pixels — invisible because the desktop preview has no notch.
                float dpi = Screen.dpi;
                float refDpi = referenceDpi > 0 ? referenceDpi : 96;
                float scale = dpi > 0f ? refDpi / dpi : 1f;

                root.style.paddingLeft   = leftPx   * scale;
                root.style.paddingRight  = rightPx  * scale;
                root.style.paddingTop    = topPx    * scale;
                root.style.paddingBottom = bottomPx * scale;
            }
            catch
            {
                // Screen.safeArea is well-supported on all real devices. Any
                // throw here is an Editor / standalone-build oddity; skip
                // padding rather than break the overlay.
            }
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
