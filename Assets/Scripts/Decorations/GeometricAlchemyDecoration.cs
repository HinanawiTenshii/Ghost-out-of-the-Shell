using System.Collections.Generic;
using UnityEngine;

/// <summary>Flat overhead alchemy props; cauldrons add a pooled world-sized bubble mesh.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D))]
[AddComponentMenu("Decorations/Geometric Alchemy Decoration")]
public sealed class GeometricAlchemyDecoration : MonoBehaviour
{
    public enum PropKind { Workbench, ReagentRack, Cauldron, Distillery, HerbTray, CrystalAltar }
    [SerializeField] private PropKind shape;
    [SerializeField] private Vector2 size = new Vector2(2.4f, 1.4f);
    [SerializeField] private bool solid = true;
    [Header("Materials / 材质配色")]
    [SerializeField] private Color wood = new Color(.67f, .39f, .20f, 1);
    [SerializeField] private Color stone = new Color(.67f, .71f, .74f, 1);
    [SerializeField] private Color accent = new Color(.18f, .85f, .49f, 1);
    [SerializeField] private int sortingOrder = -3;
    [SerializeField] private Material sourceMaterial;
    [Header("Cauldron Bubbles / 坩埚气泡（世界单位）")]
    [SerializeField] private bool animateBubbles = true;
    [SerializeField] private Vector2 bubbleRadiusRange = new Vector2(.06875f, .10625f);
    [SerializeField] private Vector2 bubbleGrowthTime = new Vector2(.8f, 1.35f);
    [SerializeField, Min(.05f)] private float bubbleInterval = .30f;
    [SerializeField, Min(.05f)] private float bubblePopDuration = .22f;

    private struct Bubble
    {
        public Vector2 anchor;
        public float age, radius, growTime, angle;
    }
    private readonly List<Bubble> bubbles = new List<Bubble>(8);
    private readonly List<Vector3> bubbleVertices = new List<Vector3>(600);
    private readonly List<Color> bubbleColors = new List<Color>(600);
    private readonly List<int> bubbleTriangles = new List<int>(1500);
    private readonly List<Vector2> bubbleUvs = new List<Vector2>(600);
    private readonly Vector2[] liquidPolygon = new Vector2[12];
    private Mesh bubbleMesh;
    private float nextBubble;

    public struct Part
    {
        public Vector2[] points;
        public Color color;
        public Part(Vector2[] p, Color c) { points = p; color = c; }
    }
    private GameObject visual;
    private Mesh mesh;
    private Material ownedMaterial;
    private bool dirty = true;
    private readonly List<Color> cauldronVertexColors = new List<Color>();
    private int liquidVertexStart = -1;
    public Color LiquidColor => accent;
    /// <summary>Recolor only the liquid vertices; do not rebuild the mesh or reset growing bubbles.</summary>
    public void SetLiquidColor(Color color)
    {
        if (shape != PropKind.Cauldron) return;
        color.a = 1f;
        if (accent == color) return;
        accent = color;
        if (mesh == null || dirty || liquidVertexStart < 0) return;
        for (int i = liquidVertexStart; i < cauldronVertexColors.Count; i++) cauldronVertexColors[i] = color;
        mesh.SetColors(cauldronVertexColors);
    }
    private void OnEnable() { dirty = true; }
    private void OnValidate()
    {
        size = new Vector2(Mathf.Max(.2f, size.x), Mathf.Max(.2f, size.y));
        bubbleRadiusRange.x = Mathf.Max(.01f, bubbleRadiusRange.x);
        bubbleRadiusRange.y = Mathf.Max(bubbleRadiusRange.x, bubbleRadiusRange.y);
        bubbleGrowthTime.x = Mathf.Max(.1f, bubbleGrowthTime.x);
        bubbleGrowthTime.y = Mathf.Max(bubbleGrowthTime.x, bubbleGrowthTime.y);
        bubbleInterval = Mathf.Max(.05f, bubbleInterval);
        bubblePopDuration = Mathf.Max(.05f, bubblePopDuration);
        dirty = true;
    }
    private void Update()
    {
        if (dirty || visual == null) Rebuild();
        if (Application.isPlaying && bubbleMesh != null) TickBubbles(Time.deltaTime);
    }
    private void OnDisable()
    {
        if (visual != null) visual.SetActive(false);
        bubbles.Clear();
        if (bubbleMesh != null) bubbleMesh.Clear();
    }
    private void OnDestroy() { Release(); }

