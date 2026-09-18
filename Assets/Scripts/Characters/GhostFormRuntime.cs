using System.Collections.Generic;
using UnityEngine;

/// <summary>Temporary ghost rules on the existing body; never replaces character data.</summary>
[DefaultExecutionOrder(-50)]
public sealed class GhostFormRuntime : MonoBehaviour
{
    private static int SkillLevel => PlayerGrowthAttributes.Instance != null ? PlayerGrowthAttributes.Instance.GhostFormLevel : 1;
    public static float Duration => SkillLevel >= 2 ? 7f : 5f;
    public static int EnergyCost => SkillLevel >= 3 ? 1 : 2;
    public float TotalDuration { get; private set; }
    public float Remaining { get; private set; }
    private ZeldaCharacterData data;
    private ZeldaFourWayMover mover;
    private Material formMaterial, particleMaterial;
    private ParticleSystem particles;
    private float emissionTimer, collisionTimer;
    private bool cleaned;
    private readonly Dictionary<SpriteRenderer, Material> originalMaterials = new Dictionary<SpriteRenderer, Material>();
    private readonly List<CollisionPair> collisionPairs = new List<CollisionPair>();
    private struct CollisionPair { public Collider2D a, b; public bool previouslyIgnored; }

    public static void Use(ZeldaFourWayMover user)
    {
        if (user == null || SoulMarkRuntime.IsTransferring || ClockworkPuppetRuntime.BlocksCharacterInput) return;
        var character = user.GetComponent<ZeldaCharacterData>();
        if (character == null || character.IsDead || character.IsGhostLike) return;
        // Validate the build-included shader before spending energy.
        if (Resources.Load<Shader>("Shaders/GhostForm") == null) { Debug.LogError("Ghost form shader is missing."); return; }
        if (!character.TrySpendPossessionEnergy(EnergyCost)) return;
        if (user.ActiveCardboardBox != null) user.ActiveCardboardBox.ExitForGhostForm();
        Restore(character, Duration, Duration);
    }

    public static void Restore(ZeldaCharacterData character, float remaining, float totalDuration = 0f)
    {
        if (character.GhostForm != null) character.GhostForm.End();
        if (character.IsDead || character is GhostZeldaCharacterData || remaining <= 0f) return;
        var shader = Resources.Load<Shader>("Shaders/GhostForm");
        if (shader == null) return;
        var effect = character.gameObject.AddComponent<GhostFormRuntime>();
        effect.data = character;
        effect.mover = character.GetComponent<ZeldaFourWayMover>();
        // Snapshot each cast's duration, so upgrading during a cast cannot rescale its countdown.
        // Older saves have no duration field; preserve their remaining time without truncating it.
        effect.TotalDuration = totalDuration > 0f ? Mathf.Clamp(totalDuration, 5f, 7f) : Mathf.Max(Duration, remaining > 5f ? 7f : 5f);
        effect.Remaining = Mathf.Clamp(remaining, 0f, effect.TotalDuration);
        effect.formMaterial = new Material(shader) { name = "Temporary Ghost Form" };
        Color eye = character.GhostFormEyeColor;
        if (QualitySettings.activeColorSpace == ColorSpace.Linear) eye = eye.linear;
        effect.formMaterial.SetVector("_EyeColor", eye);
        character.GhostForm = effect;
        effect.CreateParticles();
        effect.RefreshCollisions();
        if (effect.mover != null)
        {
            effect.mover.CancelAttackForStun();
            effect.mover.RefreshFormVisuals();
        }
    }

    public void ApplyVisual(SpriteRenderer renderer, bool moving)
    {
        if (renderer == null || formMaterial == null || Remaining <= 0f) return;
        if (!originalMaterials.ContainsKey(renderer)) originalMaterials.Add(renderer, renderer.sharedMaterial);
        renderer.sharedMaterial = formMaterial;
        renderer.color = Color.white;
        formMaterial.SetColor("_GhostTint", moving ? new Color(0.9f, 1f, 1f, 0.76f) : new Color(0.72f, 0.95f, 1f, 0.69f));
        if (particles != null)
        {
            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sortingLayerID = renderer.sortingLayerID;
            particleRenderer.sortingOrder = renderer.sortingOrder;
        }
    }

