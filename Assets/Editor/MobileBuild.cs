using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MobileBuild
{
    private const string AndroidAppId = "com.defaultcompany.basketballrush";
    private const string IosAppId = "com.defaultcompany.basketballrush";
    private const string AndroidOutput = "Builds/Android/BasketballRush.apk";
    private const string AndroidBundleOutput = "Builds/Android/BasketballRush.aab";
    private const string IosOutput = "Builds/iOS";

    [InitializeOnLoadMethod]
    private static void FixTemplatePlayerDefaults()
    {
        bool changed = false;

        string androidId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
        string iosId = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.iOS);

        if (string.IsNullOrEmpty(androidId) || androidId.Contains("unity.template") || androidId.Contains("UnityTechnologies") || androidId.Contains("get-started"))
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidAppId);
            changed = true;
        }

        if (string.IsNullOrEmpty(iosId) || iosId.Contains("unity.template") || iosId.Contains("UnityTechnologies") || iosId.Contains("get-started"))
        {
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, IosAppId);
            changed = true;
        }

        if (PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown)
        {
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            changed = true;
        }

        if (changed)
        {
            Debug.Log("[MobileBuild] Applied landscape-only orientation and Basketball Rush application identifiers.");
        }
    }

    [MenuItem("Basketball Rush/Apply Mobile Player Settings")]
    public static void ApplyMobilePlayerSettings()
    {
        ConfigurePlayerSettings();
        Debug.Log("[MobileBuild] Mobile player settings applied.");
    }

    [MenuItem("Basketball Rush/Builds/Android APK")]
    public static void BuildAndroidApk()
    {
        BuildAndroid(false, true);
    }

    [MenuItem("Basketball Rush/Builds/Android AAB")]
    public static void BuildAndroidBundle()
    {
        BuildAndroid(true, true);
    }

    [MenuItem("Basketball Rush/Builds/iOS Xcode Project")]
    public static void BuildIos()
    {
        BuildIosProject(true);
    }

    public static void BuildAndroidCI()
    {
        bool success = BuildAndroid(false, false);
        EditorApplication.Exit(success ? 0 : 1);
    }

    public static void BuildAndroidBundleCI()
    {
        bool success = BuildAndroid(true, false);
        EditorApplication.Exit(success ? 0 : 1);
    }

    public static void BuildIosCI()
    {
        bool success = BuildIosProject(false);
        EditorApplication.Exit(success ? 0 : 1);
    }

    private static bool BuildAndroid(bool appBundle, bool interactive)
    {
        ConfigurePlayerSettings();

        string outputPath = appBundle ? AndroidBundleOutput : AndroidOutput;
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        EditorUserBuildSettings.buildAppBundle = appBundle;
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        bool success = report.summary.result == BuildResult.Succeeded;

        Debug.Log("[MobileBuild] Android " + (appBundle ? "AAB" : "APK") + " build " +
                  (success ? "succeeded" : "failed") + ": " + outputPath +
                  " (" + report.summary.totalSize + " bytes)");

        if (interactive && !success)
        {
            EditorUtility.DisplayDialog("Android Build Failed", "See the Console for details.", "OK");
        }

        return success;
    }

    private static bool BuildIosProject(bool interactive)
    {
        ConfigurePlayerSettings();

#if !UNITY_EDITOR_OSX
        Debug.LogError("[MobileBuild] iOS builds require macOS with Xcode. Build the Xcode project on a Mac.");
        if (interactive)
        {
            EditorUtility.DisplayDialog("iOS Build Requires macOS", "Apple's toolchain only runs on macOS. Build the generated Xcode project on a Mac.", "OK");
        }

        return false;
#else
        Directory.CreateDirectory(IosOutput);

        BuildPlayerOptions options = new BuildPlayerOptions
        {
            scenes = GetEnabledScenes(),
            locationPathName = IosOutput,
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        bool success = report.summary.result == BuildResult.Succeeded;

        Debug.Log("[MobileBuild] iOS Xcode project build " + (success ? "succeeded" : "failed") + ": " + IosOutput);

        if (interactive && !success)
        {
            EditorUtility.DisplayDialog("iOS Build Failed", "See the Console for details.", "OK");
        }

        return success;
#endif
    }

    private static void ConfigurePlayerSettings()
    {
        PlayerSettings.companyName = "DefaultCompany";
        PlayerSettings.productName = "Basketball Rush";
        PlayerSettings.bundleVersion = "1.0.0";
        PlayerSettings.colorSpace = ColorSpace.Linear;

        PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, AndroidAppId);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, IosAppId);

        PlayerSettings.Android.bundleVersionCode = 1;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Medium);

        PlayerSettings.iOS.targetOSVersionString = "13.0";
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.iOS.requiresFullScreen = true;
        PlayerSettings.iOS.buildNumber = "1";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Medium);
    }

    private static string[] GetEnabledScenes()
    {
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        int count = 0;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].enabled)
            {
                count++;
            }
        }

        string[] enabled = new string[count];
        int index = 0;

        for (int i = 0; i < scenes.Length; i++)
        {
            if (scenes[i].enabled)
            {
                enabled[index++] = scenes[i].path;
            }
        }

        return enabled;
    }
}
