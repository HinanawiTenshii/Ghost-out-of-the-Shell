using System.Collections.Generic;
using UnityEngine;

/// <summary>Balanced geometric tree with a broad trunk and sparse flat-colour foliage facets.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Decorations/Geometric Tree Decoration")]
public sealed class GeometricTreeDecoration : MonoBehaviour
{
    [Header("Geometry / 几何外形")]
    [SerializeField, Min(0.02f)] private float trunkWidth = 0.34f;
    [SerializeField, Min(0.1f)] private float trunkHeight = 2f;
    [SerializeField] private Vector2 crownSize = new Vector2(1.9f, 1.7f);
    [SerializeField] private Vector2 crownCenter = new Vector2(0f, 2.2f);
    [Header("Flat Colors / 纯色色块")]
    [SerializeField] private Color trunkColor = new Color(0.30f, 0.22f, 0.14f, 1f);
    [SerializeField] private Color crownColor = new Color(0.28f, 0.38f, 0.21f, 1f);
    [SerializeField] private Color upperCrownColor = new Color(0.35f, 0.46f, 0.27f, 1f);
    [SerializeField] private Color leafAccentColor = new Color(0.31f, 0.415f, 0.235f, 1f);
    [Header("Collision and Occlusion / 碰撞与遮挡")]
    [SerializeField] private bool solidBase = true;
    [SerializeField, Min(0.02f)] private float baseCollisionHeight = 0.26f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField, Min(2)] private int sortingOrder = 5;
    [SerializeField] private Material sourceMaterial;

    // Symmetric convex silhouette, inset crown and two subtle mirrored leaf masses.
    // No texture, veins or outlines; all shapes remain in one mesh.
    private static readonly Vector2[] CrownOutline =
    {
        new Vector2(-0.3f, -0.5f), new Vector2(0.3f, -0.5f),
        new Vector2(0.5f, -0.2f), new Vector2(0.5f, 0.3f),
        new Vector2(0.3f, 0.6f), new Vector2(-0.3f, 0.6f),
        new Vector2(-0.5f, 0.3f), new Vector2(-0.5f, -0.2f)
    };
    private static readonly Vector2[] UpperOutline =
    {
        new Vector2(-0.23f, 0.1f), new Vector2(0.23f, 0.1f),
        new Vector2(0.36f, 0.3f), new Vector2(0.23f, 0.51f),
        new Vector2(-0.23f, 0.51f), new Vector2(-0.36f, 0.3f)
    };
    private static readonly Vector2[] LeftLeafFacet =
    {
        new Vector2(-0.3f, -0.28f), new Vector2(-0.1f, -0.1f),
        new Vector2(-0.2f, 0.1f), new Vector2(-0.43f, -0.05f)
    };
    private static readonly Vector2[] RightLeafFacet =
    {
        new Vector2(0.3f, -0.28f), new Vector2(0.43f, -0.05f),
        new Vector2(0.2f, 0.1f), new Vector2(0.1f, -0.1f)
    };

    private GameObject visual;
    private Mesh mesh;
    private Material material;
    private bool dirty = true;

    private void OnEnable()
    {
        if (visual == null || dirty) Build();
        else visual.SetActive(true);
    }

    private void OnValidate()
    {
        trunkWidth = Mathf.Max(0.02f, trunkWidth);
        trunkHeight = Mathf.Max(0.1f, trunkHeight);
        crownSize = new Vector2(Mathf.Max(0.1f, crownSize.x), Mathf.Max(0.1f, crownSize.y));
        baseCollisionHeight = Mathf.Clamp(baseCollisionHeight, 0.02f, trunkHeight);
        sortingOrder = Mathf.Max(2, sortingOrder);
        dirty = true;
    }

    private void Update()
    {
        if (dirty || visual == null) Build();
    }

    private void Build()
    {
        Release();
        dirty = false;
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider == null) collider = gameObject.AddComponent<BoxCollider2D>();
        collider.enabled = solidBase;
        collider.isTrigger = false;
        collider.autoTiling = false;
        collider.size = new Vector2(trunkWidth, baseCollisionHeight);
        collider.offset = new Vector2(0f, baseCollisionHeight * 0.5f);
        collider.edgeRadius = 0f;

        Shader shader = sourceMaterial != null ? sourceMaterial.shader : Shader.Find("Sprites/Default");
        if (shader == null) return;
        material = sourceMaterial != null ? new Material(sourceMaterial) : new Material(shader);
        material.name = "Geometric Tree Flat Colors";
        material.hideFlags = HideFlags.HideAndDontSave;
        material.color = Color.white;

        var vertices = new List<Vector3>(26);
        var colors = new List<Color>(26);
        var indices = new List<int>(48);
        var trunk = new[]
        {
            new Vector2(-trunkWidth * 0.5f, 0f), new Vector2(trunkWidth * 0.5f, 0f),
            new Vector2(trunkWidth * 0.5f, trunkHeight), new Vector2(-trunkWidth * 0.5f, trunkHeight)
        };
        AddPolygon(vertices, colors, indices, trunk, Vector2.zero, Vector2.one, trunkColor);
        AddPolygon(vertices, colors, indices, CrownOutline, crownCenter, crownSize, crownColor);
        AddPolygon(vertices, colors, indices, UpperOutline, crownCenter, crownSize, upperCrownColor);
        AddPolygon(vertices, colors, indices, LeftLeafFacet, crownCenter, crownSize, leafAccentColor);
        AddPolygon(vertices, colors, indices, RightLeafFacet, crownCenter, crownSize, leafAccentColor);
        mesh = new Mesh { name = "Minimal Geometric Tree", hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(indices, 0);
        mesh.RecalculateBounds();

        visual = new GameObject("Tree Geometry (Generated)") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        visual.transform.SetParent(transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = Mathf.Max(2, sortingOrder);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void AddPolygon(List<Vector3> vertices, List<Color> colors, List<int> indices,
        Vector2[] outline, Vector2 center, Vector2 size, Color color)
    {
        int start = vertices.Count;
        foreach (Vector2 p in outline)
        {
            vertices.Add(center + Vector2.Scale(p, size));
            colors.Add(color);
        }
        for (int i = 1; i < outline.Length - 1; i++)
        {
            indices.Add(start); indices.Add(start + i); indices.Add(start + i + 1);
        }
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) Release();
        else if (visual != null) visual.SetActive(false);
    }

    private void OnDestroy() => Release();

    private void Release()
    {
        if (visual != null) visual.SetActive(false);
        Dispose(visual); Dispose(mesh); Dispose(material);
        visual = null; mesh = null; material = null;
    }

    private static void Dispose(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
