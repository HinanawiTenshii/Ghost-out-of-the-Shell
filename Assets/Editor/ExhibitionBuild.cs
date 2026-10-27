using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Run in a disposable project copy; never changes the main project's build settings.</summary>
public static class ExhibitionBuild
{
    public static readonly string[] Scenes = {
        "Assets/Scenes/TitleScreen.unity",
        "Assets/Scenes/TutorialLevel.unity",
        "Assets/Scenes/Level2/Level2-Desert.unity",
        "Assets/Scenes/Level2/Level2-Floor1.unity",
        "Assets/Scenes/Level2/Level2-Floor-1.unity",
        "Assets/Scenes/Level2/Level2-Floor2.unity",
        "Assets/Scenes/LastScene.unity"
    };

    public static void Prepare()
    {
        RequireIsolatedProject();
        PlayerSettings.companyName = "DefaultCompany";
        PlayerSettings.productName = ExhibitionEdition.ProductName;
        PlayerSettings.bundleVersion = "1.0-exhibition";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Standalone, "com.cogitans.extensa.exhibition");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.SetManagedStrippingLevel(BuildTargetGroup.Standalone, ManagedStrippingLevel.Disabled);
        var defines = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone)
            .Split(';').Where(s => !string.IsNullOrWhiteSpace(s)).ToList();
        if (!defines.Contains("GOS_EXHIBITION")) defines.Add("GOS_EXHIBITION");
        PlayerSettings.SetScriptingDefineSymbolsForGroup(BuildTargetGroup.Standalone, string.Join(";", defines));
        EditorBuildSettings.scenes = Scenes.Select(path => new EditorBuildSettingsScene(path, true)).ToArray();
        AssetDatabase.SaveAssets();
        Debug.Log("EXHIBITION_PREPARED: restart the isolated editor to compile the edition symbol before Build.");
    }

    public static void Build()
    {
        try
        {
            RequireIsolatedProject();
            if (!ExhibitionEdition.IsEnabled || PlayerSettings.productName != ExhibitionEdition.ProductName)
                throw new InvalidOperationException("Run Prepare in a separate editor invocation first.");
            var args = Environment.GetCommandLineArgs();
            int option = Array.IndexOf(args, "-exhibitionOutput");
            if (option < 0 || option + 1 >= args.Length) throw new ArgumentException("Missing -exhibitionOutput folder");
            string output = Path.GetFullPath(args[option + 1]);
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any())
                throw new IOException("Build destination must be empty; existing releases are never overwritten: " + output);
            Directory.CreateDirectory(output);
            if (!EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).SequenceEqual(Scenes))
                throw new InvalidOperationException("Unexpected exhibition scene list.");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions {
                scenes = Scenes, locationPathName = Path.Combine(output, "GoS.exe"),
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new Exception("Exhibition build failed: " + report.summary.result + ", errors=" + report.summary.totalErrors);
            File.WriteAllText(Path.Combine(output, "展会版说明.txt"),
                "Ghost out of the Shell · 核聚变特供版\r\n\r\n" +
                "运行 GoS.exe。请保留同目录下的 GoS_Data、MonoBleedingEdge 与 DLL 文件。\r\n" +
                "流程：教学关卡 → 边境沙漠 → 城镇（Level2）→ 尾声。此包不含 Level0、Level1。\r\n" +
                "角色状态下附身所需时间缩短2秒；体力上限为正式版的2倍。\r\n" +
                "首次进入Level2：已学习生命强化、灵魂标记Ⅱ、骨牌Ⅰ，拥有3点可用D.A.T.A；主动技能自动装入空闲技能栏。\r\n" +
                "该配置仅首次进入时发放；楼层往返、存读档不会重复发放。\r\n\r\n" +
                "独立进度目录：%USERPROFILE%\\AppData\\LocalLow\\DefaultCompany\\" + ExhibitionEdition.ProductName + "\\Saves\r\n" +
                "正式版存档不会被读取或覆盖。\r\n");
            File.WriteAllText(Path.Combine(output, "build-manifest.json"), JsonUtility.ToJson(new Manifest {
                edition = ExhibitionEdition.Caption, product = PlayerSettings.productName,
                unity = Application.unityVersion, utc = DateTime.UtcNow.ToString("O"),
                scenes = Scenes, sizeBytes = report.summary.totalSize.ToString(),
                platform = "Windows x86_64", development = false
            }, true));
            Debug.Log("EXHIBITION_BUILD_SUCCESS " + output + " bytes=" + report.summary.totalSize);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }

    [Serializable] private sealed class Manifest
    {
        public string edition, product, unity, utc, sizeBytes, platform;
        public string[] scenes;
        public bool development;
    }

    private static void RequireIsolatedProject()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"))
            throw new InvalidOperationException("Exhibition build settings may only be changed inside a disposable .codex-temp project copy.");
    }
}
