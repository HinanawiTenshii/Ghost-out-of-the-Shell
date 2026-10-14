using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class ArenaQuestCompatibilitySmoke
{
    static int checks;
    static ArenaQuestCompatibilitySmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string text)
    { if (!ok) throw new Exception(text); checks++; Debug.Log("ARENA_QUEST_COMPAT_CHECK " + text); }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated copy only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(ArenaQuestCompatibilitySmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(nameof(ArenaQuestCompatibilitySmoke), false)) return;
        SessionState.SetBool(nameof(ArenaQuestCompatibilitySmoke), false);
        try { Verify(); Debug.Log("ARENA_QUEST_COMPAT_PASS checks=" + checks); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Verify()
    {
        Time.timeScale = 0;
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        var arena = new GameObject("legacy arena fixture").AddComponent<ArenaChallengeController>();
        arena.ApplyRuntimeState(new ArenaChallengeController.RuntimeState()); arena.CompleteRestoration();
        Check(!journal.ContainsEntry(QuestJournalManager.ArenaQuestId), "legacy untouched arena does not accept quest");
        var snapshot = Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor-1");
        Check(snapshot.UsesCurrentSchema && snapshot.QuestTargets.Count(t => t.questId.StartsWith("level2.arena.")) == 3,
            "baked map contains three arena targets without loading scene");
        var mini = new GameObject("mini", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
        var menu = new GameObject("menu", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
        mini.RebuildFromSnapshot(snapshot); menu.RebuildFromSnapshot(snapshot);
        Check(!mini.TryGetQuestTarget(QuestJournalManager.ArenaQuestId, out _), "unaccepted quest has no tracked map target");
        foreach (var phase in new[] { ArenaChallengeController.Phase.Preparing, ArenaChallengeController.Phase.Fighting, ArenaChallengeController.Phase.Completed })
        {
            journal.ResetAllEntries();
            var legacy = new ArenaChallengeController.RuntimeState { phase = phase, roster = new[] { 0, 1, 2 },
                wins = phase == ArenaChallengeController.Phase.Completed ? 3 : 1, winnerId = "legacy-winner", entrantId = "legacy-winner" };
            arena.ApplyRuntimeState(legacy); arena.CompleteRestoration();
            Check(journal.ContainsEntry(QuestJournalManager.ArenaQuestId) && !journal.IsEntryCompleted(QuestJournalManager.ArenaQuestId),
                "legacy " + phase + " creates pending quest from restored arena");
            Check(journal.ArenaWins == legacy.wins && journal.ArenaRounds == 3, "legacy " + phase + " preserves progress");
            string target = phase == ArenaChallengeController.Phase.Preparing ? QuestJournalManager.ArenaEntranceTarget :
                phase == ArenaChallengeController.Phase.Fighting ? QuestJournalManager.ArenaCombatTarget : QuestJournalManager.ArenaRegistrarTarget;
            Vector2 expected = snapshot.QuestTargets.Single(t => t.questId == target).position;
            Check(mini.TryGetQuestTarget(QuestJournalManager.ArenaQuestId, out var a) && a == expected, "mini resolves " + target);
            Check(menu.TryGetQuestTarget(QuestJournalManager.ArenaQuestId, out var b) && b == expected, "menu resolves " + target);
            var save = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
            journal.ResetAllEntries(); journal.ApplySaveState(save);
            Check(journal.ArenaWins == legacy.wins && QuestJournalManager.ResolveQuestMapTargetId(QuestJournalManager.ArenaQuestId) == target,
                "journal alone retains " + phase + " across unloaded scene save/restore");
            Check(legacy.phase == phase && legacy.wins == journal.ArenaWins, "restoration did not mutate checkpoint");
        }
        journal.ResetAllEntries();
        arena.ApplyRuntimeState(new ArenaChallengeController.RuntimeState { phase = ArenaChallengeController.Phase.Completed,
            claimed = true, wins = 3, roster = new[] { 0, 1, 2 } }); arena.CompleteRestoration();
        Check(journal.IsEntryCompleted(QuestJournalManager.ArenaQuestId), "legacy claimed reward backfills completed quest");
        Check(!mini.TryGetQuestTarget(QuestJournalManager.ArenaQuestId, out _) && !menu.TryGetQuestTarget(QuestJournalManager.ArenaQuestId, out _),
            "completion removes both map targets");
        var saved = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        journal.ResetAllEntries(); journal.ApplySaveState(saved);
        Check(journal.IsEntryCompleted(QuestJournalManager.ArenaQuestId) && journal.ArenaStage == QuestJournalManager.ArenaQuestStage.Claimed,
            "migrated completion survives resave");
        journal.RecordArenaQuestProgress(new ArenaChallengeController.RuntimeState { phase = ArenaChallengeController.Phase.Preparing, roster = new[] { 0, 1 } });
        Check(journal.ArenaStage == QuestJournalManager.ArenaQuestStage.Claimed, "stale callbacks cannot undo completed reward");
        journal.ResetAllEntries();
        arena.ApplyRuntimeState(new ArenaChallengeController.RuntimeState { phase = ArenaChallengeController.Phase.Completed, roster = new int[0] });
        arena.CompleteRestoration();
        Check(journal.ArenaChallengeWon && journal.ArenaRounds == 0 && !journal.IsEntryCompleted(QuestJournalManager.ArenaQuestId),
            "zero-guardian poison outcome still requires actual reward claim");
        Check(journal.TryGetEntry(QuestJournalManager.ArenaQuestId, out var freshEntry) && freshEntry.Title == "你们还不满意吗？",
            "new arena quest uses requested title");
        VerifyTitleMigration(journal);
    }

    static void VerifyTitleMigration(QuestJournalManager journal)
    {
        foreach (bool completed in new[] { false, true })
        {
            var oldSave = new QuestJournalManager.SaveState {
                arenaQuestStage = completed ? QuestJournalManager.ArenaQuestStage.Claimed : QuestJournalManager.ArenaQuestStage.Fighting,
                arenaQuestWins = completed ? 3 : 1, arenaQuestRounds = 3,
                tracked = completed ? "rename.unrelated" : QuestJournalManager.ArenaQuestId,
                completed = new System.Collections.Generic.List<string>()
            };
            oldSave.entries.Add(new QuestJournalManager.SavedEntry { id = QuestJournalManager.ArenaQuestId,
                title = "以武为证", details = "legacy arena details", scene = "Level2-Floor-1" });
            oldSave.entries.Add(new QuestJournalManager.SavedEntry { id = "rename.unrelated",
                title = "无关任务", details = "unrelated details", scene = "Level2-Floor1" });
            if (completed) oldSave.completed.Add(QuestJournalManager.ArenaQuestId);
            journal.ResetAllEntries();
            journal.ApplySaveState(JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(oldSave)));
            string label = completed ? "completed" : "active";
            Check(journal.TryGetEntry(QuestJournalManager.ArenaQuestId, out var entry) && entry.Title == "你们还不满意吗？" &&
                entry.SourceScene == "Level2-Floor-1", label + " old title migrates without changing quest identity or scene");
            Check(journal.ArenaStage == oldSave.arenaQuestStage && journal.ArenaWins == oldSave.arenaQuestWins &&
                journal.ArenaRounds == 3 && journal.IsEntryCompleted(QuestJournalManager.ArenaQuestId) == completed,
                label + " rename preserves stage, wins and completion");
            Check(journal.TrackedEntryId == oldSave.tracked, label + " rename preserves tracking");
            Check(journal.TryGetEntry("rename.unrelated", out var unrelated) && unrelated.Title == "无关任务" &&
                unrelated.Details == "unrelated details", label + " migration leaves unrelated quest unchanged");
            var resaved = journal.CaptureSaveState();
            Check(resaved.entries.Count == 2 && resaved.entries.Single(e => e.id == QuestJournalManager.ArenaQuestId).title == "你们还不满意吗？",
                label + " resave writes new title without duplicating quest");
            journal.ResetAllEntries();
            journal.ApplySaveState(JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(resaved)));
            Check(journal.TryGetEntry(QuestJournalManager.ArenaQuestId, out entry) && entry.Title == "你们还不满意吗？" &&
                journal.ArenaWins == oldSave.arenaQuestWins && journal.IsEntryCompleted(QuestJournalManager.ArenaQuestId) == completed,
                label + " migrated title and progress survive another load");
        }
    }
}
