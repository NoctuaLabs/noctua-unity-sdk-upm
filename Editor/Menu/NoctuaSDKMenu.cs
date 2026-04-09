using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using com.noctuagames.sdk;
using System.Linq;

public class NoctuaIntegrationManagerWindow : EditorWindow
{
    // ── Layout constants ─────────────────────────────────────────────────
    private const float ActionW  = 82f;   // action button width
    private const float VerW     = 116f;  // version text column
    private const float MinNameW = 140f;  // minimum network name width

    // ── Button colors ────────────────────────────────────────────────────
    private static readonly Color InstallColor = new Color(0.35f, 0.72f, 0.44f);  // green
    private static readonly Color UpdateColor  = new Color(1.00f, 0.73f, 0.18f);  // amber
    private static readonly Color RemoveColor  = new Color(0.78f, 0.32f, 0.32f);  // red

    // ── Version label text colors (rich text hex) ─────────────────────
    // Up-to-date / stable:  green
    // Outdated:             amber
    // Not installed:        muted grey
    private const string ColorStable   = "#4ade80";  // green  — on recommended stable
    private const string ColorOutdated = "#fbbf24";  // amber  — installed but behind
    private const string ColorMuted    = "#9ca3af";  // grey   — not installed placeholder

    private static GUIStyle _richLabel;
    private static GUIStyle RichLabel
    {
        get
        {
            if (_richLabel == null)
            {
                _richLabel = new GUIStyle(EditorStyles.label) { richText = true };
            }
            return _richLabel;
        }
    }

    // ── Button colors (recommended section) ─────────────────────────────
    private static readonly Color RecommendedColor = new Color(0.24f, 0.52f, 0.90f);  // blue — Install Recommended

    // ── Foldout state ────────────────────────────────────────────────────
    private bool recommendedFoldout  = true;   // open by default — most important section
    private bool iaaFoldout          = false;
    private bool maxAdaptersFoldout  = false;
    private bool admobAdaptersFoldout = false;

    private Vector2 scrollPosition;
    private GlobalConfig config;

    // ── Recommended conflict-free setup ──────────────────────────────────
    // This combination has been validated to run AppLovin MAX + AdMob demand
    // simultaneously without CocoaPods version conflicts.
    //
    // Why it works:
    //   • com.google.ads.mobile 11.0.0 → pins GMA iOS ~> 13.0.0  (allows any 13.x)
    //   • AppLovin Google adapter 13.2.0.0 → requires GMA iOS = 13.2.0  (satisfies ~> 13.0.0)
    //   → No conflict. Both constraints resolve to GMA iOS 13.2.0.
    //
    // Each entry: (display name, pkg, version, note, registry)
    // registry: "applovin" | "openupm"
    private readonly List<(string name, string pkg, string ver, string note, string registry)> recommendedSetup = new()
    {
        ( "AppLovin MAX SDK",           "com.applovin.mediation.ads",                          "8.6.2",         "Primary mediation — wraps MAX SDK 13.6.2",            "applovin" ),
        ( "AdMob / GMA SDK",            "com.google.ads.mobile",                               "11.0.0",        "Compatible: GMA iOS ~> 13.0.0, Android 25.0.0",       "openupm"  ),
        ( "AppLovin → Google (Android)","com.applovin.mediation.adapters.google.android",       "25010000.0.0",  "Routes AdMob demand through AppLovin MAX (Android)",   "applovin" ),
        ( "AppLovin → Google (iOS)",    "com.applovin.mediation.adapters.google.ios",           "13020000.0.0",  "Routes AdMob demand through AppLovin MAX (iOS)",       "applovin" ),
        ( "AppLovin → Ad Manager (Android)","com.applovin.mediation.adapters.googleadmanager.android","25010000.0.0","Routes Google Ad Manager demand (Android)",       "applovin" ),
        ( "AppLovin → Ad Manager (iOS)","com.applovin.mediation.adapters.googleadmanager.ios",  "13020000.0.0",  "Routes Google Ad Manager demand (iOS)",                "applovin" ),
    };

    // Installed version per recommended package (null = not installed)
    private Dictionary<string, string> _recInstalledVer = new();

    // ── IAA provider catalog ─────────────────────────────────────────────
    private readonly Dictionary<string, (string packageName, string version)> upmPackages = new()
    {
        { "AdMob",    ("com.google.ads.mobile",      "11.0.0") },  // wraps GMA iOS 13.0.0 + Android 25.0.0
        { "AppLovin", ("com.applovin.mediation.ads", "8.6.2")  },  // wraps MAX SDK 13.6.2
    };

