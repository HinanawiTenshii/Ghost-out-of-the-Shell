using UnityEngine;

public partial class GhostZeldaCharacterData : ZeldaCharacterData
{
    private const float ParticleEmissionInterval = 0.065f;
    private const float ParticleSpawnRadius = 0.21f;
    private const float CharacterCollisionRefreshInterval = 0.5f;
    [SerializeField] private Color idleTint = new Color(0.72f, 0.95f, 1f, 0.78f);
    [SerializeField] private Color movingTint = new Color(0.9f, 1f, 1f, 0.86f);
    public Color ConsciousnessParticleTint => idleTint;

    private ParticleSystem ghostParticles;
    private Material ghostParticleMaterial;
    private float particleEmissionTimer;
    private float characterCollisionRefreshTimer;

    public override bool CanAttack => false;
    public override bool CanTakeAttackDamage => false;
    public override bool CanInteractWithDoors => false;
    public override bool CanBeDetectedByAi => false;
    public override bool DestroyAfterControlSwitch => true;
    public override bool ParticipatesInCharacterCollision => false;

    protected override void Awake()
    {
        base.Awake();
        EnsureGhostParticles();
        IgnoreCharacterCollisions();
    }

    protected override void Update()
    {
        base.Update();
        UpdateGhostParticles(Time.deltaTime);
        UpdateIgnoredCharacterCollisions(Time.deltaTime);
    }

    public override void ApplyCharacterVisual(SpriteRenderer spriteRenderer, Vector2 facingDirection, bool isMoving, bool isAttacking)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.sprite = SelectGhostSprite(facingDirection, isMoving, Time.time);
        Color baseColor = isMoving ? movingTint : idleTint;
        spriteRenderer.color = GetDamageFeedbackTint(baseColor);
    }

    private static Vector2 ToNearestCardinalDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0f)
        {
            return Vector2.down;
        }

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x > 0f ? Vector2.right : Vector2.left;
        }

        return direction.y > 0f ? Vector2.up : Vector2.down;
    }

    private void EnsureGhostParticles()
    {
        if (ghostParticles != null)
        {
            return;
        }

        GameObject particleObject = new GameObject("Ghost Visual Particles");
        particleObject.transform.SetParent(transform, false);
        particleObject.transform.localPosition = new Vector3(0f, 0.18f, 0f);

        ghostParticles = particleObject.AddComponent<ParticleSystem>();
        ParticleSystem.MainModule main = ghostParticles.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 0.9f);
        main.startSpeed = 0f;
        main.startSize = new ParticleSystem.MinMaxCurve(0.035f, 0.07f);
        main.maxParticles = 64;

        ParticleSystem.EmissionModule emission = ghostParticles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = ghostParticles.shape;
        shape.enabled = false;

        ParticleSystem.CollisionModule collision = ghostParticles.collision;
        collision.enabled = false;

        ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ghostParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient fadeGradient = new Gradient();
        fadeGradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new[]
            {
                new GradientAlphaKey(0.85f, 0f),
                new GradientAlphaKey(0.45f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = fadeGradient;

        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        SpriteRenderer characterRenderer = GetComponent<SpriteRenderer>();
        if (characterRenderer != null)
        {
            particleRenderer.sortingLayerID = characterRenderer.sortingLayerID;
            particleRenderer.sortingOrder = characterRenderer.sortingOrder;
        }

        Shader particleShader = Shader.Find("Sprites/Default");
        if (particleShader != null)
        {
            ghostParticleMaterial = new Material(particleShader)
            {
                name = "Ghost Visual Particle Material"
            };
            particleRenderer.material = ghostParticleMaterial;
        }

        ghostParticles.Play();
    }

    private void UpdateGhostParticles(float deltaTime)
    {
        if (ghostParticles == null)
        {
            EnsureGhostParticles();
        }

        particleEmissionTimer += Mathf.Max(0f, deltaTime);
        while (particleEmissionTimer >= ParticleEmissionInterval)
        {
            particleEmissionTimer -= ParticleEmissionInterval;
            EmitGhostParticle();
        }
    }

    private void EmitGhostParticle()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 direction = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        Color particleColor = idleTint;
        particleColor.a = Mathf.Min(particleColor.a, 0.78f);

        ParticleSystem.EmitParams emitParams = new ParticleSystem.EmitParams
        {
            position = ghostParticles.transform.position + direction * ParticleSpawnRadius,
            velocity = direction * Random.Range(0.14f, 0.3f),
            startColor = particleColor,
            startLifetime = Random.Range(0.55f, 0.9f),
            startSize = Random.Range(0.035f, 0.07f)
        };
        ghostParticles.Emit(emitParams, 1);
    }

    private void UpdateIgnoredCharacterCollisions(float deltaTime)
    {
        characterCollisionRefreshTimer -= Mathf.Max(0f, deltaTime);
        if (characterCollisionRefreshTimer > 0f)
        {
            return;
        }

        characterCollisionRefreshTimer = CharacterCollisionRefreshInterval;
        IgnoreCharacterCollisions();
    }

    private void IgnoreCharacterCollisions()
    {
        Collider2D[] ghostColliders = GetComponentsInChildren<Collider2D>(true);
        foreach (ZeldaFourWayMover otherMover in ZeldaRuntimeRegistry.Movers)
        {
            ZeldaCharacterData otherCharacter = otherMover != null
                ? otherMover.GetComponent<ZeldaCharacterData>()
                : null;
            if (otherCharacter == null || otherCharacter == this)
            {
                continue;
            }

            Collider2D[] otherColliders = otherCharacter.GetComponentsInChildren<Collider2D>(true);
            for (int ghostIndex = 0; ghostIndex < ghostColliders.Length; ghostIndex++)
            {
                Collider2D ghostCollider = ghostColliders[ghostIndex];
                if (ghostCollider == null)
                {
                    continue;
                }

                for (int otherIndex = 0; otherIndex < otherColliders.Length; otherIndex++)
                {
                    Collider2D otherCollider = otherColliders[otherIndex];
                    if (otherCollider != null && otherCollider != ghostCollider)
                    {
                        Physics2D.IgnoreCollision(ghostCollider, otherCollider, true);
                    }
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (ghostParticleMaterial != null)
        {
            Destroy(ghostParticleMaterial);
        }
    }
}
