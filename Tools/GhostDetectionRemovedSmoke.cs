using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in the isolated project, never in the player's save profile.
[InitializeOnLoad]
public static class GhostDetectionRemovedSmoke
{
    const string Key = nameof(GhostDetectionRemovedSmoke);
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static string failure;
    static GhostDetectionRemovedSmoke() { EditorApplication.update += Tick; }
    static void Check(bool condition, string label)
    {
        checks++;
        if (!condition) throw new Exception(label);
    }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "Isolated project required");
        PlayerSettings.productName = "ghost-detection-removed-" + Guid.NewGuid().ToString("N");
        EditorSceneManager.OpenScene("Assets/Scenes/Level0.unity");
        SessionState.SetBool(Key, true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Key, false))
            {
                SessionState.SetBool(Key, false);
                Application.logMessageReceived += Log;
                deadline = EditorApplication.timeSinceStartup + 120;
                suite = Verify();
            }
            if (suite == null) return;
            if (failure != null) throw new Exception(failure);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Removal verification timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("GHOST_DETECTION_REMOVED_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Log(string text, string stack, LogType kind)
    {
        if (kind == LogType.Exception || kind == LogType.Error) failure = text;
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsPreparingGameplay || RetroSceneLoadReveal.IsBlockingInput ||
            GameSaveSystem.IsPreparingSceneSave || ZeldaRuntimeRegistry.GetControlledMover() == null) yield return null;
        for (int i = 0; i < 3; i++) yield return null;
        var assembly = typeof(ZeldaFourWayMover).Assembly;
        foreach (var type in new[] { "GhostRecoveryMonitor", "GhostRecoveryReachability", "GhostRecoveryWorldCapture",
            "GhostRecoverySceneData", "GhostRecoveryPrompt", "GhostRecoveryEvaluation" })
            Check(assembly.GetType(type) == null, "No compiled runtime type: " + type);
        Check(typeof(GhostDetectionRemovedSmoke).Assembly.GetType("GhostRecoverySceneBaker") == null, "No build preprocessor remains");
        Check(Resources.LoadAll<ScriptableObject>("GhostRecovery").Length == 0, "No bundled detection catalogues");
        var ghost = ZeldaRuntimeRegistry.GetControlledMover();
        Check(ghost.CharacterData is GhostZeldaCharacterData, "Level0 still spawns the controlled ghost");
        // Remove all other bodies in this disposable running scene. There is
        // deliberately no possession candidate anywhere, not just behind a wall.
        foreach (var data in Object.FindObjectsOfType<ZeldaCharacterData>(true))
            if (data != ghost.CharacterData) Object.Destroy(data.gameObject);
        yield return null;
        Check(Object.FindObjectsOfType<ZeldaCharacterData>(true).Length == 1, "Only the player's ghost remains");
        float until = Time.unscaledTime + 8f;
        while (Time.unscaledTime < until)
        {
            if (PauseMenuController.IsPaused || Time.timeScale <= 0 || DocumentReader.IsInputBlocked)
                throw new Exception("An empty ghost region must not interrupt gameplay");
            yield return null;
        }
        Check(ghost.isActiveAndEnabled && ZeldaRuntimeRegistry.GetControlledMover() == ghost, "Ghost continues operating with zero candidates");
        var pause = Object.FindObjectOfType<PauseMenuController>();
        Check(pause != null, "Authored pause menu remains available");
        pause.OpenMenu(); Check(PauseMenuController.IsPaused && Time.timeScale == 0, "Pause still opens");
        typeof(PauseMenuController).GetMethod("OpenSaveSlots", Flags).Invoke(pause, new object[] { false });
        Check(SaveSlotPanel.IsOpen, "Player may still choose load manually");
        typeof(SaveSlotPanel).GetMethod("Close", Flags).Invoke(Object.FindObjectOfType<SaveSlotPanel>(), null);
        pause.CloseMenu(); Check(!PauseMenuController.IsPaused && Time.timeScale > 0, "Closing pause restores input/time");
    }
}