    private void Update()
    {
        if (data == null || data.IsDead) { End(); return; }
        Remaining = Mathf.Max(0f, Remaining - Time.deltaTime);
        if (Remaining <= 0f) { End(); return; }
        collisionTimer -= Time.deltaTime;
        if (collisionTimer <= 0f) { collisionTimer = 0.25f; RefreshCollisions(); }
        emissionTimer += Time.deltaTime;
        while (emissionTimer >= 0.065f)
        {
            emissionTimer -= 0.065f;
            float angle = Random.Range(0f, Mathf.PI * 2f);
            Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle));
            particles.Emit(new ParticleSystem.EmitParams {
                position = particles.transform.position + direction * 0.21f,
                velocity = direction * Random.Range(0.14f, 0.3f),
                startColor = new Color(0.72f, 0.95f, 1f, 0.78f),
                startLifetime = Random.Range(0.55f, 0.9f), startSize = Random.Range(0.035f, 0.07f)
            }, 1);
        }
    }

    private void CreateParticles()
    {
        var obj = new GameObject("Ghost Form Particles");
        obj.transform.SetParent(transform, false);
        obj.transform.localPosition = new Vector3(0f, 0.18f, 0f);
        particles = obj.AddComponent<ParticleSystem>();
        var main = particles.main;
        main.loop = true; main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
        main.startSpeed = 0; main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f); main.maxParticles = 64;
        var emission = particles.emission; emission.enabled = false;
        var shape = particles.shape; shape.enabled = false;
        var collision = particles.collision; collision.enabled = false;
        var fade = particles.colorOverLifetime; fade.enabled = true;
        var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0.45f, 0.55f), new GradientAlphaKey(0f, 1f) });
        fade.color = gradient;
        particleMaterial = new Material(Shader.Find("Sprites/Default"));
        particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
        particles.Play();
    }

    private void RefreshCollisions()
    {
        var own = GetComponentsInChildren<Collider2D>(true);
        foreach (var other in ZeldaRuntimeRegistry.Movers)
        {
            if (other == null || other == mover) continue;
            var otherData = other.GetComponent<ZeldaCharacterData>();
            foreach (var a in own) foreach (var b in other.GetComponentsInChildren<Collider2D>(true))
            {
                if (a == null || b == null || a.isTrigger || b.isTrigger) continue;
                bool recorded = false;
                foreach (var pair in collisionPairs) if (pair.a == a && pair.b == b) { recorded = true; break; }
                if (!recorded) collisionPairs.Add(new CollisionPair { a = a, b = b,
                    previouslyIgnored = Physics2D.GetIgnoreCollision(a, b) && (otherData == null || !otherData.IsGhostForm) });
                Physics2D.IgnoreCollision(a, b, true);
            }
        }
    }

    private void End() { Cleanup(); Destroy(this); }
    private void Cleanup()
    {
        if (cleaned) return;
        cleaned = true;
        Remaining = 0f;
        if (data != null && data.GhostForm == this) data.GhostForm = null;
        foreach (var pair in collisionPairs)
        {
            if (pair.a == null || pair.b == null || pair.previouslyIgnored) continue;
            var other = pair.b.GetComponentInParent<ZeldaCharacterData>();
            if (other == null || other.ParticipatesInCharacterCollision) Physics2D.IgnoreCollision(pair.a, pair.b, false);
        }
        foreach (var original in originalMaterials) if (original.Key != null) original.Key.sharedMaterial = original.Value;
        if (mover != null && data != null && !data.IsDead) mover.RefreshFormVisuals();
        if (particles != null) Destroy(particles.gameObject);
        if (formMaterial != null) Destroy(formMaterial);
        if (particleMaterial != null) Destroy(particleMaterial);
    }
    private void OnDisable() => Cleanup();
    private void OnDestroy() => Cleanup();
}
