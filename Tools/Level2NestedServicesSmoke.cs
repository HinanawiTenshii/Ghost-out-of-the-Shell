using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

// Execute only in the disposable project. Does not modify authored scenes or real saves.
[InitializeOnLoad]
public static class Level2NestedServicesSmoke
{
    static readonly string[] Scenes = { "Level2-Floor-1", "Level2-Floor1", "Level2-Floor2", "Level2-Desert" };
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static Stack<IEnumerator> routines;
    static int checks, frame = -1;
    static double deadline;
    static string failure;
    static ActiveZeldaCharacterDataSource source;
    static ZeldaHealthHeartsUI hud;
    static PersistentInventory inventory;
    static PlayerGrowthAttributes growth;
    static QuestJournalManager quests;
    static MapPointOfInterestManager points;

    static Level2NestedServicesSmoke()
    {
        EditorApplication.update += Tick;
        Application.logMessageReceived += (message, stack, kind) =>
        {
            // Existing editor-only generator failure in this test copy (Shader Graph package absent).
            // Leave the original error in the log; do not hide any gameplay/runtime exception.
            if (message.Contains("Shader Graph type not found: UnityEditor.ShaderGraph.GraphData") && stack.Contains("CRTShaderGraphGenerator")) return;
            if (Application.isPlaying && (message.Contains("DontDestroyOnLoad only works") || kind == LogType.Exception))
                failure = message + "\n" + stack;
        };
    }
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true)).ToArray();
    static T Field<T>(object target, string name) => (T)target.GetType().GetField(name, Flags).GetValue(target);

    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project required");
        PlayerSettings.productName = "level2-nested-services-smoke";
        foreach (string name in Scenes)
        {
            Scene scene = EditorSceneManager.OpenScene("Assets/Scenes/Level2/" + name + ".unity");
            Check(InScene<Component>(scene).All(c => c != null), name + " no missing scripts");
            foreach (Type type in new[] { typeof(ActiveZeldaCharacterDataSource), typeof(ZeldaHealthHeartsUI),
                typeof(PersistentInventory), typeof(PlayerGrowthAttributes), typeof(PauseMenuController),
                typeof(TabJournalMenuController), typeof(CameraVisionObjectStreaming), typeof(GhostPlayerSpawnPoint),
                typeof(SharedPlayerHostilityTimer), typeof(LevelSceneGroup) })
            {
                var found = InScene<Component>(scene).Where(c => c.GetType() == type).ToArray();
                if (type == typeof(SharedPlayerHostilityTimer) && name == "Level2-Desert" && found.Length == 0) continue;
                Check(found.Length == 1, name + " one authored " + type.Name);
                Debug.Log("NESTED_SERVICE_AUDIT " + name + " / " + type.Name + " / " + AnimationUtility.CalculateTransformPath(found[0].transform, null));
            }
            Check(InScene<ActiveZeldaCharacterDataSource>(scene)[0].transform.parent != null, name + " nested source regression case");
            Check(InScene<ZeldaHealthHeartsUI>(scene)[0].transform.parent != null, name + " nested HUD regression case");
            Check(InScene<GameSaveSystem>(scene).Length == 0 && InScene<SceneTravelStateManager>(scene).Length == 0 &&
                InScene<QuestJournalManager>(scene).Length == 0 && InScene<MapPointOfInterestManager>(scene).Length == 0,
                name + " global managers bootstrap as roots, not nested scene duplicates");
        }
        SessionState.SetInt("NestedServicesChecks", checks);
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/" + Scenes[0] + ".unity");
        SessionState.SetBool(nameof(Level2NestedServicesSmoke), true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(Level2NestedServicesSmoke), false))
            {
                SessionState.SetBool(nameof(Level2NestedServicesSmoke), false);
                checks = SessionState.GetInt("NestedServicesChecks", 0);
                routines = new Stack<IEnumerator>(); routines.Push(Verify());
                deadline = EditorApplication.timeSinceStartup + 480;
            }
            if (routines == null) return;
            if (failure != null) throw new Exception(failure);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("nested services test timeout");
            while (routines.Count > 0)
            {
                var step = routines.Peek();
                if (!step.MoveNext()) { routines.Pop(); continue; }
                if (step.Current is IEnumerator child) { routines.Push(child); continue; }
                return;
            }
            routines = null;
            Debug.Log("LEVEL2_NESTED_SERVICES_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception ex) { routines = null; Debug.LogException(ex); EditorApplication.Exit(1); }
    }

    static IEnumerator Ready(string name)
    {
        yield return null; yield return null;
        while (SceneManager.GetActiveScene().name != name || RetroSceneLoadReveal.IsBlockingInput ||
            GameSaveSystem.IsLoading || GameSaveSystem.IsPreparingSceneSave ||
            SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress || SceneTravelStateManager.GetOrCreate().IsQuickRestartPending)
            yield return null;
        yield return null; yield return null;
    }

    static void RememberServices()
    {
        source = ActiveZeldaCharacterDataSource.Instance; hud = ZeldaHealthHeartsUI.Instance;
        inventory = PersistentInventory.Instance; growth = PlayerGrowthAttributes.Instance;
        quests = QuestJournalManager.Instance; points = MapPointOfInterestManager.Instance;
    }

    static void VerifyGameplay(string name)
    {
        Scene scene = SceneManager.GetActiveScene();
        foreach (Component service in new Component[] { source, hud, inventory, growth, quests, points,
            GameSaveSystem.Instance, SceneTravelStateManager.GetOrCreate() })
        {
            Check(service != null, name + " original service survives");
            Check(service.transform.parent == null && service.gameObject.scene.name == "DontDestroyOnLoad", name + " persistent root " + service.GetType().Name);
            Check(Object.FindObjectsOfType(service.GetType(), true).Length == 1, name + " singleton " + service.GetType().Name);
        }
        Check(ActiveZeldaCharacterDataSource.Instance == source && ZeldaHealthHeartsUI.Instance == hud, name + " source/HUD identity retained");
        Check(!Object.FindObjectsOfType<Transform>(true).Any(t => t.name == "SceneAssets" && t.gameObject.scene.name == "DontDestroyOnLoad"), name + " scene container not retained");
        var canvas = hud.GetComponent<Canvas>();
        Check(canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace, name + " HUD remains screen-space root canvas");
        Check(canvas.renderMode == RenderMode.ScreenSpaceOverlay || (canvas.worldCamera != null && canvas.worldCamera.gameObject.scene == scene), name + " HUD uses the new scene CRT camera");
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        Check(mover != null && source.ActiveMover == mover && source.ActiveCharacterData == mover.CharacterData, name + " active actor updates");
        Check(source.CurrentHealth == mover.CharacterData.CurrentHealth && source.SkillValue == mover.CharacterData.FinalSkillValue, name + " active stats update");
        Check(Field<ActiveZeldaCharacterDataSource>(hud, "dataSource") == source && Field<PersistentInventory>(hud, "inventory") == inventory, name + " HUD retains live subscriptions");
        Check(Field<string>(hud, "currentTaskSceneKey") == scene.path, name + " HUD task config uses gameplay scene");
        Check(Field<Scene>(hud, "taskMiniMapScene") == scene, name + " HUD minimap uses gameplay scene");
        var miniMap = hud.GetComponentInChildren<RuntimeMiniMapGraphic>(true);
        Check(miniMap != null && Field<string>(miniMap, "geometrySceneName") == name, name + " HUD map geometry refreshed");
        Check(hud.GetComponentInChildren<RuntimeBuffDurationBar>(true) != null && hud.GetComponentInChildren<RuntimeSandwormBossBar>(true) != null, name + " buff and boss UI present");
        Check(InScene<CameraVisionObjectStreaming>(scene).Single().IsInitialViewPrepared, name + " nested streamer prepares scene");
        Check(SharedPlayerHostilityTimer.ForScene(scene) != null && InScene<SharedPlayerHostilityTimer>(scene).Length == 1, name + " scene hostility timer resolved without duplicates");
        Check(Object.FindObjectsOfType<EventSystem>().Length == 1, name + " one UI event system");
        var pause = InScene<PauseMenuController>(scene).Single(); pause.OpenMenu();
        Check(PauseMenuController.IsPaused && Time.timeScale == 0, name + " nested pause UI opens");
        typeof(PauseMenuController).GetMethod("OpenSaveSlots", Flags).Invoke(pause, new object[] { true });
        Check(SaveSlotPanel.IsOpen, name + " save slots open");
        typeof(SaveSlotPanel).GetMethod("Close", Flags).Invoke(Object.FindObjectOfType<SaveSlotPanel>(), null);
        pause.CloseMenu();
        var tab = InScene<TabJournalMenuController>(scene).Single(); tab.OpenMenu();
        Check(TabJournalMenuController.IsOpen && Time.timeScale == 0, name + " nested journal opens");
        var menuMap = Field<RuntimeMiniMapGraphic>(tab, "miniMapGraphic");
        Check(menuMap != null && Field<string>(menuMap, "geometrySceneName") == name, name + " journal map geometry refreshed");
        tab.CloseMenu();
        Check(!PauseMenuController.IsPaused && !TabJournalMenuController.IsOpen && Time.timeScale > 0, name + " menus release input");
        Debug.Log("NESTED_SERVICE_RUNTIME_OK " + name);
    }

    static IEnumerator Travel(string name)
    {
        SceneTravelStateManager.GetOrCreate().TravelToScene(name, new Vector2(1000, 1000));
        yield return Ready(name);
        VerifyGameplay(name);
    }

    static IEnumerator Verify()
    {
        yield return Ready(Scenes[0]); RememberServices(); VerifyGameplay(Scenes[0]);
        var data = InScene<ZeldaCharacterData>(SceneManager.GetActiveScene()).First(d => d.NativeCharacterSkillId == "builder");
        var actor = data.gameObject; actor.SetActive(true);
        var ai = actor.GetComponent<ZeldaCharacterAiBase>(); if (ai != null) ai.enabled = false;
        var target = actor.GetComponent<ZeldaFourWayMover>(); target.enabled = false;
        var player = ZeldaRuntimeRegistry.GetControlledMover();
        player.transform.position = new Vector3(1000, 1000); actor.transform.position = new Vector3(1000, 1001);
        var state = player.CharacterData.CaptureRuntimeState(); state.possessionEnergy = 100; player.CharacterData.ApplyRuntimeState(state);
        Physics2D.SyncTransforms();
        Check((bool)typeof(ZeldaFourWayMover).GetMethod("TrySwitchControl", Flags).Invoke(player, new object[] { target }), "actual possession succeeds");
        yield return null;
        Check(source.ActiveCharacterData == data && source.SkillValue == data.FinalSkillValue, "data source follows actual possession");
        foreach (string name in Scenes.Skip(1)) yield return Travel(name);
        yield return Travel(Scenes[0]);
        Check(GameSaveSystem.Instance.Save(1, false), "save from nested-services scene");
        SceneManager.LoadScene("TitleScreen"); yield return Ready("TitleScreen");
        Object.Destroy(source.gameObject); Object.Destroy(hud.gameObject); yield return null; yield return null;
        Check(ActiveZeldaCharacterDataSource.Instance == null && ZeldaHealthHeartsUI.Instance == null, "cold-load fixture no source/HUD");
        Check(GameSaveSystem.Instance.Load(1), "load save without existing source/HUD");
        yield return Ready(Scenes[0]); RememberServices(); VerifyGameplay(Scenes[0]);
        yield return Travel(Scenes[1]);

        // Exercise the other three authored scenes as first gameplay scene, not just as duplicates.
        foreach (string first in Scenes.Skip(1))
        {
            SceneManager.LoadScene("TitleScreen"); yield return Ready("TitleScreen");
            Object.Destroy(source.gameObject); Object.Destroy(hud.gameObject); yield return null; yield return null;
            SceneManager.LoadScene(first); yield return Ready(first);
            RememberServices(); VerifyGameplay(first);
            yield return Travel(Scenes[0]);
        }
        SceneManager.LoadScene("TitleScreen"); yield return Ready("TitleScreen");
        Check(GameSaveSystem.Instance.StartNewGame(0), "normal main-menu startup");
        yield return Ready("TutorialLevel"); RememberServices();
        yield return Travel(Scenes[1]);
        foreach (string name in new[] { "Level1-Floor1", "Level1-Floor2", "Level1-Floor3", "Level1-Floor-1", "Level1-Garden" })
            yield return Travel(name);
        Check(failure == null, "no non-root persistence warnings or runtime exceptions");
    }
}
