#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Callbacks;
using UnityEngine;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif

namespace com.noctuagames.sdk.Editor.Build
{
    /// <summary>
    /// Opt-in Swift Package Manager integration of the native Noctua iOS SDK.
    ///
    /// Switch: <b>Noctua → iOS → Dependency Manager</b> (CocoaPods by default), stored in
    /// <c>ProjectSettings/NoctuaIosDependencySettings.json</c>; env <c>NOCTUA_IOS_DEPENDENCY_MANAGER</c>
    /// (<c>cocoapods</c> | <c>spm</c>) overrides it for CI. In CocoaPods mode nothing here runs.
    ///
    /// SPM mode (ad SDKs and adapters stay on CocoaPods):
    /// <list type="bullet">
    /// <item>strips the <c>NoctuaSDK/*</c> pods from the EDM4U-generated Podfile;</item>
    /// <item>links <c>NoctuaSDK</c> into UnityFramework and the code-free <c>NoctuaSDKDynamicFrameworks</c>
    /// into Unity-iPhone, so Xcode embeds the dynamic binaries (Facebook, AdjustSignature);</item>
    /// <item>fails the build if Podfile.lock still pulls a pod the package already provides.</item>
    /// </list>
    /// <c>NOCTUA_SPM_PACKAGE_URL</c> / <c>NOCTUA_SPM_PACKAGE_VERSION</c> override the package source.
    /// </summary>
    public static class NoctuaSpmIntegration
    {
        public const string SettingsPath = "ProjectSettings/NoctuaIosDependencySettings.json";
        public const string DefaultPackageUrl = "https://github.com/NoctuaLabs/noctua-native-sdk-ios.git";
        public const string DefaultPackageVersion = "0.6.0"; // noctua-native-sdk-ios 0.6.0 = native iOS SDK 0.40.1

        private const string ModeEnv = "NOCTUA_IOS_DEPENDENCY_MANAGER";
        private const string UrlEnv = "NOCTUA_SPM_PACKAGE_URL";
        private const string VersionEnv = "NOCTUA_SPM_PACKAGE_VERSION";
        private const string SettingsKey = "iosDependencyManager";
        private const string SpmValue = "spm";
        private const string CocoaPodsValue = "cocoapods";

        private const string MenuCocoaPods = "Noctua/iOS/Dependency Manager/CocoaPods";
        private const string MenuSpm = "Noctua/iOS/Dependency Manager/Swift Package Manager";

        // EDM4U IOSResolver generates the Podfile at order 40 and runs `pod install` at 50.
        private const int AddPackageOrder = 3;
        private const int StripPodsOrder = 41;
        private const int VerifyPodsOrder = int.MaxValue - 3;

        // Pods the NoctuaSDK Swift package already provides; linking them again duplicates symbols.
        private static readonly HashSet<string> SpmProvidedPods = new HashSet<string>(StringComparer.Ordinal)
        {
            "NoctuaSDK", "Adjust", "AdjustSignature", "GoogleAdsOnDeviceConversion",
            "GoogleUtilities", "GoogleDataTransport", "GoogleAppMeasurement",
            "nanopb", "PromisesObjC", "PromisesSwift", "FBAEMKit",
        };

        private static readonly string[] SpmProvidedPodPrefixes = { "Firebase", "FBSDK" };

        private static readonly Regex NoctuaPodLine =
            new Regex(@"^\s*pod\s+['""]NoctuaSDK(/[^'""]*)?['""].*(\r?\n|$)", RegexOptions.Multiline);

        // Top-level PODS: entries, e.g. `  - FirebaseCore (12.2.0):` or `  - "nanopb/decode (3.30910.0)"`.
        private static readonly Regex LockPodEntry = new Regex(@"^  - ""?([^\s/""(]+)", RegexOptions.Multiline);

        // ── Mode ────────────────────────────────────────────────────────────

        public static bool IsSpmEnabled() => ResolveSpm(
            Environment.GetEnvironmentVariable(ModeEnv),
            File.Exists(SettingsPath) ? File.ReadAllText(SettingsPath) : null);

        /// <summary>Env value wins over the settings file; anything unrecognised means CocoaPods.</summary>
        public static bool ResolveSpm(string envValue, string settingsJson)
        {
            var fromEnv = Normalize(envValue);
            if (fromEnv == SpmValue || fromEnv == CocoaPodsValue) return fromEnv == SpmValue;
            if (string.IsNullOrWhiteSpace(settingsJson)) return false;

            try
            {
                return Normalize(JObject.Parse(settingsJson)[SettingsKey]?.ToString()) == SpmValue;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[NoctuaSPM] Invalid {SettingsPath}, using CocoaPods: {e.Message}");
                return false;
            }
        }

        private static string Normalize(string value) => value?.Trim().ToLowerInvariant();

        // ── Menu switch ─────────────────────────────────────────────────────

        [MenuItem(MenuCocoaPods, false, 310)]
        public static void UseCocoaPods() => SetSpmEnabled(false);

        [MenuItem(MenuSpm, false, 311)]
        public static void UseSpm() => SetSpmEnabled(true);

        [MenuItem(MenuCocoaPods, true)]
        [MenuItem(MenuSpm, true)]
        public static bool RefreshMenuChecks()
        {
            var spm = IsSpmEnabled();
            Menu.SetChecked(MenuCocoaPods, !spm);
            Menu.SetChecked(MenuSpm, spm);
            return true;
        }

