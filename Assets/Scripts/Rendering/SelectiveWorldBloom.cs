using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Built-in RP world-only bloom. Runs on the world camera before Blocks and UI cameras.</summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
[AddComponentMenu("Rendering/Selective World Bloom")]
public sealed class SelectiveWorldBloom : MonoBehaviour
{
    [Serializable]
    public struct Surface
    {
        public SpriteRenderer renderer;
        [Range(0f, 1f)] public float strength;
        public Surface(SpriteRenderer renderer, float strength) { this.renderer = renderer; this.strength = strength; }
    }
    [SerializeField] private List<Surface> surfaces = new List<Surface>();
    [Range(0f, 2f)] public float nearIntensity = 0.85f;
    [Range(0f, 1f)] public float farIntensity = 0.22f;
    [Range(0.3f, 0.9f)] public float threshold = 0.66f;
    [Range(0.02f, 0.4f)] public float softKnee = 0.12f;
    [Range(0.5f, 4f)] public float radius = 1.5f;

    private readonly Dictionary<Sprite, Mesh> meshes = new Dictionary<Sprite, Mesh>();
    private readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();
    private Camera worldCamera;
    private Material material;
    private CommandBuffer commands;
    private bool reportedUnsupportedShader;
    private static readonly int MainTex = Shader.PropertyToID("_MainTex");
    private static readonly int Strength = Shader.PropertyToID("_Strength");

    public void SetSurfaces(List<Surface> value)
    {
        surfaces.Clear();
        surfaces.AddRange(value);
    }

    private void OnEnable() { worldCamera = GetComponent<Camera>(); }

    private bool EnsureResources()
    {
        if (material == null)
        {
            Shader shader = Resources.Load<Shader>("SelectiveWorldBloom");
            if (shader == null || !shader.isSupported)
            {
                if (!reportedUnsupportedShader)
                    Debug.LogWarning("Garden fluorescence: selective bloom shader is unavailable; keeping the original world render.", this);
                reportedUnsupportedShader = true;
                return false;
            }
            material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }
        if (commands == null) commands = new CommandBuffer { name = "Garden selective fluorescence masks" };
        return true;
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        // Never copied onto the final UI compositor; no effect on minimap / UI cameras.
        if (!Application.isPlaying || worldCamera != Camera.main || surfaces.Count == 0 ||
            (nearIntensity <= 0f && farIntensity <= 0f) || !EnsureResources())
        {
            Graphics.Blit(source, destination);
            return;
        }

        RenderTexture selection = null, visibility = null, seed = null, near = null, scratch = null, far = null, farScratch = null;
        try
        {
            selection = Temporary(source, 1, true);
            visibility = Temporary(source, 1, true);
            BuildMasks(selection, visibility);
            seed = Temporary(source, 2);
            near = Temporary(source, 2);
            scratch = Temporary(source, 2);
            far = Temporary(source, 4);
            farScratch = Temporary(source, 4);
            material.SetTexture("_SelectionTex", selection);
            material.SetTexture("_VisibilityTex", visibility);
            material.SetVector("_Settings", new Vector4(threshold, Mathf.Max(0.02f, softKnee), nearIntensity, farIntensity));
            // Extract from the already fog-composited scene, not from hidden source sprites.
            Graphics.Blit(source, seed, material, 2);
            float scale = Mathf.Max(0.5f, source.height / 1080f);
            Blur(seed, scratch, near, radius * scale);
            Graphics.Blit(seed, far);
            Blur(far, farScratch, far, radius * 1.8f * scale);
            material.SetTexture("_NearTex", near);
            material.SetTexture("_FarTex", far);
            Graphics.Blit(source, destination, material, 4);
        }
        finally
        {
            Release(selection); Release(visibility); Release(seed); Release(near);
            Release(scratch); Release(far); Release(farScratch);
        }
    }

