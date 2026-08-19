using UnityEngine;

/// <summary>
/// Configurable, collision-free 2D particle emitter that continuously sends
/// particles radially outward and fades them before removal.
/// </summary>
[DisallowMultipleComponent]
public sealed class RadialFadingParticleEmitter : MonoBehaviour
{
    [Header("Emission")]
    [SerializeField, Min(0f)] private float particlesPerSecond = 18f;
    [SerializeField, Min(0.01f)] private float particleLifetime = 1.4f;
    [SerializeField, Range(0f, 0.95f)] private float lifetimeRandomness = 0.2f;
    [SerializeField, Min(0f)] private float emissionRadius = 0.16f;
    [SerializeField, Range(0f, 1f)] private float directionRandomness = 0.12f;
    [SerializeField, Min(1)] private int maximumParticles = 256;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float outwardSpeed = 0.65f;
    [SerializeField, Range(0f, 0.95f)] private float speedRandomness = 0.25f;
    [SerializeField] private float gravityModifier;
    [SerializeField] private ParticleSystemSimulationSpace simulationSpace =
        ParticleSystemSimulationSpace.World;

    [Header("Appearance")]
    [SerializeField] private Color particleColor =
        new Color32(76, 107, 197, 255);
    [SerializeField, Min(0.001f)] private float particleSize = 0.1f;
    [SerializeField, Range(0f, 0.95f)] private float sizeRandomness = 0.3f;
    [SerializeField, Range(0f, 0.5f)] private float fadeInFraction = 0.06f;
    [SerializeField, Range(0.5f, 1f)] private float fadeOutStart = 0.72f;
    [SerializeField] private bool randomizeRotation = true;
    [SerializeField] private int sortingOrder = 2;

    private ParticleSystem particleSystemComponent;
    private Material runtimeMaterial;

    public void Configure(
        Color color,
        float emissionRate,
        float lifetime,
        float radius,
        float speed,
        float size,
        int configuredSortingOrder)
    {
        particleColor = color;
        particlesPerSecond = Mathf.Max(0f, emissionRate);
        particleLifetime = Mathf.Max(0.01f, lifetime);
        emissionRadius = Mathf.Max(0f, radius);
        outwardSpeed = Mathf.Max(0f, speed);
        particleSize = Mathf.Max(0.001f, size);
        sortingOrder = configuredSortingOrder;
        ApplyConfiguration();
    }

    private void Awake()
    {
        EnsureParticleSystem();
        ApplyConfiguration();
    }

    private void OnEnable()
    {
        if (!Application.isPlaying)
        {
            return;
        }

        EnsureParticleSystem();
        ApplyConfiguration();
        if (!particleSystemComponent.isPlaying)
        {
            particleSystemComponent.Play();
        }
    }

