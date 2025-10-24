using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using com.noctuagames.sdk;
using System.Linq;

public class NoctuaIntegrationManagerWindow : EditorWindow
{
    private Vector2 scrollPosition;
    private GlobalConfig config;

    private bool iaaFoldout = true;

    private readonly Dictionary<string, (string packageName, string version)> upmPackages = new()
    {
        { "AdMob", ("com.google.ads.mobile", "10.5.0") },
        { "AppLovin", ("com.applovin.mediation.ads", "8.5.0") }
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

        // DrawHeader("Noctua Config Information");
        // if (config != null)
        // {
        //     EditorGUILayout.BeginVertical("box");
        //     DrawLabel("Client ID", config.ClientId);
        //     EditorGUILayout.EndVertical();
        // }
        // else
        // {
        //     EditorGUILayout.HelpBox("Failed to load noctuagg.json", MessageType.Warning);
        // }

        // EditorGUILayout.Space(10);

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

        // Add scoped registry if needed
        if (manifest["scopedRegistries"] is not JArray scopedRegistries)
        {
            scopedRegistries = new JArray();
            manifest["scopedRegistries"] = scopedRegistries;
        }

        if (provider == "AppLovin")
        {
            AddScopedRegistryIfMissing(
                scopedRegistries,
                "AppLovin MAX Unity",
                "https://unity.packages.applovin.com/",
                new[] {
                    "com.applovin.mediation.ads",
                    "com.applovin.mediation.adapters",
                    "com.applovin.mediation.dsp"
                }
            );
        }
        else if (provider == "AdMob")
        {
            AddScopedRegistryIfMissing(
                scopedRegistries,
                "package.openupm.com",
                "https://package.openupm.com",
                new[] {
                    "com.google.ads.mobile",
                    "com.google.external-dependency-manager"
                }
            );
        }

        // Add dependency
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

        // Remove scoped registry if no packages from its scopes remain
        if (scopedRegistries != null)
        {
            if (provider == "AppLovin")
            {
                RemoveUnusedScopedRegistry(
                    scopedRegistries,
                    dependencies,
                    "https://unity.packages.applovin.com/"
                );
            }
            else if (provider == "AdMob")
            {
                RemoveUnusedScopedRegistry(
                    scopedRegistries,
                    dependencies,
                    "https://package.openupm.com"
                );
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
            IAAPreprocessor.RemoveDefineSymbol("UNITY_ADMOB", BuildTargetGroup.Android);
            IAAPreprocessor.RemoveDefineSymbol("UNITY_ADMOB", BuildTargetGroup.iOS);
        }
    }

    private void AddScopedRegistryIfMissing(JArray scopedRegistries, string name, string url, string[] scopes)
    {
        bool exists = scopedRegistries.Any(r => r["url"]?.ToString() == url);
        if (!exists)
        {
            var newRegistry = new JObject
            {
                ["name"] = name,
                ["url"] = url,
                ["scopes"] = new JArray(scopes)
            };

            scopedRegistries.Add(newRegistry);
            Debug.Log($"Scoped registry '{name}' added.");
        }
    }

    private void RemoveUnusedScopedRegistry(JArray scopedRegistries, JObject dependencies, string url)
    {
        for (int i = scopedRegistries.Count - 1; i >= 0; i--)
        {
            var reg = scopedRegistries[i] as JObject;
            if (reg == null || reg["url"]?.ToString() != url) continue;

            JArray scopes = reg["scopes"] as JArray;
            if (scopes == null) continue;

            bool stillUsed = scopes.Any(scope => dependencies.Properties().Any(dep => dep.Name.StartsWith(scope.ToString())));
            if (!stillUsed)
            {
                scopedRegistries.RemoveAt(i);
                Debug.Log($"Scoped registry with URL '{url}' removed.");
            }
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
