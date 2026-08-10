#if UNITY_EDITOR
using System;
using System.IO;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Lets integrators opt a build into a different Android Gradle Plugin (AGP)
/// version than whatever the consumer project's own Gradle templates pin,
/// without hand-editing <c>Assets/Plugins/Android/baseProjectTemplate.gradle</c>.
///
/// Default behavior (no override set) is a strict no-op: the AGP version stays
/// whatever the consumer project's own template/generated <c>build.gradle</c>
/// already declares (AGP 8.x today). Setting an override rewrites the AGP
/// version lines in the *generated* root <c>build.gradle</c> after Unity has
/// finished generating the Gradle project — the same "patch the generated
/// project" approach <c>BuildPostProcessor.ModifyRootBuildGradle</c> already
/// uses for the google-services plugin.
///
/// No AGP version is hardcoded here. The override is a free-form string the
/// integrator types into the Noctua Integration Manager, so trying a newer
/// AGP release never requires an SDK update — only the *major* version parsed
/// from that string is used, and only to decide whether AGP-9-specific
/// validation below should run.
///
/// AGP 9 requires Gradle ≥ 9.1.0 and JDK ≥ 17 (see
/// https://developer.android.com/build/releases/agp-9-0-0-release-notes).
///
/// Google's GMA Unity plugin had an AGP-9 + Unity 6 namespace conflict
/// (<c>GoogleMobileAdsPlugin.androidlib</c> vs the plugin's own AAR both
/// declaring <c>com.google.unity.ads</c> — see
/// https://github.com/googleads/googleads-mobile-unity/issues/4212, filed
/// against plugin v11.2). That conflict is resolved by upgrading
/// <c>com.google.ads.mobile</c> to v11.3.0+ (confirmed) — there is no
/// AGP-9-side file to patch for it. <see cref="WarnIfGmaNeedsAgp9Upgrade"/>
/// only checks the installed version and tells the integrator to upgrade via
/// the Integration Manager when it's below the fixed floor; it never edits
/// any Gradle/manifest file, since the real fix lives upstream in the GMA
/// package itself.
/// </summary>
public static class AndroidGradlePluginVersionManager
{
    private const string OverrideEditorPrefsKey = "Noctua.AgpVersionOverride";

    /// <summary>
    /// The AGP version string the integrator wants to build with, or empty to
    /// leave the consumer project's own template/generated pin untouched.
    /// </summary>
    public static string VersionOverride
    {
        get => EditorPrefs.GetString(OverrideEditorPrefsKey, string.Empty);
        set => EditorPrefs.SetString(OverrideEditorPrefsKey, value?.Trim() ?? string.Empty);
    }

    /// <summary>
    /// Parses the leading major version number out of <see cref="VersionOverride"/>
    /// (e.g. "9.0.1" → 9). Returns null when the override is empty or doesn't
    /// start with a number — callers treat null the same as "AGP 8 or default".
    /// </summary>
    public static int? GetConfiguredAgpMajorVersion()
    {
        var version = VersionOverride;

        if (string.IsNullOrEmpty(version))
        {
            return null;
        }

        var match = Regex.Match(version, @"^(\d+)");

        return match.Success && int.TryParse(match.Groups[1].Value, out var major) ? major : (int?)null;
    }

    /// <summary>True when the configured override's major version is 9 or higher.</summary>
    public static bool IsAgp9OrHigher => GetConfiguredAgpMajorVersion() is { } major && major >= 9;

    /// <summary>
    /// Rewrites the <c>com.android.application</c> / <c>com.android.library</c>
    /// plugin version lines in the generated root <c>build.gradle</c> content to
    /// <see cref="VersionOverride"/>. Returns <paramref name="gradleContent"/>
    /// unchanged when no override is set.
    /// </summary>
    public static string PatchAgpVersionInRootBuildGradle(string gradleContent)
    {
        var version = VersionOverride;

        if (string.IsNullOrEmpty(version))
        {
            return gradleContent;
        }

        var patched = Regex.Replace(
            gradleContent,
            @"id 'com\.android\.application' version '[^']*'",
            $"id 'com.android.application' version '{version}'"
        );

        patched = Regex.Replace(
            patched,
            @"id 'com\.android\.library' version '[^']*'",
            $"id 'com.android.library' version '{version}'"
        );

        if (patched != gradleContent)
        {
            Log($"Overrode AGP version to '{version}' in generated root build.gradle.");
        }

        return patched;
    }

