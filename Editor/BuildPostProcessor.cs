using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Newtonsoft.Json;
using com.noctuagames.sdk;
using Newtonsoft.Json;
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

namespace Editor
{
#if UNITY_IOS
    public class NoctuaIosBuildProcessor : MonoBehaviour
    {
        [PostProcessBuild(1)]
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

            if (firebaseEnabled && !gradleContent.Contains("com.google.gms.google-services"))
            {
                gradleContent = IncludeGoogleServicesPlugin(gradleContent);
                File.WriteAllText(rootGradlePath, gradleContent);

                Log("Added Google Services plugin to root build.gradle.");
            }
            else if (!firebaseEnabled && gradleContent.Contains("com.google.gms.google-services"))
            {
                gradleContent = ExcludeGoogleServicesPlugin(gradleContent);
                File.WriteAllText(rootGradlePath, gradleContent);

                Log("Removed Google Services plugin from root build.gradle.");
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

            var index = gradleContent.IndexOf(applyPluginString, StringComparison.Ordinal);

            if (index < 0)
            {
                gradleContent += "\n";
                index = gradleContent.Length - 1;
            }

            gradleContent = gradleContent.Insert(index, pluginEntry);

            return gradleContent;
        }

        private static string RemoveGoogleServicesPlugin(string gradleContent)
        {
            return gradleContent.Replace("apply plugin: 'com.google.gms.google-services'\n", string.Empty);
        }

        private static string IncludeGoogleServicesPlugin(string gradleContent)
        {
            const string appPluginString = "plugins {";
            var index = gradleContent.IndexOf(appPluginString, StringComparison.Ordinal);

            if (index < 0)
            {
                Debug.LogError("Failed to find 'plugins {' in build.gradle file.");

                return gradleContent;
            }

            const string pluginEntry = "\n    id 'com.google.gms.google-services' version '4.4.2' apply false";

            gradleContent = gradleContent.Insert(index + appPluginString.Length, pluginEntry);

            return gradleContent;
        }

        private static string ExcludeGoogleServicesPlugin(string gradleContent)
        {
            return gradleContent.Replace(
                "\n    id 'com.google.gms.google-services' version '4.4.2' apply false",
                string.Empty
            );
        }
        
        private static void Log(string message)
        {
            Debug.Log($"{nameof(NoctuaAndroidBuildProcessor)}: {message}");
        }
        
        private static void LogError(string message)
        {
            Debug.LogError($"{nameof(NoctuaAndroidBuildProcessor)}: {message}");
        }
        
        private static void LogWarning(string message)
        {
            Debug.LogWarning($"{nameof(NoctuaAndroidBuildProcessor)}: {message}");
        }
    }
#endif
}