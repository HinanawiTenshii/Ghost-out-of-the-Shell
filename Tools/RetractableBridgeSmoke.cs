using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RetractableBridgeSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly Stack<IEnumerator> steps = new Stack<IEnumerator>();
    static int frame = -1, checks;
    static double deadline;
    static RetractableBridgeSmoke() { EditorApplication.update += Tick; }
    static void Set(object o, string n, object v) => o.GetType().GetField(n, F).SetValue(o, v);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, F).GetValue(o);
    static void Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, F).Invoke(o, args);
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }

    public static void Run()
    {
        try
        {
            Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated project only");
            VerifyScene();
            Debug.Log("RETRACTABLE_BRIDGE_SCENE_PASS checks=" + checks);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            PlayerSettings.productName = "retractable-bridge-smoke";
            SessionState.SetBool(nameof(RetractableBridgeSmoke), true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void VerifyScene()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/TutorialLevel/Tutorial_Skills&Battles.unity");
        var bridge = Object.FindObjectOfType<RetractableBridgeMechanism>();
        Check(bridge != null, "scene bridge exists");
        Call(bridge, "Update");
        var a = Get<LeverData>(bridge, "firstLever");
        var b = Get<LeverData>(bridge, "secondLever");
        Check(a != null && b != null && a != b, "two real prefab lever references");
        Check(a.Complexity == 5 && b.Complexity == 5, "authored lever requirements preserved");
        Check(bridge.GetComponent<CameraVisionStreamingExempt>() != null, "barriers retained during streaming");
        Check(bridge.IsFullyClosed, "default retracted");
        Physics2D.SyncTransforms();
        Check(Physics2D.Linecast(new Vector2(23.67f, 75), new Vector2(23.67f, 70)).collider == bridge.GetComponent<BoxCollider2D>(), "closed gap blocks passage");
        Check(Physics2D.OverlapPoint(new Vector2(23.67f, 72.9f)) != null, "centre water collision");
        var water = GameObject.Find("Waters").GetComponentInChildren<SpriteRenderer>();
        Check(water != null && !water.GetComponent<BoxCollider2D>().enabled, "old whole-river collider disabled");
        var roads = GameObject.Find("Roads").GetComponentsInChildren<SpriteRenderer>();
        Check(roads.All(r => !r.bounds.Contains(new Vector3(23.67f,72.9f,0))), "road no longer covers river");
        Check(roads.Any(r => Mathf.Abs(r.bounds.min.y - 74.2f) < .005f), "north road ends at bank");
        Check(roads.Any(r => Mathf.Abs(r.bounds.max.y - 71.6f) < .005f), "south road ends at bank");
        Render("Bridge-Closed-preview.png");
        Set(bridge, "progress", 1f); Call(bridge, "ApplyState");
        Physics2D.SyncTransforms();
        Check(Physics2D.Linecast(new Vector2(23.67f,75),new Vector2(23.67f,70)).collider == null, "open bridge passable bank to bank");
        for (int x = 0; x <= 50; x++)
            if (x < 22.67f || x > 24.67f) Check(Physics2D.OverlapPoint(new Vector2(x,72.9f)) != null, "river remains blocked at x=" + x);
        Check(Get<Mesh>(bridge, "mesh").bounds.min.y <= -2.59f, "deck reaches southern bank");
        Render("Bridge-Open-preview.png");
    }

    static void Render(string name)
    {
        var go = new GameObject("Bridge preview camera");
        var camera = go.AddComponent<Camera>();
        go.transform.position = new Vector3(23.67f,72.9f,-20);
        camera.orthographic = true; camera.orthographicSize = 3.6f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.25f,.25f,.25f);
        var target = new RenderTexture(640,640,24); camera.targetTexture = target;
        camera.Render(); var previous = RenderTexture.active; RenderTexture.active = target;
        var texture = new Texture2D(640,640,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,640,640),0,0); texture.Apply();
        File.WriteAllBytes(Path.Combine(Directory.GetParent(Application.dataPath).FullName,name),texture.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        Object.DestroyImmediate(texture); Object.DestroyImmediate(target); Object.DestroyImmediate(go);
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(RetractableBridgeSmoke), false))
            {
                SessionState.SetBool(nameof(RetractableBridgeSmoke), false);
                deadline = EditorApplication.timeSinceStartup + 75;
                steps.Push(VerifyRuntime());
            }
            if (steps.Count == 0) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("test timed out");
            while (steps.Count > 0)
            {
                var step = steps.Peek();
                if (!step.MoveNext()) { steps.Pop(); continue; }
                if (step.Current is IEnumerator nested) { steps.Push(nested); continue; }
                return;
            }
            Debug.Log("RETRACTABLE_BRIDGE_PASS runtimeChecks=" + checks);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { steps.Clear(); Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static IEnumerator Until(Func<bool> condition, string label)
    {
        double timeout = EditorApplication.timeSinceStartup + 4;
        while (!condition()) { if (EditorApplication.timeSinceStartup > timeout) throw new Exception(label); yield return null; }
        Check(true,label);
    }

    static IEnumerator VerifyRuntime()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.CreateScene("Bridge save integration");
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(scene);
        var a = new GameObject("Lever A").AddComponent<LeverData>(); a.transform.position = new Vector3(-3,2);
        var b = new GameObject("Lever B").AddComponent<LeverData>(); b.transform.position = new Vector3(3,-4);
        var go = new GameObject("Bridge"); go.SetActive(false);
        var bridge = go.AddComponent<RetractableBridgeMechanism>();
        Set(bridge,"firstLever",a); Set(bridge,"secondLever",b); Set(bridge,"moveDuration",.3f);
        go.SetActive(true); yield return null;
        var barrier = go.GetComponent<BoxCollider2D>();
        Check(bridge.IsFullyClosed && barrier.enabled,"fresh start closed");
        a.ActivateFromClockworkPuppet(); yield return null;
        Check(bridge.IsOpenRequested && !bridge.IsFullyOpen && barrier.enabled,"lever A animates with gap blocked");
        yield return Until(()=>bridge.IsFullyOpen,"opens after animation");
        Check(!barrier.enabled,"bridge collision opens");
        b.ActivateFromClockworkPuppet(); yield return null;
        Check(!bridge.IsOpenRequested && barrier.enabled,"other lever starts retraction immediately");
        yield return Until(()=>bridge.IsFullyClosed,"fully retracts");
        b.ActivateFromClockworkPuppet(); yield return Until(()=>bridge.IsFullyOpen,"same lever can reopen");

        var body = new GameObject("Bridge occupant"); body.SetActive(false);
        body.AddComponent<ZeldaCharacterData>(); body.AddComponent<BoxCollider2D>().size = new Vector2(.5f,.5f);
        body.transform.position = new Vector3(0,-1.3f,0); body.SetActive(true);
        Physics2D.SyncTransforms(); a.ActivateFromClockworkPuppet();
        yield return null; yield return null;
        Check(bridge.IsWaitingForClearance && !barrier.enabled,"never retract through a character");
        body.transform.position = new Vector3(0,-4,0); Physics2D.SyncTransforms();
        yield return Until(()=>bridge.IsFullyClosed,"retracts after character leaves");

        a.ApplyPersistentState(true); b.ApplyPersistentState(false); yield return null;
        Check(bridge.IsFullyOpen && !barrier.enabled,"save restoration opens immediately without lever event");
        var manager = SceneTravelStateManager.GetOrCreate();
        string json = manager.CaptureDiskWorld();
        Check(json.Contains("\"hasLever\":true"), "production disk save includes lever state");
        var disk = JsonUtility.FromJson(json, typeof(SceneTravelStateManager).GetNestedType("DiskWorld", BindingFlags.NonPublic));
        object savedScene = null;
        foreach (var entry in Get<IList>(disk,"scenes"))
            if (Get<string>(entry,"name") == scene.name) savedScene = entry;
        Check(savedScene != null,"scene survives disk JSON roundtrip");
        var snapshot = Activator.CreateInstance(typeof(SceneTravelStateManager).GetNestedType("SceneState", BindingFlags.NonPublic), true);
        Set(snapshot,"sceneName",scene.name);
        foreach (var entry in Get<IList>(savedScene,"objects")) Get<IDictionary>(snapshot,"objects").Add(Get<string>(entry,"path"),entry);
        a.ApplyPersistentState(false); yield return null;
        Check(bridge.IsFullyClosed,"change state after saving");
        Call(manager,"RestoreSceneSnapshot",scene,snapshot,false); yield return null;
        Check(bridge.IsFullyOpen && !barrier.enabled,"real world restore reopens the bridge from saved levers");
        // Unloaded lever retains its saved state; streaming cannot toggle the bridge.
        a.gameObject.SetActive(false); yield return null;
        Check(bridge.IsFullyOpen,"lever unload does not change state"); a.gameObject.SetActive(true);
        a.ApplyPersistentState(false); yield return null;
        Check(bridge.IsFullyClosed && barrier.enabled,"save restoration closes immediately");
        a.ActivateFromClockworkPuppet(); yield return null; b.ActivateFromClockworkPuppet();
        yield return Until(()=>bridge.IsFullyClosed,"mid-extension toggle reverses without opening gap");
        Check(barrier.enabled,"reversal preserves blocking");
        // Loading while a character was waiting on the bridge must preserve a safe deck,
        // even if both saved lever bits request retraction.
        Set(manager,"sceneTravelRestoreInProgress",true); yield return null;
        body.transform.position = new Vector3(0,-1.3f,0); Physics2D.SyncTransforms();
        Set(manager,"sceneTravelRestoreInProgress",false); yield return null;
        Check(bridge.IsWaitingForClearance && !barrier.enabled,"load cannot trap a restored occupant in water");
        body.transform.position = new Vector3(0,-4,0); Physics2D.SyncTransforms();
        yield return Until(()=>bridge.IsFullyClosed,"loaded pending retraction finishes after clearance");
        Object.Destroy(body); Object.Destroy(go); Object.Destroy(a.gameObject); Object.Destroy(b.gameObject);
        yield return null;
    }
}
