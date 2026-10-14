using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class VisionOriginRecoverySmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    static IEnumerator suite;
    static int frame = -1, checks;
    static double deadline;
    static readonly List<GameObject> owned = new List<GameObject>();
    static Camera camera;
    static CameraCircularVision vision;
    static ZeldaFourWayMover mover;
    static int blocks, hidden;
    static VisionOriginRecoverySmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); }
    static object Call(object obj, string name, params object[] args) => obj.GetType().GetMethod(name, F).Invoke(obj, args);
    static void Set(object obj, string name, object value)
    {
        for (Type t = obj.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, F | BindingFlags.DeclaredOnly); if (f != null) { f.SetValue(obj, value); return; } }
        throw new Exception(name);
    }
    static GameObject Own(GameObject go) { owned.Add(go); return go; }
    public static void Run()
    {
        Check(Application.dataPath.Replace('\\','/').Contains("/.codex-temp/"), "isolated test project");
        PlayerSettings.productName = "vision-origin-recovery-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(nameof(VisionOriginRecoverySmoke), true); EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return; frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(VisionOriginRecoverySmoke), false))
            { SessionState.SetBool(nameof(VisionOriginRecoverySmoke), false); deadline = EditorApplication.timeSinceStartup + 90; suite = Verify(); }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Vision tests timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("VISION_ORIGIN_RECOVERY_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static ZeldaFourWayMover Actor(Vector2 pos)
    {
        var go = Own(new GameObject("Controlled vision body")); go.SetActive(false); go.transform.position = pos;
        go.AddComponent<BoxCollider2D>().size = Vector2.one * .5f;
        var data = go.AddComponent<ZeldaCharacterData>(); Set(data, "playerVisionRadius", 5f);
        var result = go.AddComponent<ZeldaFourWayMover>(); go.SetActive(true);
        ZeldaRuntimeRegistry.ClaimControlledMover(result); return result;
    }
    static BoxCollider2D Wall(Vector2 position, Vector2 size, int layer = -1)
    {
        var go = Own(new GameObject("Vision test wall")); go.layer = layer < 0 ? blocks : layer; go.transform.position = position;
        var col = go.AddComponent<BoxCollider2D>(); col.size = size;
        return col;
    }
    static Vector2 Place(Vector2 point, bool force = true)
    {
        mover.transform.position = point; Physics2D.SyncTransforms();
        Call(vision, "UpdateVision", force);
        return (Vector2)Call(vision, "GetPrimaryVisionOrigin");
    }
    static float MeshArea()
    {
        Check(vision.TryGetPostEffectVisibilityMesh(out var mesh, out var matrix), "mask mesh is active");
        float area = 0; var verts = mesh.vertices; var tris = mesh.triangles;
        for (int i = 0; i < tris.Length; i += 3)
        {
            Vector2 a = matrix.MultiplyPoint3x4(verts[tris[i]]), b = matrix.MultiplyPoint3x4(verts[tris[i+1]]), c = matrix.MultiplyPoint3x4(verts[tris[i+2]]);
            area += Mathf.Abs((b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x))*.5f;
        }
        return area;
    }
    static IEnumerator Verify()
    {
        Time.timeScale = 0;
        blocks = LayerMask.NameToLayer("Blocks"); hidden = LayerMask.NameToLayer("Hidden Blocks");
        Check(blocks >= 0 && hidden >= 0, "production occluder layers");
        mover = Actor(new Vector2(-.8f, 0));
        camera = Own(new GameObject("Vision fixture camera")).AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 4; camera.transform.position = new Vector3(0,0,-10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.1f,.3f,.6f);
        var wall = Wall(Vector2.zero, new Vector2(.3f, 24));
        Physics2D.SyncTransforms(); vision = camera.gameObject.AddComponent<CameraCircularVision>();
        bool originalStartsInside = Physics2D.queriesStartInColliders;
        Physics2D.queriesStartInColliders = true;
        // Reproduce the original zero-distance fan collapse with the uncorrected origin.
        var rawHit = Physics2D.Raycast(Vector2.zero, Vector2.left, 5, 1 << blocks);
        Check(rawHit.collider == wall && rawHit.distance == 0, "raw inside-wall ray collapses at distance zero");
        Check(MeshArea() > 20, "ordinary visible half disc");
        foreach (float x in new[] {-.18f, -.151f, -.149f, -.12f, -.05f, 0f, .1f, .149f})
        {
            Vector2 origin = Place(new Vector2(x, 0));
            Check(origin.x < -.18f && !wall.OverlapPoint(origin), "keep near-side origin at contact x=" + x);
            Check(mover.transform.position.x == x, "only vision origin changes; body position preserved");
            Check(MeshArea() > 20, "no degenerate/all-dark fan at x=" + x);
            Check(vision.IsWorldPositionVisible(new Vector3(-1,0)) && !vision.IsWorldPositionVisible(new Vector3(1,0)), "near side visible; no through-wall vision");
            Check(vision.IsWorldBoundsVisible(new Bounds(new Vector3(-1,0), Vector3.one*.2f), null), "streaming shares safe origin");
        }
        RenderPreview();
        for (int i = 1; i <= 24; i++)
        {
            Vector2 requested = new Vector2(-.1f, i * .2f);
            Vector2 sliding = Place(requested, false);
            Check(sliding.x < -.18f && Mathf.Abs(sliding.y - requested.y) < .04f,
                "vision follows tangential sliding: step=" + i + " requested=" + requested + " actual=" + sliding);
            yield return null;
        }
        Vector2 recovered = Place(new Vector2(-.6f, 0), false);
        Check(recovered == new Vector2(-.6f,0), "free origin returns immediately without sticky offset");
        Check(MeshArea() > 20, "recovery forces valid mesh despite rebuild interval");
        camera.transform.position = new Vector3(0,0,-10);
        Check((Vector2)Call(vision,"GetPrimaryVisionOrigin") == recovered, "camera smoothing/shake does not change actor origin");

        wall.transform.rotation = Quaternion.Euler(0,0,37); CameraCircularVision.NotifyBlockersChanged(); Physics2D.SyncTransforms();
        Vector2 normal = wall.transform.right;
        Place(-normal*.8f);
        Vector2 tilted = Place(normal*.08f);
        Check(Vector2.Dot(tilted,normal) < -.18f && !wall.OverlapPoint(tilted), "rotated wall keeps approach side");
        Check(vision.IsWorldPositionVisible(-normal) && !vision.IsWorldPositionVisible(normal), "rotated occlusion retained");

        wall.transform.rotation = Quaternion.identity;
        var corner = Wall(Vector2.zero, new Vector2(24,.3f)); CameraCircularVision.NotifyBlockersChanged();
        Place(new Vector2(-.8f,-.8f)); Vector2 cornerOrigin = Place(new Vector2(-.04f,-.04f));
        Check(cornerOrigin.x < -.18f && cornerOrigin.y < -.18f, "two-wall corner resolves both blockers");
        Check(MeshArea() > 10, "corner retains a visible quarter disc");
        Object.DestroyImmediate(corner.gameObject);

        // No history after reload/possession, and closing geometry invalidating old history.
        var resolver = new CameraVisionOriginResolver();
        Vector2 first = resolver.Resolve(mover.transform,new Vector2(-.14f,0),1<<blocks,.03f,1);
        Check(first.x < -.18f && !wall.OverlapPoint(first), "initial overlap recovers locally without history");
        wall.transform.position = new Vector2(first.x,0); Physics2D.SyncTransforms();
        Vector2 closed = resolver.Resolve(mover.transform,new Vector2(-.14f,0),1<<blocks,.03f,2);
        Check(!wall.OverlapPoint(closed), "door moving across cached origin recovers on geometry revision");
        Object.DestroyImmediate(wall.gameObject);

        var circleGo = Own(new GameObject("Round wall")); circleGo.layer = blocks;
        var circle = circleGo.AddComponent<CircleCollider2D>(); circle.radius = .6f;
        CameraCircularVision.NotifyBlockersChanged(); Place(new Vector2(-1,0));
        Vector2 round = Place(new Vector2(-.58f,0));
        Check(round.x < -.63f && !circle.OverlapPoint(round), "curved collider recovery");
        Object.DestroyImmediate(circleGo);
        var polyGo = Own(new GameObject("Concave wall")); polyGo.layer = blocks;
        var poly = polyGo.AddComponent<PolygonCollider2D>();
        poly.points = new[] {new Vector2(-1,-1),new Vector2(1,-1),new Vector2(1,1),new Vector2(.4f,1),new Vector2(.4f,-.4f),new Vector2(-1,-.4f)};
        CameraCircularVision.NotifyBlockersChanged(); Place(Vector2.zero);
        Vector2 concave = Place(new Vector2(.45f,0));
        Check(concave.x < .36f && !poly.OverlapPoint(concave), "concave polygon notch uses real geometry, not bounds");
        Object.DestroyImmediate(polyGo);

        wall = Wall(Vector2.zero,new Vector2(.3f,24), hidden); wall.isTrigger = true;
        CameraCircularVision.NotifyBlockersChanged(); Place(new Vector2(-.8f,0));
        Check(Place(new Vector2(-.05f,0)).x < -.18f, "Hidden Blocks triggers remain occluders");
        Object.DestroyImmediate(wall.gameObject);
        wall = Wall(Vector2.zero,new Vector2(.3f,24), LayerMask.NameToLayer("Visible Non Blocking"));
        CameraCircularVision.NotifyBlockersChanged();
        Check(Place(Vector2.zero) == Vector2.zero && vision.IsWorldPositionVisible(Vector2.right), "Visible Non Blocking does not displace origin or block vision");
        Object.DestroyImmediate(wall.gameObject);

        // Teleports/control switches clear the old room's origin; open-space behavior is exact.
        for (int i = 0; i < 30; i++)
        {
            Vector2 desired = new Vector2(i*.15f, Mathf.Sin(i));
            Check(Place(desired) == desired, "normal movement unchanged");
        }
        Check(Place(new Vector2(80,40)) == new Vector2(80,40), "teleport does not retain old origin");
        mover.enabled = false; mover = Actor(new Vector2(-.8f,0));
        wall = Wall(Vector2.zero,new Vector2(.3f,24)); CameraCircularVision.NotifyBlockersChanged();
        Place(new Vector2(-.8f,0)); Check(Place(Vector2.zero).x < -.18f, "new controlled body has independent near-side history");

        // Additional puppet reveal also uses a safe origin when the body is off camera.
        Place(new Vector2(-20,0));
        var extra = Own(new GameObject("Puppet reveal")).AddComponent<CameraVisionRevealSource>(); extra.Configure(3);
        extra.transform.position = new Vector3(-.8f,0); Call(vision,"UpdateVision",true);
        extra.transform.position = Vector3.zero; Physics2D.SyncTransforms(); Call(vision,"UpdateVision",true);
        Check(vision.IsWorldPositionVisible(new Vector3(-1,0)) && !vision.IsWorldPositionVisible(new Vector3(1,0)), "additional reveal recovers without seeing through wall");
        Object.DestroyImmediate(extra.gameObject);
        Physics2D.queriesStartInColliders = false;
        Place(new Vector2(-.8f,0)); Check(Place(Vector2.zero).x < -.18f, "safe with queriesStartInColliders disabled too");
        Check(!Physics2D.queriesStartInColliders, "resolver never changes global physics query policy");
        Physics2D.queriesStartInColliders = originalStartsInside;

        resolver.Reset(); Vector2 open = new Vector2(-3,0);
        resolver.Resolve(mover.transform,open,1<<blocks,.03f,20);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) resolver.Resolve(mover.transform,open,1<<blocks,.03f,20);
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "repeated source queries allocate zero managed memory");
        yield return null;
        foreach(var go in owned) if(go!=null) Object.DestroyImmediate(go);
    }
    static void RenderPreview()
    {
        var rt = new RenderTexture(640,360,24); camera.targetTexture = rt;
        Call(vision,"UpdateVision",true); camera.Render();
        var previous = RenderTexture.active; RenderTexture.active = rt;
        var texture = new Texture2D(640,360,TextureFormat.RGB24,false);
        texture.ReadPixels(new Rect(0,0,640,360),0,0); texture.Apply();
        Vector3 near = camera.WorldToViewportPoint(new Vector3(-1,0));
        Vector3 far = camera.WorldToViewportPoint(new Vector3(1,0));
        Color visible = texture.GetPixel((int)(near.x*640),(int)(near.y*360));
        Color occluded = texture.GetPixel((int)(far.x*640),(int)(far.y*360));
        Check(visible.b > occluded.b + .2f, "GPU stencil reveals near side but masks opposite side while body is inside wall");
        File.WriteAllBytes(Path.Combine(Application.dataPath,"../VisionOriginRecovery-preview.png"),texture.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = previous;
        Object.DestroyImmediate(texture); Object.DestroyImmediate(rt);
    }
}
