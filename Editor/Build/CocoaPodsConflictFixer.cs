#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Detects and repairs CocoaPods conflicts that arise from common iOS ad SDK combinations.
///
/// Conflict 1 — GMA SDK version mismatch:
///   com.google.ads.mobile pins Google-Mobile-Ads-SDK to a specific minor constraint (e.g. ~> 12.11.0)
///   while AppLovin Google/AdManager adapters require a newer exact version (e.g. = 13.2.0).
///   Fix: patch GoogleMobileAdsDependencies.xml to match the AppLovin adapter requirement.
///
/// Conflict 2 — AppLovin SDK version mismatch:
///   com.applovin.mediation.ads (AppLovin MAX) pins AppLovinSDK to a specific version (e.g. = 13.6.2)
///   while com.google.ads.mobile.mediation.applovin (AdMob AppLovin adapter) references a different
///   version of GoogleMobileAdsMediationAppLovin that internally requires a different AppLovinSDK
///   (e.g. = 13.6.1). CocoaPods cannot reconcile the two.
///   Fix: patch the AdMob AppLovin adapter Dependencies.xml to reference the matching CocoaPod version.
///
/// Conflict 3 — Duplicate CocoaPods repo:
///   ~/.cocoapods/repos/cocoapods duplicates trunk and causes "Found multiple specifications" warnings.
///   Fix: delete the legacy directory directly.
///
/// Menu items are only active when the active Unity build target is iOS.
/// </summary>
[InitializeOnLoad]
public static class CocoaPodsConflictFixer
{
    static CocoaPodsConflictFixer()
    {
        EditorApplication.delayCall += CheckAndWarn;
    }

    // ── Validate methods (iOS build target only) ────────────────────────

    [MenuItem("Noctua/iOS/Fix CocoaPods Conflicts", true)]
    public static bool FixAll_Validate() =>
        EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;

    [MenuItem("Noctua/iOS/Check CocoaPods Versions", true)]
    public static bool CheckVersions_Validate() =>
        EditorUserBuildSettings.activeBuildTarget == BuildTarget.iOS;

    // ── Menu actions ────────────────────────────────────────────────────

    [MenuItem("Noctua/iOS/Fix CocoaPods Conflicts", false, 300)]
    public static void FixAll()
    {
        var results = new StringBuilder();
        bool anyPatched = false;

        // ── Fix 1: GMA SDK constraint mismatch ──────────────────────────
        var (gmaXmlPath, _)   = FindCurrentGmaConstraint();
        var requiredGmaVer    = FindAppLovinRequiredGmaVersion();

        if (!string.IsNullOrEmpty(gmaXmlPath) && !string.IsNullOrEmpty(requiredGmaVer))
        {
            bool patched = PatchGmaDepsXml(gmaXmlPath, requiredGmaVer);
            results.AppendLine(patched
                ? $"✓ Patched GoogleMobileAdsDependencies.xml → ~> {requiredGmaVer}"
                : "– GoogleMobileAdsDependencies.xml already has the correct version (no change)");
            if (patched) anyPatched = true;
        }
        else if (string.IsNullOrEmpty(gmaXmlPath))
        {
            results.AppendLine("– GoogleMobileAdsDependencies.xml not found (com.google.ads.mobile not installed?)");
        }
        else
        {
            results.AppendLine("– AppLovin Google adapter not installed — no GMA constraint to patch");
        }

        // ── Fix 2: AppLovin SDK version mismatch ────────────────────────
        var (admobApplovinXmlPath, maxSdkVer, adapterSdkVer) = FindAppLovinSdkConflict();

        if (!string.IsNullOrEmpty(admobApplovinXmlPath) &&
            !string.IsNullOrEmpty(maxSdkVer) &&
            !string.IsNullOrEmpty(adapterSdkVer) &&
            maxSdkVer != adapterSdkVer)
        {
            bool patched = PatchAdmobApplovinDepsXml(admobApplovinXmlPath, maxSdkVer);
            results.AppendLine(patched
                ? $"✓ Patched GoogleMobileAdsMediationAppLovin → {maxSdkVer}.0"
                : "– AdMob AppLovin adapter already compatible (no change)");
            if (patched) anyPatched = true;
        }
        else if (string.IsNullOrEmpty(maxSdkVer))
        {
            results.AppendLine("– AppLovin MAX SDK not installed — AppLovin SDK conflict check skipped");
        }
        else if (string.IsNullOrEmpty(admobApplovinXmlPath))
        {
            results.AppendLine("– AdMob AppLovin adapter not installed — no AppLovin SDK conflict possible");
        }
        else
        {
            results.AppendLine("– AppLovin SDK versions are already compatible (no change)");
        }

        // ── Fix 3: Duplicate CocoaPods repo ─────────────────────────────
        bool repoRemoved = RemoveDuplicateCocoapodsRepo();
        results.AppendLine(repoRemoved
            ? "✓ Removed legacy ~/.cocoapods/repos/cocoapods"
            : "– Legacy cocoapods repo not present (nothing to remove)");

        // ── Fix 4: BidMachine cross-catalog version conflict ─────────────────────────────
        var (maxBmVer, admobBmVer) = FindBidMachineConflict();
        if (!string.IsNullOrEmpty(maxBmVer) && !string.IsNullOrEmpty(admobBmVer) &&
            maxBmVer != admobBmVer)
        {
            bool bmPatched = PatchManifestBidMachineVersion(admobBmVer);
            results.AppendLine(bmPatched
                ? $"✓ Updated AppLovin BidMachine iOS in manifest.json → BidMachine {admobBmVer} (matching AdMob)"
                : $"⚠ BidMachine conflict (MAX={maxBmVer}, AdMob={admobBmVer}) — update via Noctua > Integration Manager");
            if (bmPatched) anyPatched = true;
        }
        else
        {
            results.AppendLine("– BidMachine: no conflict (no change)");
        }

        // ── Fix 5: Other cross-catalog adapter version conflicts ─────────
        foreach (var pair in s_crossCatalogPairs)
        {
            var (maxVer, admobVer) = DetectCrossCatalogConflict(pair);
            if (!IsCrossCatalogVersionMismatch(maxVer, admobVer)) continue;

            bool patched = PatchManifestAdapterVersion(pair.AppLovinIosPkg, maxVer, admobVer);
            results.AppendLine(patched
                ? $"✓ Updated {pair.Network} iOS adapter → aligned to AdMob native SDK version"
                : $"⚠ {pair.Network} conflict (MAX={maxVer}, AdMob={admobVer}) — update via Integration Manager");
            if (patched) anyPatched = true;
        }

        if (anyPatched)
        {
            results.AppendLine();
            results.AppendLine("⚠ XML patches are inside Library/PackageCache and will be lost if");
            results.AppendLine("  the package cache is cleared. To make fixes permanent, upgrade");
            results.AppendLine("  packages to compatible versions in the IAA Providers section.");
        }

        EditorUtility.DisplayDialog("CocoaPods Conflict Fixer", results.ToString().Trim(), "OK");

        if (anyPatched)
            AssetDatabase.Refresh();
    }