    // ── AppLovin MAX mediation adapter catalog ───────────────────────────
    // Versions verified stable from unity.packages.applovin.com (April 2026).
    private readonly Dictionary<string, (string androidPkg, string androidVer, string iosPkg, string iosVer)> maxAdapterPackages = new()
    {
        // Tier 1
        { "Google / AdMob",        ("com.applovin.mediation.adapters.google.android",           "25010000.0.0",  "com.applovin.mediation.adapters.google.ios",           "13020000.0.0") },
        { "Google Ad Manager",     ("com.applovin.mediation.adapters.googleadmanager.android",  "25010000.0.0",  "com.applovin.mediation.adapters.googleadmanager.ios",  "13020000.0.0") },
        { "Meta Audience Network", ("com.applovin.mediation.adapters.facebook.android",         "6210000.0.0",   "com.applovin.mediation.adapters.facebook.ios",         "6210100.0.0")  },
        { "IronSource",            ("com.applovin.mediation.adapters.ironsource.android",       "904000000.0.0", "com.applovin.mediation.adapters.ironsource.ios",       "904000000.0.0")},
        { "Unity Ads",             ("com.applovin.mediation.adapters.unityads.android",         "4170000.0.0",   "com.applovin.mediation.adapters.unityads.ios",         "4170000.0.0")  },
        // Tier 2
        { "Vungle / LiftOff",      ("com.applovin.mediation.adapters.vungle.android",           "7070100.0.0",   "com.applovin.mediation.adapters.vungle.ios",           "7070100.0.0")  },
        { "Chartboost",            ("com.applovin.mediation.adapters.chartboost.android",       "9110100.0.0",   "com.applovin.mediation.adapters.chartboost.ios",       "9110000.0.0")  },
        { "InMobi",                ("com.applovin.mediation.adapters.inmobi.android",           "11020000.0.0",  "com.applovin.mediation.adapters.inmobi.ios",           "11010100.0.0") },
        { "Mintegral",             ("com.applovin.mediation.adapters.mintegral.android",        "17011100.0.0",  "com.applovin.mediation.adapters.mintegral.ios",        "801000000.0.0")},
        { "ByteDance / Pangle",    ("com.applovin.mediation.adapters.bytedance.android",        "709010300.0.0", "com.applovin.mediation.adapters.bytedance.ios",        "709010100.0.0")},
        { "BidMachine",            ("com.applovin.mediation.adapters.bidmachine.android",       "3060100.0.0",   "com.applovin.mediation.adapters.bidmachine.ios",       "306010000.0.0")},
        // Tier 3
        { "Yandex",                ("com.applovin.mediation.adapters.yandex.android",           "7180500.0.0",   "com.applovin.mediation.adapters.yandex.ios",           "7180400.0.0")  },
        { "Fyber / DT Exchange",   ("com.applovin.mediation.adapters.fyber.android",            "8040400.0.0",   "com.applovin.mediation.adapters.fyber.ios",            "8040600.0.0")  },
        { "Smaato",                ("com.applovin.mediation.adapters.smaato.android",           "23000100.0.0",  "com.applovin.mediation.adapters.smaato.ios",           "23000100.0.0") },
        { "Verve",                 ("com.applovin.mediation.adapters.verve.android",            "3070100.0.0",   "com.applovin.mediation.adapters.verve.ios",            "3070100.0.0")  },
        { "HyprMX",                ("com.applovin.mediation.adapters.hyprmx.android",           "6040203.0.0",   "com.applovin.mediation.adapters.hyprmx.ios",           "604020000.0.0")},
        { "LINE",                  ("com.applovin.mediation.adapters.line.android",             "300000010.0.0", "com.applovin.mediation.adapters.line.ios",             "3000100.0.0")  },
        { "Moloco",                ("com.applovin.mediation.adapters.moloco.android",           "4070000.0.0",   "com.applovin.mediation.adapters.moloco.ios",           "4050100.0.0")  },
        { "PubMatic",              ("com.applovin.mediation.adapters.pubmatic.android",         "5000000.0.0",   "com.applovin.mediation.adapters.pubmatic.ios",         "5000100.0.0")  },
        { "Ogury Presage",         ("com.applovin.mediation.adapters.ogurypresage.android",     "6020200.0.0",   "com.applovin.mediation.adapters.ogurypresage.ios",     "5020100.0.0")  },
        { "MobileFuse",            ("com.applovin.mediation.adapters.mobilefuse.android",       "1110000.0.0",   "com.applovin.mediation.adapters.mobilefuse.ios",       "1110000.0.0")  },
        { "BigO Ads",              ("com.applovin.mediation.adapters.bigoads.android",          "5070100.0.0",   "com.applovin.mediation.adapters.bigoads.ios",          "5010200.0.0")  },
        { "Maio",                  ("com.applovin.mediation.adapters.maio.android",             "2000400.0.0",   "com.applovin.mediation.adapters.maio.ios",             "2010600.0.0")  },
    };

