using UnityEngine;

public class GhostZeldaCharacterData : ZeldaCharacterData
{
    private const float ParticleEmissionInterval = 0.065f;
    private const float ParticleSpawnRadius = 0.21f;
    private const float CharacterCollisionRefreshInterval = 0.5f;
    [SerializeField] private Color idleTint = new Color(0.72f, 0.95f, 1f, 0.78f);
    [SerializeField] private Color movingTint = new Color(0.9f, 1f, 1f, 0.86f);

    private static Sprite ghostDownSprite;
    private static Sprite ghostUpSprite;
    private static Sprite ghostLeftSprite;
    private static Sprite ghostRightSprite;
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

        CreateDirectionSprites();

        Vector2 visualDirection = ToNearestCardinalDirection(facingDirection);
        if (visualDirection == Vector2.up)
        {
            spriteRenderer.sprite = ghostUpSprite;
        }
        else if (visualDirection == Vector2.left)
        {
            spriteRenderer.sprite = ghostLeftSprite;
        }
        else if (visualDirection == Vector2.right)
        {
            spriteRenderer.sprite = ghostRightSprite;
        }
        else
        {
            spriteRenderer.sprite = ghostDownSprite;
        }

        Color baseColor = isMoving ? movingTint : idleTint;
        spriteRenderer.color = GetDamageFeedbackTint(baseColor);
    }

    private static void CreateDirectionSprites()
    {
        if (ghostDownSprite != null)
        {
            return;
        }

        ghostDownSprite = CreateGhostSprite(Vector2.down);
        ghostUpSprite = CreateGhostSprite(Vector2.up);
        ghostLeftSprite = CreateGhostSprite(Vector2.left);
        ghostRightSprite = CreateGhostSprite(Vector2.right);
    }

    private static Sprite CreateGhostSprite(Vector2 facing)
    {
        const int size = 16;
        Texture2D texture = new Texture2D(size, size);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color body = new Color(0.82f, 0.98f, 1f, 0.88f);
        Color shade = new Color(0.46f, 0.78f, 0.96f, 0.7f);
        Color eye = new Color(0.07f, 0.16f, 0.24f, 0.95f);

        FillRect(texture, 0, 0, size, size, clear);
        // Keep the original 16-pixel canvas and prefab scale, but use stepped rows
        // to give the ghost a compact, nearly circular pixel silhouette.
        FillRect(texture, 6, 3, 4, 1, body);
        FillRect(texture, 4, 4, 8, 1, body);
        FillRect(texture, 3, 5, 10, 1, body);
        FillRect(texture, 2, 6, 12, 5, body);
        FillRect(texture, 3, 11, 10, 1, body);
        FillRect(texture, 4, 12, 8, 1, body);
        FillRect(texture, 6, 13, 4, 1, body);

        // Preserve the existing blue shade while following the new rounded edge.
        FillRect(texture, 6, 3, 3, 1, shade);
        FillRect(texture, 4, 4, 2, 1, shade);
        texture.SetPixel(3, 5, shade);
        FillRect(texture, 2, 6, 1, 5, shade);
        texture.SetPixel(3, 11, shade);

        if (facing == Vector2.up)
        {
            FillRect(texture, 6, 11, 1, 1, eye);
            FillRect(texture, 9, 11, 1, 1, eye);
        }
        else if (facing == Vector2.left)
        {
            FillRect(texture, 5, 9, 1, 2, eye);
            FillRect(texture, 8, 9, 1, 1, eye);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 10, 9, 1, 2, eye);
            FillRect(texture, 7, 9, 1, 1, eye);
        }
        else
        {
            FillRect(texture, 6, 9, 1, 2, eye);
            FillRect(texture, 9, 9, 1, 2, eye);
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.25f), 16f);
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

    private static void FillRect(Texture2D texture, int startX, int startY, int width, int height, Color color)
    {
        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }
}
