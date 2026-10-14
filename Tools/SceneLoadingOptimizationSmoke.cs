using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SceneLoadingOptimizationSmoke
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
    const string A = "TurretTravelA", B = "TurretTravelB", Folder = "Assets/TurretTravelTest/";
    static IEnumerator suite; static int frame = -1, checks; static double deadline;
    static string batteryState;
    [Serializable] class Envelope { public string payload, checksum; }
    [Serializable] class Simple { public string path; public bool active; public Vector3 position, scale; public Quaternion rotation; }
    [Serializable] class World { public List<SceneRecord> scenes; }
    [Serializable] class SceneRecord { public string name; public List<Simple> transforms; }
    static SceneLoadingOptimizationSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception("Loading regression: " + message); }
    static object Call(object owner, string name, params object[] args) => owner.GetType().GetMethod(name, F).Invoke(owner, args);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"), "isolated only");
        PlayerSettings.productName = "scene-loading-optimization-smoke";
        Directory.CreateDirectory(Folder);
        typeof(TurretTravelSmoke).GetMethod("BuildScene", F).Invoke(null, new object[] { A, true });
        typeof(TurretTravelSmoke).GetMethod("BuildScene", F).Invoke(null, new object[] { B, false });
        var scene = EditorSceneManager.OpenScene(Folder + A + ".unity");
        var root = new GameObject("Save Transform Parent");
        var wall = new GameObject("Saved Static Block", typeof(BoxCollider2D));
        wall.layer = LayerMask.NameToLayer("Blocks"); wall.transform.SetParent(root.transform);
        wall.transform.position = new Vector3(15, 10);
        var socket = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/BatterySocket.prefab"));
        socket.name = "Saved Battery"; socket.transform.position = new Vector3(20, 10);
        EditorSceneManager.SaveScene(scene);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(Folder + A + ".unity", true), new EditorBuildSettingsScene(Folder + B + ".unity", true) };
        SessionState.SetBool(nameof(SceneLoadingOptimizationSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SceneLoadingOptimizationSmoke), false))
            { SessionState.SetBool(nameof(SceneLoadingOptimizationSmoke), false); deadline = EditorApplication.timeSinceStartup + 160; suite = Verify(); }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Loading test timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("SCENE_LOADING_OPTIMIZATION_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Ready()
    {
        do { yield return null; } while (RetroSceneLoadReveal.IsBlockingInput || GameSaveSystem.IsLoading || GameSaveSystem.IsPreparingSceneSave ||
            SceneTravelStateManager.GetOrCreate().IsQuickRestartPending || SceneTravelStateManager.GetOrCreate().IsSceneTravelRestoreInProgress);
    }
    static Transform Wall() => Object.FindObjectsOfType<BoxCollider2D>(true).First(c => c.name == "Saved Static Block").transform;
    static BatterySocket Socket() => Object.FindObjectsOfType<BatterySocket>(true).First(c => c.name == "Saved Battery");
    static void AssertState()
    {
        var wall = Wall();
        Check(!wall.gameObject.activeSelf && wall.localPosition == new Vector3(18, 12, 0), "inactive static position roundtrip");
        Check(wall.localScale == new Vector3(2, 3, 1) && Quaternion.Angle(wall.localRotation, Quaternion.Euler(0, 0, 31)) < .01f, "static rotation / scale roundtrip");
        Check(JsonUtility.ToJson(Socket().CaptureRuntimeState()) == batteryState, "installed battery identity / charge roundtrip");
    }
    static void WriteSlot(int slot, GameSaveSystem.SaveData data, bool valid = true)
    {
        string payload = JsonUtility.ToJson(data), checksum;
        using (var sha = SHA256.Create()) checksum = Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        File.WriteAllText(Path.Combine(GameSaveSystem.SaveDirectory, "manual-" + (slot - 1) + ".json"),
            JsonUtility.ToJson(new Envelope { payload = payload, checksum = valid ? checksum : "bad" }));
    }
    static void CheckTemplates()
    {
        string text = "%YAML 1.1\n# 独立存档回归 " + Guid.NewGuid();
        var old = new SaveGameCatalog.SceneTemplate { name = "test", path = "test.unity", authoredState = text };
        var link = SaveSceneTemplateStore.Reference(old);
        Check(string.IsNullOrEmpty(link.authoredState) && string.IsNullOrEmpty(link.authoredStateBase64), "reference omits duplicated source");
        Check(link.CreateSaveSnapshot().authoredState == text, "compressed unicode baseline is lossless");
        string path = Path.Combine(SaveSceneTemplateStore.Folder, link.templateKey + ".gz");
        var stamp = File.GetLastWriteTimeUtc(path);
        Check(SaveSceneTemplateStore.Reference(SaveGameCatalog.SceneTemplate.FromSceneText("test", "test.unity", text)).templateKey == link.templateKey &&
            File.GetLastWriteTimeUtc(path) == stamp, "raw and base64 baselines deduplicate without rewriting");
        Check(!SaveSceneTemplateStore.Validate("../auto.json"), "path traversal rejected");
        File.WriteAllText(path, "corrupt test fixture");
        Check(!SaveSceneTemplateStore.Validate(link.templateKey), "corrupt baseline rejected");
        SaveSceneTemplateStore.Reference(old);
        Check(SaveSceneTemplateStore.Validate(link.templateKey), "known authored baseline repairs corrupt copy");
        File.Delete(path);
        Check(!SaveSceneTemplateStore.Validate(link.templateKey), "missing baseline rejected");
    }
    static IEnumerator Verify()
    {
        var ready = Ready(); while (ready.MoveNext()) yield return null;
        CheckTemplates();
        var ward = Object.FindObjectsOfType<ZeldaFourWayMover>().Single(m => m.GetComponent<TurretZeldaCharacterData>() == null);
        ward.ReceiveControl(Vector2.up); ZeldaRuntimeRegistry.ClaimControlledMover(ward);
        var wall = Wall(); wall.localPosition = new Vector3(18, 12); wall.localScale = new Vector3(2, 3, 1);
        wall.localRotation = Quaternion.Euler(0,0,31); wall.gameObject.SetActive(false);
        Socket().ApplyRuntimeState(new BatterySocket.RuntimeState { installed = true });
        Socket().TryConsumeCharge(); batteryState = JsonUtility.ToJson(Socket().CaptureRuntimeState());
        var saves = GameSaveSystem.Instance;
        Check(saves.Save(1, false), "v2 save writes");
        var data = saves.Read(1);
        Check(data != null && data.version == 2, "v2 save reads");
        var world = JsonUtility.FromJson<World>(data.world);
        Check(world.scenes.Find(s => s.name == A).transforms.Any(t => t.path.Contains("Saved Static Block")), "pure collider uses compact record");
        Check(data.visitedLevels.All(v => v.initialScenes.All(s => !string.IsNullOrEmpty(s.templateKey))), "visits reference immutable baselines");
        wall.localPosition = Vector3.zero; wall.gameObject.SetActive(true); Socket().ApplyRuntimeState(default);
        Check(saves.Load(1), "async disk load starts");
        ready = Ready(); while (ready.MoveNext()) yield return null;
        AssertState(); Check(Time.timeScale == 1f, "async load resumes simulation clock");
        var manager = SceneTravelStateManager.GetOrCreate();
        manager.CaptureQuickRestartCheckpoint(SceneManager.GetActiveScene());
        Wall().gameObject.SetActive(true); Wall().localPosition = Vector3.zero; Socket().ApplyRuntimeState(default);
        Check(manager.RestartFromQuickCheckpoint(), "async checkpoint restart starts");
        ready = Ready(); while (ready.MoveNext()) yield return null;
        AssertState(); Check(manager.LastQuickRestartRestoreSucceeded, "checkpoint restoration succeeds");
        // Build a genuine legacy world: move compact records back to old objects.
        string legacy = data.world;
        foreach (var scene in world.scenes)
        {
            // Work per-scene through the internal DTO to preserve ALL dynamic fields.
            var worldType = typeof(SceneTravelStateManager).GetNestedType("DiskWorld", F);
            var oldWorld = JsonUtility.FromJson(legacy, worldType);
            foreach (object record in (IEnumerable)worldType.GetField("scenes").GetValue(oldWorld))
            {
                if ((string)record.GetType().GetField("name").GetValue(record) != scene.name) continue;
                var objects = (IList)record.GetType().GetField("objects").GetValue(record);
                var stateType = typeof(SceneTravelStateManager).GetNestedType("ObjectState", F);
                foreach (var t in scene.transforms)
                {
                    var state = Activator.CreateInstance(stateType);
                    stateType.GetField("path").SetValue(state, t.path); stateType.GetField("activeSelf").SetValue(state, t.active);
                    stateType.GetField("localPosition").SetValue(state, t.position); stateType.GetField("localRotation").SetValue(state, t.rotation);
                    stateType.GetField("localScale").SetValue(state, t.scale); objects.Add(state);
                }
                record.GetType().GetField("transforms").SetValue(record, null);
            }
            legacy = JsonUtility.ToJson(oldWorld);
        }
        legacy = legacy.Replace(",\"transforms\":[]", "").Replace(",\"transforms\":null", "");
        data.version = 1; data.world = legacy;
        foreach (var visit in data.visitedLevels)
        { visit.initialWorld = legacy; visit.initialScenes = visit.initialScenes.Select(s => s.CreateSaveSnapshot()).ToList(); }
        WriteSlot(2, data); Check(saves.Read(2)?.version == 1, "legacy inline save reads");
        Check(saves.Load(2), "legacy disk load starts"); ready = Ready(); while (ready.MoveNext()) yield return null;
        AssertState();
        var upgraded = saves.Read(0);
        Check(upgraded.version == 2 && upgraded.visitedLevels.All(v => v.initialWorld.Contains("\"transforms\":")), "legacy checkpoints migrate once");
        Check(saves.Read(2).version == 1, "source legacy slot left unchanged");
        WriteSlot(3, data, false); Check(saves.Read(3) == null, "checksum corruption rejected");
        data.visitedLevels[0].initialScenes[0].templateKey = new string('0', 64);
        WriteSlot(3, data); Check(saves.Read(3) == null, "missing referenced baseline rejects load");
        // Runtime components must invalidate the simple-record shortcut.
        var probe = new GameObject("Added Component Probe", typeof(BoxCollider2D));
        SceneTravelStateManager.Instance.CaptureDiskWorld(); probe.AddComponent<DoorData>();
        var capture = SceneTravelStateManager.Instance.CaptureDiskWorld();
        Check(capture.Contains("\"doorDataFields\":["), "component index refreshes added gameplay scripts"); Object.DestroyImmediate(probe);
        var map1 = new GameObject("Map1", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
        var map2 = new GameObject("Map2", typeof(RectTransform), typeof(CanvasRenderer)).AddComponent<RuntimeMiniMapGraphic>();
        map1.RebuildFromScene(SceneManager.GetActiveScene()); map2.RebuildFromScene(SceneManager.GetActiveScene());
        var paths1 = (List<Vector2[]>)typeof(RuntimeMiniMapGraphic).GetField("blockPaths", F).GetValue(map1);
        var paths2 = (List<Vector2[]>)typeof(RuntimeMiniMapGraphic).GetField("blockPaths", F).GetValue(map2);
        Check(paths1.Count > 0 && paths1.Count == paths2.Count && ReferenceEquals(paths1[0], paths2[0]), "HUD and menu reuse geometry arrays");
        Object.DestroyImmediate(map1.gameObject); Object.DestroyImmediate(map2.gameObject);
        Check(RetroSceneLoadReveal.BeginTransition(A, () => { }), "empty load request accepted for failure recovery test");
        ready = Ready(); while (ready.MoveNext()) yield return null;
        Check(!RetroSceneLoadReveal.IsBlockingInput && Time.timeScale == 1f, "missing load operation uncovers old scene");
        var turret = (IEnumerator)typeof(TurretTravelSmoke).GetMethod("Verify", F).Invoke(null, null);
        while (turret.MoveNext()) yield return turret.Current;
        AssertState();
        Debug.Log("TURRET_TRAVEL_REGRESSION checks=" + typeof(TurretTravelSmoke).GetField("checks", F).GetValue(null));
    }
}