    [MenuItem("Noctua/iOS/Check CocoaPods Versions", false, 301)]
    public static void CheckVersions()
    {
        var (_, currentGmaConstraint)    = FindCurrentGmaConstraint();
        var requiredGmaVer               = FindAppLovinRequiredGmaVersion();
        bool gmaConflict                 = IsConflicting(currentGmaConstraint, requiredGmaVer);

        var (_, maxSdkVer, adapterSdkVer) = FindAppLovinSdkConflict();
        bool applovinConflict            = !string.IsNullOrEmpty(maxSdkVer) &&
                                           !string.IsNullOrEmpty(adapterSdkVer) &&
                                           maxSdkVer != adapterSdkVer;

        var repoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cocoapods", "repos", "cocoapods");
        bool hasDuplicateRepo = Directory.Exists(repoPath);

        var (maxBidMachineVer, admobBidMachineVer) = FindBidMachineConflict();
        bool bidMachineConflict = !string.IsNullOrEmpty(maxBidMachineVer) &&
                                  !string.IsNullOrEmpty(admobBidMachineVer) &&
                                  maxBidMachineVer != admobBidMachineVer;

        // compute cross-catalog conflict results up front for use in both anyConflict and sb
        var otherConflictResults = new List<(string network, string maxVer, string admobVer, bool conflict)>();
        foreach (var pair in s_crossCatalogPairs)
        {
            var (maxVer, admobVer) = DetectCrossCatalogConflict(pair);
            bool conflict = IsCrossCatalogVersionMismatch(maxVer, admobVer);
            if (!string.IsNullOrEmpty(maxVer) && !string.IsNullOrEmpty(admobVer))
                otherConflictResults.Add((pair.Network, maxVer, admobVer, conflict));
        }
        bool anyOtherConflict = otherConflictResults.Any(r => r.conflict);

        bool anyConflict = gmaConflict || applovinConflict || hasDuplicateRepo || bidMachineConflict || anyOtherConflict;

        var sb = new StringBuilder();

        sb.AppendLine("── Google-Mobile-Ads-SDK ───────────────────────────────");
        sb.AppendLine($"Current constraint  :  {currentGmaConstraint ?? "(com.google.ads.mobile not installed)"}");
        sb.AppendLine($"AppLovin requires   :  {(string.IsNullOrEmpty(requiredGmaVer) ? "(AppLovin Google adapter not installed)" : "~> " + requiredGmaVer)}");
        sb.AppendLine($"Status              :  {(gmaConflict ? "⚠  CONFLICT" : "✓  OK")}");

        sb.AppendLine();
        sb.AppendLine("── AppLovin SDK (MAX ↔ AdMob adapter) ──────────────────");
        sb.AppendLine($"AppLovin MAX SDK    :  {(string.IsNullOrEmpty(maxSdkVer) ? "(not installed)" : "AppLovinSDK = " + maxSdkVer)}");
        sb.AppendLine($"AdMob adapter needs :  {(string.IsNullOrEmpty(adapterSdkVer) ? "(AdMob AppLovin adapter not installed)" : "AppLovinSDK = " + adapterSdkVer)}");
        sb.AppendLine($"Status              :  {(applovinConflict ? "⚠  CONFLICT" : "✓  OK")}");

        sb.AppendLine();
        sb.AppendLine("── Duplicate CocoaPods repo ────────────────────────────");
        sb.AppendLine($"~/.cocoapods/repos/cocoapods  :  {(hasDuplicateRepo ? "⚠  EXISTS (causes duplicate specs)" : "✓  Not present")}");

        sb.AppendLine();
        sb.AppendLine("── BidMachine (AppLovin MAX ↔ AdMob adapter) ───────────");
        sb.AppendLine($"AppLovin MAX needs  :  {(string.IsNullOrEmpty(maxBidMachineVer) ? "(AppLovin BidMachine adapter not installed)" : "BidMachine = " + maxBidMachineVer)}");
        sb.AppendLine($"AdMob adapter needs :  {(string.IsNullOrEmpty(admobBidMachineVer) ? "(AdMob BidMachine adapter not installed)" : "BidMachine = " + admobBidMachineVer)}");
        sb.AppendLine($"Status              :  {(bidMachineConflict ? "⚠  CONFLICT" : "✓  OK")}");

        if (otherConflictResults.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("── Other cross-catalog adapter conflicts ────────────────");
            foreach (var (network, maxVer, admobVer, conflict) in otherConflictResults)
                sb.AppendLine($"{network,-22}:  MAX={maxVer}  AdMob={admobVer}  {(conflict ? "⚠  CONFLICT" : "✓  OK")}");
        }

        if (anyConflict)
        {
            sb.AppendLine();
            sb.AppendLine("Run  Noctua > iOS > Fix CocoaPods Conflicts  to resolve.");
        }

        EditorUtility.DisplayDialog("CocoaPods Version Check", sb.ToString().Trim(), "OK");
    }

