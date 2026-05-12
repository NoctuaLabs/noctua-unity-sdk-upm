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
    /// Post-build iOS pass that eliminates two distinct "Multiple commands produce
    /// '…/YourApp.app/Frameworks/&lt;Name&gt;.framework'" Xcode errors that arise when
    /// both AppLovin MAX and AdMob adapters for the same mediation network are installed.
    ///
    /// ── Conflict pattern A — duplicate PBXBuildFile entries ────────────────────
    /// Both adapters register the same xcframework as a <c>PBXBuildFile</c> in
    /// the <em>same</em> "Embed Pods Frameworks" / "Embed Frameworks" phase.  The
    /// deduper keeps the first entry and removes every subsequent one.
    ///
    /// ── Conflict pattern B — AppLovin embed vs CocoaPods script ────────────────
    /// AppLovin MAX's <c>EmbedDynamicLibrariesIfNeeded</c> ([PostProcessBuild 90])
    /// calls <c>PBXProject.AddFileToEmbedFrameworks</c>, adding the xcframework to
    /// the Unity-iPhone target's "Embed Frameworks" <c>PBXCopyFilesBuildPhase</c>.
    /// CocoaPods' <c>[CP] Embed Pods Frameworks</c> shell-script phase independently
    /// copies the same framework slice to
    /// <c>${TARGET_BUILD_DIR}/${FRAMEWORKS_FOLDER_PATH}/&lt;Name&gt;.framework</c>.
    /// Both produce the same output path → Xcode error.
    ///
    /// The deduper detects whether the <c>[CP] Embed Pods Frameworks</c> script
    /// already handles a given framework (by checking its <c>outputPaths</c>) and,
    /// if so, removes the redundant <c>PBXCopyFilesBuildPhase</c> entry that
    /// AppLovin added, leaving the CocoaPods script as the sole embedder.
    ///
    /// Callback order 1000 — after Unity's default iOS post-processors,
    /// after AppLovin MAX ([PostProcessBuild 90]), and after EDM4U / CocoaPods
    /// integration (which runs at order 45).
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
                // Pattern A — two PBXBuildFile entries in the same embed phase
                int count = DedupeBuildFileEntries(ref content, fw);
                if (count > 0)
                {
                    totalRemoved += count;
                    removedFrameworks.Add($"{fw} (deduped ×{count})");
                }

                // Pattern B — one AppLovin PBXCopyFilesBuildPhase entry conflicts
                //             with the [CP] Embed Pods Frameworks shell-script output
                count = RemovePodsEmbedConflicts(ref content, fw);
                if (count > 0)
                {
                    totalRemoved += count;
                    removedFrameworks.Add($"{fw} (removed AppLovin embed, kept CocoaPods script)");
                }
            }

            if (totalRemoved > 0 && content != original)
            {
                File.WriteAllText(projPath, content);
                Debug.Log($"[NoctuaSDK] EmbedFrameworksDeduper: resolved {totalRemoved} embed conflict(s) — " +
                          $"{string.Join(", ", removedFrameworks)}. " +
                          "Both MAX and AdMob adapters now coexist in this build.");
            }
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Pattern A
        // Removes every PBXBuildFile entry referencing <paramref name="frameworkName"/>
        // in any Embed phase EXCEPT the first, and strips orphan UUIDs from the
        // corresponding files = (...) list.
        // ─────────────────────────────────────────────────────────────────────────
        internal static int DedupeBuildFileEntries(ref string pbx, string frameworkName)
        {
            // PBXBuildFile lines look like:
            //   UUID /* FrameworkName in Embed Pods Frameworks */ = {isa = PBXBuildFile; fileRef = OTHER /* FrameworkName */; settings = {ATTRIBUTES = (CodeSignOnCopy, RemoveHeadersOnCopy, ); }; };
            //
            // The settings block contains NESTED braces — `[^}]*` stops at the
            // first inner `}` and never matches the full line.  `.*` lets the
            // greedy engine consume everything and backtrack to the last `};`.
            // Because this is Multiline mode, `.` never spans newlines.
            var escaped = Regex.Escape(frameworkName);
            var pattern = new Regex(
                @"^\s*(?<uuid>[0-9A-F]{24,})\s*/\*\s*" + escaped +
                @"\s+in\s+(?:Embed[^\*]*Frameworks)\s*\*/\s*=\s*\{.*\};\s*$",
                RegexOptions.Multiline);

            var matches = pattern.Matches(pbx).Cast<Match>().ToList();
            if (matches.Count <= 1) return 0;

            var duplicateUuids = matches.Skip(1).Select(m => m.Groups["uuid"].Value).ToList();

            // 1. Delete duplicate PBXBuildFile lines (reverse order preserves offsets)
            foreach (var m in matches.Skip(1).OrderByDescending(m => m.Index))
            {
                int end = m.Index + m.Length;
                if (end < pbx.Length && pbx[end] == '\n') end++;
                pbx = pbx.Remove(m.Index, end - m.Index);
            }

            // 2. Strip each duplicate UUID from every `files = (...)` list
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

        // ─────────────────────────────────────────────────────────────────────────
        // Pattern B
        // Removes the "in Embed Frameworks" PBXCopyFilesBuildPhase entry that
        // AppLovin MAX's EmbedDynamicLibrariesIfNeeded added when the
        // [CP] Embed Pods Frameworks CocoaPods script phase ALREADY outputs the
        // same framework — making the AppLovin embed entry redundant and conflicting.
        //
        // Only removes entries whose fileRef points to a Pods/ path (i.e. they
        // are CocoaPods-managed, not a first-party Unity framework).
        // ─────────────────────────────────────────────────────────────────────────
        internal static int RemovePodsEmbedConflicts(ref string pbx, string frameworkName)
        {
            // Quick guard: does [CP] Embed Pods Frameworks output this framework?
            // Output path format: "${TARGET_BUILD_DIR}/${FRAMEWORKS_FOLDER_PATH}/MolocoSDK.framework"
            if (!pbx.Contains("FRAMEWORKS_FOLDER_PATH}/" + frameworkName))
                return 0;

            // Strip extension to match both .framework and .xcframework variants
            // that AppLovin may have registered (e.g. MolocoSDK.xcframework).
            var baseName    = Path.GetFileNameWithoutExtension(frameworkName); // "MolocoSDK"
            var escapedBase = Regex.Escape(baseName);

            // Match: UUID /* MolocoSDK.xcframework in Embed Frameworks */ = { ... fileRef = UUID ... };
            // Note: "Embed Frameworks" only — not "Embed Pods Frameworks" (that's Pattern A).
            var buildFilePattern = new Regex(
                @"^\s*(?<uuid>[0-9A-F]{24,})\s*/\*\s*" + escapedBase +
                @"(?:\.xcframework|\.framework)\s+in\s+Embed\s+Frameworks\s*\*/\s*=\s*\{.*fileRef\s*=\s*(?<ref>[0-9A-F]{24,}).*\};\s*$",
                RegexOptions.Multiline);

            var matches = buildFilePattern.Matches(pbx).Cast<Match>().ToList();
            if (matches.Count == 0) return 0;

            int removed = 0;
            foreach (var m in matches.OrderByDescending(match => match.Index))
            {
                var fileRefUuid = m.Groups["ref"].Value;

                // Confirm the referenced file lives inside Pods/ (CocoaPods-managed).
                // PBXFileReference line: UUID /* MolocoSDK.xcframework */ = { ... path = Pods/... };
                var fileRefCheck = new Regex(
                    Regex.Escape(fileRefUuid) + @"[^;]{0,300}path\s*=\s*Pods/",
                    RegexOptions.Singleline);
                if (!fileRefCheck.IsMatch(pbx)) continue;

                var buildFileUuid = m.Groups["uuid"].Value;

                // Remove the PBXBuildFile line
                int end = m.Index + m.Length;
                if (end < pbx.Length && pbx[end] == '\n') end++;
                pbx = pbx.Remove(m.Index, end - m.Index);

                // Remove UUID reference from any `files = (...)` list
                var refPattern = new Regex(
                    @"^\s*" + Regex.Escape(buildFileUuid) + @"\s*/\*[^*]*\*/\s*,\s*\r?\n",
                    RegexOptions.Multiline);
                pbx = refPattern.Replace(pbx, string.Empty);

                removed++;
            }

            return removed;
        }
    }
}
#endif
