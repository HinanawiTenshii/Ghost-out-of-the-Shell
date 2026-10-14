using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>One short-lived mesh releases the existing helix beads in world space.</summary>
public sealed class EnergyCrystalLinkReleaseVisual : MonoBehaviour
{
    private struct Bead
    {
        public Vector2 center, halfRight, halfUp, velocity;
        public Color color;
        public float lifetime, spin;
    }

    private Bead[] beads;
    private CrystalMesh particles;
    private Material material;
    private float elapsed, duration;

    public static void Spawn(MeshFilter source, float duration, float distance)
    {
        // An offloaded/unrendered link must not leave a phantom trail when its crystal breaks.
        if (source == null || !source.gameObject.activeInHierarchy || source.sharedMesh == null
            || source.sharedMesh.vertexCount == 0) return;
        var renderer = source.GetComponent<MeshRenderer>();
        if (renderer == null || !renderer.enabled || renderer.sharedMaterial == null) return;
        var vertices = source.sharedMesh.vertices;
        var colors = source.sharedMesh.colors;
        if (vertices.Length % 4 != 0 || colors.Length != vertices.Length) return;

        var root = new GameObject("Energy Crystal Released Links") { layer = source.gameObject.layer };
        SceneManager.MoveGameObjectToScene(root, source.gameObject.scene);
        // Translation only. Moving/destroying/scaling either former endpoint cannot drag the beads.
        root.transform.position = source.transform.position;
        root.AddComponent<CameraVisionStreamingExempt>();
        var effect = root.AddComponent<EnergyCrystalLinkReleaseVisual>();
        effect.duration = Mathf.Max(.2f, duration);
        effect.material = new Material(renderer.sharedMaterial) { hideFlags = HideFlags.HideAndDontSave };
        effect.particles = new CrystalMesh(root.transform, "Released Green Link Beads", effect.material, renderer.sortingOrder);
        effect.particles.Root.GetComponent<MeshRenderer>().sortingLayerID = renderer.sortingLayerID;
        effect.beads = new Bead[vertices.Length / 4];
        for (int i = 0; i < effect.beads.Length; i++)
        {
            int v = i * 4;
            Vector2 center = source.transform.TransformPoint((vertices[v] + vertices[v + 2]) * .5f) - root.transform.position;
            Vector2 right = source.transform.TransformVector((vertices[v + 3] - vertices[v]) * .5f);
            Vector2 up = source.transform.TransformVector((vertices[v + 1] - vertices[v]) * .5f);
            // Local deterministic noise avoids touching gameplay's global Unity Random state.
            float angle = Noise(i * 5 + 1) * Mathf.PI * 2f;
            float lifetime = effect.duration * Mathf.Lerp(.65f, 1f, Noise(i * 5 + 2));
            float travel = Mathf.Max(.1f, distance) * Mathf.Lerp(.4f, 1f, Noise(i * 5 + 3));
            float dampingTime = lifetime * .55f;
            float speed = travel / (dampingTime * (1f - Mathf.Exp(-lifetime / dampingTime)));
            effect.beads[i] = new Bead {
                center = center, halfRight = right, halfUp = up, color = colors[v], lifetime = lifetime,
                velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed,
                spin = Mathf.Lerp(-2.2f, 2.2f, Noise(i * 5 + 4))
            };
        }
        effect.WriteFrame(0); // Exact original positions/size/color on the handover frame.
    }

    private static float Noise(int seed)
    {
        unchecked
        {
            uint n = (uint)seed + 0x9e3779b9u;
            n ^= n >> 16; n *= 0x7feb352du;
            n ^= n >> 15; n *= 0x846ca68bu;
            n ^= n >> 16;
            return (n & 0xffffffu) / 16777215f;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        if (elapsed >= duration) { Destroy(gameObject); return; }
        WriteFrame(elapsed);
    }

    private void WriteFrame(float time)
    {
        if (particles == null) return;
        particles.Clear();
        foreach (var bead in beads)
        {
            float progress = Mathf.Clamp01(time / bead.lifetime);
            if (progress >= 1) continue;
            float dampingTime = bead.lifetime * .55f;
            Vector2 center = bead.center + bead.velocity * (dampingTime * (1f - Mathf.Exp(-time / dampingTime)));
            float shrink = 1f - .75f * Mathf.SmoothStep(0, 1, progress);
            float angle = bead.spin * time, sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
            Vector2 right = Rotate(bead.halfRight, sin, cos) * shrink;
            Vector2 up = Rotate(bead.halfUp, sin, cos) * shrink;
            Color color = bead.color;
            color.a *= 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.08f, 1, progress));
            particles.Quad(center, right, up, color);
        }
        particles.Upload();
    }

    private static Vector2 Rotate(Vector2 vector, float sin, float cos)
        => new Vector2(vector.x * cos - vector.y * sin, vector.x * sin + vector.y * cos);

    private void OnDestroy()
    {
        particles?.Dispose();
        CrystalMesh.Release(material);
    }
}