    // ── Auto-warn on Editor load ────────────────────────────────────────

    /// <summary>
    /// Called via <see cref="EditorApplication.delayCall"/> on every Editor startup.
    /// Only logs warnings when the active build target is iOS and a conflict is detected.
    /// </summary>
    internal static void CheckAndWarn()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            return;

        var (_, currentGmaConstraint) = FindCurrentGmaConstraint();
        var requiredGmaVer            = FindAppLovinRequiredGmaVersion();

        if (IsConflicting(currentGmaConstraint, requiredGmaVer))
        {
            UnityEngine.Debug.LogWarning(
                $"[NoctuaSDK] CocoaPods conflict: Google-Mobile-Ads-SDK is constrained to " +
                $"'{currentGmaConstraint}' but the AppLovin Google adapter requires '~> {requiredGmaVer}'. " +
                "Run  Noctua > iOS > Fix CocoaPods Conflicts  to resolve.");
        }

        var (_, maxSdkVer, adapterSdkVer) = FindAppLovinSdkConflict();
        if (!string.IsNullOrEmpty(maxSdkVer) && !string.IsNullOrEmpty(adapterSdkVer) &&
            maxSdkVer != adapterSdkVer)
        {
            UnityEngine.Debug.LogWarning(
                $"[NoctuaSDK] CocoaPods conflict: AppLovin MAX SDK requires AppLovinSDK = {maxSdkVer} " +
                $"but the AdMob AppLovin mediation adapter targets AppLovinSDK = {adapterSdkVer}. " +
                "Run  Noctua > iOS > Fix CocoaPods Conflicts  to resolve.");
        }

        var (maxBidMachineVer, admobBidMachineVer) = FindBidMachineConflict();
        if (!string.IsNullOrEmpty(maxBidMachineVer) && !string.IsNullOrEmpty(admobBidMachineVer) &&
            maxBidMachineVer != admobBidMachineVer)
        {
            UnityEngine.Debug.LogWarning(
                $"[NoctuaSDK] CocoaPods conflict: AppLovin MAX BidMachine adapter requires " +
                $"BidMachine = {maxBidMachineVer} but AdMob BidMachine adapter requires " +
                $"BidMachine = {admobBidMachineVer}. " +
                "Run  Noctua > iOS > Fix CocoaPods Conflicts  to resolve.");
        }

