using System.Collections.Generic;
using UnityEngine;

/// <summary>Geometric 2D street lamp with two translucent, breathing light discs.</summary>
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Rendering/Kerosene Street Lamp")]
public sealed class KeroseneStreetLamp : MonoBehaviour
{
    [Header("Body / 灯体")]
    [SerializeField] private Material sourceMaterial;
    [SerializeField] private Color metalColor = new Color(0.09f, 0.13f, 0.17f, 1f);
    [SerializeField] private Color glassColor = new Color(1f, 0.9f, 0.59f, 0.9f);
    [SerializeField, Min(2), Tooltip("灯体绘制顺序，至少为 2，以遮挡当前绘制顺序为 1 的角色。光晕仍在灯体下方。")]
    private int sortingOrder = 5;
    [SerializeField] private string sortingLayerName = "Default";

    [Header("Light / 双层光晕")]
    [SerializeField] private bool lightOn = true;
    [SerializeField] private Vector2 lightCenter = new Vector2(0f, 2.77f);
    [SerializeField, Min(0.01f)] private float innerRadius = 0.48f;
    [SerializeField, Min(0.01f)] private float outerRadius = 0.9f;
    [SerializeField] private Color innerLightColor = new Color(1f, 0.9f, 0.52f, 0.22f);
    [SerializeField] private Color outerLightColor = new Color(1f, 0.92f, 0.65f, 0.09f);
    [SerializeField, Min(0.1f)] private float breathingPeriod = 3.2f;
    [SerializeField, Range(0f, 0.3f)] private float radiusBreathing = 0.08f;
    [SerializeField, Range(0f, 0.8f)] private float alphaBreathing = 0.18f;
    [SerializeField] private bool randomizePhase = true;

    private GameObject generatedRoot;
    private Mesh bodyMesh, discMesh;
    private Material material;
    private MeshRenderer innerLight, outerLight;
    private MaterialPropertyBlock lightProperties;
    private float elapsed, phase;
    private bool dirty = true;

    public void SetLightOn(bool value) => lightOn = value;

    private void OnEnable()
    {
        phase = randomizePhase ? Mathf.Repeat(GetInstanceID() * 0.618034f, 1f) * Mathf.PI * 2f : 0f;
        if (generatedRoot == null) Build();
        else generatedRoot.SetActive(true);
    }

    private void OnValidate()
    {
        innerRadius = Mathf.Max(0.01f, innerRadius);
        outerRadius = Mathf.Max(innerRadius, outerRadius);
        breathingPeriod = Mathf.Max(0.1f, breathingPeriod);
        sortingOrder = Mathf.Max(2, sortingOrder);
        dirty = true; // No object creation from the validation/import thread.
    }

    private void Update()
    {
        if (dirty || generatedRoot == null) Build();
        if (material == null) return;
        // Editor displays a static preview; gameplay pause also pauses breathing.
        if (Application.isPlaying) elapsed += Time.deltaTime;
        float angle = elapsed * (Mathf.PI * 2f / Mathf.Max(0.1f, breathingPeriod)) + phase;
        float pulse = Application.isPlaying ? Mathf.Sin(angle) : 0f;
        ApplyLight(outerLight, outerRadius, outerLightColor, pulse);
        ApplyLight(innerLight, innerRadius, innerLightColor, pulse);
    }

    private void ApplyLight(MeshRenderer renderer, float radius, Color tint, float pulse)
    {
        if (renderer == null) return;
        renderer.enabled = lightOn;
        renderer.transform.localPosition = new Vector3(lightCenter.x, lightCenter.y, 0f);
        renderer.transform.localScale = Vector3.one * (radius * (1f + radiusBreathing * pulse));
        tint.a *= 1f + alphaBreathing * pulse;
        lightProperties.SetColor("_Color", tint);
        renderer.SetPropertyBlock(lightProperties);
    }

