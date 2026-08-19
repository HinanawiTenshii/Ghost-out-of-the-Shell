using UnityEngine;

/// <summary>
/// Civilian-compatible character data with a distinct blacksmith appearance.
/// It keeps the civilian combat and AI contract while drawing an apron,
/// work gloves and a directional forging hammer.
/// </summary>
public sealed class BlacksmithZeldaCharacterData : ZeldaCharacterData
{
    [Header("Civilian Combat")]
    [SerializeField, Min(0)] private int attackPower = 1;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField, Min(0f)] private float attackDuration = 0.24f;
    [SerializeField] private Vector2 attackSpawnOffset =
        new Vector2(0f, 0.48f);
    [SerializeField] private Vector2 attackSize =
        new Vector2(0.46f, 0.4f);

    [Header("Blacksmith Appearance")]
    [SerializeField] private Color shirtColor =
        new Color(0.19f, 0.27f, 0.34f, 1f);
    [SerializeField] private Color apronColor =
        new Color(0.42f, 0.24f, 0.12f, 1f);
    [SerializeField] private Color trousersColor =
        new Color(0.1f, 0.13f, 0.16f, 1f);
    [SerializeField] private Color skinColor =
        new Color(0.76f, 0.52f, 0.34f, 1f);
    [SerializeField] private Color hairColor =
        new Color(0.12f, 0.075f, 0.045f, 1f);
    [SerializeField] private Color hammerColor =
        new Color(0.48f, 0.55f, 0.6f, 1f);
    [SerializeField] private Color attackVisualTint =
        new Color(0.72f, 0.76f, 0.8f, 0.82f);

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
        ZeldaAttackVisualShape.Hammer;
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
                "Blacksmith " + i);
            attackTextures[i] = CreateTexture(directions[i], true);
            attackSprites[i] = CreateSprite(
                attackTextures[i],
                "Blacksmith Attack " + i);
        }
    }

    private static Sprite CreateSprite(Texture2D texture, string spriteName)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 16f, 16f),
            new Vector2(0.5f, 0.25f),
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
                name = "Runtime Blacksmith",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        FillRect(texture, 0, 0, size, size, Color.clear);

        Color shirtShadow = Color.Lerp(shirtColor, Color.black, 0.32f);
        Color apronShadow = Color.Lerp(apronColor, Color.black, 0.3f);
        Color apronLight = Color.Lerp(apronColor, Color.white, 0.18f);
        Color skinShadow = Color.Lerp(skinColor, Color.black, 0.24f);
        Color handleColor = new Color(0.29f, 0.16f, 0.08f, 1f);
        Color hammerLight = Color.Lerp(hammerColor, Color.white, 0.22f);
        Color eyeColor = new Color(0.04f, 0.1f, 0.16f, 1f);

        // Compact civilian silhouette with a broad leather work apron.
        FillRect(texture, 6, 6, 5, 5, shirtColor);
        FillRect(texture, 6, 6, 1, 5, shirtShadow);
        FillRect(texture, 7, 6, 3, 5, apronColor);
        FillRect(texture, 7, 6, 1, 5, apronShadow);
        FillRect(texture, 8, 9, 1, 2, apronLight);
        texture.SetPixel(9, 7, apronLight);

        FillRect(texture, 6, 3, 2, 3, trousersColor);
        FillRect(texture, 9, 3, 2, 3, trousersColor);
        DrawArms(
            texture,
            facing,
            attacking,
            shirtShadow,
            apronShadow,
            skinShadow);
        DrawHead(texture, facing, skinShadow, eyeColor);
        DrawHammer(
            texture,
            facing,
            attacking,
            handleColor,
            hammerColor,
            hammerLight);

        texture.Apply(false, true);
        return texture;
    }

    private void DrawArms(
        Texture2D texture,
        Vector2 facing,
        bool attacking,
        Color shirtShadow,
        Color gloveShadow,
        Color skinShadow)
    {
        if (!attacking)
        {
            FillRect(texture, 5, 7, 1, 3, shirtShadow);
            FillRect(texture, 11, 7, 1, 3, shirtColor);
            texture.SetPixel(5, 7, gloveShadow);
            texture.SetPixel(11, 7, apronColor);
            return;
        }

        if (facing == Vector2.left)
        {
            FillRect(texture, 2, 8, 4, 2, shirtColor);
            FillRect(texture, 1, 8, 2, 2, apronColor);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 11, 8, 4, 2, shirtColor);
            FillRect(texture, 14, 8, 2, 2, apronColor);
        }
        else
        {
            FillRect(texture, 5, 9, 2, 3, shirtShadow);
            FillRect(texture, 10, 9, 2, 3, shirtColor);
            texture.SetPixel(5, 12, skinShadow);
            texture.SetPixel(11, 12, skinColor);
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
            FillRect(texture, 6, 13, 4, 1, hairColor);
            FillRect(texture, 9, 11, 1, 3, hairColor);
            texture.SetPixel(5, 11, eyeColor);
            FillRect(texture, 5, 10, 2, 1, hairColor);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 7, 10, 5, 4, skinColor);
            FillRect(texture, 7, 13, 4, 1, hairColor);
            FillRect(texture, 7, 11, 1, 3, hairColor);
            texture.SetPixel(11, 11, eyeColor);
            FillRect(texture, 10, 10, 2, 1, hairColor);
        }
        else
        {
            FillRect(texture, 6, 10, 5, 4, skinColor);
            FillRect(texture, 6, 13, 5, 1, hairColor);
            if (facing == Vector2.up)
            {
                FillRect(texture, 6, 12, 5, 2, hairColor);
            }
            else
            {
                texture.SetPixel(7, 11, eyeColor);
                texture.SetPixel(9, 11, eyeColor);
                FillRect(texture, 7, 10, 3, 1, hairColor);
                texture.SetPixel(8, 9, skinShadow);
            }
        }
    }

    private static void DrawHammer(
        Texture2D texture,
        Vector2 facing,
        bool attacking,
        Color handle,
        Color metal,
        Color metalLight)
    {
        if (attacking)
        {
            if (facing == Vector2.left)
            {
                FillRect(texture, 0, 8, 5, 1, handle);
                FillRect(texture, 0, 6, 2, 4, metal);
                texture.SetPixel(0, 9, metalLight);
            }
            else if (facing == Vector2.right)
            {
                FillRect(texture, 11, 8, 5, 1, handle);
                FillRect(texture, 14, 6, 2, 4, metal);
                texture.SetPixel(15, 9, metalLight);
            }
            else
            {
                FillRect(texture, 8, 10, 1, 6, handle);
                FillRect(texture, 6, 14, 5, 2, metal);
                FillRect(texture, 7, 15, 3, 1, metalLight);
            }
            return;
        }

        // Hammer rests beside the apron while idle.
        FillRect(texture, 12, 5, 1, 5, handle);
        FillRect(texture, 11, 9, 3, 2, metal);
        texture.SetPixel(12, 10, metalLight);
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
        Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(
            transform.position + new Vector3(0f, 0.28f, 0f),
            new Vector3(0.72f, 0.8f, 0f));
        Gizmos.color = previousColor;
    }
}
