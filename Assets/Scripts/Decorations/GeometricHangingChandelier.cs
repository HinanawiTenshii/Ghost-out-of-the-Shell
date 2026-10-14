using System.Collections.Generic;
using UnityEngine;

/// <summary>Overhead iron-wheel chandelier: one body mesh and two reusable glow meshes.</summary>
[ExecuteAlways, DisallowMultipleComponent]
[AddComponentMenu("Decorations/Geometric Hanging Chandelier")]
public sealed class GeometricHangingChandelier : MonoBehaviour
{
    [Header("Body / 俯视吊灯")]
    [SerializeField, Min(.2f), Tooltip("灯体直径，不包含外围光晕。")]
    private float diameter = 2f;
    [SerializeField] private Color metalColor = new Color(.61f, .66f, .70f, 1);
    [SerializeField] private Color socketColor = new Color(.56f, .31f, .16f, 1);
    [SerializeField] private Color waxColor = new Color(.95f, .87f, .65f, 1);
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField, Tooltip("悬挂灯体默认位于角色上方，仍会被视野遮罩遮盖。")]
    private int sortingOrder = 5;

    [Header("Warm Glow / 动态暖光")]
    [SerializeField] private Color glowColor = new Color(1, .52f, .12f, 1);
    [SerializeField, Range(0, 6)] private float glowIntensity = 2.4f;
    [SerializeField, Range(.2f, .8f), Tooltip("每盏灯光晕半径，随整体直径同比例缩放。")]
    private float glowRadius = .48f;
    [SerializeField, Min(.2f)] private float breathingPeriod = 3.2f;
    [SerializeField, Range(0, .5f)] private float breathingDepth = .25f;
    [SerializeField, Range(0, .2f)] private float flickerAmount = .07f;
    [SerializeField] private bool randomizePhase = true;

    private GameObject generatedRoot;
    private Mesh bodyMesh, haloMesh, coreMesh;
    private MeshRenderer haloRenderer, coreRenderer;
    private Material bodyMaterial, glowMaterial;
    private MaterialPropertyBlock glowProperties;
    private float elapsed, phase;
    private bool dirty = true;

    private void OnEnable()
    {
        phase = randomizePhase ? Mathf.Repeat(GetInstanceID() * .618034f, 1) * Mathf.PI * 2 : 0;
        if (dirty || generatedRoot == null) Rebuild();
        else generatedRoot.SetActive(true);
    }

    private void OnValidate()
    {
        diameter = Mathf.Max(.2f, diameter);
        breathingPeriod = Mathf.Max(.2f, breathingPeriod);
        glowIntensity = Mathf.Clamp(glowIntensity, 0, 6);
        glowRadius = Mathf.Clamp(glowRadius, .2f, .8f);
        breathingDepth = Mathf.Clamp(breathingDepth, 0, .5f);
        flickerAmount = Mathf.Clamp(flickerAmount, 0, .2f);
        dirty = true; // Never create or destroy Unity objects in the validation/import callback.
    }

    private void Update()
    {
        if (dirty || generatedRoot == null) Rebuild();
        // A stable edit-mode preview; gameplay pause also freezes the light movement.
        if (Application.isPlaying) elapsed += Time.deltaTime;
        ApplyGlow(Application.isPlaying ? elapsed : 0);
    }

    private void ApplyGlow(float time)
    {
        if (haloRenderer == null || coreRenderer == null) return;
        float angle = time * Mathf.PI * 2 / Mathf.Max(.2f, breathingPeriod) + phase;
        float breath = Mathf.Sin(angle) * breathingDepth;
        // Smooth, small fluctuations, not flashing or random frame-by-frame noise.
        float flicker = (Mathf.Sin(time * 8.7f + phase) * .65f
            + Mathf.Sin(time * 13.1f + phase * 1.7f) * .35f) * flickerAmount;
        float intensity = glowIntensity * Mathf.Max(.15f, 1 + breath + flicker);
        glowProperties.SetFloat("_Intensity", intensity);
        haloRenderer.SetPropertyBlock(glowProperties);
        coreRenderer.SetPropertyBlock(glowProperties);
        haloRenderer.enabled = coreRenderer.enabled = glowIntensity > 0;
    }

    private void Rebuild()
    {
        Release();
        dirty = false;
        Shader bodyShader = Shader.Find("Sprites/Default");
        if (bodyShader == null) return;
        bodyMaterial = new Material(bodyShader) { name = "Chandelier Geometry", hideFlags = HideFlags.HideAndDontSave };
        Shader glowShader = Resources.Load<Shader>("WarmLightGlow");
        glowMaterial = new Material(glowShader != null && glowShader.isSupported ? glowShader : bodyShader)
        { name = "Chandelier Warm Glow", hideFlags = HideFlags.HideAndDontSave };
        if (glowMaterial.HasProperty("_RadialFalloff")) glowMaterial.SetFloat("_RadialFalloff", 1);
        glowProperties = new MaterialPropertyBlock();
        generatedRoot = new GameObject("Chandelier Geometry (Generated)")
        { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        generatedRoot.transform.SetParent(transform, false);
        generatedRoot.transform.localScale = Vector3.one * (diameter * .5f);

        var body = new Geometry();
        Color iron = new Color(.22f, .27f, .30f, 1);
        Color highlight = Color.Lerp(metalColor, Color.white, .28f);
        // Three fine suspension struts meet at a central hub. No vertical pole or side elevation.
        for (int i = 0; i < 3; i++)
        {
            Vector2 end = Direction(30 + i * 120) * .66f;
            body.Beam(Vector2.zero, end, .075f, iron);
            body.Beam(Vector2.zero, end, .032f, metalColor);
        }
        body.Ring(.63f, .77f, iron);
        body.Ring(.655f, .73f, metalColor);
        body.Disc(Vector2.zero, .15f, iron);
        body.Disc(Vector2.zero, .10f, highlight);
        body.Disc(Vector2.zero, .045f, iron);

        var halo = new Geometry();
        var core = new Geometry();
        halo.GlowQuad(Vector2.zero, 1.55f, WithAlpha(glowColor, .055f));
        for (int i = 0; i < 6; i++)
        {
            Vector2 direction = Direction(90 + i * 60);
            Vector2 center = direction * .8f;
            body.Beam(direction * .69f, center, .11f, iron);
            body.Disc(center, .20f, iron);
            body.Disc(center, .155f, socketColor);
            body.Disc(center, .11f, waxColor);
            body.Disc(center + new Vector2(-.012f, .012f), .069f, new Color(1, .96f, .80f, 1));
            // Small overhead flame, not a side-view upright candle silhouette.
            body.Quad(center + new Vector2(0, -.043f), center + new Vector2(.038f, 0),
                center + new Vector2(0, .065f), center + new Vector2(-.038f, 0), new Color(1, .68f, .18f, 1));
            halo.GlowQuad(center, glowRadius, WithAlpha(glowColor, .28f));
            core.GlowQuad(center, .22f, new Color(1, .74f, .29f, .17f));
            core.GlowQuad(center, .09f, new Color(1, .94f, .66f, .70f));
        }
        bodyMesh = body.ToMesh("Overhead Chandelier Body");
        haloMesh = halo.ToMesh("Chandelier Soft Halos");
        coreMesh = core.ToMesh("Chandelier Light Cores");
        CreateRenderer("Iron Wheel and Candle Tops", bodyMesh, bodyMaterial, sortingOrder);
        haloRenderer = CreateRenderer("Warm Halo", haloMesh, glowMaterial, sortingOrder - 1);
        coreRenderer = CreateRenderer("Warm Cores", coreMesh, glowMaterial, sortingOrder + 1);
        ApplyGlow(Application.isPlaying ? elapsed : 0);
    }

    private MeshRenderer CreateRenderer(string label, Mesh mesh, Material material, int order)
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

    private static Vector2 Direction(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
    private static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }

    // Compact code-native geometry. Built only on enable/configuration changes, never per animation frame.
    private sealed class Geometry
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<Vector2> uv = new List<Vector2>();
        private readonly List<int> triangles = new List<int>();
        private int part;

        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = vertices.Count;
            float z = -part++ * .00001f;
            vertices.Add(new Vector3(a.x, a.y, z)); vertices.Add(new Vector3(b.x, b.y, z));
            vertices.Add(new Vector3(c.x, c.y, z)); vertices.Add(new Vector3(d.x, d.y, z));
            for (int i = 0; i < 4; i++) colors.Add(color);
            uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
            triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 1);
            triangles.Add(start); triangles.Add(start + 3); triangles.Add(start + 2);
        }

        public void Beam(Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 delta = (to - from).normalized;
            Vector2 normal = new Vector2(-delta.y, delta.x) * (width * .5f);
            Quad(from - normal, to - normal, to + normal, from + normal, color);
        }

        public void Ring(float inner, float outer, Color color)
        {
            for (int i = 0; i < 12; i++)
            {
                Vector2 a = Direction(i * 30), b = Direction((i + 1) * 30);
                Quad(a * inner, a * outer, b * outer, b * inner, color);
            }
        }

        public void Disc(Vector2 center, float radius, Color color)
        {
            int start = vertices.Count;
            float z = -part++ * .00001f;
            for (int i = 0; i < 12; i++)
            {
                Vector2 p = center + Direction(i * 30) * radius;
                vertices.Add(new Vector3(p.x, p.y, z)); colors.Add(color); uv.Add(Vector2.zero);
            }
            for (int i = 1; i < 11; i++)
            { triangles.Add(start); triangles.Add(start + i + 1); triangles.Add(start + i); }
        }

        public void GlowQuad(Vector2 center, float radius, Color color)
        {
            Quad(center + new Vector2(-radius, -radius), center + new Vector2(radius, -radius),
                center + new Vector2(radius, radius), center + new Vector2(-radius, radius), color);
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
            return mesh;
        }
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
        Dispose(generatedRoot); Dispose(bodyMesh); Dispose(haloMesh); Dispose(coreMesh);
        Dispose(bodyMaterial); Dispose(glowMaterial);
        generatedRoot = null; bodyMesh = haloMesh = coreMesh = null;
        bodyMaterial = glowMaterial = null; haloRenderer = coreRenderer = null;
    }
    private static void Dispose(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
