using System;
using System.IO;
using UnityEngine;

using UnityEditor;
using UnityEditor.Callbacks;

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using UnityEngine;

#if UNITY_ANDROID
using UnityEditor.Android;
#endif

namespace Editor
{
#if UNITY_IOS

    public class NoctuaBuildPostProcessor : MonoBehaviour
    {
        [PostProcessBuild]
        public static void IntegrateGoogleServices(BuildTarget buildTarget, string pathToBuiltProject)
        {
            if (buildTarget != BuildTarget.iOS) return;
            
            Debug.Log($"{nameof(NoctuaBuildPostProcessor)}: IntegrateGoogleServices");
            // Define the source and destination paths
            var sourcePath = Path.Combine(Application.dataPath, "StreamingAssets/GoogleService-Info.plist");
            
            if (!File.Exists(sourcePath))
            {
                Debug.LogWarning(
                    $"{nameof(NoctuaBuildPostProcessor)}: GoogleService-Info.plist not found at path: {sourcePath}. " +
                    "Google services plugin will not be added."
                );

                return;
            }
            
            Debug.Log($"{nameof(NoctuaBuildPostProcessor)}: sourcePath: " + sourcePath);
            var destinationPath = Path.Combine(pathToBuiltProject, "GoogleService-Info.plist");
            Debug.Log($"{nameof(NoctuaBuildPostProcessor)}: destinationPath: " + destinationPath);

            // Copy the file to the Xcode root folder
            try
            {
                File.Copy(sourcePath, destinationPath, true);
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(NoctuaBuildPostProcessor)}: Failed to copy GoogleService-Info.plist to Xcode project. " +
                               $"Error: {e.Message}");
            }

            // Load the Xcode project
            var projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            Debug.Log("BuildPostProcessor projPath: " + projPath);
            var proj = new PBXProject();
            proj.ReadFromFile(projPath);

            // Get the target GUID
            var targetGuid = proj.GetUnityMainTargetGuid();

            // Add the file to the Xcode project as a resource
            proj.AddFileToBuild(targetGuid, proj.AddFile(destinationPath, "GoogleService-Info.plist"));

            // Write the changes to the Xcode project
            try
            {
                proj.WriteToFile(projPath);
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(NoctuaBuildPostProcessor)}: Failed to write changes to Xcode project. " +
                                 $"Error: {e.Message}");
            }
        }
    }

#endif

#if UNITY_ANDROID

    public class NoctuaAndroidBuildProcessor : IPostGenerateGradleAndroidProject
    {
        public int callbackOrder { get; }

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            Debug.Log($"{nameof(NoctuaAndroidBuildProcessor)}: OnPostGenerateGradleAndroidProject");

            // Copy google-services.json to the Android root app folder
            var srcGoogleServicesJsonPath = Path.Combine(Application.dataPath, "StreamingAssets/google-services.json");

            if (!File.Exists(srcGoogleServicesJsonPath))
            {
                Debug.LogWarning(
                    $"{nameof(NoctuaAndroidBuildProcessor)}: google-services.json not found at path: {srcGoogleServicesJsonPath}. " +
                    "Google services plugin will not be added."
                );

                return;
            }

            Debug.Log($"{nameof(NoctuaAndroidBuildProcessor)}: sourcePath: " + srcGoogleServicesJsonPath);
            var dstGoogleServicesJsonPath = Path.Combine(path, "../launcher/google-services.json");


            // Copy the file to the Android root app folder
            try
            {
                File.Copy(srcGoogleServicesJsonPath, dstGoogleServicesJsonPath, true);
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(NoctuaAndroidBuildProcessor)}: Failed to copy google-services.json to Android launcher project. " +
                               $"Error: {e.Message}");
            }

            // Modify the root-level build.gradle file
            var rootGradlePath = Path.Combine(path, "../build.gradle");
            var rootGradleFile = File.ReadAllText(rootGradlePath);

            // Add the Google services plugin dependency
            if (!rootGradleFile.Contains("com.google.gms.google-services"))
            {
                rootGradleFile = rootGradleFile.Replace(
                    "id 'com.android.library' version '7.4.2' apply false",
                    "id 'com.android.library' version '7.4.2' apply false\n" +
                    "    id 'com.google.gms.google-services' version '4.4.2' apply false"
                );

                // Write the changes to the root-level build.gradle file
                try
                {
                    File.WriteAllText(rootGradlePath, rootGradleFile);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{nameof(NoctuaAndroidBuildProcessor)}: Failed to write changes to root-level build.gradle file. " +
                                   $"Error: {e.Message}");
                }
            }

            // Modify the launcher-level build.gradle file
            var launcherGradlePath = Path.Combine(path, "../launcher/build.gradle");
            var launcherGradleFile = File.ReadAllText(launcherGradlePath);

            // Add the Google services plugin
            if (!launcherGradleFile.Contains("com.google.gms.google-services"))
            {
                launcherGradleFile = launcherGradleFile.Replace(
                    "apply plugin: 'com.android.application'",
                    "apply plugin: 'com.android.application'\n" +
                    "apply plugin: 'com.google.gms.google-services'"
                );

                // Write the changes to the launcher-level build.gradle file
                try
                {
                    File.WriteAllText(launcherGradlePath, launcherGradleFile);
                }
                catch (Exception e)
                {
                    Debug.LogError($"{nameof(NoctuaAndroidBuildProcessor)}: Failed to write changes to unityLibrary-level build.gradle file. " +
                                   $"Error: {e.Message}");
                }
            }
        }
    }
#endif
}
