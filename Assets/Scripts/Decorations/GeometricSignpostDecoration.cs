using System.Collections.Generic;
using UnityEngine;

/// <summary>Flat signpost: a long pole and three alternating arrow boards.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Decorations/Geometric Signpost Decoration")]
public sealed class GeometricSignpostDecoration : MonoBehaviour
{
    [Header("Geometry / 几何外形")]
    [SerializeField, Min(0.02f)] private float poleWidth = 0.18f;
    [SerializeField, Min(0.1f)] private float poleHeight = 3.2f;
    [SerializeField, Min(0.1f)] private float boardWidth = 1.16f;
    [SerializeField, Min(0.02f)] private float boardHeight = 0.24f;
    [SerializeField, Min(0f)] private float boardGap = 0.14f;
    [SerializeField, Range(0.05f, 0.45f)] private float arrowTipRatio = 0.18f;
    [SerializeField] private bool topPointsLeft = true;
    [Header("Flat Colors / 纯色色块")]
    [SerializeField] private Color poleColor = new Color(0.30f, 0.22f, 0.14f, 1f);
    [SerializeField] private Color boardColor = new Color(0.54f, 0.38f, 0.20f, 1f);
    [Header("Collision and Occlusion / 碰撞与遮挡")]
    [SerializeField] private bool solidBase = true;
    [SerializeField, Min(0.02f)] private float baseCollisionHeight = 0.22f;
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField, Min(2)] private int sortingOrder = 5;
    [SerializeField] private Material sourceMaterial;

    private GameObject visual;
    private Mesh mesh;
    private Material material;
    private bool dirty = true;

    private void OnEnable()
    {
        if (dirty || visual == null) Build();
        else visual.SetActive(true);
    }

    private void OnValidate()
    {
        poleWidth = Mathf.Max(0.02f, poleWidth);
        boardWidth = Mathf.Max(0.1f, boardWidth);
        boardHeight = Mathf.Max(0.02f, boardHeight);
        boardGap = Mathf.Max(0f, boardGap);
        // Keep at least half the overall height as exposed pole.
        poleHeight = Mathf.Max(poleHeight, (3f * boardHeight + 2f * boardGap) * 2f);
        arrowTipRatio = Mathf.Clamp(arrowTipRatio, 0.05f, 0.45f);
        baseCollisionHeight = Mathf.Clamp(baseCollisionHeight, 0.02f, poleHeight);
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
        var baseCollider = GetComponent<BoxCollider2D>();
        baseCollider.enabled = solidBase;
        baseCollider.isTrigger = false;
        baseCollider.autoTiling = false;
        baseCollider.size = new Vector2(poleWidth, baseCollisionHeight);
        baseCollider.offset = new Vector2(0f, baseCollisionHeight * 0.5f);
        baseCollider.edgeRadius = 0f;

        Shader shader = sourceMaterial != null ? sourceMaterial.shader : Shader.Find("Sprites/Default");
        if (shader == null) return;
        material = sourceMaterial != null ? new Material(sourceMaterial) : new Material(shader);
        material.name = "Signpost Flat Colors";
        material.hideFlags = HideFlags.HideAndDontSave;
        material.color = Color.white;
        var vertices = new List<Vector3>(19);
        var colors = new List<Color>(19);
        var triangles = new List<int>(33);
        AddPolygon(vertices, colors, triangles, new[] {
            new Vector2(-poleWidth * 0.5f, 0f), new Vector2(poleWidth * 0.5f, 0f),
            new Vector2(poleWidth * 0.5f, poleHeight), new Vector2(-poleWidth * 0.5f, poleHeight)
        }, poleColor);
        for (int i = 0; i < 3; i++)
        {
            float top = poleHeight - i * (boardHeight + boardGap);
            float bottom = top - boardHeight;
            float left = -boardWidth * 0.5f;
            float right = boardWidth * 0.5f;
            float tip = boardWidth * arrowTipRatio;
            bool pointsLeft = (i % 2 == 0) == topPointsLeft;
            Vector2[] outline = pointsLeft ? new[] {
                new Vector2(left + tip, bottom), new Vector2(right, bottom),
                new Vector2(right, top), new Vector2(left + tip, top),
                new Vector2(left, (top + bottom) * 0.5f)
            } : new[] {
                new Vector2(left, bottom), new Vector2(right - tip, bottom),
                new Vector2(right, (top + bottom) * 0.5f), new Vector2(right - tip, top),
                new Vector2(left, top)
            };
            AddPolygon(vertices, colors, triangles, outline, boardColor);
        }
        mesh = new Mesh { name = "Minimal Signpost", hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices);
        mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
        visual = new GameObject("Signpost Geometry (Generated)") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        visual.transform.SetParent(transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = Mathf.Max(2, sortingOrder);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private static void AddPolygon(List<Vector3> vertices, List<Color> colors, List<int> triangles,
        Vector2[] outline, Color color)
    {
        int start = vertices.Count;
        foreach (Vector2 position in outline) { vertices.Add(position); colors.Add(color); }
        for (int i = 1; i < outline.Length - 1; i++)
        { triangles.Add(start); triangles.Add(start + i); triangles.Add(start + i + 1); }
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
