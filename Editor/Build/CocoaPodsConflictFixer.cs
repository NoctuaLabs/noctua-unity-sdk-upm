#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
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

        bool anyConflict = gmaConflict || applovinConflict || hasDuplicateRepo;

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
    /// Removes the legacy ~/.cocoapods/repos/cocoapods git mirror (introduced before the CDN trunk
    /// source was available). Its presence causes "Found multiple specifications" warnings for every
    /// pod that is also indexed on trunk. Returns true if the repo was removed.
    /// </summary>
    static bool RemoveDuplicateCocoapodsRepo()
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
}
#endif
