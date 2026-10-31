using System.Collections.Generic;
using UnityEngine;

/// <summary>Thin vector wavefront and a restrained trailing echo; no AI or damage logic.</summary>
public sealed class ZeldaInvestigationPulse : MonoBehaviour
{
    private const int Segments = 128;
    private static Material sharedMaterial;
    private static readonly Vector2[] directions = CreateDirections();
    private readonly List<Vector3> vertices = new List<Vector3>((Segments + 1) * 8);
    private readonly List<Color32> colors = new List<Color32>((Segments + 1) * 8);
    private readonly List<int> triangles = new List<int>(Segments * 36);
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private float maximumRadius, duration, elapsed;
    private Color baseColor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        if (sharedMaterial != null) Destroy(sharedMaterial);
        sharedMaterial = null;
    }

    public void Configure(float radius, float fadeDuration, Color color, int sortingLayerId, int sortingOrder)
    {
        maximumRadius = Mathf.Max(0f, radius);
        duration = Mathf.Max(.05f, fadeDuration);
        elapsed = 0;
        baseColor = color;
        if (sharedMaterial == null)
            sharedMaterial = new Material(Shader.Find("Sprites/Default")) {
                name = "Vector Investigation Ripple", mainTexture = Texture2D.whiteTexture,
                hideFlags = HideFlags.HideAndDontSave
            };
        if (mesh == null)
        {
            mesh = new Mesh { name = "Investigation Ripple Geometry", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        meshRenderer.sharedMaterial = sharedMaterial;
        meshRenderer.sortingLayerID = sortingLayerId;
        meshRenderer.sortingOrder = sortingOrder;
        if (GetComponent<CameraVisionStreamingExempt>() == null)
            gameObject.AddComponent<CameraVisionStreamingExempt>();
        ApplyFrame(0);
    }

    private void Update()
    {
        if (mesh == null || Time.deltaTime <= 0) return;
        elapsed += Time.deltaTime;
        if (elapsed >= duration) { Destroy(gameObject); return; }
        ApplyFrame(elapsed);
    }

    private void ApplyFrame(float age)
    {
        vertices.Clear(); colors.Clear(); triangles.Clear();
        float progress = Mathf.Clamp01(age / duration);
        // Preserve the original leading wave's timing and maximum radius.
        float travel = 1f - (1f - progress) * (1f - progress);
        float width = Mathf.Clamp(maximumRadius * .016f, .035f, .12f) * Mathf.Lerp(1f, .68f, progress);
        float fade = 1f - Mathf.SmoothStep(0f, 1f, progress);
        float reveal = Mathf.Clamp01(progress / .06f);
        AddRing(maximumRadius * travel, width, fade * reveal * .85f);
        if (progress > .1f)
        {
            float echo = Mathf.Clamp01((progress - .1f) / .9f);
            float echoTravel = 1f - (1f - echo) * (1f - echo);
            AddRing(maximumRadius * echoTravel * .94f, width * .62f,
                fade * Mathf.Clamp01(echo / .08f) * .3f);
        }
        mesh.Clear();
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0, false);
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(maximumRadius * 2, maximumRadius * 2, .1f));
        meshRenderer.enabled = vertices.Count > 0;
    }

    private void AddRing(float radius, float width, float alpha)
    {
        if (radius <= .0001f || alpha <= .001f || baseColor.a <= 0) return;
        width = Mathf.Min(width, radius * .35f);
        // Analytic feather only at the stroke edges: never a filled translucent disk.
        float feather = Mathf.Min(width * .16f, .01f);
        int start = vertices.Count;
        Color32 solid = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * alpha);
        Color32 clear = new Color(baseColor.r, baseColor.g, baseColor.b, 0);
        for (int i = 0; i <= Segments; i++)
        {
            Vector2 d = directions[i];
            Vertex(d * radius, clear);
            Vertex(d * (radius - feather), solid);
            Vertex(d * (radius - width + feather), solid);
            Vertex(d * (radius - width), clear);
            if (i == 0) continue;
            for (int band = 0; band < 3; band++)
            {
                int v = start + i * 4 + band;
                triangles.Add(v - 4); triangles.Add(v - 3); triangles.Add(v);
                triangles.Add(v); triangles.Add(v - 3); triangles.Add(v + 1);
            }
        }
    }

    private void Vertex(Vector2 point, Color32 color) { vertices.Add(point); colors.Add(color); }
    private static Vector2[] CreateDirections()
    {
        var result = new Vector2[Segments + 1];
        for (int i = 0; i <= Segments; i++)
        {
            float angle = i * Mathf.PI * 2 / Segments;
            result[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        return result;
    }
    private void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
