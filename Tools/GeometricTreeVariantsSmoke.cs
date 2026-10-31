using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in the isolated project. Produces an actual Unity camera render, not a mockup.
[InitializeOnLoad]
public static class GeometricTreeVariantsSmoke
{
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    static readonly string[] Names = { "GeometricTree", "GeometricBroadleafTree", "GeometricPoplarTree", "GeometricConiferTree", "GeometricUmbrellaTree" };
    static IEnumerator suite;
    static int checks, frame = -1;
    static double deadline;
    static GeometricTreeVariantsSmoke() { EditorApplication.update += Tick; }
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, F).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, F).SetValue(o, value);
    static void Build(GeometricTreeDecoration tree) => typeof(GeometricTreeDecoration).GetMethod("Build", F).Invoke(tree, null);
    public static void Run()
    {
        if (!Application.dataPath.Replace('\\', '/').Contains("/.codex-temp/")) throw new Exception("isolated only");
        PlayerSettings.productName = "geometric-tree-variants-smoke";
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Check editor creation / cleanup too: trees must be visible while laying out levels.
        for (int i = 0; i < Names.Length; i++)
        {
            GameObject prefab = Load(i);
            Check(prefab != null, Names[i] + " prefab imports");
            var editTree = Object.Instantiate(prefab).GetComponent<GeometricTreeDecoration>();
            Check(Get<Mesh>(editTree, "mesh") != null, "editor geometry appears immediately");
            var mesh = Get<Mesh>(editTree, "mesh");
            Object.DestroyImmediate(editTree.gameObject);
            Check(mesh == null, "editor destroy releases generated mesh");
        }
        SessionState.SetBool(nameof(GeometricTreeVariantsSmoke), true);
        EditorApplication.EnterPlaymode();
    }
    static GameObject Load(int index) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Decorations/Plants/" + Names[index] + ".prefab");
    static void Tick()
    {
        if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
        frame = Time.frameCount;
        try
        {
            if (SessionState.GetBool(nameof(GeometricTreeVariantsSmoke), false))
            { SessionState.SetBool(nameof(GeometricTreeVariantsSmoke), false); suite = Verify(); deadline = EditorApplication.timeSinceStartup + 120; }
            if (suite == null) return;
            if (EditorApplication.timeSinceStartup > deadline) throw new Exception("Tree tests timed out");
            if (suite.MoveNext()) return;
            suite = null; Debug.Log("TREE_VARIANTS_PASS checks=" + checks); EditorApplication.Exit(0);
        }
        catch (Exception e) { suite = null; Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Verify()
    {
        while (Time.frameCount < 3 || RetroSceneLoadReveal.IsPreparingGameplay) yield return null;
        var trees = new GeometricTreeDecoration[Names.Length];
        var meshes = new Mesh[Names.Length];
        for (int i = 0; i < Names.Length; i++)
        {
            trees[i] = Object.Instantiate(Load(i), new Vector3((i - 2) * 3.35f, 0, 0), Quaternion.identity).GetComponent<GeometricTreeDecoration>();
            var tree = trees[i]; var mesh = meshes[i] = Get<Mesh>(tree, "mesh");
            Check((int)Get<GeometricTreeDecoration.TreeShape>(tree, "shape") == i, "serialized shape " + Names[i]);
            Check(tree.GetComponentsInChildren<Renderer>(true).Length == 1, "one renderer per tree");
            Check(tree.GetComponentsInChildren<Collider2D>(true).Length == 1, "one bottom collider, no crown collider");
            Check(tree.GetComponents<MonoBehaviour>().Length == 1 && tree.GetComponentsInChildren<AudioSource>().Length == 0, "decoration only, no gameplay or sound");
            var collider = tree.GetComponent<BoxCollider2D>();
            Check(collider.enabled && !collider.isTrigger && collider.size.x == Get<float>(tree, "trunkWidth") &&
                Mathf.Abs(collider.size.y - .26f) < .001f && Mathf.Abs(collider.offset.y - .13f) < .001f, "collision confined to trunk foot");
            Check(mesh.vertexCount <= 80 && mesh.subMeshCount == 1, "small mesh budget");
            Check(mesh.bounds.min.y >= -.001f && mesh.bounds.max.y > 3f && mesh.bounds.max.y < 4f, "same scene scale and root pivot");
            var renderer = tree.GetComponentInChildren<MeshRenderer>();
            Check(renderer.sortingOrder == 5 && renderer.sortingLayerName == "Default" && tree.gameObject.layer == 0,
                "same occlusion order and layer as original");
            Check(renderer.sharedMaterial.shader.name == "Sprites/Default", "existing unlit vertex-colour material");
            Check(mesh.colors.All(c => c.a == 1f), "opaque flat colours");
            var points = mesh.vertices; var triangles = mesh.triangles;
            Check(triangles.Length % 3 == 0, "complete triangles");
            for (int t = 0; t < triangles.Length; t += 3)
            {
                Check(triangles[t] >= 0 && triangles[t] < points.Length && triangles[t + 1] < points.Length && triangles[t + 2] < points.Length, "valid indices");
                Vector3 area = Vector3.Cross(points[triangles[t + 1]] - points[triangles[t]], points[triangles[t + 2]] - points[triangles[t]]);
                Check(area.z > .000001f, "positive nondegenerate triangle");
            }
            tree.gameObject.SetActive(false);
            Check(!Get<GameObject>(tree, "visual").activeSelf && Get<Mesh>(tree, "mesh") == mesh, "distance unload retains mesh without rendering");
            tree.gameObject.SetActive(true);
            Check(Get<GameObject>(tree, "visual").activeSelf && Get<Mesh>(tree, "mesh") == mesh, "distance reload reuses geometry");
        }
        // Default enum value must reproduce the exact classic tree, not merely its bounds.
        Check(meshes[0].vertexCount == 26 && meshes[0].triangles.Length == 48, "original vertex/triangle count unchanged");
        int start = 4;
        foreach (string part in new[] { "CrownOutline", "UpperOutline", "LeftLeafFacet", "RightLeafFacet" })
        {
            var outline = (Vector2[])typeof(GeometricTreeDecoration).GetField(part, F).GetValue(null);
            foreach (Vector2 p in outline)
            {
                Vector2 expected = new Vector2(0, 2.2f) + Vector2.Scale(p, new Vector2(1.9f, 1.7f));
                Check((meshes[0].vertices[start++] - (Vector3)expected).sqrMagnitude < .00000001f, "legacy crown geometry preserved");
            }
        }
        Check(meshes.Select(m => m.bounds.size).Distinct().Count() == 5, "all five silhouettes have distinct proportions");
        yield return null; yield return null;
        for (int i = 0; i < trees.Length; i++) Check(Get<Mesh>(trees[i], "mesh") == meshes[i], "no per-frame mesh rebuild");
        Preview();
        var editable = trees[1];
        Set(editable, "crownColor", Color.red); Set(editable, "solidBase", false); Build(editable);
        Check(Get<Mesh>(editable, "mesh").colors.Contains(Color.red) && !editable.GetComponent<BoxCollider2D>().enabled, "editable colours and collision option work");
        yield return null;
        Check(editable.GetComponentsInChildren<MeshRenderer>().Length == 1, "rebuild has no duplicate renderer");
        var finalMesh = Get<Mesh>(editable, "mesh"); var material = Get<Material>(editable, "material");
        foreach (var tree in trees) Object.Destroy(tree.gameObject);
        yield return null; yield return null;
        Check(finalMesh == null && material == null, "generated resources released");
    }
    static void Preview()
    {
        var cameraObject = new GameObject("Tree Preview Camera");
        var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
        camera.orthographic = true; camera.orthographicSize = 2.45f;
        camera.transform.position = new Vector3(0, 1.9f, -10);
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.047f, .122f, .188f, 1);
        var target = RenderTexture.GetTemporary(1800, 520, 24, RenderTextureFormat.ARGB32);
        var previous = RenderTexture.active;
        camera.targetTexture = target; camera.aspect = 1800f / 520f; camera.Render();
        RenderTexture.active = target;
        var image = new Texture2D(1800, 520, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1800, 520), 0, 0); image.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "../GeometricTreeVariants-preview.png"), image.EncodeToPNG());
        Object.Destroy(image); camera.targetTexture = null; RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(target); Object.Destroy(cameraObject);
    }
}
