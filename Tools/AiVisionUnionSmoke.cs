using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Real GPU render/readback regression, not only shader-text assertions.
public static class AiVisionUnionSmoke
{
    static readonly List<Object> owned = new List<Object>();
    static Camera camera;
    static RenderTexture target;
    static int checks;
    static Color blue = new Color(.45f, .8f, 1f, .22f), red = new Color(.88f, .18f, .12f, .16f);
    static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    static T Own<T>(T value) where T : Object { owned.Add(value); return value; }
    static MeshRenderer Quad(Shader shader, Color color, Vector2 center, Vector2 size, int order, int queue = -1)
    {
        var go = Own(new GameObject("Render test quad")); go.transform.position = center;
        var mesh = Own(new Mesh());
        mesh.vertices = new[] { new Vector3(-size.x, -size.y) * .5f, new Vector3(-size.x, size.y) * .5f,
            new Vector3(size.x, size.y) * .5f, new Vector3(size.x, -size.y) * .5f };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.colors = new[] { Color.white, Color.white, Color.white, Color.white };
        mesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
        mesh.RecalculateBounds(); go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var material = Own(new Material(shader)); if (material.HasProperty("_Color")) material.color = color;
        if (queue >= 0) material.renderQueue = queue;
        var renderer = go.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = order;
        return renderer;
    }
    static Color Sample(float x = 0, float y = 0)
    {
        camera.Render();
        var previous = RenderTexture.active;
        RenderTexture.active = target;
        var texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); texture.Apply();
        Color result = texture.GetPixel(Mathf.RoundToInt((x / 6f + .5f) * 255), Mathf.RoundToInt((y / 6f + .5f) * 255));
        Object.DestroyImmediate(texture); RenderTexture.active = previous;
        return result;
    }
    static void Equal(Color a, Color b, string message)
    {
        Check(Mathf.Abs(a.r - b.r) < .012f && Mathf.Abs(a.g - b.g) < .012f && Mathf.Abs(a.b - b.b) < .012f,
            message + " expected=" + b + " actual=" + a);
    }
    public static void Run()
    {
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var shader = Resources.Load<Shader>("Shaders/AiVisionUnion");
            Check(shader != null && shader.isSupported && !ShaderUtil.ShaderHasError(shader), "union shader compiles and is supported");
            var sprite = Shader.Find("Sprites/Default");
            camera = Own(new GameObject("Render test camera")).AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10);
            camera.orthographic = true; camera.orthographicSize = 3; camera.aspect = 1;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.allowHDR = false; camera.allowMSAA = false;
            target = Own(new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32)); target.Create(); camera.targetTexture = target;
            Quad(sprite, new Color(.04f, .08f, .15f, 1), Vector2.zero, Vector2.one * 8, -20);
            var original = Quad(sprite, blue, Vector2.zero, Vector2.one * 4, 0);
            Color oldSingle = Sample(); original.enabled = false;
            var cone = Quad(shader, blue, Vector2.zero, Vector2.one * 4, 0);
            Color single = Sample(); Equal(single, oldSingle, "single cone keeps original tint and opacity");
            var overlaps = new List<MeshRenderer>();
            for (int i = 0; i < 40; i++) overlaps.Add(Quad(shader, blue, Vector2.zero, Vector2.one * 4, 0));
            Equal(Sample(), single, "41 overlapping cones have single-layer brightness");
            foreach (var overlap in overlaps) overlap.enabled = false;
            cone.transform.position = new Vector3(-.5f, 0, 0);
            var partial = Quad(shader, blue, new Vector2(.5f, 0), Vector2.one * 4, 0);
            Equal(Sample(), single, "partial overlap remains single strength");
            Equal(Sample(-2), single, "left union edge keeps full cone strength");
            Equal(Sample(2), single, "right union edge keeps full cone strength");
            cone.enabled = false; partial.enabled = false;
            var danger = Quad(shader, red, Vector2.zero, Vector2.one * 4, 0, 2999);
            Color redSingle = Sample(); cone.enabled = true;
            Equal(Sample(), redSingle, "red danger vision wins over overlapping blue");
            danger.sharedMaterial.color = new Color(1, 0, 0, 0);
            Equal(Sample(), single, "invisible cone never claims the stencil pixel");
            danger.enabled = false; cone.transform.position = Vector3.zero;

            // Check both render orders because queue and sorting order are separate.
            var maskShader = Resources.Load<Shader>("Shaders/CameraVisibilityStencil");
            var revealShader = Resources.Load<Shader>("Shaders/CameraVisionReveal");
            Check(!ShaderUtil.ShaderHasError(maskShader) && !ShaderUtil.ShaderHasError(revealShader), "visibility shaders compile");
            var mask = Quad(maskShader, Color.white, Vector2.zero, Vector2.one * 2, 29999);
            var darkness = Quad(revealShader, new Color(.02f, .03f, .04f, 1), Vector2.zero, Vector2.one * 8, 30000);
            cone.transform.localScale = Vector3.one * 2;
            foreach (int order in new[] { -1, 29999 })
            {
                mask.sortingOrder = order;
                Equal(Sample(), single, "player visibility stencil remains revealed inside cone, order=" + order);
                Color darkWithCone = Sample(2.5f); cone.enabled = false;
                Equal(darkWithCone, Sample(2.5f), "NPC vision never bypasses player darkness");
                cone.enabled = true;
            }
            mask.enabled = false; darkness.enabled = false;
            Equal(Sample(), single, "stencil clears between camera frames");
            var foreground = Quad(sprite, new Color(.8f, .8f, .8f, 1), Vector2.zero, Vector2.one, 1);
            Color withCone = Sample(); cone.enabled = false;
            Equal(withCone, Sample(), "character/foreground still covers vision");
            foreground.enabled = false;

            var actor = Own(new GameObject("AI material wiring"));
            var ai = actor.AddComponent<ZeldaCharacterAiBase>();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(ZeldaCharacterAiBase).GetMethod("CreateVisionVisual", flags).Invoke(ai, null);
            var material = (Material)typeof(ZeldaCharacterAiBase).GetField("visionMaterial", flags).GetValue(ai);
            Check(material.shader == shader && material.renderQueue == 3000, "AI uses union material");
            typeof(ZeldaCharacterAiBase).GetField("visionColor", flags).SetValue(ai, red);
            typeof(ZeldaCharacterAiBase).GetMethod("ApplyVisionMaterialAppearance", flags).Invoke(ai, null);
            Check(material.renderQueue == 2999 && material.color == red, "state colour refresh keeps red priority");
            Debug.Log("AI_VISION_UNION_PASS checks=" + checks + " graphics=" + SystemInfo.graphicsDeviceType);
            Cleanup(); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); Cleanup(); EditorApplication.Exit(1); }
    }
    static void Cleanup()
    {
        if (camera != null) camera.targetTexture = null;
        for (int i = owned.Count - 1; i >= 0; i--) if (owned[i]) Object.DestroyImmediate(owned[i]);
        owned.Clear();
    }
}
