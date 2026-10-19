using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SceneStartupStreamingSmoke
{
    const BindingFlags F = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static IEnumerator suite; static int frame = -1, checks; static double deadline;
    static SceneStartupStreamingSmoke() { EditorApplication.update += Tick; }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static FieldInfo Field(object obj, string name)
    {
        for (var type = obj.GetType(); type != null; type = type.BaseType)
        { var field = type.GetField(name, F); if (field != null) return field; }
        throw new Exception(name);
    }
    static void Set(object obj, string name, object value) => Field(obj, name).SetValue(obj, value);
    static T Get<T>(object obj, string name) => (T)Field(obj, name).GetValue(obj);
    static void Preparing(bool value) => typeof(RetroSceneLoadReveal).GetProperty("IsPreparingGameplay", F).SetValue(null, value);
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated only");
        PlayerSettings.productName = "scene-startup-streaming-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(SceneStartupStreamingSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SceneStartupStreamingSmoke), false))
            { SessionState.SetBool(nameof(SceneStartupStreamingSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Startup smoke timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("SCENE_STARTUP_STREAMING_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception ex) { suite = null; Preparing(false); Debug.LogException(ex); EditorApplication.Exit(1); }
    }
    static ZeldaPatrolCharacterAi Actor(string name, Vector2 position)
    {
        var go = new GameObject(name); go.SetActive(false); go.transform.position = position;
        go.AddComponent<CivilianZeldaCharacterData>(); go.AddComponent<ZeldaFourWayMover>().enabled = false;
        var ai = go.AddComponent<ZeldaPatrolCharacterAi>(); Set(ai, "enableAwarenessSound", false);
        go.SetActive(true); return ai;
    }
    static GameObject Block(string name, Vector2 position)
    { var go = new GameObject(name, typeof(BoxCollider2D)); go.transform.position = position; return go; }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput) yield return null;
        Preparing(true);
        var near = Actor("Near NPC", new Vector2(1, 0));
        var far = Actor("Far NPC", new Vector2(100, 0));
        var coneVisible = Actor("Offscreen NPC with visible cone", new Vector2(9, 0));
        Set(coneVisible, "visionRadius", 7f); Set(coneVisible, "facingDirection", Vector2.left);
        Check(Get<Mesh>(near, "visionMesh") == null && Get<Mesh>(far, "visionMesh") == null, "load-time NPC meshes deferred");
        Set(near, "waitTimer", 5f);
        for (int i = 0; i < 5; i++) yield return null;
        Check(Get<float>(near, "waitTimer") == 5f && near.CurrentState == ZeldaAiState.Idle, "NPC patrol and perception paused during preparation");
        var boxes = new List<GameObject>();
        for (int i = 0; i < 640; i++) boxes.Add(Block("Far Block " + i, new Vector2(50 + i % 32, 30 + i / 32)));
        var close = Block("Near collision", Vector2.zero);
        var hidden = Block("Puzzle intentionally hidden", Vector2.one); hidden.SetActive(false);
        var exempt = Block("Always loaded", new Vector2(200, 0)); exempt.AddComponent<CameraVisionStreamingExempt>();
        var groupNear = Actor("Group visible member", new Vector2(-1, 0));
        var groupFar = Actor("Group distant member", new Vector2(70, 0));
        var group = new GameObject("Patrol loading group").AddComponent<PatrolStreamingGroup>();
        Set(group, "members", new[] { groupNear, groupFar }); group.RefreshMembers();
        var sleepingA = Actor("Sleeping group A", new Vector2(170, 0));
        var sleepingB = Actor("Sleeping group B", new Vector2(180, 0));
        var sleepingGroup = new GameObject("Distant loading group").AddComponent<PatrolStreamingGroup>();
        Set(sleepingGroup, "members", new[] { sleepingA, sleepingB }); sleepingGroup.RefreshMembers();
        var crystal = Block("Crystal dependency", new Vector2(220, 0)).AddComponent<EnergyCrystal>();
        var bone = new GameObject("Bound skeleton"); bone.SetActive(false); bone.transform.position = new Vector2(240, 0);
        var skeleton = bone.AddComponent<SkeletonZeldaCharacterData>(); bone.AddComponent<ZeldaFourWayMover>().enabled = false;
        bone.AddComponent<SkeletonCharacterAi>(); bone.SetActive(true); crystal.BindSkeleton(skeleton);
        var cameraObject = new GameObject("Startup camera"); cameraObject.transform.position = new Vector3(0, 0, -10);
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 3; camera.aspect = 1;
        cameraObject.AddComponent<CameraCircularVision>().enabled = false;
        var manager = cameraObject.AddComponent<CameraVisionObjectStreaming>();
        Set(manager, "maximumSuspensionsPerFrame", 1); Set(manager, "discoveryTransformsPerFrame", 1);
        Set(manager, "unloadDelay", 20f); // Startup must not inherit gameplay grace/budgets.
        Physics2D.SyncTransforms();
        var prepare = manager.PrepareInitialView(); int preparationFrames = 0;
        while (prepare.MoveNext()) { preparationFrames++; yield return null; }
        Check(manager.IsInitialViewPrepared, "first view explicitly reports readiness");
        Check(boxes.All(b => !b.activeSelf), "all 640 distant objects asleep before reveal despite normal one-object budget");
        Check(preparationFrames < 640, "startup uses elapsed-time budget instead of normal one-object limit");
        Check(close.activeSelf && near.gameObject.activeSelf && exempt.activeSelf, "near collision, NPC and exemptions preserved");
        Check(coneVisible.gameObject.activeSelf && coneVisible.HasPreparedVision, "offscreen NPC whose cone intersects viewport remains loaded and visible");
        Check(!hidden.activeSelf && !manager.IsSuspendedByStreaming(hidden), "intentional disable never owned or restored by streaming");
        Check(groupFar.gameObject.activeSelf && !group.PatrolPaused, "visible group wakes all members together");
        Check(!sleepingA.gameObject.activeSelf && !sleepingB.gameObject.activeSelf && sleepingGroup.PatrolPaused, "offscreen group sleeps without twenty-second grace");
        Check(!bone.activeSelf && !crystal.gameObject.activeSelf, "second pass settles skeleton/crystal dependency");
        Check(Get<Mesh>(near, "visionMesh") != null && Get<Mesh>(far, "visionMesh") == null, "only visible NPC cones warmed before reveal");
        Check(Get<int>(manager, "maximumSuspensionsPerFrame") == 1 && Get<int>(manager, "discoveryTransformsPerFrame") == 1 && Get<float>(manager, "unloadDelay") == 20f, "normal budgets unchanged");
        Set(far, "warningTimer", 7f);
        for (int i = 0; i < 4; i++) manager.RefreshSceneCameraSettings();
        Check(!far.gameObject.activeSelf && Get<float>(far, "warningTimer") == 7f && manager.IsSuspendedByStreaming(far.gameObject), "repeated camera refresh does not wake/reset sleeping NPC or discard ownership");
        bone.SetActive(true);
        Check(crystal.gameObject.activeSelf, "waking bound skeleton wakes crystal after initial pruning");
        Preparing(false);
        cameraObject.transform.position = new Vector3(100, 0, -10);
        for (int i = 0; i < 4; i++) yield return null;
        Check(far.gameObject.activeSelf && Get<Mesh>(far, "visionMesh") != null, "normal movement wakes NPC and lazily builds its cone");
        Check(!hidden.activeSelf, "manual hidden state survives normal streaming");
        manager.enabled = false;
        Check(boxes.All(b => b.activeSelf) && !hidden.activeSelf, "manually disabling streamer still restores only its own suspensions");
        Debug.Log("STARTUP_FIXTURE frames=" + preparationFrames + " managed=" + Get<ICollection>(manager, "managedObjects").Count);
    }
}
