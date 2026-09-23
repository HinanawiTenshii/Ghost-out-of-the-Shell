using System.Collections.Generic;
using UnityEngine;

/// <summary>Flat geometric round/quarter-annulus furniture, with matching solid collision.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(PolygonCollider2D))]
[AddComponentMenu("Decorations/Geometric Curved Table")]
public sealed class GeometricCurvedTable : MonoBehaviour
{
    public enum TableShape { Round, QuarterArc }

    [Header("Shape / 桌面形状")]
    [SerializeField] private TableShape shape;
    [SerializeField, Min(0.15f)] private float radius = 0.9f;
    [SerializeField, Min(0.1f)] private float arcWidth = 0.65f;
    [SerializeField] private float startAngle;
    [SerializeField, Range(12, 64)] private int curveSegments = 40;
    [SerializeField, Min(0.01f)] private float rimWidth = 0.08f;
    [SerializeField, Min(0f)] private float thickness = 0.06f;
    [Header("Colors / 配色")]
    [SerializeField] private Color woodColor = new Color(0.52f, 0.28f, 0.14f, 1f);
    [SerializeField] private Color edgeColor = new Color(0.38f, 0.2f, 0.11f, 1f);
    [SerializeField] private Color surfaceColor = new Color(0.22f, 0.57f, 0.56f, 1f);
    [SerializeField] private Color insetEdgeColor = new Color(0.33f, 0.67f, 0.63f, 1f);
    [Header("Rendering and collision / 渲染与碰撞")]
    [SerializeField] private bool solid = true;
    [SerializeField] private int sortingOrder = -3;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private Material sourceMaterial;

    private GameObject visual;
    private Mesh mesh;
    private Material ownedMaterial;
    private bool dirty = true;

    private void OnEnable()
    {
        if (dirty || visual == null) Rebuild();
        else visual.SetActive(true);
    }

    private void OnValidate()
    {
        radius = Mathf.Max(0.15f, radius);
        arcWidth = Mathf.Clamp(arcWidth, 0.1f, radius * 0.9f);
        rimWidth = Mathf.Clamp(rimWidth, 0.01f, Mathf.Min(radius * 0.2f, arcWidth * 0.24f));
        thickness = Mathf.Clamp(thickness, 0f, radius * 0.15f);
        curveSegments = Mathf.Clamp(curveSegments, 12, 64);
        dirty = true; // Do not create/destroy Unity objects inside OnValidate.
    }

    private void Update()
    {
        if (dirty || visual == null) Rebuild();
    }

    private void OnDisable() { if (visual != null) visual.SetActive(false); }
    private void OnDestroy() { Release(); }

    private void Rebuild()
    {
        Release();
        dirty = false;
        bool arc = shape == TableShape.QuarterArc;
        float inner = arc ? radius - arcWidth : 0f;
        var collider = GetComponent<PolygonCollider2D>();
        collider.enabled = solid;
        collider.isTrigger = false;
        collider.offset = Vector2.zero;
        collider.pathCount = 1;
        collider.SetPath(0, BuildCollisionOutline(arc, radius, inner, curveSegments, startAngle));

        Material material = sourceMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            ownedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material = ownedMaterial;
        }
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        // Four flat layers in a single mesh: lower edge, wood rim, inset lip, tabletop.
        AddSurface(vertices, colors, triangles, arc, radius, inner, startAngle, 0f,
            new Vector2(0f, -thickness), 0.003f, edgeColor);
        AddSurface(vertices, colors, triangles, arc, radius, inner, startAngle, 0f,
            Vector2.zero, 0.002f, woodColor);
        AddSurface(vertices, colors, triangles, arc, radius - rimWidth, arc ? inner + rimWidth : 0f,
            startAngle, arc ? rimWidth : 0f, Vector2.zero, 0.001f, insetEdgeColor);
        float inset = Mathf.Min(rimWidth + 0.025f, arc ? arcWidth * 0.4f : radius * 0.4f);
        AddSurface(vertices, colors, triangles, arc, radius - inset, arc ? inner + inset : 0f,
            startAngle, arc ? inset : 0f, Vector2.zero, 0f, surfaceColor);

        mesh = new Mesh { name = "Geometric " + shape + " Table", hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.uv = new Vector2[vertices.Count];
        mesh.RecalculateBounds();
        visual = new GameObject("Generated Table Geometry") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        visual.transform.SetParent(transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    // The arc is a single concave polygon, not a disk: its open center is walkable.
    public static Vector2[] BuildCollisionOutline(bool arc, float outer, float inner, int segments, float angle)
    {
        int count = arc ? (segments + 1) * 2 : segments;
        var points = new Vector2[count];
        for (int i = 0; i < (arc ? segments + 1 : segments); i++)
        {
            float a = (angle + (arc ? 90f : 360f) * i / segments) * Mathf.Deg2Rad;
            var direction = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            points[i] = direction * outer;
            if (arc) points[count - 1 - i] = direction * inner;
        }
        return points;
    }

    private void AddSurface(List<Vector3> vertices, List<Color> colors, List<int> triangles,
        bool arc, float outer, float inner, float angle, float endInset, Vector2 offset, float z, Color color)
    {
        if (outer <= inner) return;
        // Offset both straight end faces by the same world distance, not by a fixed angle.
        float innerCut = arc ? Mathf.Min(40f, Mathf.Asin(Mathf.Clamp01(endInset / Mathf.Max(0.01f, inner))) * Mathf.Rad2Deg) : 0f;
        float outerCut = arc ? Mathf.Asin(Mathf.Clamp01(endInset / outer)) * Mathf.Rad2Deg : 0f;
        float sweep = arc ? 90f : 360f;
        for (int i = 0; i < curveSegments; i++)
        {
            float t0 = (float)i / curveSegments, t1 = (float)(i + 1) / curveSegments;
            Vector2 a = Polar(outer, angle + outerCut + (sweep - 2f * outerCut) * t0) + offset;
            Vector2 b = Polar(outer, angle + outerCut + (sweep - 2f * outerCut) * t1) + offset;
            Vector2 c = Polar(inner, angle + innerCut + (sweep - 2f * innerCut) * t1) + offset;
            Vector2 d = Polar(inner, angle + innerCut + (sweep - 2f * innerCut) * t0) + offset;
            int n = vertices.Count;
            vertices.Add(new Vector3(a.x, a.y, z)); vertices.Add(new Vector3(b.x, b.y, z));
            vertices.Add(new Vector3(c.x, c.y, z)); vertices.Add(new Vector3(d.x, d.y, z));
            for (int j = 0; j < 4; j++) colors.Add(color);
            triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 1);
            if (inner > 0f) { triangles.Add(n); triangles.Add(n + 3); triangles.Add(n + 2); }
        }
    }

    private static Vector2 Polar(float radius, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
    }

    private void Release()
    {
        if (visual != null) visual.SetActive(false);
        ReleaseObject(visual); ReleaseObject(mesh); ReleaseObject(ownedMaterial);
        visual = null; mesh = null; ownedMaterial = null;
    }

    private static void ReleaseObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target);
        else DestroyImmediate(target);
    }
}
