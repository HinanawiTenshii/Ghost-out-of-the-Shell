using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Object = UnityEngine.Object;

// Diagnostic only: opens a dependency-complete disposable scene copy and never saves it.
[InitializeOnLoad]
public static class MazePerformanceProfile
{
    [Serializable] public class Metric { public string name, unit; public double mean, p95, max, calls; }
    [Serializable] public class Actor { public string name, state, target; public Vector3 position; public bool active; public int gridWidth, gridHeight, pathLength, pathIndex; public Vector2 plannedTarget; public Vector2[] patrol; }
    [Serializable] public class Sample { public string name; public Vector3 camera; public float cameraSize, elapsed; public int allAi, activeAi, registeredMovers, activeMovers, activeSkeletons, activeRenderers, linkVertices; public Actor[] skeletons; public Metric[] metrics; }
    [Serializable] public class Report { public string gpu, cpu; public int missingScripts, totalSkeletons, bindings, totalObjects; public Vector3 crystalPosition; public Sample[] samples; public string[] logs; }
    class Counter { public string name, unit; public ProfilerRecorder recorder; public readonly List<double> values = new List<double>(); public long calls; }
    static readonly List<Counter> counters = new List<Counter>();
    static readonly List<Sample> samples = new List<Sample>();
    static readonly List<string> logs = new List<string>();
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static Report report = new Report();
    static int frame = -1;
    static double deadline;
    static bool renderFrames;
    static bool optimized;
    static SkeletonCharacterAi[] skeletons;
    static EnergyCrystal crystal;
    static ZeldaFourWayMover observer;
    static MazePerformanceProfile() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor-1.unity");
        SessionState.SetBool("MazePerformanceProfile", true);
        EditorApplication.EnterPlaymode();
    }
    static void Log(string message, string stack, LogType kind)
    { if ((kind == LogType.Error || kind == LogType.Exception || kind == LogType.Warning) && logs.Count < 40) logs.Add(kind + ": " + message); }
    static void Finish(int code)
    {
        foreach (var c in counters) c.recorder.Dispose(); counters.Clear();
        Profiler.enabled = false; report.samples = samples.ToArray(); report.logs = logs.ToArray();
        File.WriteAllText(Path.Combine(Application.dataPath, "../" + (optimized ? "Maze-performance-optimized.json" : renderFrames ? "Maze-performance-confirmation.json" : "Maze-performance.json")), JsonUtility.ToJson(report, true));
        Debug.Log("MAZE_PROFILE_COMPLETE samples=" + samples.Count + " exit=" + code);
        steps.Clear(); EditorApplication.Exit(code);
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("MazePerformanceProfile", false))
            {
                SessionState.SetBool("MazePerformanceProfile", false); Application.logMessageReceived += Log;
                optimized = Environment.GetCommandLineArgs().Contains("-optimizedMaze");
                renderFrames = optimized || Environment.GetCommandLineArgs().Contains("-confirmMaze");
                deadline = EditorApplication.timeSinceStartup + 240; steps.Push(renderFrames ? Confirm() : Measure());
            }
            if (steps.Count == 0) return;
            // Editor batch mode has no visible Game view. Explicit render makes visibility,
            // vision meshes, CRT and draw calls participate in the confirmation run.
            if (renderFrames && Camera.main != null) Camera.main.Render();
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Profile timeout");
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Finish(0);
        }
        catch (Exception e) { Debug.LogException(e); Finish(1); }
    }
    static FieldInfo Field(object owner, string name)
    {
        for (Type t = owner.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public); if (f != null) return f; }
        throw new Exception("Missing " + name);
    }
    static object Get(object owner, string name) => Field(owner, name).GetValue(owner);
    static void Set(object owner, string name, object value) => Field(owner, name).SetValue(owner, value);
    static IEnumerator Wait(float seconds)
    { double until = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < until) yield return null; }
    static void Markers()
    {
        var available = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(available);
        foreach (var h in available)
        {
            var d = ProfilerRecorderHandle.GetDescription(h);
            if (!(d.Name.StartsWith("Maze.") || d.Name.Contains(".Update") || d.Name.Contains(".LateUpdate") || d.Name.Contains(".FixedUpdate") || d.Name.Contains(".OnRenderImage") ||
                new[] { "Main Thread", "Render Thread", "BehaviourUpdate", "Physics2D.Simulate", "Camera.Render", "Canvas.BuildBatch", "GC.Collect", "Batches Count", "Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "GC Allocated In Frame", "Gfx.WaitForPresentOnGfxThread", "GPU Frame Time" }.Contains(d.Name))) continue;
            if (counters.Any(c => c.name == d.Name)) continue;
            var recorder = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            if (recorder.Valid) counters.Add(new Counter { name = d.Name, unit = d.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds ? "ms" : d.UnitType.ToString(), recorder = recorder });
        }
    }
    static void MoveObserver(Vector3 p)
    {
        observer.transform.position = p;
        observer.GetComponent<Rigidbody2D>().position = p;
        Physics2D.SyncTransforms();
    }
    static IEnumerator Measure()
    {
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; Time.captureFramerate = 0; Time.timeScale = 1;
        Profiler.enabled = true; report.gpu = SystemInfo.graphicsDeviceName; report.cpu = SystemInfo.processorType;
        if (!ActiveZeldaCharacterDataSource.Instance) new GameObject("Profile HUD source").AddComponent<ActiveZeldaCharacterDataSource>();
        if (Camera.main) Camera.main.aspect = 16f / 9f;
        yield return Wait(4);
        skeletons = Object.FindObjectsOfType<SkeletonCharacterAi>(true);
        crystal = Object.FindObjectOfType<EnergyCrystal>(true);
        report.totalSkeletons = skeletons.Length; report.bindings = crystal.Skeletons.Count; report.crystalPosition = crystal.transform.position;
        var components = Object.FindObjectsOfType<Transform>(true).Where(t => t.gameObject.scene.IsValid()).SelectMany(t => t.GetComponents<Component>()).ToArray();
        report.totalObjects = components.OfType<Transform>().Count(); report.missingScripts = components.Count(c => c == null);
        var go = new GameObject("Profile ghost observer"); go.AddComponent<GhostZeldaCharacterData>();
        observer = go.AddComponent<ZeldaFourWayMover>(); ZeldaRuntimeRegistry.ClaimControlledMover(observer);
        var origin = crystal.transform.position;
        MoveObserver(origin + new Vector3(27, -12)); yield return Wait(4); Markers();
        yield return Capture("Away from maze");
        MoveObserver(origin + new Vector3(12, -9)); yield return Wait(4); Markers();
        yield return Capture("Maze entrance");
        MoveObserver(origin + new Vector3(0, -8)); yield return Wait(5); Markers();
        yield return Capture("Inside maze baseline");

        // A/B tests change only the disposable scene. Keep states/visibility stable where possible.
        var visual = crystal.GetComponent<EnergyCrystalVisual>();
        visual.enabled = false;
        crystal.transform.Find(EnergyCrystalVisual.LinkMeshName).gameObject.SetActive(false);
        yield return Wait(1); yield return Capture("Inside maze - crystal visual off");
        visual.enabled = true;
        yield return Wait(2); yield return Capture("Inside maze - crystal restored");

        foreach (var ai in skeletons) Set(ai, "visionVisualRefreshInterval", 10000f);
        foreach (var ai in skeletons) Set(ai, "nextVisionVisualRefreshTime", Time.time + 10000f);
        yield return Wait(1); yield return Capture("Inside maze - skeleton vision mesh updates off");
        foreach (var ai in skeletons) { Set(ai, "visionVisualRefreshInterval", .08f); Set(ai, "nextVisionVisualRefreshTime", 0f); }

        foreach (var ai in skeletons) Set(ai, "enableIdlePatrol", false);
        yield return Wait(2); yield return Capture("Inside maze - skeleton idle patrol off");
        foreach (var ai in skeletons) Set(ai, "enableIdlePatrol", true);
        yield return Wait(3); yield return Capture("Inside maze - patrol restored");

        foreach (var ai in skeletons) ai.enabled = false;
        yield return Wait(1); yield return Capture("Inside maze - skeleton AI off");
        foreach (var ai in skeletons) ai.enabled = true;
        yield return Wait(3); yield return Capture("Inside maze - AI restored");
    }
    static IEnumerator Confirm()
    {
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; Time.timeScale = 1;
        Profiler.enabled = true; report.gpu = SystemInfo.graphicsDeviceName; report.cpu = SystemInfo.processorType;
        if (!ActiveZeldaCharacterDataSource.Instance) new GameObject("Profile HUD source").AddComponent<ActiveZeldaCharacterDataSource>();
        Camera.main.aspect = 16f / 9f;
        yield return Wait(4);
        skeletons = Object.FindObjectsOfType<SkeletonCharacterAi>(true);
        crystal = Object.FindObjectOfType<EnergyCrystal>(true);
        report.totalSkeletons = skeletons.Length; report.bindings = crystal.Skeletons.Count; report.crystalPosition = crystal.transform.position;
        var go = new GameObject("Profile ghost observer"); go.AddComponent<GhostZeldaCharacterData>();
        observer = go.AddComponent<ZeldaFourWayMover>(); ZeldaRuntimeRegistry.ClaimControlledMover(observer);
        MoveObserver(crystal.transform.position + new Vector3(0, -8)); yield return Wait(5); Markers();
        yield return Capture(optimized ? "Rendered maze - optimized" : "Rendered maze baseline");
        if (optimized)
        {
            yield return Wait(2); yield return Capture("Rendered maze - optimized repeat");
            MoveObserver(crystal.transform.position + new Vector3(27, -12));
            yield return Wait(4); yield return Capture("Rendered maze - away after optimization");
            MoveObserver(crystal.transform.position + new Vector3(0, -8));
            yield return Wait(5); yield return Capture("Rendered maze - optimized reloaded");
            yield break;
        }
        var flag = typeof(SkeletonCharacterAi).GetField("ProfileSkipInactive", BindingFlags.Public | BindingFlags.Static);
        flag.SetValue(null, true);
        yield return Wait(2); yield return Capture("Rendered maze - skip inactive candidates only");
        flag.SetValue(null, false);
        yield return Wait(2); yield return Capture("Rendered maze - original scan restored");
        var visual = crystal.GetComponent<EnergyCrystalVisual>();
        visual.enabled = false; crystal.transform.Find(EnergyCrystalVisual.LinkMeshName).gameObject.SetActive(false);
        yield return Wait(2); yield return Capture("Rendered maze - crystal visual off");
        visual.enabled = true;
        yield return Wait(2);
        foreach (var ai in skeletons) { Set(ai, "visionVisualRefreshInterval", 10000f); Set(ai, "nextVisionVisualRefreshTime", Time.time + 10000f); }
        yield return Wait(2); yield return Capture("Rendered maze - vision mesh updates off");
        foreach (var ai in skeletons) { Set(ai, "visionVisualRefreshInterval", .08f); Set(ai, "nextVisionVisualRefreshTime", 0f); }
        flag.SetValue(null, true);
        yield return Wait(2); yield return Capture("Rendered maze - repeat skip inactive");
    }
    static Actor ActorInfo(SkeletonCharacterAi ai)
    {
        return new Actor { name = ai.name, position = ai.transform.position, state = ai.CurrentState.ToString(), active = ai.isActiveAndEnabled,
            target = ai.CurrentTarget ? ai.CurrentTarget.name : "none", gridWidth = (int)Get(ai, "gridWidth"), gridHeight = (int)Get(ai, "gridHeight"),
            pathLength = ((IList)Get(ai, "gridPath")).Count, pathIndex = (int)Get(ai, "gridPathIndex"), plannedTarget = (Vector2)Get(ai, "gridPathPlannedTarget"),
            patrol = (Vector2[])Get(ai, "patrolPoints") };
    }
    static IEnumerator Capture(string name)
    {
        Debug.Log("MAZE_PROFILE_BEGIN " + name);
        foreach (var c in counters) { c.values.Clear(); c.calls = 0; }
        float start = Time.realtimeSinceStartup;
        for (int f = 0; f < 160; f++)
        {
            yield return null;
            foreach (var c in counters)
            {
                c.values.Add(c.recorder.LastValue / (c.unit == "ms" ? 1000000.0 : 1.0));
                if (c.recorder.Count > 0) c.calls += c.recorder.GetSample(c.recorder.Count - 1).Count;
            }
        }
        var all = Object.FindObjectsOfType<ZeldaCharacterAiBase>(true);
        var link = crystal.transform.Find(EnergyCrystalVisual.LinkMeshName).GetComponent<MeshFilter>();
        samples.Add(new Sample { name = name, camera = Camera.main.transform.position, cameraSize = Camera.main.orthographicSize,
            elapsed = Time.realtimeSinceStartup - start, allAi = all.Length, activeAi = all.Count(a => a.isActiveAndEnabled), activeSkeletons = skeletons.Count(s => s.isActiveAndEnabled),
            registeredMovers = ZeldaRuntimeRegistry.Movers.Count, activeMovers = ZeldaRuntimeRegistry.Movers.Count(m => m && m.gameObject.activeInHierarchy),
            activeRenderers = Object.FindObjectsOfType<Renderer>().Count(r => r.enabled), linkVertices = link.gameObject.activeInHierarchy ? link.sharedMesh.vertexCount : 0,
            skeletons = skeletons.Select(ActorInfo).ToArray(), metrics = counters.Select(c => { c.values.Sort(); return new Metric { name = c.name, unit = c.unit,
                mean = c.values.Average(), p95 = c.values[(int)(c.values.Count * .95)], max = c.values.Last(), calls = c.calls }; }).ToArray() });
        Debug.Log("MAZE_PROFILE_SAMPLE " + name + " activeSkeletons=" + samples.Last().activeSkeletons + " camera=" + Camera.main.transform.position);
    }
}
