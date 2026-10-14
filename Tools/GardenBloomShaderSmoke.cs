// Copy with the production shader into an isolated Unity project. Does not touch the open game scene.
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using System;

public static class GardenBloomShaderSmoke
{
    private static int assertions;
    private static void Check(bool result, string message)
    {
        if (!result) throw new Exception(message);
        assertions++;
    }
    private static Texture2D Read(RenderTexture target)
    {
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = target;
        var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false, true);
        image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
        image.Apply(); RenderTexture.active = previous;
        return image;
    }
    private static Texture2D Texture(Func<int, int, Color> color)
    {
        var image = new Texture2D(128, 128, TextureFormat.RGBA32, false, true);
        var pixels = new Color[128 * 128];
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) pixels[y * 128 + x] = color(x, y);
        image.SetPixels(pixels); image.Apply(); return image;
    }
    public static void Run()
    {
        try
        {
            Shader shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/SelectiveWorldBloom.shader");
            Check(shader != null && shader.isSupported, "Shader missing or unsupported");
            Check(!ShaderUtil.ShaderHasError(shader), "Shader compiler errors");
            var material = new Material(shader);
            Check(material.passCount == 5, "Expected five passes");
            var source = Texture((x, y) => x >= 60 && x < 68 && y >= 40 && y < 48 ? new Color(0.1f, 0.9f, 1f) : Color.black);
            var selection = Texture((x, y) => Color.white);
            var visible = Texture((x, y) => x < 66 ? Color.white : Color.black);
            var seed = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var temp = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var blur = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            var result = new RenderTexture(128, 128, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            material.SetTexture("_SelectionTex", selection);
            material.SetTexture("_VisibilityTex", visible);
            material.SetVector("_Settings", new Vector4(0.66f, 0.12f, 0.85f, 0.22f));
            Graphics.Blit(source, seed, material, 2);
            var pixels = Read(seed);
            Check(pixels.GetPixel(64, 44).b > 0.9f, "Visible highlight not extracted");
            Check(pixels.GetPixel(67, 44).b < 0.01f, "Hidden highlight leaked before blur");
            Check(pixels.GetPixel(64, 84).b < 0.01f, "Extraction vertically flipped");
            material.SetVector("_BlurDirection", new Vector4(2f / 128f, 0, 0, 0));
            Graphics.Blit(seed, temp, material, 3);
            material.SetVector("_BlurDirection", new Vector4(0, 2f / 128f, 0, 0));
            Graphics.Blit(temp, blur, material, 3);
            material.SetTexture("_NearTex", blur); material.SetTexture("_FarTex", blur);
            Graphics.Blit(source, result, material, 4);
            pixels = Read(result);
            Check(pixels.GetPixel(58, 44).b > 0.01f, "No spatial halo outside source");
            Check(pixels.GetPixel(70, 44).b < 0.01f, "Halo leaked after blur outside visibility");
            Check(pixels.GetPixel(64, 44).b >= 0.99f, "Crisp source lost");
            Check(pixels.GetPixel(20, 20).b < 0.01f, "Dark background changed");
            material.SetTexture("_SelectionTex", Texture2D.blackTexture);
            Graphics.Blit(source, seed, material, 2);
            Check(Read(seed).GetPixel(64, 44).b < 0.01f, "Unselected bright pixels glow");
            material.SetTexture("_SelectionTex", selection);
            Graphics.Blit(Texture((x,y) => new Color(0.035f, 0.29f, 0.52f)), seed, material, 2);
            Check(Read(seed).GetPixel(64,44).b < 0.01f, "Water fill emits instead of highlights");

            // GPU draw of an asymmetric world-space mask through the same command-buffer path.
            var camera = new GameObject("Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
            camera.orthographicSize = 1; camera.aspect = 1;
            var mesh = new Mesh();
            mesh.vertices = new[] {new Vector3(-0.5f,-0.5f,0),new Vector3(0,-0.5f,0),new Vector3(0,0,0),new Vector3(-0.5f,0,0)};
            mesh.uv = new[] {Vector2.zero, Vector2.right, Vector2.one, Vector2.up};
            mesh.triangles = new[] {0,1,2,0,2,3};
            var properties = new MaterialPropertyBlock();
            properties.SetTexture("_MainTex", Texture2D.whiteTexture); properties.SetFloat("_Strength", 0.7f);
            var commands = new CommandBuffer();
            commands.SetViewProjectionMatrices(camera.worldToCameraMatrix, GL.GetGPUProjectionMatrix(camera.projectionMatrix, true));
            commands.SetRenderTarget(seed); commands.ClearRenderTarget(false,true,Color.black);
            commands.DrawMesh(mesh, Matrix4x4.identity, material, 0, 0, properties);
            Graphics.ExecuteCommandBuffer(commands); commands.Release();
            pixels = Read(seed);
            Check(Mathf.Abs(pixels.GetPixel(48,48).r - 0.7f) < 0.02f, "World selection mask misaligned / wrong strength");
            Check(pixels.GetPixel(48,80).r < 0.01f, "World selection mask flipped");
            Debug.Log("GARDEN_BLOOM_GPU_PASS: " + assertions + " assertions; graphics=" + SystemInfo.graphicsDeviceType);
            EditorApplication.Exit(0);
        }
        catch (Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
