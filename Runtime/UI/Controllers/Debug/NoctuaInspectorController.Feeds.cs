using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// Inspector feed tabs: Timeline, HTTP and Trackers lists, their rows and search.
    /// </summary>
    public partial class NoctuaInspectorController
    {
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
                case Tab.Inject:
                    RenderInject(ref ok, ref failing, ref inflight);
                    break;
                case Tab.Campaigns:
                    RenderCampaigns(ref ok, ref failing, ref inflight);
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
            // Network conditioner controls — always visible on the HTTP tab.
            _listContainer.Add(BuildNetworkConditionerStrip());

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

            // "Taichi" is not a provider — taichi_* events are emitted under the Noctua provider.
            // Filter by event-name prefix instead of provider equality. The key compare is
            // case-insensitive so it can't silently break if the chip key casing ever changes.
            IReadOnlyList<TrackerEmission> snap;
            if (string.Equals(_trackerFilter, TaichiFilterKey, StringComparison.OrdinalIgnoreCase))
            {
                // Snapshot() never returns null (it ToList()s), but guard the entry and EventName
                // so a stray null can't throw out of the per-frame render tick.
                snap = (_monitor.Snapshot(null) ?? (IReadOnlyList<TrackerEmission>)System.Array.Empty<TrackerEmission>())
                    .Where(e => e != null && e.EventName != null &&
                                e.EventName.StartsWith(TaichiEventPrefix, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            else
            {
                var filter = _trackerFilter == "All" ? null : _trackerFilter;
                snap = _monitor.Snapshot(filter) ?? (IReadOnlyList<TrackerEmission>)System.Array.Empty<TrackerEmission>();
            }

            // Apply the keyword search on top of the provider/Taichi filter.
            if (!string.IsNullOrEmpty(_trackerSearch))
            {
                snap = snap.Where(PassesTrackerSearch).ToList();
            }

            if (snap.Count == 0)
            {
                _listContainer.Add(Placeholder(
                    string.IsNullOrEmpty(_trackerSearch)
                        ? "No tracker events yet. Play the game or send a test event to see activity here."
                        : $"No tracker events match \"{_trackerSearch}\". Adjust the filter or search above."));
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

        /// <summary>
        /// Compiles <see cref="_trackerSearch"/> into <see cref="_trackerSearchRegex"/> when it uses
        /// the <c>re:</c> prefix. Invalid patterns fall back to plain substring matching.
        /// </summary>
        private void CompileTrackerSearch()
        {
            const string prefix = "re:";
            if (!string.IsNullOrEmpty(_trackerSearch) &&
                _trackerSearch.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var pattern = _trackerSearch.Substring(prefix.Length);
                try
                {
                    _trackerSearchRegex = string.IsNullOrEmpty(pattern)
                        ? null
                        : new System.Text.RegularExpressions.Regex(
                            pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                catch
                {
                    _trackerSearchRegex = null; // invalid regex → substring fallback on the raw text
                }
            }
            else
            {
                _trackerSearchRegex = null;
            }
        }

        /// <summary>
        /// True when the emission matches the current keyword search. Matches against provider,
        /// event name, error, and the flattened payload / extra-param key=value pairs.
        /// </summary>
        private bool PassesTrackerSearch(TrackerEmission em)
        {
            if (em == null) return false;
            var haystack = BuildTrackerSearchText(em);
            if (_trackerSearchRegex != null)
            {
                return _trackerSearchRegex.IsMatch(haystack);
            }
            return haystack.IndexOf(_trackerSearch, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildTrackerSearchText(TrackerEmission em)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(em.Provider).Append(' ')
              .Append(em.EventName).Append(' ')
              .Append(em.Error).Append(' ');
            AppendDictForSearch(sb, em.Payload);
            AppendDictForSearch(sb, em.ExtraParams);
            return sb.ToString();
        }

        private static void AppendDictForSearch(System.Text.StringBuilder sb, IReadOnlyDictionary<string, object> dict)
        {
            if (dict == null) return;
            foreach (var kv in dict)
            {
                sb.Append(kv.Key).Append('=').Append(kv.Value).Append(' ');
            }
        }

        /// <summary>
        /// Network condition simulator strip — sits at the top of the HTTP
        /// tab. Lets QA force offline / slow-3G / packet-loss modes without
        /// real network manipulation. Pure Unity-side fault injection in
        /// `NetworkConditioner`; production builds never hit this code.
        /// </summary>
        private VisualElement BuildNetworkConditionerStrip()
        {
            var bar = new VisualElement();
            bar.style.flexDirection = FlexDirection.Row;
            bar.style.flexWrap = Wrap.Wrap;
            bar.style.flexShrink = 0;
            bar.style.paddingLeft = 8; bar.style.paddingRight = 8;
            bar.style.paddingTop = 6; bar.style.paddingBottom = 6;
            bar.style.backgroundColor = Bg1;
            bar.style.borderBottomWidth = 1;
            bar.style.borderBottomColor = Stroke;

            var label = new Label("Net:");
            label.style.color = TextMid; label.style.fontSize = 10;
            label.style.marginRight = 8;
            label.style.alignSelf = Align.Center;
            bar.Add(label);

            void AddModeChip(string text, NetworkMode mode)
            {
                var chip = new Label(text);
                bool active = NetworkConditioner.Mode == mode;
                chip.style.paddingLeft = 10; chip.style.paddingRight = 10;
                chip.style.paddingTop = 4; chip.style.paddingBottom = 4;
                chip.style.marginRight = 4; chip.style.marginBottom = 4;
                chip.style.borderTopLeftRadius = 12; chip.style.borderTopRightRadius = 12;
                chip.style.borderBottomLeftRadius = 12; chip.style.borderBottomRightRadius = 12;
                chip.style.fontSize = 11;
                // Off = green, fault modes = red so the active fault is loud.
                Color color = !active ? Bg2 : (mode == NetworkMode.Normal ? Ok : Err);
                chip.style.backgroundColor = color;
                chip.style.color = active ? Color.white : TextMid;
                chip.RegisterCallback<ClickEvent>(_ =>
                {
                    NetworkConditioner.Mode = mode;
                    _dirty = true;
                });
                bar.Add(chip);
            }

            AddModeChip("Normal",  NetworkMode.Normal);
            AddModeChip("Slow 3G", NetworkMode.Slow3G);
            AddModeChip("Offline", NetworkMode.Offline);
            AddModeChip($"Drop {NetworkConditioner.PacketLossPercent}%", NetworkMode.PacketLoss);

            return bar;
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
            // Mobile-friendly: 16/10 padding gives a ≥44pt tap target with
            // the bumped 14pt header font below.
            headerLine.style.paddingLeft = 16; headerLine.style.paddingRight = 16;
            headerLine.style.paddingTop = 10; headerLine.style.paddingBottom = 10;
            row.Add(headerLine);

            var dot = new VisualElement();
            dot.style.width = 10; dot.style.height = 10;
            dot.style.marginRight = 10; dot.style.marginTop = 5;
            dot.style.flexShrink = 0;
            dot.style.borderTopLeftRadius = 5; dot.style.borderTopRightRadius = 5;
            dot.style.borderBottomLeftRadius = 5; dot.style.borderBottomRightRadius = 5;
            dot.style.backgroundColor = ex.State == HttpExchangeState.Failed ? Err
                                     : ex.State == HttpExchangeState.Complete ? Ok
                                     : Warn;
            headerLine.Add(dot);

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.flexDirection = FlexDirection.Column;
            var head = new Label($"↗ {ex.Method}  {Shorten(ex.Url, 50)}");
            head.style.color = TextHi;
            head.style.fontSize = 14;
            body.Add(head);

            var sub = new Label(ex.State == HttpExchangeState.Complete || ex.State == HttpExchangeState.Failed
                ? $"{ex.Status}  ·  {ex.ElapsedMs}ms  ·  {ex.StartUtc.ToLocalTime():HH:mm:ss.fff}"
                : $"{StateLabel(ex.State)}  ·  {ex.StartUtc.ToLocalTime():HH:mm:ss.fff}");
            sub.style.color = TextMid;
            sub.style.fontSize = 12;
            body.Add(sub);

            headerLine.Add(body);

            // Tap-to-expand — stacks BELOW headerLine because row is Column-direction
            bool expanded = false;
            var detail = new VisualElement();
            detail.style.display = DisplayStyle.None;
            detail.style.backgroundColor = Bg1;
            detail.style.flexShrink = 0;
            detail.style.paddingLeft = 16; detail.style.paddingRight = 16;
            detail.style.paddingTop = 10; detail.style.paddingBottom = 10;
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
            // Mobile-friendly: 16/10 padding gives a ≥44pt tap target with
            // the bumped 14pt header font below.
            headerLine.style.paddingLeft = 16; headerLine.style.paddingRight = 16;
            headerLine.style.paddingTop = 10; headerLine.style.paddingBottom = 10;
            row.Add(headerLine);

            var dot = new VisualElement();
            dot.style.width = 10; dot.style.height = 10;
            dot.style.marginRight = 10; dot.style.marginTop = 5;
            dot.style.flexShrink = 0;
            dot.style.borderTopLeftRadius = 5; dot.style.borderTopRightRadius = 5;
            dot.style.borderBottomLeftRadius = 5; dot.style.borderBottomRightRadius = 5;
            dot.style.backgroundColor = PhaseColor(em.Phase);
            headerLine.Add(dot);

            var body = new VisualElement();
            body.style.flexGrow = 1;
            body.style.flexDirection = FlexDirection.Column;
            var head = new Label($"◉ {em.Provider}  ·  {em.EventName}");
            head.style.color = TextHi;
            head.style.fontSize = 14;
            body.Add(head);

            var sub = new Label($"{em.Phase}  ·  {em.CreatedUtc.ToLocalTime():HH:mm:ss.fff}");
            sub.style.color = TextMid;
            sub.style.fontSize = 12;
            body.Add(sub);
            headerLine.Add(body);

            bool expanded = false;
            var detail = new VisualElement();
            detail.style.display = DisplayStyle.None;
            detail.style.backgroundColor = Bg1;
            detail.style.flexShrink = 0;
            detail.style.paddingLeft = 16; detail.style.paddingRight = 16;
            detail.style.paddingTop = 10; detail.style.paddingBottom = 10;
            if (em.History != null)
            {
                foreach (var tr in em.History)
                {
                    var h = new Label($"{tr.Phase,-13}  {tr.AtUtc.ToLocalTime():HH:mm:ss.fff}");
                    h.style.color = TextMid;
                    h.style.fontSize = 12;
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

            // Re-fire — replays the exact event through the same pipeline.
            // Only visible when the composition root injected a handler;
            // otherwise the Inspector has no path to EventSender.
            if (EventReplayHandler != null)
            {
                var refire = MakeTextButton("Re-fire", AccentTracker);
                refire.RegisterCallback<ClickEvent>(evt =>
                {
                    try
                    {
                        EventReplayHandler.Invoke(em.EventName, em.Payload);
                        FlashStatus($"Re-fired '{em.EventName}'");
                    }
                    catch (Exception e)
                    {
                        FlashStatus($"Re-fire failed: {e.Message}");
                    }
                    evt.StopPropagation();
                });
                trackerToolbar.Add(refire);
            }

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
            // 12pt minimum for body-text monospace dumps on phone DPI.
            // Lower than the inline label sizes (13-14pt) because mono
            // dumps benefit from tighter letterspacing — but never below
            // iOS HIG's 11pt readability floor.
            l.style.fontSize = 12;
            l.style.whiteSpace = WhiteSpace.Normal;
            l.style.marginBottom = 8;
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
    }
}
