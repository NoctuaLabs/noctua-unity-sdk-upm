#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.PackageManager;
using UnityEngine;

namespace com.noctuagames.sdk.Editor.Build
{
    /// <summary>
    /// Default stabilizer — auto-heals known-broken or known-conflicting
    /// mediation adapter pins in <c>Packages/manifest.json</c> so game
    /// developers who are on the latest Noctua SDK never have to run
    /// conflict-fixer menus manually.
    ///
    /// Runs in three places:
    ///   1. Editor startup (<c>[InitializeOnLoad]</c>) — one-shot safety net
    ///      if the project was opened with a stale pin.
    ///   2. Pre-iOS-build (<c>IPreprocessBuildWithReport</c>) — belt-and-suspenders
    ///      check right before CocoaPods / Xcode integration.
    ///   3. Via menu <c>Noctua > iOS > Auto-Stabilize Now</c> — manual trigger
    ///      for diagnostics + visible log output.
    ///
    /// Deliberately conservative: only rewrites pins that match the exact
    /// broken/conflicting <c>(pkg, version)</c> pairs listed below. Any other
    /// pin is left alone so intentional version choices are preserved.
    /// </summary>
    [InitializeOnLoad]
    public static class NoctuaAdapterStabilizer
    {
        // Documented stale-pin migrations. Each key is a pkg@exact-stale-version
        // seen in the wild; value is a sentinel ("@catalog") indicating the
        // actual target version should be read from NoctuaAdapterCatalog at
        // run time — never hard-coded here — so the stabilizer follows the
        // same version the Integration Manager installs.
        //
        // If the catalog moves (AppLovin retags again), this list needs no edit.
        private static readonly Dictionary<string, string> BrokenPins = new()
        {
            // ── AppLovin MAX — iOS UPM registry unpublishes ──
            // ByteDance / Pangle iOS: the 709000000.0.0 release was unpublished
            // by AppLovin. Heal to whatever the catalog currently points at.
            { "com.applovin.mediation.adapters.bytedance.ios@709000000.0.0", "@catalog" },

            // ── AdMob — Maio vs GMA 13.x hard conflict ──
            // AdMob Maio 3.0.1 wraps native pod GoogleMobileAdsMediationMaio 2.1.6.1
            // which pins Google-Mobile-Ads-SDK ~> 12.0, conflicting with AppLovin
            // Google adapter 13.2.0.0 (GMA = 13.2.0). Heal to catalog (3.1.6 or
            // whatever the next safe bump is).
            { "com.google.ads.mobile.mediation.maio@3.0.1", "@catalog" },
        };

        // Exposed for unit tests / diagnostics only — the real force-heal set
        // is served from <see cref="NoctuaAdapterCatalog.ForceHealTargets"/>.
        private static Dictionary<string, string> BuildForceHealMap()
        {
            var map = new Dictionary<string, string>();
            foreach (var (pkg, ver) in com.noctuagames.sdk.Editor.NoctuaAdapterCatalog.ForceHealTargets())
            {
                map[pkg] = ver;
            }
            return map;
        }

        /// <summary>
        /// Resolve a BrokenPins value — either a literal version string or
        /// the "@catalog" sentinel which points at whatever
        /// <see cref="NoctuaAdapterCatalog"/> currently lists for the package.
        /// Returns null if the catalog doesn't know about the package.
        /// </summary>
        private static string ResolveTargetVersion(string pkg, string value)
        {
            if (value != "@catalog") return value;
            var catalog = BuildForceHealMap();
            return catalog.TryGetValue(pkg, out var ver) ? ver : null;
        }

        // Framework-conflict checks (NOT "both installed" — that's perfectly
        // fine for most networks). Only the pairs listed here produce one of:
        //   (a) Xcode "Multiple commands produce .../<Name>.framework"
        //       because both adapters vendor the same static framework
        //       under the same filename; or
        //   (b) CocoaPods "could not find compatible versions for pod X"
        //       because both adapters exact-pin the SAME native pod to
        //       DIFFERENT versions (impossible to resolve).
        //
        // Most MAX + AdMob adapter pairs coexist cleanly — don't expand this
        // list unless you have an Xcode or CocoaPods build log reproducing
        // the error. When a pair lands here, the only real fix is to
        // uninstall one side (Noctua SDK policy: keep MAX as primary).
        //
        // (networkName, admobPkg, maxAndroidPkg, maxIosPkg, reason)
        private static readonly (string Network, string AdmobPkg, string MaxAndroid, string MaxIos, string Reason)[] FrameworkConflictChecks =
        {
            // InMobi: AppLovinMediationInMobiAdapter vendors InMobiSDK.xcframework
            // and GoogleMobileAdsMediationInMobi depends on InMobiSDK pod, which
            // also installs the same framework — Xcode sees two copies.
            ("InMobi",
             "com.google.ads.mobile.mediation.inmobi",
             "com.applovin.mediation.adapters.inmobi.android",
             "com.applovin.mediation.adapters.inmobi.ios",
             "Xcode: two copies of InMobiSDK.framework in Embed Frameworks"),

            // Moloco: identical pattern to InMobi — both adapters embed MolocoSDK.framework.
            ("Moloco",
             "com.google.ads.mobile.mediation.moloco",
             "com.applovin.mediation.adapters.moloco.android",
             "com.applovin.mediation.adapters.moloco.ios",
             "Xcode: two copies of MolocoSDK.framework in Embed Frameworks"),

            // Pangle / ByteDance: USED TO be a hard conflict (different
            // Ads-Global pod-version pins). The catalog now pins MAX ByteDance
            // to the adapter release that wraps the same Ads-Global as AdMob
            // Pangle — both coexist. If this conflict comes back, add this
            // entry again with an updated reason.
        };

