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
public static class Level2SandwormQuestSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const string Id = QuestJournalManager.Level2SandwormQuestId;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static Level2SandwormQuestSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); Debug.Log("SANDWORM_QUEST_CHECK " + label); }
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
    static void Set(object obj, string name, object value) => obj.GetType().GetField(name, F).SetValue(obj, value);
    static T Get<T>(object obj, string name) => (T)obj.GetType().GetField(name, F).GetValue(obj);
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated project required");
        PlayerSettings.productName = "level2-sandworm-quest-smoke";
        var floor = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        var notice = Object.FindObjectsOfType<DocumentReader>(true).Single(d => d.gameObject.scene == floor && d.name == "沙虫公告");
        Check(Get<bool>(notice, "addJournalEntryOnOpen") && Get<string>(notice, "journalEntryId") == Id, "authored notice starts correct task");
        Check(Get<string>(notice, "journalEntryTitle") == "香料与獠牙", "Dune homage title authored");
        Check(Get<string>(notice, "journalEntryDetails").Contains("拾取") && notice.DocumentText.Contains("试炼证明") && notice.DocumentText.Contains("边境驻军"), "notice and task text polished and include reward clue");
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(fixture);
        var copy = Object.Instantiate(notice.gameObject); copy.name = "沙虫公告"; copy.transform.position = Vector3.zero;
        EditorSceneManager.CloseScene(floor, true);
        const string desertPath = "Assets/Scenes/Level2/Level2-Desert.unity";
        var desert = EditorSceneManager.OpenScene(desertPath, OpenSceneMode.Additive);
        Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(desert, Id, out var point) && Vector2.Distance(point, new Vector2(0,32.1f)) < .001f,
            "saved desert has fixed center target independent of live boss");
        typeof(RuntimeMiniMapSceneDataGenerator).GetMethod("GenerateSceneSnapshot", F).Invoke(null,
            new object[] { desertPath, "Assets/Resources/MiniMaps/Level2-Desert.asset", "Level2-Desert" });
        AssetDatabase.SaveAssetIfDirty(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Desert"));
        EditorSceneManager.CloseScene(desert, true); SceneManager.SetActiveScene(fixture);
        SessionState.SetBool(nameof(Level2SandwormQuestSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(Level2SandwormQuestSmoke), false))
            { SessionState.SetBool(nameof(Level2SandwormQuestSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Quest test timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("LEVEL2_SANDWORM_QUEST_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Close(DocumentReader notice) { Call(notice, "CloseDocument"); Time.timeScale = 0; }
    static void CloseItem() { ItemDescriptionWindow.GetOrCreate().CloseWindow(); Time.timeScale = 0; }
    static void CheckHudObjective(QuestJournalManager journal, bool complete)
    {
        var texts = new List<string>(); var states = new List<bool>();
        typeof(ZeldaHealthHeartsUI).GetMethod("BuildQuestObjectiveState", F).Invoke(null,
            new object[] { Id, journal, texts, states });
        Check(texts.Count == 1 && texts[0] == "击杀沙虫并取得试炼证明", "right-side HUD has exact sandworm objective");
        Check(states.Count == 1 && states[0] == complete, "HUD objective completion follows journal");
    }
    static IEnumerator Verify()
    {
        var floor = SceneManager.GetActiveScene();
        var notice = Object.FindObjectsOfType<DocumentReader>(true).Single(d => d.name == "沙虫公告");
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        Call(journal, "RefreshLevel2HeroProof", "Level2-Floor1", true);
        var actor = new GameObject("Notice reader").AddComponent<CivilianZeldaCharacterData>();
        actor.transform.position = notice.transform.position;
        var mover = actor.gameObject.AddComponent<ZeldaFourWayMover>(); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        var invRoot = new GameObject("Quest Inventory"); invRoot.SetActive(false);
        var inventory = invRoot.AddComponent<PersistentInventory>(); Set(inventory, "saveToPlayerPrefs", false);
        invRoot.SetActive(true); inventory.enabled = false; inventory.ClearAll();
        yield return null; yield return null;
        while (DocumentReader.IsInputBlocked) yield return null;
        Time.timeScale = 0; actor.transform.position = notice.transform.position; Physics2D.SyncTransforms();
        Check(Call(notice, "GetControlledCharacterNearby") != null, "reader fixture is within interaction distance in same scene");
        Check(!journal.ContainsEntry(Id), "quest not granted merely by loading notice");
        Call(notice, "OpenDocument");
        Check(DocumentReader.IsDocumentOpen && journal.ContainsEntry(Id), "actual reading interaction starts task");
        Check(journal.TryGetEntry(Id, out var entry) && entry.Title == "香料与獠牙" && !entry.IsLevelObjective, "side quest leaves main objective intact");
        Check(journal.TrackedEntryId == Id && !journal.IsEntryCompleted(Id), "new quest tracked and initially incomplete");
        CheckHudObjective(journal, false);
        Close(notice); yield return null; yield return null;
        Call(notice, "OpenDocument"); Close(notice);
        Check(journal.Entries.Count(e => e.Id == Id) == 1, "rereading does not duplicate task");
        var save = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        save.entries.Single(e => e.id == Id).title = "沙丘之下";
        journal.ResetAllEntries(); journal.ApplySaveState(save);
        Check(journal.ContainsEntry(Id) && journal.TrackedEntryId == Id && !journal.IsEntryCompleted(Id), "active task and tracking survive save JSON");
        Check(journal.TryGetEntry(Id, out entry) && entry.Title == "香料与獠牙", "legacy active save adopts renamed title");
        Check(journal.CaptureSaveState().entries.Single(e => e.id == Id).title == "香料与獠牙", "subsequent save stores renamed title");
        CheckHudObjective(journal, false);

        var map = new GameObject("Quest map", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
        map.RebuildFromSnapshot(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Desert"));
        Check(map.TryGetQuestTarget(Id, out var cached) && cached == new Vector2(0,32.1f), "unloaded desert menu map uses center target");
        var targetCheck = typeof(TabJournalMenuController).GetMethod("SceneHasQuestTarget", F);
        Check((bool)targetCheck.Invoke(null, new object[] { "Level2-Desert", floor, Id }), "desert region selection shows quest marker");
        Check(!RuntimeMiniMapGraphic.TryGetSceneQuestTarget(floor, Id, out _), "Floor1 notice is not mistaken for objective position");
        var desert = SceneManager.CreateScene("Level2-Desert"); SceneManager.SetActiveScene(desert);
        var target = new GameObject(QuestJournalManager.Level2SandwormTargetName); target.transform.position = cached;
        map.RebuildFromScene(desert);
        Check(map.TryGetQuestTarget(Id, out var live) && live == cached, "live minimap target agrees with unloaded map");
        target.SetActive(false); map.RebuildFromScene(desert);
        Check(map.TryGetQuestTarget(Id, out live) && live == cached, "streamed-out target remains discoverable");
        var menuRoot = new GameObject("Quest map menu"); menuRoot.SetActive(false);
        var menu = menuRoot.AddComponent<TabJournalMenuController>(); Set(menu, "questJournal", journal);
        Call(menu, "ApplyTrackedQuestTargetToMap", "Level2-Desert", map, false);
        Check(Get<bool>(map, "showTrackedQuestTarget") && Get<Vector2>(map, "trackedQuestTargetPosition") == cached, "menu map displays tracked hunt");

        var ordinary = Object.Instantiate(Resources.Load<TrialProofPickupItem>("PickupItems/TrialProofPickupItem"));
        Check(ordinary.TryPickUp() && !journal.IsEntryCompleted(Id), "ordinary proof does not complete sandworm hunt"); CloseItem();
        yield return null;
        var counterfeit = Object.Instantiate(Resources.Load<CounterfeitTrialProofPickupItem>("PickupItems/CounterfeitTrialProofPickupItem"));
        Check(counterfeit.TryPickUp() && !journal.IsEntryCompleted(Id), "counterfeit proof does not complete hunt"); CloseItem();
        yield return null;
        var worm = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Sandworm-Boss.prefab")).GetComponent<SandwormZeldaCharacterData>();
        worm.TakeDamage(999);
        var proof = Object.FindObjectsOfType<TrialProofPickupItem>().Single(); string proofId = proof.UniqueInstanceId;
        Check(!journal.IsEntryCompleted(Id), "killing alone waits for proof pickup");
        Check(proofId.StartsWith(TrialProofPickupItem.SandwormRewardIdentityPrefix), "boss reward retains quest provenance");
        Check(proof.TryPickUp() && journal.IsEntryCompleted(Id), "collecting actual boss reward completes quest"); CloseItem();
        CheckHudObjective(journal, true);
        Check(journal.TrackedEntryId == QuestJournalManager.Level2HeroProofId, "completion returns tracking to main objective");
        Call(menu, "ApplyTrackedQuestTargetToMap", "Level2-Desert", map, false);
        Check(!Get<bool>(map, "showTrackedQuestTarget"), "completed hunt marker removed");
        save = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        save.entries.Single(e => e.id == Id).title = "沙丘之下";
        journal.ResetAllEntries(); journal.ApplySaveState(save);
        Check(journal.IsEntryCompleted(Id), "completion survives save restore");
        Check(journal.TryGetEntry(Id, out entry) && entry.Title == "香料与獠牙", "legacy completed save adopts renamed title without resetting progress");
        CheckHudObjective(journal, true);
        yield return null; yield return null;
        SceneManager.SetActiveScene(floor); Set(notice, "journalEntryWasAdded", false);
        Call(notice, "OpenDocument"); Close(notice);
        Check(journal.IsEntryCompleted(Id) && journal.Entries.Count(e => e.Id == Id) == 1, "reloaded notice cannot restart completed task");

        journal.ResetAllEntries(); inventory.ClearAll();
        var legacy = Object.Instantiate(Resources.Load<TrialProofPickupItem>("PickupItems/TrialProofPickupItem"));
        legacy.gameObject.AddComponent<SceneTravelStableId>().Initialize(TrialProofPickupItem.SandwormRewardIdentityPrefix + Guid.NewGuid().ToString("N"));
        Check(legacy.TryPickUp() && !journal.ContainsEntry(Id), "old-format reward before notice stores hidden evidence"); CloseItem();
        save = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        journal.ResetAllEntries(); journal.ApplySaveState(save);
        yield return null; yield return null;
        Set(notice, "journalEntryWasAdded", false); Call(notice, "OpenDocument"); Close(notice);
        Check(journal.ContainsEntry(Id) && journal.IsEntryCompleted(Id), "pre-notice completion evidence survives save and completes on reading");
    }
}
