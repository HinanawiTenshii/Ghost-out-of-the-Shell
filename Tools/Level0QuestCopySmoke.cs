using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Level0QuestCopySmoke
{
    const string Key = nameof(Level0QuestCopySmoke);
    const string Escape = "level0.escape_prison", Gate = "level0.obtain_gate_key";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks;
    static Level0QuestCopySmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label)
    { if (!ok) throw new Exception(label); checks++; Debug.Log("LEVEL0_COPY_CHECK " + label); }
    static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, F).GetValue(obj);
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated test only");
        EditorSceneManager.OpenScene("Assets/Scenes/Level0.unity");
        var definitions = UnityEngine.Object.FindObjectsOfType<QuestJournalSceneData>(true)
            .SelectMany(s => Field<QuestJournalEntryDefinition[]>(s, "initialEntries")).ToArray();
        var main = definitions.Single(d => d.entryId == Escape);
        Check(main.title == "只有死人才能离开" && main.details == QuestJournalManager.Level0EscapeDescription &&
            !main.addOnlyWhenTriggered, "authored main quest uses final copy and retains automatic acceptance");
        Check(!main.details.Contains("死亡") && main.details.Contains("原本的肉体已失去控制"),
            "description explains lost bodily control without implying death");
        var doc = UnityEngine.Object.FindObjectsOfType<DocumentReader>(true).Single(d => Field<string>(d, "journalEntryId") == Gate);
        Check(Field<string>(doc, "journalEntryTitle") == "在法的门前" &&
            Field<string>(doc, "journalEntryDetails") == QuestJournalManager.Level0GateKeyDescription &&
            Field<bool>(doc, "addJournalEntryOnOpen"), "authored key document retains read trigger with final copy");
        Check(doc.DocumentText.Contains("典狱长") && doc.DocumentText.Contains("报备"), "original notice and permission clue retained");
        var map = Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level0");
        Check(map.QuestTargets.Count(t => t.questId == Escape) == 1 && map.QuestTargets.Count(t => t.questId == Gate) == 1,
            "both stable map destination IDs remain present");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetInt(Key + "checks", checks); SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false)) return;
        SessionState.SetBool(Key, false); checks = SessionState.GetInt(Key + "checks", 0);
        try { Verify(); Debug.Log("LEVEL0_QUEST_COPY_PASS checks=" + checks); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static QuestJournalManager.SaveState RoundTrip(QuestJournalManager.SaveState state) =>
        JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(state));
    static void Verify()
    {
        Time.timeScale = 0;
        var journal = QuestJournalManager.GetOrCreate();
        foreach (string id in new[] { Escape, Gate })
        {
            string oldTitle = id == Escape ? "逃脱者" : "出行审查";
            string title = id == Escape ? "只有死人才能离开" : "在法的门前";
            string oldDetails = id == Escape ? "设法从大门离开监狱" : "大门钥匙目前位于典狱长的房间内，需要设法进入该房间以获得大门钥匙";
            string details = id == Escape ? QuestJournalManager.Level0EscapeDescription : QuestJournalManager.Level0GateKeyDescription;
            foreach (bool completed in new[] { false, true })
            {
                const string note = "\n\n补充线索：已检查过巡逻路线。";
                var save = new QuestJournalManager.SaveState {
                    tracked = id, initialTracked = Escape, completed = new List<string>(),
                    initialized = new List<string> { "Level0" }
                };
                save.entries.Add(new QuestJournalManager.SavedEntry { id = id, title = oldTitle,
                    details = oldDetails + note, scene = "Level0", isLevelObjective = id == Escape });
                save.entries.Add(new QuestJournalManager.SavedEntry { id = "copy.unrelated", title = "无关任务", details = "自定义描述" });
                if (completed) save.completed.Add(id);
                journal.ResetAllEntries(); journal.ApplySaveState(RoundTrip(save));
                Check(journal.TryGetEntry(id, out var entry) && entry.Title == title && entry.Details == details + note,
                    id + " legacy title and description migrate while preserving appended notes");
                Check(journal.IsEntryCompleted(id) == completed && journal.TrackedEntryId == id && journal.InitialTrackedEntryId == Escape &&
                    entry.SourceScene == "Level0" && entry.IsLevelObjective == (id == Escape), id + " preserves progress, tracking and level objective flag");
                Check(journal.TryGetEntry("copy.unrelated", out var unrelated) && unrelated.Title == "无关任务" && unrelated.Details == "自定义描述",
                    "unrelated quest remains unchanged");
                var saved = journal.CaptureSaveState();
                Check(saved.entries.Count == 2 && saved.entries.Single(e => e.id == id).title == title &&
                    saved.entries.Single(e => e.id == id).details == details + note, "resave stores final copy without duplicates");
                journal.ResetAllEntries(); journal.ApplySaveState(RoundTrip(saved));
                Check(journal.TryGetEntry(id, out entry) && entry.Details == details + note && journal.IsEntryCompleted(id) == completed,
                    "repeated load preserves text and completion");
            }
            journal.ResetAllEntries();
            bool notificationUsesNewCopy = false;
            Action<QuestJournalChangeKind> onChange = kind => {
                if (kind == QuestJournalChangeKind.Added)
                    notificationUsesNewCopy = journal.TryGetEntry(id, out var e) && e.Title == title && e.Details == details;
            };
            journal.JournalEntriesChanged += onChange;
            journal.AddOrUpdateEntry(id, oldTitle, oldDetails, "Level0");
            journal.JournalEntriesChanged -= onChange;
            Check(notificationUsesNewCopy, "old scene snapshot callback exposes final copy to UI");
            journal.AddOrUpdateEntry(id, oldTitle, "自定义阶段描述", "Level0");
            Check(journal.TryGetEntry(id, out var custom) && custom.Title == title && custom.Details == "自定义阶段描述",
                "custom stage descriptions are not overwritten");
            journal.AddOrUpdateEntry(id, oldTitle, oldDetails + "。\n后续线索", "Level0");
            Check(journal.TryGetEntry(id, out custom) && custom.Details == details + "\n后续线索", "punctuated legacy description migrates safely");
        }
        const string priorCopy = "高墙与铁门拦住了囚犯，却未必关得住一个失去肉身的灵魂。换一张面孔，借一双脚步，也许就能从守卫眼前走向外面的世界。\n\n死亡并没有带来自由，但或许能成为越狱的开始。\n\n当前目标：设法从大门逃出监狱。";
        foreach (bool completed in new[] { false, true })
        {
            var prior = new QuestJournalManager.SaveState { tracked = Escape, initialTracked = Escape, completed = new List<string>() };
            prior.entries.Add(new QuestJournalManager.SavedEntry { id = Escape, title = "只有死人才能离开",
                details = priorCopy + "\n\n追加线索", scene = "Level0", isLevelObjective = true });
            if (completed) prior.completed.Add(Escape);
            journal.ResetAllEntries(); journal.ApplySaveState(RoundTrip(prior));
            Check(journal.TryGetEntry(Escape, out var migrated) && migrated.Title == "只有死人才能离开" &&
                migrated.Details == QuestJournalManager.Level0EscapeDescription + "\n\n追加线索", "previous homage copy replaced without changing title or notes");
            Check(journal.IsEntryCompleted(Escape) == completed && journal.TrackedEntryId == Escape && migrated.IsLevelObjective,
                "body-control correction preserves progress and tracking");
            var saved = RoundTrip(journal.CaptureSaveState()); journal.ResetAllEntries(); journal.ApplySaveState(saved);
            Check(journal.TryGetEntry(Escape, out migrated) && !migrated.Details.Contains("死亡") &&
                migrated.Details.EndsWith("追加线索") && journal.IsEntryCompleted(Escape) == completed,
                "corrected description survives resave and reload");
        }
        journal.ResetAllEntries();
        Check(journal.RecordQuestCompletion(Gate) && !journal.ContainsEntry(Gate), "key acquired before reading still records hidden completion");
        journal.AddOrUpdateEntry(Gate, "出行审查", "", "Level0");
        Check(journal.IsEntryCompleted(Gate) && journal.TryGetEntry(Gate, out var key) && key.Title == "在法的门前" &&
            key.Details == QuestJournalManager.Level0GateKeyDescription, "early key completion survives renamed task acceptance");
        journal.AddOrUpdateEntry("level1.escape_castle", "old", "Level1 original details");
        Check(journal.TryGetEntry("level1.escape_castle", out var level1) && level1.Title == QuestJournalManager.Level1EscapeTitle &&
            level1.Details == "Level1 original details", "Level1 custom copy remains unchanged with its canonical title");
    }
}