    /// <summary>
    /// When the configured override is AGP 9+, checks the generated project's
    /// Gradle version (already parsed by <c>BuildPostProcessor.GetGradleVersion</c>
    /// via the passed-in <paramref name="gradleVersion"/>) against AGP 9's
    /// documented Gradle ≥ 9.1.0 floor, and logs an actionable message if it's
    /// short. Non-fatal — the Gradle invocation will fail on its own if the
    /// requirement truly isn't met; this just surfaces the reason earlier.
    /// </summary>
    public static void ValidateAgp9Prerequisites(Version gradleVersion)
    {
        if (!IsAgp9OrHigher)
        {
            return;
        }

        var minGradle = new Version(9, 1, 0);

        if (gradleVersion < minGradle)
        {
            LogError(
                $"AGP {VersionOverride} requires Gradle >= 9.1.0, but the generated project is using Gradle {gradleVersion}. " +
                "Set a custom Gradle 9.1+ install in Preferences > External Tools (uncheck 'Gradle Installed with Unity'), " +
                "or switch to a Unity Editor version whose bundled Gradle already satisfies this."
            );
        }
        else
        {
            Log($"AGP {VersionOverride}: Gradle {gradleVersion} satisfies the >= 9.1.0 requirement.");
        }

        LogWarning(
            "AGP 9 also requires the JDK configured in Preferences > External Tools to be 17 or newer — " +
            "this cannot be validated from managed code; verify it manually if the build fails at the Gradle daemon step."
        );
    }

    /// <summary>The lowest com.google.ads.mobile version confirmed to build cleanly under AGP 9.</summary>
    private static readonly Version GmaAgp9FixedFloor = new Version(11, 3, 0);

    /// <summary>
    /// When the configured AGP override is 9+, checks the installed
    /// <c>com.google.ads.mobile</c> version against
    /// <see cref="GmaAgp9FixedFloor"/> and logs an actionable message if it's
    /// below that — the googleads-mobile-unity#4212 namespace conflict
    /// (<c>GoogleMobileAdsPlugin.androidlib</c> vs the plugin's own AAR both
    /// declaring <c>com.google.unity.ads</c>) is resolved by upgrading the
    /// package, not by patching any generated file, so this only reads
    /// <c>Packages/manifest.json</c> and never writes to it or to any Gradle
    /// output. Silent no-op when GMA isn't installed or is already at/above
    /// the fixed floor.
    /// </summary>
    public static void WarnIfGmaNeedsAgp9Upgrade()
    {
        if (!IsAgp9OrHigher)
        {
            return;
        }

        var installedVersion = GetInstalledGmaVersion();

        if (installedVersion == null)
        {
            // com.google.ads.mobile not installed — nothing to warn about.
            return;
        }

        if (installedVersion < GmaAgp9FixedFloor)
        {
            LogError(
                $"com.google.ads.mobile {installedVersion} is below {GmaAgp9FixedFloor} — under AGP 9 this hits " +
                "googleads-mobile-unity#4212 (GoogleMobileAdsPlugin.androidlib namespace conflict, " +
                "https://github.com/googleads/googleads-mobile-unity/issues/4212). " +
                $"Upgrade com.google.ads.mobile to {GmaAgp9FixedFloor}+ via the Noctua Integration Manager's " +
                "Recommended Setup to resolve it — there is no AGP-9-side workaround for older versions."
            );
        }
        else
        {
            Log($"com.google.ads.mobile {installedVersion} satisfies the AGP 9 fixed floor ({GmaAgp9FixedFloor}+).");
        }
    }

    /// <summary>
    /// Reads the installed <c>com.google.ads.mobile</c> version straight from
    /// <c>Packages/manifest.json</c> (read-only). Returns null when the
    /// package isn't listed or the version string isn't parseable — e.g. a
    /// git/UPM-registry URL pin instead of a bare semver.
    /// </summary>
    private static Version GetInstalledGmaVersion()
    {
        var manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");

        if (!File.Exists(manifestPath))
        {
            return null;
        }

        var manifest = JObject.Parse(File.ReadAllText(manifestPath));
        var versionToken = manifest["dependencies"]?["com.google.ads.mobile"];

        return versionToken != null && Version.TryParse(versionToken.ToString(), out var version)
            ? version
            : null;
    }

    private static void Log(string message)
    {
        Debug.Log($"[NoctuaSDK] {nameof(AndroidGradlePluginVersionManager)}: {message}");
    }

    private static void LogWarning(string message)
    {
        Debug.LogWarning($"[NoctuaSDK] {nameof(AndroidGradlePluginVersionManager)}: {message}");
    }

    private static void LogError(string message)
    {
        Debug.LogError($"[NoctuaSDK] {nameof(AndroidGradlePluginVersionManager)}: {message}");
    }
}
#endif