    private void Build()
    {
        Release();
        dirty = false;
        ConfigureBaseCollider();
        Shader shader = sourceMaterial != null ? sourceMaterial.shader : Shader.Find("Sprites/Default");
        if (shader == null) return;
        material = sourceMaterial != null ? new Material(sourceMaterial) : new Material(shader);
        material.name = "Kerosene Lamp Geometry";
        material.hideFlags = HideFlags.HideAndDontSave;
        material.color = Color.white;
        lightProperties = new MaterialPropertyBlock();
        generatedRoot = new GameObject("Lamp Geometry (Generated)") { hideFlags = HideFlags.HideAndDontSave };
        generatedRoot.transform.SetParent(transform, false);
        generatedRoot.layer = gameObject.layer;

        var vertices = new List<Vector3>(20);
        var colors = new List<Color>(20);
        var triangles = new List<int>(30);
        // Five flat shapes only: base, straight pole, frame, glass and roof.
        // No trim, crossbar, highlight strips, inner mullion, burner or finial.
        // Trapezoids: bottom Y, top Y, bottom width, top width, center X, color.
        AddShape(vertices, colors, triangles, 0f, 0.22f, 0.42f, 0.42f, 0f, metalColor);
        AddShape(vertices, colors, triangles, 0.22f, 2.52f, 0.11f, 0.11f, 0f, metalColor);
        AddShape(vertices, colors, triangles, 2.52f, 3.02f, 0.27f, 0.59f, 0f, metalColor);
        AddShape(vertices, colors, triangles, 2.57f, 2.97f, 0.18f, 0.46f, 0f, glassColor);
        AddShape(vertices, colors, triangles, 3.02f, 3.20f, 0.66f, 0.22f, 0f, metalColor);
        bodyMesh = new Mesh { name = "Geometric Kerosene Lamp", hideFlags = HideFlags.HideAndDontSave };
        bodyMesh.SetVertices(vertices);
        bodyMesh.SetColors(colors);
        bodyMesh.SetTriangles(triangles, 0);
        bodyMesh.RecalculateBounds();
        // Only the base is solid. The rest draws over passing characters without
        // obstructing their movement; character body sprites use sorting order 1.
        CreateRenderer("Body", bodyMesh, Mathf.Max(2, sortingOrder));
        discMesh = CreateDisc();
        outerLight = CreateRenderer("Outer Warm Light", discMesh, sortingOrder - 2);
        innerLight = CreateRenderer("Inner Warm Light", discMesh, sortingOrder - 1);
        ApplyLight(outerLight, outerRadius, outerLightColor, 0f);
        ApplyLight(innerLight, innerRadius, innerLightColor, 0f);
    }

    private void ConfigureBaseCollider()
    {
        // A persistent root component, not a hidden generated child: it survives
        // visual rebuilds and is available to physics/AI and scene inspection.
        BoxCollider2D baseCollider = GetComponent<BoxCollider2D>();
        if (baseCollider == null) baseCollider = gameObject.AddComponent<BoxCollider2D>();
        baseCollider.isTrigger = false;
        baseCollider.autoTiling = false;
        baseCollider.offset = new Vector2(0f, 0.11f);
        baseCollider.size = new Vector2(0.42f, 0.22f);
        baseCollider.edgeRadius = 0f;
    }

    private static void AddShape(List<Vector3> vertices, List<Color> colors, List<int> triangles,
        float bottom, float top, float bottomWidth, float topWidth, float x, Color color)
    {
        int start = vertices.Count;
        vertices.Add(new Vector3(x - bottomWidth * 0.5f, bottom, 0f));
        vertices.Add(new Vector3(x + bottomWidth * 0.5f, bottom, 0f));
        vertices.Add(new Vector3(x + topWidth * 0.5f, top, 0f));
        vertices.Add(new Vector3(x - topWidth * 0.5f, top, 0f));
        for (int i = 0; i < 4; i++) colors.Add(color);
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }

    private static Mesh CreateDisc()
    {
        const int segments = 64;
        var vertices = new Vector3[segments * 2 + 1];
        var colors = new Color[vertices.Length];
        var indices = new int[segments * 9];
        colors[0] = Color.white;
        for (int i = 0; i < segments; i++)
        {
            float angle = i * Mathf.PI * 2f / segments;
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
            vertices[1 + i] = direction * 0.92f;
            vertices[1 + segments + i] = direction;
            colors[1 + i] = Color.white;
            colors[1 + segments + i] = new Color(1f, 1f, 1f, 0f);
            int a = 1 + i, b = 1 + (i + 1) % segments, offset = i * 9;
            indices[offset] = 0; indices[offset + 1] = a; indices[offset + 2] = b;
            indices[offset + 3] = a; indices[offset + 4] = a + segments; indices[offset + 5] = b + segments;
            indices[offset + 6] = a; indices[offset + 7] = b + segments; indices[offset + 8] = b;
        }
        var mesh = new Mesh { name = "Soft Edge Light Disc", hideFlags = HideFlags.HideAndDontSave };
        mesh.vertices = vertices;
        mesh.colors = colors;
        mesh.triangles = indices;
        mesh.RecalculateBounds();
        return mesh;
    }

    private MeshRenderer CreateRenderer(string label, Mesh mesh, int order)
    {
        var child = new GameObject(label) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        child.transform.SetParent(generatedRoot.transform, false);
        child.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingLayerName = sortingLayerName;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    private void OnDisable()
    {
        if (!Application.isPlaying) Release();
        else if (generatedRoot != null) generatedRoot.SetActive(false);
    }

    private void OnDestroy() => Release();

    private void Release()
    {
        if (generatedRoot != null) generatedRoot.SetActive(false);
        Dispose(generatedRoot); Dispose(bodyMesh); Dispose(discMesh); Dispose(material);
        generatedRoot = null; bodyMesh = null; discMesh = null; material = null;
        innerLight = null; outerLight = null;
    }

    private static void Dispose(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value);
        else DestroyImmediate(value);
    }
}
