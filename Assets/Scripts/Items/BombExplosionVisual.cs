using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Flat-colour vector blast: fast flash, rolling fire, pressure rim and drifting smoke.
/// Visual only: the caller still owns damage, audio and investigation. One bounded
/// dynamic mesh replaces the old per-fragment GameObjects.
/// </summary>
public sealed class BombExplosionVisual : MonoBehaviour
{
    private struct Fragment
    {
        public Vector2 Direction;
        public float Reach, Width, Delay, End, Turn;
    }
    private struct Puff
    {
        public Vector2 Direction;
        public float Radius, Delay, Drift, Shade, Reach;
    }

    private const int CircleSteps = 32;
    private static Material sharedMaterial;
    private static readonly Vector2[] circle = CreateUnitCircle();
    private readonly List<Vector3> vertices = new List<Vector3>(2048);
    private readonly List<Color32> colors = new List<Color32>(2048);
    private readonly List<int> triangles = new List<int>(6144);
    private readonly Fragment[] fragments = new Fragment[20];
    private readonly Puff[] puffs = new Puff[9];
    private Mesh mesh;
    private MeshRenderer meshRenderer;
    private int fragmentCount;
    private float duration, lifetime, elapsed, phase;
    private Vector2 blastSize;
    private Color blastColor, hotColor, smokeColor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache()
    {
        if (sharedMaterial != null) Destroy(sharedMaterial);
        sharedMaterial = null;
    }

