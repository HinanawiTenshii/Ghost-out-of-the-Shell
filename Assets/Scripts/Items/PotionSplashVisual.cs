using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One mesh: tinted liquid sheets, droplets, wet marks and tiny bottle shards.</summary>
public sealed class PotionSplashVisual : MonoBehaviour
{
    public const float Duration = 1.05f;
    private const int Sectors = 48, Drops = 36, Shards = 10;
    private const int VertexCount = Sectors * 9 + Drops * 24 + Shards * 3;
    private static AudioClip shatterSound;
    private readonly Vector3[] vertices = new Vector3[VertexCount];
    private readonly Color32[] colors = new Color32[VertexCount];
    private Mesh mesh;
    private Material material;
    private AudioSource sound;
    private Color liquid;
    private float radius, elapsed;
    private int cursor;
    private bool soundPaused;

    public static void PrewarmAudio()
    {
        if (shatterSound == null) shatterSound = Resources.Load<AudioClip>("Audio/PotionShatter");
        if (shatterSound != null && shatterSound.loadState == AudioDataLoadState.Unloaded)
            shatterSound.LoadAudioData();
    }

    public static PotionSplashVisual Spawn(Scene scene, Vector3 position, float radius, Color tint)
    {
        var root = new GameObject("Potion Liquid Splash");
        SceneManager.MoveGameObjectToScene(root, scene);
        root.transform.position = position;
        root.AddComponent<CameraVisionStreamingRegion>().Configure(radius);
        var effect = root.AddComponent<PotionSplashVisual>();
        effect.radius = radius;
        effect.liquid = tint;
        effect.Build();
        PrewarmAudio();
        if (shatterSound != null)
        {
            effect.sound = root.AddComponent<AudioSource>();
            var audio = effect.sound;
            audio.playOnAwake = false; audio.loop = false; audio.clip = shatterSound;
            audio.volume = .65f; audio.pitch = 1f; audio.spatialBlend = 1f; audio.dopplerLevel = 0f;
            audio.rolloffMode = AudioRolloffMode.Linear; audio.minDistance = 2f; audio.maxDistance = 16f;
            audio.Play();
        }
        return effect;
    }

    private void Build()
    {
        mesh = new Mesh { name = "Potion Splash Mesh" }; mesh.MarkDynamic();
        mesh.vertices = vertices;
        var indices = new int[VertexCount]; for (int i = 0; i < indices.Length; i++) indices[i] = i;
        mesh.triangles = indices;
        material = new Material(Shader.Find("Sprites/Default"));
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material; renderer.sortingOrder = 2;
        WriteFrame(0f);
    }

    private void Update()
    {
        bool pause = Time.timeScale <= 0f || CharacterPotionBuffs.IsRestoringWorld;
        if (sound != null)
        {
            if (pause && !soundPaused) sound.Pause();
            else if (!pause && soundPaused) sound.UnPause();
            soundPaused = pause;
            if (!pause) sound.pitch = Mathf.Max(.01f, Time.timeScale);
        }
        if (pause) return;
        elapsed += Time.deltaTime;
        WriteFrame(elapsed);
        if (elapsed >= Duration) Destroy(gameObject);
    }

    private static Vector2 Radial(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a));
    private void Triangle(Vector2 a, Vector2 b, Vector2 c, Color tint)
    {
        vertices[cursor] = a; vertices[cursor + 1] = b; vertices[cursor + 2] = c;
        colors[cursor] = colors[cursor + 1] = colors[cursor + 2] = tint; cursor += 3;
    }
    private void Quad(Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
    { Triangle(a, b, c, tint); Triangle(a, c, d, tint); }
    private float Edge(float angle) => .66f + .12f * Mathf.Sin(angle * 7f + .2f) + .09f * Mathf.Cos(angle * 11f);

    private void WriteFrame(float time)
    {
        cursor = 0;
        float p = Mathf.Clamp01(time / Duration);
        float spread = 1f - Mathf.Pow(1f - Mathf.Clamp01(time / .24f), 3f);
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.3f, 1f, p));
        var pool = liquid; pool.a = .17f * fade;
        var rim = Color.Lerp(liquid, Color.white, .22f); rim.a = .44f * fade;
        // Irregular transparent wet patch, not an opaque fireball. A broken fine rim
        // briefly communicates the precise affected circle without hiding characters.
        for (int i = 0; i < Sectors; i++)
        {
            float a = i * Mathf.PI * 2f / Sectors, b = (i + 1) * Mathf.PI * 2f / Sectors;
            Triangle(Vector2.zero, Radial(a) * (radius * spread * Edge(a)),
                Radial(b) * (radius * spread * Edge(b)), pool);
            float end = Mathf.Lerp(a, b, .58f);
            float outer = radius * spread, inner = Mathf.Max(0f, outer - .035f);
            Quad(Radial(a) * inner, Radial(a) * outer, Radial(end) * outer, Radial(end) * inner, rim);
        }
        for (int i = 0; i < Drops; i++)
        {
            float angle = i * 2.39996323f;
            Vector2 dir = Radial(angle), across = new Vector2(-dir.y, dir.x);
            float reach = radius * (.4f + .6f * ((i * 17 % 37) / 36f));
            float flight = Mathf.Clamp01(time / (.22f + i % 4 * .025f));
            float eased = 1f - (1f - flight) * (1f - flight);
            float halfWidth = (.055f + (i % 4) * .018f) * (1f - .32f * p);
            float halfLength = halfWidth * (1.3f + 2.4f * Mathf.Sin(flight * Mathf.PI));
            Vector2 center = dir * Mathf.Max(0f, reach * eased - halfLength);
            Color tint = Color.Lerp(liquid, Color.white, i % 5 == 0 ? .55f : .1f);
            tint.a = (.85f - .25f * flight) * fade;
            for (int corner = 0; corner < 8; corner++)
            {
                float a = corner * Mathf.PI / 4f, b = (corner + 1) * Mathf.PI / 4f;
                Triangle(center, center + dir * (Mathf.Cos(a) * halfLength) + across * (Mathf.Sin(a) * halfWidth),
                    center + dir * (Mathf.Cos(b) * halfLength) + across * (Mathf.Sin(b) * halfWidth), tint);
            }
        }
        for (int i = 0; i < Shards; i++)
        {
            float angle = i * Mathf.PI * 2 / Shards + .12f;
            Vector2 center = Radial(angle) * (.08f + .65f * spread);
            float spin = angle + time * (i % 2 == 0 ? 8f : -7f);
            var tint = Color.Lerp(liquid, Color.white, .72f); tint.a = Mathf.Max(0f, 1f - p * 1.7f);
            Triangle(center + Radial(spin) * .095f, center + Radial(spin + 2.5f) * .055f,
                center + Radial(spin + 4f) * .075f, tint);
        }
        mesh.vertices = vertices; mesh.colors32 = colors; mesh.RecalculateBounds();
    }
    private void OnDestroy()
    {
        if (mesh != null) Destroy(mesh);
        if (material != null) Destroy(material);
    }
}
