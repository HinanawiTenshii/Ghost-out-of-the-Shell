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

// Actual authored document/injectors, inventory transaction, both map consumers,
// hierarchy, disk saves and scene travel. Only run in the disposable test copy.
[InitializeOnLoad]
public static class ArenaInjectionQuestSmoke
{
    const string Key = nameof(ArenaInjectionQuestSmoke);
    const string ScenePath = "Assets/Scenes/Level2/Level2-Floor-1.unity";
    const string Quest = QuestJournalManager.ArenaInjectionQuestId;
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int checks, frame = -1;
    static double deadline;
    static ArenaInjectionQuestSmoke() { EditorApplication.update += Tick; }
    static QuestJournalManager J => QuestJournalManager.GetOrCreate();
    static ZeldaFourWayMover P => ZeldaRuntimeRegistry.GetControlledMover();
    static DocumentReader Doc => Object.FindObjectsOfType<DocumentReader>(true).Single(d => d.name == "注液器说明");
    static LiquidInjector[] Injectors => Object.FindObjectsOfType<LiquidInjector>(true).OrderBy(i => i.transform.position.x).ToArray();
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, F).GetValue(o);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, F).Invoke(o, args);
    static void Check(bool ok, string label)
    { checks++; if (!ok) throw new Exception("INJECTION QUEST: " + label); Debug.Log("INJECTION_QUEST_CHECK " + label); }

    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("Isolated copy required");
        PlayerSettings.productName = "arena-injection-quest-smoke";
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true),
            new EditorBuildSettingsScene("Assets/Scenes/Level2/Level2-Floor1.unity", true) };
        EditorSceneManager.OpenScene(ScenePath);
        Check(Get<bool>(Doc, "addJournalEntryOnOpen") && Get<string>(Doc, "journalEntryId") == Quest &&
            Get<string>(Doc, "journalParentEntryId") == QuestJournalManager.ArenaQuestId, "authored reading starts correct child quest");
        Check(Get<string>(Doc, "journalEntryTitle") == "为你的健康干杯" &&
            QuestJournalManager.ArenaInjectionQuestTitle == "为你的健康干杯", "authored and canonical titles use final name");
        Check(Doc.DocumentText.Contains("注液器使用说明") && !Doc.DocumentText.Contains("在此输入"), "authored instructions populated");
        Check(Injectors.Length == 2 && Injectors.All(i => i.PotionInjectionQuestId == Quest), "both authored injectors configured");
        Check(Injectors.All(i => Get<TemporaryCharacterEffectArea[]>(i, "linkedAreas").Length == 2), "existing pool and stream bindings retained");
        foreach (var i in Injectors) Debug.Log("INJECTION_POSITION " + i.name + " " + i.transform.position.ToString("R"));
        typeof(RuntimeMiniMapSceneDataGenerator).GetMethod("GenerateSceneSnapshot", F).Invoke(null,
            new object[] { ScenePath, "Assets/Resources/MiniMaps/Level2-Floor-1.asset", "Level2-Floor-1" });
        AssetDatabase.SaveAssetIfDirty(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor-1"));
        SessionState.SetInt(Key + "checks", checks); SessionState.SetBool(Key, true);
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
                SessionState.SetBool(Key, false); checks = SessionState.GetInt(Key + "checks", 0);
                deadline = EditorApplication.timeSinceStartup + 360; steps.Push(Test());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timed out");
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("ARENA_INJECTION_QUEST_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Ready()
    {
        int stable = 0;
        while (stable < 3)
        {
            bool ready = !DocumentReader.IsInputBlocked && !CharacterPotionBuffs.IsRestoringWorld && !GameSaveSystem.IsPreparingSceneSave;
            stable = ready ? stable + 1 : 0; yield return null;
        }
    }
    static void QuietScene()
    {
        foreach (var s in Object.FindObjectsOfType<CameraVisionObjectStreaming>(true)) s.enabled = false;
        foreach (var ai in Object.FindObjectsOfType<ZeldaCharacterAiBase>(true)) ai.enabled = false;
    }
    static void Move(Vector3 p)
    {
        P.EndItemAttack(); P.transform.position = p;
        var rb = P.GetComponent<Rigidbody2D>(); if (rb != null) rb.position = p;
        Physics2D.SyncTransforms();
    }
    static IEnumerator Read()
    {
        Doc.gameObject.SetActive(true); Move(Doc.transform.position);
        Call(Doc, "OpenDocument"); Check(DocumentReader.IsDocumentOpen, "actual reading window opens");
        Call(Doc, "CloseDocument"); yield return Ready();
    }
    static void Parent()
    {
        J.RecordArenaQuestProgress(new ArenaChallengeController.RuntimeState {
            phase = ArenaChallengeController.Phase.Preparing, roster = new[] { 0, 1, 2 } });
    }
    static void Potion(LiquidInjector i, AlchemyPotionKind kind)
    {
        Move(i.transform.position + Vector3.down * .9f); Set(i, "nextInteractionTime", 0f);
        var bag = PersistentInventory.Instance; bag.ClearAll();
        bag.SetSlot(0, AlchemyRecipeCatalog.GetItemId(kind), 1); bag.SelectSlot(0);
    }
    static void CheckMaps(int count)
    {
        var positions = new List<Vector2>();
        var expected = Injectors.Select(i => (Vector2)i.transform.position).ToArray();
        var snapshot = Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor-1");
        Check(snapshot.QuestTargets.Count(t => t.questId == Quest) == 2, "snapshot retains both alternatives independently of progress");
        var hud = Object.FindObjectOfType<ZeldaHealthHeartsUI>();
        var mini = Get<RuntimeMiniMapGraphic>(hud, "taskMiniMapGraphic"); mini.RebuildFromSnapshot(snapshot);
        Call(hud, "ApplyTrackedQuestTarget", SceneManager.GetActiveScene(), (Vector2)P.transform.position);
        var menu = Object.FindObjectOfType<TabJournalMenuController>();
        var map = new GameObject("menu test map", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
        map.rectTransform.sizeDelta = new Vector2(600, 500); map.RebuildFromSnapshot(snapshot);
        Call(menu, "ApplyTrackedQuestTargetToMap", "Level2-Floor-1", map, false);
        foreach (var graphic in new[] { mini, map })
        {
            graphic.GetQuestTargets(Quest, positions);
            Check(positions.Count == count && (count == 0 || positions.SequenceEqual(expected)), "map returns exact injector positions");
            if (count == 0) continue; // completion auto-tracks parent instead
            Check(Get<bool>(graphic, "showTrackedQuestTarget") && Get<List<Vector2>>(graphic, "additionalTrackedQuestTargets").Count == 1,
                "map UI consumer displays both target markers");
            int vertices;
            using (var vh = new VertexHelper()) { Call(graphic, "PopulateMarkers", vh); vertices = vh.currentVertCount; }
            graphic.SetTrackedQuestTarget(true, positions[0], true);
            using (var vh = new VertexHelper())
            { Call(graphic, "PopulateMarkers", vh); Check(vertices > vh.currentVertCount, "second destination adds actual marker geometry"); }
            graphic.SetTrackedQuestTargets(positions, true);
        }
        if (count > 0)
        {
            var camera = Camera.main; camera.transform.position = new Vector3((expected[0].x + expected[1].x) / 2,
                expected[0].y, -10); camera.orthographicSize = 30;
            Call(hud, "UpdateTrackedQuestWorldMarker", true, expected[0]);
            var extra = Get<List<GameObject>>(hud, "additionalQuestWorldMarkers");
            Check(Get<GameObject>(hud, "trackedQuestWorldMarker").activeSelf && extra.Count == 1 && extra[0].activeSelf &&
                (Vector2)extra[0].transform.position == expected[1], "both world markers visible at corresponding injectors");
            Check(extra[0].GetComponentsInChildren<SpriteRenderer>().All(r => r.sortingOrder < 30000), "extra world marker remains below visibility mask");
        }
        Object.Destroy(map.gameObject);
    }
    static void VerifyTitleMigration()
    {
        foreach (bool completed in new[] { false, true })
        {
            J.ResetAllEntries(); Parent();
            J.AddOrUpdateEntry(Quest, "为你的健康干杯", "legacy injection details", "Level2-Floor-1");
            if (completed) J.CompleteEntry(Quest);
            J.SetEntryCollapsed(QuestJournalManager.ArenaQuestId, true);
            var old = J.CaptureSaveState();
            old.entries.Single(e => e.id == Quest).title = "池中玄机";
            J.ResetAllEntries();
            J.ApplySaveState(JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(old)));
            string label = completed ? "completed" : "pending";
            Check(J.TryGetEntry(Quest, out var entry) && entry.Title == "为你的健康干杯" &&
                entry.Details == "legacy injection details" && entry.SourceScene == "Level2-Floor-1",
                label + " old save title migrates without changing details or scene");
            Check(J.IsEntryCompleted(Quest) == completed && J.TrackedEntryId == old.tracked &&
                J.GetParentEntryId(Quest) == QuestJournalManager.ArenaQuestId && J.IsEntryCollapsed(QuestJournalManager.ArenaQuestId),
                label + " rename preserves progress, tracking, parent and foldout");
            var saved = J.CaptureSaveState();
            Check(saved.entries.Count(e => e.id == Quest) == 1 && saved.entries.Single(e => e.id == Quest).title == "为你的健康干杯",
                label + " resave writes final title without duplicate quest");
            J.ResetAllEntries();
            J.ApplySaveState(JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(saved)));
            Check(J.TryGetEntry(Quest, out entry) && entry.Title == "为你的健康干杯" && J.IsEntryCompleted(Quest) == completed,
                label + " final title and completion survive second load");
        }
        J.ResetAllEntries();
    }
    static IEnumerator VerifyEarlyInjection()
    {
        // Other child quests retain the original strict parent gate.
        J.ConfigureParent("early.other.child", "early.other.parent");
        Check(!J.RecordQuestCompletion("early.other.child") && !J.CaptureSaveState().pendingCompleted.Contains("early.other.child"),
            "default completion API still rejects evidence before parent");
        J.ResetAllEntries();
        var left = Injectors[0];
        var bag = PersistentInventory.Instance;
        Move(left.transform.position + Vector3.down); Set(left, "nextInteractionTime", 0f); bag.ClearAll();
        Check(!left.TryInject() && !J.CaptureSaveState().pendingCompleted.Contains(Quest), "empty early attempt records nothing");
        bag.SetSlot(0, AlchemyRecipeCatalog.RedHerbId, 1); bag.SelectSlot(0);
        Check(!left.TryInject() && !J.CaptureSaveState().pendingCompleted.Contains(Quest), "non-potion early attempt records nothing");
        left.SetConditionSatisfied(true);
        Check(left.TryInject() && !J.CaptureSaveState().pendingCompleted.Contains(Quest), "preset early injection records nothing");
        left.SetConditionSatisfied(false);
        var linked = Get<TemporaryCharacterEffectArea[]>(left, "linkedAreas");
        Potion(left, AlchemyPotionKind.Veil); Set(left, "linkedAreas", new TemporaryCharacterEffectArea[0]);
        Check(!left.TryInject() && !J.CaptureSaveState().pendingCompleted.Contains(Quest), "failed early injection records nothing");
        Set(left, "linkedAreas", linked);
        left.ApplyRuntimeState(left.CaptureRuntimeState());
        Check(!J.CaptureSaveState().pendingCompleted.Contains(Quest), "restoring injector state does not invent early evidence");

        Potion(left, AlchemyPotionKind.Veil);
        bool atomic = false;
        int notifications = 0;
        Action inventoryCallback = () => atomic = J.CaptureSaveState().pendingCompleted.Contains(Quest) &&
            !J.ContainsEntry(Quest) && bag.GetQuantity(GlassBottlePickupItem.DefaultItemId) == 1;
        Action<QuestJournalChangeKind> questCallback = _ => notifications++;
        bag.InventoryChanged += inventoryCallback; J.JournalEntriesChanged += questCallback;
        bool success = left.TryInject();
        bag.InventoryChanged -= inventoryCallback; J.JournalEntriesChanged -= questCallback;
        Check(success && atomic && notifications == 0, "early injection atomically stores silent evidence with returned bottle");
        Check(!J.ContainsEntry(Quest) && !J.ContainsEntry(QuestJournalManager.ArenaQuestId) && !J.IsEntryCompleted(Quest),
            "early success never accepts parent or child");
        yield return Read();
        Check(!J.ContainsEntry(Quest) && J.CaptureSaveState().pendingCompleted.Contains(Quest), "reading before parent cannot consume hidden evidence");

        // Replacing the pool afterwards must not erase the historical successful action.
        bag.ClearAll(); Move(left.transform.position + Vector3.down); Set(left, "nextInteractionTime", 0f);
        left.SetConditionSatisfied(true);
        Check(left.TryInject() && J.CaptureSaveState().pendingCompleted.Contains(Quest), "later preset replacement retains earlier potion evidence");
        left.SetConditionSatisfied(false);
        Move(Doc.transform.position); yield return Ready();
        Check(GameSaveSystem.Instance.Save(1, false), "save early evidence before parent and child acceptance");
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level2-Floor1", Vector2.zero);
        yield return Ready(); QuietScene();
        Check(J.CaptureSaveState().pendingCompleted.Contains(Quest) && !J.ContainsEntry(Quest), "hidden evidence survives leaving injector scene");
        Check(GameSaveSystem.Instance.Load(1), "load early evidence from disk");
        yield return Ready(); QuietScene();
        Check(SceneManager.GetActiveScene().name == "Level2-Floor-1" && J.CaptureSaveState().pendingCompleted.Contains(Quest) &&
            !J.ContainsEntry(Quest), "disk load restores basement and unaccepted early evidence");
        Parent();
        Check(!J.ContainsEntry(Quest) && J.CaptureSaveState().pendingCompleted.Contains(Quest), "accepting parent alone still does not reveal child");
        int bottles = PersistentInventory.Instance.GetQuantity(GlassBottlePickupItem.DefaultItemId);
        yield return Read();
        Check(J.IsEntryCompleted(Quest) && !J.IsEntryCompleted(QuestJournalManager.ArenaQuestId) &&
            !J.CaptureSaveState().pendingCompleted.Contains(Quest) && J.TrackedEntryId == QuestJournalManager.ArenaQuestId,
            "read after load immediately completes only child and returns tracking to parent");
        Check(PersistentInventory.Instance.GetQuantity(GlassBottlePickupItem.DefaultItemId) == bottles,
            "retroactive completion never consumes another potion or grants another bottle");
        CheckMaps(0);

        // The other injector also counts when the parent is already accepted.
        J.ResetAllEntries(); Parent(); Set(Doc, "journalEntryWasAdded", false);
        Potion(Injectors[1], AlchemyPotionKind.Berserk);
        Check(Injectors[1].TryInject() && !J.ContainsEntry(Quest) && J.CaptureSaveState().pendingCompleted.Contains(Quest),
            "right injector remembers success after parent but before reading");
        var early = JsonUtility.ToJson(J.CaptureSaveState()); J.ResetAllEntries();
        J.ApplySaveState(JsonUtility.FromJson<QuestJournalManager.SaveState>(early));
        yield return Read();
        Check(J.IsEntryCompleted(Quest), "right injector evidence completes on reading after journal restore");
        int completions = 0;
        Action<QuestJournalChangeKind> onComplete = kind => { if (kind == QuestJournalChangeKind.Completed) completions++; };
        J.JournalEntriesChanged += onComplete;
        yield return Read();
        Potion(Injectors[1], AlchemyPotionKind.Might); Check(Injectors[1].TryInject(), "completed task does not prevent later injection");
        J.JournalEntriesChanged -= onComplete;
        Check(completions == 0, "rereading and reinjecting never repeat completion notification");
        J.ResetAllEntries(); Set(Doc, "journalEntryWasAdded", false);
        Check(!J.CaptureSaveState().pendingCompleted.Contains(Quest), "fresh quest reset clears early evidence");
    }
    static IEnumerator Test()
    {
        yield return Ready(); QuietScene(); J.ResetAllEntries();
        VerifyTitleMigration();
        var arena = Object.FindObjectOfType<ArenaChallengeController>();
        var actor = arena.residents.First(ai => ai.gameObject != arena.registrar.gameObject && !arena.guardians.Any(g => g.gameObject == ai.gameObject));
        CameraVisionObjectStreaming.RestoreForGameplay(actor.gameObject); actor.gameObject.SetActive(true);
        if (P != null) P.enabled = false;
        var mover = actor.GetComponent<ZeldaFourWayMover>(); mover.ReceiveControl(Vector2.right); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        yield return VerifyEarlyInjection();
        yield return Read(); Check(!J.ContainsEntry(Quest), "reading before parent does not accept child");
        Parent();
        Check(!J.ContainsEntry(Quest), "parent acceptance alone does not start child");
        yield return Read();
        Check(J.ContainsEntry(Quest) && !J.IsEntryCompleted(Quest) && J.TrackedEntryId == Quest, "without early evidence rereading after parent accepts and tracks pending child");
        Check(J.TryGetEntry(Quest, out var accepted) && accepted.Title == "为你的健康干杯", "newly accepted quest displays final title");
        var rows = new List<QuestJournalManager.HierarchyRow>(); J.GetVisibleHierarchy(rows);
        Check(rows.Single(r => r.entry.Id == Quest).depth == 1 && rows.Single(r => r.entry.Id == QuestJournalManager.ArenaQuestId).hasChildren,
            "child nests under arena with foldout");
        J.SetEntryCollapsed(QuestJournalManager.ArenaQuestId, true); J.GetVisibleHierarchy(rows);
        Check(rows.All(r => r.entry.Id != Quest), "parent foldout hides child"); J.SetEntryCollapsed(QuestJournalManager.ArenaQuestId, false);
        CheckMaps(2);
        var texts = new List<string>(); var done = new List<bool>();
        typeof(ZeldaHealthHeartsUI).GetMethod("BuildQuestObjectiveState", F).Invoke(null, new object[] { Quest, J, texts, done });
        Check(texts.SequenceEqual(new[] { QuestJournalManager.ArenaInjectionObjective }) && !done[0], "HUD has injection objective");
        var pending = JsonUtility.ToJson(J.CaptureSaveState());
        var left = Injectors[0]; var bag = PersistentInventory.Instance;
        Move(left.transform.position + Vector3.down); Set(left, "nextInteractionTime", 0f); bag.ClearAll();
        Check(!left.TryInject() && !J.IsEntryCompleted(Quest), "empty selection cannot complete");
        bag.SetSlot(0, AlchemyRecipeCatalog.RedHerbId, 1); bag.SelectSlot(0);
        Check(!left.TryInject() && !J.IsEntryCompleted(Quest), "non-potion cannot complete");
        left.SetConditionSatisfied(true); Check(left.TryInject() && !J.IsEntryCompleted(Quest), "configured preset injection does not complete potion quest");
        left.SetConditionSatisfied(false);
        var linked = Get<TemporaryCharacterEffectArea[]>(left, "linkedAreas");
        Potion(left, AlchemyPotionKind.Might); Set(left, "linkedAreas", new TemporaryCharacterEffectArea[0]);
        Check(!left.TryInject() && !J.IsEntryCompleted(Quest) && bag.GetQuantity(AlchemyRecipeCatalog.GetItemId(AlchemyPotionKind.Might)) == 1,
            "invalid target keeps potion and task pending"); Set(left, "linkedAreas", linked);
        int n = 0;
        foreach (AlchemyPotionKind kind in Enum.GetValues(typeof(AlchemyPotionKind)))
        {
            J.ApplySaveState(JsonUtility.FromJson<QuestJournalManager.SaveState>(pending));
            var i = Injectors[n++ % 2]; Potion(i, kind);
            bool atomic = false;
            Action callback = () => { atomic = J.IsEntryCompleted(Quest) && bag.GetQuantity(GlassBottlePickupItem.DefaultItemId) == 1; };
            bag.InventoryChanged += callback;
            bool injected = i.TryInject(); bag.InventoryChanged -= callback;
            Check(injected && atomic && J.IsEntryCompleted(Quest) && !J.IsEntryCompleted(QuestJournalManager.ArenaQuestId),
                "successful " + kind + " on " + i.name + " completes only child atomically");
            Check(J.TrackedEntryId == QuestJournalManager.ArenaQuestId, "completion returns tracking to parent");
        }
        CheckMaps(0);
        // Save a pending task, travel away, display cached basement targets, then reload.
        J.ApplySaveState(JsonUtility.FromJson<QuestJournalManager.SaveState>(pending)); Move(Doc.transform.position);
        yield return Ready(); Check(GameSaveSystem.Instance.Save(1, false), "pending quest disk save");
        SceneTravelStateManager.GetOrCreate().TravelToScene("Level2-Floor1", Vector2.zero); yield return Ready(); QuietScene();
        Check(J.ContainsEntry(Quest) && !J.IsEntryCompleted(Quest) && J.GetParentEntryId(Quest) == QuestJournalManager.ArenaQuestId,
            "pending child and parent link survive travel");
        var cached = new GameObject("unloaded map", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
        cached.RebuildFromSnapshot(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor-1"));
        var destinations = new List<Vector2>(); cached.GetQuestTargets(Quest, destinations);
        Check(destinations.Count == 2, "unloaded basement menu still has two task destinations"); Object.Destroy(cached.gameObject);
        Check(GameSaveSystem.Instance.Load(1), "reload pending quest from disk"); yield return Ready(); QuietScene();
        Check(SceneManager.GetActiveScene().name == "Level2-Floor-1" && !J.IsEntryCompleted(Quest), "pending quest restored in basement");
        CheckMaps(2);
        Potion(Injectors[1], AlchemyPotionKind.Berserk); Check(Injectors[1].TryInject() && J.IsEntryCompleted(Quest), "restored right injector completes task");
        Move(Doc.transform.position); yield return Ready(); Check(GameSaveSystem.Instance.Save(2, false), "completed quest disk save");
        Check(GameSaveSystem.Instance.Load(2), "reload completed quest"); yield return Ready(); QuietScene();
        Check(J.IsEntryCompleted(Quest) && J.GetParentEntryId(Quest) == QuestJournalManager.ArenaQuestId, "completed child preserved by disk restore");
        yield return Read(); Check(J.IsEntryCompleted(Quest), "rereading after reload cannot reopen completed task"); CheckMaps(0);
    }
}
