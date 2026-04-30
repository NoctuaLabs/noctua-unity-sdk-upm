using UnityEngine;
using UnityEngine.UIElements;

namespace com.noctuagames.sdk.Inspector
{
    /// <summary>
    /// "Build" tab — read-only sanity panel that surfaces the most
    /// common "is this build configured correctly?" questions in one
    /// view. No interactions; one tap on a row copies its value to
    /// the clipboard for handoff to a server-side issue tracker.
    /// </summary>
    public partial class NoctuaInspectorController
    {
        private static string Or(string s, string fallback) =>
            string.IsNullOrEmpty(s) ? fallback : s;

        private void RenderBuild(ref int ok, ref int failing, ref int inflight)
        {
            var info = Noctua.BuildSanity();

            // Versions section
            _listContainer.Add(BuildSection("SDK / Build", new[]
            {
                ("Unity SDK version",      Or(info.UnitySdkVersion, "—"),                            false),
                ("Native plugin version",  Or(info.NativeSdkVersion, "—"),                           false),
                ("Bundle ID",              Or(info.BundleId, "—"),                                   false),
                ("App version",            Or(info.AppVersion, "—"),                                 false),
                ("Unity Editor version",   Or(info.UnityVersion, "—"),                               false),
                ("Sandbox mode",           info.IsSandbox ? "ENABLED" : "disabled",                  false),
                ("Region",                 Or(info.Region, "—"),                                     false),
            }));

            // Config section
            _listContainer.Add(BuildSection("Config", new[]
            {
                ("noctuagg.json SHA-256",  string.IsNullOrEmpty(info.ConfigChecksum) ? "—" : info.ConfigChecksum, false),
                ("Adjust app token",       string.IsNullOrEmpty(info.AdjustAppTokenMasked) ? "(unset)" : info.AdjustAppTokenMasked, false),
                ("Firebase project ID",    string.IsNullOrEmpty(info.FirebaseProjectId) ? "(unset)" : info.FirebaseProjectId,       false),
                ("GoogleServices file",    info.GoogleServicesPresent ? "present" : "MISSING",       !info.GoogleServicesPresent),
            }));

            // Platform section
            _listContainer.Add(BuildSection("Platform", new[]
            {
                ("SKAdNetworks (iOS)",     info.SkAdNetworksCount      < 0 ? "—" : info.SkAdNetworksCount.ToString(),      info.SkAdNetworksCount      == 0),
                ("Permissions (Android)",  info.AndroidPermissionsCount< 0 ? "—" : info.AndroidPermissionsCount.ToString(), false),
            }));

            // Full noctuagg.json — surfaced verbatim so devs can verify
            // every field (game ID, base URLs, tracker eventMaps, Firebase
            // / Facebook configs) without leaving the Inspector.
            _listContainer.Add(BuildRawConfigSection(info));

            // Adjust event mapping — game-event-name → callback-token,
            // with last-seen status pulled from the live tracker monitor.
            _listContainer.Add(BuildAdjustEventMapSection());

            // Experiment / feature flag overrides — sandbox-only, mutable.
            _listContainer.Add(BuildExperimentSection());

            // Action row — bug-report export.
            var actions = new VisualElement();
            actions.style.flexShrink = 0;
            actions.style.flexDirection = FlexDirection.Row;
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.paddingLeft = 12; actions.style.paddingRight = 12;
            actions.style.paddingTop = 12; actions.style.paddingBottom = 12;

            actions.Add(MakeButton(_bugReportInFlight ? "Exporting…" : "Export bug report", () =>
            {
                if (_bugReportInFlight) return;
                _bugReportInFlight = true;
                _dirty = true;
                StartCoroutine(BugReportExporter.Export(
                    _logLedger, _monitor, _httpLog, info,
                    path =>
                    {
                        _bugReportInFlight = false;
                        if (string.IsNullOrEmpty(path))
                            ShowToast("Bug-report export failed");
                        else
                            ShowToast($"Bug report → {path}");
                    }));
            }));
            _listContainer.Add(actions);

            // Lightweight scoring — flag obvious omissions in the status bar.
            if (string.IsNullOrEmpty(info.NativeSdkVersion))     failing++;
            if (string.IsNullOrEmpty(info.AdjustAppTokenMasked)) failing++;
            if (string.IsNullOrEmpty(info.FirebaseProjectId))    failing++;
            if (!info.GoogleServicesPresent)                     failing++;
            if (info.SkAdNetworksCount == 0 && Application.platform == RuntimePlatform.IPhonePlayer) failing++;
            if (failing == 0) ok++;
        }

