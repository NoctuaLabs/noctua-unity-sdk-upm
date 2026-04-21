#if UNITY_EDITOR && UNITY_IOS
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace com.noctuagames.sdk.Editor.Build
{
    /// <summary>
    /// Strips the legacy <c>source 'https://github.com/CocoaPods/Specs'</c>
    /// line from the generated iOS <c>Podfile</c>.
    ///
    /// Why: EDM4U + AppLovin's Unity package frequently inject that source
    /// into the Podfile. CocoaPods then clones the entire Specs repo into
    /// <c>~/.cocoapods/repos/cocoapods</c> on every <c>pod install</c>,
    /// which duplicates every podspec already served by the trunk CDN
    /// (<c>~/.cocoapods/repos/trunk</c>). Result: "Found multiple
    /// specifications for …" warnings for every dependency and a bloated
    /// local spec repo (~1 GB).
    ///
    /// We delete the source line AND <see cref="CocoaPodsConflictFixer.RemoveDuplicateCocoapodsRepo"/>
    /// deletes the cached directory — together they keep the duplicate gone.
    ///
    /// Callback order 55 — AFTER EDM4U (IOSResolver, typically order 0–50)
    /// generates Podfile but BEFORE <c>pod install</c> runs.
    /// </summary>
    public static class PodfileSourceScrubber
    {
        private const int CallbackOrder = 55;
        private const string TrunkCdn = "source 'https://cdn.cocoapods.org/'";

        [PostProcessBuild(CallbackOrder)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            var podfile = Path.Combine(pathToBuiltProject, "Podfile");
            if (!File.Exists(podfile))
            {
                // EDM4U generates Podfile in the build output dir; if missing,
                // EDM4U didn't run (custom integration?). Skip silently.
                return;
            }

            var original = File.ReadAllText(podfile);
            var patched = original;

            // 1. Remove any `source 'https://github.com/CocoaPods/Specs…'` line.
            //    The URL may or may not have a trailing slash, and may be
            //    'Specs.git'. Match all variants.
            patched = Regex.Replace(
                patched,
                @"^\s*source\s+['""]https?://github\.com/CocoaPods/Specs(?:\.git)?/?['""]\s*$\r?\n?",
                string.Empty,
                RegexOptions.Multiline);

            // 2. Ensure the trunk CDN source IS present (it's the default but
            //    some AppLovin templates omit it, and without it CocoaPods
            //    falls back to the Specs repo if anything else references it).
            if (!patched.Contains("cdn.cocoapods.org"))
            {
                patched = TrunkCdn + "\n" + patched;
            }

            if (patched == original) return;

            try
            {
                File.WriteAllText(podfile, patched);
                Debug.Log("[NoctuaSDK] PodfileSourceScrubber: removed legacy `source 'https://github.com/CocoaPods/Specs'` " +
                          "from Podfile — duplicate spec-repo warnings will no longer regenerate.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NoctuaSDK] PodfileSourceScrubber: failed to write Podfile: {e.Message}");
            }
        }
    }
}
#endif
