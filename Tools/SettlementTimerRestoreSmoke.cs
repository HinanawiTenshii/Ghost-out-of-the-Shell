using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>Run only in the disposable Unity project, with a separate save directory.</summary>
[InitializeOnLoad]
public static class SettlementTimerRestoreSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Stack<IEnumerator> routines;
    static int frame = -1, checks;
    static double deadline;
    static SettlementTimerRestoreSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string label)
    {
        checks++;
        if (!value) throw new Exception(label);
    }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
        PlayerSettings.productName = "settlement-timer-restore-smoke";
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        SessionState.SetBool(nameof(SettlementTimerRestoreSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SettlementTimerRestoreSmoke), false))
            {
                SessionState.SetBool(nameof(SettlementTimerRestoreSmoke), false);
                routines = new Stack<IEnumerator>(); routines.Push(Verify());
                deadline = EditorApplication.timeSinceStartup + 360;
            }
            if (routines == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("timer smoke timeout");
            while (routines.Count > 0)
            {
                var step = routines.Peek();
                if (!step.MoveNext()) { routines.Pop(); continue; }
                if (step.Current is IEnumerator child) { routines.Push(child); continue; }
                return;
            }
            routines = null;
            Debug.Log("SETTLEMENT_TIMER_RESTORE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception ex) { routines = null; Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static IEnumerator Ready(string scene)
    {
        yield return null; yield return null;
        while (SceneManager.GetActiveScene().name != scene || RetroSceneLoadReveal.IsBlockingInput ||
            GameSaveSystem.IsPreparingSceneSave || LevelRunStatistics.Restoring) yield return null;
        yield return null;
    }
    static void TimerTick()
    {
        typeof(LevelRunStatistics).GetMethod("LateUpdate", F).Invoke(Object.FindObjectOfType<LevelRunStatistics>(), null);
    }
    static void CheckContinues(double previous, bool partial, string label)
    {
        var s = LevelRunStatistics.Current;
        Check(s.hasElapsedTime && s.elapsedTimeIncomplete == partial, label + " timer validity");
        Check(s.elapsedSeconds >= previous && s.elapsedSeconds < previous + 3, label + " restored seconds, excluding load time");
        double before = s.elapsedSeconds;
        TimerTick();
        Check(s.elapsedSeconds > before, label + " timer actually advances");
        Check(LevelSettlementScreen.FormatClearTime(s) != "—", label + " visible duration");
    }
    static void MigrationCases()
    {
        foreach (string json in new[] {
            "{\"level\":2,\"deaths\":37}",
            "{\"level\":2,\"deaths\":37,\"hasElapsedTime\":false,\"elapsedSeconds\":0}",
            "{\"level\":2,\"deaths\":37,\"elapsedSeconds\":125.5}" })
        {
            var old = JsonUtility.FromJson<LevelRunStatistics.State>(json);
            double seconds = old.elapsedSeconds;
            LevelRunStatistics.Restore(old);
            CheckContinues(seconds, true, "legacy migration");
            Check(LevelRunStatistics.Current.deaths == 37 && LevelRunStatistics.Current.completeHistory, "timer migration preserves known action records");
            Check(!old.hasElapsedTime && old.elapsedSeconds == seconds, "migration does not mutate checkpoint source");
            var snapshot = LevelRunStatistics.Capture();
            LevelRunStatistics.Restore(snapshot);
            Check(LevelRunStatistics.Current.elapsedSeconds == snapshot.elapsedSeconds && LevelRunStatistics.Current.elapsedTimeIncomplete, "partial flag and exact seconds roundtrip");
        }
        foreach (double invalid in new[] { -1, double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            LevelRunStatistics.Restore(new LevelRunStatistics.State { level = 2, hasElapsedTime = true, elapsedSeconds = invalid });
            CheckContinues(0, true, "invalid duration repair");
        }
        LevelRunStatistics.Restore(null);
        CheckContinues(0, true, "save predating all run statistics");
        Check(!LevelRunStatistics.Current.completeHistory, "missing action history stays unknown");
        LevelRunStatistics.Restore(new LevelRunStatistics.State { level = 2, hasElapsedTime = true, elapsedSeconds = 90061.5 });
        Check(!LevelRunStatistics.Current.elapsedTimeIncomplete && LevelRunStatistics.Current.elapsedSeconds == 90061.5, "modern timer remains full and exact");
        Check(LevelSettlementScreen.FormatClearTime(LevelRunStatistics.Current) == "01时 01分 01秒", "24 hour display rollover retained");
        LevelRunStatistics.Current.hasElapsedTime = false;
        Check(LevelRunStatistics.Capture().elapsedTimeIncomplete && LevelRunStatistics.Current.hasElapsedTime, "capture repairs live legacy state before settlement/save");
        LevelRunStatistics.ResetSession();
        var cleared = LevelRunStatistics.Capture();
        Check(cleared.level == -1 && !cleared.hasElapsedTime && !cleared.elapsedTimeIncomplete && cleared.elapsedSeconds == 0, "main menu stays unrecorded");
        typeof(LevelRunStatistics).GetMethod("SceneLoaded", F).Invoke(Object.FindObjectOfType<LevelRunStatistics>(), new object[] { SceneManager.GetActiveScene(), LoadSceneMode.Single });
        Check(LevelRunStatistics.Current.hasElapsedTime && !LevelRunStatistics.Current.elapsedTimeIncomplete && LevelRunStatistics.Current.elapsedSeconds == 0, "fresh run starts complete timer");
    }
    static IEnumerator Verify()
    {
        yield return Ready("Level2-Floor1");
        Check(Application.persistentDataPath.Contains("settlement-timer-restore-smoke"), "user saves isolated");
        MigrationCases();
        var timer = LevelRunStatistics.Current;
        double seconds = timer.elapsedSeconds;
        Time.timeScale = 0; TimerTick();
        Check(timer.elapsedSeconds == seconds, "pause excludes time");
        Time.timeScale = 1;
        typeof(GameSaveSystem).GetField("loading", F).SetValue(GameSaveSystem.Instance, true);
        TimerTick();
        Check(timer.elapsedSeconds == seconds, "disk restore excludes time");
        typeof(GameSaveSystem).GetField("loading", F).SetValue(GameSaveSystem.Instance, false);
        typeof(RetroSceneLoadReveal).GetProperty("IsBlockingInput", F).SetValue(null, true);
        TimerTick();
        Check(timer.elapsedSeconds == seconds, "black cover excludes time");
        typeof(RetroSceneLoadReveal).GetProperty("IsBlockingInput", F).SetValue(null, false);

        var saves = GameSaveSystem.Instance;
        Check(saves.Save(1, false), "write real quicksave fixture");
        var legacy = saves.Read(1);
        // Reproduce the user's real save: known counters, but an inactive zero timer.
        legacy.player.levelRun.hasElapsedTime = false;
        legacy.player.levelRun.elapsedSeconds = 0;
        legacy.player.levelRun.elapsedTimeIncomplete = false;
        legacy.player.levelRun.deaths = 37;
        string path = (string)typeof(GameSaveSystem).GetMethod("SlotPath", F).Invoke(saves, new object[] { 1 });
        typeof(GameSaveSystem).GetMethod("WriteData", F).Invoke(null, new object[] { path, legacy });
        Check(saves.Load(1), "load real legacy-shaped save");
        yield return Ready("Level2-Floor1");
        CheckContinues(0, true, "disk legacy migration");
        Check(LevelRunStatistics.Current.deaths == 37, "real restore preserves screenshot action count");
        Check(saves.Read(0).player.levelRun.hasElapsedTime && saves.Read(0).player.levelRun.elapsedTimeIncomplete, "post-load autosave retains migration");
        for (int i = 0; i < 3; i++)
        {
            LevelRunStatistics.Current.elapsedSeconds += 60.25;
            Check(saves.Save(1, false), "repeated quicksave " + i);
            var snapshot = saves.Read(1).player.levelRun;
            LevelRunStatistics.Current.elapsedSeconds += 500;
            Check(saves.Load(1), "repeated quickload " + i);
            yield return Ready("Level2-Floor1");
            CheckContinues(snapshot.elapsedSeconds, true, "repeated disk roundtrip " + i);
        }
        seconds = LevelRunStatistics.Current.elapsedSeconds;
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level2-Floor2", Vector2.zero);
        yield return Ready("Level2-Floor2");
        CheckContinues(seconds, true, "same-level scene travel");
        LevelRunStatistics.Restore(new LevelRunStatistics.State { level = 2, hasElapsedTime = true, elapsedSeconds = 3661.75 });
        Check(saves.Save(2, false), "write modern manual save");
        Check(saves.Load(2), "load modern manual save");
        yield return Ready("Level2-Floor2");
        CheckContinues(3661.75, false, "modern manual roundtrip");
        var manager = SceneTravelStateManager.GetOrCreate();
        manager.CaptureQuickRestartCheckpoint(SceneManager.GetActiveScene());
        seconds = LevelRunStatistics.Current.elapsedSeconds;
        LevelRunStatistics.Current.elapsedSeconds += 500;
        Check(manager.RestartFromQuickCheckpoint(), "checkpoint restore starts");
        yield return Ready("Level2-Floor2");
        CheckContinues(seconds, false, "checkpoint roundtrip");
        LevelRunStatistics.Restore(new LevelRunStatistics.State { level = 2, hasElapsedTime = false, elapsedSeconds = 125.5 });
        Check(LevelSettlementScreen.TryOpen("LastScene"), "settlement opens");
        while (RetroSceneLoadReveal.IsBlockingInput) yield return null;
        var screen = Object.FindObjectOfType<LevelSettlementScreen>();
        var texts = screen.GetComponentsInChildren<Text>();
        Check(texts.Single(t => t.name == "Clear Time").text == "00时 02分 05秒", "migrated report displays digits");
        Check(texts.Any(t => t.text == "通关用时（部分记录）") && texts.Any(t => t.text == "仅统计恢复计时后的已记录时段"), "partial time clearly distinguished from total");
        var note = texts.Single(t => t.text == "仅统计恢复计时后的已记录时段");
        Check(-note.rectTransform.anchoredPosition.y + note.rectTransform.sizeDelta.y < 141, "partial note fits above header divider");
        TimerTick();
        Check(LevelRunStatistics.Current.elapsedSeconds == 125.5, "settlement freezes timer");
        typeof(LevelSettlementSmoke).GetMethod("Render", F).Invoke(null, new object[] { screen, 1280, 720, "partial-timer" });
    }
}
