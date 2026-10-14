using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Actual prefabs/shader in the isolated Unity project. No production scenes are opened or saved.
public static class WallCandleSmoke
{
    static int checks;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); checks++; }
    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Private).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
    static void Call(object o, string method, params object[] args) => o.GetType().GetMethod(method, Private).Invoke(o, args);
    // Original opaque body-pixel bounds, including both tray and mounting plate (not transparent padding).
    static readonly Rect[] OriginalBounds = {
        Rect.MinMaxRect(-.25f,-.5625f,.25f,.1875f), Rect.MinMaxRect(-.1875f,-.25f,.5625f,.3125f),
        Rect.MinMaxRect(-.3125f,-.1875f,.25f,.5625f), Rect.MinMaxRect(-.5625f,-.25f,.125f,.25f)
    };

    public static void Run()
    {
        try
        {
            var roots = new GameObject[2]; var candles = new PixelWallCandleVisual[2];
            for (int variant = 0; variant < 2; variant++)
            {
                string name = variant == 0 ? "PixelWallCandle" : "PixelGoldWallCandle";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/TestPrefabs/" + name + ".prefab");
                Check(prefab != null, name + " imports");
                var root = UnityEngine.Object.Instantiate(prefab); roots[variant] = root;
                var visual = root.GetComponent<PixelWallCandleVisual>(); candles[variant] = visual;
                Check(root.transform.localScale == new Vector3(variant == 0 ? .75f : .5f, .75f, 1), "Original different prefab scales preserved");
                Check(root.GetComponent<SpriteRenderer>() != null && root.GetComponents<MonoBehaviour>().Length == 1, "Old component references and script GUID retained");
                Check(root.GetComponentsInChildren<Collider2D>(true).Length == 0 && root.GetComponentsInChildren<Light>(true).Length == 0, "No collision or real lights added");
                Check(root.transform.Find("Outer Glow") == null && root.transform.Find("Inner Glow") == null, "No old circular halo objects generated");
                Check(root.GetComponentsInChildren<MeshRenderer>().Length == 3, "Only body, flame and local bloom meshes");
                Check(Get<bool>(visual, "useGoldMount") == (variant == 1), "Gold variant setting retained");
                for (int direction = 0; direction < 4; direction++)
                {
                    visual.SetMountingDirection((WallCandleDirection)direction);
                    Check(visual.MountingDirection == (WallCandleDirection)direction, "Public four-way API still works");
                    var body = Get<Mesh>(visual, "bodyMesh"); var bounds = body.bounds; Rect expected = OriginalBounds[direction];
                    Check(Mathf.Abs(bounds.min.x - expected.xMin) < .0001f && Mathf.Abs(bounds.max.x - expected.xMax) < .0001f
                        && Mathf.Abs(bounds.min.y - expected.yMin) < .0001f && Mathf.Abs(bounds.max.y - expected.yMax) < .0001f,
                        "Original body footprint for direction " + direction + ": " + bounds);
                    // If a pre-remake script snapshot is staged, verify the baseline against its actual raster too.
                    Type legacy = Type.GetType("LegacyWallCandleVisual, Assembly-CSharp-Editor");
                    if (legacy != null)
                    {
                        var method = legacy.GetMethod("CreateBodySprite", BindingFlags.Static | BindingFlags.NonPublic);
                        object dir = Enum.ToObject(method.GetParameters()[0].ParameterType, direction);
                        var sprite = (Sprite)method.Invoke(null, new[] { dir, (object)(variant == 1) });
                        int minX = 24, minY = 24, maxX = -1, maxY = -1;
                        var pixels = sprite.texture.GetPixels();
                        for (int y = 0; y < 24; y++) for (int x = 0; x < 24; x++) if (pixels[y * 24 + x].a > 0)
                        { minX = Math.Min(x, minX); minY = Math.Min(y, minY); maxX = Math.Max(x, maxX); maxY = Math.Max(y, maxY); }
                        Check(expected == Rect.MinMaxRect((minX - 12) / 16f, (minY - 12) / 16f, (maxX + 1 - 12) / 16f, (maxY + 1 - 12) / 16f), "Baseline matches old actual opaque pixels");
                        UnityEngine.Object.DestroyImmediate(sprite.texture); UnityEngine.Object.DestroyImmediate(sprite);
                    }
                    Check(body.vertexCount <= 68, "Simple mount geometry budget");
                    var anchor = Get<Vector2>(visual, "flameAnchor");
                    var flame = Get<Mesh>(visual, "flameMesh"); var glow = Get<Mesh>(visual, "glowMesh");
                    Check(flame.vertexCount == 1008 && glow.vertexCount == 72, "Merged geometry, no per-pixel game objects");
                    Check(flame.bounds.min.y >= anchor.y + .21874f && flame.bounds.max.y <= anchor.y + .53126f, "Flame stays inside original vertical footprint");
                    var vertices = body.vertices; var triangles = body.triangles;
                    for (int i = 0; i < triangles.Length; i += 3)
                        Check(Vector3.Cross(vertices[triangles[i + 1]] - vertices[triangles[i]], vertices[triangles[i + 2]] - vertices[triangles[i]]).z < 0, "Valid geometric body triangles");
                }
                visual.SetMountingDirection(WallCandleDirection.Up);
                root.transform.position = new Vector3(variant == 0 ? -.3f : .3f, 0, 0);
            }
            var candle = candles[0]; var flameMesh = Get<Mesh>(candle, "flameMesh"); var glowMesh = Get<Mesh>(candle, "glowMesh");
            var originalBody = Get<Mesh>(candle, "bodyMesh");
            var glowMat = Get<Material>(candle, "glowMaterial");
            Check(glowMat.shader.name == "Cogitans/Warm Light Glow" && glowMat.shader.isSupported, "Existing additive glow shader supported");
            byte[] minAlpha = new byte[252], maxAlpha = new byte[252]; for (int i = 0; i < 252; i++) minAlpha[i] = 255;
            var silhouettes = new HashSet<int>();
            for (int frame = 0; frame < 200; frame++)
            {
                Call(candle, "UpdateFlame", frame * .02f); Color32[] colors = flameMesh.colors32, glow = glowMesh.colors32;
                int hash = 17, live = 0, core = 0;
                for (int i = 0; i < 252; i++)
                {
                    Color32 color = colors[i * 4]; minAlpha[i] = Math.Min(minAlpha[i], color.a); maxAlpha[i] = Math.Max(maxAlpha[i], color.a);
                    if (color.a > 0) { live++; Check(color.r >= color.g && color.g > color.b, "Reference shape retains original warm colors"); }
                    if (color.a > 180 && color.g > 220 && color.b > 130) core++;
                    unchecked { hash = hash * 31 + (color.a > 128 ? 1 : 0); }
                }
                Check(live > 20 && core > 0, "Steady burning base and visible pale-yellow core"); silhouettes.Add(hash);
                for (int row = 0; row < 18; row++)
                {
                    bool empty = true;
                    for (int col = 0; col < 14; col++) if (colors[(row * 14 + col) * 4].a > 6) empty = false;
                    if (empty) Check(glow[row * 4].a == 0, "No residual light where flame has disappeared");
                }
            }
            int changing = 0; for (int i = 0; i < 252; i++) if (minAlpha[i] == 0 && maxAlpha[i] > 180) changing++;
            Check(changing > 20 && silhouettes.Count > 40, "Contour visibly deforms and flame fragments disappear");
            Check(originalBody == Get<Mesh>(candle, "bodyMesh") && flameMesh == Get<Mesh>(candle, "flameMesh") && glowMat == Get<Material>(candle, "glowMaterial"), "Animation reuses resources");

            var camera = new GameObject("Wall Candle Preview Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, -.045f, -10); camera.orthographic = true; camera.orthographicSize = .49f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f, .09f, .15f);
            var target = new RenderTexture(800, 560, 24); camera.targetTexture = target; camera.aspect = 800f / 560;
            var image = new Texture2D(800, 560, TextureFormat.RGBA32, false);
            foreach (var visual in candles) Call(visual, "UpdateFlame", .32f);
            var lit = Capture(camera, target, image);
            foreach (var visual in candles) Get<MeshRenderer>(visual, "flameGlowRenderer").enabled = false;
            var unlit = Capture(camera, target, image);
            int glowPixels = 0, distantGlow = 0;
            for (int y = 0; y < 560; y++) for (int x = 0; x < 800; x++)
            {
                int i = y * 800 + x;
                if (lit[i].r - unlit[i].r > .004f) { glowPixels++; if (y < 250) distantGlow++; }
            }
            Check(glowPixels > 100 && distantGlow == 0, "Only local flame bloom; no large light circle around mount");
            foreach (var visual in candles) Get<MeshRenderer>(visual, "flameGlowRenderer").enabled = true;
            Capture(camera, target, image); File.WriteAllBytes(Application.dataPath + "/../WallCandles-preview.png", image.EncodeToPNG());
            string frames = Application.dataPath + "/../wall-candle-frames"; Directory.CreateDirectory(frames);
            for (int frame = 0; frame < 48; frame++)
            {
                foreach (var visual in candles) Call(visual, "UpdateFlame", frame / 20f);
                Capture(camera, target, image); File.WriteAllBytes(Path.Combine(frames, "candle-" + frame.ToString("000") + ".png"), image.EncodeToPNG());
            }
            var mask = GameObject.CreatePrimitive(PrimitiveType.Quad); mask.transform.position = new Vector3(0, 0, -1); mask.transform.localScale = Vector3.one * 4;
            var maskMaterial = new Material(Shader.Find("Sprites/Default")); maskMaterial.color = Color.black;
            var maskRenderer = mask.GetComponent<MeshRenderer>(); maskRenderer.sharedMaterial = maskMaterial; maskRenderer.sortingOrder = 30000;
            var masked = Capture(camera, target, image);
            Check(masked[400 * 800 + 225].r < .01f && masked[280 * 800 + 570].r < .01f, "Visibility mask covers flame and mount");
            UnityEngine.Object.DestroyImmediate(mask); UnityEngine.Object.DestroyImmediate(maskMaterial);
            foreach (var root in roots)
            {
                var visual = root.GetComponent<PixelWallCandleVisual>(); var oldMesh = Get<Mesh>(visual, "bodyMesh");
                root.SetActive(false); Check(oldMesh == null, "Editor disable cleans generated assets");
                root.SetActive(true); Check(root.GetComponentsInChildren<MeshRenderer>().Length == 3, "Reenable does not duplicate effects");
                oldMesh = Get<Mesh>(visual, "bodyMesh"); UnityEngine.Object.DestroyImmediate(root); Check(oldMesh == null, "Destroy cleans assets");
            }
            camera.targetTexture = null; RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(camera.gameObject); UnityEngine.Object.DestroyImmediate(image); UnityEngine.Object.DestroyImmediate(target);
            Debug.Log("WALL_CANDLE_SMOKE_PASS checks=" + checks + " changingCells=" + changing + " silhouettes=" + silhouettes.Count + " bloomPixels=" + glowPixels);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static Color[] Capture(Camera camera, RenderTexture target, Texture2D image)
    {
        camera.Render(); RenderTexture.active = target; image.ReadPixels(new Rect(0, 0, 800, 560), 0, 0); image.Apply(); return image.GetPixels();
    }
}