    // ── AdMob mediation adapter catalog ──────────────────────────────────
    // Versions verified stable from package.openupm.com (April 2026).
    private readonly Dictionary<string, (string pkg, string ver)> admobAdapterPackages = new()
    {
        { "AppLovin",               ("com.google.ads.mobile.mediation.applovin",            "8.7.1")  },
        { "Unity Ads",              ("com.google.ads.mobile.mediation.unity",               "3.17.0") },
        { "IronSource / LevelPlay", ("com.google.ads.mobile.mediation.ironsource",          "4.4.1")  },
        { "Chartboost",             ("com.google.ads.mobile.mediation.chartboost",          "4.11.2") },
        { "Meta Audience Network",  ("com.google.ads.mobile.mediation.metaaudiencenetwork", "3.18.3") },
        { "Liftoff / Vungle",       ("com.google.ads.mobile.mediation.liftoffmonetize",     "5.7.1")  },
        { "Pangle / ByteDance",     ("com.google.ads.mobile.mediation.pangle",              "5.9.0")  },
        { "Mintegral",              ("com.google.ads.mobile.mediation.mintegral",           "2.0.6")  },
        { "DT Exchange / Fyber",    ("com.google.ads.mobile.mediation.dtexchange",          "3.5.6")  },
        { "InMobi",                 ("com.google.ads.mobile.mediation.inmobi",              "5.0.2")  },
        { "myTarget",               ("com.google.ads.mobile.mediation.mytarget",            "3.35.0") },
        { "Moloco",                 ("com.google.ads.mobile.mediation.moloco",              "3.4.1")  },
        { "PubMatic",               ("com.google.ads.mobile.mediation.pubmatic",            "1.5.0")  },
        { "BidMachine",             ("com.google.ads.mobile.mediation.bidmachine",          "1.0.2")  },
        { "LINE",                   ("com.google.ads.mobile.mediation.line",                "2.0.2")  },
        { "Maio",                   ("com.google.ads.mobile.mediation.maio",                "3.1.6")  },
        { "i-mobile",               ("com.google.ads.mobile.mediation.imobile",             "1.3.9")  },
    };

    // ── Runtime state (includes current installed versions) ───────────────
    private Dictionary<string, (bool installed, string currentVersion, string latestVersion)> iaaProviders = new();
    // curAndroidVer / curIosVer: null = not installed
    private Dictionary<string, (bool androidInstalled, bool iosInstalled, string curAndroidVer, string curIosVer)> maxAdapterStates = new();
    private Dictionary<string, (bool installed, string currentVersion)> admobAdapterStates = new();

    // ── Menu ─────────────────────────────────────────────────────────────

    [MenuItem("Noctua/Documentation")]
    public static void OpenDocumentation() => Application.OpenURL("https://docs.noctua.gg");

