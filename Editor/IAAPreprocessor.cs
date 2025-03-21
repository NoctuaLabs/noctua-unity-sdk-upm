#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

[InitializeOnLoad]
public static class IAAPreprocessor
{
    static IAAPreprocessor()
    {
        CheckAndSetPreprocessorSymbols();
    }

    private static void CheckAndSetPreprocessorSymbols()
    {
        BuildTargetGroup targetGroup = BuildTargetGroup.Android;
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);

        // Convert defines to a list for easier manipulation
        var defineList = defines.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

        // Check if SDKs exist
        bool hasAdMob = Directory.Exists("Assets/GoogleMobileAds");
        bool hasAppLovin = Directory.Exists("Assets/MaxSdk"); // Adjusted the path

        Debug.Log($"AdMob SDK Exists: {hasAdMob}");
        Debug.Log($"AppLovin SDK Exists: {hasAppLovin}");

        // Manage AdMob define
        UpdateDefineSymbol(defineList, "UNITY_ADMOB", hasAdMob);
        UpdateDefineSymbol(defineList, "UNITY_APPLOVIN", hasAppLovin);

        // Apply changes only if there was a modification
        string newDefines = string.Join(";", defineList);
        if (newDefines != defines)
        {
            PlayerSettings.SetScriptingDefineSymbolsForGroup(targetGroup, newDefines);
            Debug.Log("Updated Preprocessor Symbols: " + newDefines);
        }
    }

    private static void UpdateDefineSymbol(System.Collections.Generic.List<string> defineList, string symbol, bool shouldExist)
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
}
#endif
