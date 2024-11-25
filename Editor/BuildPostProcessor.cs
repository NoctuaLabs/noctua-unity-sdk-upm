using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using com.noctuagames.sdk;
using UnityEngine;

#if UNITY_IOS
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
#endif

#if UNITY_ANDROID
using UnityEditor.Android;
using UnityEditor.Graphs;
#endif

#if UNITY_IOS
    public class NoctuaIosBuildProcessor : MonoBehaviour
    {
        [PostProcessBuild(1)]
        public static void EnableKeychainSharing(BuildTarget buildTarget, string pathToBuiltProject)
        {
            string entitlementsFileName = "NoctuaSDK.entitlements";
            string entitlementsFilePath = Path.Combine(pathToBuiltProject, entitlementsFileName);

            Log($"Creating entitlements file at path: {entitlementsFilePath}");
            var entitlements = new PlistDocument();
            var keychainGroups = entitlements.root.CreateArray("keychain-access-groups");
            keychainGroups.AddString("$(AppIdentifierPrefix)com.noctuagames.accounts");

            if (File.Exists(entitlementsFilePath))
            {
                File.Delete(entitlementsFilePath);
            }

            File.WriteAllText(entitlementsFilePath, entitlements.WriteToString());

            Log($"Added keychain sharing entitlements to Xcode project.");
            string pbxProjectPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            var pbxProject = new PBXProject();
            pbxProject.ReadFromFile(pbxProjectPath);
            string targetGuid = pbxProject.GetUnityMainTargetGuid();
            pbxProject.AddCapability(targetGuid, PBXCapabilityType.KeychainSharing, entitlementsFileName);
            pbxProject.WriteToFile(pbxProjectPath);

            Log($"Loaded Info.plist from Xcode project.");
            string plistPath = Path.Combine(pathToBuiltProject, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);

            Log($"Expose AppIdentifierPrefix by Info.plist");
            string appIdPrefix = "$(AppIdentifierPrefix)";
            plist.root.SetString("AppIdPrefix", appIdPrefix);

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

            var firebaseEnabled = noctuaConfig.Firebase != null && File.Exists(sourcePath);

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
                proj.AddFileToBuild(targetGuid, proj.AddFile(destinationPath, "GoogleService-Info.plist"));

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
            if (noctuaConfig.Facebook is null)
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

            if (noctuaConfig.Facebook is { AppId: not null, ClientToken: not null })
            {
                AddFacebookMetaData(manifestDoc, noctuaConfig.Facebook);

                Log("Added Facebook meta-data to AndroidManifest.xml.");
            }
            else
            {
                RemoveFacebookMetaData(manifestDoc);

                Log("Removed Facebook meta-data from AndroidManifest.xml.");
            }

            manifestDoc.Save(manifestPath);

            Log("AndroidManifest.xml modified for Facebook App Events settings.");
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
            var firebaseEnabled = noctuaConfig.Firebase != null && File.Exists(googleServicesJsonPath);
            
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
            var firebaseEnabled = noctuaConfig.Firebase != null && File.Exists(googleServicesJsonPath);

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
            var isGoogleServicesEnabled = noctuaConfig.Firebase != null && File.Exists(googleServicesJsonPath);
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

        private static void AddFacebookMetaData(XDocument manifestDoc, FacebookConfig facebookConfig)
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