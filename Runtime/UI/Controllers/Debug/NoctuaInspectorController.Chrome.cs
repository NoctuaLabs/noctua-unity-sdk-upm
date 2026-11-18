using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// Inspector overlay chrome: root layout, tab bar, filter bar, buttons and the status bar.
    /// </summary>
    public partial class NoctuaInspectorController
    {
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

            // Mobile safe-area: pad away from the notch (top), home-indicator
            // (bottom), and rotated-landscape side cutouts. ConstantPhysicalSize
            // keeps the values in pt-equivalent so this works across DPIs.
            // We sample Screen.safeArea once at Awake — rotation isn't handled
            // here because the Inspector is a one-off debug surface and
            // re-applying on every render would cost more than it's worth.
            ApplySafeAreaInsets(_root, _referenceDpi);

            // PanelSettings created via ScriptableObject.CreateInstance has no
            // default theme assigned, so text glyphs render as empty. Assign
            // Unity's built-in runtime font (LegacyRuntime.ttf on 2022.2+,
            // falls back to Arial on older versions) as the root-inherited
            // font so every descendant Label renders.
            var font = LoadBuiltinFont();
            if (font != null) _root.style.unityFont = font;

            // Outer scroll context — wraps the header / tab strip / filter
            // bar / content so they all scroll TOGETHER. Pattern adopted by
            // every modern mobile app (Twitter / GitHub / Slack): chrome
            // scrolls off the top to give content the full viewport, status
            // bar stays anchored at the bottom for at-a-glance state.
            //
            // We don't nest a vertical ScrollView inside this one — content
            // rows are added directly to _listContainer (a regular
            // VisualElement now). The horizontal tabsScroll is fine
            // because it scrolls on a different axis.
            var outerScroll = new ScrollView(ScrollViewMode.Vertical);
            outerScroll.style.flexGrow = 1;
            outerScroll.style.flexShrink = 1;
            outerScroll.style.minHeight = 0;
            outerScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
            outerScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.flexShrink = 0;
            // Mobile-friendly header — 16pt horizontal padding, 12pt vertical
            // gives the close button a 44pt+ tap target once fontSize is 14pt+.
            header.style.paddingLeft = 16; header.style.paddingRight = 16;
            header.style.paddingTop = 12; header.style.paddingBottom = 12;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = Stroke;
            header.style.backgroundColor = Bg1;

            var logo = new VisualElement();
            logo.style.width = 22; logo.style.height = 22;
            logo.style.marginRight = 10;
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
            title.style.fontSize = 16;
            title.style.flexGrow = 1;
            header.Add(title);

            // Mobile close button — 14pt label + 10/18 padding = ~44pt tap
            // target. Plain "Close" string (not glyph ×/✕) so every fallback
            // font renders it without hunting for a bundled icon font.
            var close = new Button(() => Toggle()) { text = "Close" };
            close.style.color = TextHi;
            close.style.backgroundColor = Bg2;
            close.style.borderTopWidth = 0; close.style.borderBottomWidth = 0;
            close.style.borderLeftWidth = 0; close.style.borderRightWidth = 0;
            close.style.paddingLeft = 18; close.style.paddingRight = 18;
            close.style.paddingTop = 10; close.style.paddingBottom = 10;
            close.style.marginLeft = 4; close.style.marginRight = 0;
            close.style.fontSize = 14;
            close.style.minWidth = 72;
            close.style.minHeight = 44;
            close.style.borderTopLeftRadius = 6; close.style.borderTopRightRadius = 6;
            close.style.borderBottomLeftRadius = 6; close.style.borderBottomRightRadius = 6;
            header.Add(close);
            outerScroll.Add(header);

            // Tab strip — wrapped in a horizontal ScrollView so 7 tabs work on
            // portrait phones without overflowing the screen edge. Vertical
            // scrolling is disabled to keep the strip a single row; the user
            // swipes horizontally to reach Build / Memory on narrow viewports.
            var tabsScroll = new ScrollView(ScrollViewMode.Horizontal);
            tabsScroll.style.flexShrink = 0;
            tabsScroll.style.backgroundColor = Bg1;
            tabsScroll.style.borderBottomWidth = 1;
            tabsScroll.style.borderBottomColor = Stroke;
            tabsScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            tabsScroll.verticalScrollerVisibility = ScrollerVisibility.Hidden;

            var tabs = new VisualElement();
            tabs.style.flexDirection = FlexDirection.Row;
            tabs.style.flexShrink = 0;

            _tabTimelineBtn = MakeTab("Timeline", () => { _tab = Tab.Timeline; UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabHttpBtn     = MakeTab("HTTP",     () => { _tab = Tab.Http;     UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabTrackersBtn = MakeTab("Trackers", () => { _tab = Tab.Trackers; UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabLogsBtn     = MakeTab("Logs",     () => { _tab = Tab.Logs;     UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabPerfBtn     = MakeTab("Perf",     () => { _tab = Tab.Perf;     UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabMemoryBtn   = MakeTab("Memory",   () => { _tab = Tab.Memory;   UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabBuildBtn    = MakeTab("Build",    () => { _tab = Tab.Build;    UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabInjectBtn   = MakeTab("Inject",   () => { _tab = Tab.Inject;   UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            _tabCampaignsBtn = MakeTab("Campaigns", () => { _tab = Tab.Campaigns; UpdateTabChrome(); UpdateFilterBarVisibility(); RenderList(); });
            tabs.Add(_tabTimelineBtn);
            tabs.Add(_tabHttpBtn);
            tabs.Add(_tabTrackersBtn);
            tabs.Add(_tabLogsBtn);
            tabs.Add(_tabPerfBtn);
            tabs.Add(_tabMemoryBtn);
            tabs.Add(_tabBuildBtn);
            tabs.Add(_tabInjectBtn);
            tabs.Add(_tabCampaignsBtn);
            tabsScroll.Add(tabs);
            outerScroll.Add(tabsScroll);
            UpdateTabChrome();

            // Filter bar (tracker tab only)
            _filterBar = MakeFilterBar();
            outerScroll.Add(_filterBar);
            UpdateFilterBarVisibility();

            // List container — plain VisualElement now (the outer ScrollView
            // owns the scrolling). Keeps RenderList()'s `_listContainer.Clear()`
            // / Add() calls intact; only the type changed.
            _listContainer = new VisualElement();
            _listContainer.style.flexShrink = 0;
            _listContainer.style.paddingTop = 4;
            outerScroll.Add(_listContainer);

            // Now mount the outer scroller into the root.
            _root.Add(outerScroll);

            // Status bar — pinned at the bottom (sibling of outerScroll
            // inside _root). Stays visible while content scrolls.
            _statusBar = new Label("0 events");
            _statusBar.style.color = TextMid;
            _statusBar.style.flexShrink = 0;
            _statusBar.style.paddingLeft = 16; _statusBar.style.paddingRight = 16;
            _statusBar.style.paddingTop = 10; _statusBar.style.paddingBottom = 10;
            _statusBar.style.borderTopWidth = 1;
            _statusBar.style.borderTopColor = Stroke;
            _statusBar.style.backgroundColor = Bg1;
            _statusBar.style.fontSize = 13;
            _root.Add(_statusBar);
        }

        private Label MakeTab(string label, Action onClick)
        {
            var lbl = new Label(label);
            lbl.style.color = TextMid;
            // 18/14 padding + 15pt font ≈ 44pt tap target — meets Apple HIG
            // and Material guidelines without making the strip overwhelming.
            lbl.style.paddingLeft = 18; lbl.style.paddingRight = 18;
            lbl.style.paddingTop = 14; lbl.style.paddingBottom = 14;
            lbl.style.fontSize = 15;
            // NoWrap + flexShrink:0 — UI Toolkit's default whiteSpace=Normal
            // wraps "Timeline" into "Ti me li ne" (one char per line) when
            // the parent flex container squeezes the tab to a tiny width.
            // The horizontal tabsScroll lets long strips slide off-screen
            // instead of compressing each tab.
            lbl.style.whiteSpace = WhiteSpace.NoWrap;
            lbl.style.flexShrink = 0;
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
                case TaichiFilterKey: return new Color(0x2D / 255f, 0xD4 / 255f, 0xBF / 255f, 1f); // teal/cyan
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
            SetActive(_tabInjectBtn,   _tab == Tab.Inject);
            SetActive(_tabCampaignsBtn, _tab == Tab.Campaigns);
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
            foreach (var key in new[] { "All", "Noctua", "Firebase", "Adjust", "Facebook", TaichiFilterKey })
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

            // Keyword search — matches event name, provider, error, and payload/extra params.
            // Supports `re:<pattern>` for regex; plain substring otherwise.
            var search = new TextField { value = _trackerSearch };
            StyleSearchField(search);
            search.tooltip = "Search trackers: event name / provider / payload. Prefix `re:` for regex.";
            search.RegisterValueChangedCallback(evt =>
            {
                _trackerSearch = evt.newValue ?? "";
                CompileTrackerSearch();
                RenderList();
            });
            bar.Add(search);

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
            lbl.style.marginLeft = 12;
            // Mobile-friendly: 8pt vertical + 8pt horizontal padding + 13pt
            // font ≈ 30pt+ tap target. Inline toolbar action label, sits
            // alongside the row's tap-to-expand handler — the row itself
            // is the primary affordance, the toolbar is secondary.
            lbl.style.paddingLeft = 8; lbl.style.paddingRight = 8;
            lbl.style.paddingTop = 8; lbl.style.paddingBottom = 8;
            lbl.style.fontSize = 13;
            return lbl;
        }

        /// <summary>
        /// Make a <see cref="TextField"/> visible in this themeless panel. PanelSettings created via
        /// <c>CreateInstance</c> has no theme stylesheet, so UI Toolkit never sizes/paints the inner
        /// <c>unity-text-input</c> — a raw TextField renders collapsed/invisible. We style the field
        /// and its inner input explicitly (background, border, text colour, font) so it shows.
        /// </summary>
        private void StyleSearchField(TextField field)
        {
            field.style.height = 26;
            field.style.minWidth = 150;
            field.style.marginLeft = 4; field.style.marginRight = 4;
            field.style.marginTop = 2; field.style.marginBottom = 2;
            field.style.backgroundColor = Bg2;
            field.style.color = TextHi;
            field.style.fontSize = 12;
            field.style.borderTopWidth = 1; field.style.borderBottomWidth = 1;
            field.style.borderLeftWidth = 1; field.style.borderRightWidth = 1;
            field.style.borderTopColor = Stroke; field.style.borderBottomColor = Stroke;
            field.style.borderLeftColor = Stroke; field.style.borderRightColor = Stroke;
            field.style.borderTopLeftRadius = 4; field.style.borderTopRightRadius = 4;
            field.style.borderBottomLeftRadius = 4; field.style.borderBottomRightRadius = 4;

            // The inner text-input child needs explicit styling + font in a themeless panel.
            var input = field.Q("unity-text-input");
            if (input != null)
            {
                input.style.backgroundColor = Bg2;
                input.style.color = TextHi;
                input.style.paddingLeft = 6; input.style.paddingRight = 6;
                input.style.paddingTop = 2; input.style.paddingBottom = 2;
                input.style.borderTopWidth = 0; input.style.borderBottomWidth = 0;
                input.style.borderLeftWidth = 0; input.style.borderRightWidth = 0;
                var f = LoadBuiltinFont();
                if (f != null) input.style.unityFont = f;
            }
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
    }
}
