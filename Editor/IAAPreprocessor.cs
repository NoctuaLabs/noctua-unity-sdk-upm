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
        CheckAndSetPreprocessorSymbols(BuildTargetGroup.Android);
        CheckAndSetPreprocessorSymbols(BuildTargetGroup.iOS);
    }

    private static void CheckAndSetPreprocessorSymbols(BuildTargetGroup targetGroup)
    {
        string defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(targetGroup);
        var defineList = defines.Split(';').Where(d => !string.IsNullOrWhiteSpace(d)).ToList();

        bool hasAdMob = Directory.Exists("Assets/GoogleMobileAds");
        bool hasAppLovin = Directory.Exists("Assets/MaxSdk");

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
