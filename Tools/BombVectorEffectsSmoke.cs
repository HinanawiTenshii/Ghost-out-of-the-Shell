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

// Isolated-project play-mode regression and actual Unity-rendered visual contact sheets.
[InitializeOnLoad]
public static class BombVectorEffectsSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static readonly Color Background = new Color(.047f, .08f, .12f, 1);
    static BombVectorEffectsSmoke() { EditorApplication.update += Tick; }
    static object Get(object o, string name) => o.GetType().GetField(name, F).GetValue(o);
    static object Call(object o, string name, params object[] args) => o.GetType().GetMethod(name, F).Invoke(o, args);
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/"), "isolated only");
        PlayerSettings.productName = "bomb-vector-effects-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(BombVectorEffectsSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(BombVectorEffectsSmoke), false))
            { SessionState.SetBool(nameof(BombVectorEffectsSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 100; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Timeout");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("BOMB_VECTOR_EFFECTS_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void CheckGeometry(Component effect, bool pulse)
    {
        var mesh = effect.GetComponent<MeshFilter>().sharedMesh;
        Check(mesh.vertexCount <= 2048, "bounded geometry");
        Check(mesh.triangles.All(i => i >= 0 && i < mesh.vertexCount), "valid triangle indices");
        Check(mesh.vertices.All(v => !float.IsNaN(v.x) && !float.IsNaN(v.y) && v.z == 0), "finite flat XY geometry");
        Check(mesh.colors32.Length == mesh.vertexCount, "vertex colours for every point");
        Check(effect.GetComponentsInChildren<Renderer>().Length == 1 && effect.transform.childCount == 0,
            "single renderer without per-particle objects");
        Check(effect.GetComponent<Renderer>().sharedMaterial.mainTexture == Texture2D.whiteTexture,
            "vector silhouettes, no raster effect texture");
        Check(effect.GetComponent<Collider2D>() == null && effect.GetComponent<AudioSource>() == null,
            "no new damage, collision or sound source");
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        Time.timeScale = 0;
        new GameObject("Test Listener").AddComponent<AudioListener>();
        Material sharedBlast = null, sharedPulse = null;
        foreach (string id in new[] { "BombPickupItem", "PurpleBombPickupItem", "DecoyBombPickupItem", "ProximityBombPickupItem" })
        {
            var template = Resources.Load<BombPickupItem>("PickupItems/Bombs/" + id);
            Check(template != null, "bomb template " + id);
            var bomb = template.CreateConfiguredPlacedBomb(Vector3.zero, null);
            bomb.DetonateImmediately();
            var visual = Object.FindObjectsOfType<BombExplosionVisual>().Single();
            var pulse = Object.FindObjectsOfType<ZeldaInvestigationPulse>().Single();
            var hit = Object.FindObjectsOfType<ZeldaAttackHitbox>().Single();
            Check((Vector2)Get(visual, "blastSize") == template.ExplosionSize, "visual footprint follows bomb configuration");
            Check((int)Get(hit, "attackPower") == template.ExplosionAttackPower, "original attack power unchanged");
            Check(Mathf.Approximately((float)Get(pulse, "maximumRadius"), template.InvestigationRadius), "original attraction radius");
            Check(Mathf.Approximately((float)Get(pulse, "duration"), template.InvestigationPulseDuration), "original ripple duration");
            Check((Color)Get(pulse, "baseColor") == template.InvestigationPulseColor, "original ripple colour");
            var blastMaterial = visual.GetComponent<Renderer>().sharedMaterial;
            var pulseMaterial = pulse.GetComponent<Renderer>().sharedMaterial;
            if (sharedBlast != null) Check(sharedBlast == blastMaterial && sharedPulse == pulseMaterial, "materials cached across explosions");
            sharedBlast = blastMaterial; sharedPulse = pulseMaterial;
            float lifetime = (float)Get(visual, "lifetime");
            for (int i = 0; i < 5; i++)
            {
                Call(visual, "ApplyFrame", lifetime * i / 5);
                Call(pulse, "ApplyFrame", template.InvestigationPulseDuration * i / 5);
                CheckGeometry(visual, false); CheckGeometry(pulse, true);
                var vertices = pulse.GetComponent<MeshFilter>().sharedMesh.vertices;
                Check(vertices.All(v => v.magnitude <= template.InvestigationRadius + .001f), "ring never exceeds attraction radius");
                if (i > 0)
                {
                    float p = i / 5f;
                    float expected = template.InvestigationRadius * (1f - (1f-p)*(1f-p));
                    Check(Mathf.Abs(vertices.Max(v => v.magnitude) - expected) < .001f, "leading edge retains old propagation curve");
                    Check(vertices.All(v => v.magnitude > expected * .45f), "ripple never fills its centre");
                }
            }
            Call(visual, "ApplyFrame", lifetime); Call(pulse, "ApplyFrame", template.InvestigationPulseDuration);
            Check(!visual.GetComponent<Renderer>().enabled && !pulse.GetComponent<Renderer>().enabled, "effects fully fade at end");
            var savedMesh = visual.GetComponent<MeshFilter>().sharedMesh;
            float elapsed = (float)Get(visual, "elapsed");
            yield return null;
            Check((float)Get(visual, "elapsed") == elapsed, "paused gameplay also pauses visual");
            Object.Destroy(visual.gameObject); Object.Destroy(pulse.gameObject); Object.Destroy(hit.gameObject);
            if (bomb != null) Object.Destroy(bomb.gameObject);
            yield return null; yield return null;
            Check(savedMesh == null, "per-instance geometry released on destruction");
        }

        var repeat = new GameObject("Reconfigure").AddComponent<BombExplosionVisual>();
        repeat.Configure(new Vector2(4, 2), .45f, Color.red, 0, 5, 10000);
        var sameMesh = repeat.GetComponent<MeshFilter>().sharedMesh;
        Check((int)Get(repeat, "fragmentCount") == 20, "editor particle counts bounded");
        repeat.Configure(Vector2.one, .3f, Color.blue, 0, 7, 0);
        Check((int)Get(repeat, "fragmentCount") == 4 && repeat.GetComponent<MeshFilter>().sharedMesh == sameMesh,
            "reconfigure reuses geometry and resets state");
        Call(repeat, "ApplyFrame", .04f);
        Check(sameMesh.colors32.Any(c => c.b > c.r + 50), "non-bomb blue callers retain their tint");
        repeat.Configure(Vector2.one, .1f, Color.gray, 0, 7);
        Time.timeScale = 1;
        float end = Time.time + .35f;
        while (Time.time < end) yield return null;
        Check(repeat == null, "normal lifetime cleans up automatically");
        Time.timeScale = 0;
        Preview();
        Time.timeScale = 1;
    }
    static void Preview()
    {
        var roots = new List<GameObject>();
        var camera = new GameObject("Bomb Preview Camera").AddComponent<Camera>();
        camera.enabled = false; camera.cullingMask = 1 << 31;
        camera.transform.position = new Vector3(100, 0, -10); camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Background;
        var tints = new[] { new Color(1, .2f, .08f, .65f), new Color(.33f,.81f,1,.68f), new Color(.88f,.89f,.84f,1) };
        float[] ages = { .025f, .09f, .21f, .39f, .57f };
        for (int row=0; row<3; row++) for (int col=0; col<ages.Length; col++)
        {
            var root = new GameObject("Blast frame") { layer = 31 }; roots.Add(root);
            root.transform.position = new Vector3(100 + (col-2)*3.7f, (1-row)*3.3f, 0);
            var visual = root.AddComponent<BombExplosionVisual>();
            UnityEngine.Random.InitState(71);
            visual.Configure(Vector2.one * 3, .45f, tints[row], 0, 10);
            Call(visual, "ApplyFrame", ages[col]);
        }
        camera.orthographicSize = 5.25f;
        Capture(camera, "BombVectorBlast-preview.png", 1500, 850);
        foreach (var root in roots) Object.DestroyImmediate(root); roots.Clear();
        for (int i=0;i<4;i++)
        {
            var root = new GameObject("Wave frame") { layer = 31 }; roots.Add(root);
            root.transform.position = new Vector3(100+(i-1.5f)*11,0,0);
            var pulse = root.AddComponent<ZeldaInvestigationPulse>();
            pulse.Configure(5, .7f, Color.white, 0, 10);
            Call(pulse, "ApplyFrame", .07f + i*.16f);
        }
        camera.orthographicSize = 6.4f;
        Capture(camera, "InvestigationRipple-preview.png", 1500, 430);
        foreach (var root in roots) Object.DestroyImmediate(root);
        Object.Destroy(camera.gameObject);
    }
    static void Capture(Camera camera, string name, int width, int height)
    {
        var rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        camera.aspect = width / (float)height; camera.targetTexture = rt;
        camera.Render(); RenderTexture.active = rt;
        var image = new Texture2D(width, height, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, width, height), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../" + name), image.EncodeToPNG());
        Check(image.GetPixels().Count(c => c.r > .2f || c.g > .3f) > 1000, "actual rendered effect is visible: " + name);
        Object.Destroy(image); camera.targetTexture = null; RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt);
    }
}