        // Tracks whether a bug-report export is currently running so the
        // button label flips to "Exporting…" and re-tap is suppressed.
        private bool _bugReportInFlight;

        // Cache of the new-flag key/value being typed into the input
        // fields. Reset to empty after a successful submit so the next
        // render starts with blank fields.
        private string _newFlagKey = "";
        private string _newFlagVal = "";

        /// <summary>
        /// Experiment / feature flag override section. Lists every entry
        /// currently in <see cref="ExperimentManager"/> and offers inline
        /// edit (tap value to change) plus a "+ Add" form at the bottom
        /// for new keys. All edits route through <c>ExperimentManager.SetFlag</c>
        /// — same code path the SDK uses internally — so the override
        /// is identical to a real experiment assignment from the server.
        /// </summary>
        // Tracks whether the noctuagg.json section is expanded (full JSON
        // visible) or collapsed (header-only). Default collapsed because the
        // pretty-printed JSON is hundreds of lines tall and would push every
        // other section below the fold on a portrait phone.
        private bool _rawConfigExpanded;

        /// <summary>
        /// Renders the full <c>noctuagg.json</c> contents as a code-block.
        /// Tap-to-expand keeps the dense Build tab navigable; once
        /// expanded, devs see every field (game ID, base URLs, IAP /
        /// Firebase / Facebook configs, tracker eventMaps, etc.) in one
        /// pane and can copy the whole blob to clipboard for pasting
        /// into a bug report.
        /// </summary>
        private VisualElement BuildRawConfigSection(BuildSanityInfo info)
        {
            var box = new VisualElement();
            box.style.flexShrink = 0;
            box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.paddingTop = 12; box.style.paddingBottom = 4;

            // Header row: section title + length hint + chevron
            var headerRow = new VisualElement();
            headerRow.style.flexShrink = 0;
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.paddingTop = 6; headerRow.style.paddingBottom = 6;
            headerRow.RegisterCallback<ClickEvent>(_ =>
            {
                _rawConfigExpanded = !_rawConfigExpanded;
                _dirty = true;
            });

            var head = new Label("noctuagg.json");
            head.style.color = TextLo; head.style.fontSize = 12;
            head.style.flexGrow = 1;
            headerRow.Add(head);

            var hint = new Label(string.IsNullOrEmpty(info.RawConfigJson)
                ? "(unavailable — sandbox not enabled at init)"
                : $"{info.RawConfigJson.Length} chars");
            hint.style.color = TextMid; hint.style.fontSize = 11;
            hint.style.marginRight = 8;
            headerRow.Add(hint);

            if (!string.IsNullOrEmpty(info.RawConfigJson))
            {
                var chev = new Label(_rawConfigExpanded ? "▼" : "▶");
                chev.style.color = TextLo; chev.style.fontSize = 12;
                chev.style.flexShrink = 0;
                headerRow.Add(chev);
            }

            box.Add(headerRow);

            if (string.IsNullOrEmpty(info.RawConfigJson) || !_rawConfigExpanded)
            {
                return box;
            }

            // Code-block style: dark surface, rendered as one Label per
            // line. UI Toolkit's Label has a height-measurement bug when
            // a single Label holds thousands of characters — the wrapped
            // height isn't propagated correctly to the parent column,
            // and the rendered text overlaps the next section. Splitting
            // by '\n' so each Label holds a single JSON line lets each
            // one wrap (or not) independently, and the parent column
            // sums per-line heights cleanly. ~100 Labels for a typical
            // 4000-char config is well below UI Toolkit's element-count
            // ceiling.
            var codeWrap = new VisualElement();
            codeWrap.style.flexShrink = 0;
            codeWrap.style.flexDirection = FlexDirection.Column;
            codeWrap.style.overflow = Overflow.Hidden;
            codeWrap.style.backgroundColor = Bg2;
            codeWrap.style.paddingLeft = 12; codeWrap.style.paddingRight = 12;
            codeWrap.style.paddingTop = 10; codeWrap.style.paddingBottom = 10;
            codeWrap.style.marginTop = 4;
            codeWrap.style.borderTopLeftRadius = 6; codeWrap.style.borderTopRightRadius = 6;
            codeWrap.style.borderBottomLeftRadius = 6; codeWrap.style.borderBottomRightRadius = 6;

            // Cap displayed lines to keep extreme configs from blowing the
            // layout budget. 500 lines covers every realistic noctuagg.json
            // with eventMaps and per-platform configs; if a real-world file
            // exceeds this, the Copy button still gives the full content.
            const int maxLines = 500;
            var lines = info.RawConfigJson.Split('\n');
            int rendered = 0;
            for (int i = 0; i < lines.Length && rendered < maxLines; i++, rendered++)
            {
                var line = lines[i];
                var l = new Label(string.IsNullOrEmpty(line) ? " " : line);
                l.style.color = TextHi;
                l.style.fontSize = 12;
                l.style.whiteSpace = WhiteSpace.Normal;
                l.style.flexShrink = 0;
                codeWrap.Add(l);
            }
            if (rendered < lines.Length)
            {
                var more = new Label($"… ({lines.Length - rendered} more lines — use Copy to see all)");
                more.style.color = TextMid;
                more.style.fontSize = 11;
                more.style.unityFontStyleAndWeight = FontStyle.Italic;
                more.style.marginTop = 4;
                more.style.flexShrink = 0;
                codeWrap.Add(more);
            }

            box.Add(codeWrap);

            // Copy button — same affordance pattern as the Logs row.
            var copy = new Label("Copy noctuagg.json");
            copy.style.color = TextHi;
            copy.style.backgroundColor = Bg2;
            copy.style.paddingLeft = 14; copy.style.paddingRight = 14;
            copy.style.paddingTop = 8; copy.style.paddingBottom = 8;
            copy.style.marginTop = 8;
            copy.style.fontSize = 12;
            copy.style.borderTopLeftRadius = 6; copy.style.borderTopRightRadius = 6;
            copy.style.borderBottomLeftRadius = 6; copy.style.borderBottomRightRadius = 6;
            copy.style.alignSelf = Align.FlexStart;
            copy.RegisterCallback<ClickEvent>(evt =>
            {
                GUIUtility.systemCopyBuffer = info.RawConfigJson;
                ShowToast($"Copied {info.RawConfigJson.Length} chars");
                evt.StopPropagation();
            });
            box.Add(copy);

            return box;
        }

