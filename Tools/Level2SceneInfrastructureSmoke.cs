using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Run ONLY in the isolated project. Generated maps may be copied back; scenes are never saved here.
[InitializeOnLoad]
public static class Level2SceneInfrastructureSmoke
{
    static readonly string[] Names = { "Level2-Floor1", "Level2-Floor-1", "Level2-Floor2" };
    static readonly string[] GroupNames = { "Level2-Floor1", "Level2-Floor-1", "Level2-Floor2", "Level2-Desert" };
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static string failure;
    static Level2SceneInfrastructureSmoke() { EditorApplication.update += Tick; }
    static string PathFor(string name) => "Assets/Scenes/Level2/" + name + ".unity";
    static T[] InScene<T>(Scene s) where T : Component => s.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated test project required");
        PlayerSettings.productName = "level2-infrastructure-smoke"; // Never writes the real game's saves/preferences.
        EditorBuildSettings.scenes = GroupNames.Select(n => new EditorBuildSettingsScene(PathFor(n), true)).ToArray();
        foreach (var name in Names)
        {
            var s = EditorSceneManager.OpenScene(PathFor(name));
            Check(InScene<Component>(s).All(c => c != null), name + ": no missing scripts");
            Check(InScene<ActiveZeldaCharacterDataSource>(s).Length == 1, name + ": data source");
            Check(InScene<PersistentInventory>(s).Length == 1, name + ": inventory");
            Check(InScene<PlayerGrowthAttributes>(s).Length == 1, name + ": growth");
            Check(InScene<ZeldaHealthHeartsUI>(s).Length == 1, name + ": HUD");
            Check(InScene<PauseMenuController>(s).Length == 1, name + ": pause and save menu");
            Check(InScene<TabJournalMenuController>(s).Length == 1, name + ": tab menu");
            Check(InScene<GhostPlayerSpawnPoint>(s).Length == 1, name + ": original spawn preserved");
            Check(InScene<CameraVisionObjectStreaming>(s).Length == 1, name + ": original streaming preserved");
            Check(InScene<SharedPlayerHostilityTimer>(s).Length == 1, name + ": no duplicate timer");
            var group = InScene<LevelSceneGroup>(s).Single().CreateDefinition();
            Check(group.SceneNames.SequenceEqual(GroupNames) && group.EntrySceneName == Names[0], name + ": common save/map level");
            Check(InScene<GameSaveSystem>(s).Length == 0 && InScene<SceneTravelStateManager>(s).Length == 0, name + ": no duplicate bootstrapped save services");
            var captureObject = new GameObject("Snapshot Capture", typeof(RectTransform), typeof(CanvasRenderer));
            captureObject.hideFlags = HideFlags.HideAndDontSave;
            var capture = captureObject.AddComponent<RuntimeMiniMapGraphic>(); capture.RebuildFromScene(s);
            string assetPath = "Assets/Resources/MiniMaps/" + name + ".asset";
            var map = AssetDatabase.LoadAssetAtPath<RuntimeMiniMapSceneData>(assetPath);
            if (!map) { map = ScriptableObject.CreateInstance<RuntimeMiniMapSceneData>(); AssetDatabase.CreateAsset(map, assetPath); }
            capture.CopyCurrentGeometryTo(map, name, InScene<TabJournalMenuController>(s).Single().AreaDisplayName);
            EditorUtility.SetDirty(map); Object.DestroyImmediate(captureObject);
            Debug.Log("LEVEL2_EDIT_OK " + name);
        }
        AssetDatabase.SaveAssets();
        var catalog = Resources.Load<SaveGameCatalog>("SaveGameCatalog");
        foreach (var n in Names) Check(catalog.scenes.Count(s => s.name == n) == 1 && catalog.scenes.Single(s => s.name == n).CreateSaveSnapshot().authoredState.Contains("Level2 Scene Group"), n + ": updated save template");
        EditorSceneManager.OpenScene(PathFor(Names[0]));
        SessionState.SetInt("Level2InfrastructureChecks", checks);
        SessionState.SetBool("Level2InfrastructureRun", true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("Level2InfrastructureRun", false))
            {
                SessionState.SetBool("Level2InfrastructureRun", false);
                checks = SessionState.GetInt("Level2InfrastructureChecks", 0);
                Application.logMessageReceived += (message, stack, kind) => { if (kind == LogType.Exception || kind == LogType.Error) failure = message; };
                deadline = EditorApplication.timeSinceStartup + 180; suite = Verify();
            }
            if (suite == null) return;
            if (failure != null) throw new Exception("Runtime error: " + failure);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("runtime suite timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("LEVEL2_INFRASTRUCTURE_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Ready(string name)
    {
        int stable = 0;
        while (stable < 4)
        {
            bool ready = SceneManager.GetActiveScene().name == name && !GameSaveSystem.IsLoading &&
                !GameSaveSystem.IsPreparingSceneSave && !RetroSceneLoadReveal.IsBlockingInput &&
                !SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress && ZeldaRuntimeRegistry.GetControlledMover() != null;
            stable = ready ? stable + 1 : 0; yield return null;
        }
    }
    static void VerifyRuntime(string name)
    {
        Check(Object.FindObjectsOfType<ZeldaHealthHeartsUI>().Length == 1, name + ": one persistent HUD");
        Check(Object.FindObjectsOfType<PersistentInventory>().Length == 1, name + ": one inventory");
        Check(Object.FindObjectsOfType<PlayerGrowthAttributes>().Length == 1, name + ": one growth state");
        Check(Object.FindObjectsOfType<ActiveZeldaCharacterDataSource>().Length == 1, name + ": one data source");
        Check(Object.FindObjectsOfType<GameSaveSystem>().Length == 1 && Object.FindObjectsOfType<SceneTravelStateManager>().Length == 1, name + ": save services bootstrapped");
        Check(Object.FindObjectsOfType<EventSystem>().Length == 1, name + ": one UI input system");
        Check(ZeldaHealthHeartsUI.Instance.GetComponentInChildren<RuntimeBuffDurationBar>() != null, name + ": buffs attached");
        Check(ZeldaHealthHeartsUI.Instance.GetComponentInChildren<RuntimeMiniMapGraphic>() != null, name + ": minimap attached");
        var pause = Object.FindObjectOfType<PauseMenuController>(); pause.OpenMenu();
        Check(PauseMenuController.IsPaused && Time.timeScale == 0, name + ": pause opens");
        typeof(PauseMenuController).GetMethod("OpenSaveSlots", Flags).Invoke(pause, new object[] { true });
        Check(SaveSlotPanel.IsOpen, name + ": save slot window opens");
        // Close through the panel's real API, then restore the game menu.
        var panel = Object.FindObjectOfType<SaveSlotPanel>();
        typeof(SaveSlotPanel).GetMethod("Close", Flags).Invoke(panel, null);
        pause.CloseMenu(); Check(!PauseMenuController.IsPaused && Time.timeScale > 0, name + ": unpause");
        var tab = Object.FindObjectOfType<TabJournalMenuController>(); tab.OpenMenu();
        Check(TabJournalMenuController.IsOpen, name + ": tab opens");
        var names = (List<string>)typeof(TabJournalMenuController).GetMethod("GetMapSceneNames", Flags).Invoke(null, new object[] { name });
        Check(names.SequenceEqual(GroupNames), name + ": map lists all floors and desert"); tab.CloseMenu();
        foreach (var n in GroupNames) Check(Resources.Load<RuntimeMiniMapSceneData>("MiniMaps/" + n) != null, name + ": map available for " + n);
    }
    static IEnumerator Verify()
    {
        var wait = Ready(Names[0]); while (wait.MoveNext()) yield return null;
        VerifyRuntime(Names[0]);
        var inventory = PersistentInventory.Instance; inventory.ClearAll();
        var item = Resources.LoadAll<GameObject>("PickupItems").Select(p => p.GetComponent<PickupItemBase>()).First(p => p is GlassBottlePickupItem);
        Check(inventory.TryAddItem(item.ItemId, 2, 3), "test inventory prepared");
        var hud = ZeldaHealthHeartsUI.Instance;
        for (int i = 1; i < Names.Length; i++)
        {
            SceneTravelStateManager.GetOrCreate().TravelToScene(Names[i], new Vector2(5, 5));
            wait = Ready(Names[i]); while (wait.MoveNext()) yield return null;
            VerifyRuntime(Names[i]);
            Check(PersistentInventory.Instance == inventory && inventory.GetQuantity(item.ItemId) == 2, "inventory persists on " + Names[i]);
            Check(ZeldaHealthHeartsUI.Instance == hud, "HUD retained on " + Names[i]);
            Debug.Log("LEVEL2_RUNTIME_OK " + Names[i]);
        }
        Check(GameSaveSystem.Instance.Save(6, false), "manual save succeeds");
        var data = GameSaveSystem.Instance.Read(6);
        Check(data != null && data.scene == Names[2], "saved scene identity");
        var visit = data.visitedLevels.Single();
        Check(visit.id == "level2" && visit.initialScenes.Count == 4, "one level contains all floor and desert templates");
        inventory.ClearAll();
        Check(GameSaveSystem.Instance.Load(6), "saved Level2 scene is loadable");
        wait = Ready(Names[2]); while (wait.MoveNext()) yield return null;
        VerifyRuntime(Names[2]);
        Check(PersistentInventory.Instance.GetQuantity(item.ItemId) == 2, "disk load restores inventory");
        Check(ZeldaRuntimeRegistry.GetControlledMover() != null, "disk load restores controlled actor");
    }
}
