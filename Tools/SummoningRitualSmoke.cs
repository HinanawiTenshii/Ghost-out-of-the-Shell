using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class SummoningRitualSmoke
{
    [Serializable] private sealed class DiskWorld { public DiskScene[] scenes; }
    [Serializable] private sealed class DiskScene { public DiskObject[] objects; }
    [Serializable] private sealed class DiskObject { public bool hasSummoningCircle; public SummoningCircle.RuntimeState summoningCircleState; }
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    const string CirclePath = "Assets/Prefabs/Decorations/SummoningCircle.prefab";
    const string VisitorPath = "Assets/Prefabs/Level2NPCS/Idle/OuterRealmVisitor-level2-Idle.prefab";
    static IEnumerator steps;
    static int frame = -1, checks;
    static double deadline;
    static SummoningRitualSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated test project only");
        PlayerSettings.productName = "summoning-ritual-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(SummoningRitualSmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(SummoningRitualSmoke), false))
            { SessionState.SetBool(nameof(SummoningRitualSmoke), false); steps = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (steps == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("ritual test timeout");
            if (steps.MoveNext()) return;
            steps = null; Debug.Log("SUMMONING_RITUAL_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { steps = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static SummoningCircle NewCircle(Vector3 position)
    {
        var circle = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(CirclePath), position, Quaternion.identity).GetComponent<SummoningCircle>();
        circle.SummonPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisitorPath).GetComponent<ZeldaCharacterData>();
        return circle;
    }
    static void Call(SummoningCircle c, string name, params object[] args) => typeof(SummoningCircle).GetMethod(name, F).Invoke(c, args);
    static Mesh Mesh(SummoningCircle c) => c.transform.Find("Summoning Ritual Particles").GetComponent<MeshFilter>().sharedMesh;
    static void Refresh(SummoningCircle c) => c.GetComponent<SummoningCircleVisual>().RefreshImmediately();
    static void Stage(SummoningCircle c, SummoningCircle.RitualStage stage, float elapsed, bool summoned = false)
    {
        c.ApplyRuntimeState(new SummoningCircle.RuntimeState { litMask = 63, ritualStage = stage, ritualElapsed = elapsed, summoned = summoned });
        Call(c, "UpdateRitualAudio"); Refresh(c);
    }
    static IEnumerator Verify()
    {
        while (RetroSceneLoadReveal.IsBlockingInput) yield return null;
        var scene = SceneManager.CreateScene("Ritual Smoke"); SceneManager.SetActiveScene(scene);
        var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
        camera.transform.position = new Vector3(0, 1, -10); camera.orthographic = true; camera.orthographicSize = 6; camera.aspect = 16f/9;
        camera.gameObject.AddComponent<AudioListener>();
        var circle = NewCircle(Vector3.zero);
        Check(circle.Stage == SummoningCircle.RitualStage.Dormant && !circle.HasSummoned, "new seal dormant");
        Check(Mesh(circle).vertexCount == 0, "no ritual particles before charged");
        Check(circle.GetComponentInChildren<AudioSource>() == null, "idle seal has no sound voice");
        foreach (var name in new[] { "SummoningGather", "SummoningEruption", "SummoningBurst" })
        {
            var clip = Resources.Load<AudioClip>("Audio/" + name);
            Check(clip != null && clip.channels == 1 && clip.frequency == 44100, "runtime clip " + name);
        }
        circle.ApplyRuntimeState(new SummoningCircle.RuntimeState { litMask = 63 });
        Call(circle, "Update");
        Check(circle.Stage == SummoningCircle.RitualStage.Gathering && !circle.HasSummoned, "six lit crystals start gathering, not instant spawn");
        Check(circle.StageDuration == 3f, "default gathering is three seconds");
        Stage(circle, SummoningCircle.RitualStage.Gathering, 1.3f);
        Check(Mesh(circle).vertexCount > 1000 && Mesh(circle).vertexCount < 2000, "six dense streams in one bounded mesh");
        var points = Mesh(circle).vertices;
        for (int crystal = 0; crystal < 6; crystal++)
        {
            Vector3 local = circle.transform.InverseTransformPoint(circle.CrystalPosition(crystal));
            Check(points.Any(p => Vector3.Distance(p, local) < .4f), "stream originates at crystal " + crystal);
        }
        var audio = circle.GetComponentInChildren<AudioSource>(true);
        Check(audio != null && audio.clip.name == "SummoningGather" && !audio.loop && !audio.playOnAwake, "single gathering sound");
        Check(Mathf.Abs(audio.time - 1.3f) < .08f, "restored gathering sound seeks to saved age");
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .016f) == Vector2.zero, "gathering never shakes");
        string saved = JsonUtility.ToJson(circle.CaptureRuntimeState());
        Stage(circle, SummoningCircle.RitualStage.Gathering, .2f);
        circle.ApplyRuntimeState(JsonUtility.FromJson<SummoningCircle.RuntimeState>(saved)); Refresh(circle);
        Check(Mesh(circle).vertices.SequenceEqual(points), "serialized gathering restores identical particle positions");
        Check(circle.StageElapsed == 1.3f && !circle.HasSummoned, "phase progress restored");
        var manager = SceneTravelStateManager.GetOrCreate();
        string disk = manager.CaptureDiskWorld();
        var diskCircle = JsonUtility.FromJson<DiskWorld>(disk).scenes.SelectMany(s => s.objects).Single(o => o.hasSummoningCircle).summoningCircleState;
        Check(diskCircle.ritualStage == SummoningCircle.RitualStage.Gathering && diskCircle.ritualElapsed == 1.3f, "real world snapshot includes ritual fields");
        Check(!disk.Contains("Summoning Ritual Particles") && !disk.Contains("Summoning Ritual Audio"), "generated effects are not saved as scene objects");
        Time.timeScale = 0; Call(circle, "Update");
        Check(circle.StageElapsed == 1.3f && !audio.isPlaying, "pause freezes stage and audio");
        Time.timeScale = 1;
        Call(circle, "AdvanceRitual", 1.7f); Call(circle, "UpdateRitualAudio");
        Check(circle.Stage == SummoningCircle.RitualStage.Erupting && !circle.HasSummoned && circle.StageDuration == 3f, "gather ends at eruption, still no NPC");
        Stage(circle, SummoningCircle.RitualStage.Erupting, 1.1f);
        Check(audio.clip.name == "SummoningEruption" && Mathf.Abs(audio.time - 1.1f) < .08f, "eruption audio phase and restore offset");
        Check(Mesh(circle).bounds.max.y > 3.5f && Mesh(circle).bounds.size.y > Mesh(circle).bounds.size.x, "particles rise in a tall column");
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .016f).sqrMagnitude > .00001f, "visible eruption shakes camera");
        camera.transform.position = new Vector3(100, 100, -10);
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .016f) == Vector2.zero, "offscreen seal does not shake");
        Call(circle, "UpdateRitualAudio"); Check(audio.volume == 0, "far-off sound attenuates to silence");
        camera.transform.position = new Vector3(0, 1, -10); Call(circle, "UpdateRitualAudio");
        Check(audio.volume > .4f && Mathf.Abs(audio.time - 1.1f) < .08f, "moving back resumes current sound, not onset");
        camera.cullingMask = 0;
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .016f) == Vector2.zero, "camera layer masking respected");
        camera.cullingMask = -1;
        Time.timeScale = 0;
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .016f) == Vector2.zero, "paused eruption does not shake");
        Time.timeScale = 1;
        typeof(RetroSceneLoadReveal).GetProperty("IsBlockingInput", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SetValue(null, true);
        Call(circle, "Update");
        Check(circle.StageElapsed == 1.1f && !audio.isPlaying && CameraEnvironmentalShakeSource.SampleOffset(camera, null, .016f) == Vector2.zero, "loading cover freezes effects and shake");
        typeof(RetroSceneLoadReveal).GetProperty("IsBlockingInput", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).SetValue(null, false);
        string eruptionSave = JsonUtility.ToJson(circle.CaptureRuntimeState());
        Object.Destroy(circle.gameObject); yield return null;
        circle = NewCircle(Vector3.zero);
        circle.ApplyRuntimeState(JsonUtility.FromJson<SummoningCircle.RuntimeState>(eruptionSave));
        Call(circle, "UpdateRitualAudio"); Refresh(circle);
        Check(circle.Stage == SummoningCircle.RitualStage.Erupting && circle.StageElapsed == 1.1f && !circle.HasSummoned, "scene recreation resumes eruption, no early NPC");
        Check(!Object.FindObjectsOfType<SummonedCharacterOrigin>().Any(), "still no NPC before burst");
        Call(circle, "AdvanceRitual", 1.91f); Call(circle, "UpdateRitualAudio"); Refresh(circle);
        Check(circle.HasSummoned && circle.Stage == SummoningCircle.RitualStage.Dispersing, "burst appears together with summoned actor");
        var spawned = Object.FindObjectsOfType<SummonedCharacterOrigin>().Single();
        Check(spawned.PrefabName == circle.SummonPrefab.name && spawned.transform.position == circle.transform.position, "actual visitor at seal center");
        Check(spawned.GetComponent<ZeldaCharacterData>().Health == 666 && !spawned.GetComponent<ZeldaFourWayMover>().enabled, "NPC stats and player control unchanged");
        spawned.GetComponent<ZeldaCharacterAiBase>().enabled = false;
        audio = circle.GetComponentInChildren<AudioSource>(true);
        Check(audio.clip.name == "SummoningBurst", "release sound starts with NPC");
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .016f) == Vector2.zero, "shaking ends when eruption ends");
        Stage(circle, SummoningCircle.RitualStage.Dispersing, .5f, true);
        Check(Mesh(circle).bounds.min.x < -2 && Mesh(circle).bounds.max.x > 2 && Mesh(circle).bounds.min.y < -2, "burst spreads in all ground directions");
        var burstSave = circle.CaptureRuntimeState();
        circle.ApplyRuntimeState(JsonUtility.FromJson<SummoningCircle.RuntimeState>(JsonUtility.ToJson(burstSave)));
        Call(circle, "AdvanceRitual", 2f); Call(circle, "UpdateRitualAudio"); Refresh(circle);
        Check(Object.FindObjectsOfType<SummonedCharacterOrigin>().Length == 1, "restoring burst cannot duplicate NPC");
        Check(circle.Stage == SummoningCircle.RitualStage.Complete && Mesh(circle).vertexCount == 0 && !audio.isPlaying, "effect clears completely at end");
        circle.ApplyRuntimeState(JsonUtility.FromJson<SummoningCircle.RuntimeState>("{\"litMask\":63,\"summoned\":true}"));
        Call(circle, "Update");
        Check(circle.Stage == SummoningCircle.RitualStage.Complete && !circle.IsRitualActive, "old completed save never replays ritual");
        circle.gameObject.SetActive(false);
        Check(CameraEnvironmentalShakeSource.SampleOffset(camera, null, .016f) == Vector2.zero && !audio.isPlaying, "disable cleans up sound and camera source");
        Preview();
    }
    static void Preview()
    {
        var root = new GameObject("Ritual Preview");
        for (int i = 0; i < 3; i++)
        {
            var c = NewCircle(new Vector3((i - 1) * 9, 1000)); c.transform.SetParent(root.transform); c.enabled = false;
            Stage(c, (SummoningCircle.RitualStage)(i + 1), i == 0 ? 2.4f : i == 1 ? 1.7f : .4f, i == 2);
            if (i == 2)
            {
                var visitor = Object.Instantiate(c.SummonPrefab.gameObject, c.transform.position, Quaternion.identity, root.transform);
                foreach (var ai in visitor.GetComponents<ZeldaCharacterAiBase>()) ai.enabled = false;
                foreach (var mover in visitor.GetComponents<ZeldaFourWayMover>()) mover.enabled = false;
            }
            var label = new GameObject("Caption"); label.transform.SetParent(root.transform);
            label.transform.position = c.transform.position + new Vector3(0, -3.5f);
            var text = label.AddComponent<TextMesh>(); text.text = new[] { "CONVERGENCE", "ERUPTION", "RELEASE / APPEAR" }[i];
            text.fontSize = 40; text.characterSize = .1f; text.anchor = TextAnchor.MiddleCenter; text.color = new Color(.83f,.7f,1);
        }
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 30;
        var camera = new GameObject("Preview Camera").AddComponent<Camera>(); camera.transform.position = new Vector3(0, 1000.8f, -10);
        camera.orthographic = true; camera.orthographicSize = 5.5f; camera.cullingMask = 1 << 30;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color32(13,20,34,255);
        var rt = new RenderTexture(1650, 620, 24); camera.targetTexture = rt; camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var image = new Texture2D(1650, 620, TextureFormat.RGBA32, false); image.ReadPixels(new Rect(0,0,1650,620),0,0); image.Apply();
        File.WriteAllBytes(Application.dataPath + "/../SummoningRitual-preview.png", image.EncodeToPNG());
        RenderTexture.active = previous; camera.targetTexture = null;
        Object.Destroy(image); Object.Destroy(rt); Object.Destroy(camera.gameObject); Object.Destroy(root);
    }
}