        private static void SetSpmEnabled(bool spm)
        {
            var value = spm ? SpmValue : CocoaPodsValue;
            File.WriteAllText(SettingsPath, new JObject { [SettingsKey] = value } + "\n");
            Debug.Log($"[NoctuaSPM] iOS dependency manager set to '{value}' ({SettingsPath}).");

            var envOverride = Environment.GetEnvironmentVariable(ModeEnv);
            EditorUtility.DisplayDialog(
                "Noctua iOS Dependency Manager",
                (spm
                    ? "Swift Package Manager selected. Ad SDKs and adapters stay on CocoaPods."
                    : "CocoaPods selected.") +
                "\n\nUse a clean (Replace) build folder when switching." +
                (string.IsNullOrWhiteSpace(envOverride) ? "" : $"\n\nNote: {ModeEnv}={envOverride} overrides this setting."),
                "OK");
        }

        // ── Podfile / Podfile.lock ──────────────────────────────────────────

        /// <summary>Returns the Podfile content with every <c>pod 'NoctuaSDK…'</c> line removed.</summary>
        public static string StripNoctuaPods(string podfile) => NoctuaPodLine.Replace(podfile, string.Empty);

        /// <summary>Pods in a Podfile.lock that the NoctuaSDK Swift package already provides.</summary>
        public static IReadOnlyList<string> FindSpmProvidedPods(string podfileLock)
        {
            if (string.IsNullOrEmpty(podfileLock)) return Array.Empty<string>();

            var start = podfileLock.IndexOf("PODS:", StringComparison.Ordinal);
            if (start < 0) return Array.Empty<string>();
            var end = podfileLock.IndexOf("\nDEPENDENCIES:", start, StringComparison.Ordinal);
            var podsSection = end < 0 ? podfileLock.Substring(start) : podfileLock.Substring(start, end - start);

            return LockPodEntry.Matches(podsSection)
                .Cast<Match>()
                .Select(m => m.Groups[1].Value)
                .Where(name => SpmProvidedPods.Contains(name) ||
                               SpmProvidedPodPrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
                .Distinct()
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToList();
        }

        // ── Post-build hooks (SPM mode only) ────────────────────────────────

        [PostProcessBuild(StripPodsOrder)]
        public static void StripNoctuaPodsFromPodfile(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS || !IsSpmEnabled()) return;

            var podfilePath = Path.Combine(pathToBuiltProject, "Podfile");
            if (!File.Exists(podfilePath))
            {
                Debug.LogWarning("[NoctuaSPM] Podfile not found, nothing to strip: " + podfilePath);
                return;
            }

            var original = File.ReadAllText(podfilePath);
            var stripped = StripNoctuaPods(original);
            if (stripped == original) return;

            File.WriteAllText(podfilePath, stripped);
            Debug.Log("[NoctuaSPM] Removed NoctuaSDK pods from Podfile (provided by Swift Package Manager).");
        }

        [PostProcessBuild(VerifyPodsOrder)]
        public static void VerifyNoDuplicatePods(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS || !IsSpmEnabled()) return;

            var lockPath = Path.Combine(pathToBuiltProject, "Podfile.lock");
            if (!File.Exists(lockPath)) return; // no pods installed at all → nothing can clash

            var clashes = FindSpmProvidedPods(File.ReadAllText(lockPath));
            if (clashes.Count == 0) return;

            throw new BuildFailedException(
                "[NoctuaSPM] Swift Package Manager mode, but Podfile.lock also installs pods that the NoctuaSDK " +
                $"Swift package already provides: {string.Join(", ", clashes)}. Linking both duplicates symbols " +
                "and can crash at runtime. Remove the dependency that pulls these pods, or switch back via " +
                "Noctua → iOS → Dependency Manager → CocoaPods.");
        }

#if UNITY_IOS
        [PostProcessBuild(AddPackageOrder)]
        public static void AddNoctuaSwiftPackage(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.iOS || !IsSpmEnabled()) return;

            var url = EnvOrDefault(UrlEnv, DefaultPackageUrl);
            var version = EnvOrDefault(VersionEnv, DefaultPackageVersion);

            var projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);

            var mainTarget = project.GetUnityMainTargetGuid();
            var frameworkTarget = project.GetUnityFrameworkTargetGuid();
            var packageGuid = project.AddRemotePackageReferenceAtVersion(url, version);

            // NoctuaSDK is static: linking it into Unity-iPhone too would duplicate Firebase/Adjust
            // (the 2025 rollback cause). The app links only the code-free embed product.
            project.AddRemotePackageFrameworkToProject(frameworkTarget, "NoctuaSDK", packageGuid, false);
            project.AddRemotePackageFrameworkToProject(mainTarget, "NoctuaSDKDynamicFrameworks", packageGuid, false);

            project.SetBuildProperty(frameworkTarget, "CLANG_ENABLE_MODULES", "YES");
            // Firebase's SPM guide requires -ObjC so registration classes in static libs load.
            var ldFlags = project.GetBuildPropertyForAnyConfig(frameworkTarget, "OTHER_LDFLAGS") ?? string.Empty;
            if (!ldFlags.Split(' ').Contains("-ObjC")) project.AddBuildProperty(frameworkTarget, "OTHER_LDFLAGS", "-ObjC");

            project.WriteToFile(projectPath);
            Debug.Log($"[NoctuaSPM] Linked Swift package NoctuaSDK {version} ({url}) into UnityFramework " +
                      "and NoctuaSDKDynamicFrameworks into Unity-iPhone.");
        }
#endif

        private static string EnvOrDefault(string name, string fallback)
        {
            var value = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
#endif