        private VisualElement BuildExperimentSection()
        {
            var box = new VisualElement();
            box.style.flexShrink = 0;
            box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.paddingTop = 12; box.style.paddingBottom = 4;

            var head = new Label("Experiments & feature flags");
            head.style.color = TextLo; head.style.fontSize = 12;
            head.style.paddingBottom = 6;
            box.Add(head);

            var snapshot = ExperimentManager.Snapshot();
            if (snapshot.Count == 0)
            {
                var muted = new Label("(no flags set)");
                muted.style.color = TextMid; muted.style.fontSize = 13;
                muted.style.paddingBottom = 6;
                box.Add(muted);
            }
            else
            {
                foreach (var kv in snapshot)
                {
                    box.Add(BuildExperimentRow(kv.Key, kv.Value?.ToString() ?? ""));
                }
            }

            // "+ Add" form. Two text fields + a submit button.
            var form = new VisualElement();
            form.style.flexShrink = 0;
            form.style.flexDirection = FlexDirection.Row;
            form.style.flexWrap = Wrap.Wrap;
            form.style.paddingTop = 6;

            var keyField = new TextField { value = _newFlagKey };
            keyField.style.minWidth = 120; keyField.style.marginRight = 4; keyField.style.marginBottom = 4;
            keyField.tooltip = "Flag key";
            keyField.RegisterValueChangedCallback(evt => _newFlagKey = evt.newValue ?? "");
            form.Add(keyField);

            var valField = new TextField { value = _newFlagVal };
            valField.style.minWidth = 120; valField.style.marginRight = 4; valField.style.marginBottom = 4;
            valField.tooltip = "Flag value";
            valField.RegisterValueChangedCallback(evt => _newFlagVal = evt.newValue ?? "");
            form.Add(valField);

            form.Add(MakeButton("+ Add", () =>
            {
                if (string.IsNullOrWhiteSpace(_newFlagKey)) return;
                ExperimentManager.SetFlag(_newFlagKey.Trim(), _newFlagVal ?? "");
                _newFlagKey = "";
                _newFlagVal = "";
                ShowToast($"Flag set");
            }));
            box.Add(form);
            return box;
        }

