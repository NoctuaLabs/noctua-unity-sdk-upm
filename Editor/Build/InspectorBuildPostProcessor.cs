#if UNITY_EDITOR
using System;
using System.IO;
using System.Xml.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace com.noctuagames.sdk.Editor.Build
{
    /// <summary>
    /// iOS post-build step that injects <c>-FIRDebugEnabled</c> into the
    /// Xcode scheme's LaunchAction &amp; TestAction when
    /// <c>Assets/StreamingAssets/noctuagg.json</c> has <c>sandboxEnabled: true</c>.
    ///
    /// Firebase Analytics picks up this launch argument and routes events to
    /// DebugView in real time, which the Noctua Inspector then surfaces
    /// in-device via <c>OSLogStore</c> tailing. On release/production
    /// deploys (<c>sandboxEnabled: false</c>) any previously injected flag
    /// is stripped so leftover debug state can't leak upward.
    ///
    /// Deprecated <c>-FIRAnalyticsDebugEnabled</c> is explicitly removed —
    /// newer Firebase SDKs treat it as a no-op and it can confuse reviewers.
    ///
    /// Callback order 100 — runs after the default Unity iOS post-build
    /// (order 0) and after <see cref="BuildPostProcessor"/> which handles
    /// GoogleService-Info.plist integration (order 45).
    /// </summary>
    public static class InspectorBuildPostProcessor
    {
        private const int CallbackOrder = 100;
        private const string FlagEnable = "-FIRDebugEnabled";
        private const string FlagDeprecated = "-FIRAnalyticsDebugEnabled";

        [PostProcessBuild(CallbackOrder)]
        public static void InjectFirebaseDebug(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            bool sandbox = ReadSandboxFlag();

            var schemePath = Path.Combine(
                pathToBuiltProject,
                "Unity-iPhone.xcodeproj/xcshareddata/xcschemes/Unity-iPhone.xcscheme"
            );
            if (!File.Exists(schemePath))
            {
                Debug.LogWarning($"[NoctuaBuild] xcscheme not found at {schemePath}; skipping FIRDebugEnabled injection");
                return;
            }

            try
            {
                var doc = XDocument.Load(schemePath);

                // Apply to both LaunchAction (Run) and TestAction (Tests).
                foreach (var action in doc.Descendants("LaunchAction"))
                {
                    ApplyFlag(action, sandbox);
                }
                foreach (var action in doc.Descendants("TestAction"))
                {
                    ApplyFlag(action, sandbox);
                }

                doc.Save(schemePath);
                Debug.Log($"[NoctuaBuild] Firebase DebugView {(sandbox ? "enabled" : "disabled")} (sandboxEnabled={sandbox})");
            }
            catch (Exception e)
            {
                Debug.LogError($"[NoctuaBuild] Failed to patch xcscheme for FIRDebugEnabled: {e.Message}");
            }
        }

        private static bool ReadSandboxFlag()
        {
            try
            {
                var cfgPath = Path.Combine(Application.dataPath, "StreamingAssets", "noctuagg.json");
                if (!File.Exists(cfgPath)) return false;
                var raw = File.ReadAllText(cfgPath);
                var cfg = JsonConvert.DeserializeObject<GlobalConfig>(raw);
                return cfg?.Noctua?.IsSandbox == true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NoctuaBuild] Could not read sandboxEnabled from noctuagg.json: {e.Message}");
                return false;
            }
        }

        private static void ApplyFlag(XElement action, bool sandbox)
        {
            // <CommandLineArguments>
            //   <CommandLineArgument argument="-FIRDebugEnabled" isEnabled="YES" />
            //   …
            // </CommandLineArguments>
            var args = action.Element("CommandLineArguments");
            if (args == null)
            {
                if (!sandbox) return; // nothing to strip, nothing to add
                args = new XElement("CommandLineArguments");
                action.Add(args);
            }

            // Always remove deprecated flag
            RemoveArg(args, FlagDeprecated);

            // Remove existing FIRDebugEnabled entries before re-adding
            RemoveArg(args, FlagEnable);

            if (sandbox)
            {
                args.Add(new XElement("CommandLineArgument",
                    new XAttribute("argument", FlagEnable),
                    new XAttribute("isEnabled", "YES")));
            }

            if (!args.HasElements) args.Remove();
        }

        private static void RemoveArg(XElement args, string flag)
        {
            var matches = args.Elements("CommandLineArgument");
            foreach (var el in matches)
            {
                var attr = el.Attribute("argument");
                if (attr != null && attr.Value == flag)
                {
                    el.Remove();
                }
            }
        }
    }
}
#endif
