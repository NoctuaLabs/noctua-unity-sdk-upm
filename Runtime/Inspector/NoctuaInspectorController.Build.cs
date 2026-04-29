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

            // Experiment / feature flag overrides — sandbox-only, mutable.
            _listContainer.Add(BuildExperimentSection());

            // Action row — bug-report export.
            var actions = new VisualElement();
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
        private VisualElement BuildExperimentSection()
        {
            var box = new VisualElement();
            box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.paddingTop = 12; box.style.paddingBottom = 4;

            var head = new Label("Experiments & feature flags");
            head.style.color = TextLo; head.style.fontSize = 10;
            head.style.paddingBottom = 6;
            box.Add(head);

            var snapshot = ExperimentManager.Snapshot();
            if (snapshot.Count == 0)
            {
                var muted = new Label("(no flags set)");
                muted.style.color = TextMid; muted.style.fontSize = 11;
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

        private VisualElement BuildExperimentRow(string key, string currentValue)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingTop = 3; row.style.paddingBottom = 3;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Stroke;

            var k = new Label(key);
            k.style.color = TextMid; k.style.fontSize = 11;
            k.style.minWidth = 140; k.style.flexShrink = 0;
            row.Add(k);

            // Inline-editable value field. Edits commit on EndOfLine
            // (Enter) and on focus-loss to avoid burning a write per
            // keystroke into the underlying dictionary.
            var v = new TextField { value = currentValue };
            v.style.flexGrow = 1; v.style.flexShrink = 1;
            v.style.fontSize = 11;
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
            box.style.paddingLeft = 12; box.style.paddingRight = 12;
            box.style.paddingTop = 12; box.style.paddingBottom = 4;

            var head = new Label(title);
            head.style.color = TextLo; head.style.fontSize = 10;
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
            row.style.flexDirection = FlexDirection.Row;
            row.style.paddingTop = 3; row.style.paddingBottom = 3;
            row.style.borderBottomWidth = 1;
            row.style.borderBottomColor = Stroke;

            var l = new Label(label);
            l.style.color = TextMid; l.style.fontSize = 11;
            l.style.flexGrow = 1; l.style.flexShrink = 1;
            row.Add(l);

            var v = new Label(value);
            v.style.color = warn ? Err : TextHi;
            v.style.fontSize = 11;
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
