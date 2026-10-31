using System.Collections.Generic;
using UnityEngine;

/// <summary>Balanced geometric tree with a broad trunk and sparse flat-colour foliage facets.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Decorations/Geometric Tree Decoration")]
public sealed class GeometricTreeDecoration : MonoBehaviour
{
    // Keep the original silhouette at zero so existing scenes/prefabs are unchanged.
    public enum TreeShape { Classic = 0, Broadleaf = 1, Poplar = 2, Conifer = 3, Umbrella = 4 }

    [Header("Geometry / 几何外形")]
    [SerializeField] private TreeShape shape = TreeShape.Classic;
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

    private static readonly Vector2[] PoplarOutline = {
        new Vector2(0f, -.53f), new Vector2(.27f, -.3f),
        new Vector2(.42f, .04f), new Vector2(.3f, .4f),
        new Vector2(0f, .62f), new Vector2(-.3f, .4f),
        new Vector2(-.42f, .04f), new Vector2(-.27f, -.3f)
    };
    private static readonly Vector2[] FirLower = {
        new Vector2(-.5f, -.5f), new Vector2(.5f, -.5f),
        new Vector2(.075f, .16f), new Vector2(-.075f, .16f)
    };
    private static readonly Vector2[] FirMiddle = {
        new Vector2(-.43f, -.18f), new Vector2(.43f, -.18f),
        new Vector2(.05f, .38f), new Vector2(-.05f, .38f)
    };
    private static readonly Vector2[] FirTop = {
        new Vector2(-.34f, .14f), new Vector2(.34f, .14f),
        new Vector2(.035f, .6f), new Vector2(-.035f, .6f)
    };
    private static readonly Vector2[] UmbrellaOutline = {
        new Vector2(-.5f, -.08f), new Vector2(-.37f, -.3f),
        new Vector2(.34f, -.3f), new Vector2(.5f, -.05f),
        new Vector2(.41f, .26f), new Vector2(.18f, .51f),
        new Vector2(-.16f, .52f), new Vector2(-.41f, .25f)
    };
    private static readonly Vector2[] UmbrellaLight = {
        new Vector2(-.38f, .08f), new Vector2(.33f, .08f),
        new Vector2(.26f, .29f), new Vector2(.12f, .43f),
        new Vector2(-.14f, .44f), new Vector2(-.31f, .25f)
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

        var vertices = new List<Vector3>(80);
        var colors = new List<Color>(80);
        var indices = new List<int>(180);
        var trunk = new[]
        {
            new Vector2(-trunkWidth * 0.5f, 0f), new Vector2(trunkWidth * 0.5f, 0f),
            new Vector2(trunkWidth * 0.5f, trunkHeight), new Vector2(-trunkWidth * 0.5f, trunkHeight)
        };
        AddPolygon(vertices, colors, indices, trunk, Vector2.zero, Vector2.one, trunkColor);
        AddCrown(vertices, colors, indices);
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

    private void AddCrown(List<Vector3> vertices, List<Color> colors, List<int> indices)
    {
        // Each crown/branch is convex, so the existing triangle fan stays valid.
        // Layered flat colours share one mesh/material, not separate renderers.
        switch (shape)
        {
            case TreeShape.Broadleaf:
                AddBranch(vertices, colors, indices, new Vector2(0f, trunkHeight * .55f),
                    crownCenter + Vector2.Scale(new Vector2(-.3f, -.05f), crownSize), trunkWidth * .48f);
                AddBranch(vertices, colors, indices, new Vector2(0f, trunkHeight * .65f),
                    crownCenter + Vector2.Scale(new Vector2(.3f, 0f), crownSize), trunkWidth * .48f);
                AddCrownPart(vertices, colors, indices, CrownOutline, new Vector2(-.23f, -.02f), new Vector2(.65f, .74f), crownColor);
                AddCrownPart(vertices, colors, indices, CrownOutline, new Vector2(.23f, .02f), new Vector2(.64f, .8f), crownColor);
                AddCrownPart(vertices, colors, indices, CrownOutline, new Vector2(-.03f, .23f), new Vector2(.66f, .68f), leafAccentColor);
                AddCrownPart(vertices, colors, indices, UpperOutline, new Vector2(-.03f, .23f), new Vector2(.66f, .68f), upperCrownColor);
                AddCrownPart(vertices, colors, indices, UpperOutline, new Vector2(-.23f, -.02f), new Vector2(.6f, .6f), leafAccentColor);
                AddCrownPart(vertices, colors, indices, RightLeafFacet, new Vector2(.1f, 0f), new Vector2(.75f, .75f), leafAccentColor);
                break;
            case TreeShape.Poplar:
                AddCrownPart(vertices, colors, indices, PoplarOutline, Vector2.zero, Vector2.one, crownColor);
                AddCrownPart(vertices, colors, indices, PoplarOutline, new Vector2(-.04f, .09f), new Vector2(.69f, .78f), leafAccentColor);
                AddCrownPart(vertices, colors, indices, PoplarOutline, new Vector2(-.065f, .24f), new Vector2(.4f, .46f), upperCrownColor);
                break;
            case TreeShape.Conifer:
                AddCrownPart(vertices, colors, indices, FirLower, Vector2.zero, Vector2.one, crownColor);
                AddCrownPart(vertices, colors, indices, FirMiddle, Vector2.zero, Vector2.one, leafAccentColor);
                AddCrownPart(vertices, colors, indices, FirTop, Vector2.zero, Vector2.one, upperCrownColor);
                break;
            case TreeShape.Umbrella:
                AddBranch(vertices, colors, indices, new Vector2(0f, trunkHeight * .54f),
                    crownCenter + Vector2.Scale(new Vector2(-.31f, -.12f), crownSize), trunkWidth * .52f);
                AddBranch(vertices, colors, indices, new Vector2(0f, trunkHeight * .68f),
                    crownCenter + Vector2.Scale(new Vector2(.33f, -.09f), crownSize), trunkWidth * .46f);
                AddCrownPart(vertices, colors, indices, UmbrellaOutline, Vector2.zero, Vector2.one, crownColor);
                AddCrownPart(vertices, colors, indices, UmbrellaLight, Vector2.zero, Vector2.one, upperCrownColor);
                AddCrownPart(vertices, colors, indices, LeftLeafFacet, new Vector2(.04f, 0f), new Vector2(.9f, .7f), leafAccentColor);
                AddCrownPart(vertices, colors, indices, RightLeafFacet, new Vector2(.03f, .02f), new Vector2(.85f, .65f), leafAccentColor);
                break;
            default:
                // Preserve the original vertex order, coordinates and palette exactly.
                AddPolygon(vertices, colors, indices, CrownOutline, crownCenter, crownSize, crownColor);
                AddPolygon(vertices, colors, indices, UpperOutline, crownCenter, crownSize, upperCrownColor);
                AddPolygon(vertices, colors, indices, LeftLeafFacet, crownCenter, crownSize, leafAccentColor);
                AddPolygon(vertices, colors, indices, RightLeafFacet, crownCenter, crownSize, leafAccentColor);
                break;
        }
    }

    private void AddCrownPart(List<Vector3> vertices, List<Color> colors, List<int> indices,
        Vector2[] outline, Vector2 offset, Vector2 size, Color color)
    {
        AddPolygon(vertices, colors, indices, outline, crownCenter + Vector2.Scale(offset, crownSize),
            Vector2.Scale(size, crownSize), color);
    }

    private void AddBranch(List<Vector3> vertices, List<Color> colors, List<int> indices,
        Vector2 from, Vector2 to, float width)
    {
        Vector2 direction = (to - from).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * width * .5f;
        var branch = new[] { from - normal, to - normal * .65f, to + normal * .65f, from + normal };
        AddPolygon(vertices, colors, indices, branch, Vector2.zero, Vector2.one, trunkColor);
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