    [MenuItem("Noctua/Noctua Integration Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<NoctuaIntegrationManagerWindow>(false, "Noctua Integration Manager", true);
        window.minSize = new Vector2(600, 500);
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────

    private void OnEnable()
    {
        LoadConfig();
        RefreshAllStates();
    }

    private void RefreshAllStates()
    {
        CheckIAAInstallStates();
        CheckMaxAdapterInstallStates();
        CheckAdmobAdapterInstallStates();
        CheckRecommendedSetupState();
    }

    private void CheckRecommendedSetupState()
    {
        var deps = ReadManifestDependencies();
        _recInstalledVer.Clear();
        foreach (var item in recommendedSetup)
        {
            string cur = deps != null && deps.TryGetValue(item.pkg, out var v) ? v?.ToString() : null;
            _recInstalledVer[item.pkg] = cur;
        }
    }

    // ── GUI ───────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
        EditorGUILayout.Space(6);

        DrawFoldoutSection("★ Recommended Setup — AppLovin MAX + AdMob (Conflict-Free)", ref recommendedFoldout, DrawRecommendedSection);
        EditorGUILayout.Space(8);
        DrawFoldoutSection("IAA Providers (UPM Only)", ref iaaFoldout, DrawIAASection);
        EditorGUILayout.Space(8);
        DrawFoldoutSection("AppLovin MAX — Ad Network Adapters", ref maxAdaptersFoldout, DrawMaxAdaptersSection);
        EditorGUILayout.Space(8);
        DrawFoldoutSection("AdMob — Mediation Adapters", ref admobAdaptersFoldout, DrawAdmobAdaptersSection);
        EditorGUILayout.Space(6);

        EditorGUILayout.EndScrollView();
    }

    // ── Section renderers ─────────────────────────────────────────────────

    private void DrawRecommendedSection()
    {
        // Compute overall health
        int total      = recommendedSetup.Count;
        int upToDate   = 0;
        int needsWork  = 0;
        foreach (var item in recommendedSetup)
        {
            _recInstalledVer.TryGetValue(item.pkg, out string cur);
            bool installed = cur != null;
            bool outdated  = installed && IsUpdateAvailable(cur, item.ver);
            if (installed && !outdated) upToDate++;
            else needsWork++;
        }

        bool allGood = needsWork == 0;

        // Explanation banner
        EditorGUILayout.HelpBox(
            "This combination runs AppLovin MAX and AdMob demand on BOTH Android and iOS without conflicts.\n\n" +
            "Android: Google Play Services Ads 25.1.0 is compatible with AppLovin MAX 13.6.2 adapters.\n" +
            "iOS:     AdMob SDK 11.0.0 pins GMA iOS ~> 13.0.0 — fully satisfied by the AppLovin Google adapter's requirement of GMA iOS 13.2.0.\n\n" +
            "AppLovin MAX acts as the mediation layer. AdMob demand flows through AppLovin's Google adapter, so only one dependency tree exists — no version fighting.",
            allGood ? MessageType.Info : MessageType.Warning);

        EditorGUILayout.Space(6);

        // Overall status + Install All button
        EditorGUILayout.BeginHorizontal();

        string statusText = allGood
            ? Colored("✓  All packages installed at recommended versions", ColorStable)
            : Colored($"⚠  {needsWork} of {total} package(s) need attention", ColorOutdated);
        GUILayout.Label(statusText, RichLabel, GUILayout.ExpandWidth(true));

        if (!allGood)
        {
            DrawButton("Install / Update All", RecommendedColor, 148f, () =>
            {
                InstallRecommendedSetup();
                RefreshAllStates();
            });
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space(6);

        // Per-package table
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("Package",             EditorStyles.boldLabel, GUILayout.ExpandWidth(true), GUILayout.MinWidth(MinNameW));
        GUILayout.Label("Installed",           EditorStyles.boldLabel, GUILayout.Width(VerW));
        GUILayout.Label("★ Recommended",       EditorStyles.boldLabel, GUILayout.Width(VerW));
        GUILayout.Label("Notes",               EditorStyles.boldLabel, GUILayout.ExpandWidth(true), GUILayout.MinWidth(120f));
        EditorGUILayout.EndHorizontal();

        foreach (var item in recommendedSetup)
        {
            _recInstalledVer.TryGetValue(item.pkg, out string cur);
            bool installed = cur != null;
            bool outdated  = installed && IsUpdateAvailable(cur, item.ver);

            string installedLabel   = installed
                ? Colored(cur,      outdated ? ColorOutdated : ColorStable)
                : Colored("–",      ColorMuted);
            string recommendedLabel = Colored($"★ {item.ver}", ColorStable);
            string noteLabel        = installed && !outdated
                ? Colored("✓ " + item.note, ColorStable)
                : (installed
                    ? Colored("↑ Update available — " + item.note, ColorOutdated)
                    : Colored("Not installed — " + item.note, ColorMuted));

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(item.name,      GUILayout.ExpandWidth(true), GUILayout.MinWidth(MinNameW));
            GUILayout.Label(installedLabel,   RichLabel, GUILayout.Width(VerW));
            GUILayout.Label(recommendedLabel, RichLabel, GUILayout.Width(VerW));
            GUILayout.Label(noteLabel,        RichLabel, GUILayout.ExpandWidth(true), GUILayout.MinWidth(120f));
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space(4);
        EditorGUILayout.HelpBox(
            "After installation, switch build target to iOS and run  Noctua > iOS > Fix CocoaPods Conflicts  if any CocoaPods version warning appears in the Console.",
            MessageType.None);
    }

    private void DrawIAASection()
    {
        EditorGUILayout.HelpBox(
            "★ Recommended versions are tested and verified stable for production. " +
            "Install or update to the recommended version to avoid build and runtime errors.",
            MessageType.None);
        EditorGUILayout.Space(2);

        // Header
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("Name",                EditorStyles.boldLabel, GUILayout.ExpandWidth(true), GUILayout.MinWidth(MinNameW));
        GUILayout.Label("Installed",           EditorStyles.boldLabel, GUILayout.Width(VerW));
        GUILayout.Label("★ Recommended",       EditorStyles.boldLabel, GUILayout.Width(VerW));
        GUILayout.Label("Action",              EditorStyles.boldLabel, GUILayout.Width(ActionW * 2 + 4));
        EditorGUILayout.EndHorizontal();

        foreach (var provider in new List<string>(iaaProviders.Keys))
        {
            var kv = iaaProviders[provider];
            DrawPackageRow(
                provider,
                kv.installed, kv.currentVersion, kv.latestVersion,
                onInstall: () => { AddPackageToManifest(provider); CheckIAAInstallStates(); },
                onUpdate:  () => { AddPackageToManifest(provider); CheckIAAInstallStates(); },
                onRemove:  () => { RemovePackageFromManifest(provider); CheckIAAInstallStates(); });
        }
    }

    private void DrawMaxAdaptersSection()
    {
        EditorGUILayout.HelpBox(
            "★ Recommended versions are tested with AppLovin MAX SDK 8.6.2. " +
            "Install or update to recommended to prevent CocoaPods conflicts and ad-fill issues.",
            MessageType.None);
        EditorGUILayout.Space(2);

        // Header
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("Network",             EditorStyles.boldLabel, GUILayout.ExpandWidth(true), GUILayout.MinWidth(MinNameW));
        GUILayout.Label("Installed",           EditorStyles.boldLabel, GUILayout.Width(VerW));
        GUILayout.Label("★ Recommended",       EditorStyles.boldLabel, GUILayout.Width(VerW));
        GUILayout.Label("Action",              EditorStyles.boldLabel, GUILayout.Width(ActionW * 2 + 4));
        EditorGUILayout.EndHorizontal();

        foreach (var name in new List<string>(maxAdapterPackages.Keys))
        {
            if (!maxAdapterStates.TryGetValue(name, out var s))
                s = (false, false, null, null);

            var pkg = maxAdapterPackages[name];
            bool anyInstalled = s.androidInstalled || s.iosInstalled;

            string displayCurrent = (s.androidInstalled && s.iosInstalled)
                ? OlderVersion(s.curAndroidVer, s.curIosVer)
                : (s.androidInstalled ? s.curAndroidVer : s.curIosVer);

            DrawPackageRow(
                name,
                anyInstalled, displayCurrent, pkg.androidVer,
                onInstall: () => AddMaxAdapterToManifest(name),
                onUpdate:  () => AddMaxAdapterToManifest(name),
                onRemove:  () => RemoveMaxAdapterFromManifest(name));
        }
    }

    private void DrawAdmobAdaptersSection()
    {
        EditorGUILayout.HelpBox(
            "★ Recommended versions are tested with AdMob SDK 11.0.0 (GMA iOS 13.0 / Android 25.0). " +
            "Update to recommended to avoid mediation errors and rejected store submissions.",
            MessageType.None);
        EditorGUILayout.Space(2);

        // Header
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("Network",             EditorStyles.boldLabel, GUILayout.ExpandWidth(true), GUILayout.MinWidth(MinNameW));
        GUILayout.Label("Installed",           EditorStyles.boldLabel, GUILayout.Width(VerW));
        GUILayout.Label("★ Recommended",       EditorStyles.boldLabel, GUILayout.Width(VerW));
        GUILayout.Label("Action",              EditorStyles.boldLabel, GUILayout.Width(ActionW * 2 + 4));
        EditorGUILayout.EndHorizontal();

        foreach (var name in new List<string>(admobAdapterPackages.Keys))
        {
            if (!admobAdapterStates.TryGetValue(name, out var s))
                s = (false, null);

            DrawPackageRow(
                name,
                s.installed, s.currentVersion, admobAdapterPackages[name].ver,
                onInstall: () => AddAdmobAdapterToManifest(name),
                onUpdate:  () => AddAdmobAdapterToManifest(name),
                onRemove:  () => RemoveAdmobAdapterFromManifest(name));
        }
    }

    // ── Unified row renderer ──────────────────────────────────────────────

    /// <summary>
    /// Single row renderer used by all three sections.
    /// <para>Installed version is shown amber when outdated, green when at recommended, grey when not installed.</para>
    /// <para>Recommended version is always shown green with a ★ prefix to indicate it is the stable target.</para>
    /// </summary>
    private void DrawPackageRow(
        string label,
        bool isInstalled, string currentVer, string recommendedVer,
        Action onInstall, Action onUpdate, Action onRemove)
    {
        bool canUpdate = isInstalled && IsUpdateAvailable(currentVer, recommendedVer);

        // Version label markup
        string installedLabel = isInstalled
            ? Colored(currentVer ?? "-", canUpdate ? ColorOutdated : ColorStable)
            : Colored("–", ColorMuted);

        // Recommended version — always green with a ★ to reinforce it is the stable target
        string recommendedLabel = Colored($"★ {recommendedVer ?? "-"}", ColorStable);

        EditorGUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.ExpandWidth(true), GUILayout.MinWidth(MinNameW));

        GUILayout.Label(installedLabel,   RichLabel, GUILayout.Width(VerW));
        GUILayout.Label(recommendedLabel, RichLabel, GUILayout.Width(VerW));

        if (!isInstalled)
        {
            DrawButton("Install", InstallColor, ActionW, onInstall);
            GUILayout.Space(ActionW + 4);
        }
        else if (canUpdate)
        {
            DrawButton("→ Stable", UpdateColor, ActionW, onUpdate);
            DrawButton("Remove",   RemoveColor, ActionW, onRemove);
        }
        else
        {
            GUILayout.Space(ActionW + 4);
            DrawButton("Remove", RemoveColor, ActionW, onRemove);
        }
        EditorGUILayout.EndHorizontal();
    }

    // ── Shared draw primitive ─────────────────────────────────────────────

    private static void DrawButton(string label, Color color, float width, Action onClick)
    {
        var prev = GUI.backgroundColor;
        GUI.backgroundColor = color;
        if (GUILayout.Button(label, GUILayout.Width(width)))
            onClick?.Invoke();
        GUI.backgroundColor = prev;
    }

    /// <summary>Wraps <paramref name="text"/> in a Unity rich-text color tag.</summary>
    private static string Colored(string text, string hexColor) =>
        $"<color={hexColor}>{text}</color>";

    private void DrawFoldoutSection(string title, ref bool state, Action content)
    {
        state = EditorGUILayout.Foldout(state, title, true, EditorStyles.foldoutHeader);
        if (!state) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.Space(2);
        content.Invoke();
        EditorGUILayout.Space(2);
        EditorGUILayout.EndVertical();
    }

    // ── State refresh ─────────────────────────────────────────────────────

    private void LoadConfig()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");
        if (!File.Exists(path)) return;
        try
        {
            config = Newtonsoft.Json.JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText(path));
        }
        catch (Exception ex)
        {
            Debug.LogError("[NoctuaSDK] Failed to load noctuagg.json: " + ex.Message);
        }
    }

