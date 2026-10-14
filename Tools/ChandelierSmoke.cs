using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Run only in the isolated Unity validation project; never opens/saves a gameplay scene.
public static class ChandelierSmoke
{
    static int checks;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); checks++; }
    static T Get<T>(object owner, string field) => (T)owner.GetType().GetField(field, Private).GetValue(owner);
    static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
    static void Call(object owner, string method, params object[] args) => owner.GetType().GetMethod(method, Private).Invoke(owner, args);

    public static void Run()
    {
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/HangingChandelier.prefab");
            Check(prefab != null, "Real production prefab imports");
            var root = UnityEngine.Object.Instantiate(prefab);
            var lamp = root.GetComponent<GeometricHangingChandelier>();
            Check(lamp != null && root.GetComponents<MonoBehaviour>().Length == 1, "Only decorative script");
            Check(root.GetComponentsInChildren<Collider2D>(true).Length == 0, "No movement/vision collider");
            Check(root.GetComponentsInChildren<Light>(true).Length == 0, "No expensive realtime light");
            Check(root.GetComponentsInChildren<MeshRenderer>().Length == 3, "Only three compact draw meshes");
            var body = Get<Mesh>(lamp, "bodyMesh");
            var halo = Get<Mesh>(lamp, "haloMesh");
            var core = Get<Mesh>(lamp, "coreMesh");
            Check(body.vertexCount == 492 && halo.vertexCount == 28 && core.vertexCount == 48, "Expected vertex budget");
            Check(Mathf.Abs(body.bounds.center.x) < .001f && Mathf.Abs(body.bounds.center.y) < .001f, "Centered pivot");
            Check(body.bounds.size.x <= 2.001f && Mathf.Abs(body.bounds.size.y - 2) < .001f, "Two-unit body footprint");
            foreach (var mesh in new[] { body, halo, core })
            {
                var vertices = mesh.vertices; var triangles = mesh.triangles;
                Check(vertices.Length == mesh.colors.Length && vertices.Length == mesh.uv.Length, "Complete color and UV buffers");
                for (int i = 0; i < triangles.Length; i += 3)
                    Check(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).z < 0,
                        "Nondegenerate consistent front-facing triangles");
            }
            var glowMaterial = Get<Material>(lamp, "glowMaterial");
            Check(glowMaterial.shader.name == "Cogitans/Warm Light Glow" && glowMaterial.shader.isSupported, "Existing warm additive shader active");
            Check(glowMaterial.GetFloat("_RadialFalloff") == 1, "Radial falloff enabled, not glowing squares");
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
                Check(renderer.gameObject.layer == 0 && renderer.sortingOrder >= 4 && renderer.sortingOrder <= 6,
                    "Hanging above characters but below visibility mask");

            Set(lamp, "phase", 0f);
            float lowest = float.MaxValue, highest = 0, darkestTime = 0, brightestTime = 0, last = 0;
            var properties = new MaterialPropertyBlock();
            var haloRenderer = Get<MeshRenderer>(lamp, "haloRenderer");
            for (int frame = 0; frame < 200; frame++)
            {
                float time = frame * .02f;
                Call(lamp, "ApplyGlow", time); haloRenderer.GetPropertyBlock(properties);
                float value = properties.GetFloat("_Intensity");
                Check(value > 1 && value < 4, "Breathing never blacks out or flashes excessively");
                if (frame > 0) Check(Mathf.Abs(value - last) < .12f, "Smooth temporal change");
                if (value < lowest) { lowest = value; darkestTime = time; }
                if (value > highest) { highest = value; brightestTime = time; }
                last = value;
            }
            Check(highest - lowest > 1, "Noticeable dynamic glow amplitude");
            Check(body == Get<Mesh>(lamp, "bodyMesh") && halo == Get<Mesh>(lamp, "haloMesh") && glowMaterial == Get<Material>(lamp, "glowMaterial"),
                "Animation reuses meshes and material");

            var camera = new GameObject("Preview Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true; camera.orthographicSize = 1.65f;
            camera.backgroundColor = new Color(.07f, .10f, .18f); camera.clearFlags = CameraClearFlags.SolidColor;
            var target = new RenderTexture(640, 640, 24); camera.targetTexture = target; camera.aspect = 1;
            var image = new Texture2D(640, 640, TextureFormat.RGBA32, false);
            Call(lamp, "ApplyGlow", brightestTime); var bright = Capture(camera, target, image);
            Call(lamp, "ApplyGlow", darkestTime); var dim = Capture(camera, target, image);
            int varying = 0, haloPixels = 0;
            for (int y = 0; y < 640; y++) for (int x = 0; x < 640; x++)
            {
                int index = y * 640 + x;
                if (bright[index].r > dim[index].r + .008f) varying++;
                float radius = new Vector2(x - 320, y - 320).magnitude / 640 * 3.3f;
                if (radius > 1.02f && radius < 1.2f && bright[index].r > dim[index].r + .006f) haloPixels++;
            }
            Check(varying > 10000 && haloPixels > 500, "GPU shows breathing and soft light beyond silhouette");
            Call(lamp, "ApplyGlow", 0f); Capture(camera, target, image);
            File.WriteAllBytes(Application.dataPath + "/../HangingChandelier-preview.png", image.EncodeToPNG());
            string frames = Application.dataPath + "/../chandelier-frames";
            Directory.CreateDirectory(frames);
            for (int frame = 0; frame < 48; frame++)
            {
                Call(lamp, "ApplyGlow", frame * 3.2f / 48); Capture(camera, target, image);
                File.WriteAllBytes(Path.Combine(frames, "glow-" + frame.ToString("000") + ".png"), image.EncodeToPNG());
            }
            var mask = GameObject.CreatePrimitive(PrimitiveType.Quad);
            mask.transform.position = new Vector3(0, 0, -1); mask.transform.localScale = new Vector3(5, 5, 1);
            var maskMaterial = new Material(Shader.Find("Sprites/Default")); maskMaterial.color = Color.black;
            var maskRenderer = mask.GetComponent<MeshRenderer>(); maskRenderer.sharedMaterial = maskMaterial; maskRenderer.sortingOrder = 30000;
            var masked = Capture(camera, target, image);
            Check(masked[320 * 640 + 320].r < .01f && masked[480 * 640 + 320].r < .01f, "Vision mask covers body and glow");
            UnityEngine.Object.DestroyImmediate(mask); UnityEngine.Object.DestroyImmediate(maskMaterial);

            Set(lamp, "glowIntensity", 0f); Call(lamp, "ApplyGlow", 1f);
            Check(!haloRenderer.enabled && !Get<MeshRenderer>(lamp, "coreRenderer").enabled, "Zero intensity disables glow renderers");
            Set(lamp, "glowIntensity", 2.4f);
            root.transform.localScale = new Vector3(1.5f, .7f, 1); root.transform.rotation = Quaternion.Euler(0, 0, 35);
            Check(root.GetComponentsInChildren<Collider2D>().Length == 0, "Transform cannot introduce collision");
            root.SetActive(false);
            Check(body == null && halo == null && core == null && glowMaterial == null, "Editor disable releases generated resources");
            root.SetActive(true); Call(lamp, "Update");
            Check(root.GetComponentsInChildren<MeshRenderer>().Length == 3, "Reenable rebuilds exactly once");
            var nextBody = Get<Mesh>(lamp, "bodyMesh"); Call(lamp, "OnValidate"); Call(lamp, "Update");
            Check(nextBody == null && root.GetComponentsInChildren<MeshRenderer>().Length == 3, "Inspector edit releases old meshes");
            nextBody = Get<Mesh>(lamp, "bodyMesh"); UnityEngine.Object.DestroyImmediate(root);
            Check(nextBody == null, "Destroy releases resources");
            camera.targetTexture = null; RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("CHANDELIER_SMOKE_PASS checks=" + checks + " haloPixels=" + haloPixels + " varyingPixels=" + varying);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static Color[] Capture(Camera camera, RenderTexture target, Texture2D image)
    {
        camera.Render(); RenderTexture.active = target;
        image.ReadPixels(new Rect(0, 0, 640, 640), 0, 0); image.Apply(); return image.GetPixels();
    }
}
