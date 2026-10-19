using UnityEngine;

/// <summary>One batched mesh per visual layer, including all six streams of fragments.</summary>
[ExecuteAlways, DisallowMultipleComponent]
public sealed partial class SummoningCircleVisual : MonoBehaviour
{
    [SerializeField, Min(.5f)] private float radius = 2.1f;
    [SerializeField] private Color runeColor = new Color(.62f, .25f, .86f, 1);
    [SerializeField] private int sortingOrder = -4;
    [SerializeField, Range(24, 120)] private int fragmentsPerCharacter = 72;
    [Header("Activated Crystals / 已激活水晶")]
    [SerializeField, Min(.5f), Tooltip("已激活水晶的呼吸周期（秒）。")]
    private float crystalBreathDuration = 3f;
    [SerializeField, Range(0f, 3f), Tooltip("水晶局部泛光强度，不影响献祭粒子与召唤闪光。")]
    private float crystalGlowIntensity = 1.3f;
    [SerializeField, Min(.5f), Tooltip("水晶外围柔光的直径（法阵局部单位）。")]
    private float crystalHaloSize = 1.8f;
    private SummoningCircle circle;
    private CrystalMesh seal, crystals, glow, crystalBloom, fragments, fragmentGlow;
    private Material solidMaterial, glowMaterial, crystalBloomMaterial;
    private Renderer visibilityProbe;
    private float nextFrame, pulseStarted = -100;
    private bool dirty;
    private static readonly Color Stone = new Color32(73, 81, 94, 255);
    private static readonly Color StoneEdge = new Color32(136, 146, 162, 255);

    private Vector2 Tip(int i)
    {
        float angle = (90 + i * 60) * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }
    public Vector3 CrystalPosition(int index) => transform.TransformPoint(Tip(index));
    public Bounds SealBounds => visibilityProbe != null ? visibilityProbe.bounds : new Bounds(transform.position, Vector3.one * radius * 2.4f);
    private void OnEnable() { Build(); RefreshImmediately(); }
    private void OnValidate()
    {
        radius = Mathf.Max(.5f, radius);
        crystalBreathDuration = Mathf.Max(.5f, crystalBreathDuration);
        crystalGlowIntensity = Mathf.Clamp(crystalGlowIntensity, 0f, 3f);
        crystalHaloSize = Mathf.Max(.5f, crystalHaloSize);
        dirty = true;
    }
    private void OnDisable() { if (!Application.isPlaying) Release(); }
    private void OnDestroy() => Release();
    private void Update()
    {
        if (dirty) { dirty = false; Release(); }
        Build();
        float time = Application.isPlaying ? Time.time : Time.realtimeSinceStartup;
        if (time < nextFrame) return;
        nextFrame = time + 1f / 30;
        // Keep gameplay awake, but avoid rebuilding decorative meshes outside every camera.
        if (Application.isPlaying && visibilityProbe != null && !visibilityProbe.isVisible &&
            (circle == null || (circle.Absorptions.Count == 0 && !circle.IsRitualActive)) && time - pulseStarted > 1) return;
        DrawFrame(time);
    }
    public void RefreshImmediately()
    {
        Build(); circle = GetComponent<SummoningCircle>();
        DrawFrame(Application.isPlaying ? Time.time : Time.realtimeSinceStartup);
    }
    public void PlaySummonPulse() { pulseStarted = Time.time; RefreshImmediately(); }