    private void CheckIAAInstallStates()
    {
        var deps = ReadManifestDependencies();
        iaaProviders.Clear();
        foreach (var kvp in upmPackages)
        {
            string pkg = kvp.Value.packageName;
            if (deps != null && deps.TryGetValue(pkg, out var vt))
                iaaProviders[kvp.Key] = (true, vt?.ToString() ?? "-", kvp.Value.version);
            else
                iaaProviders[kvp.Key] = (false, "-", kvp.Value.version);
        }
    }

    private void CheckMaxAdapterInstallStates()
    {
        var deps = ReadManifestDependencies();
        maxAdapterStates.Clear();
        foreach (var kvp in maxAdapterPackages)
        {
            string curA = deps != null && deps.TryGetValue(kvp.Value.androidPkg, out var av) ? av?.ToString() : null;
            string curI = deps != null && deps.TryGetValue(kvp.Value.iosPkg,     out var iv) ? iv?.ToString() : null;
            maxAdapterStates[kvp.Key] = (curA != null, curI != null, curA, curI);
        }
    }

    private void CheckAdmobAdapterInstallStates()
    {
        var deps = ReadManifestDependencies();
        admobAdapterStates.Clear();
        foreach (var kvp in admobAdapterPackages)
        {
            string curV = deps != null && deps.TryGetValue(kvp.Value.pkg, out var pv) ? pv?.ToString() : null;
            admobAdapterStates[kvp.Key] = (curV != null, curV);
        }
    }

