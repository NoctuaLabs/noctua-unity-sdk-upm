#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using com.noctuagames.sdk;
using System;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;

[InitializeOnLoad]
public static class BuildPreprocessor
{
    private static readonly string configPath = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
    
    static BuildPreprocessor()
    {

        var gradleVersion = GetGradleVersion(Directory.GetCurrentDirectory());
        if (gradleVersion < new Version(8, 1))
        {
            ModifyMainTemplate(Directory.GetCurrentDirectory());
        }

        // iOS: when com.unity.mobile.notifications is installed, bridge Noctua's
        // CustomAppController parent class to LocalNotificationAppController so both
        // Noctua's FCM wiring AND Unity's local-notification delivery stay active
        // (avoids the sibling IMPL_APP_CONTROLLER_SUBCLASS conflict that would
        // otherwise pick one controller arbitrarily and silently drop the other).
        SyncLocalNotificationParentDefine();

        // Load iaaEnabled flag
        bool iaaEnabled = LoadIAAFlag();

        if (!iaaEnabled)
        {
            Debug.Log("[BuildPreprocessor] iaaEnabled is false. No define symbols will be added.");
            return; // Stop here if disabled
        }

        CheckAndSetPreprocessorSymbols(BuildTargetGroup.Android);
        CheckAndSetPreprocessorSymbols(BuildTargetGroup.iOS);
    }

    private static bool LoadIAAFlag()
    {
        string configPath = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
        Debug.Log($"[BuildPreprocessor] Loading config from: {configPath}");

        try
        {
            if (!File.Exists(configPath))
            {
                Debug.LogWarning("[BuildPreprocessor] Config not found, defaulting iaaEnabled = false.");
                return false;
            }

            string jsonConfig = File.ReadAllText(configPath, Encoding.UTF8);
            var globalConfig = JsonConvert.DeserializeObject<GlobalConfig>(jsonConfig);

            if (globalConfig?.Noctua == null)
            {
                Debug.LogWarning("[BuildPreprocessor] Noctua section missing, defaulting iaaEnabled = false.");
                return false;
            }

            Debug.Log($"[BuildPreprocessor] iaaEnabled: {globalConfig.Noctua.isIAAEnabled}");
            return globalConfig.Noctua.isIAAEnabled;
        }
        catch (Exception e)
        {
            Debug.LogError($"[BuildPreprocessor] Failed to parse config: {e.Message}");
            return false; // fallback
        }
    }

    private static void CheckAndSetPreprocessorSymbols(BuildTargetGroup targetGroup)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
        var defineList = defines.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

        bool hasAdMob = IsPackageInstalled("Assets/GoogleMobileAds", "com.google.ads.mobile");
        bool hasAppLovin = IsPackageInstalled("Assets/MaxSdk", "com.applovin.mediation.ads");

        Debug.Log($"[{targetGroup}] AdMob SDK Exists: {hasAdMob}");
        Debug.Log($"[{targetGroup}] AppLovin SDK Exists: {hasAppLovin}");

        UpdateDefineSymbol(defineList, "UNITY_ADMOB", hasAdMob);
        UpdateDefineSymbol(defineList, "UNITY_APPLOVIN", hasAppLovin);