    private void OnDisable()
    {
        if (particleSystemComponent != null && particleSystemComponent.isPlaying)
        {
            particleSystemComponent.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void OnDestroy()
    {
        if (runtimeMaterial != null)
        {
            Destroy(runtimeMaterial);
        }
    }

    public void ApplyConfiguration()
    {
        EnsureParticleSystem();
        if (particleSystemComponent == null)
        {
            return;
        }

        bool shouldResume = Application.isPlaying &&
                            gameObject.activeInHierarchy &&
                            enabled;
        if (particleSystemComponent.isPlaying ||
            particleSystemComponent.isPaused)
        {
            // Duration and several main-module values cannot be changed while
            // a system is alive. Clearing first also makes Play Mode Inspector
            // edits safe.
            particleSystemComponent.Stop(
                true,
                ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        float safeLifetime = Mathf.Max(0.01f, particleLifetime);
        float safeSpeed = Mathf.Max(0f, outwardSpeed);
        float safeSize = Mathf.Max(0.001f, particleSize);

        ParticleSystem.MainModule main = particleSystemComponent.main;
        main.loop = true;
        main.playOnAwake = true;
        main.duration = Mathf.Max(1f, safeLifetime);
        main.simulationSpace = simulationSpace;
        main.maxParticles = Mathf.Max(1, maximumParticles);
        main.gravityModifier = gravityModifier;
        main.startLifetime = CreateRandomizedCurve(
            safeLifetime,
            lifetimeRandomness);
        main.startSpeed = CreateRandomizedCurve(safeSpeed, speedRandomness);
        main.startSize = CreateRandomizedCurve(safeSize, sizeRandomness);
        main.startRotation = randomizeRotation
            ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f)
            : new ParticleSystem.MinMaxCurve(0f);
        main.startColor = Color.white;
        main.stopAction = ParticleSystemStopAction.None;

        ParticleSystem.EmissionModule emission = particleSystemComponent.emission;
        emission.enabled = particlesPerSecond > 0f;
        emission.rateOverTime = Mathf.Max(0f, particlesPerSecond);

        ParticleSystem.ShapeModule shape = particleSystemComponent.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0f, emissionRadius);
        shape.radiusThickness = 1f;
        shape.arc = 360f;
        shape.randomDirectionAmount = directionRandomness;
        shape.alignToDirection = false;

        ParticleSystem.CollisionModule collision =
            particleSystemComponent.collision;
        collision.enabled = false;
        ParticleSystem.TriggerModule trigger = particleSystemComponent.trigger;
        trigger.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
            particleSystemComponent.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(particleColor, 0f),
                new GradientColorKey(particleColor, 1f)
            },
            new[]
            {
                new GradientAlphaKey(fadeInFraction > 0f ? 0f : particleColor.a, 0f),
                new GradientAlphaKey(particleColor.a, Mathf.Max(0.001f, fadeInFraction)),
                new GradientAlphaKey(particleColor.a, Mathf.Max(fadeInFraction, fadeOutStart)),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fadeGradient;

        ParticleSystemRenderer particleRenderer =
            particleSystemComponent.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.sortingOrder = sortingOrder;
        EnsureMaterial(particleRenderer);
        if (shouldResume)
        {
            particleSystemComponent.Play();
        }
    }

    private void EnsureParticleSystem()
    {
        if (particleSystemComponent == null)
        {
            particleSystemComponent = GetComponent<ParticleSystem>();
        }
        if (particleSystemComponent == null)
        {
            particleSystemComponent = gameObject.AddComponent<ParticleSystem>();
        }
    }

    private void EnsureMaterial(ParticleSystemRenderer particleRenderer)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            Debug.LogWarning(
                "RadialFadingParticleEmitter could not find the project's working Sprites/Default shader.",
                this);
            return;
        }

        if (runtimeMaterial == null || runtimeMaterial.shader != shader)
        {
            if (runtimeMaterial != null)
            {
                Destroy(runtimeMaterial);
            }
            runtimeMaterial = new Material(shader)
            {
                name = "Radial Fading Particle Runtime Material",
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        // Sprites/Default is already used by the project's Ghost and growth
        // particle effects and correctly multiplies the particle vertex color.
        runtimeMaterial.color = Color.white;
        particleRenderer.sharedMaterial = runtimeMaterial;
    }

    private static ParticleSystem.MinMaxCurve CreateRandomizedCurve(
        float value,
        float randomness)
    {
        float variation = Mathf.Clamp01(randomness);
        return new ParticleSystem.MinMaxCurve(
            Mathf.Max(0f, value * (1f - variation)),
            Mathf.Max(0f, value * (1f + variation)));
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        particlesPerSecond = Mathf.Max(0f, particlesPerSecond);
        particleLifetime = Mathf.Max(0.01f, particleLifetime);
        emissionRadius = Mathf.Max(0f, emissionRadius);
        maximumParticles = Mathf.Max(1, maximumParticles);
        outwardSpeed = Mathf.Max(0f, outwardSpeed);
        particleSize = Mathf.Max(0.001f, particleSize);
        fadeOutStart = Mathf.Max(fadeInFraction, fadeOutStart);
        if (Application.isPlaying)
        {
            ApplyConfiguration();
        }
    }
#endif
}