        foreach (var pair in s_crossCatalogPairs)
        {
            var (maxVer, admobVer) = DetectCrossCatalogConflict(pair);
            if (IsCrossCatalogVersionMismatch(maxVer, admobVer))
            {
                UnityEngine.Debug.LogWarning(
                    $"[NoctuaSDK] CocoaPods conflict: AppLovin MAX {pair.Network} adapter " +
                    $"({pair.AppLovinPodName} {maxVer}) and AdMob {pair.Network} adapter " +
                    $"({pair.AdmobPodName} {admobVer}) require different native SDK versions. " +
                    "Run  Noctua > iOS > Fix CocoaPods Conflicts  to resolve.");
            }
        }
    }

    // ── Version detection ───────────────────────────────────────────────

    /// <summary>
    /// Locates GoogleMobileAdsDependencies.xml in the package cache and parses the current
    /// iOS CocoaPod version constraint for Google-Mobile-Ads-SDK.
    /// </summary>
    static (string path, string constraint) FindCurrentGmaConstraint()
    {
        var xmlPath = FindPackageCacheFile(
            "com.google.ads.mobile",
            Path.Combine("GoogleMobileAds", "Editor", "GoogleMobileAdsDependencies.xml"));

        if (string.IsNullOrEmpty(xmlPath))
            return (null, null);

        var content = File.ReadAllText(xmlPath);
        var match = Regex.Match(content,
            @"<iosPod\s+name=""Google-Mobile-Ads-SDK""\s+version=""([^""]+)""");

        return (xmlPath, match.Success ? match.Groups[1].Value : null);
    }

    /// <summary>
    /// Finds the AppLovin Google (or Google Ad Manager) iOS adapter Dependencies.xml and
    /// derives the required Google-Mobile-Ads-SDK version from the adapter's own version string.
    /// e.g. adapter version "13.2.0.0" → required GMA SDK "13.2.0"
    /// </summary>
    static string FindAppLovinRequiredGmaVersion()
    {
        foreach (var pkgPrefix in new[]
        {
            "com.applovin.mediation.adapters.google.ios",
            "com.applovin.mediation.adapters.googleadmanager.ios"
        })
        {
            var depPath = FindPackageCacheFile(pkgPrefix, Path.Combine("Editor", "Dependencies.xml"));
            if (string.IsNullOrEmpty(depPath)) continue;

            var content = File.ReadAllText(depPath);

            // Match: <iosPod name="AppLovinMediationGoogle[AdManager]Adapter" version="13.2.0.0">
            // Extract first 3 version components: "13.2.0"
            var match = Regex.Match(content,
                @"<iosPod\s+name=""AppLovinMediationGoogle[^""]*""\s+version=""(\d+\.\d+\.\d+)\.\d+""");

            if (match.Success)
                return match.Groups[1].Value;
        }

        return null;
    }

    /// <summary>
    /// Detects a version conflict between AppLovin MAX SDK and the AdMob AppLovin mediation adapter.
    ///
    /// AppLovin MAX SDK (com.applovin.mediation.ads) pins AppLovinSDK to an exact version (e.g. = 13.6.2).
    /// The AdMob AppLovin mediation adapter (com.google.ads.mobile.mediation.applovin) references a
    /// CocoaPod (GoogleMobileAdsMediationAppLovin) that internally requires a specific AppLovinSDK version.
    /// If the two differ, CocoaPods cannot reconcile them.
    ///
    /// Returns (admobAdapterXmlPath, maxSdkVersion, adapterSdkVersion).
    /// adapterSdkVersion is derived from the CocoaPod version: "13.6.1.0" → "13.6.1".
    /// </summary>
    static (string admobAdapterXmlPath, string maxSdkVersion, string adapterSdkVersion) FindAppLovinSdkConflict()
    {
        // 1. Find AppLovin MAX SDK's AppLovinSDK version
        var maxDepsPath = FindPackageCacheFile(
            "com.applovin.mediation.ads",
            Path.Combine("AppLovin", "Editor", "Dependencies.xml"));

        if (string.IsNullOrEmpty(maxDepsPath))
            return (null, null, null);

        var maxContent = File.ReadAllText(maxDepsPath);
        // e.g. <iosPod name="AppLovinSDK" version="= 13.6.2" ...> or version="13.6.2"
        var maxMatch = Regex.Match(maxContent,
            @"<iosPod\s+name=""AppLovinSDK""\s+version=""[=\s]*(\d+\.\d+\.\d+)");
        if (!maxMatch.Success)
            return (null, null, null);

        var maxSdkVersion = maxMatch.Groups[1].Value; // e.g. "13.6.2"

        // 2. Find AdMob AppLovin mediation adapter Dependencies.xml
        var admobPath = FindPackageCacheFile(
            "com.google.ads.mobile.mediation.applovin",
            Path.Combine("source", "plugin", "Assets", "GoogleMobileAds", "Mediation",
                         "AppLovin", "Editor", "AppLovinMediationDependencies.xml"));

        if (string.IsNullOrEmpty(admobPath))
            return (null, maxSdkVersion, null);

        var admobContent = File.ReadAllText(admobPath);
        // e.g. <iosPod name="GoogleMobileAdsMediationAppLovin" version="= 13.6.1.0">
        // Extract first 3 components: "13.6.1"
        var adapterMatch = Regex.Match(admobContent,
            @"<iosPod\s+name=""GoogleMobileAdsMediationAppLovin""\s+version=""[=\s]*(\d+\.\d+\.\d+)\.\d+""");
        if (!adapterMatch.Success)
            return (admobPath, maxSdkVersion, null);

        var adapterSdkVersion = adapterMatch.Groups[1].Value; // e.g. "13.6.1"

        return (admobPath, maxSdkVersion, adapterSdkVersion);
    }

    // ── Cross-catalog adapter pairs (AppLovin MAX ↔ AdMob) ─────────────
    // Each entry: network display name, AppLovin iOS UPM pkg prefix,
    // AppLovin iosPod element name, AdMob UPM pkg prefix, AdMob Dependencies.xml
    // wildcard search pattern, AdMob iosPod element name.
    private readonly struct CrossCatalogPair
    {
        public readonly string Network;
        public readonly string AppLovinIosPkg;
        public readonly string AppLovinPodName;
        public readonly string AdmobPkg;
        public readonly string AdmobPodPattern;
        public readonly string AdmobPodName;

        public CrossCatalogPair(string network,
            string maxPkg, string maxPod,
            string admobPkg, string admobPattern, string admobPod)
        {
            Network         = network;
            AppLovinIosPkg  = maxPkg;
            AppLovinPodName = maxPod;
            AdmobPkg        = admobPkg;
            AdmobPodPattern = admobPattern;
            AdmobPodName    = admobPod;
        }
    }

    private static readonly CrossCatalogPair[] s_crossCatalogPairs =
    {
        new CrossCatalogPair("PubMatic",
            "com.applovin.mediation.adapters.pubmatic.ios",   "AppLovinMediationPubMaticAdapter",
            "com.google.ads.mobile.mediation.pubmatic",       "*PubMatic*Dependencies.xml",     "GoogleMobileAdsMediationPubMatic"),
        new CrossCatalogPair("IronSource",
            "com.applovin.mediation.adapters.ironsource.ios", "AppLovinMediationIronSourceAdapter",
            "com.google.ads.mobile.mediation.ironsource",     "*IronSource*Dependencies.xml",   "GoogleMobileAdsMediationIronSource"),
        new CrossCatalogPair("Pangle / ByteDance",
            "com.applovin.mediation.adapters.bytedance.ios",  "AppLovinMediationByteDanceAdapter",
            "com.google.ads.mobile.mediation.pangle",         "*Pangle*Dependencies.xml",       "GoogleMobileAdsMediationPangle"),
        new CrossCatalogPair("Mintegral",
            "com.applovin.mediation.adapters.mintegral.ios",  "AppLovinMediationMintegralAdapter",
            "com.google.ads.mobile.mediation.mintegral",      "*Mintegral*Dependencies.xml",    "GoogleMobileAdsMediationMintegral"),
        new CrossCatalogPair("DT Exchange / Fyber",
            "com.applovin.mediation.adapters.fyber.ios",      "AppLovinMediationFyberAdapter",
            "com.google.ads.mobile.mediation.dtexchange",     "*DTExchange*Dependencies.xml",   "GoogleMobileAdsMediationFyber"),
        new CrossCatalogPair("Moloco",
            "com.applovin.mediation.adapters.moloco.ios",     "AppLovinMediationMolocoAdapter",
            "com.google.ads.mobile.mediation.moloco",         "*Moloco*Dependencies.xml",       "GoogleMobileAdsMediationMoloco"),
    };

    /// <summary>
    /// Detects a version conflict between the AppLovin MAX BidMachine iOS adapter and the
    /// AdMob BidMachine mediation adapter. Both transitively pin the BidMachine CocoaPod to
    /// an exact version; if those versions differ, pod install fails.
    /// Returns (maxBidMachineVersion, admobBidMachineVersion) — null strings if not installed.
    /// </summary>
    static (string maxBidMachineVer, string admobBidMachineVer) FindBidMachineConflict()
    {
        // 1. AppLovin MAX BidMachine iOS adapter
        var maxDepsPath = FindPackageCacheFile(
            "com.applovin.mediation.adapters.bidmachine.ios",
            Path.Combine("Editor", "Dependencies.xml"));

        if (string.IsNullOrEmpty(maxDepsPath)) return (null, null);

        var maxContent = File.ReadAllText(maxDepsPath);
        // e.g. <iosPod name="AppLovinMediationBidMachineAdapter" version="3.6.1.0.0">
        var maxMatch = Regex.Match(maxContent,
            @"<iosPod\s+name=""AppLovinMediationBidMachineAdapter""\s+version=""(\d+\.\d+\.\d+)\.\d+");
        if (!maxMatch.Success) return (null, null);
        var maxBidMachineVer = maxMatch.Groups[1].Value; // e.g. "3.6.1"

        // 2. AdMob BidMachine adapter — search package dir for its Dependencies.xml
        var admobDepsPath = FindPackageCacheFileWild(
            "com.google.ads.mobile.mediation.bidmachine",
            "*BidMachine*Dependencies.xml");
        if (string.IsNullOrEmpty(admobDepsPath)) return (maxBidMachineVer, null);

        var admobContent = File.ReadAllText(admobDepsPath);
        // e.g. <iosPod name="GoogleMobileAdsMediationBidMachine" version="= 3.5.1.2">
        var admobMatch = Regex.Match(admobContent,
            @"<iosPod\s+name=""GoogleMobileAdsMediationBidMachine""\s+version=""[=\s]*(\d+\.\d+\.\d+)\.\d+");
        if (!admobMatch.Success) return (maxBidMachineVer, null);
        var admobBidMachineVer = admobMatch.Groups[1].Value; // e.g. "3.5.1"

        return (maxBidMachineVer, admobBidMachineVer);
    }

    /// <summary>
    /// Detects whether the AppLovin MAX and AdMob adapters for the given network wrap
    /// different versions of the same underlying CocoaPod. Conflict is determined by
    /// comparing the first three version components (major.minor.patch) of each adapter.
    /// Returns (appLovinAdapterVer, admobAdapterVer) — null if either package is not installed.
    /// </summary>
    static (string appLovinVer, string admobVer) DetectCrossCatalogConflict(CrossCatalogPair pair)
    {
        var maxDepsPath = FindPackageCacheFile(
            pair.AppLovinIosPkg, Path.Combine("Editor", "Dependencies.xml"));
        if (string.IsNullOrEmpty(maxDepsPath)) return (null, null);

        var maxContent = File.ReadAllText(maxDepsPath);
        var maxMatch = Regex.Match(maxContent,
            $@"<iosPod\s+name=""{Regex.Escape(pair.AppLovinPodName)}""\s+version=""([^""]+)""");
        if (!maxMatch.Success) return (null, null);
        var appLovinVer = maxMatch.Groups[1].Value.Trim();

        var admobDepsPath = FindPackageCacheFileWild(pair.AdmobPkg, pair.AdmobPodPattern);
        if (string.IsNullOrEmpty(admobDepsPath)) return (appLovinVer, null);

        var admobContent = File.ReadAllText(admobDepsPath);
        var admobMatch = Regex.Match(admobContent,
            $@"<iosPod\s+name=""{Regex.Escape(pair.AdmobPodName)}""\s+version=""[=\s]*([^""]+)""");
        if (!admobMatch.Success) return (appLovinVer, null);
        var admobVer = admobMatch.Groups[1].Value.Trim();

        return (appLovinVer, admobVer);
    }

    /// <summary>
    /// Returns true when the first three version components (native SDK version) of
    /// <paramref name="appLovinVer"/> and <paramref name="admobVer"/> differ.
    /// </summary>
    static bool IsCrossCatalogVersionMismatch(string appLovinVer, string admobVer)
    {
        if (string.IsNullOrEmpty(appLovinVer) || string.IsNullOrEmpty(admobVer))
            return false;
        return GetFirstThreeComponents(appLovinVer) != GetFirstThreeComponents(admobVer);
    }

    // ── Conflict logic ──────────────────────────────────────────────────

    /// <summary>
    /// Returns true when <paramref name="currentConstraint"/> cannot satisfy
    /// <paramref name="requiredVersion"/>.
    ///
    /// CocoaPods pessimistic operator semantics:
    ///   "~> X.Y.Z"  (3 components) → >= X.Y.Z, &lt; X.(Y+1).0  (minor is pinned)
    ///   "~> X.Y"    (2 components) → >= X.Y,   &lt; (X+1).0    (major is pinned)
    /// </summary>
    static bool IsConflicting(string currentConstraint, string requiredVersion)
    {
        if (string.IsNullOrEmpty(currentConstraint) || string.IsNullOrEmpty(requiredVersion))
            return false;

        // Parse constraint: "~> 12.11.0" or "~> 13.0" or "12.11.0"
        var constraintMatch = Regex.Match(currentConstraint, @"(\d+)\.(\d+)(?:\.(\d+))?");
        var requiredMatch   = Regex.Match(requiredVersion,   @"(\d+)\.(\d+)\.(\d+)");

        if (!constraintMatch.Success || !requiredMatch.Success) return false;

        int cMajor     = int.Parse(constraintMatch.Groups[1].Value);
        int cMinor     = int.Parse(constraintMatch.Groups[2].Value);
        bool threeComp = constraintMatch.Groups[3].Success; // ~> X.Y.Z pins minor
        int cPatch     = threeComp ? int.Parse(constraintMatch.Groups[3].Value) : 0;

        int rMajor = int.Parse(requiredMatch.Groups[1].Value);
        int rMinor = int.Parse(requiredMatch.Groups[2].Value);
        int rPatch = int.Parse(requiredMatch.Groups[3].Value);

        if (cMajor != rMajor) return true; // major version mismatch — always a conflict

        // 3-component constraint (~> X.Y.Z): minor is pinned; required must be >= X.Y.Z and < X.(Y+1).0
        if (threeComp)
        {
            if (cMinor != rMinor) return true;      // different minor — out of range
            if (cPatch > rPatch)  return true;      // constraint floor exceeds required version
        }

        // 2-component constraint (~> X.Y): all X.* versions allowed — same major → no conflict
        return false;
    }

    // ── Fix operations ──────────────────────────────────────────────────

    /// <summary>
    /// Regex-replaces the Google-Mobile-Ads-SDK version attribute in
    /// <paramref name="xmlPath"/> with <c>~> <paramref name="requiredVersion"/></c>.
    /// Returns true if the file was actually modified.
    /// </summary>
    static bool PatchGmaDepsXml(string xmlPath, string requiredVersion)
    {
        var content = File.ReadAllText(xmlPath);

        // Replace only the version attribute on the Google-Mobile-Ads-SDK iosPod element
        var patched = Regex.Replace(
            content,
            @"(<iosPod\s+name=""Google-Mobile-Ads-SDK""\s+version="")([^""]+)("")",
            $"${{1}}~> {requiredVersion}$3");

        if (patched == content)
            return false; // nothing changed

        File.WriteAllText(xmlPath, patched);
        UnityEngine.Debug.Log(
            $"[NoctuaSDK] Patched Google-Mobile-Ads-SDK constraint → '~> {requiredVersion}' in\n{xmlPath}\n" +
            "NOTE: This file is inside Library/PackageCache and will be reset if the package cache is cleared. " +
            "Upgrade com.google.ads.mobile to 11.0.0+ to make the fix permanent.");
        return true;
    }

    /// <summary>
    /// Patches the AdMob AppLovin mediation adapter Dependencies.xml so that the
    /// GoogleMobileAdsMediationAppLovin CocoaPod version matches the AppLovin MAX SDK's
    /// AppLovinSDK requirement. For example, if AppLovin MAX requires AppLovinSDK = 13.6.2,
    /// this changes GoogleMobileAdsMediationAppLovin version to = 13.6.2.0.
    /// Returns true if the file was actually modified.
    /// </summary>
    static bool PatchAdmobApplovinDepsXml(string xmlPath, string requiredSdkVersion)
    {
        var content = File.ReadAllText(xmlPath);

        // Replace: version="13.6.1.0" (or any 4-component version) with version="X.Y.Z.0"
        var patched = Regex.Replace(
            content,
            @"(<iosPod\s+name=""GoogleMobileAdsMediationAppLovin""\s+version="")[^""]+("")",
            $"$1{requiredSdkVersion}.0$2");

        if (patched == content)
            return false;

        File.WriteAllText(xmlPath, patched);
        UnityEngine.Debug.Log(
            $"[NoctuaSDK] Patched GoogleMobileAdsMediationAppLovin → '{requiredSdkVersion}.0' in\n{xmlPath}\n" +
            "NOTE: This file is inside Library/PackageCache and will be reset if the package cache is cleared.");
        return true;
    }

    /// <summary>
    /// Updates the AppLovin BidMachine iOS adapter version in manifest.json to the version
    /// that wraps the same BidMachine native SDK version required by the AdMob adapter.
    /// AppLovin UPM version encoding: BidMachine X.Y.Z → (X*100000000 + Y*1000000 + Z*10000).0.0
    /// Returns true if manifest.json was actually modified.
    /// </summary>
    static bool PatchManifestBidMachineVersion(string admobBidMachineVer)
    {
        var parts = admobBidMachineVer.Split('.');
        if (parts.Length < 3 ||
            !int.TryParse(parts[0], out var major) ||
            !int.TryParse(parts[1], out var minor) ||
            !int.TryParse(parts[2], out var patch))
            return false;

        // Encode: X.Y.Z → X0Y0Z0000.0.0  (same scheme AppLovin uses for all adapter UPM versions)
        var targetUpmVer = $"{(long)major * 100000000 + minor * 1000000 + patch * 10000}.0.0";

        var manifestPath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath), "Packages", "manifest.json");
        if (!File.Exists(manifestPath)) return false;

        var content = File.ReadAllText(manifestPath);
        var patched = Regex.Replace(
            content,
            @"(""com\.applovin\.mediation\.adapters\.bidmachine\.ios""\s*:\s*"")([^""]+)("")",
            $"${{1}}{targetUpmVer}$3");

        if (patched == content) return false;

        File.WriteAllText(manifestPath, patched);
        UnityEngine.Debug.Log(
            $"[NoctuaSDK] Updated AppLovin BidMachine iOS in manifest.json → {targetUpmVer} " +
            $"(aligned with AdMob BidMachine {admobBidMachineVer}).\n" +
            "Unity will auto-resolve the updated package.");
        return true;
    }

    /// <summary>
    /// Updates the AppLovin iOS adapter version in manifest.json so that it targets the
    /// same native SDK version as the corresponding AdMob adapter.
    ///
    /// AppLovin UPM version encoding:
    ///   4-component adapters (X.Y.Z.W): UPM = X*10^6 + Y*10^4 + Z*10^2 + W
    ///   5-component adapters (X.Y.Z.W.V): UPM = X*10^8 + Y*10^6 + Z*10^4 + W*10^2 + V
    ///
    /// Target version is built from the first three components of <paramref name="admobAdapterVer"/>
    /// (the native SDK version) with the remaining components set to zero.
    ///
    /// Returns true if manifest.json was modified.
    /// </summary>
    static bool PatchManifestAdapterVersion(string appLovinIosPkg,
        string currentAppLovinVer, string admobAdapterVer)
    {
        // Determine native SDK version from AdMob adapter (first 3 components)
        var admobParts = admobAdapterVer.TrimStart('=', ' ').Split('.');
        if (admobParts.Length < 3 ||
            !int.TryParse(admobParts[0], out var major) ||
            !int.TryParse(admobParts[1], out var minor) ||
            !int.TryParse(admobParts[2], out var patch))
            return false;

        // Use the same number of version components as the current AppLovin version
        var currentParts = currentAppLovinVer.TrimStart('=', ' ').Split('.');
        long targetUpm;
        if (currentParts.Length >= 5)
            targetUpm = (long)major * 100000000L + minor * 1000000L + patch * 10000L;
        else
            targetUpm = (long)major * 1000000L + minor * 10000L + patch * 100L;

        var targetUpmVer = $"{targetUpm}.0.0";

        var manifestPath = Path.Combine(
            Path.GetDirectoryName(Application.dataPath), "Packages", "manifest.json");
        if (!File.Exists(manifestPath)) return false;

        var content = File.ReadAllText(manifestPath);
        var escaped = Regex.Escape(appLovinIosPkg);
        var patched = Regex.Replace(
            content,
            $@"(""{escaped}""\s*:\s*"")([^""]+)("")",
            $"${{1}}{targetUpmVer}$3");

        if (patched == content) return false;

        File.WriteAllText(manifestPath, patched);
        UnityEngine.Debug.Log(
            $"[NoctuaSDK] Updated {appLovinIosPkg} in manifest.json → {targetUpmVer} " +
            $"(aligned to AdMob adapter native SDK {major}.{minor}.{patch}).\n" +
            "Unity will auto-resolve the updated package.");
        return true;
    }

    /// <summary>
    /// Removes the legacy ~/.cocoapods/repos/cocoapods git mirror (introduced before the CDN trunk
    /// source was available). Its presence causes "Found multiple specifications" warnings for every
    /// pod that is also indexed on trunk. Returns true if the repo was removed.
    /// </summary>
    internal static bool RemoveDuplicateCocoapodsRepo()
    {
        var repoPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".cocoapods", "repos", "cocoapods");

        if (!Directory.Exists(repoPath))
            return false;

        try
        {
            // Delete the directory directly — more reliable than shelling out to `pod repo remove`
            // which can fail if `pod` is not on the shell PATH inside a Unity Editor process.
            Directory.Delete(repoPath, recursive: true);
            UnityEngine.Debug.Log("[NoctuaSDK] Removed legacy ~/.cocoapods/repos/cocoapods.");
            return true;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError(
                $"[NoctuaSDK] Failed to delete ~/.cocoapods/repos/cocoapods: {ex.Message}\n" +
                "Delete it manually:  rm -rf ~/.cocoapods/repos/cocoapods");
            return false;
        }
    }

    // ── Utility ─────────────────────────────────────────────────────────

    static string GetFirstThreeComponents(string version)
    {
        var parts = version.TrimStart('=', ' ').Split('.');
        return parts.Length >= 3 ? $"{parts[0]}.{parts[1]}.{parts[2]}" : version;
    }

    /// <summary>
    /// Finds the first file at <paramref name="relativePath"/> inside any directory in
    /// Library/PackageCache/ whose name starts with <paramref name="packagePrefix"/>@.
    /// </summary>
    static string FindPackageCacheFile(string packagePrefix, string relativePath)
    {
        var projectPath = Path.GetDirectoryName(Application.dataPath);
        var cacheDir    = Path.Combine(projectPath, "Library", "PackageCache");

        if (!Directory.Exists(cacheDir)) return null;

        foreach (var dir in Directory.GetDirectories(cacheDir, packagePrefix + "@*"))
        {
            var candidate = Path.Combine(dir, relativePath);
            if (File.Exists(candidate)) return candidate;
        }

        return null;
    }

    /// <summary>
    /// Like <see cref="FindPackageCacheFile"/> but searches recursively for any file matching
    /// <paramref name="searchPattern"/> (supports wildcards) inside the package directory.
    /// </summary>
    static string FindPackageCacheFileWild(string packagePrefix, string searchPattern)
    {
        var projectPath = Path.GetDirectoryName(Application.dataPath);
        var cacheDir    = Path.Combine(projectPath, "Library", "PackageCache");
        if (!Directory.Exists(cacheDir)) return null;
        foreach (var dir in Directory.GetDirectories(cacheDir, packagePrefix + "@*"))
        {
            var files = Directory.GetFiles(dir, searchPattern, SearchOption.AllDirectories);
            if (files.Length > 0) return files[0];
        }
        return null;
    }
}

/// <summary>
/// Runs before every iOS build to delete the legacy ~/.cocoapods/repos/cocoapods directory.
/// This directory is sometimes re-created by `pod repo update` and causes "Found multiple
/// specifications" warnings for every pod during pod install.
/// </summary>
public class CocoaPodsPreBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.iOS)
            return;

        bool removed = CocoaPodsConflictFixer.RemoveDuplicateCocoapodsRepo();
        if (removed)
            UnityEngine.Debug.Log(
                "[NoctuaSDK] Pre-build: Removed legacy ~/.cocoapods/repos/cocoapods " +
                "to prevent duplicate specifications warnings during pod install.");
    }
}
#endif
