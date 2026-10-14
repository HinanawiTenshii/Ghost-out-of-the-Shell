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
public static class Level2HeroProofSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    const string Quest = QuestJournalManager.Level2HeroProofId;
    static int checks, frame = -1;
    static IEnumerator suite;
    static double deadline;
    static Level2HeroProofSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, F).Invoke(target, args);
    static RuntimeMiniMapSceneData Bake(string name)
    {
        typeof(RuntimeMiniMapSceneDataGenerator).GetMethod("GenerateSceneSnapshot", F).Invoke(null,
            new object[] { "Assets/Scenes/Level2/" + name + ".unity", "Assets/Resources/MiniMaps/" + name + ".asset", name });
        AssetDatabase.SaveAssets();
        return AssetDatabase.LoadAssetAtPath<RuntimeMiniMapSceneData>("Assets/Resources/MiniMaps/" + name + ".asset");
    }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "hero-proof-quest-smoke";
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Desert.unity");
        var marker = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Level2HeroQuestTarget>(true)).Single();
        Check(!marker.IsTrialEntrance && marker.GetComponent<PersistentSceneTransitionPoint>().TargetSceneName == "Level2-Floor1", "desert stage target is exact city transition");
        Vector2 cityPosition = marker.transform.position;
        Check(cityPosition == new Vector2(22.31f, 71.37f), "city transition remains at authored position");
        var desert = Bake(scene.name);
        Check(desert.QuestTargets.Single(t => t.questId == QuestJournalManager.Level2CityTargetId).position == cityPosition, "desert snapshot stores exact transition position");
        scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        marker = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<Level2HeroQuestTarget>(true)).Single();
        Check(marker.IsTrialEntrance && marker.GetComponent<SceneTransitionPoint>().TargetSceneName == "LastScene", "floor1 stage target is exact existing trial entrance; destination unchanged");
        var city = Bake(scene.name);
        Vector2 trialPosition = marker.transform.position;
        Check(city.QuestTargets.Single(t => t.questId == QuestJournalManager.Level2TrialTargetId).position == trialPosition, "city snapshot stores exact world-space entrance position");
        Debug.Log("HERO_PROOF_TARGETS city=" + cityPosition.ToString("F5") + " trial=" + trialPosition.ToString("F5"));
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        var copy = Object.Instantiate(marker.gameObject); copy.name = "Trial Entrance Fixture";
        copy.transform.SetParent(null); SceneManager.MoveGameObjectToScene(copy, fixture); copy.transform.position = trialPosition;
        EditorSceneManager.SaveScene(fixture, "Assets/HeroProofFixture.unity");
        EditorSceneManager.CloseScene(scene, true);
        var arrival = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        EditorSceneManager.SaveScene(arrival, "Assets/HeroProofArrival.unity");
        EditorSceneManager.CloseScene(arrival, true);
        EditorBuildSettings.scenes = EditorBuildSettings.scenes.Where(s => s.path != "Assets/HeroProofArrival.unity")
            .Concat(new[] { new EditorBuildSettingsScene("Assets/HeroProofArrival.unity", true) }).ToArray();
        var serialized = new SerializedObject(copy.GetComponent<SceneTransitionPoint>());
        serialized.FindProperty("targetSceneName").stringValue = "HeroProofArrival"; serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorSceneManager.SaveScene(fixture);
        SessionState.SetInt("HeroProofAuthoredChecks", checks); SessionState.SetBool(nameof(Level2HeroProofSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(Level2HeroProofSmoke), false))
            {
                SessionState.SetBool(nameof(Level2HeroProofSmoke), false); checks = SessionState.GetInt("HeroProofAuthoredChecks", 0);
                deadline = EditorApplication.timeSinceStartup + 90; suite = Verify();
            }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("LEVEL2_HERO_PROOF_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Arrive(QuestJournalManager journal, Scene scene)
    { SceneManager.SetActiveScene(scene); Call(journal, "OnSceneLoaded", scene, LoadSceneMode.Single); }
    static void Objectives(QuestJournalManager journal, int count, bool firstComplete, bool secondComplete)
    {
        var texts = new List<string>(); var completed = new List<bool>();
        typeof(ZeldaHealthHeartsUI).GetMethod("BuildQuestObjectiveState", F).Invoke(null, new object[] { Quest, journal, texts, completed });
        Check(texts.Count == count && completed[0] == firstComplete && (count < 2 || completed[1] == secondComplete), "HUD stage objectives and completion strikes");
    }
    static IEnumerator Verify()
    {
        while (DocumentReader.IsInputBlocked) yield return null;
        var journal = QuestJournalManager.GetOrCreate(); journal.ResetAllEntries();
        var desertScene = SceneManager.CreateScene("Level2-Desert");
        var cityScene = SceneManager.CreateScene("Level2-Floor1");
        var upperScene = SceneManager.CreateScene("Level2-Floor2");
        var lowerScene = SceneManager.CreateScene("Level2-Floor-1");
        var trial = Object.FindObjectOfType<SceneTransitionPoint>();
        SceneManager.MoveGameObjectToScene(trial.gameObject, cityScene);
        Arrive(journal, desertScene);
        Check(journal.Level2HeroProofStage == 1 && journal.TryGetEntry(Quest, out _), "desert arrival automatically creates stage one");
        journal.TryGetEntry(Quest, out var entry);
        Check(entry.IsLevelObjective && entry.Title == "英雄之证" && entry.Details.Contains("勇者") && entry.Details.Contains("到达") == false, "main quest title, narrative and level-objective highlighting flag");
        Check(journal.TrackedEntryId == Quest && journal.InitialTrackedEntryId == Quest, "main quest initially tracked and used as default fallback");
        Objectives(journal, 1, false, false);
        var stageOne = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        var mapObject = new GameObject("Hero Map Tests", typeof(RectTransform), typeof(CanvasRenderer));
        var map = mapObject.AddComponent<RuntimeMiniMapGraphic>(); Object.DontDestroyOnLoad(mapObject);
        var desertMap = Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Desert");
        var cityMap = Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/Level2-Floor1");
        map.RebuildFromSnapshot(desertMap);
        Check(map.TryGetQuestTarget(Quest, out var cityTarget) && cityTarget == new Vector2(22.31f, 71.37f), "HUD/full map resolve stage-one target from cached snapshot");
        map.RebuildFromSnapshot(cityMap); Check(!map.TryGetQuestTarget(Quest, out _), "trial marker hidden before city arrival");
        var regionMethod = typeof(TabJournalMenuController).GetMethod("SceneHasQuestTarget", F);
        Check((bool)regionMethod.Invoke(null, new object[] { "Level2-Desert", cityScene, Quest }), "unloaded desert region button displays first-stage marker");
        Arrive(journal, cityScene);
        Check(journal.Level2HeroProofStage == 2 && !journal.IsEntryCompleted(Quest), "entering Floor1 advances without completing quest");
        Check(journal.Entries.Count(e => e.Id == Quest) == 1 && entry.IsLevelObjective, "same main entry updated, no duplicate stage quests");
        Check(entry.Details.Contains("已抵达") && entry.Details.Contains("试炼大门"), "stage-two narrative and objective updated");
        Objectives(journal, 2, true, false);
        Check(map.TryGetQuestTarget(Quest, out var trialTarget) && trialTarget == (Vector2)trial.transform.position, "existing map cache instantly changes target without rebuilding");
        Check((bool)regionMethod.Invoke(null, new object[] { "Level2-Floor1", cityScene, Quest }), "live city region button resolves trial target");
        Check(!(bool)regionMethod.Invoke(null, new object[] { "Level2-Desert", cityScene, Quest }), "desert region marker disappears at stage two");
        trial.gameObject.SetActive(false);
        Check(RuntimeMiniMapGraphic.TryGetSceneQuestTarget(cityScene, Quest, out var dormantTarget) && dormantTarget == trialTarget, "streamed-out entrance remains a valid live map target");
        trial.gameObject.SetActive(true);
        journal.AddOrUpdateEntry("side.test", "支线", "支线任务");
        Arrive(journal, upperScene); Arrive(journal, lowerScene); Arrive(journal, desertScene);
        Check(journal.Level2HeroProofStage == 2 && journal.TrackedEntryId == "side.test", "returning through other floors/desert cannot regress or steal side-quest tracking");
        map.RebuildFromSnapshot(desertMap); Check(!map.TryGetQuestTarget(Quest, out _), "returning desert map no longer points to city entry");
        Check((bool)regionMethod.Invoke(null, new object[] { "Level2-Floor1", desertScene, Quest }), "unloaded city region button retains second-stage marker");
        var stageTwo = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        journal.ResetAllEntries(); journal.ApplySaveState(stageTwo);
        Check(journal.Level2HeroProofStage == 2 && journal.TrackedEntryId == "side.test", "JSON save restores second stage and chosen tracked quest in desert");
        journal.ApplySaveState(stageOne);
        Check(journal.Level2HeroProofStage == 1 && journal.TrackedEntryId == Quest, "loading earlier save legitimately restores first stage");
        map.RebuildFromSnapshot(desertMap); Check(map.TryGetQuestTarget(Quest, out _), "marker follows older save immediately");
        // Old saves lack the new quest field and entry, but their visited-scene evidence must migrate.
        var old = new QuestJournalManager.SaveState { initialized = new List<string> { "Level2-Desert", "Level2-Floor1" }, tracked = "side.test" };
        old.entries.Add(new QuestJournalManager.SavedEntry { id = "side.test", title = "支线", details = "已有的任务", scene = "Level2-Floor1" });
        journal.ApplySaveState(old);
        Check(journal.Level2HeroProofStage == 2 && journal.TryGetEntry(Quest, out _) && journal.TrackedEntryId == "side.test", "legacy save migration infers city arrival without stealing tracked side quest");
        // Starting directly on a city floor for editing/testing gives the appropriate main stage.
        foreach (var scene in new[] { cityScene, upperScene, lowerScene })
        { journal.ResetAllEntries(); Arrive(journal, scene); Check(journal.Level2HeroProofStage == 2 && journal.TryGetEntry(Quest, out var main) && main.IsLevelObjective, "direct city-floor start supplies stage two"); }
        Arrive(journal, cityScene); journal.TrackEntry(Quest);
        var originalTarget = trial.TargetSceneName;
        typeof(SceneTransitionPoint).GetField("targetSceneName", F).SetValue(trial, "");
        Call(trial, "LoadTargetScene");
        Check(journal.Level2HeroProofStage == 2 && !journal.IsEntryCompleted(Quest), "unconfigured transition cannot complete main quest");
        typeof(SceneTransitionPoint).GetField("targetSceneName", F).SetValue(trial, originalTarget);
        Call(trial, "LoadTargetScene");
        Check(journal.Level2HeroProofStage == 2 && !journal.IsEntryCompleted(Quest), "interaction cover animation does not complete before actual scene arrival");
        while (SceneManager.GetActiveScene().name != "HeroProofArrival" || DocumentReader.IsInputBlocked) yield return null;
        Check(journal.Level2HeroProofStage == 3 && journal.IsEntryCompleted(Quest), "real asynchronous SceneTransitionPoint arrival completes quest despite fresh-level reset");
        Check(journal.TryGetEntry(Quest, out var complete) && complete.IsLevelObjective && complete.Details.Contains("已打开"), "completed main quest carried to destination journal");
        Objectives(journal, 2, true, true);
        map.RebuildFromSnapshot(cityMap); Check(!map.TryGetQuestTarget(Quest, out _), "completed quest has no map target");
        var completedSave = JsonUtility.FromJson<QuestJournalManager.SaveState>(JsonUtility.ToJson(journal.CaptureSaveState()));
        journal.ResetAllEntries(); journal.ApplySaveState(completedSave);
        Check(journal.Level2HeroProofStage == 3 && journal.IsEntryCompleted(Quest), "completed state survives JSON restore");
        journal.ResetAllEntries(); Check(journal.Level2HeroProofStage == 0 && !journal.ContainsEntry(Quest), "new-game reset clears all stages and pending arrival");
        // Existing default main quests keep their canonical names and tracking semantics.
        journal.AddOrUpdateEntry("level0.escape_prison", "old", "escape"); journal.TryGetEntry("level0.escape_prison", out var level0);
        journal.AddOrUpdateEntry("level1.escape_castle", "old", "escape"); journal.TryGetEntry("level1.escape_castle", out var level1);
        Check(level0.Title == QuestJournalManager.Level0EscapeTitle && level1.Title == QuestJournalManager.Level1EscapeTitle, "existing main quests retain canonical titles");
        Object.Destroy(mapObject); yield return null;
    }
}
