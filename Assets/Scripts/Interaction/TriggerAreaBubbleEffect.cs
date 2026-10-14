using System.Collections.Generic;
using UnityEngine;

/// <summary>Cauldron-style grow/pop bubbles, clipped to an arbitrary 2D trigger, in one pooled mesh.</summary>
internal sealed class TriggerAreaBubbleEffect
{
    private struct Bubble { public Vector2 anchor; public float age, radius, growTime, angle; }
    private readonly Transform owner;
    private readonly GameObject root;
    private readonly Mesh mesh;
    private readonly Material material;
    private readonly MeshRenderer renderer;
    private readonly List<Bubble> bubbles = new List<Bubble>(8);
    private readonly List<Vector3> vertices = new List<Vector3>(600);
    private readonly List<Color> colors = new List<Color>(600);
    private readonly List<int> triangles = new List<int>(1500);
    private readonly List<Vector2> uvs = new List<Vector2>(600);
    private readonly List<Vector2> path = new List<Vector2>(32);
    private Collider2D source;
    private float nextBubble = .1f;

    public TriggerAreaBubbleEffect(Transform owner)
    {
        this.owner = owner;
        root = new GameObject("Temporary Area Cauldron Bubbles", typeof(MeshFilter), typeof(MeshRenderer))
            { hideFlags = HideFlags.DontSave, layer = owner.gameObject.layer };
        root.transform.SetParent(owner, false);
        mesh = new Mesh { name = "Trigger Area Cauldron Bubbles", hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic(); root.GetComponent<MeshFilter>().sharedMesh = mesh;
        material = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        renderer = root.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
        renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
    }
    public void Tick(Collider2D collider, float deltaTime, Color accent, Vector2 radiusRange, Vector2 growthTime,
        float interval, float popDuration, int layer, int order)
    {
        if (deltaTime <= 0f) return;
        if (source != collider) { Clear(); source = collider; }
        if (source == null || !source.enabled || !source.gameObject.activeInHierarchy ||
            Mathf.Abs(owner.localToWorldMatrix.determinant) < .000001f)
        { Clear(); return; }
        renderer.sortingLayerID = layer; renderer.sortingOrder = order; root.layer = owner.gameObject.layer;
        renderer.enabled = true;
        for (int i = bubbles.Count - 1; i >= 0; i--)
        {
            Bubble bubble = bubbles[i]; bubble.age += deltaTime;
            if (bubble.age >= bubble.growTime + popDuration || !HasRoom(Center(bubble.anchor), bubble.radius * 1.9f)) bubbles.RemoveAt(i);
            else bubbles[i] = bubble;
        }
        nextBubble -= deltaTime;
        if (nextBubble <= 0f && bubbles.Count < 8)
        {
            nextBubble = interval * Random.Range(.7f, 1.3f);
            Bounds bounds = source.bounds;
            float radius = Random.Range(radiusRange.x, radiusRange.y);
            for (int attempt = 0; attempt < 24; attempt++)
            {
                Vector2 point = new Vector2(Random.Range(bounds.min.x, bounds.max.x), Random.Range(bounds.min.y, bounds.max.y));
                if (!HasRoom(point, radius * 1.9f)) continue;
                bool overlaps = false;
                foreach (var other in bubbles)
                    if (Vector2.Distance(point, Center(other.anchor)) < (radius + other.radius) * 1.25f) { overlaps = true; break; }
                if (overlaps) continue;
                bubbles.Add(new Bubble {
                    anchor = source.transform.InverseTransformPoint(point), radius = radius,
                    growTime = Random.Range(growthTime.x, growthTime.y), angle = Random.value * Mathf.PI * 2f
                });
                break;
            }
        }
        vertices.Clear(); colors.Clear(); triangles.Clear(); uvs.Clear();
        Color rim = Color.Lerp(accent, Color.white, .65f), fill = Color.Lerp(accent, Color.white, .2f);
        foreach (var bubble in bubbles)
        {
            Vector3 center = Center(bubble.anchor);
            if (bubble.age < bubble.growTime)
            {
                float radius = GeometricAlchemyDecoration.GrowingBubbleRadius(bubble.age, bubble.growTime, bubble.radius);
                Disc(center, radius, rim); Disc(center, radius * .72f, fill);
            }
            else
            {
                float t = Mathf.Clamp01((bubble.age - bubble.growTime) / popDuration);
                Color color = rim; color.a = 1f - t;
                for (int i = 0; i < 5; i++)
                {
                    float angle = bubble.angle + i * Mathf.PI * 2f / 5f;
                    var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * bubble.radius * Mathf.Lerp(.8f, 1.65f, t);
                    Disc(center + offset, bubble.radius * .22f * (1f - t), color);
                }
            }
        }
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0); mesh.RecalculateBounds();
    }
    private Vector3 Center(Vector2 anchor)
    {
        Vector3 center = source.transform.TransformPoint(anchor);
        center.z = owner.position.z - .006f;
        return center;
    }
    private bool HasRoom(Vector2 center, float radius)
    {
        if (!source.OverlapPoint(center)) return false;
        // Exact edge clearance for rectangular/polygonal regions, including
        // concave shapes, offsets, rotated and mirrored/non-uniform transforms.
        if (source is BoxCollider2D box)
        {
            Vector2 half = box.size * .5f;
            Vector2 previous = box.transform.TransformPoint(box.offset + new Vector2(-half.x, -half.y));
            for (int i = 0; i < 4; i++)
            {
                Vector2 corner = i == 0 ? new Vector2(half.x,-half.y) : i == 1 ? new Vector2(half.x,half.y)
                    : i == 2 ? new Vector2(-half.x,half.y) : new Vector2(-half.x,-half.y);
                Vector2 next = box.transform.TransformPoint(box.offset + corner);
                if (EdgeDistance(center, previous, next) < radius) return false;
                previous = next;
            }
            return true;
        }
        if (source is PolygonCollider2D polygon)
        {
            for (int p = 0; p < polygon.pathCount; p++)
            {
                path.Clear(); polygon.GetPath(p, path);
                if (path.Count < 3) return false;
                Vector2 previous = polygon.transform.TransformPoint(path[path.Count - 1] + polygon.offset);
                foreach (var vertex in path)
                {
                    Vector2 next = polygon.transform.TransformPoint(vertex + polygon.offset);
                    if (EdgeDistance(center, previous, next) < radius) return false;
                    previous = next;
                }
            }
            return true;
        }
        // Round/capsule/composite colliders use their real physics shape instead
        // of an AABB; a small safety margin keeps the entire pop inside the rim.
        for (int i = 0; i < 32; i++)
        {
            float angle = i * Mathf.PI * 2f / 32f;
            if (!source.OverlapPoint(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (radius * 1.01f))) return false;
        }
        return true;
    }
    private static float EdgeDistance(Vector2 point, Vector2 start, Vector2 end)
    {
        Vector2 edge = end - start;
        float t = edge.sqrMagnitude < .000001f ? 0f : Mathf.Clamp01(Vector2.Dot(point - start, edge) / edge.sqrMagnitude);
        return Vector2.Distance(point, start + edge * t);
    }
    private void Disc(Vector3 center, float radius, Color color)
    {
        int first = vertices.Count;
        for (int i = 0; i < 10; i++)
        {
            float angle = i * Mathf.PI * 2f / 10f;
            vertices.Add(owner.InverseTransformPoint(center + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * radius));
            colors.Add(color); uvs.Add(Vector2.zero);
        }
        for (int i = 1; i < 9; i++) { triangles.Add(first); triangles.Add(first+i+1); triangles.Add(first+i); }
    }
    public void Clear()
    {
        bubbles.Clear(); nextBubble = .1f;
        if (mesh != null) mesh.Clear();
        if (renderer != null) renderer.enabled = false;
    }
    public void Dispose()
    {
        Clear();
        if (root != null) { root.SetActive(false); Object.Destroy(root); }
        if (mesh != null) Object.Destroy(mesh);
        if (material != null) Object.Destroy(material);
    }
}
