using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PatrolRouteStreamingSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly List<GameObject> objects = new List<GameObject>();
    static IEnumerator suite;
    static int frame = -1, checks;
    static PatrolRouteStreamingSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(PatrolRouteStreamingSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(PatrolRouteStreamingSmoke), false))
            { SessionState.SetBool(nameof(PatrolRouteStreamingSmoke), false); Time.captureFramerate = 0; suite = Verify(); }
            if (suite == null || suite.MoveNext()) return;
            suite = null; Clear(); Time.captureFramerate = 0;
            Debug.Log("PATROL_ROUTE_STREAMING_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); Clear(); EditorApplication.Exit(1); }
    }
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static FieldInfo Field(object target, string name)
    {
        for (Type t = target.GetType(); t != null; t = t.BaseType)
        { var field = t.GetField(name, F); if (field != null) return field; }
        throw new Exception("Missing " + name);
    }
    static void Set(object o, string key, object value) => Field(o, key).SetValue(o, value);
    static T Get<T>(object o, string key) => (T)Field(o, key).GetValue(o);
    static GameObject Own(string name) { var go = new GameObject(name); objects.Add(go); return go; }
    static void Clear() { for (int i = objects.Count - 1; i >= 0; i--) if (objects[i]) Object.DestroyImmediate(objects[i]); objects.Clear(); }
    static ZeldaPatrolCharacterAi Make(int index)
    {
        var go = Own("Independent Patrol " + index); go.SetActive(false); go.transform.position = new Vector2(index * 8, 0);
        go.AddComponent<CivilianZeldaCharacterData>();
        var mover = go.AddComponent<ZeldaFourWayMover>(); mover.enabled = false;
        var ai = go.AddComponent<ZeldaPatrolCharacterAi>();
        go.GetComponent<SpriteRenderer>().sprite = Sprite.Create(Texture2D.whiteTexture,
            new Rect(0, 0, Texture2D.whiteTexture.width, Texture2D.whiteTexture.height), Vector2.one * .5f, 4f);
        Set(ai, "enableAwarenessSound", false);
        Set(ai, "patrolPoint2Offset", new Vector2(4, 0));
        return ai;
    }
    static int ActiveCount(ZeldaPatrolCharacterAi[] members)
    { int count = 0; foreach (var ai in members) if (ai.gameObject.activeInHierarchy) count++; return count; }
    static float[] Timers(ZeldaPatrolCharacterAi[] members)
    { var values = new float[members.Length]; for (int i = 0; i < values.Length; i++) values[i] = Get<float>(members[i], "waitTimer"); return values; }
    static void CheckTimers(ZeldaPatrolCharacterAi[] members, float[] values, string label)
    { for (int i = 0; i < members.Length; i++) Check(Mathf.Abs(values[i] - Get<float>(members[i], "waitTimer")) < .0001f, label + " member=" + i); }
    static IEnumerator Wait(float seconds) { float end = Time.time + seconds; while (Time.time < end) yield return null; }
    static IEnumerator Until(Func<bool> predicate, string label)
    { float end = Time.time + 3; while (!predicate() && Time.time < end) yield return null; Check(predicate(), label); }
    static void Custom(ZeldaPatrolCharacterAi ai, params Vector2[] worldPoints)
    {
        Vector2 origin = Get<Vector2>(ai, "initialScenePosition");
        var points = new ZeldaPatrolCharacterAi.PatrolWaypoint[worldPoints.Length];
        for (int i = 0; i < points.Length; i++)
            points[i] = new ZeldaPatrolCharacterAi.PatrolWaypoint { offset = worldPoints[i] - origin, waitDuration = 20 };
        Set(ai, "useCustomPatrolRoute", true); Set(ai, "customPatrolPoints", points);
        typeof(ZeldaPatrolCharacterAi).GetMethod("RebuildPatrolPoints", F).Invoke(ai, null);
        Set(ai, "currentPatrolPointIndex", 0); Set(ai, "waitTimer", 20f);
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 1;
        var members = new ZeldaPatrolCharacterAi[6];
        for (int i = 0; i < members.Length; i++) members[i] = Make(i + 30);
        Set(members[0], "patrolPoint2Offset", new Vector2(-480, 0));
        var groupObject = Own("Route loading group"); groupObject.SetActive(false);
        var group = groupObject.AddComponent<PatrolStreamingGroup>(); Set(group, "members", members); groupObject.SetActive(true);
        foreach (var ai in members) { ai.gameObject.SetActive(true); Set(ai, "waitTimer", 100f); }
        var cameraObject = Own("Route camera"); cameraObject.SetActive(false); cameraObject.transform.position = new Vector3(1000, 0, -10);
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 2; camera.aspect = 1;
        cameraObject.AddComponent<CameraCircularVision>().enabled = false;
        var manager = cameraObject.AddComponent<CameraVisionObjectStreaming>();
        Set(manager, "visibilityCheckInterval", .02f); Set(manager, "unloadDelay", .06f);
        Set(manager, "sceneRescanInterval", .1f); Set(manager, "discoveryTransformsPerFrame", 10000);
        Set(manager, "visibilityChecksPerFrame", 10000); Set(manager, "maximumActivationsPerFrame", 1);
        Set(manager, "maximumSuspensionsPerFrame", 1); Set(manager, "activationPreloadPadding", .5f); Set(manager, "viewportPreloadFraction", 0f);
        cameraObject.SetActive(true);
        var wait = Until(() => ActiveCount(members) == 0, "far group unloads"); while (wait.MoveNext()) yield return null;
        float[] stopped = Timers(members);
        cameraObject.transform.position = new Vector3(0, 0, -10);
        wait = Until(() => ActiveCount(members) == 6 && !group.PatrolPaused, "legacy connecting segment wakes entire group with both endpoints outside");
        while (wait.MoveNext())
        {
            if (group.PatrolPaused) CheckTimers(members, stopped, "budgeted route wake preserves timing");
            yield return null;
        }
        foreach (var ai in members) Check(Mathf.Abs(ai.transform.position.x) > 100, "not accidentally loaded by member body");
        cameraObject.transform.position = new Vector3(0, 8, -10);
        wait = Until(() => ActiveCount(members) == 0, "leaving route unloads again"); while (wait.MoveNext()) yield return null;
        Custom(members[0], new Vector2(-20,-20), new Vector2(20,-20), new Vector2(20,20), new Vector2(-20,20));
        cameraObject.transform.position = new Vector3(0, 0, -10);
        wait = Wait(.3f); while (wait.MoveNext()) yield return null;
        Check(ActiveCount(members) == 0, "empty interior of route AABB does not wake group");
        cameraObject.transform.position = new Vector3(-20, 0, -10);
        wait = Until(() => ActiveCount(members) == 6 && !group.PatrolPaused, "closing last-to-first custom leg wakes group"); while (wait.MoveNext()) yield return null;
        members[5].gameObject.SetActive(false);
        cameraObject.transform.position = new Vector3(0, 0, -10);
        wait = Until(() => ActiveCount(members) == 0, "custom loop leaves range"); while (wait.MoveNext()) yield return null;
        Custom(members[0], Vector2.zero);
        wait = Until(() => ActiveCount(members) == 5 && !group.PatrolPaused, "one point route wakes non-disabled members"); while (wait.MoveNext()) yield return null;
        Check(!members[5].gameObject.activeSelf, "manual disable not overridden");
        Custom(members[0]);
        wait = Until(() => ActiveCount(members) == 0, "empty route does not retain distant bodies"); while (wait.MoveNext()) yield return null;
        Custom(members[0], new Vector2(-20,0), new Vector2(20,0));
        cameraObject.transform.position = new Vector3(1000, 0, -10);
        var region = Own("Remote loading region").AddComponent<CameraVisionStreamingRegion>(); region.Configure(3);
        wait = Until(() => ActiveCount(members) == 5 && !group.PatrolPaused, "remote circular retention region intersects route"); while (wait.MoveNext()) yield return null;
        region.enabled = false;
        wait = Until(() => ActiveCount(members) == 0, "removed retention region permits unload"); while (wait.MoveNext()) yield return null;
        // Exact clipping rejects a diagonal that misses the view even when its AABB overlaps.
        var planes = GeometryUtility.CalculateFrustumPlanes(camera);
        var intersects = typeof(CameraVisionObjectStreaming).GetMethod("IntersectsStreamingSegment", BindingFlags.Static | BindingFlags.NonPublic);
        Check(!(bool)intersects.Invoke(null, new object[] { new Vector3(990,0,0), new Vector3(1000,10,0), planes, .5f }), "diagonal AABB false positive rejected");
        Check((bool)intersects.Invoke(null, new object[] { new Vector3(990,0,0), new Vector3(1010,0,0), planes, .5f }), "segment crossing viewport detected");
        Clear(); yield return null;
        // Re-run existing pause/barrier/ungroup and flexible route regressions unchanged.
        var regression = (IEnumerator)typeof(PatrolStreamingGroupSmoke).GetMethod("Verify", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        while (regression.MoveNext()) yield return regression.Current;
    }
}
