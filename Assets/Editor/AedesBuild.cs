using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Headless Android player build, driven from the command line by <c>Tools/build.sh</c>.
///
/// Invoked as:
///   Unity -batchmode -nographics -quit -projectPath . -buildTarget Android \
///         -executeMethod AedesBuild.BuildAndroid \
///         -scenes "Assets/A.unity;Assets/B.unity" -outputPath build/Android/X.apk [-clean]
///
/// The player build *is* the APK: Unity writes a signed .apk straight to -outputPath, so
/// there is no separate packaging step. Debug signing is used because no custom keystore is
/// configured, which is what sideloading to a developer-mode headset needs.
/// </summary>
public static class AedesBuild
{
    /// <summary>Value of a "-name value" pair, or null when absent.</summary>
    private static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        // Stops one short: a trailing "-name" has no value to read.
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i] == name) return args[i + 1];
        return null;
    }

    /// <summary>
    /// Presence of a valueless flag. <see cref="Arg"/> cannot express this: it consumes a
    /// following value and ignores the final argv slot.
    /// </summary>
    private static bool HasFlag(string name) =>
        Environment.GetCommandLineArgs().Any(a => a == name);

    private static void Fail(string message)
    {
        Debug.LogError("[AedesBuild] " + message);
        EditorApplication.Exit(1);
    }

    public static void BuildAndroid()
    {
        string scenesArg = Arg("-scenes");
        string[] scenes = string.IsNullOrEmpty(scenesArg)
            ? EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray()
            : scenesArg.Split(';').Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();

        if (scenes.Length == 0)
        {
            Fail("No scenes to build. Pass -scenes, or enable scenes in Build Settings.");
            return;
        }

        foreach (var scene in scenes)
        {
            if (!File.Exists(scene))
            {
                Fail("Scene not found: " + scene);
                return;
            }
            Debug.Log("[AedesBuild] scene: " + scene);
        }

        string output = Arg("-outputPath");
        if (string.IsNullOrEmpty(output))
        {
            Fail("Missing -outputPath.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output)));

        // An .apk is what we sideload; an .aab cannot be installed with `adb install`.
        EditorUserBuildSettings.buildAppBundle = false;

        // A crashed build can leave the IL2CPP cache holding a stub libil2cpp.so, and every
        // later incremental build then reuses it and still reports success. -clean is the
        // way out. Tools/build.sh detects the stub and re-runs with this flag.
        bool clean = HasFlag("-clean");
        Debug.Log($"[AedesBuild] output: {output} (clean={clean})");

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = clean ? BuildOptions.CleanBuildCache : BuildOptions.None,
        });

        var summary = report.summary;
        Debug.Log($"[AedesBuild] result={summary.result} errors={summary.totalErrors} " +
                  $"warnings={summary.totalWarnings} size={summary.totalSize} time={summary.totalTime}");

        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
    }
}
