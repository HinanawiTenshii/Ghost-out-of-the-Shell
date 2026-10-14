using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Two batched meshes per column: falling shafts and expanding, ground-level shockwaves.</summary>
public sealed class HolyLightColumnVisual : MonoBehaviour
{
    private static Material glow;
    private static readonly Color Gold = new Color(1f, .69f, .2f);
    private static readonly Color Ivory = new Color(1f, .96f, .8f);
    private const float MinimumShaftHeight = 24f;
    private Mesh ground, column;
    private MeshRenderer groundRenderer, columnRenderer;
    private readonly List<Vector3> vertices = new List<Vector3>(900);
    private readonly List<Color> colors = new List<Color>(900);
    private readonly List<Vector2> uvs = new List<Vector2>(900);
    private readonly List<int> triangles = new List<int>(1400);
    private float radius, hold;
    private Camera viewCamera;
    private float shaftHeight, descentStartHeight;
    private bool finished;

    public void Configure(float attackRadius, float duration)
    {
        radius = attackRadius; hold = duration;
        shaftHeight = MinimumShaftHeight;
        ExtendShaftAboveView();
        descentStartHeight = shaftHeight;
        if (glow == null)
        {
            var shader = Resources.Load<Shader>("HolyLightGlow");
            glow = new Material(shader != null && shader.isSupported ? shader : Shader.Find("Sprites/Default")) {
                name = "Holy Light Gold White Glow", hideFlags = HideFlags.HideAndDontSave };
            if (glow.HasProperty("_CoreFraction")) { glow.SetFloat("_CoreFraction", .52f); glow.SetFloat("_Intensity", 1.1f); }
        }
        ground = MakeMesh("Holy Light Ground Waves", -6, out groundRenderer);
        column = MakeMesh("Holy Light Descending Shafts", 12, out columnRenderer);
        RenderAt(0f);
    }
    private Mesh MakeMesh(string label, int order, out MeshRenderer renderer)
    {
        var child = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        child.transform.SetParent(transform, false);
        var mesh = new Mesh { name = label, hideFlags = HideFlags.HideAndDontSave }; mesh.MarkDynamic();
        child.GetComponent<MeshFilter>().sharedMesh = mesh;
        renderer = child.GetComponent<MeshRenderer>(); renderer.sharedMaterial = glow; renderer.sortingOrder = order;
        renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off; renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        return mesh;
    }
    public void RenderAt(float age)
    {
        if (ground == null || finished) return;
        float impactAge = age - HolyLightSkillRuntime.DescentDuration;
        float fade = 1f - Mathf.Clamp01((impactAge - hold) / HolyLightSkillRuntime.FadeDuration);
        if (fade <= 0f)
        {
            groundRenderer.enabled = columnRenderer.enabled = false; finished = true; return;
        }
        Clear();
        if (impactAge >= 0f)
        {
            float waveRadius = HolyLightSkillRuntime.GetShockwaveRadius(radius, impactAge, hold);
            // Grow the boundary with the damage front. All stroke geometry stays INSIDE it.
            Ring(waveRadius, .12f, Gold, .34f * fade);
            for (int i = 0; i < 3; i++)
            {
                float phase = impactAge / .72f - i / 3f;
                if (phase < 0f) continue;
                phase = Mathf.Repeat(phase, 1f);
                Ring(Mathf.Min(waveRadius, radius * (.06f + .94f * phase)), (.11f + .08f * radius) * (1f - .45f * phase),
                    i == 0 ? Ivory : Gold, (1f - phase) * .8f * fade);
            }
            // Short cardinal impact rays and a luminous contact point.
            float flash = Mathf.Clamp01(1f - impactAge / .3f);
            for (int i = 0; i < 4; i++)
            {
                Vector2 d = new Vector2(Mathf.Cos(i * Mathf.PI * .5f), Mathf.Sin(i * Mathf.PI * .5f));
                Vector2 n = new Vector2(-d.y, d.x) * (.06f + radius * .035f);
                Vector2 a = d * waveRadius * .12f, b = d * waveRadius * (.35f + .45f * (1f - flash));
                n *= Mathf.Clamp01(waveRadius / Mathf.Max(.01f, radius));
                Quad(a - n, b - n, b + n, a + n, Ivory, flash * fade);
            }
        }
        Upload(ground);
        Clear();
        ExtendShaftAboveView();
        float height = shaftHeight;
        float descent = Mathf.Clamp01(age / HolyLightSkillRuntime.DescentDuration);
        // Camera movement can extend the unseen top, but must not move the falling
        // front back up or change its existing landing time.
        float bottom = descentStartHeight * (1f - descent * descent);
        float flicker = .88f + .12f * Mathf.Sin(age * 23f);
        float width = radius > 1.5f ? 2.1f : 1.1f;
        Shaft(0f, width, bottom, height, Gold, .42f * fade * flicker);
        Shaft(0f, width * .22f, bottom, height, Ivory, .88f * fade);
        Shaft(-width * .43f, width * .17f, bottom + .18f, height, Gold, .56f * fade);
        Shaft(width * .43f, width * .17f, bottom + .18f, height, Gold, .56f * fade);
        if (impactAge >= 0f)
        {
            // Sparse drifting embers, generated in the same mesh without particle GameObjects.
            for (int i = 0; i < 10; i++)
            {
                float t = Mathf.Repeat(impactAge * .55f + i * .137f, 1f);
                float angle = i * 2.39996f;
                Vector2 p = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * (.16f + t * .52f);
                p.y += t * .8f;
                float s = (.035f + .013f * radius) * (1f - t);
                Quad(p + new Vector2(-s, -s), p + new Vector2(-s, s), p + new Vector2(s, s), p + new Vector2(s, -s),
                    i % 3 == 0 ? Ivory : Gold, (1f - t) * fade);
            }
        }
        Upload(column);
    }
    private void ExtendShaftAboveView()
    {
        if (viewCamera == null || !viewCamera.isActiveAndEnabled) viewCamera = Camera.main;
        if (viewCamera == null || !viewCamera.orthographic) return;
        // The game uses an orthographic XY view. Include roll/aspect in the world-Y
        // extent and leave a full screen of headroom for following, shake and zoom.
        Transform cameraTransform = viewCamera.transform;
        float halfHeight = viewCamera.orthographicSize *
            (Mathf.Abs(cameraTransform.up.y) + Mathf.Abs(cameraTransform.right.y) * viewCamera.aspect);
        float worldTop = cameraTransform.position.y + halfHeight + Mathf.Max(8f, halfHeight * 2f);
        float localTop = transform.InverseTransformPoint(new Vector3(transform.position.x, worldTop, transform.position.z)).y;
        // Never retract a live shaft when the camera moves back down.
        shaftHeight = Mathf.Max(shaftHeight, localTop);
    }
    private void Shaft(float x, float width, float bottom, float top, Color color, float alpha)
    {
        if (top <= bottom) return;
        Quad(new Vector2(x - width * .5f, bottom), new Vector2(x - width * .5f, top),
            new Vector2(x + width * .5f, top), new Vector2(x + width * .5f, bottom), color, alpha);
    }
    private void Ring(float outerRadius, float width, Color color, float alpha)
    {
        const int segments = 48;
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments, b = (i + 1) * Mathf.PI * 2f / segments;
            Vector2 da = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), db = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
            float inner = Mathf.Max(0f, outerRadius - width), outer = outerRadius;
            Quad(da * inner, db * inner, db * outer, da * outer, color, alpha);
        }
    }
    private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color, float alpha)
    {
        int start = vertices.Count; color.a = alpha;
        vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
        for (int i = 0; i < 4; i++) colors.Add(color);
        uvs.Add(new Vector2(0, 0)); uvs.Add(new Vector2(1, 0)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(0, 1));
        triangles.Add(start); triangles.Add(start + 1); triangles.Add(start + 2);
        triangles.Add(start); triangles.Add(start + 2); triangles.Add(start + 3);
    }
    private void Clear() { vertices.Clear(); colors.Clear(); uvs.Clear(); triangles.Clear(); }
    private void Upload(Mesh mesh)
    {
        mesh.Clear(); mesh.SetVertices(vertices); mesh.SetColors(colors); mesh.SetUVs(0, uvs); mesh.SetTriangles(triangles, 0);
    }
    private void OnDestroy() { if (ground != null) Destroy(ground); if (column != null) Destroy(column); }
}