    // ── Recommended setup installer ───────────────────────────────────────

    /// <summary>
    /// Installs or upgrades all packages in the recommended conflict-free setup in one manifest write.
    /// Both Android and iOS packages are handled; appropriate scoped registries are added.
    /// </summary>
    private void InstallRecommendedSetup()
    {
        if (!TryLoadManifest(out var manifest, out var deps)) return;

        if (manifest["scopedRegistries"] is not JArray regs)
        {
            regs = new JArray();
            manifest["scopedRegistries"] = regs;
        }

        // Ensure both scoped registries are present
        AddScopedRegistryIfMissing(regs, "AppLovin MAX Unity", "https://unity.packages.applovin.com/",
            new[] { "com.applovin.mediation.ads", "com.applovin.mediation.adapters", "com.applovin.mediation.dsp" });
        AddScopedRegistryIfMissing(regs, "package.openupm.com", "https://package.openupm.com",
            new[] { "com.google.ads.mobile", "com.google.external-dependency-manager" });

        bool changed = false;
        foreach (var item in recommendedSetup)
            changed |= SetVersion(deps, item.pkg, item.ver);

        if (changed)
        {
            WriteManifest(manifest);
            Debug.Log("[NoctuaSDK] Recommended conflict-free setup installed/updated.");
            AssetDatabase.Refresh();
        }
        else
        {
            Debug.Log("[NoctuaSDK] Recommended setup already up to date — no changes made.");
        }
    }

    // ── Manifest operations ───────────────────────────────────────────────

    private JObject ReadManifestDependencies()
    {
        string path = ManifestPath();
        if (!File.Exists(path)) return null;
        var manifest = JObject.Parse(File.ReadAllText(path));
        return manifest["dependencies"] as JObject;
    }

