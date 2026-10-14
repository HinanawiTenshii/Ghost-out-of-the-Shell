using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Profiling;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using Object = UnityEngine.Object;

// Diagnostic only. Run in the isolated copy; never save the opened scene.
[InitializeOnLoad]
public static class Level2Floor1Profile
{
    [Serializable] public class Count { public string name; public int total, active; }
    [Serializable] public class Group { public string name; public int members; public Vector2 min, max; }
    [Serializable] public class Metric { public string name, category, unit; public double mean, p95, max; }
    [Serializable] public class Sample { public string name; public Vector3 camera; public float size, aspect; public int width, height, frames, activeAi, allAi, activeRenderers, activeColliders, managed, suspended, loadedGroups, activeStatsWindows, activeCanvases, activeSquadMembers; public Count[] aiStates; public Metric[] metrics; }
    [Serializable] public class Report { public string gpu, cpu; public int initialObjects, missingScripts; public Count[] components; public Group[] groups; public Sample[] samples; public string[] logs; }
    class Counter { public string name, category, unit; public ProfilerRecorder recorder; public readonly List<double> values = new List<double>(); }
    static readonly Report report = new Report();
    static readonly List<Sample> samples = new List<Sample>();
    static readonly List<string> logs = new List<string>();
    static readonly List<Counter> counters = new List<Counter>();
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    static IEnumerator run; static int frame = -1;
    static Level2Floor1Profile() { EditorApplication.update += Tick; }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/Level2-Floor1.unity");
        Application.logMessageReceived += Log;
        Inventory();
        SessionState.SetString("Floor1Inventory", JsonUtility.ToJson(report));
        SessionState.SetBool("Floor1Profile", true); EditorApplication.EnterPlaymode();
    }
    static void Log(string message, string stack, LogType kind)
    { if ((kind == LogType.Error || kind == LogType.Exception || kind == LogType.Warning) && logs.Count < 30) logs.Add(kind + ": " + message); }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("Floor1Profile", false))
            {
                SessionState.SetBool("Floor1Profile", false);
                JsonUtility.FromJsonOverwrite(SessionState.GetString("Floor1Inventory", "{}"), report);
                Application.logMessageReceived += Log; run = Measure();
            }
            if (run == null || run.MoveNext()) return;
            run = null; Finish(); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); Finish(); EditorApplication.Exit(1); }
    }
    static void Finish()
    {
        foreach (var c in counters) c.recorder.Dispose(); counters.Clear();
        Profiler.enabled = false; report.samples = samples.ToArray(); report.logs = logs.ToArray();
        File.WriteAllText(Path.Combine(Application.dataPath, "../Level2Floor1-profile.json"), JsonUtility.ToJson(report, true));
        Debug.Log("FLOOR1_PROFILE_COMPLETE samples=" + samples.Count);
    }
    static object Field(object owner, string name)
    {
        for (Type type = owner.GetType(); type != null; type = type.BaseType)
        { var f = type.GetField(name, F); if (f != null) return f.GetValue(owner); }
        return null;
    }
    static void Inventory()
    {
        var components = new List<Component>();
        foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects()) components.AddRange(root.GetComponentsInChildren<Component>(true));
        report.initialObjects = components.OfType<Transform>().Count(); report.missingScripts = components.Count(c => c == null);
        report.components = components.Where(c => c != null).GroupBy(c => c.GetType().Name).Select(g => new Count { name = g.Key, total = g.Count(), active = g.Count(c => c.gameObject.activeInHierarchy && (!(c is Behaviour) || ((Behaviour)c).enabled)) }).OrderByDescending(c => c.total).ToArray();
        var groups = new List<Group>();
        foreach (var g in components.OfType<PatrolStreamingGroup>())
        {
            var ai = (ZeldaPatrolCharacterAi[])Field(g, "members");
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = -min;
            foreach (var member in ai)
            {
                if (!member) continue;
                Vector2 origin = member.transform.position;
                var offsets = (bool)Field(member, "useCustomPatrolRoute")
                    ? ((ZeldaPatrolCharacterAi.PatrolWaypoint[])Field(member, "customPatrolPoints")).Select(p => p.offset)
                    : new[] { Vector2.zero, (Vector2)Field(member, "patrolPoint2Offset"), (Vector2)Field(member, "patrolPoint3Offset"), (Vector2)Field(member, "patrolPoint4Offset") };
                foreach (var offset in offsets) { min = Vector2.Min(min, origin + offset); max = Vector2.Max(max, origin + offset); }
            }
            groups.Add(new Group { name = g.name, members = ai.Count(a => a != null), min = min, max = max });
        }
        report.groups = groups.ToArray();
    }
    static IEnumerator Wait(float seconds)
    { double end = EditorApplication.timeSinceStartup + seconds; while (EditorApplication.timeSinceStartup < end) yield return null; }
    static IEnumerator Measure()
    {
        QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1; Time.captureFramerate = 0; Time.timeScale = 1;
        Profiler.enabled = true; report.gpu = SystemInfo.graphicsDeviceName; report.cpu = SystemInfo.processorType;
        // Direct scene entry skips the title bootstrap. Restore its common HUD
        // services explicitly in this diagnostic copy before taking samples.
        if (!ActiveZeldaCharacterDataSource.Instance) new GameObject("Profile HUD source").AddComponent<ActiveZeldaCharacterDataSource>();
        if (!ZeldaHealthHeartsUI.Instance) new GameObject("Profile HUD", typeof(RectTransform), typeof(Canvas)).AddComponent<ZeldaHealthHeartsUI>();
        if (Camera.main) Camera.main.aspect = 16f / 9f;
        var wait = Wait(5); while (wait.MoveNext()) yield return null;
        var allAi = Object.FindObjectsOfType<ZeldaCharacterAiBase>(true);
        var points = new List<Vector3>();
        foreach (var ai in allAi.OrderByDescending(a => allAi.Count(b => Mathf.Abs(a.transform.position.x - b.transform.position.x) < 20 && Mathf.Abs(a.transform.position.y - b.transform.position.y) < 12)))
            if (points.All(p => Vector2.Distance(p, ai.transform.position) > 25)) { points.Add(ai.transform.position); if (points.Count == 2) break; }
        // Discover real markers after systems have executed. No Deep Profile.
        var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var handle in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(handle);
            bool timer = d.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
            if (!(d.Name.Contains(".Update") || d.Name.Contains(".LateUpdate") || d.Name.Contains(".FixedUpdate") || d.Name.Contains(".OnRenderImage") ||
                new[] { "Main Thread", "Render Thread", "BehaviourUpdate", "Physics2D.Simulate", "Camera.Render", "Canvas.BuildBatch", "GC.Collect", "Batches Count", "Draw Calls Count", "SetPass Calls Count", "Triangles Count", "Vertices Count", "GC Allocated In Frame", "System Used Memory", "Total Used Memory", "Gfx.WaitForPresentOnGfxThread" }.Contains(d.Name))) continue;
            var rec = new ProfilerRecorder(handle, 1, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            if (rec.Valid) counters.Add(new Counter { name = d.Name, category = d.Category.Name, unit = timer ? "ms" : d.UnitType.ToString(), recorder = rec });
        }
        Debug.Log("PROFILE_MARKERS " + string.Join(",", counters.Select(c => c.name)));
        var capture = Capture("Scene entry"); while (capture.MoveNext()) yield return capture.Current;
        // A ghost observer prevents combat from confounding the steady-state density samples.
        var observer = new GameObject("Profile-only observer"); observer.AddComponent<GhostZeldaCharacterData>();
        var mover = observer.AddComponent<ZeldaFourWayMover>(); ZeldaRuntimeRegistry.ClaimControlledMover(mover);
        for (int i = 0; i < points.Count; i++)
        {
            observer.transform.position = points[i]; observer.GetComponent<Rigidbody2D>().position = points[i];
            ActiveZeldaCharacterDataSource.Instance.Refresh();
            wait = Wait(4); while (wait.MoveNext()) yield return null;
            capture = Capture("Dense area " + (i + 1)); while (capture.MoveNext()) yield return capture.Current;
        }
        // Controlled A/B check: only disable already invisible attribute panels.
        // This is confined to the unsaved validation scene, not the game project.
        var hiddenStats = Object.FindObjectsOfType<ZeldaCharacterStatsWindow>()
            .Where(w => w.enabled && Field(w, "overlayObject") is GameObject panel && !panel.activeInHierarchy).ToArray();
        foreach (var window in hiddenStats) window.enabled = false;
        wait = Wait(2); while (wait.MoveNext()) yield return null;
        capture = Capture("Dense area 2 - hidden stats updates disabled"); while (capture.MoveNext()) yield return capture.Current;
        foreach (var window in hiddenStats) if (window) window.enabled = true;
        wait = Wait(2); while (wait.MoveNext()) yield return null;
        capture = Capture("Dense area 2 - restored"); while (capture.MoveNext()) yield return capture.Current;
        capture = Capture("Moving observer", () => {
            Vector3 next = observer.transform.position + Vector3.left * 4f * Time.unscaledDeltaTime;
            observer.transform.position = next; observer.GetComponent<Rigidbody2D>().position = next;
        }); while (capture.MoveNext()) yield return capture.Current;
    }
    static IEnumerator Capture(string name, Action perFrame = null)
    {
        foreach (var c in counters) c.values.Clear();
        for (int f = 0; f < 180; f++)
        {
            if (perFrame != null) perFrame();
            yield return null;
            foreach (var c in counters) c.values.Add(c.recorder.LastValue / (c.unit == "ms" ? 1000000.0 : 1.0));
        }
        var ai = Object.FindObjectsOfType<ZeldaCharacterAiBase>(true); var manager = Object.FindObjectOfType<CameraVisionObjectStreaming>();
        var managed = manager ? (IList)Field(manager, "managedObjects") : null;
        int suspended = 0; if (managed != null) foreach (var item in managed) if ((bool)Field(item, "suspendedByManager")) suspended++;
        var sample = new Sample { name = name, camera = Camera.main ? Camera.main.transform.position : Vector3.zero,
            size = Camera.main ? Camera.main.orthographicSize : 0, aspect = Camera.main ? Camera.main.aspect : 0, width = Screen.width, height = Screen.height, frames = 180,
            activeAi = ai.Count(a => a.isActiveAndEnabled), allAi = ai.Length,
            activeRenderers = Object.FindObjectsOfType<Renderer>().Count(r => r.enabled), activeColliders = Object.FindObjectsOfType<Collider2D>().Count(c => c.enabled),
            managed = managed == null ? 0 : managed.Count, suspended = suspended,
            activeStatsWindows = Object.FindObjectsOfType<ZeldaCharacterStatsWindow>().Count(w => w.isActiveAndEnabled),
            activeCanvases = Object.FindObjectsOfType<Canvas>().Count(c => c.isActiveAndEnabled),
            activeSquadMembers = Object.FindObjectsOfType<SquadPatrolCharacterAi>().Count(c => c.isActiveAndEnabled),
            loadedGroups = Object.FindObjectsOfType<PatrolStreamingGroup>().Count(g => !g.PatrolPaused),
            aiStates = ai.Where(a => a.isActiveAndEnabled).GroupBy(a => a.CurrentState).Select(g => new Count { name = g.Key.ToString(), total = g.Count() }).ToArray(),
            metrics = counters.Select(c => { c.values.Sort(); return new Metric { name = c.name, category = c.category, unit = c.unit, mean = c.values.Average(), p95 = c.values[(int)(c.values.Count * .95)], max = c.values.Last() }; }).ToArray() };
        samples.Add(sample); Debug.Log("PROFILE_SAMPLE " + name + " camera=" + sample.camera + " activeAi=" + sample.activeAi + " renderers=" + sample.activeRenderers);
    }
}
