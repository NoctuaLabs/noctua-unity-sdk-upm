#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Detects and repairs CocoaPods version conflicts that arise when both the Google Mobile Ads
/// Unity plugin and AppLovin MAX mediation adapters are installed in the same project.
///
/// Root cause:
///   - com.google.ads.mobile pins  Google-Mobile-Ads-SDK to a specific minor constraint (e.g. ~> 12.11.0)
///   - AppLovin Google/AdManager adapters require a newer exact version       (e.g. = 13.2.0)
///   - CocoaPods cannot reconcile the two → pod install fails
///
/// Fix:
///   - Patch GoogleMobileAdsDependencies.xml to use the constraint required by the AppLovin adapter
///   - Remove the legacy ~/.cocoapods/repos/cocoapods repository that causes duplicate-spec warnings
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
        var (xmlPath, _) = FindCurrentGmaConstraint();
        var requiredVersion = FindAppLovinRequiredGmaVersion();

        var results = new StringBuilder();

        bool xmlPatched = false;
        if (!string.IsNullOrEmpty(xmlPath) && !string.IsNullOrEmpty(requiredVersion))
        {
            xmlPatched = PatchGmaDepsXml(xmlPath, requiredVersion);
            results.AppendLine(xmlPatched
                ? $"✓ Patched GoogleMobileAdsDependencies.xml → ~> {requiredVersion}"
                : "– GoogleMobileAdsDependencies.xml already has the correct version (no change)");
        }
        else if (string.IsNullOrEmpty(xmlPath))
        {
            results.AppendLine("– GoogleMobileAdsDependencies.xml not found (com.google.ads.mobile not installed?)");
        }
        else
        {
            results.AppendLine("– AppLovin Google adapter not installed — no target version to patch to");
        }

        bool repoRemoved = RemoveDuplicateCocoapodsRepo();
        results.AppendLine(repoRemoved
            ? "✓ Removed legacy ~/.cocoapods/repos/cocoapods"
            : "– Legacy cocoapods repo not present (nothing to remove)");

        if (xmlPatched)
        {
            results.AppendLine();
            results.AppendLine("⚠ The XML patch is inside Library/PackageCache and will be lost if");
            results.AppendLine("  the package cache is cleared. To make it permanent, upgrade");
            results.AppendLine("  com.google.ads.mobile to 11.0.0+ in the IAA Providers section.");
        }

        EditorUtility.DisplayDialog("CocoaPods Conflict Fixer", results.ToString().Trim(), "OK");

        if (xmlPatched)
            AssetDatabase.Refresh();
    }

    [MenuItem("Noctua/iOS/Check CocoaPods Versions", false, 301)]
    public static void CheckVersions()
    {
        var (_, currentConstraint) = FindCurrentGmaConstraint();
        var requiredVersion = FindAppLovinRequiredGmaVersion();

        bool hasConflict = IsConflicting(currentConstraint, requiredVersion);
        string statusLine = hasConflict ? "⚠  CONFLICT DETECTED" : "✓  No conflict";

        var sb = new StringBuilder();
        sb.AppendLine("Google-Mobile-Ads-SDK (iOS CocoaPod)");
        sb.AppendLine();
        sb.AppendLine($"Current constraint  :  {currentConstraint ?? "(com.google.ads.mobile not installed)"}");
        sb.AppendLine($"AppLovin requires   :  {(string.IsNullOrEmpty(requiredVersion) ? "(AppLovin Google adapter not installed)" : "~> " + requiredVersion)}");
        sb.AppendLine();
        sb.AppendLine($"Status: {statusLine}");

        if (hasConflict)
            sb.AppendLine("\nRun  Noctua > iOS > Fix CocoaPods Conflicts  to resolve.");

        EditorUtility.DisplayDialog("CocoaPods Version Check", sb.ToString().Trim(), "OK");
    }

    // ── Auto-warn on Editor load ────────────────────────────────────────

    /// <summary>
    /// Called via <see cref="EditorApplication.delayCall"/> on every Editor startup.
    /// Only logs a warning when the active build target is iOS and a conflict is detected.
    /// </summary>
    internal static void CheckAndWarn()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            return;

        var (_, currentConstraint) = FindCurrentGmaConstraint();
        var requiredVersion = FindAppLovinRequiredGmaVersion();

        if (IsConflicting(currentConstraint, requiredVersion))
        {
            UnityEngine.Debug.LogWarning(
                $"[NoctuaSDK] CocoaPods conflict detected: Google-Mobile-Ads-SDK is constrained to " +
                $"'{currentConstraint}' but the installed AppLovin Google adapter requires '~> {requiredVersion}'. " +
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
            // `pod` is typically at /usr/local/bin/pod or /usr/bin/pod; use login shell to find it.
            // Do NOT redirect stdout/stderr — if pipes are not drained the child process deadlocks.
            var psi = new ProcessStartInfo("/bin/bash", "-lc \"pod repo remove cocoapods\"")
            {
                RedirectStandardOutput = false,
                RedirectStandardError  = false,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(30_000); // 30-second timeout

            bool removed = !Directory.Exists(repoPath);
            if (removed)
                UnityEngine.Debug.Log("[NoctuaSDK] Removed legacy ~/.cocoapods/repos/cocoapods.");
            else
                UnityEngine.Debug.LogWarning(
                    "[NoctuaSDK] 'pod repo remove cocoapods' ran but the directory still exists. " +
                    "You may need to delete ~/.cocoapods/repos/cocoapods manually.");

            return removed;
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[NoctuaSDK] Failed to run 'pod repo remove': {ex.Message}");
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