        /// <summary>
        /// Adjust event-map section. For every entry in the platform-
        /// active <c>eventMap</c>, displays
        ///   game_event_name  →  adjust_token  ·  last seen: PHASE @ HH:mm:ss
        /// where the "last seen" reads the most recent
        /// <see cref="TrackerEmission"/> with matching token from the
        /// tracker monitor's snapshot. Helps diagnose "I tracked X but
        /// nothing showed up in Adjust" by surfacing whether the SDK ever
        /// fired the corresponding token.
        /// </summary>
        private VisualElement BuildAdjustEventMapSection()
        {
            var box = new VisualElement();
            box.style.flexShrink = 0;
            box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.paddingTop = 12; box.style.paddingBottom = 4;

            var head = new Label("Adjust event mapping");
            head.style.color = TextLo; head.style.fontSize = 12;
            head.style.paddingBottom = 6;
            box.Add(head);

            var map = BuildSanityProvider.ResolveAdjustEventMap(Noctua.Config);
            if (map == null || map.Count == 0)
            {
                var muted = new Label("(no Adjust eventMap configured for this platform)");
                muted.style.color = TextMid; muted.style.fontSize = 13;
                muted.style.whiteSpace = WhiteSpace.Normal;
                box.Add(muted);
                return box;
            }

            // Index Adjust emissions by token for cheap lookup. The
            // adjust callback token surfaces in TrackerEmission.ExtraParams
            // under the key "adjustToken" (set by both the iOS and Android
            // log tailers once the SDK observes a successful emission).
            var lastSeen = IndexLastSeenAdjustTokens();

            foreach (var kv in map)
            {
                box.Add(BuildAdjustEventMapRow(kv.Key, kv.Value, lastSeen));
            }
            return box;
        }

        private System.Collections.Generic.Dictionary<string, TrackerEmission> IndexLastSeenAdjustTokens()
        {
            var result = new System.Collections.Generic.Dictionary<string, TrackerEmission>();
            if (_monitor == null) return result;
            foreach (var em in _monitor.Snapshot())
            {
                if (em.Provider != "Adjust" || em.ExtraParams == null) continue;
                if (!em.ExtraParams.TryGetValue("adjustToken", out var rawToken)) continue;
                var token = rawToken?.ToString();
                if (string.IsNullOrEmpty(token)) continue;
                // Snapshot is oldest-first; later writes overwrite, so the
                // dict ends with the most recent observation per token.
                result[token] = em;
            }
            return result;
        }

