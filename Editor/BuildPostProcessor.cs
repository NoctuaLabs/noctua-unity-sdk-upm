using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using com.noctuagames.sdk;
using UnityEngine;
using UnityEngine.Networking;

#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEditor.iOS.Xcode.Extensions;
#endif

#if UNITY_ANDROID
using UnityEditor.Android;
using UnityEditor.Graphs;
#endif

#if UNITY_IOS
    public class NoctuaIosBuildProcessor : MonoBehaviour
    {
        [PostProcessBuild(1)]
        public static void ExposeLogFiles(BuildTarget buildTarget, string pathToBuiltProject)
        {
            Log($"Loaded Info.plist from Xcode project.");
            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            Log($"Expose log files to users by Info.plist");
            plist.root.SetBoolean("UIFileSharingEnabled", true);
            plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true);

            Log($"Write changes to Info.plist");    
            plist.WriteToFile(plistPath);
        }

        [PostProcessBuild(2)]
        public static void IntegrateGoogleServices(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget != BuildTarget.iOS) return;

            var noctuaConfigPath = Path.Combine(Application.dataPath, "StreamingAssets", "noctuagg.json");
            var noctuaConfig = JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText(noctuaConfigPath));

            if (noctuaConfig == null)
            {
                LogError("Failed to parse noctuagg.json.");

                return;
            }

            var sourcePath = Path.Combine(Application.dataPath, "StreamingAssets/GoogleService-Info.plist");

            if (!File.Exists(sourcePath))
            {
                LogWarning("GoogleService-Info.plist not found. Disabling Firebase services.");
            }

            var firebaseEnabled = File.Exists(sourcePath);

            var destinationPath = Path.Combine(pathToBuiltProject, "GoogleService-Info.plist");

            if (!firebaseEnabled && File.Exists(destinationPath))
            {
                File.Delete(destinationPath);

                Log("Deleted GoogleService-Info.plist from Xcode project folder.");
            }
            else if (firebaseEnabled)
            {
                try
                {
                    File.Copy(sourcePath, destinationPath, true);

                    Log("Copied GoogleService-Info.plist to Xcode project folder.");
                }
                catch (Exception e)
                {
                    LogError($"Failed to copy GoogleService-Info.plist to path '{destinationPath}': {e.Message}");

                    return;
                }
            }

            var projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var proj = new PBXProject();

            try
            {
                proj.ReadFromFile(projPath);
            }
            catch (Exception e)
            {
                LogError($"Failed to read Xcode project at path '{projPath}': {e.Message}");

                return;
            }

            var targetGuid = proj.GetUnityMainTargetGuid();

            if (firebaseEnabled)
            {
                proj.AddFileToBuild(targetGuid, proj.AddFile("GoogleService-Info.plist", "GoogleService-Info.plist"));

                Log("Added GoogleService-Info.plist to Xcode project.");
            }
            else
            {
                var googleServiceInfoGuid = proj.FindFileGuidByProjectPath("GoogleService-Info.plist");

                if (googleServiceInfoGuid != null)
                {
                    proj.RemoveFile(googleServiceInfoGuid);

                    Log("Removed GoogleService-Info.plist from Xcode project.");
                }
            }

            try
            {
                proj.WriteToFile(projPath);

                Log("Wrote changes to Xcode project.");
            }
            catch (Exception e)
            {
                LogError($"Failed to write changes to Xcode project at path '{projPath}': {e.Message}");
            }
        }

        // [PostProcessBuild(3)]
        // public static void AddNoctuaSPM(BuildTarget buildTarget, string pathToBuiltProject)
        // {
        //     if (buildTarget != BuildTarget.iOS)
        //         return;

        //     string projectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
        //     PBXProject pbxProject = new();
        //     pbxProject.ReadFromFile(projectPath);

        //     string mainTargetGuid = pbxProject.GetUnityMainTargetGuid();           // Unity-iPhone
        //     string unityFrameworkTargetGuid = pbxProject.GetUnityFrameworkTargetGuid(); // UnityFramework

        //     // Add the Noctua SDK Swift Package
        //     string packageGuid = pbxProject.AddRemotePackageReferenceAtVersion(
        //         "https://github.com/NoctuaLabs/noctua-native-sdk-ios.git",
        //         "0.5.0"
        //     );

        //     // Link NoctuaSDK to both Unity-iPhone and UnityFramework
        //     pbxProject.AddRemotePackageFrameworkToProject(mainTargetGuid, "NoctuaSDK", packageGuid, false);
        //     pbxProject.AddRemotePackageFrameworkToProject(unityFrameworkTargetGuid, "NoctuaSDK", packageGuid, false);
            
        //     // Note: This is not working as expected, so we are not using it for now. we need manual setting CodeSignOnCopy.
        //     // // Find the Swift Package product file GUID
        //     // string fileGuid = pbxProject.FindFileGuidByRealPath("NoctuaSDK");
            
        //     // // Fallback: search all fileRefs if direct lookup fails
        //     // if (!string.IsNullOrEmpty(fileGuid))
        //     // {
        //     //     Log($"File NoctuaSDK Found");

        //     //     pbxProject.AddFileToBuild(mainTargetGuid, fileGuid);
        //     //     pbxProject.AddFileToEmbedFrameworks(mainTargetGuid, fileGuid);
        //     //     pbxProject.SetCodeSignOnCopy(mainTargetGuid, fileGuid, true);
        //     // }
        //     // else
        //     // {
        //     //     LogError("NoctuaSDK file not found in project. Skipping file addition.");
        //     // }

        //     // Set CLANG_ENABLE_MODULES = YES for UnityFramework target
        //     pbxProject.SetBuildProperty(unityFrameworkTargetGuid, "CLANG_ENABLE_MODULES", "YES");
            
        //     pbxProject.WriteToFile(projectPath);

        //     Log("Added Noctua SPM to Unity-iPhone and UnityFramework.");
        // }

        [PostProcessBuild(4)]
        public static void EnableKeychainSharing(BuildTarget buildTarget, string pathToBuiltProject)
        {
            Log($"Added keychain sharing entitlements to Xcode project.");

            string pbxProjectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var pbxProject = new PBXProject();
            pbxProject.ReadFromFile(pbxProjectPath);
            string targetGuid = pbxProject.GetUnityMainTargetGuid();

            Log($"Loaded Xcode project at path: {pbxProjectPath}");
            
            var entitlementsFile = pbxProject.GetBuildPropertyForAnyConfig(targetGuid, "CODE_SIGN_ENTITLEMENTS");

            if (string.IsNullOrEmpty(entitlementsFile))
            {
                Log($"No code sign entitlements file found. Creating new entitlements file.");
                
                entitlementsFile = "Unity-iPhone.entitlements";
            }
            else
            {
                Log($"Code sign entitlements file found: {entitlementsFile}");
            }

            var entitlementsFilePath = Path.Combine(pathToBuiltProject, entitlementsFile);

            Log($"Entitlements file path: {entitlementsFilePath}");

            var entitlements = new PlistDocument();

            if (File.Exists(entitlementsFilePath))
            {
                entitlements.ReadFromFile(entitlementsFilePath);

                Log($"Read entitlements file at path: {entitlementsFilePath}");
            }
            else
            {
                pbxProject.SetBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", entitlementsFile);

                Log($"Set project code sign entitlements to: {entitlementsFile}");
            }

            PlistElementArray keychainGroups;

            try
            {
                keychainGroups = entitlements.root["keychain-access-groups"]?.AsArray();
            }
            catch (Exception e)
            {
                LogError($"Failed to read keychain-access-groups from entitlements: {e.Message}");

                return;
            }

            keychainGroups ??= entitlements.root.CreateArray("keychain-access-groups");
            var keychainGroup = $"$(AppIdentifierPrefix)com.noctuagames.accounts";

            if (keychainGroups.values.All(value => value.AsString() != keychainGroup))
            {
                keychainGroups.AddString(keychainGroup);

                Log($"Added keychain-access-groups to entitlements.");
            }

            entitlements.WriteToFile(entitlementsFilePath);
            pbxProject.WriteToFile(pbxProjectPath);
            
            Log($"Loaded Info.plist from Xcode project.");
            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            Log($"Expose AppIdentifierPrefix by Info.plist");
            string appIdPrefix = "$(AppIdentifierPrefix)";
            plist.root.SetString("AppIdPrefix", appIdPrefix);

            Log($"Write changes to Info.plist");    
            plist.WriteToFile(plistPath);
        }

        [PostProcessBuild(5)]
        public static void EnablePushNotificationCapability(BuildTarget buildTarget, string pathToBuiltProject)
        {
            Log($"Enabling push notification capability in Xcode project.");

            string pbxProjectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var pbxProject = new PBXProject();
            pbxProject.ReadFromFile(pbxProjectPath);
            string targetGuid = pbxProject.GetUnityMainTargetGuid();

            Log($"Loaded Xcode project at path: {pbxProjectPath}");

            // Add Push Notification capability
            Log("Adding Push Notification capability to Xcode project.");
            pbxProject.AddCapability(targetGuid, PBXCapabilityType.PushNotifications);

            var entitlementsFile = pbxProject.GetBuildPropertyForAnyConfig(targetGuid, "CODE_SIGN_ENTITLEMENTS");

            if (string.IsNullOrEmpty(entitlementsFile))
            {
                Log($"No code sign entitlements file found. Creating new entitlements file.");
                entitlementsFile = "Unity-iPhone.entitlements";
            }
            
            var entitlementsFilePath = Path.Combine(pathToBuiltProject, entitlementsFile);
            Log($"Entitlements file path: {entitlementsFilePath}");

            var entitlements = new PlistDocument();

            if (File.Exists(entitlementsFilePath))
            {
                entitlements.ReadFromFile(entitlementsFilePath);
                Log($"Read entitlements file at path: {entitlementsFilePath}");
            }
            else
            {
                pbxProject.SetBuildProperty(targetGuid, "CODE_SIGN_ENTITLEMENTS", entitlementsFile);
                Log($"Set project code sign entitlements to: {entitlementsFile}");
            }

            // Add the aps-environment entitlement for push notifications
            try
            {
                entitlements.root.SetString("aps-environment", "production");
                Log("Added aps-environment entitlement for Push Notifications.");
            }
            catch (Exception e)
            {
                LogError($"Failed to add aps-environment entitlement: {e.Message}");
                return;
            }

            entitlements.WriteToFile(entitlementsFilePath);
            pbxProject.WriteToFile(pbxProjectPath);

            Log("Successfully enabled Push Notification capability and updated entitlements.");
        }


        private static void Log(string message)
        {
            Debug.Log($"{nameof(NoctuaIosBuildProcessor)}: {message}");
        }

        private static void LogError(string message)
        {
            Debug.LogError($"{nameof(NoctuaIosBuildProcessor)}: {message}");
        }

        private static void LogWarning(string message)
        {
            Debug.LogWarning($"{nameof(NoctuaIosBuildProcessor)}: {message}");
        }
    }