    private void Blur(RenderTexture input, RenderTexture temporary, RenderTexture output, float distance)
    {
        material.SetVector("_BlurDirection", new Vector4(distance / input.width, 0f, 0f, 0f));
        Graphics.Blit(input, temporary, material, 3);
        material.SetVector("_BlurDirection", new Vector4(0f, distance / input.height, 0f, 0f));
        Graphics.Blit(temporary, output, material, 3);
    }

    private void BuildMasks(RenderTexture selection, RenderTexture visibility)
    {
        commands.Clear();
        commands.SetViewProjectionMatrices(worldCamera.worldToCameraMatrix,
            GL.GetGPUProjectionMatrix(worldCamera.projectionMatrix, true));
        commands.SetRenderTarget(selection);
        commands.ClearRenderTarget(false, true, Color.clear);
        foreach (Surface surface in surfaces)
        {
            SpriteRenderer renderer = surface.renderer;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                renderer.sprite == null || surface.strength <= 0f || renderer.color.a <= 0f ||
                (worldCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0 ||
                renderer.drawMode != SpriteDrawMode.Simple) continue;
            Mesh mesh = GetSpriteMesh(renderer.sprite);
            properties.Clear();
            properties.SetTexture(MainTex, renderer.sprite.texture);
            properties.SetFloat(Strength, Mathf.Clamp01(surface.strength) * renderer.color.a);
            Matrix4x4 matrix = renderer.localToWorldMatrix * Matrix4x4.Scale(
                new Vector3(renderer.flipX ? -1f : 1f, renderer.flipY ? -1f : 1f, 1f));
            commands.DrawMesh(mesh, matrix, material, 0, 0, properties);
        }
        commands.SetRenderTarget(visibility);
        CameraCircularVision vision = worldCamera.GetComponent<CameraCircularVision>();
        bool masked = vision != null && vision.isActiveAndEnabled;
        commands.ClearRenderTarget(false, true, masked ? Color.black : Color.white);
        if (masked && vision.TryGetPostEffectVisibilityMesh(out Mesh visibleMesh, out Matrix4x4 visibleMatrix))
            commands.DrawMesh(visibleMesh, visibleMatrix, material, 0, 1);
        Graphics.ExecuteCommandBuffer(commands);
        // Blit sets its own matrices and target; no persistent camera / layer changes.
    }

    private Mesh GetSpriteMesh(Sprite sprite)
    {
        if (meshes.TryGetValue(sprite, out Mesh mesh)) return mesh;
        Vector2[] points = sprite.vertices;
        var vertices = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++) vertices[i] = points[i];
        ushort[] sourceTriangles = sprite.triangles;
        var triangles = new int[sourceTriangles.Length];
        for (int i = 0; i < triangles.Length; i++) triangles[i] = sourceTriangles[i];
        mesh = new Mesh { name = "Bloom selection: " + sprite.name, hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = vertices; mesh.uv = sprite.uv; mesh.triangles = triangles;
        meshes.Add(sprite, mesh);
        return mesh;
    }

    private static RenderTexture Temporary(RenderTexture source, int divisor, bool mask = false)
    {
        var descriptor = source.descriptor;
        descriptor.width = Mathf.Max(1, source.width / divisor);
        descriptor.height = Mathf.Max(1, source.height / divisor);
        descriptor.depthBufferBits = 0; descriptor.msaaSamples = 1;
        descriptor.useMipMap = false; descriptor.autoGenerateMips = false;
        if (mask)
        {
            descriptor.colorFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.R8)
                ? RenderTextureFormat.R8 : RenderTextureFormat.ARGB32;
            descriptor.sRGB = false;
        }
        var texture = RenderTexture.GetTemporary(descriptor);
        texture.filterMode = mask ? FilterMode.Point : FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        return texture;
    }

    private static void Release(RenderTexture texture) { if (texture != null) RenderTexture.ReleaseTemporary(texture); }
    private void OnDisable()
    {
        if (commands != null) { commands.Release(); commands = null; }
        if (material != null) { Destroy(material); material = null; }
        foreach (Mesh mesh in meshes.Values) if (mesh != null) Destroy(mesh);
        meshes.Clear();
    }
}
