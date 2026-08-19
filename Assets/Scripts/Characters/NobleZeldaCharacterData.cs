using UnityEngine;

/// <summary>
/// Civilian-compatible character data with a long ceremonial robe silhouette.
/// Its combat contract remains identical to the standard civilian.
/// </summary>
public sealed class NobleZeldaCharacterData : ZeldaCharacterData
{
    [Header("Civilian Combat")]
    [SerializeField, Min(0)] private int attackPower = 1;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField, Min(0f)] private float attackDuration = 0.2f;
    [SerializeField] private Vector2 attackSpawnOffset =
        new Vector2(0f, 0.48f);
    [SerializeField] private Vector2 attackSize =
        new Vector2(0.42f, 0.36f);

    [Header("Noble Robe Appearance")]
    [SerializeField] private Color robeColor =
        new Color(0.22f, 0.25f, 0.52f, 1f);
    [SerializeField] private Color robeTrimColor =
        new Color(0.82f, 0.72f, 0.36f, 1f);
    [SerializeField] private Color skinColor =
        new Color(0.84f, 0.64f, 0.46f, 1f);
    [SerializeField] private Color hairColor =
        new Color(0.14f, 0.09f, 0.07f, 1f);
    [SerializeField] private Color attackVisualTint =
        new Color(0.85f, 0.72f, 0.56f, 0.8f);

    [Header("Royal Appearance")]
    [SerializeField] private bool showCrown;
    [SerializeField] private Color crownColor =
        new Color(0.95f, 0.76f, 0.18f, 1f);
    [SerializeField] private Color crownJewelColor =
        new Color(0.22f, 0.72f, 0.92f, 1f);

    private readonly Sprite[] idleSprites = new Sprite[4];
    private readonly Sprite[] attackSprites = new Sprite[4];
    private readonly Texture2D[] idleTextures = new Texture2D[4];
    private readonly Texture2D[] attackTextures = new Texture2D[4];

    public override bool CanAttack => true;
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape =>
        ZeldaAttackVisualShape.Rock;
    public override Color AttackVisualTint => attackVisualTint;

    public override void ApplyCharacterVisual(
        SpriteRenderer spriteRenderer,
        Vector2 facingDirection,
        bool isMoving,
        bool isAttacking)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        EnsureSprites();
        int directionIndex = DirectionIndex(facingDirection);
        spriteRenderer.sprite = isAttacking
            ? attackSprites[directionIndex]
            : idleSprites[directionIndex];
        Color movementTint = isMoving
            ? Color.white
            : new Color(0.94f, 0.97f, 1f, 1f);
        spriteRenderer.color = GetDamageFeedbackTint(movementTint);
    }

    private void EnsureSprites()
    {
        if (idleSprites[0] != null)
        {
            return;
        }

        Vector2[] directions =
        {
            Vector2.down,
            Vector2.up,
            Vector2.left,
            Vector2.right
        };
        for (int i = 0; i < directions.Length; i++)
        {
            idleTextures[i] = CreateTexture(directions[i], false);
            idleSprites[i] = CreateSprite(
                idleTextures[i],
                "Noble " + i);
            attackTextures[i] = CreateTexture(directions[i], true);
            attackSprites[i] = CreateSprite(
                attackTextures[i],
                "Noble Attack " + i);
        }
    }

    private static Sprite CreateSprite(Texture2D texture, string spriteName)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.22f),
            16f);
        sprite.name = spriteName;
        return sprite;
    }

    private Texture2D CreateTexture(Vector2 facing, bool attacking)
    {
        const int size = 16;
        Texture2D texture =
            new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "Runtime Noble",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        FillRect(texture, 0, 0, size, size, Color.clear);

        Color robeShadow = Color.Lerp(robeColor, Color.black, 0.34f);
        Color robeLight = Color.Lerp(robeColor, Color.white, 0.18f);
        Color trimShadow = Color.Lerp(robeTrimColor, Color.black, 0.22f);
        Color skinShadow = Color.Lerp(skinColor, Color.black, 0.24f);
        Color shoeColor = Color.Lerp(robeShadow, Color.black, 0.35f);
        Color eyeColor = new Color(0.04f, 0.1f, 0.17f, 1f);

        DrawLongRobe(
            texture,
            facing,
            robeShadow,
            robeLight,
            trimShadow,
            shoeColor);
        DrawSleeves(
            texture,
            facing,
            attacking,
            robeShadow,
            skinShadow);
        DrawHead(texture, facing, skinShadow, eyeColor);
        if (showCrown)
        {
            DrawCrown(texture, facing);
        }

        texture.Apply(false, true);
        return texture;
    }

    private void DrawLongRobe(
        Texture2D texture,
        Vector2 facing,
        Color robeShadow,
        Color robeLight,
        Color trimShadow,
        Color shoeColor)
    {
        // A narrow upper body widening into a floor-length hem.
        FillRect(texture, 5, 7, 7, 4, robeColor);
        FillRect(texture, 5, 7, 1, 4, robeShadow);
        FillRect(texture, 5, 5, 7, 2, robeColor);
        FillRect(texture, 4, 3, 9, 2, robeColor);
        FillRect(texture, 4, 2, 9, 1, robeShadow);
        FillRect(texture, 5, 1, 2, 1, shoeColor);
        FillRect(texture, 10, 1, 2, 1, shoeColor);

        if (facing == Vector2.up)
        {
            FillRect(texture, 6, 8, 5, 2, robeShadow);
            FillRect(texture, 8, 3, 1, 6, robeLight);
            texture.SetPixel(8, 2, robeTrimColor);
            return;
        }

        if (facing == Vector2.left)
        {
            FillRect(texture, 4, 3, 2, 5, robeShadow);
            FillRect(texture, 6, 4, 1, 6, robeTrimColor);
            texture.SetPixel(6, 3, trimShadow);
            return;
        }

        if (facing == Vector2.right)
        {
            FillRect(texture, 11, 3, 2, 5, robeLight);
            FillRect(texture, 10, 4, 1, 6, robeTrimColor);
            texture.SetPixel(10, 3, trimShadow);
            return;
        }

        // Front-facing collar, long central trim and jeweled clasp.
        FillRect(texture, 7, 9, 3, 1, robeTrimColor);
        FillRect(texture, 8, 4, 1, 5, robeTrimColor);
        texture.SetPixel(8, 3, trimShadow);
        texture.SetPixel(8, 9, ZeldaUiPalette.Primary);
        texture.SetPixel(10, 6, robeLight);
    }

    private void DrawSleeves(
        Texture2D texture,
        Vector2 facing,
        bool attacking,
        Color robeShadow,
        Color skinShadow)
    {
        if (!attacking)
        {
            FillRect(texture, 4, 7, 1, 3, robeShadow);
            FillRect(texture, 12, 7, 1, 3, robeColor);
            texture.SetPixel(4, 7, skinShadow);
            texture.SetPixel(12, 7, skinColor);
            return;
        }

        if (facing == Vector2.left)
        {
            FillRect(texture, 2, 7, 4, 2, robeColor);
            FillRect(texture, 1, 7, 2, 2, skinColor);
            FillRect(texture, 12, 7, 1, 3, robeShadow);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 11, 7, 4, 2, robeColor);
            FillRect(texture, 14, 7, 2, 2, skinColor);
            FillRect(texture, 4, 7, 1, 3, robeShadow);
        }
        else if (facing == Vector2.up)
        {
            FillRect(texture, 5, 10, 2, 4, robeShadow);
            FillRect(texture, 10, 10, 2, 4, robeColor);
            texture.SetPixel(5, 14, skinShadow);
            texture.SetPixel(11, 14, skinColor);
        }
        else
        {
            FillRect(texture, 5, 5, 2, 5, robeShadow);
            FillRect(texture, 10, 5, 2, 5, robeColor);
            texture.SetPixel(5, 4, skinShadow);
            texture.SetPixel(11, 4, skinColor);
        }
    }

    private void DrawHead(
        Texture2D texture,
        Vector2 facing,
        Color skinShadow,
        Color eyeColor)
    {
        if (facing == Vector2.left)
        {
            FillRect(texture, 5, 10, 5, 4, skinColor);
            FillRect(texture, 6, 13, 4, 2, hairColor);
            FillRect(texture, 9, 11, 1, 3, hairColor);
            texture.SetPixel(5, 11, eyeColor);
            texture.SetPixel(5, 10, skinShadow);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 7, 10, 5, 4, skinColor);
            FillRect(texture, 7, 13, 4, 2, hairColor);
            FillRect(texture, 7, 11, 1, 3, hairColor);
            texture.SetPixel(11, 11, eyeColor);
            texture.SetPixel(11, 10, skinShadow);
        }
        else
        {
            FillRect(texture, 6, 10, 5, 4, skinColor);
            FillRect(texture, 6, 13, 5, 2, hairColor);
            if (facing == Vector2.up)
            {
                FillRect(texture, 6, 11, 5, 3, hairColor);
            }
            else
            {
                texture.SetPixel(7, 11, eyeColor);
                texture.SetPixel(9, 11, eyeColor);
                texture.SetPixel(8, 10, skinShadow);
            }
        }
    }

    private void DrawCrown(Texture2D texture, Vector2 facing)
    {
        int centerX = facing == Vector2.left
            ? 7
            : facing == Vector2.right ? 9 : 8;
        Color crownShadow = Color.Lerp(crownColor, Color.black, 0.24f);

        // Broad gold band plus three tall points remain readable in every facing.
        FillRect(texture, centerX - 3, 14, 7, 1, crownShadow);
        texture.SetPixel(centerX - 3, 15, crownColor);
        texture.SetPixel(centerX - 2, 14, crownColor);
        texture.SetPixel(centerX, 15, crownColor);
        texture.SetPixel(centerX + 2, 14, crownColor);
        texture.SetPixel(centerX + 3, 15, crownColor);
        texture.SetPixel(centerX, 14, crownJewelColor);
    }

    private static int DirectionIndex(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x < 0f ? 2 : 3;
        }

        return direction.y > 0f ? 1 : 0;
    }

    private static void FillRect(
        Texture2D texture,
        int x,
        int y,
        int width,
        int height,
        Color color)
    {
        for (int py = y; py < y + height; py++)
        {
            for (int px = x; px < x + width; px++)
            {
                if (px >= 0 && px < texture.width &&
                    py >= 0 && py < texture.height)
                {
                    texture.SetPixel(px, py, color);
                }
            }
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < idleSprites.Length; i++)
        {
            if (idleSprites[i] != null) Destroy(idleSprites[i]);
            if (attackSprites[i] != null) Destroy(attackSprites[i]);
            if (idleTextures[i] != null) Destroy(idleTextures[i]);
            if (attackTextures[i] != null) Destroy(attackTextures[i]);
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        attackPower = Mathf.Max(0, attackPower);
        attackDuration = Mathf.Max(0f, attackDuration);
        attackSize.x = Mathf.Max(0f, attackSize.x);
        attackSize.y = Mathf.Max(0f, attackSize.y);
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Color previousColor = Gizmos.color;
        Gizmos.color = new Color(0.42f, 0.62f, 1f, 0.9f);
        Gizmos.DrawWireCube(
            transform.position + new Vector3(0f, 0.27f, 0f),
            new Vector3(0.72f, 0.84f, 0f));
        Gizmos.color = previousColor;
    }
}
