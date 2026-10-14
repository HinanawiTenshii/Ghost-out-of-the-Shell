using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Coarse stone pixels and purple flame blocks, batched into two meshes per meteor.</summary>
public sealed class MeteorShowerVisual : MonoBehaviour
{
    private static Material solid, glow;
    private static readonly Color Purple = new Color(.57f, .12f, .94f);
    private static readonly Color Light = new Color(.84f, .5f, 1f);
    private static readonly Color Core = new Color(.98f, .84f, 1f);
    private static readonly string[] Rock = { "..DDDD..", ".DHHHHD.", "DHLLHHHD", "DHLLHHHD", "DHHHHHHD", ".DHHHHD.", "..DDDD.." };
    private readonly List<Vector3> vertices = new List<Vector3>(1200);
    private readonly List<Color> colors = new List<Color>(1200);
    private readonly List<Vector2> uvs = new List<Vector2>(1200);
    private readonly List<int> triangles = new List<int>(1800);
    private Mesh ground, air;
    private MeshRenderer groundRenderer, airRenderer;
    private float startHeight, seed;
    private int lastFrame = -1;
    private bool hasFire;

    public void Configure(bool fire, float phase)
    {
        hasFire = fire; seed = phase * 7.31f;
        var camera = Camera.main;
        startHeight = camera != null && camera.orthographic
            ? Mathf.Max(12f, camera.transform.position.y + camera.orthographicSize * 1.5f - transform.position.y + 4f) : 16f;
        if (solid == null) solid = new Material(Shader.Find("Sprites/Default")) {
            name = "Meteor Stone Pixels", hideFlags = HideFlags.HideAndDontSave };
        if (glow == null)
        {
            var shader = Resources.Load<Shader>("HolyLightGlow");
            glow = new Material(shader != null && shader.isSupported ? shader : Shader.Find("Sprites/Default")) {
                name = "Meteor Purple Flame Glow", hideFlags = HideFlags.HideAndDontSave };
            if (glow.HasProperty("_CoreFraction")) { glow.SetFloat("_CoreFraction", .8f); glow.SetFloat("_Intensity", 1f); }
        }
        ground = MakeMesh("Meteor Impact And Fire", -5, glow, out groundRenderer);
        air = MakeMesh("Falling Meteor", 12, solid, out airRenderer);
    }
    private Mesh MakeMesh(string label, int order, Material material, out MeshRenderer renderer)
    {
        var go = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer)); go.transform.SetParent(transform, false);
        var mesh = new Mesh { name = label, hideFlags = HideFlags.HideAndDontSave }; mesh.MarkDynamic();
        go.GetComponent<MeshFilter>().sharedMesh = mesh; renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.sortingOrder = order;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return mesh;
    }
    public void RenderAt(float age)
    {
        int frame = Mathf.FloorToInt(age * 24f);
        if (frame == lastFrame || ground == null) return; lastFrame = frame;
        float impact = age - MeteorShowerSkillRuntime.FallDuration;
        float end = hasFire ? MeteorShowerSkillRuntime.FireDuration : .65f;
        float fade = 1f - Mathf.Clamp01((impact - end) / .25f);
        groundRenderer.enabled = fade > 0; airRenderer.enabled = impact < 0;
        if (fade <= 0) return;
        Clear();
        const float radius = MeteorShowerSkillRuntime.Radius;
        if (impact < 0)
        {
            float warning = .24f + .1f * Mathf.Sin(age * 18f);
            Ring(radius, .08f, Purple, warning);
            Ring(.38f, .1f, Light, .7f);
            Block(Vector2.zero, new Vector2(.18f, .8f), Light, .7f);
            Block(Vector2.zero, new Vector2(.8f, .18f), Light, .7f);
        }
        else
        {
            float flash = Mathf.Clamp01(1f - impact / .55f);
            // The outer edge is always exactly the gameplay radius. The inner
            // expanding ring and fragments provide motion without implying a larger hit area.
            Ring(radius, .12f, Light, flash * .85f);
            Disc(radius, Purple, flash * .14f);
            float spread = radius * Mathf.Clamp01(impact / .3f);
            Ring(spread, .34f * flash, Light, flash);
            Disc(Mathf.Min(.75f, spread), Core, flash * .8f);
            for (int i = 0; i < 22; i++)
            {
                float angle = i * 2.39996f + seed;
                Vector2 p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Min(radius - .2f, impact * (4f + i % 4));
                Block(p, Vector2.one * (.12f + i % 3 * .06f), i % 3 == 0 ? Core : Purple, flash);
            }
            if (hasFire)
            {
                Disc(radius, Purple, .065f * fade);
                Ring(radius, .065f, Purple, .18f * fade);
                // Stable disk distribution; only the small flame shapes grow,
                // split, rise and disappear. No per-flame objects or colliders.
                for (int i = 0; i < 42; i++)
                {
                    float angle = i * 2.39996f + seed;
                    Vector2 p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * Mathf.Sqrt((i + .5f) / 42f) * (radius - .35f);
                    float t = Mathf.Repeat(impact * (1.05f + (i % 3) * .13f) + i * .173f, 1f);
                    float size = (.18f + .18f * Mathf.Sin(t * Mathf.PI)) * (1f - t * .45f);
                    p.y += t * .42f;
                    float alpha = Mathf.Sin(t * Mathf.PI) * fade;
                    Block(p, new Vector2(size * 1.6f, size * 1.3f), Purple, alpha * .8f);
                    Block(p + new Vector2(size * .15f, size * .6f), new Vector2(size * .8f, size * 1.6f), Purple, alpha);
                    Block(p + new Vector2(-size * .18f, size * .2f), new Vector2(size * .6f, size * .95f), Light, alpha * .8f);
                    Block(p + new Vector2(size * .4f, size * 1.7f), Vector2.one * size * .5f, Purple, alpha);
                    Block(p + new Vector2(-size * .5f, size * 1.15f), Vector2.one * size * .4f, Light, alpha * .55f);
                }
            }
        }
        Upload(ground);
        if (impact >= 0) return;
        Clear();
        float travel = Mathf.Clamp01(age / MeteorShowerSkillRuntime.FallDuration);
        float height = startHeight * (1f - travel * travel);
        Vector2 stone = new Vector2(height * .22f, height);
        for (int i = 10; i >= 0; i--)
        {
            float flicker = .8f + .2f * Mathf.Sin(age * 28f + i * 2.1f + seed);
            Vector2 p = stone + new Vector2(.13f, .59f) * i;
            Block(p, Vector2.one * (1.02f - i * .07f) * flicker, i < 3 ? Light : Purple, 1f - i * .065f);
        }
        // Stepped, blunt stone silhouette, large enough to read through CRT.
        for (int y = 0; y < Rock.Length; y++) for (int x = 0; x < 8; x++)
        {
            char c = Rock[y][x]; if (c == '.') continue;
            Color tint = c == 'D' ? new Color(.19f, .09f, .27f) : c == 'H' ? new Color(.4f, .24f, .52f) : Light;
            Block(stone + new Vector2(x - 3.5f, 3 - y) * .14f, Vector2.one * .14f, tint, 1f);
        }
        Upload(air);
    }
    private void Block(Vector2 center, Vector2 size, Color color, float alpha)
    {
        Vector2 half = size * .5f;
        Quad(center + new Vector2(-half.x, -half.y), center + new Vector2(-half.x, half.y),
            center + half, center + new Vector2(half.x, -half.y), color, alpha);
    }
    private void Ring(float radius, float width, Color color, float alpha, bool filled = false)
    {
        if (radius <= 0 || width <= 0) return;
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI / 24, b = (i + 1) * Mathf.PI / 24;
            Vector2 da = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            float inner = Mathf.Max(0, radius - width);
            Quad(da * inner, db * inner, db * radius, da * radius, color, alpha, filled ? 2 : 1);
        }
    }
    private void Disc(float radius, Color color, float alpha) => Ring(radius, radius, color, alpha, true);
    private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, float alpha, int uvMode = 0)
    {
        if (alpha <= 0) return;
        int n = vertices.Count; color.a = alpha;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        for (int i = 0; i < 4; i++) colors.Add(color);
        // The glow shader fades along UV.y. Rings fade radially, not across
        // each segment; filled disks sample the core uniformly to avoid spokes.
        if (uvMode == 2) for (int i = 0; i < 4; i++) uvs.Add(Vector2.one * .5f);
        else {
            uvs.Add(Vector2.zero); uvs.Add(uvMode == 1 ? Vector2.right : Vector2.up);
            uvs.Add(Vector2.one); uvs.Add(uvMode == 1 ? Vector2.up : Vector2.right);
        }
        triangles.Add(n); triangles.Add(n + 1); triangles.Add(n + 2); triangles.Add(n); triangles.Add(n + 2); triangles.Add(n + 3);
    }
    private void Clear() { vertices.Clear(); colors.Clear(); uvs.Clear(); triangles.Clear(); }
    private void Upload(Mesh mesh) { mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0); }
    private void OnDestroy() { if (ground != null) Destroy(ground); if (air != null) Destroy(air); }
}
