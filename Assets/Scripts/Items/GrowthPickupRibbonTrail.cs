using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One bounded, soft-edged mesh for every pickup ribbon. History survives the
/// particle head and is clipped from the tail, so arrival consumes the whole strip.
/// Positions stay continuous; there is deliberately no pixel snapping.
/// </summary>
internal sealed class GrowthPickupRibbonTrail
{
    private const int Capacity = 64;
    private sealed class Ribbon
    {
        public readonly Vector3[] points = new Vector3[Capacity];
        public readonly float[] times = new float[Capacity];
        public int first, count;
        public float width, arrivalTime = -1f;
        public Vector3 head, arrivalPosition;
        public int Index(int offset) => (first + offset) % Capacity;
    }

    private readonly Ribbon[] ribbons;
    private readonly float duration;
    private readonly Transform root;
    private readonly Mesh mesh;
    private readonly List<Vector3> vertices;
    private readonly List<Color> colors;
    private readonly List<Vector2> uvs;
    private readonly List<int> triangles;
    private readonly List<Vector3> path = new List<Vector3>(Capacity + 1);
    private readonly List<float> distances = new List<float>(Capacity + 1);
    public bool HasVisibleRibbons { get; private set; }

    public GrowthPickupRibbonTrail(Transform parent, int count, float seconds, Material material, int order)
    {
        duration = seconds;
        ribbons = new Ribbon[count];
        for (int i = 0; i < count; i++) ribbons[i] = new Ribbon();
        int maximumVertices = count * (Capacity + 1) * 5;
        vertices = new List<Vector3>(maximumVertices);
        colors = new List<Color>(maximumVertices);
        uvs = new List<Vector2>(maximumVertices);
        triangles = new List<int>(count * Capacity * 24);
        var go = new GameObject("Absorbing Light Ribbons");
        root = go.transform;
        root.SetParent(parent, false);
        mesh = new Mesh { name = "Growth Pickup Ribbon Mesh", hideFlags = HideFlags.HideAndDontSave };
        mesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = order;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    public void SetWidth(int id, float width) { ribbons[id].width = width; }

    public void Record(int id, Vector3 position, float time, bool force = false)
    {
        Ribbon ribbon = ribbons[id];
        ribbon.head = position;
        if (ribbon.count > 0)
        {
            int last = ribbon.Index(ribbon.count - 1);
            if (time <= ribbon.times[last])
            {
                ribbon.points[last] = position;
                return;
            }
            // Bound storage even on high-refresh monitors. The live head is
            // rendered separately, so this does not quantize its movement.
            if (!force && time - ribbon.times[last] < 1f / 120f) return;
        }
        if (ribbon.count == Capacity)
        {
            ribbon.first = (ribbon.first + 1) % Capacity;
            ribbon.count--;
        }
        int next = ribbon.Index(ribbon.count++);
        ribbon.points[next] = position;
        ribbon.times[next] = time;
    }

    public void Arrive(int id, Vector3 position, float time)
    {
        Record(id, position, time, true);
        ribbons[id].arrivalPosition = position;
        ribbons[id].arrivalTime = time;
    }

    public void Clear(int id) { ribbons[id].count = 0; ribbons[id].arrivalTime = -1f; }
    public void ClearAll() { for (int i = 0; i < ribbons.Length; i++) Clear(i); }

    public void Render(float time, bool hasTarget, Vector3 destination)
    {
        vertices.Clear(); colors.Clear(); uvs.Clear(); triangles.Clear();
        HasVisibleRibbons = false;
        for (int id = 0; id < ribbons.Length; id++)
        {
            Ribbon ribbon = ribbons[id];
            if (ribbon.arrivalTime >= 0f && time >= ribbon.arrivalTime + duration) Clear(id);
            if (ribbon.count == 0) continue;
            float cutoff = time - duration;
            while (ribbon.count > 1 && ribbon.times[ribbon.Index(1)] <= cutoff)
            {
                ribbon.first = ribbon.Index(1);
                ribbon.count--;
            }
            path.Clear(); distances.Clear();
            Vector3 first = ribbon.points[ribbon.first];
            if (ribbon.count > 1)
            {
                int next = ribbon.Index(1);
                float t = Mathf.InverseLerp(ribbon.times[ribbon.first], ribbon.times[next], cutoff);
                first = Vector3.Lerp(first, ribbon.points[next], t);
            }
            AddPathPoint(first);
            for (int i = 1; i < ribbon.count; i++) AddPathPoint(ribbon.points[ribbon.Index(i)]);
            AddPathPoint(ribbon.head);
            if (path.Count < 2) continue;
            float length = distances[distances.Count - 1];
            HasVisibleRibbons = true;
            float absorption = ribbon.arrivalTime >= 0f
                ? Mathf.Clamp01((time - ribbon.arrivalTime) / duration) : 0f;
            Vector3 follow = ribbon.arrivalTime >= 0f && hasTarget
                ? destination - ribbon.arrivalPosition : Vector3.zero;
            // Follow the absorbing body, but never stretch a finished ribbon
            // across the map when possession switches to a distant character.
            if (follow.sqrMagnitude > 9f) follow = Vector3.zero;
            int start = vertices.Count;
            for (int i = 0; i < path.Count; i++)
            {
                float u = distances[i] / length;
                Vector3 tangent = path[Mathf.Min(i + 1, path.Count - 1)] - path[Mathf.Max(0, i - 1)];
                Vector3 normal = new Vector3(-tangent.y, tangent.x, 0f).normalized;
                // A tapered tail, fuller shoulder and narrow tip: a ribbon, not
                // a uniform laser line. Width also contracts during absorption.
                float width = ribbon.width * 0.75f * Mathf.Pow(u, 0.65f)
                    * (1f - 0.8f * Mathf.Pow(u, 6f)) * Mathf.Sqrt(1f - absorption);
                Vector3 center = path[i] + follow * u * u;
                for (int rail = 0; rail < 5; rail++)
                {
                    float across = (rail - 2) * 0.5f;
                    vertices.Add(root.InverseTransformPoint(center + normal * across * width));
                    Color color = rail == 2 ? Color.white : new Color(0.7f, 0.92f, 1f, 1f);
                    color.a = (rail == 0 || rail == 4 ? 0f : rail == 2 ? 0.95f : 0.42f)
                        * Mathf.SmoothStep(0f, 1f, Mathf.Min(u * 4f, 1f));
                    colors.Add(color);
                    uvs.Add(new Vector2(u, rail * 0.25f));
                }
                if (i == 0) continue;
                for (int rail = 0; rail < 4; rail++)
                {
                    int a = start + (i - 1) * 5 + rail;
                    triangles.Add(a); triangles.Add(a + 5); triangles.Add(a + 1);
                    triangles.Add(a + 1); triangles.Add(a + 5); triangles.Add(a + 6);
                }
            }
        }
        mesh.Clear(false);
        mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateBounds();
    }

    private void AddPathPoint(Vector3 point)
    {
        float step = path.Count > 0 ? Vector3.Distance(point, path[path.Count - 1]) : 0f;
        if (path.Count > 0 && step < 0.0001f) return;
        distances.Add(path.Count > 0 ? distances[distances.Count - 1] + step : 0f);
        path.Add(point);
    }

    public void Dispose() { if (mesh != null) UnityEngine.Object.Destroy(mesh); }
}