    private void Rebuild()
    {
        Release(); dirty = false;
        var collider = GetComponent<BoxCollider2D>();
        collider.enabled = solid; collider.isTrigger = false;
        collider.offset = Vector2.zero; collider.size = size;
        var material = sourceMaterial;
        if (material == null)
        {
            var shader = Shader.Find("Sprites/Default");
            if (shader == null) return;
            ownedMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
            material = ownedMaterial;
        }
        var parts = BuildParts(shape, wood, stone, accent);
        Vector2 reference = ReferenceSize(shape);
        Vector2 scale = new Vector2(size.x / reference.x, size.y / reference.y);
        var vertices = new List<Vector3>(); var colors = new List<Color>();
        var triangles = new List<int>();
        for (int p = 0; p < parts.Count; p++)
        {
            int first = vertices.Count;
            if (shape == PropKind.Cauldron && p == parts.Count-1) liquidVertexStart = first;
            foreach (Vector2 point in parts[p].points)
            {
                vertices.Add(new Vector3(point.x * scale.x, point.y * scale.y, -p * .0001f));
                colors.Add(parts[p].color);
            }
            // Rectangles and convex facets only, viewed along the XY plane.
            for (int i = 1; i < parts[p].points.Length - 1; i++)
            { triangles.Add(first); triangles.Add(first + i + 1); triangles.Add(first + i); }
        }
        mesh = new Mesh { name = "Overhead Alchemy " + shape, hideFlags = HideFlags.HideAndDontSave };
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetTriangles(triangles, 0);
        if (shape == PropKind.Cauldron) cauldronVertexColors.AddRange(colors);
        mesh.uv = new Vector2[vertices.Count]; mesh.RecalculateBounds();
        visual = new GameObject("Generated Alchemy Geometry") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
        visual.transform.SetParent(transform, false);
        visual.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.sortingOrder = sortingOrder;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        if (Application.isPlaying && shape == PropKind.Cauldron && animateBubbles)
        {
            bubbleMesh = new Mesh { name = "Cauldron Bubbles", hideFlags = HideFlags.HideAndDontSave };
            bubbleMesh.MarkDynamic();
            var go = new GameObject("Bubbles") { hideFlags = HideFlags.HideAndDontSave, layer = gameObject.layer };
            go.transform.SetParent(visual.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = bubbleMesh;
            var bubbleRenderer = go.AddComponent<MeshRenderer>();
            bubbleRenderer.sharedMaterial = material;
            bubbleRenderer.sortingOrder = sortingOrder + 1;
            bubbleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            bubbleRenderer.receiveShadows = false;
            nextBubble = .10f;
        }
    }

    private Vector3 BubbleWorldCenter(Vector2 anchor)
    {
        Vector2 reference = ReferenceSize(PropKind.Cauldron);
        return transform.TransformPoint(new Vector3(anchor.x * size.x / reference.x,
            anchor.y * size.y / reference.y, -.006f));
    }

    // Tests all transformed liquid edges, including nonuniform/negative parent scales.
    // Insetting in WORLD units keeps the entire pop inside the liquid without scaling bubbles.
    public static bool HasBubbleRoom(Vector2 center, float radius, Vector2[] polygon)
    {
        float sign = 0;
        for (int i = 0; i < polygon.Length; i++)
        {
            Vector2 edge = polygon[(i + 1) % polygon.Length] - polygon[i];
            Vector2 offset = center - polygon[i];
            float cross = edge.x * offset.y - edge.y * offset.x;
            float length = edge.magnitude;
            if (length < .000001f || Mathf.Abs(cross) < radius * length) return false;
            if (i == 0) sign = cross;
            else if (cross * sign <= 0) return false;
        }
        return true;
    }

    public static float GrowingBubbleRadius(float age, float growTime, float maximum)
    {
        float t = Mathf.Clamp01(age / growTime);
        return maximum * Mathf.Lerp(.12f, 1f, t * t * (3f - 2f * t));
    }

    private void TickBubbles(float dt)
    {
        if (dt <= 0) return; // Pauses with gameplay.
        for (int i = 0; i < liquidPolygon.Length; i++)
        {
            float a = i * Mathf.PI * 2 / liquidPolygon.Length;
            liquidPolygon[i] = BubbleWorldCenter(new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (.62f * .66f));
        }
        for (int i = bubbles.Count - 1; i >= 0; i--)
        {
            Bubble b = bubbles[i]; b.age += dt;
            if (b.age >= b.growTime + bubblePopDuration ||
                !HasBubbleRoom(BubbleWorldCenter(b.anchor), b.radius * 1.9f, liquidPolygon))
                bubbles.RemoveAt(i);
            else bubbles[i] = b;
        }
        nextBubble -= dt;
        if (nextBubble <= 0 && bubbles.Count < 8)
        {
            nextBubble = bubbleInterval * Random.Range(.7f, 1.3f);
            float radius = Random.Range(bubbleRadiusRange.x, bubbleRadiusRange.y);
            // Bounded work. Tiny cauldrons simply stop emitting when world-size bubbles cannot fit.
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector2 anchor = Random.insideUnitCircle * (.62f * .66f);
                Vector3 center = BubbleWorldCenter(anchor);
                if (!HasBubbleRoom(center, radius * 1.9f, liquidPolygon)) continue;
                bool overlaps = false;
                foreach (Bubble other in bubbles)
                    if (Vector3.Distance(center, BubbleWorldCenter(other.anchor)) < (radius + other.radius) * 1.25f)
                    { overlaps = true; break; }
                if (overlaps) continue;
                bubbles.Add(new Bubble { anchor = anchor, radius = radius,
                    growTime = Random.Range(bubbleGrowthTime.x, bubbleGrowthTime.y), angle = Random.value * Mathf.PI * 2 });
                break;
            }
        }
        // Keep spawn/lifetime simulation identical; only defer invisible mesh work.
        if (!SceneVisualWorkBudget.IsVisible(transform, size)) return;
        bubbleVertices.Clear(); bubbleColors.Clear(); bubbleTriangles.Clear(); bubbleUvs.Clear();
        Color rim = Color.Lerp(accent, Color.white, .65f);
        Color fill = Color.Lerp(accent, Color.white, .20f);
        foreach (Bubble b in bubbles)
        {
            Vector3 center = BubbleWorldCenter(b.anchor);
            if (b.age < b.growTime)
            {
                float r = GrowingBubbleRadius(b.age, b.growTime, b.radius);
                BubbleDisc(center, r, rim);
                BubbleDisc(center, r * .72f, fill);
            }
            else
            {
                float t = Mathf.Clamp01((b.age - b.growTime) / bubblePopDuration);
                Color c = rim; c.a = 1 - t;
                // Small five-way liquid pop, not a combat explosion or damage effect.
                for (int i = 0; i < 5; i++)
                {
                    float a = b.angle + i * Mathf.PI * 2 / 5;
                    Vector3 offset = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * b.radius * Mathf.Lerp(.8f, 1.65f, t);
                    BubbleDisc(center + offset, b.radius * .22f * (1 - t), c);
                }
            }
        }
        bubbleMesh.Clear(); bubbleMesh.SetVertices(bubbleVertices); bubbleMesh.SetColors(bubbleColors);
        bubbleMesh.SetUVs(0, bubbleUvs); bubbleMesh.SetTriangles(bubbleTriangles, 0); bubbleMesh.RecalculateBounds();
    }

