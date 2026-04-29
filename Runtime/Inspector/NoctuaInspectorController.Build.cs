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

            // Lightweight scoring — flag obvious omissions in the status bar.
            if (string.IsNullOrEmpty(info.NativeSdkVersion))     failing++;
            if (string.IsNullOrEmpty(info.AdjustAppTokenMasked)) failing++;
            if (string.IsNullOrEmpty(info.FirebaseProjectId))    failing++;
            if (!info.GoogleServicesPresent)                     failing++;
            if (info.SkAdNetworksCount == 0 && Application.platform == RuntimePlatform.IPhonePlayer) failing++;
            if (failing == 0) ok++;
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
