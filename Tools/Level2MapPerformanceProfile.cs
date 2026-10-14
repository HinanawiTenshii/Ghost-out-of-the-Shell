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

// Diagnostic A/Bs only. Never saves scenes or changes production components.
[InitializeOnLoad]
public static class Level2MapPerformanceProfile
{
    [Serializable] public class Metric { public string name, unit; public double mean, p95, max, calls; }
    [Serializable] public class Sample { public string scene, phase; public int frames, width, height, activeAi, maps, canvases, walls, surfaces, wallRebuilds, terrainRebuilds, markerRebuilds; public Metric[] metrics; }
    [Serializable] public class Report { public string gpu, cpu; public List<Sample> samples = new List<Sample>(); public List<string> errors = new List<string>(); }
    class Counter { public string name, unit; public ProfilerRecorder recorder; public List<double> values = new List<double>(); public double calls; }
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly string[] scenes = { "Level2-Floor1", "Level2-Floor-1", "Level2-Floor2" };
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static readonly List<Counter> counters = new List<Counter>();
    static Report report = new Report();
    static int frame = -1;
    static double deadline;
    static Camera[] cameras;
    static RenderTexture target;
    static RuntimeMiniMapGraphic map;
    static ZeldaHealthHeartsUI hud;
    static ZeldaFourWayMover observer;
    static Vector3 origin;
    static bool moving, hideMap;
    static int movementFrame;
    static bool confirm;
    static Level2MapPerformanceProfile() { EditorApplication.update += Tick; }
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\','/').Contains("/.codex-temp/")) throw new Exception("Isolated project required");
        PlayerSettings.productName = "level2-map-performance-profile";
        EditorSceneManager.OpenScene("Assets/Scenes/Level2/" + (Environment.GetCommandLineArgs().Contains("-confirmMap") ? "Level2-Floor2" : "Level2-Floor1") + ".unity");
        SessionState.SetBool("MapPerformanceProfile", true);
        EditorApplication.EnterPlaymode();
    }
    static object Get(object owner, string name) => owner.GetType().GetField(name, F).GetValue(owner);
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool("MapPerformanceProfile", false))
            {
                SessionState.SetBool("MapPerformanceProfile", false); deadline = EditorApplication.timeSinceStartup + 360;
                confirm = Environment.GetCommandLineArgs().Contains("-confirmMap");
                Application.logMessageReceived += (msg, stack, type) => { if ((type == LogType.Error || type == LogType.Exception) && report.errors.Count < 20) report.errors.Add(msg); };
                steps.Push(Measure());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Profile deadline");
            if (hud != null) typeof(ZeldaHealthHeartsUI).GetField("showTaskPointer", F).SetValue(hud, !hideMap);
            if (moving && observer != null)
            {
                var position = origin + Vector3.right * (Mathf.Sin(movementFrame++ * .035f) * 2f);
                observer.transform.position = position; observer.GetComponent<Rigidbody2D>().position = position;
                typeof(ZeldaHealthHeartsUI).GetMethod("UpdateTaskPointer", F).Invoke(hud, null);
            }
            if (hideMap && map != null) map.gameObject.SetActive(false);
            if (cameras != null)
            {
                Canvas.ForceUpdateCanvases();
                foreach (var cam in cameras) if (cam && cam.isActiveAndEnabled) cam.Render();
            }
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
    static void Finish(int code)
    {
        foreach (var counter in counters) counter.recorder.Dispose(); counters.Clear();
        Profiler.enabled = false;
        File.WriteAllText(Path.Combine(Application.dataPath, "../Level2-map-performance" + (confirm ? "-confirmation" : Environment.GetCommandLineArgs().Contains("-optimizedMap") ? "-optimized" : "") + ".json"), JsonUtility.ToJson(report, true));
        Debug.Log("MAP_PERFORMANCE_COMPLETE samples=" + report.samples.Count + " exit=" + code);
        steps.Clear(); EditorApplication.Exit(code);
    }
    static IEnumerator Wait(int frames) { for (int i = 0; i < frames; i++) yield return null; }
    static void Markers()
    {
        var handles = new List<ProfilerRecorderHandle>(); ProfilerRecorderHandle.GetAvailable(handles);
        foreach (var h in handles)
        {
            var d = ProfilerRecorderHandle.GetDescription(h);
            if (!(d.Name.StartsWith("MapProfile.") || d.Name.Contains(".Update") || d.Name.Contains(".LateUpdate") || d.Name.Contains(".OnRenderImage") ||
                d.Name.Contains("Canvas") || d.Name.Contains("UI.") || new[] { "Main Thread", "Render Thread", "BehaviourUpdate", "Physics2D.Simulate", "Camera.Render", "GC.Collect", "Draw Calls Count", "Batches Count", "Vertices Count", "GC Allocated In Frame", "Gfx.WaitForPresentOnGfxThread" }.Contains(d.Name))) continue;
            if (counters.Any(c => c.name == d.Name)) continue;
            var rec = new ProfilerRecorder(h, 1, ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.SumAllSamplesInFrame | ProfilerRecorderOptions.WrapAroundWhenCapacityReached);
            if (rec.Valid) counters.Add(new Counter { name = d.Name, unit = d.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds ? "ms" : d.UnitType.ToString(), recorder = rec });
        }
    }
    static int Builds(string field) => map ? ((RuntimeMiniMapLayerGraphic)Get(map, field)).MeshBuildCount : 0;
    static IEnumerator Capture(string phase)
    {
        Markers(); foreach (var c in counters) { c.values.Clear(); c.calls = 0; }
        int w = Builds("wallLayer"), t = Builds("surfaceLayer"), m = Builds("markerLayer");
        for (int i = 0; i < 180; i++)
        {
            yield return null;
            foreach (var c in counters)
            {
                c.values.Add(c.recorder.LastValue / (c.unit == "ms" ? 1000000.0 : 1));
                if (c.recorder.Count > 0) c.calls += c.recorder.GetSample(0).Count;
            }
        }
        var sample = new Sample { scene = SceneManager.GetActiveScene().name, phase = phase, frames = 180, width = target.width, height = target.height,
            activeAi = Object.FindObjectsOfType<ZeldaCharacterAiBase>().Count(a => a.isActiveAndEnabled),
            maps = Object.FindObjectsOfType<RuntimeMiniMapGraphic>().Count(g => g.isActiveAndEnabled),
            canvases = Object.FindObjectsOfType<Canvas>().Count(c => c.isActiveAndEnabled),
            walls = ((IList)Get(map,"blockPaths")).Count, surfaces = ((IList)Get(map,"surfaces")).Count,
            wallRebuilds = Builds("wallLayer") - w, terrainRebuilds = Builds("surfaceLayer") - t, markerRebuilds = Builds("markerLayer") - m,
            metrics = counters.Select(c => { c.values.Sort(); return new Metric { name = c.name, unit = c.unit, mean = c.values.Average(), p95 = c.values[171], max = c.values.Last(), calls = c.calls / 180 }; }).ToArray() };
        report.samples.Add(sample); Debug.Log("MAP_PROFILE_SAMPLE " + sample.scene + " / " + phase + " meshes=" + sample.wallRebuilds + "/" + sample.terrainRebuilds + "/" + sample.markerRebuilds);
    }
    static IEnumerator Measure()
    {
        Profiler.enabled = true; QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
        report.gpu = SystemInfo.graphicsDeviceName; report.cpu = SystemInfo.processorType;
        foreach (var scene in confirm ? new[] { "Level2-Floor2" } : scenes)
        {
            cameras = null;
            if (SceneManager.GetActiveScene().name != scene) { SceneManager.LoadScene(scene); yield return null; }
            yield return Wait(70);
            var source = ZeldaRuntimeRegistry.GetControlledMover(); origin = source ? source.transform.position : Vector3.zero;
            if (scene == "Level2-Floor-1") { var crystal = Object.FindObjectOfType<EnergyCrystal>(true); if (crystal) origin = crystal.transform.position + new Vector3(0,-8,0); }
            var actor = new GameObject("Profile ghost observer"); actor.AddComponent<GhostZeldaCharacterData>();
            observer = actor.AddComponent<ZeldaFourWayMover>(); observer.transform.position = origin; observer.GetComponent<Rigidbody2D>().position = origin;
            ZeldaRuntimeRegistry.ClaimControlledMover(observer); ActiveZeldaCharacterDataSource.Instance.Refresh();
            hud = ZeldaHealthHeartsUI.Instance; map = hud.GetComponentInChildren<RuntimeMiniMapGraphic>(true);
            yield return Wait(70);
            if (target != null) Object.Destroy(target); target = new RenderTexture(1280, 720, 24);
            cameras = Object.FindObjectsOfType<Camera>().Where(c => c.enabled && c.targetTexture == null).OrderBy(c => c.depth).ToArray();
            foreach (var cam in cameras) { cam.targetTexture = target; cam.aspect = 16f/9f; }
            yield return Wait(60);
            if (confirm)
            {
                var registry = typeof(CRTScreenEffect).GetField("RegisteredCanvases", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
                Debug.Log("MAP_PROFILE_CANVASES total=" + Object.FindObjectsOfType<Canvas>(true).Length + " registered=" + registry.GetType().GetProperty("Count").GetValue(registry));
                typeof(RuntimeMiniMapLayerGraphic).GetField("TraceDirty", BindingFlags.Public | BindingFlags.Static).SetValue(null, true);
                moving = true; movementFrame = 0; yield return Wait(30); yield return Capture("Confirm moving baseline");
                var pause = Object.FindObjectOfType<PauseMenuController>(); pause.enabled = false;
                yield return Wait(30); yield return Capture("Confirm pause priority updates disabled");
                typeof(RuntimeMiniMapLayerGraphic).GetField("SkipDimensionDirty", BindingFlags.Public | BindingFlags.Static).SetValue(null, true);
                yield return Wait(30); yield return Capture("Confirm static map dimension callbacks suppressed");
                typeof(CRTScreenEffect).GetField("SkipRegistryConfigure", BindingFlags.Public | BindingFlags.Static).SetValue(null, true);
                yield return Wait(30); yield return Capture("Confirm repeated canvas configuration suppressed");
                moving = false;
                yield break;
            }
            yield return Capture("Baseline stationary");
            hideMap = true; map.gameObject.SetActive(false); yield return Wait(30); yield return Capture("Map hidden");
            hideMap = false; map.gameObject.SetActive(true); yield return Wait(30); yield return Capture("Map restored");
            var canvas = hud.GetComponent<Canvas>(); canvas.enabled = false; hideMap = true; yield return Wait(30); yield return Capture("HUD canvas disabled");
            canvas.enabled = true; hideMap = false; map.gameObject.SetActive(true);
            moving = true; movementFrame = 0; yield return Wait(30); yield return Capture("Moving map");
            hideMap = true; yield return Wait(30); yield return Capture("Moving map hidden");
            moving = false; hideMap = false; map.gameObject.SetActive(true); yield return Wait(30);
            foreach (var cam in cameras) if (cam) cam.targetTexture = null;
            cameras = null;
        }
    }
}