    private void AddPackageToManifest(string provider)
    {
        if (!upmPackages.ContainsKey(provider)) return;

        if (!TryLoadManifest(out var manifest, out var deps)) return;

        var (packageName, version) = upmPackages[provider];

        if (manifest["scopedRegistries"] is not JArray scopedRegistries)
        {
            scopedRegistries = new JArray();
            manifest["scopedRegistries"] = scopedRegistries;
        }

        if (provider == "AppLovin")
            AddScopedRegistryIfMissing(scopedRegistries, "AppLovin MAX Unity", "https://unity.packages.applovin.com/",
                new[] { "com.applovin.mediation.ads", "com.applovin.mediation.adapters", "com.applovin.mediation.dsp" });
        else if (provider == "AdMob")
            AddScopedRegistryIfMissing(scopedRegistries, "package.openupm.com", "https://package.openupm.com",
                new[] { "com.google.ads.mobile", "com.google.external-dependency-manager" });

        bool changed = SetVersion(deps, packageName, version);
        if (changed)
        {
            WriteManifest(manifest);
            Debug.Log($"[NoctuaSDK] {provider} ({packageName}) → {version}");
            AssetDatabase.Refresh();
        }
    }

    private void RemovePackageFromManifest(string provider)
    {
        if (!upmPackages.ContainsKey(provider)) return;
        if (!TryLoadManifest(out var manifest, out var deps)) return;

        var (packageName, _) = upmPackages[provider];
        bool changed = false;

        if (deps.ContainsKey(packageName)) { deps.Remove(packageName); changed = true; }

        JArray scopedRegistries = manifest["scopedRegistries"] as JArray;
        if (scopedRegistries != null)
        {
            if (provider == "AppLovin")
                RemoveUnusedScopedRegistry(scopedRegistries, deps, "https://unity.packages.applovin.com/");
            else if (provider == "AdMob")
                RemoveUnusedScopedRegistry(scopedRegistries, deps, "https://package.openupm.com");
        }

        if (changed) WriteManifest(manifest);
        AssetDatabase.Refresh();

        if (provider == "AppLovin")
        {
            BuildPreprocessor.RemoveDefineSymbol("UNITY_APPLOVIN", BuildTargetGroup.Android);
            BuildPreprocessor.RemoveDefineSymbol("UNITY_APPLOVIN", BuildTargetGroup.iOS);
        }
        else
        {
            BuildPreprocessor.RemoveDefineSymbol("UNITY_ADMOB", BuildTargetGroup.Android);
            BuildPreprocessor.RemoveDefineSymbol("UNITY_ADMOB", BuildTargetGroup.iOS);
        }
    }

    private void AddMaxAdapterToManifest(string name)
    {
        if (!maxAdapterPackages.ContainsKey(name)) return;
        if (!TryLoadManifest(out var manifest, out var deps)) return;

        if (manifest["scopedRegistries"] is not JArray regs) { regs = new JArray(); manifest["scopedRegistries"] = regs; }
        AddScopedRegistryIfMissing(regs, "AppLovin MAX Unity", "https://unity.packages.applovin.com/",
            new[] { "com.applovin.mediation.ads", "com.applovin.mediation.adapters", "com.applovin.mediation.dsp" });

        var (androidPkg, androidVer, iosPkg, iosVer) = maxAdapterPackages[name];
        bool changed = SetVersion(deps, androidPkg, androidVer) | SetVersion(deps, iosPkg, iosVer);

        if (changed)
        {
            WriteManifest(manifest);
            Debug.Log($"[NoctuaSDK] AppLovin MAX adapter '{name}' → {androidVer} / {iosVer}");
            AssetDatabase.Refresh();
        }
        CheckMaxAdapterInstallStates();
    }

    private void RemoveMaxAdapterFromManifest(string name)
    {
        if (!maxAdapterPackages.ContainsKey(name)) return;
        if (!TryLoadManifest(out var manifest, out var deps)) return;

        var (androidPkg, _, iosPkg, _) = maxAdapterPackages[name];
        bool changed = false;
        if (deps.ContainsKey(androidPkg)) { deps.Remove(androidPkg); changed = true; }
        if (deps.ContainsKey(iosPkg))     { deps.Remove(iosPkg);     changed = true; }

        if (changed)
        {
            if (manifest["scopedRegistries"] is JArray regs)
                RemoveUnusedScopedRegistry(regs, deps, "https://unity.packages.applovin.com/");
            WriteManifest(manifest);
            AssetDatabase.Refresh();
        }
        CheckMaxAdapterInstallStates();
    }

