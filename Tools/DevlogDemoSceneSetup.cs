using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Generate and verify in the isolated project; copy only the new scene + meta back.
[InitializeOnLoad]
public static class DevlogDemoSceneSetup
{
    const string ScenePath = "Assets/Scenes/DevlogDemo.unity";
    const string RunKey = "DevlogDemoSceneSetup.Run";
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static string failure;

    static DevlogDemoSceneSetup() { EditorApplication.update += Tick; }
    static void Check(bool condition, string message)
    {
        checks++;
        if (!condition) throw new Exception(message);
    }
    static T[] InScene<T>(Scene scene) where T : Component => scene.GetRootGameObjects()
        .SelectMany(go => go.GetComponentsInChildren<T>(true)).ToArray();
    static GameObject Prefab(string name, Scene scene, Vector3 position)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");
        Check(prefab != null, "Prefab exists: " + name);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        go.transform.position = position;
        go.transform.rotation = Quaternion.identity;
        PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
        return go;
    }
    static void Edit(Object target, string property, Action<SerializedProperty> change)
    {
        var serialized = new SerializedObject(target);
        var field = serialized.FindProperty(property);
        Check(field != null, "Serialized field: " + property);
        change(field); serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    static void MatchLevel0Crt(CRTScreenEffect target)
    {
        var sourceScene = EditorSceneManager.OpenScene("Assets/Scenes/Level0.unity", OpenSceneMode.Additive);
        try
        {
            var source = InScene<CRTScreenEffect>(sourceScene).Single();
            var serializedSource = new SerializedObject(source);
            var serializedTarget = new SerializedObject(target);
            var field = serializedSource.GetIterator();
            while (field.NextVisible(true))
            {
                // Copy the effect's authored configuration, never its GameObject identity.
                if (field.depth == 0 && !field.name.StartsWith("m_"))
                    serializedTarget.CopyFromSerializedProperty(field);
            }
            serializedTarget.ApplyModifiedPropertiesWithoutUndo();
            target.enabled = source.enabled;
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
            SessionState.SetString(RunKey + ".CRT", JsonUtility.ToJson(source));
        }
        finally { EditorSceneManager.CloseScene(sourceScene, true); }
    }

    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "Isolated project required");
        PlayerSettings.productName = "devlog-demo-scene-smoke";
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Existing prefabs preserve the game's visual and input configuration.
        var camera = Prefab("Main Camera", scene, new Vector3(0, 0, -10));
        MatchLevel0Crt(camera.GetComponent<CRTScreenEffect>());
        Prefab("ActiveZeldaCharacterDataSource", scene, Vector3.zero);
        Prefab("PlayerStates", scene, Vector3.zero);
        var inventory = Prefab("PersistentInventory", scene, Vector3.zero).GetComponent<PersistentInventory>();
        Edit(inventory, "saveToPlayerPrefs", p => p.boolValue = false);
        Edit(inventory, "saveKey", p => p.stringValue = "CogitansExtensa.DevlogDemo.Inventory");
        var hud = Prefab("ZeldaHealthHeartsUI", scene, Vector3.zero).GetComponent<ZeldaHealthHeartsUI>();
        // Legacy name: this flag controls the whole minimap, not just quests.
        Edit(hud, "showTaskPointer", p => p.boolValue = true);
        Edit(hud, "taskPointerDestinations", p => p.arraySize = 0);
        var menu = Prefab("PauseMenu", scene, Vector3.zero).GetComponent<TabJournalMenuController>();
        Edit(menu, "areaNameOverride", p => p.stringValue = "开发日志演示");
        Prefab("GhostSpawnPoint", scene, Vector3.zero);
        new GameObject("Shared Player Hostility Timer").AddComponent<SharedPlayerHostilityTimer>();
        var settings = new GameObject("Devlog Demo Settings");
        settings.AddComponent<DevlogDemoSceneSettings>();
        var group = settings.AddComponent<LevelSceneGroup>();
        Edit(group, "levelId", p => p.stringValue = "devlog-demo");
        Edit(group, "displayName", p => p.stringValue = "开发日志演示");
        Edit(group, "scenes", p => p.arraySize = 0); // Falls back to this scene's name/path.
        // No environment, props, NPCs, objectives, music or artificial collision.
        RenderSettings.skybox = null;
        RenderSettings.fog = false;
        Check(EditorSceneManager.SaveScene(scene, ScenePath), "Scene saved");
        // Test actual serialized asset, not merely the construction-time objects.
        scene = EditorSceneManager.OpenScene(ScenePath);
        Check(scene.GetRootGameObjects().Length == 9, "Only nine infrastructure/spawn roots");
        Check(InScene<Component>(scene).All(c => c != null), "No missing scripts");
        Check(InScene<Collider2D>(scene).Length == 0, "No authored collision");
        Check(InScene<Renderer>(scene).Length == 0, "No authored scenery or actors");
        Check(InScene<ZeldaCharacterData>(scene).Length == 0, "Ghost is spawned, not duplicated");
        Check(InScene<GhostPlayerSpawnPoint>(scene).Length == 1, "One ghost spawn");
        Check(InScene<Camera>(scene).Length == 1, "One gameplay camera");
        Check(InScene<AudioListener>(scene).Length == 1, "One audio listener");
        Check(JsonUtility.ToJson(InScene<CRTScreenEffect>(scene).Single()) == SessionState.GetString(RunKey + ".CRT", ""), "Saved CRT parameters exactly match Level0");
        Check(InScene<CameraVisionObjectStreaming>(scene).Length == 1, "Streaming ready for later content");
        Check(InScene<LevelSceneGroup>(scene).Single().CreateDefinition().EntrySceneName == "DevlogDemo", "Independent demo identity");
        Check(!EditorBuildSettings.scenes.Any(s => s.path == ScenePath), "Formal build list unchanged");
        SessionState.SetInt(RunKey + ".Checks", checks);
        SessionState.SetBool(RunKey, true);
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(RunKey, false))
            {
                SessionState.SetBool(RunKey, false);
                checks = SessionState.GetInt(RunKey + ".Checks", 0);
                Application.logMessageReceived += Log;
                deadline = EditorApplication.timeSinceStartup + 100;
                suite = Verify();
            }
            if (suite == null) return;
            if (failure != null) throw new Exception("Runtime error: " + failure);
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Runtime test timed out");
            if (suite.MoveNext()) return;
            suite = null;
            Debug.Log("DEVLOG_DEMO_PASS checks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Log(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error) failure = message;
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsPreparingSceneSave ||
            ZeldaRuntimeRegistry.GetControlledMover() == null) yield return null;
        for (int i = 0; i < 5; i++) yield return null;
        var scene = SceneManager.GetActiveScene();
        var ghost = ZeldaRuntimeRegistry.GetControlledMover();
        Check(ghost.CharacterData is GhostZeldaCharacterData, "Spawned controllable ghost");
        Check(Object.FindObjectsOfType<ZeldaCharacterData>().Length == 1, "Only one actor at runtime");
        Check((Vector2)ghost.transform.position == Vector2.zero, "Ghost starts at origin");
        Check(ActiveZeldaCharacterDataSource.Instance.ActiveMover == ghost, "HUD data connected");
        Check(Object.FindObjectsOfType<PersistentInventory>().Length == 1, "One inventory");
        Check(PersistentInventory.Instance.SlotCount == 5 && PersistentInventory.Instance.Slots.All(s => s.Quantity == 0), "Empty default inventory");
        Check(Object.FindObjectsOfType<PlayerGrowthAttributes>().Length == 1, "Growth/skills ready");
        Check(Object.FindObjectsOfType<ZeldaHealthHeartsUI>().Length == 1, "One HUD");
        Check(Object.FindObjectsOfType<EventSystem>().Length == 1, "One UI input system");
        Check(Object.FindObjectsOfType<GameSaveSystem>().Length == 1 && Object.FindObjectsOfType<SceneTravelStateManager>().Length == 1, "Bootstrap services ready");
        Check(JsonUtility.ToJson(Camera.main.GetComponent<CRTScreenEffect>()) == SessionState.GetString(RunKey + ".CRT", ""), "Runtime CRT parameters match Level0");
        Check(QuestJournalManager.GetOrCreate().CaptureSaveState().entries.Count == 0, "No unrelated quests");
        var pause = Object.FindObjectOfType<PauseMenuController>();
        pause.OpenMenu();
        Check(PauseMenuController.IsPaused && Time.timeScale == 0, "Pause works");
        pause.CloseMenu();
        Check(!PauseMenuController.IsPaused && Time.timeScale > 0, "Resume works");
        var tab = Object.FindObjectOfType<TabJournalMenuController>();
        tab.OpenMenu();
        Check(TabJournalMenuController.IsOpen, "Map/journal/skills menu opens");
        Check(tab.AreaDisplayName == "开发日志演示", "Demo map label");
        tab.CloseMenu();
        Check(!TabJournalMenuController.IsOpen, "Tab menu closes");
        Vector3 initialCamera = Camera.main.transform.position;
        ghost.transform.position += new Vector3(4, 2, 0);
        float until = Time.time + 8;
        while (Time.time < until)
        {
            if (PauseMenuController.IsPaused) throw new Exception("Unexpected empty-scene pause");
            yield return null;
        }
        Check(!PauseMenuController.IsPaused, "No empty-scene interruption after eight seconds");
        var cameraDelta = (Vector2)(Camera.main.transform.position - initialCamera);
        Check(Vector2.Distance(cameraDelta, new Vector2(4, 2)) < .1f, "Camera follows ghost");
        Check(DevlogDemoSceneSettings.SuppressesAutomaticSaves(scene), "Automatic-save exemption is scene-local");
        GameSaveSystem.RequestAutoSave("demo_test", true);
        Check(!GameSaveSystem.Instance.Save(0, false), "Automatic save suppressed");
        var settings = Object.FindObjectOfType<DevlogDemoSceneSettings>();
        settings.enabled = false;
        Check(!DevlogDemoSceneSettings.SuppressesAutomaticSaves(scene), "Options release when disabled");
        settings.enabled = true;
        var normal = SceneManager.CreateScene("DemoPolicyRegression");
        Check(!DevlogDemoSceneSettings.SuppressesAutomaticSaves(normal), "Normal scenes unchanged");
        SceneManager.UnloadSceneAsync(normal);
    }
}
