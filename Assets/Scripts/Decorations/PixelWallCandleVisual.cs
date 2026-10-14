using System.Collections.Generic;
using UnityEngine;

public enum WallCandleDirection { Up, Right, Down, Left }

/// <summary>Geometric wall mount and living warm flame. Keeps the original mounting API and prefab footprint.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelWallCandleVisual : MonoBehaviour
{
    private const float EditorBoundsSize = 1.5f;
    private const int FlameColumns = 14, FlameRows = 18;
    [SerializeField] private WallCandleDirection mountingDirection = WallCandleDirection.Up;
    [SerializeField, Min(.04f), Tooltip("保留原动画速度配置；数值越小火舌变化越快。")]
    private float flameFrameDuration = .14f;
    [SerializeField] private int baseSortingOrder = 2;
    [SerializeField, Min(.1f)] private float glowPulseSpeed = 2.2f;
    [SerializeField, Range(0, .25f)] private float glowPulseAmount = .08f;
    [SerializeField] private bool useGoldMount;
    [SerializeField, Range(0, 6), InspectorName("火焰泛光强度")] private float glowIntensity = 1.35f;
    [SerializeField, Range(.01f, .1f), InspectorName("火焰边缘泛光宽度")]
    private float flameGlowWidth = .045f;

    private SpriteRenderer bodyRenderer;
    private GameObject generatedRoot;
    private Mesh bodyMesh, flameMesh, glowMesh;
    private Material bodyMaterial, glowMaterial;
    private MeshRenderer flameGlowRenderer;
    private Vector3[] glowVertices;
    private Color32[] flameColors, glowColors;
    private Vector2 flameAnchor;
    private float elapsed, phase, lastFlameUpdate = -1f;
    private bool dirty = true;

    public WallCandleDirection MountingDirection => mountingDirection;

    private void OnEnable()
    {
        phase = Mathf.Repeat(GetInstanceID() * .618034f, 1) * 3f;
        if (dirty || generatedRoot == null) Rebuild();
        else generatedRoot.SetActive(true);
    }

    private void Update()
    {
        if (dirty || generatedRoot == null) Rebuild();
        if (Application.isPlaying && Time.deltaTime > 0)
        {
            elapsed += Time.deltaTime;
            // Fixed 30 Hz visual sampling is enough for the coarse flame and bounds CPU work in candle-heavy rooms.
            if (elapsed - lastFlameUpdate >= 1f / 30f &&
                SceneVisualWorkBudget.IsVisible(transform, Vector2.one * EditorBoundsSize))
            {
                lastFlameUpdate = elapsed;
                UpdateFlame(elapsed + phase);
            }
        }
    }

    public void SetMountingDirection(WallCandleDirection direction)
    {
        mountingDirection = (WallCandleDirection)Mathf.Clamp((int)direction, 0, 3);
        dirty = true;
        if (isActiveAndEnabled) Rebuild();
    }

    private void Rebuild()
    {
        Release(); dirty = false;
        bodyRenderer = GetComponent<SpriteRenderer>();
        // Preserve the serialized renderer/fileID, sorting layer and existing scene references.
        // It no longer carries a bitmap: the visible replacement uses three combined meshes.
        bodyRenderer.sprite = null;
        bodyRenderer.sortingOrder = baseSortingOrder;
        SuppressLegacySprite("Flame"); SuppressLegacySprite("Outer Glow"); SuppressLegacySprite("Inner Glow");
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null) return;
        bodyMaterial = new Material(shader) { name = "Geometric Wall Candle", hideFlags = HideFlags.HideAndDontSave };
        generatedRoot = new GameObject("Wall Candle Geometry (Generated)")
        { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        generatedRoot.transform.SetParent(transform, false);
        bodyMesh = BuildBody();
        CreateRenderer("Mount and Wax", bodyMesh, bodyMaterial, baseSortingOrder);

        Vector2Int anchor = RotatePoint(12, 10, mountingDirection);
        flameAnchor = PixelCenter(anchor.x, anchor.y);
        var vertices = new Vector3[FlameColumns * FlameRows * 4];
        flameColors = new Color32[vertices.Length];
        var indices = new List<int>();
        float cellWidth = .25f / FlameColumns, cellHeight = .3125f / FlameRows;
        for (int row = 0; row < FlameRows; row++)
        for (int col = 0; col < FlameColumns; col++)
        {
            int start = (row * FlameColumns + col) * 4;
            float x = flameAnchor.x - .125f + col * cellWidth;
            float y = flameAnchor.y + .21875f + row * cellHeight;
            WriteQuad(vertices, start, x, y, cellWidth, cellHeight);
            AddQuad(indices, start);
        }
        flameMesh = new Mesh { name = "Living Candle Flame", hideFlags = HideFlags.HideAndDontSave };
        flameMesh.MarkDynamic(); flameMesh.vertices = vertices; flameMesh.triangles = indices.ToArray();
        flameMesh.uv = new Vector2[vertices.Length];
        CreateRenderer("Living Flame", flameMesh, bodyMaterial, baseSortingOrder + 1);

        Shader glowShader = Resources.Load<Shader>("WarmLightGlow");
        if (glowShader != null && glowShader.isSupported)
        {
            glowMaterial = new Material(glowShader) { name = "Warm Flame Edge Bloom", hideFlags = HideFlags.HideAndDontSave };
            glowMaterial.SetFloat("_RadialFalloff", 1);
            glowVertices = new Vector3[FlameRows * 4]; glowColors = new Color32[glowVertices.Length];
            var uv = new Vector2[glowVertices.Length];
            indices.Clear();
            for (int i = 0; i < FlameRows; i++)
            {
                AddQuad(indices, i * 4);
                uv[i * 4] = Vector2.zero; uv[i * 4 + 1] = Vector2.right;
                uv[i * 4 + 2] = Vector2.one; uv[i * 4 + 3] = Vector2.up;
            }
            glowMesh = new Mesh { name = "Warm Flame Contour Bloom", hideFlags = HideFlags.HideAndDontSave };
            glowMesh.MarkDynamic(); glowMesh.vertices = glowVertices; glowMesh.triangles = indices.ToArray(); glowMesh.uv = uv;
            flameGlowRenderer = CreateRenderer("Flame Edge Bloom", glowMesh, glowMaterial, baseSortingOrder + 2);
        }
        UpdateFlame(Application.isPlaying ? elapsed + phase : .32f);
    }

    private void SuppressLegacySprite(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null) return;
        var renderer = child.GetComponent<SpriteRenderer>();
        if (renderer != null) renderer.enabled = false;
    }

    private Mesh BuildBody()
    {
        var geometry = new Geometry();
        Color edge = useGoldMount ? new Color(.43f, .26f, .10f, 1) : new Color(.24f, .29f, .32f, 1);
        Color metal = useGoldMount ? new Color(.84f, .57f, .18f, 1) : new Color(.61f, .66f, .70f, 1);
        Color highlight = useGoldMount ? new Color(1, .80f, .35f, 1) : new Color(.80f, .85f, .87f, 1);
        // Exact original 24/16 coordinate frame and bracket footprint, with broad geometric faces.
        MountRect(geometry, 8, 3, 8, 2, edge);
        MountRect(geometry, 8.5f, 3.5f, 7, 1.25f, metal);
        MountRect(geometry, 10, 5, 4, 2, edge);
        MountRect(geometry, 10.5f, 5, 3, 1.5f, metal);
        MountRect(geometry, 11, 7, 2, 2, edge);
        MountRect(geometry, 11.65f, 7, .7f, 2, highlight);

        Vector2Int anchor = RotatePoint(12, 10, mountingDirection);
        float x = anchor.x, y = anchor.y;
        // The mounting bracket turns, but candle and flame stay upright, matching the previous API.
        switch (mountingDirection)
        {
            case WallCandleDirection.Up: geometry.Rect(x - 1, y - 5, 2, 3, edge); break;
            case WallCandleDirection.Right: geometry.Rect(x + 3, y - 2, 3, 3, edge); geometry.Rect(x + 3.4f, y - 1.4f, 2.2f, 1.8f, metal); break;
            case WallCandleDirection.Down: geometry.Rect(x - 1, y - 4, 2, 2, metal); break;
            case WallCandleDirection.Left: geometry.Rect(x - 6, y - 2, 3, 3, edge); geometry.Rect(x - 5.6f, y - 1.4f, 2.2f, 1.8f, metal); break;
        }
        geometry.Rect(x - 4, y - 2, 8, 2, edge);
        geometry.Rect(x - 3.5f, y - 1.5f, 7, 1.1f, metal);
        geometry.Rect(x - 3, y - .65f, 6, .45f, highlight);
        if (mountingDirection == WallCandleDirection.Down)
        {
            geometry.Rect(x - 4, y - 1, 1, 2, edge); geometry.Rect(x + 3, y - 1, 1, 2, edge);
        }
        geometry.Rect(x - 2, y, 4, 4, new Color(.68f, .66f, .52f, 1));
        geometry.Rect(x - 1.5f, y + .25f, 3, 3.5f, new Color(.93f, .89f, .71f, 1));
        geometry.Rect(x - 1.5f, y + 3, 3, 1, new Color(1, .96f, .80f, 1));
        geometry.Rect(x - 1, y + 4, 2, 1, edge);
        return geometry.ToMesh();
    }

    private void MountRect(Geometry geometry, float x, float y, float width, float height, Color color)
    {
        geometry.Quad(RotateEdge(x, y), RotateEdge(x + width, y), RotateEdge(x + width, y + height), RotateEdge(x, y + height), color);
    }
    private Vector2 RotateEdge(float x, float y)
    {
        Vector2 p = new Vector2((x - 12) / 16, (y - 12) / 16);
        switch (mountingDirection)
        {
            case WallCandleDirection.Right: return new Vector2(-p.y, p.x);
            case WallCandleDirection.Down: return -p;
            case WallCandleDirection.Left: return new Vector2(p.y, -p.x);
            default: return p;
        }
    }
    private static Vector2Int RotatePoint(int x, int y, WallCandleDirection direction)
    {
        switch (direction)
        {
            case WallCandleDirection.Right: return new Vector2Int(23 - y, x);
            case WallCandleDirection.Down: return new Vector2Int(23 - x, 23 - y);
            case WallCandleDirection.Left: return new Vector2Int(y, 23 - x);
            default: return new Vector2Int(x, y);
        }
    }
    private static Vector2 PixelCenter(int x, int y) => new Vector2((x + .5f - 12) / 16, (y + .5f - 12) / 16);

    private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float dx = (x - cx) / rx, dy = (y - cy) / ry;
        return 1 - dx * dx - dy * dy;
    }

    // A small, fixed geometry grid gives the reference's stepped silhouette, not individual sprites.
    // Color/alpha changes deform the flame; curved tongues and detached wisps grow and disappear.
    private static Color SampleFlame(float x, float y, float time)
    {
        float sway = Mathf.Sin(time * 4.3f) * .035f;
        float pulse = 1 + Mathf.Sin(time * 3.7f) * .08f;
        float density = Ellipse(x, y, sway, .29f, .34f * pulse, .265f);
        density = Mathf.Max(density, Ellipse(x, y, -.12f + sway, .48f, .20f, .23f));
        float tongueLife = Mathf.Repeat(time * 1.25f, 1);
        float tongueHeight = .72f + .20f * Mathf.Sin(tongueLife * Mathf.PI);
        float tongueCenter = .13f + sway + Mathf.Sin(y * 7 - time * 4) * .055f;
        if (y > .33f && y < tongueHeight)
        {
            float narrow = Mathf.Lerp(.12f, .018f, Mathf.InverseLerp(.33f, tongueHeight, y));
            density = Mathf.Max(density, 1 - Mathf.Pow((x - tongueCenter) / narrow, 2));
        }
        float opacity = Mathf.SmoothStep(0, 1, density / .16f);
        Color color = density < .26f ? new Color(.73f, .20f, .025f, opacity) : new Color(1, .36f, .05f, opacity);
        if (density >= .26f && Ellipse(x, y, -.045f + sway, .34f + Mathf.Sin(time * 3) * .025f, .19f, .23f) > .18f)
            color = new Color(1, .72f, .08f, opacity);
        float coreX = -.04f + sway, coreY = .27f + Mathf.Sin(time * 4) * .025f;
        if (opacity > 0 && ((Mathf.Abs(x - coreX) < .045f && Mathf.Abs(y - coreY) < .085f)
            || (Mathf.Abs(x - coreX) < .10f && Mathf.Abs(y - coreY) < .035f)))
            color = new Color(1, .96f, .58f, opacity);

        for (int i = 0; i < 2; i++)
        {
            float life = Mathf.Repeat(time / (.74f + i * .13f) + i * .47f, 1);
            float grow = Mathf.SmoothStep(0, 1, life / .2f);
            float fade = 1 - Mathf.SmoothStep(0, 1, (life - .55f) / .40f);
            float cx = .11f + Mathf.Sin(time * 2 + i) * .065f + i * -.20f;
            float cy = .57f + life * .38f;
            float size = .045f * (.25f + grow * .75f);
            if (Mathf.Abs(x - cx) < size && Mathf.Abs(y - cy) < size * 1.2f && grow * fade > color.a)
                color = new Color(1, .50f, .055f, grow * fade);
        }
        return color;
    }

    private void UpdateFlame(float time)
    {
        if (flameMesh == null) return;
        float animationTime = time * (.14f / Mathf.Max(.04f, flameFrameDuration));
        float cellWidth = .25f / FlameColumns, cellHeight = .3125f / FlameRows;
        for (int row = 0; row < FlameRows; row++)
        {
            int first = FlameColumns, last = -1;
            float rowAlpha = 0;
            for (int col = 0; col < FlameColumns; col++)
            {
                Color32 color = SampleFlame((col + .5f) / FlameColumns - .5f, (row + .5f) / FlameRows, animationTime);
                int start = (row * FlameColumns + col) * 4;
                for (int j = 0; j < 4; j++) flameColors[start + j] = color;
                if (color.a > 6) { first = Mathf.Min(first, col); last = col; rowAlpha = Mathf.Max(rowAlpha, color.a / 255f); }
            }
            if (glowMesh == null) continue;
            float x = flameAnchor.x - .125f + (last < 0 ? 0 : first) * cellWidth;
            float width = last < 0 ? cellWidth : (last - first + 1) * cellWidth;
            float y = flameAnchor.y + .21875f + row * cellHeight;
            WriteQuad(glowVertices, row * 4, x - flameGlowWidth, y - flameGlowWidth,
                width + flameGlowWidth * 2, cellHeight + flameGlowWidth * 2);
            Color glow = new Color(1, .55f, .10f, rowAlpha * .13f);
            for (int j = 0; j < 4; j++) glowColors[row * 4 + j] = glow;
        }
        flameMesh.colors32 = flameColors;
        if (glowMesh == null) return;
        glowMesh.vertices = glowVertices; glowMesh.colors32 = glowColors; glowMesh.RecalculateBounds();
        glowMaterial.SetFloat("_Intensity", glowIntensity * (1 + glowPulseAmount * Mathf.Sin(time * glowPulseSpeed)));
        flameGlowRenderer.enabled = glowIntensity > 0;
    }

    private static void WriteQuad(Vector3[] vertices, int start, float x, float y, float width, float height)
    {
        vertices[start] = new Vector3(x, y, -.01f);
        vertices[start + 1] = new Vector3(x + width, y, -.01f);
        vertices[start + 2] = new Vector3(x + width, y + height, -.01f);
        vertices[start + 3] = new Vector3(x, y + height, -.01f);
    }

    private MeshRenderer CreateRenderer(string label, Mesh mesh, Material material, int order)
    {
        var child = new GameObject(label) { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        child.transform.SetParent(generatedRoot.transform, false); child.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = child.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.sortingLayerID = bodyRenderer.sortingLayerID; renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows = false;
        return renderer;
    }
    private static void AddQuad(List<int> indices, int start)
    {
        indices.Add(start); indices.Add(start + 2); indices.Add(start + 1);
        indices.Add(start); indices.Add(start + 3); indices.Add(start + 2);
    }
    private sealed class Geometry
    {
        private readonly List<Vector3> vertices = new List<Vector3>();
        private readonly List<Color> colors = new List<Color>();
        private readonly List<int> triangles = new List<int>();
        public void Rect(float x, float y, float w, float h, Color color) => Quad(new Vector2(x - 12, y - 12) / 16,
            new Vector2(x + w - 12, y - 12) / 16, new Vector2(x + w - 12, y + h - 12) / 16, new Vector2(x - 12, y + h - 12) / 16, color);
        public void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
        {
            int start = vertices.Count; float z = -start * .00001f;
            vertices.Add(new Vector3(a.x, a.y, z)); vertices.Add(new Vector3(b.x, b.y, z));
            vertices.Add(new Vector3(c.x, c.y, z)); vertices.Add(new Vector3(d.x, d.y, z));
            for (int i = 0; i < 4; i++) colors.Add(color); AddQuad(triangles, start);
        }
        public Mesh ToMesh()
        {
            var mesh = new Mesh { name = "Geometric Wall Candle Mount", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
            mesh.uv = new Vector2[vertices.Count]; mesh.RecalculateBounds(); return mesh;
        }
    }
    private void OnValidate()
    {
        mountingDirection = (WallCandleDirection)Mathf.Clamp((int)mountingDirection, 0, 3);
        flameFrameDuration = Mathf.Max(.04f, flameFrameDuration); glowPulseSpeed = Mathf.Max(.1f, glowPulseSpeed);
        glowPulseAmount = Mathf.Clamp(glowPulseAmount, 0, .25f); glowIntensity = Mathf.Clamp(glowIntensity, 0, 6);
        flameGlowWidth = Mathf.Clamp(flameGlowWidth, .01f, .1f); dirty = true;
    }
    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Matrix4x4 previous = Gizmos.matrix; Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix; Gizmos.color = new Color(.35f, .9f, 1, .8f);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(EditorBoundsSize, EditorBoundsSize, 0));
        Gizmos.matrix = previous; Gizmos.color = previousColor;
    }
    private void OnDisable()
    {
        if (!Application.isPlaying) Release(); else if (generatedRoot != null) generatedRoot.SetActive(false);
    }
    private void OnDestroy() => Release();
    private void Release()
    {
        if (generatedRoot != null) generatedRoot.SetActive(false);
        Dispose(generatedRoot); Dispose(bodyMesh); Dispose(flameMesh); Dispose(glowMesh);
        Dispose(bodyMaterial); Dispose(glowMaterial);
        generatedRoot = null; bodyMesh = flameMesh = glowMesh = null; bodyMaterial = glowMaterial = null;
        flameGlowRenderer = null; glowVertices = null; flameColors = glowColors = null;
    }
    private static void Dispose(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
