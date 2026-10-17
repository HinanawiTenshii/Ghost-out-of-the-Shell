using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class KnightQuestTitleSmoke
{
    const string Id = QuestJournalManager.KnightAmbushQuestId;
    const string Title = "不必刺杀骑士团长";
    static int checks;
    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks++;
    }

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"))
            throw new Exception("Isolated project only");
        Check(QuestJournalManager.KnightAmbushQuestTitle == Title, "canonical title");
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor2.unity");
        var notice = UnityEngine.Object.FindObjectsOfType<DocumentReader>(true)
            .Single(d => d.name == "巡逻队信息");
        var serialized = new SerializedObject(notice);
        Check(serialized.FindProperty("journalEntryTitle").stringValue == Title, "authored notice title");
        Check(serialized.FindProperty("journalEntryId").stringValue == Id, "stable quest ID");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        var go = new GameObject("Quest rename test");
        go.SetActive(false);
        var journal = go.AddComponent<QuestJournalManager>();
        foreach (bool completed in new[] { false, true })
        {
            var state = new QuestJournalManager.SaveState {
                tracked = Id,
                completed = completed ? new List<string> { Id } : new List<string>()
            };
            state.entries.Add(new QuestJournalManager.SavedEntry {
                id = QuestJournalManager.FortressProofQuestId, title = "失窃的证明", details = "parent", scene = "Level2-Floor2"
            });
            state.entries.Add(new QuestJournalManager.SavedEntry {
                id = Id, title = "骑士长的退路", details = "preserved progress", scene = "Level2-Floor2"
            });
            journal.ApplySaveState(JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(state)));
            var entry = journal.Entries.Single(e => e.Id == Id);
            Check(entry.Title == Title && entry.Details == "preserved progress", "old save title migration without resetting details");
            Check(journal.IsEntryCompleted(Id) == completed, "completion unchanged");
            Check(journal.TrackedEntryId == Id, "tracking unchanged");
            Check(journal.CaptureSaveState().entries.Single(e => e.id == Id).title == Title, "canonical title saved");
        }
        journal.AddOrUpdateEntry(Id, "骑士长的退路", "preserved progress");
        Check(journal.Entries.Single(e => e.Id == Id).Title == Title, "old scene snapshot cannot revert title");
        UnityEngine.Object.DestroyImmediate(go);
        Debug.Log("KNIGHT_QUEST_TITLE_PASS checks=" + checks);
        EditorApplication.Exit(0);
    }
}
