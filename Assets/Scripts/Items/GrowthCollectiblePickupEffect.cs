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
    private Vector3[] returnStarts, returnControls, returnEndOffsets, returnTargets;
    private float[] returnElapsed, returnDurations;
    private bool[] returnReady;
    private GrowthPickupRibbonTrail ribbons;
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
        // Existing scene instances stored 0.02s: give them a readable ribbon
        // without rewriting scene or prefab overrides.
        trailDuration = Mathf.Clamp(trailSeconds, 0.18f, 0.45f);
        flashDuration = Mathf.Clamp(flashSeconds, 0.05f, 0.5f);
        origin = transform.position;
        buffer = new ParticleSystem.Particle[count];
        scatterOffsets = new Vector3[count];
        scatterStarts = new Vector3[count];
        scatterControls = new Vector3[count];
        scatterEasePowers = new float[count];
        flightVelocities = new Vector3[count];
        speedMultipliers = new float[count];
        returnStarts = new Vector3[count];
        returnControls = new Vector3[count];
        returnEndOffsets = new Vector3[count];
        returnTargets = new Vector3[count];
        returnElapsed = new float[count];
        returnDurations = new float[count];
        returnReady = new bool[count];
        var random = new System.Random(GetInstanceID());

        particles = gameObject.AddComponent<ParticleSystem>();
        ConfigureSystem(particles, count);
        var trails = particles.trails;
        // A single shared ribbon mesh owns history and absorption independently
        // of ParticleSystem lifetime/array compaction.
        trails.enabled = false;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = dotMaterial;
        renderer.sortingOrder = sortingOrder;
        ribbons = new GrowthPickupRibbonTrail(transform, count, trailDuration, trailMaterial, sortingOrder - 1);

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
            ribbons.SetWidth(i, buffer[i].startSize);
            ribbons.Record(i, buffer[i].position, 0f);
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
                if (id >= 0 && id < scatterOffsets.Length) ribbons.Clear(id);
                continue;
            }
            if (buffer[i].remainingLifetime <= 0f) continue;
            if (previousElapsed < scatterDuration)
            {
                float sampleTime = previousElapsed;
                float endTime = Mathf.Min(elapsed, scatterDuration);
                while (sampleTime < endTime)
                {
                    sampleTime = Mathf.Min(sampleTime + 1f / 60f, endTime);
                    EvaluateScatter(id, sampleTime, out Vector3 position, out Vector3 velocity);
                    buffer[i].position = position;
                    flightVelocities[id] = velocity;
                    ribbons.Record(id, position, sampleTime);
                }
            }
            if (homingDelta > 0f && hasTarget)
            {
                // Rebase only on a teleport/possession jump, preserving the current
                // head position and tangent. Ordinary movement bends the endpoint.
                if (!returnReady[id] || (destination - returnTargets[id]).sqrMagnitude > 9f)
                    BeginReturn(id, buffer[i].position, destination);
                returnTargets[id] = destination;
                float timeLeft = homingDelta;
                bool arrived = false;
                while (timeLeft > 0f)
                {
                    float stepTime = Mathf.Min(timeLeft, 1f / 60f);
                    Vector3 previousPosition = buffer[i].position;
                    returnElapsed[id] += stepTime;
                    float u = Mathf.Clamp01(returnElapsed[id] / returnDurations[id]);
                    buffer[i].position = EvaluateReturn(returnStarts[id], returnControls[id],
                        destination + returnEndOffsets[id], destination, u);
                    flightVelocities[id] = (buffer[i].position - previousPosition) / stepTime;
                    timeLeft -= stepTime;
                    ribbons.Record(id, buffer[i].position, elapsed - timeLeft);
                    if (u >= 1f || PassesTarget(previousPosition, buffer[i].position, destination))
                    {
                        arrived = true;
                        break;
                    }
                }
                if (arrived)
                {
                    buffer[i].position = destination;
                    buffer[i].remainingLifetime = -1f;
                    ribbons.Arrive(id, destination, elapsed - timeLeft);
                    flashes.Emit(new ParticleSystem.EmitParams
                    {
                        position = destination,
                        velocity = Vector3.zero,
                        startColor = Color.white,
                        startSize = 0.36f,
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
                returnReady[id] = false;
                ribbons.Record(id, buffer[i].position, elapsed);
            }
            remaining++;
        }
        particles.SetParticles(buffer, count);
        if (expired) ribbons.ClearAll();
        ribbons.Render(elapsed, hasTarget, destination);
        if (remaining == 0 && !ribbons.HasVisibleRibbons)
            cleanupRemaining = flashDuration + 0.05f;
    }

    private void EvaluateScatter(int id, float time, out Vector3 position, out Vector3 velocity)
    {
        float progress = Mathf.Clamp01(time / scatterDuration);
        float ease = 1f - Mathf.Pow(1f - progress, scatterEasePowers[id]);
        float t = Mathf.Lerp(ease, progress, 0.35f);
        float rate = (0.65f * scatterEasePowers[id] *
            Mathf.Pow(1f - progress, scatterEasePowers[id] - 1f) + 0.35f) / scatterDuration;
        float inverse = 1f - t;
        position = origin + inverse * inverse * scatterStarts[id]
            + 2f * inverse * t * scatterControls[id] + t * t * scatterOffsets[id];
        velocity = (2f * inverse * (scatterControls[id] - scatterStarts[id])
            + 2f * t * (scatterOffsets[id] - scatterControls[id])) * rate;
    }

    private void BeginReturn(int id, Vector3 position, Vector3 destination)
    {
        Vector3 direction = destination - position;
        float distance = direction.magnitude;
        returnDurations[id] = Mathf.Clamp((0.3f + distance * 1.8f / homingSpeed)
            / speedMultipliers[id], 0.38f, 1.15f);
        returnElapsed[id] = 0f;
        returnStarts[id] = position;
        // Cubic derivative at the start matches the outward flight tangent.
        Vector3 tangent = flightVelocities[id] * (returnDurations[id] / (3f * 0.35f));
        returnControls[id] = position + Vector3.ClampMagnitude(tangent, Mathf.Max(0.25f, distance));
        Vector3 side = new Vector3(-direction.y, direction.x, 0f).normalized;
        float bend = Mathf.Min(distance * 0.24f, 0.7f) * ((id & 1) == 0 ? 1f : -1f);
        returnEndOffsets[id] = -direction * 0.18f + side * bend;
        returnTargets[id] = destination;
        returnReady[id] = true;
    }

    private static Vector3 EvaluateReturn(Vector3 start, Vector3 control, Vector3 endControl,
        Vector3 end, float progress)
    {
        float u = Mathf.Clamp01(progress);
        float t = 0.35f * u + 0.65f * u * u * u;
        float inverse = 1f - t;
        return inverse * inverse * inverse * start + 3f * inverse * inverse * t * control
            + 3f * inverse * t * t * endControl + t * t * t * end;
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
            // Rebase from the existing head and tangent even for a nearby
            // possession switch; don't teleport the end of an almost-finished curve.
            if (returnReady != null) System.Array.Clear(returnReady, 0, returnReady.Length);
            cachedTarget = mover;
            targetData = mover != null ? mover.GetComponent<ZeldaCharacterData>() : null;
            var visual = mover != null ? mover.transform.Find("Visual") : null;
            targetVisual = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        }
        // Traveling players live in DontDestroyOnLoad while still collecting in
        // the active level. Compare gameplay scenes, not Unity storage scenes.
        changedScene = mover != null &&
            ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != gameObject.scene;
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
        ribbons?.Dispose();
        if (flashMaterial != null) Destroy(flashMaterial);
    }
}
