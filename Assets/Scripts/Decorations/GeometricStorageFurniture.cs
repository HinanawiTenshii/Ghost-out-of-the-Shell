using System.Collections.Generic;
using UnityEngine;

/// <summary>Chest-matched flat geometric storage props, without per-frame geometry generation.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(PolygonCollider2D))]
[AddComponentMenu("Decorations/Geometric Storage Furniture")]
public sealed class GeometricStorageFurniture : MonoBehaviour
{
    public enum FurnitureShape { StorageRack, HorizontalBarrel }
    [SerializeField] private FurnitureShape shape;
    [SerializeField] private Vector2 size = new Vector2(2.8f, 2.5f);
    [Header("Chest Palette / 木箱配色")]
    [SerializeField] private Color woodColor = new Color(0.67f, 0.39f, 0.2f, 1f);
    [SerializeField] private Color edgeColor = new Color(0.38f, 0.2f, 0.11f, 1f);
    [SerializeField] private Color lightWoodColor = new Color(0.73f, 0.45f, 0.24f, 1f);
    [SerializeField] private Color metalColor = new Color(0.67f, 0.71f, 0.74f, 1f);
    [SerializeField] private bool solid = true;
    [SerializeField] private int sortingOrder = -3;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private Material sourceMaterial;

    private struct Part
    {
        public Vector2[] Points;
        public Color Color;
        public Part(Vector2[] points, Color color) { Points = points; Color = color; }
    }

    private GameObject visual;
    private Mesh mesh;
    private Material ownedMaterial;
    private bool dirty = true;

    private void OnEnable() { if (dirty || visual == null) Rebuild(); else visual.SetActive(true); }
    private void OnValidate()
    {
        size = new Vector2(Mathf.Max(0.2f, size.x), Mathf.Max(0.2f, size.y));
        dirty = true;
    }
    private void Update() { if (dirty || visual == null) Rebuild(); }
    private void OnDisable() { if (visual != null) visual.SetActive(false); }
    private void OnDestroy() { Release(); }