    public void Configure(Vector2 size, float visualDuration, Color color,
        int sortingLayerId, int sortingOrder, int whiteParticleCount = 9)
    {
        duration = Mathf.Max(.08f, visualDuration);
        // Only translucent smoke survives this short tail, never another damage pulse.
        lifetime = duration + Mathf.Min(.28f, duration * .55f);
        elapsed = 0;
        blastSize = new Vector2(Mathf.Max(.05f, size.x), Mathf.Max(.05f, size.y));
        blastColor = new Color(color.r, color.g, color.b, Mathf.Max(.65f, color.a));
        hotColor = Color.Lerp(blastColor, Color.white, .67f);
        smokeColor = Color.Lerp(new Color(.22f, .25f, .29f), blastColor, .12f);
        fragmentCount = Mathf.Clamp(whiteParticleCount, 4, fragments.Length);
        phase = Random.Range(0f, Mathf.PI * 2);
        for (int i = 0; i < fragmentCount; i++)
        {
            float angle = phase + i * Mathf.PI * 2 / fragmentCount + Random.Range(-.18f, .18f);
            fragments[i] = new Fragment {
                Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                Reach = Random.Range(.38f, .49f), Width = Random.Range(.009f, .018f),
                Delay = Random.Range(0f, .055f), End = Random.Range(.65f, .97f), Turn = Random.Range(-.65f, .65f)
            };
        }
        for (int i = 0; i < puffs.Length; i++)
        {
            float angle = phase + i * Mathf.PI * 2 / puffs.Length + Random.Range(-.22f, .22f);
            puffs[i] = new Puff {
                Direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)),
                Radius = Random.Range(.076f, .114f), Delay = Random.Range(.04f, .18f),
                Drift = Random.Range(.01f, .055f), Shade = Random.Range(.02f, .17f),
                Reach = i % 3 == 0 ? Random.Range(.1f, .17f) : Random.Range(.24f, .31f)
            };
        }
        EnsureMesh();
        meshRenderer.sortingLayerID = sortingLayerId;
        meshRenderer.sortingOrder = Mathf.Clamp(sortingOrder + 2, short.MinValue, short.MaxValue);
        if (GetComponent<CameraVisionStreamingExempt>() == null)
            gameObject.AddComponent<CameraVisionStreamingExempt>();
        ApplyFrame(0);
    }

    private void EnsureMesh()
    {
        if (sharedMaterial == null)
            sharedMaterial = new Material(Shader.Find("Sprites/Default")) {
                name = "Flat Vector Explosion", mainTexture = Texture2D.whiteTexture,
                hideFlags = HideFlags.HideAndDontSave
            };
        if (mesh == null)
        {
            mesh = new Mesh { name = "Vector Explosion Geometry", hideFlags = HideFlags.HideAndDontSave };
            mesh.MarkDynamic();
            var filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null) meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            meshRenderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }
        meshRenderer.sharedMaterial = sharedMaterial;
    }

    private void Update()
    {
        if (mesh == null || Time.deltaTime <= 0) return;
        elapsed += Time.deltaTime;
        if (elapsed >= lifetime) { Destroy(gameObject); return; }
        ApplyFrame(elapsed);
    }

    private void ApplyFrame(float age)
    {
        vertices.Clear(); colors.Clear(); triangles.Clear();
        float t = Mathf.Clamp01(age / duration);
        float total = Mathf.Clamp01(age / lifetime);
        // Staggered faceted smoke puffs behind the fire, with small flat highlight caps.
        for (int i = 0; i < puffs.Length; i++)
        {
            Puff puff = puffs[i];
            float p = Mathf.Clamp01((total - puff.Delay) / (1f - puff.Delay));
            float alpha = Mathf.SmoothStep(0, 1, p * 7) * (1f - SmoothRange(.36f, 1f, p));
            if (alpha <= .001f) continue;
            float travel = EaseOut(p);
            Vector2 position = puff.Direction * Mathf.Lerp(.04f, puff.Reach, travel) + Vector2.up * (p * puff.Drift);
            Vector2 radius = Vector2.one * puff.Radius * Mathf.Lerp(.7f, 1.3f, travel);
            radius.y *= .86f;
            Color shade = Color.Lerp(smokeColor, new Color(.53f, .54f, .53f), puff.Shade);
            AddDisc(position, radius, WithAlpha(shade, alpha * .66f), 12, phase + i);
            AddDisc(position + new Vector2(-.012f, .02f) * travel, radius * .63f,
                WithAlpha(Color.Lerp(shade, Color.white, .12f), alpha * .26f), 12, phase + i);
        }

        float expansion = EaseOut(Mathf.Clamp01(t / .62f));
        float fireAlpha = 1f - SmoothRange(.2f, .79f, t);
        if (fireAlpha > .001f)
        {
            float radius = Mathf.Lerp(.075f, .32f, expansion);
            float roll = phase + t * .28f;
            AddCloud(radius, WithAlpha(Color.Lerp(blastColor, new Color(.16f, .12f, .13f), .25f), fireAlpha * .9f), roll);
            AddCloud(radius * .85f, WithAlpha(blastColor, fireAlpha), roll + .09f);
            AddCloud(radius * .57f, WithAlpha(Color.Lerp(blastColor, Color.white, .42f), fireAlpha), roll + .53f);
        }

        // Narrow pressure rim follows the configured blast footprint, not the AI range.
        float rimAlpha = (1f - SmoothRange(.2f, .69f, t)) * .76f;
        if (rimAlpha > .001f)
            AddArc(.11f + .385f * EaseOut(Mathf.Clamp01(t / .65f)), Mathf.Lerp(.018f, .004f, t),
                0, Mathf.PI * 2, WithAlpha(hotColor, rimAlpha), 64);
        if (t > .18f && t < .8f)
        {
            float alpha = (1f - SmoothRange(.3f, .8f, t)) * .35f;
            for (int i = 0; i < 6; i++)
                AddArc(.07f + .37f * expansion, .006f, phase + i * Mathf.PI / 3,
                    .34f, WithAlpha(hotColor, alpha), 6);
        }

        for (int i = 0; i < fragmentCount; i++)
        {
            Fragment piece = fragments[i];
            float p = Mathf.Clamp01((t - piece.Delay) / (piece.End - piece.Delay));
            if (t < piece.Delay || p >= 1) continue;
            float travel = EaseOut(p);
            Vector2 side = new Vector2(-piece.Direction.y, piece.Direction.x);
            Vector2 position = piece.Direction * Mathf.Lerp(.04f, piece.Reach, travel)
                + side * Mathf.Sin(p * Mathf.PI) * piece.Turn * .018f;
            float length = Mathf.Lerp(.105f, .017f, travel);
            float width = piece.Width * Mathf.Lerp(1f, .45f, p);
            float turn = p * piece.Turn;
            Vector2 forward = piece.Direction * Mathf.Cos(turn) + side * Mathf.Sin(turn);
            Vector2 across = new Vector2(-forward.y, forward.x);
            Color tint = Color.Lerp(Color.white, hotColor, SmoothRange(.2f, .85f, p));
            AddShard(position, forward * length, across * width,
                WithAlpha(tint, 1f - SmoothRange(.4f, 1f, p)));
            if ((i & 1) == 0)
                AddShard(position * .78f - side * .035f, across * width * 1.6f, forward * width * .6f,
                    WithAlpha(Color.Lerp(blastColor, Color.white, .25f), (1f - p) * .85f));
        }
        float flashAlpha = 1f - SmoothRange(.04f, .28f, t);
        if (flashAlpha > .001f)
            AddCloud(Mathf.Lerp(.04f, .14f, EaseOut(Mathf.Clamp01(t / .28f))),
                WithAlpha(Color.Lerp(blastColor, Color.white, .91f), flashAlpha), phase);

        mesh.Clear();
        mesh.SetVertices(vertices); mesh.SetColors(colors);
        mesh.SetTriangles(triangles, 0, false);
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(blastSize.x * 1.12f, blastSize.y * 1.12f, .1f));
        meshRenderer.enabled = vertices.Count > 0;
    }

    private static float SmoothRange(float from, float to, float value)
        => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, value));
    private static float EaseOut(float t) { float inv = 1f - t; return 1f - inv * inv * inv; }
    private Color WithAlpha(Color color, float alpha) { color.a = alpha * blastColor.a; return color; }
    private void Vertex(Vector2 point, Color32 color)
    {
        vertices.Add(new Vector3(point.x * blastSize.x, point.y * blastSize.y, 0));
        colors.Add(color);
    }
    private void AddCloud(float radius, Color tint, float rotation)
    {
        int start = vertices.Count;
        Vertex(Vector2.zero, tint);
        for (int i = 0; i < CircleSteps; i++)
        {
            float angle = i * Mathf.PI * 2 / CircleSteps;
            float lobes = 1f + .065f * Mathf.Sin(angle * 3 + rotation) +
                .055f * Mathf.Sin(angle * 7 - rotation) + .035f * Mathf.Sin(angle * 5 + rotation * 2);
            Vertex(circle[i] * (radius * lobes), tint);
            Triangle(start, start + 1 + i, start + 1 + (i + 1) % CircleSteps);
        }
    }
    private void AddDisc(Vector2 position, Vector2 radius, Color tint, int steps, float rotation)
    {
        int start = vertices.Count;
        Vertex(position, tint);
        for (int i = 0; i < steps; i++)
        {
            float angle = rotation + i * Mathf.PI * 2 / steps;
            Vertex(position + new Vector2(Mathf.Cos(angle) * radius.x, Mathf.Sin(angle) * radius.y), tint);
            Triangle(start, start + 1 + i, start + 1 + (i + 1) % steps);
        }
    }
    private void AddArc(float radius, float thickness, float startAngle, float sweep, Color tint, int steps)
    {
        int start = vertices.Count;
        for (int i = 0; i <= steps; i++)
        {
            float angle = startAngle + i * sweep / steps;
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vertex(direction * radius, tint); Vertex(direction * (radius - thickness), tint);
            if (i == 0) continue;
            int v = start + i * 2;
            Triangle(v - 2, v - 1, v); Triangle(v, v - 1, v + 1);
        }
    }
    private void AddShard(Vector2 position, Vector2 along, Vector2 across, Color tint)
    {
        int start = vertices.Count;
        Vertex(position + along * .15f, tint); Vertex(position + across, tint);
        Vertex(position - along, tint); Vertex(position - across * .65f, tint);
        Triangle(start, start + 1, start + 2); Triangle(start, start + 2, start + 3);
    }
    private void Triangle(int a, int b, int c) { triangles.Add(a); triangles.Add(b); triangles.Add(c); }
    private static Vector2[] CreateUnitCircle()
    {
        var result = new Vector2[CircleSteps];
        for (int i = 0; i < result.Length; i++)
        {
            float angle = i * Mathf.PI * 2 / result.Length;
            result[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }
        return result;
    }
    private void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