    private void AddAdmobAdapterToManifest(string name)
    {
        if (!admobAdapterPackages.ContainsKey(name)) return;
        if (!TryLoadManifest(out var manifest, out var deps)) return;

        if (manifest["scopedRegistries"] is not JArray regs) { regs = new JArray(); manifest["scopedRegistries"] = regs; }
        AddScopedRegistryIfMissing(regs, "package.openupm.com", "https://package.openupm.com",
            new[] { "com.google.ads.mobile", "com.google.external-dependency-manager" });

        var (pkg, ver) = admobAdapterPackages[name];
        bool changed = SetVersion(deps, pkg, ver);

        if (changed)
        {
            WriteManifest(manifest);
            Debug.Log($"[NoctuaSDK] AdMob adapter '{name}' ({pkg}) → {ver}");
            AssetDatabase.Refresh();
        }
        CheckAdmobAdapterInstallStates();
    }

    private void RemoveAdmobAdapterFromManifest(string name)
    {
        if (!admobAdapterPackages.ContainsKey(name)) return;
        if (!TryLoadManifest(out var manifest, out var deps)) return;

        var (pkg, _) = admobAdapterPackages[name];
        if (!deps.ContainsKey(pkg)) { CheckAdmobAdapterInstallStates(); return; }

        deps.Remove(pkg);
        if (manifest["scopedRegistries"] is JArray regs)
            RemoveUnusedScopedRegistry(regs, deps, "https://package.openupm.com");

        WriteManifest(manifest);
        AssetDatabase.Refresh();
        CheckAdmobAdapterInstallStates();
    }

    // ── Manifest utilities ────────────────────────────────────────────────

    private static string ManifestPath() =>
        Path.Combine(Application.dataPath, "../Packages/manifest.json");

    private static bool TryLoadManifest(out JObject manifest, out JObject deps)
    {
        manifest = null; deps = null;
        string path = ManifestPath();
        if (!File.Exists(path)) return false;
        manifest = JObject.Parse(File.ReadAllText(path));
        deps = manifest["dependencies"] as JObject;
        if (deps == null) { Debug.LogError("[NoctuaSDK] manifest.json has no 'dependencies' key."); return false; }
        return true;
    }

    private static void WriteManifest(JObject manifest) =>
        File.WriteAllText(ManifestPath(), manifest.ToString(Newtonsoft.Json.Formatting.Indented));

    /// <summary>Sets <paramref name="pkg"/> to <paramref name="ver"/> in deps.
    /// Returns true if the value actually changed.</summary>
    private static bool SetVersion(JObject deps, string pkg, string ver)
    {
        if (deps.TryGetValue(pkg, out var existing) && existing?.ToString() == ver)
            return false;   // already at target version
        deps[pkg] = ver;
        return true;
    }

    private void AddScopedRegistryIfMissing(JArray scopedRegistries, string name, string url, string[] scopes)
    {
        if (scopedRegistries.Any(r => r["url"]?.ToString() == url)) return;
        scopedRegistries.Add(new JObject { ["name"] = name, ["url"] = url, ["scopes"] = new JArray(scopes) });
        Debug.Log($"[NoctuaSDK] Added scoped registry '{name}'.");
    }

    private void RemoveUnusedScopedRegistry(JArray scopedRegistries, JObject dependencies, string url)
    {
        for (int i = scopedRegistries.Count - 1; i >= 0; i--)
        {
            var reg = scopedRegistries[i] as JObject;
            if (reg == null || reg["url"]?.ToString() != url) continue;
            JArray scopes = reg["scopes"] as JArray;
            if (scopes == null) continue;
            bool stillUsed = scopes.Any(scope =>
                dependencies.Properties().Any(dep => dep.Name.StartsWith(scope.ToString())));
            if (!stillUsed) { scopedRegistries.RemoveAt(i); Debug.Log($"[NoctuaSDK] Removed scoped registry '{url}'."); }
        }
    }

    // ── Version helpers ───────────────────────────────────────────────────

    /// <summary>Returns true when <paramref name="latestVersion"/> is newer than
    /// <paramref name="currentVersion"/>. Compares each dot-separated component as an integer.</summary>
    private static bool IsUpdateAvailable(string currentVersion, string latestVersion)
    {
        if (string.IsNullOrEmpty(currentVersion) || currentVersion == "-") return false;
        if (string.IsNullOrEmpty(latestVersion))  return false;

        var cur = currentVersion.Split('.');
        var lat = latestVersion.Split('.');
        int len = Math.Max(cur.Length, lat.Length);

        for (int i = 0; i < len; i++)
        {
            int c = i < cur.Length && int.TryParse(cur[i], out int cv) ? cv : 0;
            int l = i < lat.Length && int.TryParse(lat[i], out int lv) ? lv : 0;
            if (l > c) return true;
            if (l < c) return false;
        }
        return false;
    }

    /// <summary>Returns the older of two version strings (for display when two platforms installed).</summary>
    private static string OlderVersion(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b;
        if (string.IsNullOrEmpty(b)) return a;
        return IsUpdateAvailable(a, b) ? a : b;
    }
}
