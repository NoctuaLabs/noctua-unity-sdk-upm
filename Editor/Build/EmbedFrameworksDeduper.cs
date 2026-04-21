#if UNITY_EDITOR && UNITY_IOS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace com.noctuagames.sdk.Editor.Build
{
    /// <summary>
    /// Post-build iOS pass that removes duplicate <c>Embed Frameworks</c>
    /// entries for a curated list of vendored native SDKs (currently
    /// <c>InMobiSDK.framework</c> and <c>MolocoSDK.framework</c>).
    ///
    /// These native SDKs are bundled as <c>vendored_frameworks</c> inside
    /// BOTH the AppLovin MAX adapter and the AdMob adapter for the same
    /// network. CocoaPods installs the pods fine, but Xcode's "Embed Pods
    /// Frameworks" phase ends up with two <c>PBXBuildFile</c> entries
    /// referencing the same filename, which triggers:
    ///
    ///   "Multiple commands produce '…/YourApp.app/Frameworks/&lt;Name&gt;.framework'"
    ///
    /// The deduper parses <c>project.pbxproj</c>, keeps the first
    /// <c>PBXBuildFile</c> per framework name in the Embed phase, and drops
    /// subsequent duplicates (both the entry itself and its UUID in the
    /// phase's <c>files = (...)</c> list). Result: both AppLovin MAX and
    /// AdMob mediation adapters coexist at runtime without breaking Xcode.
    ///
    /// Callback order 1000 — after Unity's default iOS post-processors and
    /// after EDM4U / CocoaPods integration (which runs at order ≤ 700).
    /// </summary>
    public static class EmbedFrameworksDeduper
    {
        private const int CallbackOrder = 1000;

        [PostProcessBuild(CallbackOrder)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS) return;

            var projPath = Path.Combine(pathToBuiltProject, "Unity-iPhone.xcodeproj", "project.pbxproj");
            if (!File.Exists(projPath))
            {
                Debug.LogWarning($"[NoctuaSDK] EmbedFrameworksDeduper: project.pbxproj not found at {projPath}");
                return;
            }

            var content = File.ReadAllText(projPath);
            var original = content;
            int totalRemoved = 0;
            var removedFrameworks = new List<string>();

            foreach (var fw in NoctuaAdapterStabilizer.CollidingFrameworkNames)
            {
                int count = DedupeBuildFileEntries(ref content, fw);
                if (count > 0)
                {
                    totalRemoved += count;
                    removedFrameworks.Add($"{fw} ×{count}");
                }
            }

            if (totalRemoved > 0 && content != original)
            {
                File.WriteAllText(projPath, content);
                Debug.Log($"[NoctuaSDK] EmbedFrameworksDeduper: removed {totalRemoved} duplicate Embed Frameworks entries — {string.Join(", ", removedFrameworks)}. " +
                          "Both MAX and AdMob adapters now coexist in this build.");
            }
        }

        /// <summary>
        /// Removes every PBXBuildFile entry referencing
        /// <paramref name="frameworkName"/> in the Embed phase EXCEPT the
        /// first, and strips the orphan UUIDs from any <c>files = (...)</c>
        /// list that referenced them.
        /// </summary>
        internal static int DedupeBuildFileEntries(ref string pbx, string frameworkName)
        {
            // PBXBuildFile lines for the embed phase look like:
            //   UUID /* FrameworkName in Embed Pods Frameworks */ = {isa = PBXBuildFile; fileRef = OTHER /* FrameworkName */; settings = {...}; };
            // We match any "in <Embed …> Frameworks" variant to be robust across CocoaPods
            // versions (Embed Pods Frameworks, Embed Frameworks, etc.).
            var escaped = Regex.Escape(frameworkName);
            var pattern = new Regex(
                @"^\s*(?<uuid>[0-9A-F]{24,})\s*/\*\s*" + escaped +
                @"\s+in\s+(?:Embed[^\*]*Frameworks)\s*\*/\s*=\s*\{[^}]*\};\s*$",
                RegexOptions.Multiline);

            var matches = pattern.Matches(pbx).Cast<Match>().ToList();
            if (matches.Count <= 1) return 0;

            // Keep the first UUID; every later UUID is a duplicate we'll strip.
            var duplicateUuids = matches.Skip(1).Select(m => m.Groups["uuid"].Value).ToList();

            // 1. Delete the duplicate PBXBuildFile lines (walk in reverse so
            //    earlier offsets stay valid).
            foreach (var m in matches.Skip(1).OrderByDescending(m => m.Index))
            {
                // Trim trailing newline with the line for cleanliness
                int end = m.Index + m.Length;
                if (end < pbx.Length && pbx[end] == '\n') end++;
                pbx = pbx.Remove(m.Index, end - m.Index);
            }

            // 2. Strip each duplicate UUID from every `files = (...)` list it
            //    appears in (Embed phase files list). Match the entire line:
            //      UUID /* FrameworkName in Embed Pods Frameworks */,
            foreach (var uuid in duplicateUuids)
            {
                var refPattern = new Regex(
                    @"^\s*" + Regex.Escape(uuid) + @"\s*/\*\s*" + escaped +
                    @"\s+in\s+(?:Embed[^\*]*Frameworks)\s*\*/\s*,\s*\r?\n",
                    RegexOptions.Multiline);
                pbx = refPattern.Replace(pbx, string.Empty);
            }

            return duplicateUuids.Count;
        }
    }
}
#endif
