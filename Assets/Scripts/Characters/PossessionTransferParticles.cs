using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene-owned visual only: a loose stream of ghost-coloured motes between bodies.</summary>
public sealed class PossessionTransferParticles : MonoBehaviour
{
    private const int Capacity = 48;
    private const float EmissionInterval = 1f / 36f;
    private const float CancelFadeDuration = 0.16f;
    private struct Mote
    {
        public Vector3 origin;
        public float age, duration, bend, size, phase;
    }

    private readonly Mote[] motes = new Mote[Capacity];
    private readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[Capacity];
    private ZeldaFourWayMover source, target;
    private ParticleSystem particles;
    private Material particleMaterial;
    private Color tint;
    private int count;
    private float emissionTimer, cancelAge;
    private bool emitting = true, cancelled;
    private Vector3 lastDestination;

    public static PossessionTransferParticles Create(ZeldaFourWayMover source, ZeldaFourWayMover target, Color tint)
    {
        var root = new GameObject("Possession Consciousness Particles");
        root.SetActive(false);
        root.layer = source.gameObject.layer;
        SceneManager.MoveGameObjectToScene(root, ZeldaRuntimeRegistry.GetGameplayScene(source.gameObject));
        root.AddComponent<CameraVisionStreamingExempt>();
        var effect = root.AddComponent<PossessionTransferParticles>();
        effect.source = source;
        effect.target = target;
        effect.tint = tint;
        effect.lastDestination = target.PossessionVisualCenter;
        effect.Initialize();
        root.SetActive(true);
        effect.particles.Play();
        effect.EmitMote();
        effect.EmitMote();
        return effect;
    }

    private void Initialize()
    {
        particles = gameObject.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.maxParticles = Capacity;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        // Positions/lifetimes are owned by this script; no second integration step.
        main.simulationSpeed = 0f;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        var bodyRenderer = source.GetComponent<SpriteRenderer>();
        if (bodyRenderer != null)
        {
            renderer.sortingLayerID = bodyRenderer.sortingLayerID;
            renderer.sortingOrder = bodyRenderer.sortingOrder + 2;
        }
        Shader shader = Shader.Find("Sprites/Default");
        if (shader != null)
        {
            particleMaterial = new Material(shader) { name = "Possession Motes" };
            renderer.sharedMaterial = particleMaterial;
        }
    }

    public void StopEmission(bool completed)
    {
        emitting = false;
        if (!completed) cancelled = true;
        // Completed streams finish their short flight even if the old ghost is destroyed.
    }

    private void EmitMote()
    {
        if (count >= Capacity || source == null) return;
        Vector3 origin = source.PossessionVisualCenter + (Vector3)(Random.insideUnitCircle * 0.12f);
        motes[count++] = new Mote
        {
            origin = origin,
            duration = Mathf.Clamp(Vector3.Distance(origin, lastDestination) / Random.Range(3.2f, 5f), 0.2f, 0.65f),
            bend = Random.Range(-0.32f, 0.32f),
            size = Random.Range(0.035f, 0.075f),
            phase = Random.Range(0f, Mathf.PI * 2f)
        };
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        if (target == null || ZeldaRuntimeRegistry.GetGameplayScene(target.gameObject) != gameObject.scene ||
            target.CharacterData == null || target.CharacterData.IsDead)
            StopEmission(false);
        else lastDestination = target.PossessionVisualCenter;
        if (emitting && (source == null || !source.isActiveAndEnabled ||
            ZeldaRuntimeRegistry.GetGameplayScene(source.gameObject) != gameObject.scene || !source.IsPossessionInProgress))
            StopEmission(false);

        if (emitting)
        {
            // Cap catch-up emission after a long frame; no burst or unbounded work.
            emissionTimer += Mathf.Min(dt, 0.1f);
            while (emissionTimer >= EmissionInterval)
            {
                emissionTimer -= EmissionInterval;
                EmitMote();
            }
        }
        if (cancelled) cancelAge += dt;
        float cancelAlpha = cancelled ? Mathf.Clamp01(1f - cancelAge / CancelFadeDuration) : 1f;
        int active = 0;
        for (int i = 0; i < count; i++)
        {
            Mote mote = motes[i];
            mote.age += dt;
            if (mote.age >= mote.duration || cancelAlpha <= 0f) continue;
            float t = mote.age / mote.duration;
            Vector3 direction = lastDestination - mote.origin;
            Vector3 normal = new Vector3(-direction.y, direction.x, 0f).normalized;
            float arc = Mathf.Sin(t * Mathf.PI);
            Vector3 position = Vector3.Lerp(mote.origin, lastDestination, t)
                + normal * (mote.bend + Mathf.Sin(t * 7f + mote.phase) * 0.045f) * arc;
            Color color = tint;
            color.a *= Mathf.Clamp01(t * 10f) * Mathf.Clamp01((1f - t) * 6f) * cancelAlpha;
            buffer[active] = new ParticleSystem.Particle
            {
                position = position,
                startColor = color,
                startSize = mote.size * Mathf.Lerp(1f, 0.5f, t),
                startLifetime = 1f,
                remainingLifetime = 1f,
                randomSeed = (uint)(active + 1)
            };
            motes[active++] = mote;
        }
        count = active;
        particles.SetParticles(buffer, count);
        if (!emitting && count == 0) Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (particleMaterial != null) Destroy(particleMaterial);
    }
}
