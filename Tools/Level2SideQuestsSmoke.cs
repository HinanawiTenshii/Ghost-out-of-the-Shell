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
public static class Level2SideQuestsSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const string Summon = QuestJournalManager.SummoningQuestId;
    const string Forgery = QuestJournalManager.CounterfeitQuestId;
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static Level2SideQuestsSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; Debug.Log("SIDE_QUEST_CHECK " + label); }
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, F).Invoke(o, args);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, F).GetValue(o);
    static void Set(object o, string n, object value) => o.GetType().GetField(n, F).SetValue(o, value);
    static QuestJournalManager.SaveState Save(QuestJournalManager j) => JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(j.CaptureSaveState()));

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("isolated tests only");
        PlayerSettings.productName = "level2-side-quests-smoke";
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        foreach (var floor in new[] { "Level2-Floor1", "Level2-Floor2" })
        {
            string path = "Assets/Scenes/Level2/" + floor + ".unity";
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            if (floor == "Level2-Floor1")
            {
                var notice = Object.FindObjectsOfType<DocumentReader>(true).Single(d => d.gameObject.scene == scene && d.name == "召唤需知");
                Check(Get<bool>(notice, "addJournalEntryOnOpen") && Get<string>(notice, "journalEntryId") == Summon, "authored notice starts summoning task");
                Check(Get<string>(notice, "journalEntryTitle") == "六把钥匙", "authored notice uses final quest title");
                Check(new[] { "大主教", "金色服装", "守卫士官", "大厨", "大工程师", "诡异商人", "隐居剑客" }
                    .All(clue => notice.DocumentText.Contains(clue)), "all six original notice clues preserved");
                Check(notice.DocumentText.Contains("六把钥匙") && notice.DocumentText.Contains("矩形范围") &&
                    notice.DocumentText.Contains("在范围外杀死他们，仪式不会接纳"), "polished notice explains keys and exact offering rules");
                var craftsman = Object.FindObjectsOfType<DialogueQuestProgression>(true).Single(q => q.gameObject.scene == scene && q.QuestId == Forgery);
                Check(Get<string>(craftsman, "questTitle") == "假作真时", "craftsman dialogue uses final quest title");
                var circle = Object.FindObjectsOfType<SummoningCircle>(true).Single(c => c.gameObject.scene == scene && c.JournalQuestId == Summon);
                Check(circle.BoundCharacters.Count == 6 && circle.BoundCharacters.All(i => i != null) && circle.SummonPrefab != null, "all six original offerings and summon prefab preserved");
                Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(scene, QuestJournalManager.SummoningCircleTarget, out var position) &&
                    Vector2.Distance(position, circle.transform.position) < .01f, "live summoning marker points to exact seal");
                var copy = Object.Instantiate(notice.gameObject); copy.name = "召唤需知";
                SceneManager.MoveGameObjectToScene(copy, fixture); copy.transform.position = Vector3.zero;
                copy = Object.Instantiate(circle.gameObject); copy.name = "召唤法阵";
                SceneManager.MoveGameObjectToScene(copy, fixture); copy.transform.position = Vector3.right * 5;
                Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(scene, QuestJournalManager.CounterfeitCraftsmanTarget, out position) &&
                    Vector2.Distance(position, new Vector2(-80.41f,-33.49f)) < .01f, "live craftsman marker");
            }
            else
                Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(scene, QuestJournalManager.CounterfeitMerchantTarget, out var position) &&
                    Vector2.Distance(position, new Vector2(96.32f,39.73f)) < .01f, "live gem merchant marker");
            typeof(RuntimeMiniMapSceneDataGenerator).GetMethod("GenerateSceneSnapshot", F).Invoke(null,
                new object[] { path, "Assets/Resources/MiniMaps/" + floor + ".asset", floor });
            AssetDatabase.SaveAssetIfDirty(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/" + floor));
            EditorSceneManager.CloseScene(scene, true);
        }
        SceneManager.SetActiveScene(fixture);
        SessionState.SetInt("sideQuestChecks", checks); SessionState.SetBool(nameof(Level2SideQuestsSmoke), true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(Level2SideQuestsSmoke), false))
            { SessionState.SetBool(nameof(Level2SideQuestsSmoke), false); checks = SessionState.GetInt("sideQuestChecks", 0); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 90; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("side quest test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("LEVEL2_SIDE_QUESTS_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Hud(string quest, QuestJournalManager journal, out List<string> text, out List<bool> done)
    {
        text = new List<string>(); done = new List<bool>();
        typeof(ZeldaHealthHeartsUI).GetMethod("BuildQuestObjectiveState", F).Invoke(null, new object[] { quest, journal, text, done });
    }

    static void VerifyLegacyTitles(QuestJournalManager journal)
    {
        foreach (bool completed in new[] { false, true })
        {
            var legacy = new QuestJournalManager.SaveState { tracked = Summon, summoningLitCrystals = completed ? 6 : 3,
                summoningPerformed = completed, completed = new List<string>() };
            legacy.entries.Add(new QuestJournalManager.SavedEntry { id = Forgery, title = "真假试炼", details = QuestJournalManager.CounterfeitPriceKnown });
            legacy.entries.Add(new QuestJournalManager.SavedEntry { id = Summon, title = "六魂之约", details = "legacy ritual", scene = "Level2-Floor1" });
            legacy.entries.Add(new QuestJournalManager.SavedEntry { id = "unrelated", title = "无关任务", details = "keep" });
            if (completed) { legacy.completed.Add(Forgery); legacy.completed.Add(Summon); }
            journal.ApplySaveState(legacy);
            Check(journal.TryGetEntry(Forgery, out var forged) && forged.Title == "假作真时" &&
                journal.TryGetEntry(Summon, out var ritual) && ritual.Title == "六把钥匙", "old save titles migrate, completed=" + completed);
            Check(journal.IsEntryCompleted(Forgery) == completed && journal.IsEntryCompleted(Summon) == completed &&
                journal.SummoningLitCrystals == legacy.summoningLitCrystals && journal.EntryDetailsContain(Forgery, QuestJournalManager.CounterfeitPriceKnown),
                "renaming preserves completion, crystal progress and price stage, completed=" + completed);
            if (!completed) Check(journal.TrackedEntryId == Summon && QuestJournalManager.ResolveQuestMapTargetId(Forgery) == QuestJournalManager.CounterfeitMerchantTarget,
                "renaming preserves tracking and merchant marker");
            var saved = Save(journal);
            Check(saved.entries.Single(e => e.id == Forgery).title == "假作真时" && saved.entries.Single(e => e.id == Summon).title == "六把钥匙" &&
                saved.entries.Single(e => e.id == "unrelated").title == "无关任务", "resave uses final titles without renaming other tasks");
        }
        journal.ResetAllEntries();
        journal.AddOrUpdateEntry(Forgery, "真假试炼", "old scene snapshot");
        Check(journal.TryGetEntry(Forgery, out var restored) && restored.Title == "假作真时", "old scene title cannot overwrite final name");
        journal.ResetAllEntries();
    }

    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked || GameSaveSystem.IsPreparingSceneSave) yield return null;
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        VerifyLegacyTitles(journal);
        var notice = Object.FindObjectsOfType<DocumentReader>().Single(n => n.name == "召唤需知");
        var circle = Object.FindObjectsOfType<SummoningCircle>().Single(c => c.JournalQuestId == Summon);
        var body = new GameObject("Reader").AddComponent<SwordZeldaCharacterData>();
        var player = body.gameObject.AddComponent<ZeldaFourWayMover>(); player.ReceiveControl(Vector2.up); ZeldaRuntimeRegistry.ClaimControlledMover(player);
        player.transform.position = notice.transform.position;
        var inventoryObject = new GameObject("test inventory"); inventoryObject.SetActive(false);
        var inventory = inventoryObject.AddComponent<PersistentInventory>(); Set(inventory,"saveToPlayerPrefs",false);
        inventoryObject.SetActive(true); inventory.enabled = false; inventory.ClearAll();
        yield return null;
        Check(!journal.ContainsEntry(Summon), "entering level does not accept summoning task");
        circle.ApplyRuntimeState(new SummoningCircle.RuntimeState { litMask = 3 });
        Check(journal.SummoningLitCrystals == 2 && !journal.ContainsEntry(Summon), "pre-notice physical progress retained without auto-acceptance");
        Call(notice, "OpenDocument");
        Check(DocumentReader.IsDocumentOpen && journal.ContainsEntry(Summon), "actually reading notice starts quest");
        Check(journal.TryGetEntry(Summon, out var entry) && entry.Title == "六把钥匙" && journal.TrackedEntryId == Summon, "new task named and tracked");
        Check(entry.Details.Contains("2/6") && entry.Details.Contains("矩形范围") && entry.Details.Contains("隐居剑客"), "journal has progress and six offering clues");
        Canvas.ForceUpdateCanvases();
        var readingText = Get<UnityEngine.UI.Text>(notice, "contentText");
        Check(readingText.text == notice.DocumentText && readingText.preferredHeight <= readingText.rectTransform.rect.height + .1f,
            "full notice fits existing reading window: " + readingText.preferredHeight + "/" + readingText.rectTransform.rect.height);
        Call(notice, "CloseDocument"); yield return null;
        Hud(Summon,journal,out var texts,out var done);
        Check(texts.Count == 2 && texts[0].Contains("2/6") && !done[0] && !done[1], "summoning HUD reflects progress");
        var savedJournal = Save(journal); var savedCircle = circle.CaptureRuntimeState();
        Object.Destroy(circle.gameObject); yield return null;
        journal.ResetAllEntries(); journal.ApplySaveState(savedJournal);
        Check(journal.SummoningLitCrystals == 2 && !journal.IsEntryCompleted(Summon), "partial ritual journal survives unloaded seal and JSON restore");
        var map = new GameObject("map", typeof(RectTransform)).AddComponent<RuntimeMiniMapGraphic>();
        map.RebuildFromSnapshot(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor1"));
        Check(map.TryGetQuestTarget(Summon, out var point) && Vector2.Distance(point, new Vector2(33.104588f,6.43f)) < .01f, "unloaded scene summoning marker works");

        // Recreate a configured seal and complete the real summon using restored physical state.
        var sealObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/SummoningCircle.prefab"));
        circle = sealObject.GetComponent<SummoningCircle>(); Set(circle,"journalQuestId",Summon);
        circle.SummonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Level2NPCS/Idle/OuterRealmVisitor-level2-Idle.prefab")?.GetComponent<ZeldaCharacterData>();
        if (circle.SummonPrefab == null)
            circle.SummonPrefab = AssetDatabase.FindAssets("t:Prefab OuterRealmVisitor").Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<GameObject>).Select(g => g.GetComponent<ZeldaCharacterData>()).First(d => d != null);
        circle.ApplyRuntimeState(savedCircle);
        Check(journal.SummoningLitCrystals == 2, "seal restoration matches journal");
        circle.ApplyRuntimeState(new SummoningCircle.RuntimeState { litMask = 31 });
        Check(journal.SummoningLitCrystals == 5 && !journal.IsEntryCompleted(Summon), "five crystals not prematurely completed");
        circle.ApplyRuntimeState(new SummoningCircle.RuntimeState { litMask = 63 });
        Check(!journal.IsEntryCompleted(Summon), "six crystals alone wait for actual summon");
        yield return null; yield return null;
        Check(circle.HasSummoned && journal.IsEntryCompleted(Summon), "actual summon completes task");
        Hud(Summon,journal,out texts,out done); Check(done.All(v => v), "completed ritual HUD checks both objectives");
        Check(QuestJournalManager.ResolveQuestMapTargetId(Summon) == null, "completed ritual removes marker");
        savedJournal = Save(journal); journal.ResetAllEntries(); journal.ApplySaveState(savedJournal);
        Check(journal.IsEntryCompleted(Summon) && journal.SummoningLitCrystals == 6, "completed ritual JSON save persists");
        while (DocumentReader.IsInputBlocked) yield return null;
        Call(notice,"OpenDocument"); Call(notice,"CloseDocument");
        Check(journal.IsEntryCompleted(Summon) && journal.Entries.Count(e => e.Id == Summon) == 1, "rereading does not duplicate or reset task");

        journal.ResetAllEntries(); journal.AddOrUpdateEntry(Forgery,QuestJournalManager.CounterfeitQuestTitle,"find gem","Level2-Floor1");
        map.RebuildFromSnapshot(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor2"));
        Check(map.TryGetQuestTarget(Forgery,out point) && Vector2.Distance(point,new Vector2(96.32f,39.73f)) < .01f, "forgery points to merchant across floors");
        journal.AppendEntryDetails(Forgery,QuestJournalManager.CounterfeitPriceKnown);
        inventory.SetSlot(0,"gold_coin",20); inventory.SetSlot(1,"gold_coin",10);
        Check(map.TryGetQuestTarget(Forgery,out point) && Vector2.Distance(point,new Vector2(96.32f,39.73f)) < .01f, "30 coin stage remains fixed at merchant even with enough money");
        Hud(Forgery,journal,out texts,out done); Check(texts.Any(t => t.Contains("30金币")) && done[0] && !done[1], "HUD shows buy gem objective after price learned");
        inventory.ClearAll(); var gem = Object.Instantiate(Resources.Load<GoldenGemPickupItem>("PickupItems/GoldenGemPickupItem"));
        Check(gem.TryPickUp(), "world gem pickup succeeds"); ItemDescriptionWindow.GetOrCreate().CloseWindow();
        Check(QuestJournalManager.ResolveQuestMapTargetId(Forgery) == QuestJournalManager.CounterfeitCraftsmanTarget, "alternate gem pickup advances marker");
        map.RebuildFromSnapshot(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor1"));
        Check(map.TryGetQuestTarget(Forgery,out point) && Vector2.Distance(point,new Vector2(-80.41f,-33.49f)) < .01f, "return marker uses cached craftsman position");
        var legacy = new QuestJournalManager.SaveState(); legacy.entries.Add(new QuestJournalManager.SavedEntry { id=Forgery,title="真假试炼",details="old progress" });
        journal.ApplySaveState(legacy);
        Check(QuestJournalManager.ResolveQuestMapTargetId(Forgery) == QuestJournalManager.CounterfeitCraftsmanTarget, "old save with held gem migrates return marker");
        journal.CompleteEntry(Forgery); Check(!map.TryGetQuestTarget(Forgery,out _), "completed forgery marker hidden");
        journal.ResetAllEntries(); Check(journal.SummoningLitCrystals == 0 && !journal.ContainsEntry(Summon), "fresh game clears ritual task and evidence");
    }
}