        private VisualElement BuildAdjustEventMapRow(
            string eventName,
            string token,
            System.Collections.Generic.Dictionary<string, TrackerEmission> lastSeen)
        {
            var row = new VisualElement();
            row.style.flexShrink = 0;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 6; row.style.paddingBottom = 6;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Stroke;

            var name = new Label(eventName);
            name.style.color = TextHi; name.style.fontSize = 13;
            name.style.flexGrow = 1; name.style.flexShrink = 1;
            row.Add(name);

            var arrow = new Label("→");
            arrow.style.color = TextLo; arrow.style.fontSize = 12;
            arrow.style.marginLeft = 6; arrow.style.marginRight = 6;
            row.Add(arrow);

            var tok = new Label(string.IsNullOrEmpty(token) ? "(empty)" : token);
            tok.style.color = string.IsNullOrEmpty(token) ? Err : TextHi;
            tok.style.fontSize = 13;
            tok.style.unityFontStyleAndWeight = FontStyle.Bold;
            tok.style.marginRight = 8;
            row.Add(tok);

            // Last-seen badge.
            string status;
            Color color;
            if (lastSeen.TryGetValue(token ?? "", out var em))
            {
                status = $"{em.Phase} @ {em.CreatedUtc.ToLocalTime():HH:mm:ss}";
                color  = em.Phase == TrackerEventPhase.Acknowledged ? Ok
                       : em.Phase == TrackerEventPhase.Failed       ? Err
                       : Warn;
            }
            else
            {
                status = "never seen";
                color  = TextLo;
            }
            var badge = new Label(status);
            badge.style.color = color;
            badge.style.fontSize = 12;
            row.Add(badge);

            return row;
        }

        private VisualElement BuildExperimentRow(string key, string currentValue)
        {
            var row = new VisualElement();
            row.style.flexShrink = 0;
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 6; row.style.paddingBottom = 6;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Stroke;

            var k = new Label(key);
            k.style.color = TextMid; k.style.fontSize = 13;
            k.style.minWidth = 140; k.style.flexShrink = 0;
            row.Add(k);

            // Inline-editable value field. Edits commit on EndOfLine
            // (Enter) and on focus-loss to avoid burning a write per
            // keystroke into the underlying dictionary.
            var v = new TextField { value = currentValue };
            v.style.flexGrow = 1; v.style.flexShrink = 1;
            v.style.fontSize = 13;
            v.RegisterCallback<BlurEvent>(_ =>
            {
                if (v.value != currentValue)
                {
                    ExperimentManager.SetFlag(key, v.value ?? "");
                    ShowToast($"Updated '{key}'");
                }
            });
            row.Add(v);

            return row;
        }

        private VisualElement BuildSection(string title, (string label, string value, bool warn)[] rows)
        {
            var box = new VisualElement();
            box.style.flexShrink = 0;
            box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.paddingTop = 12; box.style.paddingBottom = 4;

            var head = new Label(title);
            head.style.color = TextLo; head.style.fontSize = 12;
            head.style.paddingBottom = 6;
            box.Add(head);

            foreach (var (label, value, warn) in rows)
            {
                box.Add(BuildBuildRow(label, value, warn));
            }
            return box;
        }

        /// <summary>
        /// Single row: label on the left, value on the right. Tapping
        /// the row copies the value to the system clipboard with a
        /// small toast — same affordance as the Logs tab so QA muscle-
        /// memory carries over.
        /// </summary>
        private VisualElement BuildBuildRow(string label, string value, bool warn)
        {
            var row = new VisualElement();
            row.style.flexShrink = 0;
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 6; row.style.paddingBottom = 6;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Stroke;

            var l = new Label(label);
            l.style.color = TextMid; l.style.fontSize = 13;
            l.style.flexGrow = 1; l.style.flexShrink = 1;
            row.Add(l);

            var v = new Label(value);
            v.style.color = warn ? Err : TextHi;
            v.style.fontSize = 13;
            v.style.unityFontStyleAndWeight = FontStyle.Bold;
            v.style.flexShrink = 0;
            v.style.maxWidth = 220;
            v.style.whiteSpace = WhiteSpace.NoWrap;
            v.style.overflow = Overflow.Hidden;
            v.style.unityTextOverflowPosition = TextOverflowPosition.Start;
            v.style.textOverflow = TextOverflow.Ellipsis;
            row.Add(v);

            row.RegisterCallback<ClickEvent>(_ =>
            {
                GUIUtility.systemCopyBuffer = $"{label}: {value}";
                ShowToast($"Copied: {label}");
            });
            return row;
        }
    }
}
