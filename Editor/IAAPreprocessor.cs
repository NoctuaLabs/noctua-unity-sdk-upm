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

[InitializeOnLoad]
public static class IAAPreprocessor
{
    private static readonly string configPath = 
        Path.Combine(Application.streamingAssetsPath, "noctuagg.json");

    static IAAPreprocessor()
    {
        // Load iaaEnabled flag
        bool iaaEnabled = LoadIAAFlag();

        if (!iaaEnabled)
        {
            Debug.Log("[IAAPreprocessor] iaaEnabled is false. No define symbols will be added.");
            return; // Stop here if disabled
        }

        CheckAndSetPreprocessorSymbols(BuildTargetGroup.Android);
        CheckAndSetPreprocessorSymbols(BuildTargetGroup.iOS);
    }

    private static bool LoadIAAFlag()
    {
        string configPath = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
        Debug.Log($"[IAAPreprocessor] Loading config from: {configPath}");

        try
        {
            if (!File.Exists(configPath))
            {
                Debug.LogWarning("[IAAPreprocessor] Config not found, defaulting iaaEnabled = false.");
                return false;
            }

            string jsonConfig = File.ReadAllText(configPath, Encoding.UTF8);
            var globalConfig = JsonConvert.DeserializeObject<GlobalConfig>(jsonConfig);

            if (globalConfig?.Noctua == null)
            {
                Debug.LogWarning("[IAAPreprocessor] Noctua section missing, defaulting iaaEnabled = false.");
                return false;
            }

            Debug.Log($"[IAAPreprocessor] iaaEnabled: {globalConfig.Noctua.isIAAEnabled}");
            return globalConfig.Noctua.isIAAEnabled;
        }
        catch (Exception e)
        {
            Debug.LogError($"[IAAPreprocessor] Failed to parse config: {e.Message}");
            return false; // fallback
        }
    }

    private static void CheckAndSetPreprocessorSymbols(BuildTargetGroup targetGroup)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
        var defineList = defines.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

        bool hasAdMob = Directory.Exists("Assets/GoogleMobileAds") ||
                        Directory.Exists("Packages/com.google.ads.mobile/GoogleMobileAds");

        bool hasAppLovin = Directory.Exists("Assets/MaxSdk") || 
                           Directory.Exists("Packages/com.applovin.mediation.ads");

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

    public static void RemoveDefineSymbol(string symbol, BuildTargetGroup targetGroup)
    {
        Debug.Log($"Removing define symbol: {symbol} for target group: {targetGroup}");

        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
        var defineList = defines.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

        if (defineList.Contains(symbol))
        {
            defineList.Remove(symbol);
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, string.Join(";", defineList));
            Debug.Log($"[{targetGroup}] Manually removed define symbol: {symbol}");
        }
    }
}

#endif
