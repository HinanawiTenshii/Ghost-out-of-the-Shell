using UnityEngine;

/// <summary>
/// Oversized SwordGuy-derived mechanical brute with a radial shockwave attack.
/// </summary>
public sealed class BehemothZeldaCharacterData : SwordZeldaCharacterData
{
    [Header("Behemoth Combat")]
    [SerializeField, Min(0)] private int behemothAttackPower = 4;
    [SerializeField] private GameObject behemothAttackPrefab;
    [SerializeField, Min(0.05f)] private float behemothAttackDuration = 0.58f;
    [SerializeField] private Vector2 shockwaveSize = new Vector2(5.1f, 5.1f);
    [SerializeField] private Color shockwaveColor =
        new Color(0.95f, 0.12f, 0.08f, 0.78f);

    [Header("Behemoth Appearance")]
    [SerializeField] private Color armorColor =
        new Color(0.46f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color armorDarkColor =
        new Color(0.18f, 0.21f, 0.22f, 1f);
    [SerializeField] private Color energyColor =
        new Color(0.82f, 0.035f, 0.025f, 1f);
    [SerializeField] private Color eyeColor =
        new Color(1f, 0.75f, 0.12f, 1f);

    private readonly Sprite[] idleSprites = new Sprite[4];
    private readonly Sprite[] attackSprites = new Sprite[4];
    private readonly Texture2D[] idleTextures = new Texture2D[4];
    private readonly Texture2D[] attackTextures = new Texture2D[4];

    public override int AttackPower => behemothAttackPower;
    public override GameObject AttackPrefab => behemothAttackPrefab;
    public override float AttackDuration => behemothAttackDuration;
    public override Vector2 AttackSpawnOffset => Vector2.zero;
    public override Vector2 AttackSize => shockwaveSize;
    public override ZeldaAttackVisualShape AttackVisualShape =>
        ZeldaAttackVisualShape.Shockwave;
    public override Color AttackVisualTint => shockwaveColor;

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
        int index = DirectionIndex(facingDirection);
        spriteRenderer.sprite = isAttacking
            ? attackSprites[index]
            : idleSprites[index];
        Color baseTint = isMoving
            ? Color.white
            : new Color(0.92f, 0.96f, 1f, 1f);
        spriteRenderer.color = GetDamageFeedbackTint(baseTint);
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
                "Behemoth " + i);
            attackTextures[i] = CreateTexture(directions[i], true);
            attackSprites[i] = CreateSprite(
                attackTextures[i],
                "Behemoth Attack " + i);
        }
    }

    private static Sprite CreateSprite(Texture2D texture, string spriteName)
    {
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, 32f, 28f),
            new Vector2(0.5f, 0.18f),
            16f);
        sprite.name = spriteName;
        return sprite;
    }

    private Texture2D CreateTexture(Vector2 facing, bool attacking)
    {
        const int width = 32;
        const int height = 28;
        Texture2D texture =
            new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "Runtime Behemoth",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };
        FillRect(texture, 0, 0, width, height, Color.clear);

        Color armorLight = Color.Lerp(armorColor, Color.white, 0.28f);
        Color armorShadow = Color.Lerp(armorColor, Color.black, 0.3f);
        Color energyLight = Color.Lerp(energyColor, Color.white, 0.18f);

        // Wide armored torso and red internal energy frame.
        FillRect(texture, 7, 9, 18, 12, energyColor);
        FillRect(texture, 9, 10, 14, 10, armorDarkColor);
        FillRect(texture, 11, 11, 10, 8, armorColor);
        FillRect(texture, 12, 17, 8, 2, energyLight);
        FillRect(texture, 14, 12, 4, 5, energyColor);
        FillRect(texture, 15, 13, 2, 3, armorDarkColor);

        // Raised shoulder towers and broad plated arms.
        FillRect(texture, 3, 14, 7, 9, armorColor);
        FillRect(texture, 22, 14, 7, 9, armorColor);
        FillRect(texture, 4, 20, 4, 5, armorLight);
        FillRect(texture, 24, 20, 4, 5, armorLight);
        FillRect(texture, 1, attacking ? 7 : 10, 7, 8, armorShadow);
        FillRect(texture, 24, attacking ? 7 : 10, 7, 8, armorShadow);
        FillRect(texture, 2, attacking ? 8 : 11, 5, 2, armorLight);
        FillRect(texture, 25, attacking ? 8 : 11, 5, 2, armorLight);

        // Heavy fists become lower and wider during the ground slam.
        int fistY = attacking ? 4 : 7;
        FillRect(texture, 1, fistY, 7, 5, armorColor);
        FillRect(texture, 24, fistY, 7, 5, armorColor);
        FillRect(texture, 2, fistY, 2, 2, armorLight);
        FillRect(texture, 28, fistY, 2, 2, armorLight);
        FillRect(texture, 3, fistY - 1, 1, 2, armorDarkColor);
        FillRect(texture, 6, fistY - 1, 1, 2, armorDarkColor);
        FillRect(texture, 25, fistY - 1, 1, 2, armorDarkColor);
        FillRect(texture, 28, fistY - 1, 1, 2, armorDarkColor);

        // Two separated piston legs make the silhouette much larger than
        // ordinary characters without becoming a solid rectangle.
        FillRect(texture, 9, 2, 6, 7, armorDarkColor);
        FillRect(texture, 18, 2, 6, 7, armorDarkColor);
        FillRect(texture, 10, 3, 4, 5, armorColor);
        FillRect(texture, 19, 3, 4, 5, armorColor);
        FillRect(texture, 10, 2, 4, 2, armorLight);
        FillRect(texture, 19, 2, 4, 2, armorLight);
        texture.SetPixel(12, 6, energyColor);
        texture.SetPixel(21, 6, energyColor);

        DrawDirectionalHead(
            texture,
            facing,
            armorLight,
            armorShadow,
            energyLight);

        if (attacking)
        {
            // Bright core and ground-contact sparks telegraph the shockwave.
            FillRect(texture, 13, 10, 6, 6, energyLight);
            FillRect(texture, 15, 11, 2, 4, energyColor);
            FillRect(texture, 0, 3, 4, 1, energyLight);
            FillRect(texture, 28, 3, 4, 1, energyLight);
        }

        texture.Apply(false, true);
        return texture;
    }

    private void DrawDirectionalHead(
        Texture2D texture,
        Vector2 facing,
        Color armorLight,
        Color armorShadow,
        Color energyLight)
    {
        if (facing == Vector2.left)
        {
            FillRect(texture, 9, 19, 10, 6, armorColor);
            FillRect(texture, 8, 21, 4, 3, armorShadow);
            FillRect(texture, 10, 24, 7, 2, armorLight);
            FillRect(texture, 8, 20, 2, 2, eyeColor);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 14, 19, 10, 6, armorColor);
            FillRect(texture, 21, 21, 4, 3, armorShadow);
            FillRect(texture, 16, 24, 7, 2, armorLight);
            FillRect(texture, 23, 20, 2, 2, eyeColor);
        }
        else
        {
            FillRect(texture, 11, 19, 11, 7, armorColor);
            FillRect(texture, 12, 25, 9, 2, armorLight);
            FillRect(texture, 11, 20, 2, 5, armorShadow);
            FillRect(texture, 20, 20, 2, 5, armorShadow);
            if (facing == Vector2.up)
            {
                FillRect(texture, 14, 21, 5, 4, armorDarkColor);
                FillRect(texture, 15, 24, 3, 1, energyLight);
            }
            else
            {
                FillRect(texture, 13, 20, 7, 2, armorDarkColor);
                FillRect(texture, 13, 21, 2, 2, eyeColor);
                FillRect(texture, 18, 21, 2, 2, eyeColor);
            }
        }
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
        behemothAttackPower = Mathf.Max(0, behemothAttackPower);
        behemothAttackDuration =
            Mathf.Max(0.05f, behemothAttackDuration);
        shockwaveSize.x = Mathf.Max(0.1f, shockwaveSize.x);
        shockwaveSize.y = Mathf.Max(0.1f, shockwaveSize.y);
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.12f, 0.08f, 0.9f);
        Gizmos.DrawWireCube(
            new Vector3(0f, 0.72f, 0f),
            new Vector3(2f, 1.75f, 0f));
        Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.45f);
        Gizmos.DrawWireSphere(Vector3.zero, shockwaveSize.x * 0.5f);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
