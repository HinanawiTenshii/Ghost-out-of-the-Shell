using System;
using System.Collections;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneLoadingBenchmark
{
    static IEnumerator suite;
    static double deadline;
    static int frame = -1;
    static SceneLoadingBenchmark() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated project only");
        PlayerSettings.productName = "scene-loading-benchmark";
        RuntimeMiniMapSceneDataGenerator.GenerateSnapshots(false);
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        SessionState.SetBool("LoadingBenchmark", true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("LoadingBenchmark", false))
            { SessionState.SetBool("LoadingBenchmark", false); deadline = EditorApplication.timeSinceStartup + 240; suite = Verify(); }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("benchmark timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("SCENE_LOADING_BENCHMARK_PASS"); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Ready(string name)
    {
        int stable = 0;
        while (stable < 5)
        {
            stable = SceneManager.GetActiveScene().name == name && !RetroSceneLoadReveal.IsBlockingInput && !GameSaveSystem.IsPreparingSceneSave &&
                !GameSaveSystem.IsLoading && !SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress && ZeldaRuntimeRegistry.GetControlledMover() != null ? stable + 1 : 0;
            yield return null;
        }
    }
    static IEnumerator Verify()
    {
        var wait = Ready("Level2-Floor1"); while (wait.MoveNext()) yield return null;
        foreach (var name in new[] { "Level2-Floor2", "Level2-Floor-1", "Level2-Floor1" })
        {
            SceneTravelStateManager.GetOrCreate().TravelToScene(name, Vector2.zero);
            wait = Ready(name); while (wait.MoveNext()) yield return null;
            Debug.Log("BENCHMARK_SAVE_BYTES " + name + " " + new FileInfo(Path.Combine(GameSaveSystem.SaveDirectory, "auto.json")).Length);
        }
    }
}
