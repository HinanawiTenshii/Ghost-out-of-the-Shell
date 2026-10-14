using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class PatrolStreamingGroupSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly List<GameObject> objects = new List<GameObject>();
    static IEnumerator suite;
    static int frame = -1, checks;
    static PatrolStreamingGroupSmoke() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(PatrolStreamingGroupSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(PatrolStreamingGroupSmoke), false))
            { SessionState.SetBool(nameof(PatrolStreamingGroupSmoke), false); Time.captureFramerate = 0; suite = Verify(); }
            if (suite == null || suite.MoveNext()) return;
            suite = null; Clear(); Time.captureFramerate = 0;
            Debug.Log("PATROL_STREAMING_GROUP_PASS checks=" + checks); EditorApplication.Exit(0);
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
    static IEnumerator Verify()
    {
        Time.timeScale = 1;
        var members = new ZeldaPatrolCharacterAi[6];
        for (int i = 0; i < members.Length; i++) members[i] = Make(i);
        var groupObject = Own("Patrol streaming group"); groupObject.SetActive(false);
        var group = groupObject.AddComponent<PatrolStreamingGroup>(); Set(group, "members", members); groupObject.SetActive(true);
        foreach (var ai in members) { ai.gameObject.SetActive(true); Set(ai, "waitTimer", 20f); }
        var unrelated = Make(20); unrelated.gameObject.SetActive(true); Set(unrelated, "waitTimer", 20f);

        var cameraObject = Own("Streaming test camera"); cameraObject.SetActive(false); cameraObject.transform.position = new Vector3(0, 0, -10);
        var camera = cameraObject.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 2; camera.aspect = 1;
        cameraObject.AddComponent<CameraCircularVision>().enabled = false;
        var manager = cameraObject.AddComponent<CameraVisionObjectStreaming>();
        Set(manager, "visibilityCheckInterval", .02f); Set(manager, "unloadDelay", .06f);
        Set(manager, "sceneRescanInterval", .1f); Set(manager, "discoveryTransformsPerFrame", 10000);
        Set(manager, "visibilityChecksPerFrame", 10000); Set(manager, "maximumActivationsPerFrame", 1);
        Set(manager, "maximumSuspensionsPerFrame", 1); Set(manager, "activationPreloadPadding", .5f); Set(manager, "viewportPreloadFraction", 0f);
        cameraObject.SetActive(true);
        var wait = Wait(.3f); while (wait.MoveNext()) yield return null;
        Check(ActiveCount(members) == 6 && !group.PatrolPaused, "one visible member retains all independent patrols");
        Check(!unrelated.gameObject.activeSelf, "unbound patrol retains ordinary distance loading: managed=" +
            Get<IList>(manager, "managedObjects").Count + " enabled=" + manager.StreamingEnabled + " retained=" + unrelated.RequiresVisionStreamingRetention +
            " bounds=" + Get<Bounds>(Get<IDictionary>(manager, "managedLookup")[unrelated.gameObject], "worldBounds") +
            " camera=" + camera.transform.position + " size=" + camera.orthographicSize + " planes=" + Get<bool>(manager, "hasStreamingPlanes") +
            " outside=" + Get<float>(Get<IDictionary>(manager, "managedLookup")[unrelated.gameObject], "outsideTimer") +
            " inside=" + typeof(CameraVisionObjectStreaming).GetMethod("IsInsideStreamingRetentionRange", F).Invoke(manager, new[] { Get<IDictionary>(manager, "managedLookup")[unrelated.gameObject] }));
        float[] initial = Timers(members);
        for (int i = 1; i < initial.Length; i++) Check(Mathf.Abs(initial[i] - initial[0]) < .0001f, "initial clocks remain synchronized");

        for (int cycle = 0; cycle < 3; cycle++)
        {
            cameraObject.transform.position = new Vector3(1000, 0, -10);
            wait = Until(() => group.PatrolPaused, "whole group freezes before unloading"); while (wait.MoveNext()) yield return null;
            float[] stopped = Timers(members);
            Vector3[] positions = new Vector3[6]; for (int i = 0; i < 6; i++) positions[i] = members[i].transform.position;
            float end = Time.time + .3f;
            bool staggeredUnload = false;
            while (Time.time < end)
            {
                int count = ActiveCount(members); staggeredUnload |= count > 0 && count < 6;
                CheckTimers(members, stopped, "no late unload advances a patrol clock");
                for (int i = 0; i < 6; i++) Check(Vector3.Distance(positions[i], members[i].transform.position) < .001f, "no frozen physics movement");
                yield return null;
            }
            Check(staggeredUnload && ActiveCount(members) == 0, "suspension remains budgeted");
            cameraObject.transform.position = new Vector3(0, 0, -10);
            end = Time.time + 2; bool staggeredLoad = false;
            do
            {
                yield return null;
                int count = ActiveCount(members); staggeredLoad |= count > 0 && count < 6;
                if (count < 6) Check(group.PatrolPaused, "early arrivals wait for the last member");
                if (group.PatrolPaused) CheckTimers(members, stopped, "loading does not advance independent waits");
            } while (group.PatrolPaused && Time.time < end);
            Check(!group.PatrolPaused && ActiveCount(members) == 6 && staggeredLoad, "budgeted activation then shared release");
            wait = Wait(.12f); while (wait.MoveNext()) yield return null;
            float elapsed = stopped[0] - Get<float>(members[0], "waitTimer");
            for (int i = 0; i < 6; i++) Check(Mathf.Abs(stopped[i] - Get<float>(members[i], "waitTimer") - elapsed) < .0001f, "no loading-order drift after resume");
        }

        // Moving bodies must freeze too, not only patrol stop timers.
        foreach (var ai in members) Set(ai, "waitTimer", 0f);
        Vector3 start = members[0].transform.position;
        wait = Wait(.3f); while (wait.MoveNext()) yield return null;
        Check(Vector3.Distance(start, members[0].transform.position) > .05f, "fixture is actively walking");
        cameraObject.transform.position = new Vector3(1000, 0, -10);
        wait = Until(() => group.PatrolPaused, "moving group freezes"); while (wait.MoveNext()) yield return null;
        var frozenPositions = new Vector3[6];
        var frozenIndices = new int[6];
        for (int i = 0; i < 6; i++) { frozenPositions[i] = members[i].GetComponent<Rigidbody2D>().position; frozenIndices[i] = Get<int>(members[i], "currentPatrolPointIndex"); }
        float freezeEnd = Time.time + .3f;
        while (Time.time < freezeEnd)
        {
            for (int i = 0; i < 6; i++)
            {
                Check(Vector3.Distance(frozenPositions[i], members[i].GetComponent<Rigidbody2D>().position) < .001f, "walking body cannot drift while group is frozen: " + i + " before=" + frozenPositions[i] + " after=" + members[i].GetComponent<Rigidbody2D>().position);
                Check(Get<int>(members[i], "currentPatrolPointIndex") == frozenIndices[i], "unloading preserves each independent waypoint index");
            }
            yield return null;
        }
        cameraObject.transform.position = new Vector3(0, 0, -10);
        wait = Until(() => !group.PatrolPaused && ActiveCount(members) == 6, "moving group resumes"); while (wait.MoveNext()) yield return null;

        // Gameplay-disabled actors must neither wake nor block the barrier.
        members[5].gameObject.SetActive(false);
        cameraObject.transform.position = new Vector3(1000, 0, -10);
        wait = Wait(.3f); while (wait.MoveNext()) yield return null;
        cameraObject.transform.position = new Vector3(0, 0, -10);
        wait = Until(() => !group.PatrolPaused && ActiveCount(members) == 5, "manual disable is excluded from readiness"); while (wait.MoveNext()) yield return null;
        Check(!members[5].gameObject.activeSelf, "manual disable is never overridden");

        cameraObject.transform.position = new Vector3(1000, 0, -10);
        wait = Until(() => group.PatrolPaused, "group pauses again"); while (wait.MoveNext()) yield return null;
        // A frozen group never suppresses another state (nor alarm retention).
        Set(members[0], "currentState", ZeldaAiState.Hostile);
        var gate = typeof(ZeldaPatrolCharacterAi).GetProperty("StreamingPatrolPaused", F);
        Check(!(bool)gate.GetValue(members[0]), "hostile AI bypasses patrol freeze");
        Set(members[0], "currentState", ZeldaAiState.Search);
        Check(!(bool)gate.GetValue(members[0]), "investigation/search bypasses patrol freeze");
        Set(members[0], "currentState", ZeldaAiState.Idle);
        Set(members[0], "alarmResponseActive", true);
        Check(!(bool)gate.GetValue(members[0]), "alarm retention bypasses patrol freeze");
        Set(members[0], "alarmResponseActive", false);
        manager.enabled = false;
        Check(!group.PatrolPaused && ActiveCount(members) == 5, "disabling streaming restores owned suspensions and releases the group");
        Check(!members[5].gameObject.activeSelf, "streaming shutdown preserves manual disable");
        manager.enabled = true;
        wait = Wait(.3f); while (wait.MoveNext()) yield return null;
        group.enabled = false;
        Check(!group.PatrolPaused, "removing group never leaves a stale pause");
        cameraObject.transform.position = new Vector3(0, 0, -10);
        wait = Wait(.3f); while (wait.MoveNext()) yield return null;
        Check(ActiveCount(members) == 1, "ungrouping restores independent streaming");
        Clear(); yield return null;
        Time.captureFramerate = 60;
        var regression = (IEnumerator)typeof(FlexiblePatrolSmoke).GetMethod("Verify", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        while (regression.MoveNext()) yield return regression.Current;
    }
}