        static NoctuaAdapterStabilizer()
        {
            // Delay to avoid running during asset-database import; let Editor finish loading.
            EditorApplication.delayCall += () => RunSilent(source: "startup");
        }

        // ── Menu ──────────────────────────────────────────────────────────

        [MenuItem("Noctua/iOS/Auto-Stabilize Now", false, 302)]
        public static void MenuRunStabilizerNow()
        {
            var report = new StringBuilder();
            int changed = RunInternal(report);
            var collisions = DetectFrameworkCollisions();

            var msg = new StringBuilder();
            if (changed == 0 && collisions.Count == 0)
            {
                msg.Append("No stale pins or known adapter collisions detected.\n\nManifest is stable; post-build steps will dedupe framework clashes automatically.");
                EditorUtility.DisplayDialog("Noctua Adapter Stabilizer", msg.ToString().Trim(), "OK");
                return;
            }

            if (changed > 0)
            {
                msg.AppendLine($"Patched {changed} adapter pin(s):");
                msg.AppendLine(report.ToString());
            }

            if (collisions.Count > 0)
            {
                msg.AppendLine("⚠  Both MAX + AdMob adapters installed for mediation networks");
                msg.AppendLine("    whose adapters cannot coexist on iOS:");
                foreach (var (net, reason, _) in collisions)
                {
                    msg.AppendLine($"  • {net}");
                    msg.AppendLine($"       {reason}");
                }
                msg.AppendLine();
                msg.AppendLine("Fix options:");
                msg.AppendLine("  1. Open Noctua > Noctua Integration Manager and uninstall either");
                msg.AppendLine("     the MAX or the AdMob adapter for each listed network, or");
                msg.AppendLine("  2. Click \"Keep MAX / Remove AdMob\" below — this removes the");
                msg.AppendLine("     AdMob-side package from manifest.json (MAX is the Noctua SDK");
                msg.AppendLine("     primary mediator).");
            }

            bool remove = collisions.Count > 0 && EditorUtility.DisplayDialog(
                "Noctua Adapter Stabilizer",
                msg.ToString().Trim(),
                "Keep MAX / Remove AdMob",
                "Close");

            if (remove)
            {
                RemoveAdmobSide(collisions);
                EditorUtility.DisplayDialog(
                    "Noctua Adapter Stabilizer",
                    $"Removed {collisions.Count} AdMob-side adapter(s) from manifest.json. " +
                    "UPM is re-resolving now.",
                    "OK");
            }
        }

        private static void RemoveAdmobSide(List<(string Network, string Reason, string AdmobPkg)> collisions)
        {
            if (!TryLoadManifest(out var manifest, out var deps)) return;
            int removed = 0;
            foreach (var (net, _, admobPkg) in collisions)
            {
                if (deps.Remove(admobPkg))
                {
                    Debug.Log($"[NoctuaSDK] Stabilizer removed AdMob-side adapter for {net}: {admobPkg}");
                    removed++;
                }
            }
            if (removed > 0)
            {
                WriteManifest(manifest);
                try { Client.Resolve(); } catch { /* non-fatal */ }
            }
        }

        [MenuItem("Noctua/iOS/Auto-Stabilize Now", true)]
        public static bool MenuRunStabilizerNow_Validate() =>
            EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;

        /// <summary>
        /// User-facing "I hit the UPM error, fix it" button. Always visible
        /// (not gated on iOS build target) because the broken pin blocks
        /// package resolution on every platform, not just during iOS build.
        /// </summary>
        [MenuItem("Noctua/Fix Broken Adapter Pins", false, 310)]
        public static void MenuFixBrokenAdapterPins()
        {
            var report = new StringBuilder();
            int changed = RunInternal(report);
            if (changed == 0)
            {
                EditorUtility.DisplayDialog(
                    "Noctua Adapter Stabilizer",
                    "No broken adapter pins detected. Manifest is clean.\n\n" +
                    "If Unity's Package Manager is still showing a \"Package cannot be found\" error, " +
                    "try Assets > Reimport All, or restart the Editor.",
                    "OK");
                return;
            }
            EditorUtility.DisplayDialog(
                "Noctua Adapter Stabilizer",
                $"Healed {changed} adapter pin(s):\n\n{report}\n" +
                "UPM is re-resolving now — the previous \"Package cannot be found\" error should disappear.",
                "OK");
        }

        // ── Silent entry points (startup / pre-build) ─────────────────────

