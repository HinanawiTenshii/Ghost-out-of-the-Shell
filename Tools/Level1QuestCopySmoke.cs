using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class Level1QuestCopySmoke
{
    const string Key = nameof(Level1QuestCopySmoke);
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Ids = { "level1.escape_castle", "level1.craft_super_bomb", "level1.open_monster_cage", "level1.find_castle_gate_key" };
    static readonly string[] OldTitles = { "逃脱者（续集）", "爆破手", "笼中困兽", "老套，但有用" };
    static readonly string[] Titles = { "假如再也见不到你", "数到三，不许数到五", "最后的破坏者", "制钥师的答案" };
    static readonly string[] OldDetails = { "设法从大门逃出城堡", "信件中提到的超级炸弹似乎可以破坏城堡大门，需要找到[引线][超级炸弹外壳]与[魔法火药]后在信件旁边的工作台上合成超级炸弹", "笼中被束缚的怪物似乎具备破坏城堡大门的能力，但是其被一对锁所束缚，需要找到[牢笼密码]与[怪物牢笼钥匙]打开其束缚才能设法去控制它", "城堡大门被锁住了，需要找到对应的钥匙才能打开" };
    static readonly string[] Details = { QuestJournalManager.Level1EscapeDescription, QuestJournalManager.Level1BombDescription, QuestJournalManager.Level1MonsterDescription, QuestJournalManager.Level1GateKeyDescription };
    static readonly string[] Scenes = { "Level1-Garden", "Level1-Floor-1", "Level1-Floor1", "Level1-Garden" };
    static int checks;
    static Level1QuestCopySmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label)
    { if (!ok) throw new Exception(label); checks++; Debug.Log("LEVEL1_COPY_CHECK " + label); }
    static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, F).GetValue(obj);

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"))
            throw new Exception("Isolated test only");
        EditorSceneManager.OpenScene("Assets/Scenes/Level1/Level1-Garden.unity");
        var main = UnityEngine.Object.FindObjectsOfType<QuestJournalSceneData>(true)
            .SelectMany(s => Field<QuestJournalEntryDefinition[]>(s, "initialEntries")).Single(d => d.entryId == Ids[0]);
        Check(main.title == Titles[0] && main.details == Details[0] && !main.addOnlyWhenTriggered,
            "garden main quest has final copy and retains auto acceptance");
        var gate = UnityEngine.Object.FindObjectsOfType<DoorHingeInteraction>(true)
            .Single(d => Field<string>(d, "lockedJournalEntryId") == Ids[3]);
        Check(Field<string>(gate, "lockedJournalEntryTitle") == Titles[3] &&
            Field<string>(gate, "lockedJournalEntryDetails") == Details[3],
            "locked gate trigger has final key quest copy");
        InspectDocument("Level1-Floor-1", 1, "", true);
        InspectDocument("Level1-Floor1", 2, "", true);
        InspectDocument("Level1-Floor3", 2, "\n牢笼的密码为[0451]", false);
        for (int i = 0; i < Ids.Length; i++)
        {
            var map = Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/" + Scenes[i]);
            Check(map != null && map.QuestTargets.Any(t => t.questId == Ids[i]), Ids[i] + " stable map target retained");
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetInt(Key + "checks", checks);
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void InspectDocument(string scene, int i, string suffix, bool accepts)
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Level1/" + scene + ".unity");
        var doc = UnityEngine.Object.FindObjectsOfType<DocumentReader>(true)
            .Single(d => Field<string>(d, "journalEntryId") == Ids[i]);
        Check(Field<string>(doc, "journalEntryTitle") == Titles[i] &&
            Field<string>(doc, "journalEntryDetails") == Details[i] + suffix &&
            Field<bool>(doc, "addJournalEntryOnOpen") == accepts, scene + " authored document and trigger mode preserved");
        if (!accepts)
        {
            var changes = Field<QuestJournalEventChange[]>(doc, "journalChangesOnRead");
            Check(changes.Any(c => c.entryId == Ids[i] && c.operation == QuestJournalEventOperation.AppendDetails &&
                c.details == "牢笼的密码为[0451]"), "password still appends evidence rather than accepting quest");
        }
        Check(doc.DocumentText.Contains(i == 1 ? "老国王" : accepts ? "双重锁" : "0451"),
            scene + " original readable document unaffected");
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Key, false)) return;
        SessionState.SetBool(Key, false);
        checks = SessionState.GetInt(Key + "checks", 0);
        try { Verify(); Debug.Log("LEVEL1_QUEST_COPY_PASS checks=" + checks); EditorApplication.Exit(0); }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static QuestJournalManager.SaveState RoundTrip(QuestJournalManager.SaveState state) =>
        JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(state));
    static void Verify()
    {
        Time.timeScale = 0;
        var journal = QuestJournalManager.GetOrCreate();
        for (int i = 0; i < Ids.Length; i++)
        {
            string id = Ids[i];
            string note = i == 1 ? "\n已找到引信\n已找到超级炸弹外壳\n已找到魔力火药" :
                i == 2 ? "\n牢笼的密码为[0451]\n已找到怪物牢笼钥匙" : "\n追加线索：已经检查过巡逻路线。";
            foreach (bool completed in new[] { false, true })
            {
                var save = new QuestJournalManager.SaveState {
                    tracked = id, initialTracked = Ids[0],
                    initialized = new List<string> { "Level1-Garden", "Level1-Floor-1", "Level1-Floor1", "Level1-Floor3" },
                    completed = new List<string>()
                };
                save.entries.Add(new QuestJournalManager.SavedEntry { id = id, title = OldTitles[i],
                    details = OldDetails[i] + note, scene = Scenes[i], isLevelObjective = i == 0 });
                save.entries.Add(new QuestJournalManager.SavedEntry { id = "unrelated", title = "无关任务", details = "其他描述" });
                if (completed) save.completed.Add(id);
                journal.ResetAllEntries(); journal.ApplySaveState(RoundTrip(save));
                Check(journal.TryGetEntry(id, out var entry) && entry.Title == Titles[i] && entry.Details == Details[i] + note,
                    id + " legacy copy migrates and discoveries survive");
                Check(journal.IsEntryCompleted(id) == completed && journal.TrackedEntryId == id &&
                    journal.InitialTrackedEntryId == Ids[0] && entry.SourceScene == Scenes[i] &&
                    entry.IsLevelObjective == (i == 0), id + " progress, tracking and source preserved");
                Check(journal.TryGetEntry("unrelated", out var unrelated) && unrelated.Title == "无关任务" &&
                    unrelated.Details == "其他描述", "unrelated entries unchanged");
                var captured = RoundTrip(journal.CaptureSaveState());
                Check(captured.entries.Count == 2 && captured.initialized.SequenceEqual(save.initialized) &&
                    captured.entries.Single(e => e.id == id).title == Titles[i], "resave has canonical title and no duplicate quest");
                journal.ResetAllEntries(); journal.ApplySaveState(captured);
                Check(journal.TryGetEntry(id, out entry) && entry.Details == Details[i] + note &&
                    journal.IsEntryCompleted(id) == completed, "repeated load keeps final copy and progress");
            }
            journal.ResetAllEntries();
            bool notified = false;
            Action<QuestJournalChangeKind> handler = kind => {
                if (kind == QuestJournalChangeKind.Added)
                    notified = journal.TryGetEntry(id, out var e) && e.Title == Titles[i] && e.Details == Details[i];
            };
            journal.JournalEntriesChanged += handler;
            journal.AddOrUpdateEntry(id, OldTitles[i], OldDetails[i], Scenes[i]);
            journal.JournalEntriesChanged -= handler;
            Check(notified, id + " old scene snapshots notify UI with final copy immediately");
            Check(!journal.EntryDetailsContain(id, "0451") && !journal.EntryDetailsContain(id, "已找到"),
                "new description does not falsely mark clues discovered");
            journal.AddOrUpdateEntry(id, OldTitles[i], "自定义阶段内容", Scenes[i]);
            Check(journal.TryGetEntry(id, out var custom) && custom.Title == Titles[i] && custom.Details == "自定义阶段内容",
                "custom stage text preserved");
            journal.AddOrUpdateEntry(id, OldTitles[i], OldDetails[i] + "。\r\n新线索", Scenes[i]);
            Check(journal.TryGetEntry(id, out custom) && custom.Details == Details[i] + "\r\n新线索",
                "legacy punctuation and CRLF clues migrate");
            journal.AddOrUpdateEntry(id, OldTitles[i], OldDetails[i] + "（自定义改写）", Scenes[i]);
            Check(journal.TryGetEntry(id, out custom) && custom.Details == OldDetails[i] + "（自定义改写）",
                "similar but edited text is not overwritten");
            journal.ResetAllEntries();
            journal.ApplyEventChanges(new[] { new QuestJournalEventChange {
                entryId = id, operation = QuestJournalEventOperation.AppendDetails, details = note.Trim()
            } }, "Level1-Floor2");
            Check(journal.RecordQuestCompletion(id) && !journal.ContainsEntry(id), "completion before acceptance stays hidden");
            var pending = RoundTrip(journal.CaptureSaveState());
            journal.ResetAllEntries(); journal.ApplySaveState(pending);
            Check(!journal.ContainsEntry(id), "pending progress survives travel-style save and restore without revealing quest");
            journal.AddOrUpdateEntry(id, OldTitles[i], OldDetails[i], Scenes[i]);
            Check(journal.TryGetEntry(id, out var accepted) && accepted.Details == Details[i] + note &&
                accepted.Title == Titles[i] && journal.IsEntryCompleted(id), "early discoveries and completion apply on acceptance");
            journal.ResetAllEntries();
            journal.AddOrUpdateEntry(id, OldTitles[i], "", Scenes[i]);
            Check(journal.TryGetEntry(id, out var blank) && blank.Details == Details[i], "blank legacy entry receives final copy");
        }
        // Exercise the existing Level0 save/copy regression alongside the expanded migration.
        typeof(Level0QuestCopySmoke).GetMethod("Verify", F).Invoke(null, null);
        Check(true, "Level0 copy and save regression also passed");
    }
}