#endif

#if UNITY_ANDROID

    public class NoctuaAndroidBuildProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder => 1;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var rootAndroidProjectPath = Path.Combine(path, "..");
            var googleServicesJsonPath = Path.Combine(Application.dataPath, "StreamingAssets", "google-services.json");
            
            if (!File.Exists(googleServicesJsonPath))
            {
                LogWarning("Google Services config file not found. Disabling Firebase services.");
            }
            
            var noctuaConfigPath = Path.Combine(Application.dataPath, "StreamingAssets", "noctuagg.json");
            var noctuaConfig = JsonConvert.DeserializeObject<GlobalConfig>(File.ReadAllText(noctuaConfigPath));

            if (noctuaConfig == null)
            {
                LogError("Failed to parse noctuagg.json.");

                return;
            }

            ModifyAndroidManifest(rootAndroidProjectPath, noctuaConfig);
            ModifyRootBuildGradle(rootAndroidProjectPath, noctuaConfig);
            ModifyLauncherBuildGradle(rootAndroidProjectPath, noctuaConfig);
            CopyOrRemoveGoogleServicesJson(rootAndroidProjectPath, noctuaConfig);
        }

        private static void ModifyAndroidManifest(string path, GlobalConfig noctuaConfig)
        {
            if (noctuaConfig.Facebook.Android is null)
            {
                LogWarning("Facebook config is null. Skipped modifying AndroidManifest.xml for Facebook App Events.");

                return;
            }

            var manifestPath = Path.Combine(path, "launcher", "src", "main", "AndroidManifest.xml");

            if (!File.Exists(manifestPath))
            {
                LogError($"AndroidManifest.xml not found at path: {manifestPath}");

                return;
            }

            var manifestDoc = XDocument.Load(manifestPath);

            if (noctuaConfig.Facebook.Android is { AppId: not null, ClientToken: not null })
            {
                AddFacebookMetaData(manifestDoc, noctuaConfig.Facebook.Android);

                Log("Added Facebook meta-data to AndroidManifest.xml.");
            }
            else
            {
                RemoveFacebookMetaData(manifestDoc);

                Log("Removed Facebook meta-data from AndroidManifest.xml.");
            }

            AddNoctuaGamesMetaData(manifestDoc, noctuaConfig);

            manifestDoc.Save(manifestPath);

            Log("AndroidManifest.xml modified for Facebook App Events settings.");
        }

        private static void AddNoctuaGamesMetaData(XDocument manifestDoc, GlobalConfig noctuaConfig)
        {
            var request = UnityWebRequest.Get($"{noctuaConfig.Noctua.BaseUrl}/games/build-config");
            request.SetRequestHeader("X-CLIENT-ID", noctuaConfig.ClientId);
            request.SetRequestHeader("X-BUNDLE-ID", Application.identifier);
            request.SetRequestHeader("X-PLATFORM", Utility.GetPlatformType());
            var noctuaGames = new List<string>();

            request.SendWebRequest();
            
            const int timeoutMs = 10000; // Timeout in milliseconds
            const int sleepIntervalMs = 100; // Polling interval in milliseconds
            int elapsedMs = 0;

            // Wait for the request to complete or timeout
            while (!request.isDone && elapsedMs < timeoutMs)
            {
                Thread.Sleep(sleepIntervalMs);
                elapsedMs += sleepIntervalMs;
            }

            // Handle timeout
            if (!request.isDone)
            {
                LogError("Request to fetch Noctua games timed out.");
                
                return;
            }
            
            if (request.result != UnityWebRequest.Result.Success)
            {
                LogError($"Failed to fetch Noctua games: {request.error}");
                
                return;
            }

            var buildConfigResult = JsonConvert.DeserializeObject<BuildConfigResult>(request.downloadHandler.text);
            
            if (buildConfigResult == null)
            {
                LogError("Failed to parse InitGameResponse from JSON.");

                return;
            }
            
            if (!buildConfigResult.Success)
            {
                LogError($"Failed to fetch Noctua games: {request.downloadHandler.text}");

                return;
            }
            
            noctuaGames.AddRange(buildConfigResult.Data.ActiveBundleIds);

            XNamespace ns = "http://schemas.android.com/apk/res/android";

            var queriesNode = manifestDoc.Root?.Element("queries");
            
            if (queriesNode == null)
            {
                manifestDoc.Root?.Add(new XElement("queries"));
                queriesNode = manifestDoc.Root?.Element("queries");
            }
            
            queriesNode?.RemoveNodes();
            
            foreach (var game in noctuaGames)
            {
                queriesNode?.Add(
                    new XElement(
                        "provider",
                        new XAttribute(ns + "authorities", $"{game}.noctuaaccountprovider")
                    )
                );
            }
        }

        private class BuildConfigData
        {
            [JsonProperty("active_bundle_ids")] public List<string> ActiveBundleIds;
        }
        
        private class BuildConfigResult
        {
            [JsonProperty("success")] public bool Success;
            [JsonProperty("data")] public BuildConfigData Data;
        }

        private static void ModifyRootBuildGradle(string path, GlobalConfig noctuaConfig)
        {
            var rootGradlePath = Path.Combine(path, "build.gradle");

            if (!File.Exists(rootGradlePath))
            {
                LogError($"Root build.gradle not found at path: {rootGradlePath}");

                return;
            }

            var gradleContent = File.ReadAllText(rootGradlePath);
            var googleServicesJsonPath = Path.Combine(Application.dataPath, "StreamingAssets", "google-services.json");
            var firebaseEnabled = File.Exists(googleServicesJsonPath);
            
            var gradleVersion = GetGradleVersion(path);
            
            if (gradleVersion.Major >= 7)
            {
                if (firebaseEnabled && !gradleContent.Contains("com.google.gms.google-services"))
                {
                    gradleContent = IncludeGoogleServicesPluginForGradle7(gradleContent);
                    File.WriteAllText(rootGradlePath, gradleContent);

                    Log("Added Google Services plugin to root build.gradle.");
                }
                else if (!firebaseEnabled && gradleContent.Contains("com.google.gms.google-services"))
                {
                    gradleContent = ExcludeGoogleServicesPluginForGradle7(gradleContent);
                    File.WriteAllText(rootGradlePath, gradleContent);

                    Log("Removed Google Services plugin from root build.gradle.");
                }
            }
            else if (gradleVersion.Major == 6)
            {
                if (firebaseEnabled && !gradleContent.Contains("com.google.gms:google-services"))
                {
                    gradleContent = IncludeGoogleServicesPluginForGradle6(gradleContent);
                    File.WriteAllText(rootGradlePath, gradleContent);

                    Log("Added Google Services plugin to root build.gradle.");
                }
                else if (!firebaseEnabled && gradleContent.Contains("com.google.gms:google-services"))
                {
                    gradleContent = ExcludeGoogleServicesPluginForGradle6(gradleContent);
                    File.WriteAllText(rootGradlePath, gradleContent);

                    Log("Removed Google Services plugin from root build.gradle.");
                }
            }
            else
            {
                LogError($"Unsupported Gradle version: {gradleVersion}");
            }
        }

        private static void ModifyLauncherBuildGradle(string path, GlobalConfig noctuaConfig)
        {
            var launcherGradlePath = Path.Combine(path, "launcher", "build.gradle");
            
            if (!File.Exists(launcherGradlePath))
            {
                LogError($"Launcher build.gradle not found at path: {launcherGradlePath}");

                return;
            }

            var gradleContent = File.ReadAllText(launcherGradlePath);
            var googleServicesJsonPath = Path.Combine(Application.dataPath, "StreamingAssets", "google-services.json");
            var firebaseEnabled = File.Exists(googleServicesJsonPath);

            if (firebaseEnabled && !gradleContent.Contains("com.google.gms.google-services"))
            {
                gradleContent = ApplyGoogleServicesPlugin(gradleContent);
                File.WriteAllText(launcherGradlePath, gradleContent);

                Log("Added Google Services plugin to launcher build.gradle.");
            }
            else if (!firebaseEnabled && gradleContent.Contains("com.google.gms.google-services"))
            {
                gradleContent = RemoveGoogleServicesPlugin(gradleContent);
                File.WriteAllText(launcherGradlePath, gradleContent);

                Log("Removed Google Services plugin from launcher build.gradle.");
            }
        }

        private static void CopyOrRemoveGoogleServicesJson(string rootProjectPath, GlobalConfig noctuaConfig)
        {
            var googleServicesJsonPath = Path.Combine(Application.dataPath, "StreamingAssets", "google-services.json");
            var isGoogleServicesEnabled = File.Exists(googleServicesJsonPath);
            var destinationPath = Path.Combine(rootProjectPath, "launcher", "google-services.json");

            if (isGoogleServicesEnabled)
            {
                File.Copy(googleServicesJsonPath, destinationPath, true);

                Log("Copied google-services.json to launcher folder.");
            }
            else if (File.Exists(destinationPath))
            {
                File.Delete(destinationPath);

                Log("Removed google-services.json from launcher folder.");
            }
        }

        private static void AddFacebookMetaData(XDocument manifestDoc, FacebookAndroidConfig facebookConfig)
        {
            if (facebookConfig == null)
            {
                LogWarning("Cannot add Facebook meta-data to AndroidManifest.xml. Facebook config is null.");

                return;
            }

            var appNode = manifestDoc.Root?.Element("application");

            if (appNode == null)
            {
                LogError("Failed to find application node in AndroidManifest.xml.");

                return;
            }

            XNamespace ns = "http://schemas.android.com/apk/res/android";

            var fbAppIdNode = appNode.Descendants().FirstOrDefault(
                node =>
                    node.Name.LocalName == "meta-data" &&
                    node.Attributes().Any(attr => attr.Name == ns+"name" && attr.Value == "com.facebook.sdk.ApplicationId")
            );

            fbAppIdNode?.Remove();

            appNode.Add(
                new XElement(
                    "meta-data",
                    new XAttribute(ns + "name", "com.facebook.sdk.ApplicationId"),
                    new XAttribute(ns + "value", "fb"+ facebookConfig.AppId)
                )
            );

            var fbClientTokenNode = appNode.Descendants().FirstOrDefault(
                node =>
                    node.Name.LocalName == "meta-data" &&
                    node.Attributes().Any(attr => attr.Name == ns+"name" && attr.Value == "com.facebook.sdk.ClientToken")
            );
            
            fbClientTokenNode?.Remove();

            appNode.Add(
                new XElement(
                    "meta-data",
                    new XAttribute(ns + "name", "com.facebook.sdk.ClientToken"),
                    new XAttribute(ns + "value", facebookConfig.ClientToken)
                )
            );
        }

        private static void RemoveFacebookMetaData(XDocument manifestDoc)
        {
            var appNode = manifestDoc.Root?.Element("application");

            if (appNode == null)
            {
                LogWarning("Failed to find application node in AndroidManifest.xml.");

                return;
            }

            appNode.Descendants().FirstOrDefault(e => e.Attributes().Any(attr => attr.Value is "com.facebook.sdk.ApplicationId"))?.Remove();
            appNode.Descendants().FirstOrDefault(e => e.Attributes().Any(attr => attr.Value is "com.facebook.sdk.ClientToken"))?.Remove();
        }

        private static string ApplyGoogleServicesPlugin(string gradleContent)
        {
            const string applyPluginString = "apply plugin:";
            const string pluginEntry = "apply plugin: 'com.google.gms.google-services'\n";
            const string crashlyticsPluginEntry = "apply plugin: 'com.google.firebase.crashlytics'\n";

            var index = gradleContent.LastIndexOf(applyPluginString, StringComparison.Ordinal);

            if (index < 0)
            {
                gradleContent += "\n";
                index = gradleContent.Length - 1;
            }
            else
            {
                index = gradleContent[index..].IndexOf('\n');
                
                if (index < 0)
                {
                    index = gradleContent.Length - 1;
                }
                else
                {
                    index += 1;
                }
            }

            gradleContent = gradleContent.Insert(index, pluginEntry + crashlyticsPluginEntry);

            return gradleContent;
        }

        private static string RemoveGoogleServicesPlugin(string gradleContent)
        {
            gradleContent = gradleContent.Replace("apply plugin: 'com.google.gms.google-services'\n", string.Empty);
            gradleContent = gradleContent.Replace("apply plugin: 'com.google.firebase.crashlytics'\n", string.Empty);

            return gradleContent;
        }

        private static string IncludeGoogleServicesPluginForGradle7(string gradleContent)
        {
            const string appPluginString = "plugins {";
            var index = gradleContent.IndexOf(appPluginString, StringComparison.Ordinal);

            if (index < 0)
            {
                Debug.LogError("Failed to find 'plugins {' in build.gradle file.");

                return gradleContent;
            }

            const string pluginEntry = "\n    id 'com.google.gms.google-services' version '4.4.2' apply false";
            const string crashlyticsPluginEntry = "\n    id 'com.google.firebase.crashlytics' version '2.9.5' apply false";

            gradleContent = gradleContent.Insert(index + appPluginString.Length, pluginEntry + crashlyticsPluginEntry);

            return gradleContent;
        }

        private static string ExcludeGoogleServicesPluginForGradle7(string gradleContent)
        {
            gradleContent = gradleContent.Replace(
                "\n    id 'com.google.gms.google-services' version '4.4.2' apply false",
                string.Empty
            );
            
            gradleContent = gradleContent.Replace(
                "\n    id 'com.google.firebase.crashlytics' version '2.9.5' apply false",
                string.Empty
            );

            return gradleContent;
        }
        
        private static string IncludeGoogleServicesPluginForGradle6(string gradleContent)
        {
            const string appPluginString = "dependencies {";
            var index = gradleContent.IndexOf(appPluginString, StringComparison.Ordinal);

            if (index < 0)
            {
                Debug.LogError("Failed to find 'plugins {' in build.gradle file.");

                return gradleContent;
            }

            const string pluginEntry = "\n            classpath 'com.google.gms:google-services:4.3.10'";
            const string crashlyticsPluginEntry = "\n            classpath 'com.google.firebase:firebase-crashlytics-gradle:2.9.5'";

            gradleContent = gradleContent.Insert(index + appPluginString.Length, pluginEntry + crashlyticsPluginEntry);

            return gradleContent;
        }

        private static string ExcludeGoogleServicesPluginForGradle6(string gradleContent)
        {
            gradleContent = gradleContent.Replace(
                "\n            classpath 'com.google.gms:google-services:4.3.10'",
                string.Empty
            );

            gradleContent = gradleContent.Replace(
                "\n            classpath 'com.google.firebase:firebase-crashlytics-gradle:2.9.5'",
                string.Empty
            );

            return gradleContent;
        }

        private static Version GetGradleVersion(string projectPath)
        {
            var wrapperPropertiesPath = Path.Combine(projectPath, "gradle", "wrapper", "gradle-wrapper.properties");

            if (!File.Exists(wrapperPropertiesPath))
            {
                LogError("gradle-wrapper.properties not found.");
                return new Version(7, 0); // Default to Gradle 7.0 if we can't find it
            }

            var versionLine = File.ReadAllLines(wrapperPropertiesPath)
                .FirstOrDefault(line => line.StartsWith("distributionUrl"));
            
            if (versionLine == null)
            {
                LogWarning("Failed to read distributionUrl from gradle-wrapper.properties. Defaulting to Gradle 7.0.");
                
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
        
        private static void Log(string message, [CallerMemberName] string caller = "")
        {
            Debug.Log($"{nameof(NoctuaAndroidBuildProcessor)}.{caller}: {message}");
        }
        
        private static void LogError(string message, [CallerMemberName] string caller = "")
        {
            Debug.LogError($"{nameof(NoctuaAndroidBuildProcessor)}.{caller}: {message}");
        }
        
        private static void LogWarning(string message, [CallerMemberName] string caller = "")
        {
            Debug.LogWarning($"{nameof(NoctuaAndroidBuildProcessor)}.{caller}: {message}");
        }
    }
#endif
