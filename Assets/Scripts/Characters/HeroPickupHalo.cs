using UnityEngine;

/// <summary>Small presentation-only radial glow: one mesh, no particles, physics or per-frame allocations.</summary>
internal sealed class HeroPickupHalo : System.IDisposable
{
    private const int Segments = 32;
    private const int Rays = 12;
    private const int VertexCount = Segments * 12 + Rays * 6;
    private readonly GameObject root;
    private readonly Mesh mesh;
    private readonly Material material;
    private readonly MeshRenderer renderer;
    private readonly Vector3[] vertices = new Vector3[VertexCount];
    private readonly Color[] colors = new Color[VertexCount];
    private int vertex;

    public HeroPickupHalo(Transform parent)
    {
        root = new GameObject("Hero Pickup Radiating Halo") { hideFlags = HideFlags.DontSave };
        root.transform.SetParent(parent, false);
        root.layer = parent.gameObject.layer;
        mesh = new Mesh { name = "Pickup soft halo and outward rays", hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        var triangles = new int[VertexCount];
        for (int i = 0; i < triangles.Length; i++) triangles[i] = i;
        mesh.vertices = vertices; mesh.colors = colors; mesh.triangles = triangles;
        root.AddComponent<MeshFilter>().sharedMesh = mesh;
        material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        material.mainTexture = Texture2D.whiteTexture;
        renderer = root.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }

    public void Draw(Vector3 center, int layer, int order, float elapsed, float duration, float opacity)
    {
        root.transform.localPosition = center;
        renderer.sortingLayerID = layer; renderer.sortingOrder = order;
        float fade = Mathf.Clamp01(elapsed / .08f) * Mathf.Clamp01((duration - elapsed) / .16f) * opacity;
        float breath = .5f + .5f * Mathf.Sin(elapsed * Mathf.PI * 2f / .9f);
        vertex = 0;
        Fan(.57f + .06f * breath, new Color(.63f, .9f, 1f, .3f * fade));
        Fan(.34f + .025f * breath, new Color(1f, 1f, 1f, .48f * fade));
        // Thin, tapered rays move outwards in staggered waves. The centre stays
        // behind the item, keeping its original silhouette/colour clearly readable.
        for (int i = 0; i < Rays; i++)
        {
            float phase = Mathf.Repeat(elapsed / .8f + (i % 3) * .11f, 1f);
            Vector2 direction = Direction((i + .5f) * Mathf.PI * 2f / Rays);
            Vector2 side = new Vector2(-direction.y, direction.x) * .014f;
            Vector2 start = direction * Mathf.Lerp(.27f, .43f, phase);
            Vector2 end = direction * Mathf.Lerp(i % 2 == 0 ? .65f : .55f, .82f, phase);
            Color inner = new Color(.87f, .97f, 1f, (.58f + .2f * breath) * fade * (1f - phase));
            Color outer = new Color(.65f, .9f, 1f, 0f);
            Quad(start - side, start + side, end + side * .25f, end - side * .25f, inner, outer);
        }
        float ripple = Mathf.Repeat(elapsed / .9f, 1f);
        float radius = Mathf.Lerp(.25f, .82f, ripple);
        Color ring = new Color(.7f, .93f, 1f, .27f * fade * (1f - ripple));
        for (int i = 0; i < Segments; i++)
        {
            Vector2 a = Direction(i * Mathf.PI * 2f / Segments);
            Vector2 b = Direction((i + 1) * Mathf.PI * 2f / Segments);
            Quad(a * radius, b * radius, b * (radius + .014f), a * (radius + .014f), ring, new Color(ring.r, ring.g, ring.b, 0f));
        }
        mesh.vertices = vertices; mesh.colors = colors;
        mesh.RecalculateBounds();
    }

    private static Vector2 Direction(float angle) => new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

    private void Fan(float radius, Color center)
    {
        Color edge = new Color(center.r, center.g, center.b, 0f);
        for (int i = 0; i < Segments; i++)
        {
            Add(Vector2.zero, center);
            Add(Direction(i * Mathf.PI * 2f / Segments) * radius, edge);
            Add(Direction((i + 1) * Mathf.PI * 2f / Segments) * radius, edge);
        }
    }

    private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color inner, Color outer)
    {
        Add(a, inner); Add(b, inner); Add(c, outer);
        Add(a, inner); Add(c, outer); Add(d, outer);
    }

    private void Add(Vector2 position, Color color) { vertices[vertex] = position; colors[vertex++] = color; }

    public void Dispose()
    {
        if (root != null) root.SetActive(false);
        Release(root); Release(mesh); Release(material);
    }

    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Object.Destroy(value); else Object.DestroyImmediate(value);
    }
}
