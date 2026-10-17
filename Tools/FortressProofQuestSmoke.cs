using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class FortressProofQuestSmoke
{
    const string Key = nameof(FortressProofQuestSmoke), Id = QuestJournalManager.FortressProofQuestId;
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static int checks, frame = -1;
    static IEnumerator suite;
    static double deadline;
    static FortressProofQuestSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label)
    { if (!ok) throw new Exception(label); checks++; Debug.Log("FORTRESS_QUEST_CHECK " + label); }
    static FieldInfo Field(object obj, string name)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { var field = t.GetField(name, F | BindingFlags.DeclaredOnly); if (field != null) return field; }
        throw new Exception(name);
    }
    static T Get<T>(object obj, string name) => (T)Field(obj, name).GetValue(obj);
    static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
    static QuestJournalManager.SaveState Save(QuestJournalManager journal) =>
        JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated project only");
        PlayerSettings.productName = "fortress-proof-quest-smoke";
        Check(QuestJournalManager.FortressProofQuestTitle == "失窃的证明", "official quest title");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor2.unity");
        var notice = Object.FindObjectsOfType<DocumentReader>(true).Single(d => d.name == "要塞情报");
        Check(Get<bool>(notice, "addJournalEntryOnOpen") && Get<string>(notice, "journalEntryId") == Id &&
            Get<bool>(notice, "addJournalEntryOnlyOnce"), "Floor2 intelligence starts new quest only on reading");
        Check(Get<string>(notice, "journalEntryTitle") == QuestJournalManager.FortressProofQuestTitle &&
            Get<string>(notice, "journalEntryDetails") == QuestJournalManager.FortressProofDescription,
            "authored name and straightforward journal copy match runtime");
        Check(string.IsNullOrEmpty(Get<string>(notice, "journalParentEntryId")), "independent quest has no accidental prerequisite");
        Check(notice.DocumentText.Contains("要塞二楼") && notice.DocumentText.Contains("守卫者骑士长") &&
            notice.DocumentText.Contains("权限极高") && notice.DocumentText.Contains("守卫严密") &&
            notice.DocumentText.Contains("试炼证明"), "intelligence covers location, permissions, guards and reward");
        var proof = Object.FindObjectsOfType<TrialProofPickupItem>(true).Single(p =>
            Get<QuestJournalEventChange[]>(p, "journalChangesOnPickup").Any(c => c.entryId == Id && c.operation == QuestJournalEventOperation.Complete));
        Check(Vector2.Distance(proof.transform.position, new Vector2(-48, 32.91f)) < .001f, "existing proof has not moved");
        Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(scene, Id, out var target) &&
            Vector2.Distance(target, proof.transform.position) < .001f, "live map targets exact configured proof");
        proof.gameObject.SetActive(false);
        Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(scene, Id, out var unloaded) && target == unloaded,
            "distance-unloaded proof retains target");
        proof.gameObject.SetActive(true);
        var snapshot = Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor2");
        Check(snapshot.QuestTargets.Count(t => t.questId == Id) == 1 &&
            Vector2.Distance(snapshot.QuestTargets.Single(t => t.questId == Id).position, target) < .001f,
            "committed unloaded-scene snapshot includes same target");
        var previous = snapshot.QuestTargets.Where(t => t.questId != Id).ToArray();
        typeof(RuntimeMiniMapSceneDataGenerator).GetMethod("GenerateSceneSnapshot", F).Invoke(null, new object[] {
            scene.path, "Assets/Resources/MiniMaps/Level2-Floor2.asset", scene.name });
        Check(snapshot.QuestTargets.Count(t => t.questId == Id) == 1 &&
            Vector2.Distance(snapshot.QuestTargets.Single(t => t.questId == Id).position, target) < .001f,
            "automatic rebake retains quest target");
        foreach (var old in previous)
            Check(snapshot.QuestTargets.Any(t => t.questId == old.questId && t.position == old.position), "existing map target retained " + old.questId);
        AssetDatabase.SaveAssetIfDirty(snapshot);
        Debug.Log("FORTRESS_PROOF_MAP_TARGET " + target.ToString("F6") + " revision=" + snapshot.SourceRevision);

        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(fixture);
        var docCopy = Object.Instantiate(notice.gameObject); docCopy.name = "要塞情报"; docCopy.transform.position = Vector3.zero;
        var proofCopy = Object.Instantiate(proof.gameObject); proofCopy.name = "Fortress proof fixture";
        // The original item is nested under a translated scene root. Detaching
        // a prefab-instance clone must preserve its world position explicitly.
        proofCopy.transform.position = proof.transform.position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(proofCopy.transform);
        EditorSceneManager.CloseScene(scene, true);
        SessionState.SetInt(Key + ".checks", checks); SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(Key, false))
            {
                SessionState.SetBool(Key, false); checks = SessionState.GetInt(Key + ".checks", 0);
                suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90;
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Quest test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("FORTRESS_PROOF_QUEST_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Close(DocumentReader doc) { Call(doc, "CloseDocument"); Time.timeScale = 0; }
    static void CloseItem() { ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 0; }
    static void Hud(QuestJournalManager journal, bool complete)
    {
        var texts = new List<string>(); var done = new List<bool>();
        typeof(ZeldaHealthHeartsUI).GetMethod("BuildQuestObjectiveState", F).Invoke(null, new object[] { Id, journal, texts, done });
        Check(texts.Count == 1 && texts[0] == QuestJournalManager.FortressProofObjective &&
            done.Count == 1 && done[0] == complete, "right HUD objective and completion");
    }
    static IEnumerator Verify()
    {
        var doc = Object.FindObjectOfType<DocumentReader>();
        var proof = Object.FindObjectOfType<TrialProofPickupItem>();
        var completion = Get<QuestJournalEventChange[]>(proof, "journalChangesOnPickup");
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        Call(journal, "RefreshLevel2HeroProof", "Level2-Floor2", true);
        var actor = new GameObject("Quest tester").AddComponent<CivilianZeldaCharacterData>();
        var mover = actor.gameObject.AddComponent<ZeldaFourWayMover>(); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        actor.transform.position = doc.transform.position;
        var invGo = new GameObject("Quest inventory"); invGo.SetActive(false);
        var inv = invGo.AddComponent<PersistentInventory>(); Set(inv, "saveToPlayerPrefs", false);
        invGo.SetActive(true); inv.enabled = false; inv.ClearAll();
        yield return null; yield return null;
        while (DocumentReader.IsInputBlocked) yield return null;
        Time.timeScale = 0; Physics2D.SyncTransforms();
        Check(!journal.ContainsEntry(Id), "loading document does not accept quest");
        Call(doc, "OpenDocument");
        Check(DocumentReader.IsDocumentOpen && journal.ContainsEntry(Id) && journal.TrackedEntryId == Id &&
            !journal.IsEntryCompleted(Id), "actual reading opens window, starts and tracks quest");
        Hud(journal, false); Close(doc);
        yield return null; yield return null;
        Call(doc, "OpenDocument"); Close(doc);
        Check(journal.Entries.Count(e => e.Id == Id) == 1, "rereading does not duplicate quest");

        var state = Save(journal);
        state.entries.Single(e => e.id == Id).title = "要塞中的证明";
        journal.ResetAllEntries(); journal.ApplySaveState(state);
        Check(journal.Entries.Single(e => e.Id == Id).Title == "失窃的证明" &&
            Save(journal).entries.Single(e => e.id == Id).title == "失窃的证明",
            "legacy active quest title upgrades on load and is saved canonically");
        Check(journal.ContainsEntry(Id) && journal.TrackedEntryId == Id && !journal.IsEntryCompleted(Id),
            "quest acceptance and tracking survive travel-style JSON restore");
        var map = new GameObject("Proof map", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
        map.RebuildFromSnapshot(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor2"));
        Check(map.TryGetQuestTarget(Id, out var point) && Vector2.Distance(point, proof.transform.position) < .001f,
            "menu snapshot points to actual proof while Floor2 unloaded: map=" + point + " proof=" + proof.transform.position +
            " targets=" + Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor2").QuestTargets.Length);
        var menuGo = new GameObject("Quest menu"); menuGo.SetActive(false);
        var menu = menuGo.AddComponent<TabJournalMenuController>(); Set(menu, "questJournal", journal);
        Call(menu, "ApplyTrackedQuestTargetToMap", "Level2-Floor2", map, false);
        Check(Get<bool>(map, "showTrackedQuestTarget"), "map shows tracked target");
        Check((bool)typeof(TabJournalMenuController).GetMethod("SceneHasQuestTarget", F).Invoke(null,
            new object[] { "Level2-Floor2", SceneManager.GetActiveScene(), Id }), "Floor2 region receives objective indicator");
        foreach (string resource in new[] { "TrialProofPickupItem", "CounterfeitTrialProofPickupItem" })
        {
            var other = Object.Instantiate(Resources.Load<PickupItemBase>("PickupItems/" + resource));
            Check(other.TryPickUp() && !journal.IsEntryCompleted(Id), resource + " does not finish fortress task");
            CloseItem(); yield return null;
        }
        var wormProof = Object.Instantiate(Resources.Load<TrialProofPickupItem>("PickupItems/TrialProofPickupItem"));
        wormProof.SetUniqueInstanceId(TrialProofPickupItem.SandwormRewardIdentityPrefix + Guid.NewGuid().ToString("N"));
        Check(wormProof.TryPickUp() && !journal.IsEntryCompleted(Id), "sandworm proof does not finish fortress task");
        CloseItem(); yield return null;
        Check(proof.TryPickUp() && journal.IsEntryCompleted(Id), "collecting exact fortress proof completes quest");
        CloseItem(); Hud(journal, true);
        Check(journal.TrackedEntryId == QuestJournalManager.Level2HeroProofId, "completion restores main objective tracking");
        Call(menu, "ApplyTrackedQuestTargetToMap", "Level2-Floor2", map, false);
        Check(!Get<bool>(map, "showTrackedQuestTarget"), "completed fortress marker removed");
        state = Save(journal); state.entries.Single(e => e.id == Id).title = "要塞中的证明";
        journal.ResetAllEntries(); journal.ApplySaveState(state);
        Check(journal.Entries.Single(e => e.Id == Id).Title == "失窃的证明",
            "legacy completed quest title upgrades without resetting progress");
        Check(journal.IsEntryCompleted(Id), "completed state survives JSON save");
        yield return null; yield return null;
        Set(doc, "journalEntryWasAdded", false); Call(doc, "OpenDocument"); Close(doc);
        Check(journal.IsEntryCompleted(Id) && journal.Entries.Count(e => e.Id == Id) == 1, "reread after restore cannot restart completed quest");

        journal.ResetAllEntries(); inv.ClearAll();
        var early = Object.Instantiate(Resources.Load<TrialProofPickupItem>("PickupItems/TrialProofPickupItem"));
        Set(early, "journalChangesOnPickup", completion);
        Check(early.TryPickUp() && !journal.ContainsEntry(Id), "early pickup stores evidence without accepting unread quest");
        CloseItem();
        state = Save(journal); journal.ResetAllEntries(); journal.ApplySaveState(state);
        yield return null; yield return null;
        Set(doc, "journalEntryWasAdded", false); Call(doc, "OpenDocument"); Close(doc);
        Check(journal.ContainsEntry(Id) && journal.IsEntryCompleted(Id), "reading after early pickup and save immediately completes quest");
    }
}

