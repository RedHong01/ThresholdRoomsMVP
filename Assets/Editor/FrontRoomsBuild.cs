using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Creates the FrontRooms prototype scene and builds players from the menu or the command line:
/// Unity -batchmode -quit -projectPath . -executeMethod FrontRoomsBuild.BuildAll
/// </summary>
public static class FrontRoomsBuild
{
    private const string ScenePath = "Assets/Scenes/FP_FrontRooms.unity";
    private const string LegacyScenePath = "Assets/Scenes/MVP_ThresholdRun.unity";

    [MenuItem("FrontRooms/Create Prototype Scene")]
    public static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject("FrontRooms");
        var game = root.AddComponent<FrontRoomsGame>();
        game.EnsureEditorPreview();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[]
        {
            new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene(LegacyScenePath, false),
        };
        Debug.Log("[FrontRoomsBuild] scene ready: " + ScenePath);
    }

    private static void ApplyPlayerSettings()
    {
        PlayerSettings.productName = "FrontRooms";
        PlayerSettings.companyName = "Red Wang";
        PlayerSettings.bundleVersion = "0.2.0";
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1600;
        PlayerSettings.defaultScreenHeight = 900;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;
    }

    [MenuItem("FrontRooms/Build macOS")]
    public static void BuildMac() => Build(BuildTarget.StandaloneOSX, "Builds/Mac/FrontRooms.app");

    [MenuItem("FrontRooms/Build Windows")]
    public static void BuildWindows() => Build(BuildTarget.StandaloneWindows64, "Builds/Windows/FrontRooms.exe");

    /// <summary>
    /// Builds the browser target with the checked-in WebGL settings (Brotli + fallback,
    /// hashed files, data caching, and a conservative initial heap). The output is
    /// intentionally ignored by Git so the repository stays source-first.
    /// </summary>
    [MenuItem("FrontRooms/Build WebGL")]
    public static void BuildWebGL() => Build(BuildTarget.WebGL, "Builds/WebGL");

    public static void BuildAll()
    {
        BuildMac();
        BuildWindows();
    }

    private static void Build(BuildTarget target, string location)
    {
        FrontRoomsVerification.Run();
        if (!File.Exists(ScenePath)) CreateScene();
        ApplyPlayerSettings();
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = location,
            target = target,
            options = BuildOptions.None,
        });
        var summary = report.summary;
        Debug.Log(string.Format("[FrontRoomsBuild] {0}: {1} · {2} errors · {3:0.0} MB · {4}", target, summary.result, summary.totalErrors, summary.totalSize / 1048576f, location));
        if (summary.result != BuildResult.Succeeded && Application.isBatchMode) EditorApplication.Exit(1);
    }
}