    private void Build()
    {
        if (seal != null) return;
        circle = GetComponent<SummoningCircle>();
        solidMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        var shader = Resources.Load<Shader>("ElectricGateGlow");
        glowMaterial = new Material(shader != null ? shader : Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
        glowMaterial.SetFloat("_Radial", 1); glowMaterial.SetFloat("_Intensity", 1.15f); glowMaterial.SetFloat("_CoreFraction", .24f);
        // Separate local bloom material: changing crystal brightness must not
        // amplify the offering particles or the one-shot summon pulse.
        var bloomShader = Resources.Load<Shader>("WarmLightGlow");
        crystalBloomMaterial = new Material(bloomShader != null ? bloomShader : Shader.Find("Sprites/Default"))
            { hideFlags = HideFlags.HideAndDontSave };
        crystalBloomMaterial.SetFloat("_RadialFalloff", 1);
        crystalBloomMaterial.SetFloat("_Intensity", crystalGlowIntensity);
        seal = new CrystalMesh(transform, "Summoning Hexagram", solidMaterial, sortingOrder);
        visibilityProbe = seal.Root.GetComponent<Renderer>();
        glow = new CrystalMesh(transform, "Six Crystal Halos", glowMaterial, sortingOrder + 1);
        crystals = new CrystalMesh(transform, "Six Offering Crystals", solidMaterial, sortingOrder + 2);
        // One shared mesh for all six gems; normal world sorting/vision masking
        // still applies. No lights or full-screen bloom pass per crystal.
        crystalBloom = new CrystalMesh(transform, "Activated Crystal Bloom", crystalBloomMaterial, sortingOrder + 3);
        fragments = new CrystalMesh(transform, "Offering Fragments", solidMaterial, sortingOrder + 3);
        fragmentGlow = new CrystalMesh(transform, "Offering Fragment Glow", glowMaterial, sortingOrder + 3);
        ritualParticles = new CrystalMesh(transform, "Summoning Ritual Particles", solidMaterial, sortingOrder + 5);
        ritualGlow = new CrystalMesh(transform, "Summoning Ritual Glow", glowMaterial, sortingOrder + 4);

        Ring(seal, radius * 1.14f, .15f, Stone);
        Ring(seal, radius * 1.19f, .035f, StoneEdge);
        Ring(seal, radius * 1.13f, .045f, runeColor);
        Ring(seal, radius * .48f, .045f, runeColor * new Color(.75f, .75f, .75f, 1));
        for (int i = 0; i < 6; i++)
        {
            // Two overlapping triangles, not six radial spokes.
            Line(seal, Tip(i), Tip((i + 2) % 6), .065f, runeColor);
            Vector2 tip = Tip(i), radial = tip.normalized, tangent = new Vector2(-radial.y, radial.x);
            seal.Quad(tip, Vector2.one * .52f, new Color32(32, 37, 54, 255));
            seal.Quad(tip, Vector2.one * .43f, StoneEdge);
            seal.Quad(tip, Vector2.one * .32f, Stone);
            // Short, geometric inscriptions around the perimeter keep the existing prop vocabulary.
            Vector2 rune = Quaternion.Euler(0, 0, 30) * (tip * 1.14f);
            Line(seal, rune - tangent * .10f, rune + tangent * .10f, .045f, runeColor);
            Line(seal, rune, rune + radial * .14f, .04f, runeColor);
        }
        seal.Upload();
    }
    private static void Line(CrystalMesh mesh, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 half = (b - a) * .5f;
        Vector2 normal = new Vector2(-half.y, half.x).normalized * width * .5f;
        mesh.Quad((a + b) * .5f, half, normal, color);
    }
    private static void Ring(CrystalMesh mesh, float r, float width, Color color)
    {
        const int sides = 48;
        for (int i = 0; i < sides; i++)
        {
            float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
            Line(mesh, new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r,
                new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * r, width, color);
        }
    }
    private void DrawFrame(float time)
    {
        crystals.Clear(); glow.Clear(); crystalBloom.Clear(); fragments.Clear(); fragmentGlow.Clear();
        crystalBloomMaterial.SetFloat("_Intensity", crystalGlowIntensity);
        for (int i = 0; i < 6; i++)
        {
            Vector2 p = Tip(i);
            bool lit = circle != null && circle.IsCrystalLit(i);
            Color dark = lit ? new Color(.44f, .12f, .69f) : new Color(.22f, .17f, .29f);
            Color light = lit ? new Color(.82f, .53f, 1) : new Color(.43f, .34f, .52f);
            Color bright = lit ? new Color(.96f, .82f, 1) : new Color(.55f, .46f, .62f);
            float breath = .5f + .5f * Mathf.Sin(time * (Mathf.PI * 2 / Mathf.Max(.5f, crystalBreathDuration)) + i * .35f);
            if (lit)
            {
                // Keep the gem's facets legible at the bright end of the breath.
                dark = Color.Lerp(dark, new Color(.59f, .23f, .83f), breath);
                light = Color.Lerp(light, new Color(.9f, .67f, 1), breath);
            }
            Vector2 top = p + Vector2.up * .34f, bottom = p - Vector2.up * .27f;
            Vector2 left = p + new Vector2(-.19f, .07f), right = p + new Vector2(.19f, .07f);
            crystals.Triangle(bottom, left, top, light);
            crystals.Triangle(bottom, top, right, dark);
            crystals.Triangle(left, top, p + new Vector2(0, .09f), bright);
            if (lit)
            {
                float size = crystalHaloSize * (.92f + breath * .16f);
                float strength = .55f + breath * .45f;
                // Wide diffuse bloom escapes the silhouette. The smaller lobe
                // sits above the gem, so opaque facets cannot hide its light.
                glow.Quad(p, Vector2.one * size, new Color(.69f, .25f, 1, strength * .62f * crystalGlowIntensity));
                crystalBloom.Quad(p, Vector2.one * size, new Color(.63f, .2f, 1, strength * .18f));
                crystalBloom.Quad(p + Vector2.up * .035f, new Vector2(.7f, .95f), new Color(.8f, .48f, 1, strength * .28f));
                crystals.Quad(p, Vector2.one * .07f, new Color(.98f, .87f, 1));
            }
        }
        if (circle != null)
            foreach (var flight in circle.Absorptions) DrawFlight(flight);
        DrawRitual();
        float pulse = (time - pulseStarted) / 1.0f;
        if (pulse >= 0 && pulse < 1)
        {
            float alpha = 1 - pulse;
            glow.Quad(Vector2.zero, Vector2.one * (1 + pulse * radius * 2), new Color(.77f, .4f, 1, alpha * .65f));
            Ring(crystals, Mathf.Max(.01f, radius * pulse), .05f * alpha, new Color(.88f, .65f, 1, alpha));
        }
        crystals.Upload(); glow.Upload(); crystalBloom.Upload(); fragments.Upload(); fragmentGlow.Upload();
    }
    private static float Random01(int seed, int i, uint salt)
    {
        uint hash = unchecked((uint)seed * 747796405u + (uint)i * 2891336453u + salt);
        hash = (hash ^ (hash >> 16)) * 2246822519u;
        hash ^= hash >> 13;
        return (hash & 0xffffff) / 16777215f;
    }
    private void DrawFlight(SummoningCircle.Absorption flight)
    {
        Vector3 target = CrystalPosition(flight.crystal);
        float progress = Mathf.Clamp01(flight.elapsed / Mathf.Max(.01f, flight.duration));
        Vector3 scale = transform.lossyScale;
        Vector2 sizeScale = new Vector2(1 / Mathf.Max(.01f, Mathf.Abs(scale.x)), 1 / Mathf.Max(.01f, Mathf.Abs(scale.y)));
        for (int i = 0; i < fragmentsPerCharacter; i++)
        {
            float a = Random01(flight.seed, i, 17), b = Random01(flight.seed, i, 317), c = Random01(flight.seed, i, 1171);
            float t = Mathf.Clamp01(progress * 1.22f - c * .22f);
            if (t >= 1) continue;
            Vector3 offset = new Vector3((a - .5f) * flight.bodySize.x, (b - .5f) * flight.bodySize.y);
            Vector3 start = flight.origin + offset;
            Vector3 burst = start + offset.normalized * (.35f + c * .45f);
            Vector3 position;
            if (t < .18f) position = Vector3.Lerp(start, burst, Mathf.Sin(t / .18f * Mathf.PI * .5f));
            else
            {
                float u = (t - .18f) / .82f; u *= u;
                Vector3 axis = target - burst;
                Vector3 normal = new Vector3(-axis.y, axis.x).normalized;
                Vector3 control = (target + burst) * .5f + normal * ((a - .5f) * 2 + .6f);
                position = (1 - u) * (1 - u) * burst + 2 * (1 - u) * u * control + u * u * target;
            }
            float disappear = 1 - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.91f, 1, t));
            float size = (.055f + c * .065f) * disappear;
            Vector2 local = transform.InverseTransformPoint(position);
            Color color = Color.Lerp(new Color(.55f, .14f, .85f), new Color(.89f, .66f, 1), b);
            color.a = disappear;
            fragments.Quad(local, sizeScale * size, color);
            color.a *= .36f;
            fragmentGlow.Quad(local, sizeScale * size * 3.5f, color);
        }
    }
    private void Release()
    {
        seal?.Dispose(); crystals?.Dispose(); glow?.Dispose(); crystalBloom?.Dispose(); fragments?.Dispose(); fragmentGlow?.Dispose();
        ritualParticles?.Dispose(); ritualGlow?.Dispose(); ritualParticles = ritualGlow = null;
        seal = crystals = glow = crystalBloom = fragments = fragmentGlow = null;
        CrystalMesh.Release(solidMaterial); CrystalMesh.Release(glowMaterial); CrystalMesh.Release(crystalBloomMaterial);
        solidMaterial = glowMaterial = crystalBloomMaterial = null;
    }
}
