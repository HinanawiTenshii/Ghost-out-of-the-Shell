using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Run in the isolated validation project. Build produces ordinary static assets;
// no generator/behaviour needs to ship on the cactus or run every frame.
public static class CactusPrefabSmoke
{
    const string Folder = "Assets/Prefabs/Decorations/Plants/";
    const string PrefabPath = Folder + "GeometricCactus.prefab";
    const string MeshPath = Folder + "GeometricCactus-Mesh.asset";
    static readonly List<Vector3> vertices = new List<Vector3>();
    static readonly List<Color> colors = new List<Color>();
    static readonly List<int> triangles = new List<int>();
    static readonly Color Shadow = new Color(.09f, .42f, .30f);
    static readonly Color Green = new Color(.12f, .70f, .40f);
    static readonly Color Light = new Color(.22f, .83f, .48f);
    static readonly Color Rib = new Color(.43f, .85f, .58f);
    static readonly Color Thorn = new Color(.68f, .85f, .65f);
    static int checks;

    public static void Build()
    {
        try
        {
            // Refuse to overwrite an artist-edited prefab. Regeneration is only
            // intended for a fresh isolated project, not the production scene.
            if (File.Exists(PrefabPath) || File.Exists(MeshPath))
                throw new InvalidOperationException("Cactus assets already exist; use Run to validate them.");
            vertices.Clear(); colors.Clear(); triangles.Clear();
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();

            // Two broad, staggered arms and a rounded/chamfered central stem.
            // Flat facets match GeometricTree; sparse small dash-shaped thorns
            // sit within the silhouette instead of forming a spiky outline.
            Bevel(-.88f, .64f, -.13f, 1.03f, .10f, Shadow);
            Bevel(.13f, .92f, .88f, 1.31f, .10f, Shadow);
            Bevel(-.88f, .73f, -.52f, 1.59f, .10f, Shadow);
            Bevel(.52f, 1.01f, .88f, 1.90f, .10f, Shadow);
            Bevel(-.82f, .75f, -.13f, 1.03f, .07f, Green);
            Bevel(.13f, 1.03f, .82f, 1.31f, .07f, Green);
            Bevel(-.82f, .80f, -.56f, 1.59f, .08f, Green);
            Bevel(.56f, 1.08f, .82f, 1.90f, .08f, Green);
            Rect(-.80f, 1.00f, -.74f, 1.49f, Light);
            Rect(.58f, 1.28f, .64f, 1.80f, Light);

            Bevel(-.30f, 0f, .30f, 2.30f, .13f, Shadow);
            Bevel(-.25f, .08f, .22f, 2.30f, .12f, Green);
            Poly(Light, -.25f, .18f, -.11f, .18f, -.11f, 2.27f,
                -.13f, 2.27f, -.25f, 2.15f);
            Rect(.09f, .18f, .14f, 2.14f, Shadow);
            Rect(-.09f, .29f, -.045f, 2.13f, Rib);
            Rect(-.71f, 1.07f, -.67f, 1.45f, Rib);
            Rect(.69f, 1.36f, .73f, 1.74f, Rib);
            // Restrained highlights on the upward-facing caps.
            Rect(-.12f, 2.21f, .08f, 2.26f, Light);
            Rect(-.73f, 1.50f, -.65f, 1.54f, Light);
            Rect(.64f, 1.81f, .74f, 1.85f, Light);
            foreach (float y in new[] { .48f, .98f, 1.48f, 1.98f })
                Rect(-.20f, y, -.14f, y + .035f, Thorn);
            foreach (float y in new[] { .72f, 1.22f, 1.72f })
                Rect(.015f, y, .065f, y + .035f, Thorn);
            Rect(-.64f, 1.22f, -.59f, 1.255f, Thorn);
            Rect(.75f, 1.54f, .80f, 1.575f, Thorn);

            var mesh = new Mesh { name = "GeometricCactus" };
            mesh.SetVertices(vertices); mesh.SetColors(colors);
            mesh.uv = new Vector2[vertices.Count];
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, MeshPath);
            var root = new GameObject("GeometricCactus");
            root.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
            renderer.sortingOrder = 5; // Same as GeometricTree: above actors, below the vision overlay.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            var collider = root.AddComponent<BoxCollider2D>();
            collider.size = new Vector2(.48f, .24f);
            collider.offset = new Vector2(0f, .12f); // Root/pivot is planted at the foot.
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
            AssetDatabase.SaveAssets();
            Run();
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Check(bool condition, string label)
    {
        if (!condition) throw new Exception(label);
        checks++;
    }

    public static void Run()
    {
        try
        {
            checks = 0;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            Check(prefab, "Prefab imports");
            var root = UnityEngine.Object.Instantiate(prefab);
            var renderer = root.GetComponent<MeshRenderer>();
            var mesh = root.GetComponent<MeshFilter>().sharedMesh;
            var collider = root.GetComponent<BoxCollider2D>();
            Check(root.GetComponentsInChildren<MonoBehaviour>(true).Length == 0, "No behaviour/interaction/damage scripts");
            Check(root.GetComponentsInChildren<Renderer>(true).Length == 1, "Single shared static mesh renderer");
            Check(root.GetComponentsInChildren<Collider2D>(true).Length == 1, "Exactly one base collider");
            Check(root.GetComponentsInChildren<Rigidbody2D>(true).Length == 0, "Static decoration, no simulated body");
            Check(!collider.isTrigger && collider.enabled, "Solid base");
            Check(collider.size == new Vector2(.48f, .24f) && collider.offset == new Vector2(0, .12f), "Collision restricted to bottom");
            Check(renderer.sortingLayerName == "Default" && renderer.sortingOrder == 5 && root.layer == 0, "Matches tree layer and sorting");
            Check(mesh.vertexCount < 200 && mesh.subMeshCount == 1, "Low geometry cost");
            Check(Mathf.Abs(mesh.bounds.size.x - 1.76f) < .001f && Mathf.Abs(mesh.bounds.size.y - 2.30f) < .001f, "Expected cactus dimensions");
            Check(mesh.bounds.min.y == 0 && Mathf.Abs(mesh.bounds.center.x) < .001f, "Foot pivot");
            Check(mesh.colors.Length == mesh.vertexCount && mesh.uv.Length == mesh.vertexCount, "Complete flat-color mesh");
            Physics2D.SyncTransforms();
            Check(collider.OverlapPoint(new Vector2(0, .12f)), "Foot blocks characters");
            foreach (var point in new[] { new Vector2(0, .50f), new Vector2(0, 1.5f), new Vector2(-.70f, 1.2f), new Vector2(.70f, 1.5f) })
                Check(!collider.OverlapPoint(point), "Upper stem and arms do not block movement");
            root.SetActive(false); root.SetActive(true);
            Check(root.GetComponent<MeshFilter>().sharedMesh == mesh, "Streaming reactivation reuses asset without allocations");
            var copy = UnityEngine.Object.Instantiate(prefab);
            Check(copy.GetComponent<MeshFilter>().sharedMesh == mesh && copy.GetComponent<MeshRenderer>().sharedMaterial == renderer.sharedMaterial,
                "Instances share mesh and material");
            UnityEngine.Object.DestroyImmediate(copy);

            var camera = new GameObject("Cactus Preview Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 1.15f, -10);
            camera.orthographic = true; camera.orthographicSize = 1.50f; camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.07f, .10f, .16f);
            var rt = new RenderTexture(640, 640, 24); camera.targetTexture = rt;
            var image = new Texture2D(640, 640, TextureFormat.RGB24, false);
            Capture(camera, rt, image);
            File.WriteAllBytes(Application.dataPath + "/../Cactus-preview.png", image.EncodeToPNG());
            int greenPixels = 0;
            foreach (var pixel in image.GetPixels()) if (pixel.g > .35f && pixel.g > pixel.r * 1.2f) greenPixels++;
            Check(greenPixels > 50000, "Actual GPU render has readable green cactus silhouette");

            var actor = GameObject.CreatePrimitive(PrimitiveType.Quad);
            actor.transform.position = new Vector3(0, 1.15f, 0);
            actor.transform.localScale = new Vector3(1.1f, .35f, 1);
            var actorMaterial = new Material(Shader.Find("Sprites/Default")) { color = Color.red };
            var actorRenderer = actor.GetComponent<MeshRenderer>();
            actorRenderer.sharedMaterial = actorMaterial; actorRenderer.sortingOrder = 1;
            Capture(camera, rt, image);
            Check(image.GetPixel(320, 320).g > image.GetPixel(320, 320).r, "Cactus visually occludes character layer at upper body");
            Check(image.GetPixel(230, 320).r > .9f, "Character still visible outside silhouette");
            actor.transform.localScale = new Vector3(5, 5, 1);
            actorMaterial.color = Color.black; actorRenderer.sortingOrder = 30000;
            Capture(camera, rt, image);
            Check(image.GetPixel(320, 320).grayscale < .01f, "Vision overlay covers cactus normally");
            UnityEngine.Object.DestroyImmediate(actor); UnityEngine.Object.DestroyImmediate(actorMaterial);

            root.transform.position = new Vector3(3, -2, 0);
            root.transform.rotation = Quaternion.Euler(0, 0, 90);
            root.transform.localScale = new Vector3(1.5f, 2f, 1);
            Physics2D.SyncTransforms();
            Check(collider.OverlapPoint(root.transform.TransformPoint(0, .12f, 0)), "Base collision follows scaling/rotation");
            Check(!collider.OverlapPoint(root.transform.TransformPoint(0, 1.2f, 0)), "Transformed upper body remains non-colliding");
            camera.targetTexture = null; RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("CACTUS_PREFAB_PASS checks=" + checks + " vertices=" + mesh.vertexCount);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void Capture(Camera camera, RenderTexture target, Texture2D image)
    {
        camera.Render(); RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply();
    }

    static void Rect(float x0, float y0, float x1, float y1, Color color)
        => Poly(color, x0, y0, x1, y0, x1, y1, x0, y1);

    static void Bevel(float x0, float y0, float x1, float y1, float cut, Color color)
        => Poly(color, x0 + cut, y0, x1 - cut, y0, x1, y0 + cut, x1, y1 - cut,
            x1 - cut, y1, x0 + cut, y1, x0, y1 - cut, x0, y0 + cut);

    static void Poly(Color color, params float[] points)
    {
        int start = vertices.Count;
        for (int i = 0; i < points.Length; i += 2)
        {
            vertices.Add(new Vector3(points[i], points[i + 1], 0)); colors.Add(color);
        }
        for (int i = 1; i < points.Length / 2 - 1; i++)
        { triangles.Add(start); triangles.Add(start + i + 1); triangles.Add(start + i); }
    }
}