        string newDefines = string.Join(";", defineList);
        if (newDefines != defines)
        {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newDefines);
            Debug.Log($"[{targetGroup}] Updated Preprocessor Symbols: {newDefines}");
        }
    }

    /// <summary>
    /// Returns true if the SDK is installed either as a legacy asset folder
    /// (e.g. Assets/MaxSdk from a .unitypackage import) or as a UPM registry
    /// package (Library/PackageCache/&lt;packageId&gt;@*).
    /// </summary>
    private static bool IsPackageInstalled(string legacyAssetsPath, string upmPackageId)
    {
        if (Directory.Exists(legacyAssetsPath))
            return true;

        var cacheDir = Path.Combine("Library", "PackageCache");
        if (!Directory.Exists(cacheDir))
            return false;

        return Directory.GetDirectories(cacheDir, upmPackageId + "@*").Length > 0;
    }

    private static void UpdateDefineSymbol(List<string> defineList, string symbol, bool shouldExist)
    {
        if (shouldExist && !defineList.Contains(symbol))
        {
            defineList.Add(symbol);
            Debug.Log($"Added Define Symbol: {symbol}");
        }
        else if (!shouldExist && defineList.Contains(symbol))
        {
            defineList.Remove(symbol);
            Debug.Log($"Removed Define Symbol: {symbol}");
        }
    }

    public static void AddDefineSymbol(string symbol, BuildTargetGroup targetGroup)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
        var defineList = defines.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

        if (!defineList.Contains(symbol))
        {
            defineList.Add(symbol);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, string.Join(";", defineList));
            Debug.Log($"[{targetGroup}] Added define symbol: {symbol}");
        }
    }

    public static void RemoveDefineSymbol(string symbol, BuildTargetGroup targetGroup)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
        var defineList = defines.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

        if (defineList.Contains(symbol))
        {
            defineList.Remove(symbol);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, string.Join(";", defineList));
            Debug.Log($"[{targetGroup}] Removed define symbol: {symbol}");
        }
    }

    private static void ModifyMainTemplate(string projectPath)
    {
        var gradleTemplatePath = Path.Combine(projectPath, "Assets", "Plugins", "Android", "mainTemplate.gradle");

        if (!File.Exists(gradleTemplatePath))
        {
            Debug.LogWarning("mainTemplate.gradle not found — is 'Custom Main Gradle Template' enabled in Player Settings?");
            return;
        }

        var lines = File.ReadAllLines(gradleTemplatePath).ToList();
        var regex = new Regex(@"com\.noctuagames\.sdk:noctua-android-sdk:(\d+\.\d+\.\d+)");

        bool sdkUpdated = false;
        bool slf4jAdded = false;

        for (int i = 0; i < lines.Count; i++)
        {
            var match = regex.Match(lines[i]);
            if (match.Success)
            {
                var version = match.Groups[1].Value; // e.g. "0.12.4"
                var newLine = $"    implementation('com.noctuagames.sdk:noctua-android-sdk:{version}') {{ exclude group: 'org.slf4j', module: 'slf4j-api' }}";

                if (!lines[i].Contains("exclude group"))
                {
                    lines[i] = newLine;
                    sdkUpdated = true;
                    Debug.Log($"Updated Noctua SDK dependency to exclude slf4j-api (version {version}).");
                }
            }
        }

        // Add slf4j-api:1.7.36 if not already present
        if (!lines.Any(l => l.Contains("org.slf4j:slf4j-api")))
        {
            int dependenciesIndex = lines.FindIndex(l => l.Trim().StartsWith("dependencies"));
            if (dependenciesIndex != -1)
            {
                lines.Insert(dependenciesIndex + 1, "    implementation 'org.slf4j:slf4j-api:1.7.36'");
                slf4jAdded = true;
                Debug.Log("Added org.slf4j:slf4j-api:1.7.36 dependency.");
            }
        }

        if (sdkUpdated || slf4jAdded)
        {
            File.WriteAllLines(gradleTemplatePath, lines);
            Debug.Log("mainTemplate.gradle updated successfully.");
        }
        else
        {
            Debug.Log("No changes made to mainTemplate.gradle.");
        }
    }
    
    private static Version GetGradleVersion(string projectPath)
    {
        var wrapperPropertiesPath = Path.Combine(projectPath, "gradle", "wrapper", "gradle-wrapper.properties");

        if (!File.Exists(wrapperPropertiesPath))
        {
            Debug.LogWarning("gradle-wrapper.properties not found.");
            return new Version(7, 0); // Default to Gradle 7.0 if we can't find it
        }

        var versionLine = File.ReadAllLines(wrapperPropertiesPath)
            .FirstOrDefault(line => line.StartsWith("distributionUrl"));
        
        if (versionLine == null)
        {
            Debug.LogWarning("Failed to read distributionUrl from gradle-wrapper.properties. Defaulting to Gradle 7.0.");
            return new Version(7, 0); // Default to Gradle 7.0 if we can't find it
        }
        
        var match = Regex.Match(versionLine, @"gradle-(\d+\.\d+\.\d+)-bin\.zip");

        if (match.Success          &&
            match.Groups.Count > 1 &&
            Version.TryParse(match.Groups[1].Value, out var gradleVersion))
        {
            return gradleVersion;
        }

        return new Version(7, 0); // Default to Gradle 7.0 if parsing fails
    }

    private const string LocalNotificationParentSymbol = "NOCTUA_USE_LOCAL_NOTIFICATION_PARENT";
    private const string MobileNotificationsPackage    = "com.unity.mobile.notifications";

    /// <summary>
    /// Keeps the <c>NOCTUA_USE_LOCAL_NOTIFICATION_PARENT</c> iOS scripting define in sync with
    /// the presence of <c>com.unity.mobile.notifications</c> in <c>Packages/manifest.json</c>.
    /// When the package is installed, CustomAppController inherits from
    /// <c>LocalNotificationAppController</c> (chained above Unity's local-notification handler)
    /// instead of direct <c>UnityAppController</c>, preventing the sibling subclass conflict.
    /// Runs on every editor load — idempotent, safe to re-run.
    /// </summary>
    private static void SyncLocalNotificationParentDefine()
    {
        try
        {
            var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
            if (!File.Exists(manifestPath))
            {
                RemoveDefineSymbol(LocalNotificationParentSymbol, BuildTargetGroup.iOS);
                return;
            }

            var manifestText = File.ReadAllText(manifestPath);
            bool hasMobileNotifications = manifestText.Contains(MobileNotificationsPackage);

            if (hasMobileNotifications)
            {
                AddDefineSymbol(LocalNotificationParentSymbol, BuildTargetGroup.iOS);
                Debug.Log($"[BuildPreprocessor] {MobileNotificationsPackage} detected — " +
                          $"added {LocalNotificationParentSymbol} to iOS defines so " +
                          "CustomAppController bridges to LocalNotificationAppController.");
            }
            else
            {
                RemoveDefineSymbol(LocalNotificationParentSymbol, BuildTargetGroup.iOS);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BuildPreprocessor] SyncLocalNotificationParentDefine failed: {e.Message}");
        }
    }
}

#endif