        public static void RunSilent(string source)
        {
            var report = new StringBuilder();
            int changed = RunInternal(report);
            if (changed > 0)
            {
                Debug.LogWarning($"[NoctuaSDK] Stabilizer ({source}) patched {changed} adapter pin(s):\n{report}");
            }
            // Framework collision is fixed post-build inside
            // `EmbedFrameworksDeduper`, not here — both adapters are allowed
            // to coexist; this is intentional.
        }

        /// <summary>
        /// Returns the mediation networks for which both MAX and AdMob
        /// adapters are installed AND the pair is known to produce an
        /// iOS-build failure (framework duplication or pod-version conflict).
        /// Returns the network name and a short human-readable reason string.
        /// </summary>
        public static List<(string Network, string Reason, string AdmobPkg)> DetectFrameworkCollisions()
        {
            var result = new List<(string, string, string)>();
            if (!TryLoadManifest(out var _, out var deps)) return result;

            foreach (var check in FrameworkConflictChecks)
            {
                if (!deps.ContainsKey(check.AdmobPkg)) continue;
                bool maxPresent =
                    (check.MaxIos     != null && deps.ContainsKey(check.MaxIos))  ||
                    (check.MaxAndroid != null && deps.ContainsKey(check.MaxAndroid));
                if (maxPresent) result.Add((check.Network, check.Reason, check.AdmobPkg));
            }
            return result;
        }

        /// <summary>
        /// Framework names that the post-build deduper should collapse to
        /// a single Embed Frameworks entry. Derived from
        /// <see cref="FrameworkConflictChecks"/> — one per listed network.
        /// </summary>
        internal static readonly string[] CollidingFrameworkNames =
        {
            "InMobiSDK.framework",
            "MolocoSDK.framework",
        };

        private static int RunInternal(StringBuilder report)
        {
            if (!TryLoadManifest(out var manifest, out var deps))
                return 0;

            int count = 0;

            // 1. Exact-match broken-pin migrations (covers documented registry unpublishes).
            foreach (var kv in BrokenPins)
            {
                var atIdx = kv.Key.LastIndexOf('@');
                if (atIdx <= 0) continue;
                var pkg = kv.Key.Substring(0, atIdx);
                var brokenVer = kv.Key.Substring(atIdx + 1);
                var targetVer = ResolveTargetVersion(pkg, kv.Value);
                if (string.IsNullOrEmpty(targetVer)) continue;

                if (!deps.TryGetValue(pkg, out var token)) continue;
                var current = token?.ToString();
                if (current != brokenVer) continue;

                deps[pkg] = targetVer;
                report.AppendLine($"  • {pkg}: {brokenVer} → {targetVer}  (broken-pin heal)");
                count++;
            }

            // 2. Force-heal packages that AppLovin / AdMob frequently retag on
            //    the UPM registry. For these curated packages we rewrite any
            //    pin that differs from the catalog version — not just a single
            //    known-bad version — because new stale variants appear every
            //    few months and users hit a confusing "Package cannot be
            //    found" UPM error until they manually re-install.
            foreach (var kv in BuildForceHealMap())
            {
                if (!deps.TryGetValue(kv.Key, out var token)) continue;
                var current = token?.ToString();
                if (string.IsNullOrEmpty(current) || current == kv.Value) continue;

                deps[kv.Key] = kv.Value;
                report.AppendLine($"  • {kv.Key}: {current} → {kv.Value}  (force-heal to catalog)");
                count++;
            }

            if (count > 0)
            {
                WriteManifest(manifest);
                try { Client.Resolve(); } catch { /* non-fatal */ }
            }
            return count;
        }

        // ── Manifest helpers (local copy to avoid coupling to NoctuaSDKMenu) ──

        private const string ManifestPath = "Packages/manifest.json";

        private static bool TryLoadManifest(out JObject manifest, out JObject deps)
        {
            manifest = null; deps = null;
            try
            {
                if (!File.Exists(ManifestPath)) return false;
                var text = File.ReadAllText(ManifestPath);
                manifest = JObject.Parse(text);
                deps = manifest["dependencies"] as JObject;
                return deps != null;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NoctuaSDK] Stabilizer could not read {ManifestPath}: {e.Message}");
                return false;
            }
        }

        private static void WriteManifest(JObject manifest)
        {
            try
            {
                File.WriteAllText(ManifestPath, manifest.ToString() + "\n");
                AssetDatabase.Refresh();
            }
            catch (Exception e)
            {
                Debug.LogError($"[NoctuaSDK] Stabilizer failed to write {ManifestPath}: {e.Message}");
            }
        }
    }

    /// <summary>
    /// Runs the stabilizer immediately before an iOS build starts, catching
    /// any new broken pin that arrived since Editor launch (e.g. a package
    /// update performed in the current session).
    /// </summary>
    public class NoctuaStabilizerPreBuildProcessor : IPreprocessBuildWithReport
    {
        // Order slightly after CocoaPods pre-build (0) so heal happens first.
        public int callbackOrder => -10;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;
            NoctuaAdapterStabilizer.RunSilent(source: "pre-build");
        }
    }
}
#endif
