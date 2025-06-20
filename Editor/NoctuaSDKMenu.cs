using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using com.noctuagames.sdk;

public class NoctuaIntegrationManagerWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private GlobalConfig config;

    private bool iaaFoldout = true;

    private readonly Dictionary<string, (string packageName, string version)> upmPackages = new()
    {
        { "AdMob", ("com.google.ads.mobile", "-") },
        { "AppLovin", ("com.applovin.mediation.ads", "-") }
    };

    private Dictionary<string, (bool installed, string currentVersion, string latestVersion)> iaaProviders = new();

    [MenuItem("Noctua/Documentation")]
    public static void OpenDocumentation()
    {
        Application.OpenURL("https://docs.noctua.gg");
    }

    [MenuItem("Noctua/Noctua Integration Manager")]
    public static void ShowWindow()
    {
        var window = GetWindow<NoctuaIntegrationManagerWindow>(false, "Noctua Integration Manager", true);
        window.minSize = new Vector2(900, 600);
    }

    private void OnEnable()
    {
        LoadConfig();
        CheckIAAInstallStates();
    }

    private void OnGUI()
    {
        scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);

        DrawHeader("Noctua Config Information");
        if (config != null)
        {
            EditorGUILayout.BeginVertical("box");
            DrawLabel("Client ID", config.ClientId);
            EditorGUILayout.EndVertical();
        }
        else
        {
            EditorGUILayout.HelpBox("Failed to load noctuagg.json", MessageType.Warning);
        }

        EditorGUILayout.Space(10);

        DrawFoldoutSection("IAA Providers (UPM Only)", ref iaaFoldout, () =>
        {
            DrawTableHeader();

            // Clone keys for safe iteration during dictionary modification
            var providerKeys = new List<string>(iaaProviders.Keys);

            foreach (var provider in providerKeys)
            {
                var kv = iaaProviders[provider];
                bool isInstalled = kv.installed;
                string currentVersion = kv.currentVersion;
                string latestVersion = kv.latestVersion;

                DrawProviderRow(provider, isInstalled, currentVersion, latestVersion,
                    () =>
                    {
                        AddPackageToManifest(provider);
                        iaaProviders[provider] = (true, latestVersion, latestVersion);
                    },
                    () =>
                    {
                        RemovePackageFromManifest(provider);
                        iaaProviders[provider] = (false, "-", latestVersion);
                    });
            }
        });

        EditorGUILayout.EndScrollView();
    }

    private void LoadConfig()
    {
        string path = Path.Combine(Application.streamingAssetsPath, "noctuagg.json");

        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                config = Newtonsoft.Json.JsonConvert.DeserializeObject<GlobalConfig>(json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("Failed to load noctuagg.json: " + ex.Message);
            }
        }
    }

    private void CheckIAAInstallStates()
    {
        string manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
        if (!File.Exists(manifestPath)) return;

        JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
        var dependencies = manifest["dependencies"] as JObject;

        iaaProviders.Clear();

        foreach (var kvp in upmPackages)
        {
            string provider = kvp.Key;
            string packageName = kvp.Value.packageName;

            if (dependencies.TryGetValue(packageName, out var versionToken))
            {
                iaaProviders[provider] = (true, versionToken?.ToString() ?? "-", kvp.Value.version);
            }
            else
            {
                iaaProviders[provider] = (false, "-", kvp.Value.version);
            }
        }
    }

    private void AddPackageToManifest(string provider)
    {
        if (!upmPackages.ContainsKey(provider)) return;

        string manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
        JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
        JObject dependencies = (JObject)manifest["dependencies"];

        var (packageName, version) = upmPackages[provider];
        if (!dependencies.ContainsKey(packageName))
        {
            dependencies[packageName] = version;
            File.WriteAllText(manifestPath, manifest.ToString(Newtonsoft.Json.Formatting.Indented));
            Debug.Log($"{provider} ({packageName}) added to manifest.json.");
            AssetDatabase.Refresh();
        }
    }

    private void RemovePackageFromManifest(string provider)
    {
        if (!upmPackages.ContainsKey(provider)) return;

        string manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
        JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
        JObject dependencies = (JObject)manifest["dependencies"];
        JArray scopedRegistries = manifest["scopedRegistries"] as JArray;

        var (packageName, _) = upmPackages[provider];

        // Remove from dependencies
        if (dependencies.ContainsKey(packageName))
        {
            dependencies.Remove(packageName);
            Debug.Log($"{provider} ({packageName}) removed from dependencies.");
        }

        // Remove registry only if none of its scopes are still used
        if (scopedRegistries != null)
        {
            for (int i = scopedRegistries.Count - 1; i >= 0; i--)
            {
                var reg = scopedRegistries[i] as JObject;
                if (reg == null) continue;

                string registryName = reg["name"]?.ToString();
                string url = reg["url"]?.ToString();
                JArray scopes = reg["scopes"] as JArray;

                if (scopes != null)
                {
                    bool stillUsed = false;
                    foreach (var scope in scopes)
                    {
                        if (dependencies.ContainsKey(scope.ToString()))
                        {
                            stillUsed = true;
                            break;
                        }
                    }

                    if (!stillUsed)
                    {
                        scopedRegistries.RemoveAt(i);
                        Debug.Log($"Scoped registry '{registryName}' removed.");
                    }
                }
            }
        }

        File.WriteAllText(manifestPath, manifest.ToString(Newtonsoft.Json.Formatting.Indented));
        AssetDatabase.Refresh();

        Debug.Log($"{provider} package removed from manifest.json.");

        if (provider == "AppLovin")
        {
            IAAPreprocessor.RemoveDefineSymbol("UNITY_APPLOVIN", BuildTargetGroup.Android);
            IAAPreprocessor.RemoveDefineSymbol("UNITY_APPLOVIN", BuildTargetGroup.iOS);
        }
        else
        {
            IAAPreprocessor.RemoveDefineSymbol("UNITY_ADMOB", BuildTargetGroup.iOS);
            IAAPreprocessor.RemoveDefineSymbol("UNITY_ADMOB", BuildTargetGroup.Android);
        }
    }

    private void DrawHeader(string title)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUILayout.Space(5);
    }

    private void DrawLabel(string label, string value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(250));
        EditorGUILayout.LabelField(value ?? "<empty>", EditorStyles.helpBox);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawFoldoutSection(string title, ref bool foldoutState, System.Action content)
    {
        foldoutState = EditorGUILayout.Foldout(foldoutState, title, true);
        if (foldoutState)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.BeginVertical("box");
            content.Invoke();
            EditorGUILayout.EndVertical();
            EditorGUI.indentLevel--;
        }
    }

    private void DrawTableHeader()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUILayout.Label("Name", EditorStyles.boldLabel, GUILayout.Width(150));
        GUILayout.Label("Status", EditorStyles.boldLabel, GUILayout.Width(100));
        GUILayout.Label("Current Version", EditorStyles.boldLabel, GUILayout.Width(200));
        GUILayout.Label("Latest Version", EditorStyles.boldLabel, GUILayout.Width(200));
        GUILayout.Label("Action", EditorStyles.boldLabel, GUILayout.Width(80));
        EditorGUILayout.EndHorizontal();
    }

    private void DrawProviderRow(string label, bool isInstalled, string current, string latest, System.Action onInstall, System.Action onUninstall)
    {
        EditorGUILayout.BeginHorizontal();

        GUILayout.Label(label, GUILayout.Width(150));
        GUILayout.Label(isInstalled ? "Installed" : "Not Installed", GUILayout.Width(100));
        GUILayout.Label(current, GUILayout.Width(200));
        GUILayout.Label(latest, GUILayout.Width(200));

        if (GUILayout.Button(isInstalled ? "Remove" : "Install", GUILayout.Width(80)))
        {
            if (isInstalled) onUninstall?.Invoke();
            else onInstall?.Invoke();
        }

        EditorGUILayout.EndHorizontal();
    }
}