    private void BubbleDisc(Vector3 worldCenter, float radius, Color color)
    {
        int first = bubbleVertices.Count;
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.PI * 2 / 10;
            // Form a fixed-size circle in world space, then undo the ENTIRE transform hierarchy.
            Vector3 world = worldCenter + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0) * radius;
            bubbleVertices.Add(transform.InverseTransformPoint(world));
            bubbleColors.Add(color); bubbleUvs.Add(Vector2.zero);
        }
        for (int i = 1; i < 9; i++)
        { bubbleTriangles.Add(first); bubbleTriangles.Add(first + i + 1); bubbleTriangles.Add(first + i); }
    }

    public static Vector2 ReferenceSize(PropKind kind)
    {
        switch (kind)
        {
            case PropKind.Workbench: return new Vector2(2.4f, 1.4f);
            case PropKind.ReagentRack: return new Vector2(1.8f, 1.05f);
            case PropKind.Cauldron: return new Vector2(1.6f, 1.4f);
            case PropKind.Distillery: return new Vector2(2.2f, 1.4f);
            case PropKind.HerbTray: return new Vector2(1.4f, .9f);
            default: return new Vector2(1.5f, 1.5f);
        }
    }

    public static List<Part> BuildParts(PropKind kind, Color wood, Color stone, Color accent)
    {
        var p = new List<Part>();
        Color edge = new Color(.38f, .20f, .11f, 1);
        Color iron = new Color(.24f, .29f, .32f, 1);
        Color dark = new Color(.11f, .16f, .19f, 1);
        Color copper = new Color(.82f, .47f, .23f, 1);
        Color paper = new Color(.90f, .90f, .76f, 1);
        Color purple = new Color(.68f, .28f, .87f, 1);
        switch (kind)
        {
            case PropKind.Workbench:
                Frame(p, 2.4f, 1.4f, edge, wood);
                p.Add(Rect(0, 0, 2.10f, 1.10f, new Color(.20f, .54f, .51f, 1)));
                // Open book lies flat; only pages and the spine show.
                p.Add(Rect(-.54f, -.04f, .79f, .72f, edge));
                p.Add(Rect(-.73f, -.04f, .32f, .62f, paper));
                p.Add(Rect(-.35f, -.04f, .32f, .62f, paper));
                p.Add(Rect(-.73f, .12f, .20f, .035f, stone));
                p.Add(Rect(-.35f, .04f, .20f, .035f, stone));
                Vessel(p, .43f, -.22f, .25f, stone, dark, accent);
                p.Add(Rect(.50f, -.17f, .10f, .36f, stone)); // Pestle, viewed from above.
                Bottle(p, .27f, .34f, .16f, accent, stone, edge);
                Bottle(p, .78f, .29f, .18f, purple, stone, edge);
                break;
            case PropKind.ReagentRack:
                Frame(p, 1.8f, 1.05f, edge, wood);
                p.Add(Rect(0, 0, 1.55f, .79f, edge));
                foreach (float x in new[] { -.53f, 0f, .53f })
                    foreach (float y in new[] { -.24f, .24f })
                        Bottle(p, x, y, .18f, x < 0 ? accent : x > 0 ? purple : copper, stone, edge);
                p.Add(Rect(0, 0, 1.56f, .07f, wood));
                break;
            case PropKind.Cauldron:
                // Two handles in the same plane as the circular open rim; no front face.
                p.Add(Rect(-.65f, 0, .30f, .28f, iron));
                p.Add(Rect(.65f, 0, .30f, .28f, iron));
                p.Add(Rect(-.69f, 0, .13f, .12f, stone));
                p.Add(Rect(.69f, 0, .13f, .12f, stone));
                p.Add(Disc(0, 0, .70f, iron));
                Vessel(p, 0, 0, .62f, stone, dark, accent);
                break;
            case PropKind.Distillery:
                Frame(p, 2.2f, 1.4f, edge, wood);
                p.Add(Rect(0, 0, 1.96f, 1.16f, iron));
                p.Add(Disc(-.53f, .03f, .43f, dark));
                p.Add(Disc(-.53f, .03f, .37f, copper));
                p.Add(Disc(-.53f, .03f, .25f, Color.Lerp(copper, paper, .22f)));
                p.Add(Disc(-.53f, .03f, .09f, stone)); // Boiler lid cap.
                // Orthogonal cooling pipe and collection vessel, all seen from above.
                p.Add(Rect(-.02f, .13f, .60f, .12f, copper));
                p.Add(Rect(.24f, -.04f, .12f, .46f, copper));
                p.Add(Rect(.46f, -.21f, .54f, .12f, copper));
                p.Add(Rect(.67f, -.02f, .12f, .48f, copper));
                p.Add(Rect(.60f, .34f, .50f, .38f, stone));
                Vessel(p, .60f, .34f, .19f, stone, dark, accent);
                break;
            case PropKind.HerbTray:
                Frame(p, 1.4f, .9f, edge, wood);
                p.Add(Rect(0, 0, 1.16f, .66f, paper));
                foreach (float x in new[] { -.36f, .08f })
                {
                    p.Add(Rect(x, -.03f, .05f, .50f, edge));
                    foreach (float y in new[] { -.14f, .04f, .22f })
                    {
                        p.Add(Diamond(x - .10f, y, .20f, .12f, accent));
                        p.Add(Diamond(x + .08f, y - .06f, .18f, .11f, Color.Lerp(accent, paper, .22f)));
                    }
                }
                p.Add(Rect(.43f, -.15f, .18f, .12f, purple));
                p.Add(Rect(.40f, .07f, .14f, .14f, copper));
                break;
            case PropKind.CrystalAltar:
                Frame(p, 1.5f, 1.5f, iron, stone);
                p.Add(Rect(0, 0, 1.18f, 1.18f, iron));
                p.Add(Disc(0, 0, .52f, copper));
                p.Add(Disc(0, 0, .46f, iron));
                // Central crystal's plan-view facets, not an upright side silhouette.
                p.Add(Diamond(0, 0, .62f, .74f, purple));
                p.Add(new Part(new[] { new Vector2(0, -.37f), new Vector2(.31f, 0), new Vector2(0, .37f) }, accent));
                p.Add(Diamond(0, 0, .20f, .26f, Color.Lerp(accent, Color.white, .60f)));
                foreach (float x in new[] { -.61f, .61f })
                    foreach (float y in new[] { -.61f, .61f }) p.Add(Rect(x, y, .10f, .10f, copper));
                break;
        }
        return p;
    }
    private static void Frame(List<Part> p, float w, float h, Color edge, Color top)
    { p.Add(Rect(0, 0, w, h, edge)); p.Add(Rect(0, 0, w - .14f, h - .14f, top)); }
    private static void Vessel(List<Part> p, float x, float y, float r, Color rim, Color inside, Color liquid)
    { p.Add(Disc(x, y, r, rim)); p.Add(Disc(x, y, r * .81f, inside)); p.Add(Disc(x, y, r * .66f, liquid)); }
    private static void Bottle(List<Part> p, float x, float y, float r, Color liquid, Color glass, Color cork)
    {
        p.Add(Disc(x, y, r, glass)); p.Add(Disc(x, y, r * .78f, liquid));
        p.Add(Disc(x, y, r * .35f, cork));
    }
    private static Part Rect(float x, float y, float w, float h, Color c) => new Part(new[] {
        new Vector2(x-w/2,y-h/2), new Vector2(x+w/2,y-h/2),
        new Vector2(x+w/2,y+h/2), new Vector2(x-w/2,y+h/2) }, c);
    private static Part Diamond(float x, float y, float w, float h, Color c) => new Part(new[] {
        new Vector2(x,y-h/2), new Vector2(x+w/2,y), new Vector2(x,y+h/2), new Vector2(x-w/2,y) }, c);
    private static Part Disc(float x, float y, float r, Color c)
    {
        var points = new Vector2[12];
        for (int i = 0; i < points.Length; i++)
        { float a = i * Mathf.PI * 2 / points.Length; points[i] = new Vector2(x + Mathf.Cos(a) * r, y + Mathf.Sin(a) * r); }
        return new Part(points, c);
    }
    private void Release()
    {
        if (visual != null) visual.SetActive(false);
        ReleaseObject(visual); ReleaseObject(mesh); ReleaseObject(bubbleMesh); ReleaseObject(ownedMaterial);
        bubbles.Clear(); bubbleMesh = null;
        visual = null; mesh = null; ownedMaterial = null;
        cauldronVertexColors.Clear(); liquidVertexStart = -1;
    }
    private static void ReleaseObject(Object target)
    {
        if (target == null) return;
        if (Application.isPlaying) Destroy(target); else DestroyImmediate(target);
    }
}
