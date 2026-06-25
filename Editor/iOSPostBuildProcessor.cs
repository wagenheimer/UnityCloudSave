#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Callbacks;
#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using UnityEngine;

/// <summary>
/// Automatically adds the iCloud Key-Value Storage capability to the generated
/// Xcode project after every iOS build. No manual Xcode configuration needed.
/// </summary>
public static class iOSPostBuildProcessor
{
    [PostProcessBuild(100)]
    public static void OnPostProcessBuild(BuildTarget target, string buildPath)
    {
#if UNITY_IOS
        if (target != BuildTarget.iOS) return;

        string projPath = PBXProject.GetPBXProjectPath(buildPath);

        var capabilityManager = new ProjectCapabilityManager(
            projPath,
            "Unity-iPhone.entitlements",
            "Unity-iPhone"
        );

        // Key-Value Storage only — no CloudKit, no iCloud Documents.
        capabilityManager.AddiCloud(
            enableKeyValueStorage:  true,
            enableiCloudDocument:   false,
            enablecloudKit:         false,
            addDefaultContainerId:  false,
            customContainerIds:     null
        );

        capabilityManager.WriteToFile();

        Debug.Log("[CloudSave] iCloud Key-Value Storage capability added to Xcode project.");
#endif
    }
}
#endif
