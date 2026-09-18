using UnityEngine;

/// <summary>Visual-only scatter, homing and arrival effect. Never awards currency.</summary>
public sealed class GrowthCollectiblePickupEffect : MonoBehaviour
{
    private const float MaximumDuration = 12f;
    private ParticleSystem particles;
    private ParticleSystem flashes;
    private ParticleSystem.Particle[] buffer;
    private Vector3[] scatterOffsets;
    private Vector3[] scatterStarts;
    private Vector3[] scatterControls;
    private float[] scatterEasePowers;
    private Vector3[] flightVelocities;
    private float[] speedMultipliers;
    private Material flashMaterial;
    private Vector3 origin;
    private float scatterDuration, homingSpeed, trailDuration, flashDuration;
    private float elapsed, cleanupRemaining = -1f;
    private bool begun;
    private ZeldaFourWayMover cachedTarget;
    private ZeldaCharacterData targetData;
    private SpriteRenderer targetVisual;

    public void Initialize(int count, float scatterSeconds, float scatterSpeed, float followSpeed,
        float trailSeconds, float flashSeconds, int sortingOrder,
        Material dotMaterial, Material trailMaterial, Texture glowTexture)
    {
        count = Mathf.Clamp(count, 8, 64);
        scatterDuration = Mathf.Max(0.1f, scatterSeconds);
        homingSpeed = Mathf.Max(0.1f, followSpeed);
        trailDuration = Mathf.Clamp(trailSeconds, 0.005f, 0.6f);
        flashDuration = Mathf.Clamp(flashSeconds, 0.05f, 0.5f);
        origin = transform.position;
        buffer = new ParticleSystem.Particle[count];
        scatterOffsets = new Vector3[count];
        scatterStarts = new Vector3[count];
        scatterControls = new Vector3[count];
        scatterEasePowers = new float[count];
        flightVelocities = new Vector3[count];
        speedMultipliers = new float[count];
        var random = new System.Random(GetInstanceID());

        particles = gameObject.AddComponent<ParticleSystem>();
        ConfigureSystem(particles, count);
        var trails = particles.trails;
        trails.enabled = true;
        trails.ratio = 1f;
        trails.lifetime = trailDuration;
        trails.minVertexDistance = 0.025f;
        trails.worldSpace = true;
        // The arrival flash replaces the dot; do not leave a detached line behind it.
        trails.dieWithParticles = true;
        trails.inheritParticleColor = true;
        trails.sizeAffectsWidth = true;
        trails.sizeAffectsLifetime = false;
        trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.32f), new Keyframe(0.35f, 0.10f), new Keyframe(1f, 0f)));
        var trailFade = new Gradient();
        trailFade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0.2f, 0.3f),
                new GradientAlphaKey(0f, 0.7f), new GradientAlphaKey(0f, 1f) });
        trails.colorOverLifetime = trailFade;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = dotMaterial;
        renderer.trailMaterial = trailMaterial;
        renderer.sortingOrder = sortingOrder;

        for (int i = 0; i < count; i++)
        {
            // Independent angles deliberately allow clusters and gaps rather than
            // assigning each particle an evenly spaced sector of the circle.
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float radius = Mathf.Max(0.1f, scatterSpeed) * scatterDuration *
                Mathf.Lerp(0.65f, 1.9f, (float)random.NextDouble());
            scatterOffsets[i] = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius;
            float startAngle = (float)random.NextDouble() * Mathf.PI * 2f;
            float startRadius = Mathf.Min(0.12f, radius * 0.15f) * (float)random.NextDouble();
            scatterStarts[i] = new Vector3(Mathf.Cos(startAngle), Mathf.Sin(startAngle), 0f) * startRadius;
            float bendAngle = angle + Mathf.Lerp(-1.2f, 1.2f, (float)random.NextDouble());
            float controlRadius = radius * Mathf.Lerp(0.25f, 0.65f, (float)random.NextDouble());
            scatterControls[i] = new Vector3(Mathf.Cos(bendAngle), Mathf.Sin(bendAngle), 0f) * controlRadius;
            scatterEasePowers[i] = Mathf.Lerp(1.6f, 3.4f, (float)random.NextDouble());
            speedMultipliers[i] = Mathf.Lerp(0.8f, 1.2f, (float)random.NextDouble());
            buffer[i] = new ParticleSystem.Particle
            {
                position = origin + scatterStarts[i],
                velocity = Vector3.zero,
                startColor = Color.white,
                startSize = Mathf.Lerp(0.08f, 0.14f, (float)random.NextDouble()),
                startLifetime = MaximumDuration + 1f,
                remainingLifetime = MaximumDuration + 1f,
                randomSeed = (uint)(i + 1)
            };
        }

        var flashObject = new GameObject("Arrival Flashes");
        flashObject.transform.SetParent(transform, false);
        flashes = flashObject.AddComponent<ParticleSystem>();
        ConfigureSystem(flashes, count);
        flashMaterial = new Material(dotMaterial)
        {
            name = "Growth Pickup Arrival Glow",
            hideFlags = HideFlags.HideAndDontSave,
            mainTexture = glowTexture
        };
        var flashRenderer = flashes.GetComponent<ParticleSystemRenderer>();
        flashRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        flashRenderer.sharedMaterial = flashMaterial;
        flashRenderer.sortingOrder = sortingOrder + 1;
        var flashFade = flashes.colorOverLifetime;
        flashFade.enabled = true;
        var fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        flashFade.color = fade;
        var flashSize = flashes.sizeOverLifetime;
        flashSize.enabled = true;
        flashSize.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.35f), new Keyframe(0.2f, 1f), new Keyframe(1f, 0.15f)));
    }

    private static void ConfigureSystem(ParticleSystem system, int maximum)
    {
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = system.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = MaximumDuration + 2f;
        main.startSpeed = 0f;
        main.startLifetime = MaximumDuration + 1f;
        main.startColor = Color.white;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
        main.gravityModifier = 0f;
        main.maxParticles = maximum;
        main.stopAction = ParticleSystemStopAction.None;
        main.useUnscaledTime = false;
        var emission = system.emission;
        emission.enabled = false;
        var shape = system.shape;
        shape.enabled = false;
    }

    public void Begin()
    {
        if (begun || particles == null) return;
        begun = true;
        particles.Play(false);
        flashes.Play(false);
        particles.SetParticles(buffer, buffer.Length);
    }

    private void LateUpdate()
    {
        if (!begun || Time.deltaTime <= 0f) return;
        if (cleanupRemaining >= 0f)
        {
            cleanupRemaining -= Time.deltaTime;
            if (cleanupRemaining <= 0f) Destroy(gameObject);
            return;
        }

        float previousElapsed = elapsed;
        elapsed += Time.deltaTime;
        bool hasTarget = TryGetTarget(out Vector3 destination, out bool changedScene);
        bool expired = elapsed >= MaximumDuration || changedScene;
        int count = particles.GetParticles(buffer);
        int remaining = 0;
        float scatterProgress = Mathf.Clamp01(elapsed / scatterDuration);
        // Split a frame that crosses the phase boundary instead of holding the
        // endpoints for a whole frame. The remaining time continues the same flight.
        float homingDelta = Mathf.Max(0f, elapsed - Mathf.Max(previousElapsed, scatterDuration));
        for (int i = 0; i < count; i++)
        {
            // ParticleSystem can compact/reorder the array after deaths. Seed is the
            // stable identity, not the current GetParticles array index.
            int id = (int)buffer[i].randomSeed - 1;
            if (expired || id < 0 || id >= scatterOffsets.Length)
            {
                buffer[i].remainingLifetime = -1f;
                continue;
            }
            if (previousElapsed < scatterDuration)
            {
                // Retain a small linear component so the outward curve decelerates
                // without coming to a full stop before homing takes over.
                float ease = 1f - Mathf.Pow(1f - scatterProgress, scatterEasePowers[id]);
                float t = Mathf.Lerp(ease, scatterProgress, 0.35f);
                float rate = (0.65f * scatterEasePowers[id] *
                    Mathf.Pow(1f - scatterProgress, scatterEasePowers[id] - 1f) + 0.35f) / scatterDuration;
                float inverse = 1f - t;
                buffer[i].position = origin + inverse * inverse * scatterStarts[id]
                    + 2f * inverse * t * scatterControls[id] + t * t * scatterOffsets[id];
                // Exact curve tangent, not a finite difference dependent on frame rate.
                flightVelocities[id] = (2f * inverse * (scatterControls[id] - scatterStarts[id])
                    + 2f * t * (scatterOffsets[id] - scatterControls[id])) * rate;
            }
            if (homingDelta > 0f && hasTarget)
            {
                float distance = Vector3.Distance(buffer[i].position, destination);
                float speed = (homingSpeed + Mathf.Max(0f, elapsed - scatterDuration) * 18f + distance * 1.2f)
                    * speedMultipliers[id];
                // Carry outward momentum into a damped pursuit. Keeping this velocity
                // across frames also smooths turns when the controlled character changes.
                float timeLeft = homingDelta;
                bool arrived = false;
                while (timeLeft > 0f)
                {
                    float stepTime = Mathf.Min(timeLeft, 1f / 60f);
                    Vector3 previousPosition = buffer[i].position;
                    // Aim through the character rather than braking to rest behind a
                    // moving target. SmoothDamp still preserves momentum and smooth turns.
                    Vector3 approach = destination - previousPosition;
                    Vector3 pursuitPoint = destination + approach.normalized * Mathf.Max(0.35f, speed * 0.22f);
                    buffer[i].position = Vector3.SmoothDamp(previousPosition, pursuitPoint,
                        ref flightVelocities[id], 0.22f, speed, stepTime);
                    timeLeft -= stepTime;
                    if (PassesTarget(previousPosition, buffer[i].position, destination))
                    {
                        arrived = true;
                        break;
                    }
                }
                if (arrived)
                {
                    buffer[i].position = destination;
                    buffer[i].remainingLifetime = -1f;
                    flashes.Emit(new ParticleSystem.EmitParams
                    {
                        position = destination,
                        velocity = Vector3.zero,
                        startColor = Color.white,
                        startSize = 0.5f,
                        startLifetime = flashDuration
                    }, 1);
                    continue;
                }
            }
            else if (homingDelta > 0f)
            {
                // A temporary gap during possession should not freeze particles abruptly.
                float decay = Mathf.Exp(-5f * homingDelta);
                buffer[i].position += flightVelocities[id] * ((1f - decay) / 5f);
                flightVelocities[id] *= decay;
            }
            remaining++;
        }
        particles.SetParticles(buffer, count);
        if (remaining == 0)
            cleanupRemaining = Mathf.Max(trailDuration, flashDuration) + 0.05f;
    }

    private static bool PassesTarget(Vector3 from, Vector3 to, Vector3 target)
    {
        // Test the travelled segment, not just its endpoint: fast particles must
        // still arrive even when a single integration step crosses the character.
        Vector3 segment = to - from;
        float fraction = segment.sqrMagnitude > 0.000001f
            ? Mathf.Clamp01(Vector3.Dot(target - from, segment) / segment.sqrMagnitude) : 0f;
        return (from + segment * fraction - target).sqrMagnitude <= 0.06f * 0.06f;
    }

    private bool TryGetTarget(out Vector3 position, out bool changedScene)
    {
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover != cachedTarget)
        {
            cachedTarget = mover;
            targetData = mover != null ? mover.GetComponent<ZeldaCharacterData>() : null;
            var visual = mover != null ? mover.transform.Find("Visual") : null;
            targetVisual = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        }
        changedScene = mover != null && mover.gameObject.scene != gameObject.scene;
        position = Vector3.zero;
        if (mover == null || changedScene || !mover.isActiveAndEnabled || (targetData != null && targetData.IsDead))
            return false;
        position = targetVisual != null && targetVisual.enabled && targetVisual.sprite != null
            ? targetVisual.bounds.center : mover.transform.position;
        position.z = origin.z;
        return true;
    }

    private void OnDestroy()
    {
        if (flashMaterial != null) Destroy(flashMaterial);
    }
}
