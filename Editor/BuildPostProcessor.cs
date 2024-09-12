using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;
using UnityEngine;

[InitializeOnLoad]
public class NoctuaBuildPostProcessor : MonoBehaviour
{
    [PostProcessBuild]
    public static void CopyFirebaseConfiguration(BuildTarget buildTarget, string pathToBuiltProject)
    {
        Debug.Log($"{nameof(NoctuaBuildPostProcessor)}: CopyFirebaseConfiguration");

        if (buildTarget == BuildTarget.iOS)
        {
            // Define the source and destination paths
            string sourcePath = Path.Combine(Application.dataPath, "StreamingAssets/GoogleService-Info.plist");
            Debug.Log($"{nameof(NoctuaBuildPostProcessor)}: sourcePath: " + sourcePath);
            string destinationPath = Path.Combine(pathToBuiltProject, "GoogleService-Info.plist");
            Debug.Log($"{nameof(NoctuaBuildPostProcessor)}: destinationPath: " + destinationPath);

            // Copy the file to the Xcode root folder
            File.Copy(sourcePath, destinationPath, true);

            // Load the Xcode project
            string projPath = PBXProject.GetPBXProjectPath(pathToBuiltProject);
            Debug.Log("BuildPostProcessor projPath: " + projPath);
            PBXProject proj = new PBXProject();
            proj.ReadFromFile(projPath);

            // Get the target GUID
            string targetGuid = proj.GetUnityMainTargetGuid();

            // Add the file to the Xcode project as a resource
            proj.AddFileToBuild(targetGuid, proj.AddFile(destinationPath, "GoogleService-Info.plist"));

            // Write the changes to the Xcode project
            proj.WriteToFile(projPath);
        }
        else if (buildTarget == BuildTarget.Android)
        {
            // Define the source and destination paths
            string sourcePath = Path.Combine(Application.dataPath, "StreamingAssets/google-services.json");
            Debug.Log($"{nameof(NoctuaBuildPostProcessor)}: sourcePath: " + sourcePath);
            string destinationPath = Path.Combine(pathToBuiltProject, "google-services.json");
            Debug.Log($"{nameof(NoctuaBuildPostProcessor)}: destinationPath: " + destinationPath);

            // Copy the file to the Android root folder
            File.Copy(sourcePath, destinationPath, true);
        }
    }
}