using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Small pale glass/gold splinters and a sound pulse, not a bomb explosion.</summary>
public sealed class GlassBottleShatterVisual : MonoBehaviour
{
    public const float Duration = .65f;
    private Mesh mesh;
    private Material material;
    private readonly Vector3[] vertices = new Vector3[24];
    private readonly Color32[] colors = new Color32[24];
    private float elapsed;
    public static void Spawn(Scene scene, Vector3 position, float radius)
    {
        var root = new GameObject("Glass Bottle Shards"); SceneManager.MoveGameObjectToScene(root, scene); root.transform.position = position;
        root.AddComponent<CameraVisionStreamingExempt>();
        var effect = root.AddComponent<GlassBottleShatterVisual>(); effect.Build();
        if (radius > 0)
        {
            var pulseRoot = new GameObject("Bottle Investigation Pulse"); SceneManager.MoveGameObjectToScene(pulseRoot, scene);
            pulseRoot.transform.position = position; pulseRoot.AddComponent<CameraVisionStreamingExempt>();
            pulseRoot.AddComponent<ZeldaInvestigationPulse>().Configure(radius, .55f, new Color(.66f,.88f,.93f,.5f), 0, 3);
        }
        AudioClip clip = Resources.Load<AudioClip>("Audio/GlassBreak");
        if (clip != null)
        {
            var soundRoot = new GameObject("Glass Bottle Break Sound"); SceneManager.MoveGameObjectToScene(soundRoot, scene);
            soundRoot.transform.position = position; soundRoot.AddComponent<CameraVisionStreamingExempt>();
            var audio = soundRoot.AddComponent<AudioSource>(); audio.playOnAwake = false; audio.loop = false;
            audio.clip = clip; audio.volume = .6f; audio.pitch = 1.12f; audio.spatialBlend = 1; audio.dopplerLevel = 0;
            audio.rolloffMode = AudioRolloffMode.Linear; audio.minDistance = 1; audio.maxDistance = Mathf.Max(6, radius * 1.5f);
            audio.Play(); Destroy(soundRoot, clip.length / audio.pitch + .1f);
        }
    }
    private void Build()
    {
        mesh = new Mesh { name = "Eight Glass Splinters" }; mesh.MarkDynamic(); mesh.vertices = vertices;
        var indices = new int[24]; for (int i = 0; i < 24; i++) indices[i] = i;
        mesh.triangles = indices; mesh.uv = new Vector2[24];
        material = new Material(Shader.Find("Sprites/Default"));
        gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = gameObject.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.sortingOrder = 3;
        WriteFrame(0);
    }
    private void Update()
    {
        elapsed += Time.deltaTime; WriteFrame(elapsed);
        if (elapsed >= Duration) Destroy(gameObject);
    }
    private void WriteFrame(float time)
    {
        float progress = Mathf.Clamp01(time / Duration);
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI * 2 / 8 + .13f;
            Vector2 drift = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (.05f + .46f * (1 - Mathf.Pow(1 - progress, 2)));
            float rotation = a + time * (i % 2 == 0 ? 7 : -8);
            Color tint = i % 3 == 0 ? new Color(.93f,.69f,.25f,1-progress) : new Color(.73f,.93f,1,1-progress);
            for (int j = 0; j < 3; j++)
            {
                float corner = rotation - j * Mathf.PI * 2 / 3;
                vertices[i * 3 + j] = drift + new Vector2(Mathf.Cos(corner) * .025f, Mathf.Sin(corner) * .065f) * (1 - progress * .45f);
                colors[i * 3 + j] = tint;
            }
        }
        mesh.vertices = vertices; mesh.colors32 = colors; mesh.RecalculateBounds();
    }
    private void OnDestroy() { if (mesh != null) Destroy(mesh); if (material != null) Destroy(material); }
}