    private void Rebuild()
    {
        Release();
        dirty = false;
        bool barrel = shape == FurnitureShape.HorizontalBarrel;
        Vector2 reference = barrel ? new Vector2(3.4f, 1.9f) : new Vector2(2.8f, 2.5f);
        Vector2 scale = new Vector2(size.x / reference.x, size.y / reference.y);
        PolygonCollider2D collider = GetComponent<PolygonCollider2D>();
        collider.enabled = solid;
        collider.isTrigger = false;
        collider.offset = Vector2.zero;
        collider.pathCount = 1;
        Vector2[] outline = CollisionOutline(barrel);
        for (int i = 0; i < outline.Length; i++) outline[i] = Vector2.Scale(outline[i], scale);
        collider.SetPath(0, outline);

        Material material = sourceMaterial;
        if (material == null)
        {
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            ownedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material = ownedMaterial;
        }
        List<Part> parts = BuildParts(barrel, woodColor, edgeColor, lightWoodColor, metalColor);
        var vertices = new List<Vector3>();
        var colors = new List<Color>();
        var triangles = new List<int>();
        for (int p = 0; p < parts.Count; p++)
        {
            Part part = parts[p];
            int first = vertices.Count;
            foreach (Vector2 point in part.Points)
            {
                vertices.Add(new Vector3(point.x * scale.x, point.y * scale.y, -p * 0.0001f));
                colors.Add(part.Color);
            }
            // All drawing parts are convex, including the tapered barrel shell.
            for (int i = 1; i < part.Points.Length - 1; i++)
            {
                triangles.Add(first); triangles.Add(first + i + 1); triangles.Add(first + i);
            }
        }
        mesh = new Mesh { name = "Geometric " + shape, hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        mesh.uv = new Vector2[vertices.Count];
        mesh.RecalculateBounds();
        visual = new GameObject("Generated Storage Geometry") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        visual.transform.SetParent(transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static List<Part> BuildParts(bool barrel, Color wood, Color edge, Color light, Color metal)
    {
        var parts = new List<Part>();
        Color metalShadow = new Color(0.35f, 0.39f, 0.42f, 1f);
        if (barrel)
        {
            // Plan view of a barrel lying along X: hidden cradle and end faces stay hidden.
            parts.Add(new Part(BarrelOutline(), edge));
            parts.Add(new Part(new[] {
                new Vector2(-1.59f,-0.51f), new Vector2(-1.10f,-0.84f),
                new Vector2(1.10f,-0.84f), new Vector2(1.59f,-0.51f),
                new Vector2(1.59f,0.51f), new Vector2(1.10f,0.84f),
                new Vector2(-1.10f,0.84f), new Vector2(-1.59f,0.51f)
            }, wood));
            // Broad central crown and equal edge shading describe the upper curved surface.
            foreach (float y in new[] {-0.73f, 0.73f})
                parts.Add(Rect(0f, y, 2.20f, 0.18f, Color.Lerp(wood, edge, 0.3f)));
            parts.Add(Rect(0f, 0f, 3.08f, 0.64f, light));
            parts.Add(Rect(-1.52f, 0f, 0.12f, 1.02f, edge));
            parts.Add(Rect(1.52f, 0f, 0.12f, 1.02f, edge));
            foreach (float x in new[] {-0.92f, 0.92f})
            {
                parts.Add(Rect(x, 0f, 0.24f, 1.88f, metalShadow));
                parts.Add(Rect(x, 0f, 0.16f, 1.64f, metal));
            }
            parts.Add(Rect(0f, 0f, 0.24f, 0.24f, edge));
            parts.Add(Rect(0f, 0f, 0.13f, 0.13f, wood)); // Bung on the upward-facing crown.
        }
        else
        {
            // Only the top platform, cargo lids and post end caps are visible from above.
            // Lower shelves and vertical posts must not read as a front elevation.
            parts.Add(Rect(0f, 0f, 2.8f, 2.5f, edge));
            parts.Add(Rect(0f, 0f, 2.56f, 2.26f, light));
            parts.Add(Rect(0f, 0f, 2.34f, 2.04f, wood));
            AddCrate(parts, -0.57f, 0.12f, 0.88f, 1.60f, edge, light, metal);
            AddCrate(parts, 0.53f, 0.51f, 0.90f, 0.76f, edge, light, metal);
            AddCrate(parts, 0.57f, -0.48f, 0.82f, 0.76f, edge, light, metal);
            foreach (float x in new[] {-1.27f, 1.27f})
                foreach (float y in new[] {-1.12f, 1.12f})
                    parts.Add(Rect(x, y, 0.18f, 0.18f, metal));
        }
        return parts;
    }

    private static void AddCrate(List<Part> parts, float x, float y, float w, float h, Color edge, Color wood, Color metal)
    {
        parts.Add(Rect(x, y, w, h, edge));
        parts.Add(Rect(x, y, w - 0.12f, h - 0.12f, wood));
        // Two straps over a closed lid, matching the existing Chest's overhead construction.
        parts.Add(Rect(x, y - h * 0.27f, w - 0.04f, 0.10f, metal));
        parts.Add(Rect(x, y + h * 0.27f, w - 0.04f, 0.10f, metal));
    }

    private static Part Rect(float x, float y, float w, float h, Color color)
    {
        return new Part(new[] { new Vector2(x-w*0.5f,y-h*0.5f), new Vector2(x+w*0.5f,y-h*0.5f),
            new Vector2(x+w*0.5f,y+h*0.5f), new Vector2(x-w*0.5f,y+h*0.5f) }, color);
    }

    private static Vector2[] BarrelOutline()
    {
        return new[] { new Vector2(-1.7f,-0.57f), new Vector2(-1.12f,-0.95f),
            new Vector2(1.12f,-0.95f), new Vector2(1.7f,-0.57f),
            new Vector2(1.7f,0.57f), new Vector2(1.12f,0.95f),
            new Vector2(-1.12f,0.95f), new Vector2(-1.7f,0.57f) };
    }

    private static Vector2[] CollisionOutline(bool barrel)
    {
        if (!barrel) return Rect(0f, 0f, 2.8f, 2.5f, Color.white).Points;
        return BarrelOutline();
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
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }
}
