using System;
using System.IO;
using FrontRooms.Race;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor-only harness for the data-only race slice. It never loads or mutates the current gameplay scene.
/// Batch mode: Unity -batchmode -projectPath <project> -executeMethod FrontRoomsRaceVerification.RunBatch -quit
/// Optional args: -race-count 100, -race-seed 123, -race-output /absolute/path/report.json.
/// </summary>
public static class FrontRoomsRaceVerification
{
    private const int DefaultSeedCount = 100;
    private const int DefaultSeed = 0x13579BDF;

    [MenuItem("FrontRooms/Race Slice/Verify 100 seeds")]
    public static void Verify100Seeds()
    {
        var report = BuildReport(DefaultSeedCount, DefaultSeed);
        var path = WriteReport(report, null);
        Debug.Log("[FrontRoomsRace] " + (report.passed ? "PASS" : "FAIL") + " · " + path);
    }

    [MenuItem("FrontRooms/Race Slice/Generate seed 324508639")]
    public static void GenerateExampleSeed()
    {
        var report = BuildReport(1, DefaultSeed);
        var path = WriteReport(report, null);
        Debug.Log("[FrontRoomsRace] seed " + DefaultSeed + " · " + path);
    }

    public static void RunBatch()
    {
        var count = ReadIntArgument("-race-count", DefaultSeedCount);
        count = Mathf.Clamp(count, 1, 1000);
        var seed = ReadIntArgument("-race-seed", DefaultSeed);
        var report = BuildReport(count, seed);
        var path = WriteReport(report, ReadStringArgument("-race-output", null));
        Debug.Log("[FrontRoomsRace] " + (report.passed ? "PASS" : "FAIL") + " · " + report.passedSeeds + "/" + report.requestedSeeds + " · " + path);
        if (Application.isBatchMode) EditorApplication.Exit(report.passed ? 0 : 1);
    }

    private static FrontRoomsRaceBatchReport BuildReport(int count, int firstSeed)
    {
        var report = new FrontRoomsRaceBatchReport { requestedSeeds = count, passed = true };
        for (var i = 0; i < count; i++)
        {
            // The golden-ratio increment visits the full 32-bit signed seed space without using UnityEngine.Random.
            var seed = unchecked(firstSeed + (int)(0x9E3779B9u * (uint)i));
            var spec = FrontRoomsRaceGenerator.Generate(seed);
            report.results.Add(spec);
            if (spec.validation != null && spec.validation.passed) report.passedSeeds++;
            else report.passed = false;
        }
        if (report.passedSeeds != report.requestedSeeds) report.passed = false;
        return report;
    }

    private static string WriteReport(FrontRoomsRaceBatchReport report, string explicitPath)
    {
        var projectRoot = Directory.GetParent(Application.dataPath).FullName;
        var path = string.IsNullOrEmpty(explicitPath) ? Path.Combine(projectRoot, "Verification", "race-slice-latest.json") : explicitPath;
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        return path;
    }

    private static int ReadIntArgument(string name, int fallback)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == name && int.TryParse(args[i + 1], out var value)) return value;
        return fallback;
    }

    private static string ReadStringArgument(string name, string fallback)
    {
        var args = Environment.GetCommandLineArgs();
        for (var i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return fallback;
    }
}
