using UnityEngine;

/// <summary>A configurable-clothing civilian that uses the standard character systems.</summary>
public sealed class CivilianZeldaCharacterData : ZeldaCharacterData
{
    [Header("Civilian Combat")]
    [SerializeField, Min(0)] private int attackPower = 1;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField, Min(0f)] private float attackDuration = 0.2f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.48f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.42f, 0.36f);

    [Header("Configurable Clothing")]
    [SerializeField] private Color clothingColor = new Color(0.3f, 0.62f, 0.78f, 1f);
    [SerializeField] private Color trousersColor = new Color(0.16f, 0.2f, 0.3f, 1f);
    [SerializeField] private Color skinColor = new Color(0.84f, 0.62f, 0.43f, 1f);
    [SerializeField] private Color hairColor = new Color(0.16f, 0.11f, 0.08f, 1f);
    [SerializeField] private Color attackVisualTint = new Color(0.85f, 0.72f, 0.56f, 0.8f);

    private readonly Sprite[] sprites = new Sprite[4];
    private readonly Texture2D[] textures = new Texture2D[4];
    private readonly Sprite[] attackSprites = new Sprite[4];
    private readonly Texture2D[] attackTextures = new Texture2D[4];

    public override bool CanAttack => true;
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Rock;
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
            : sprites[directionIndex];
        Color movementTint = isMoving
            ? Color.white
            : new Color(0.94f, 0.97f, 1f, 1f);
        spriteRenderer.color = GetDamageFeedbackTint(movementTint);
    }

    private void EnsureSprites()
    {
        if (sprites[0] != null)
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
            textures[i] = CreateTexture(directions[i], false);
            sprites[i] = Sprite.Create(
                textures[i],
                new Rect(0f, 0f, 16f, 16f),
                new Vector2(0.5f, 0.25f),
                16f);
            sprites[i].name = "Civilian " + i;

            attackTextures[i] = CreateTexture(directions[i], true);
            attackSprites[i] = Sprite.Create(
                attackTextures[i],
                new Rect(0f, 0f, 16f, 16f),
                new Vector2(0.5f, 0.25f),
                16f);
            attackSprites[i].name = "Civilian Attack " + i;
        }
    }

    private Texture2D CreateTexture(Vector2 facing, bool attacking)
    {
        const int size = 16;
        Texture2D texture =
            new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.name = "Runtime Civilian";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        FillRect(texture, 0, 0, size, size, Color.clear);

        Color clothingShadow = Color.Lerp(clothingColor, Color.black, 0.28f);
        Color clothingLight = Color.Lerp(clothingColor, Color.white, 0.2f);
        Color trousersLight = Color.Lerp(trousersColor, Color.white, 0.12f);
        Color skinShadow = Color.Lerp(skinColor, Color.black, 0.25f);
        Color eye = new Color(0.05f, 0.12f, 0.2f, 1f);

        // Narrow shoulders, torso and tucked-in arms distinguish the civilian
        // from the broader prisoner and guard silhouettes.
        FillRect(texture, 6, 6, 5, 5, clothingColor);
        FillRect(texture, 6, 6, 1, 5, clothingShadow);
        FillRect(texture, 8, 8, 2, 2, clothingLight);
        DrawArms(
            texture,
            facing,
            attacking,
            clothingShadow,
            skinShadow);

        FillRect(texture, 6, 3, 2, 3, trousersColor);
        FillRect(texture, 9, 3, 2, 3, trousersColor);
        texture.SetPixel(7, 4, trousersLight);
        texture.SetPixel(10, 4, trousersLight);

        DrawHead(texture, facing, skinShadow, eye);
        texture.Apply(false, true);
        return texture;
    }

    private void DrawArms(
        Texture2D texture,
        Vector2 facing,
        bool attacking,
        Color clothingShadow,
        Color skinShadow)
    {
        if (!attacking)
        {
            FillRect(texture, 5, 7, 1, 3, clothingShadow);
            FillRect(texture, 11, 7, 1, 3, clothingColor);
            texture.SetPixel(5, 7, skinShadow);
            texture.SetPixel(11, 7, skinColor);
            return;
        }

        if (facing == Vector2.left)
        {
            FillRect(texture, 2, 7, 4, 2, clothingColor);
            FillRect(texture, 1, 7, 2, 2, skinColor);
            FillRect(texture, 11, 7, 1, 3, clothingShadow);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 11, 7, 4, 2, clothingColor);
            FillRect(texture, 14, 7, 2, 2, skinColor);
            FillRect(texture, 5, 7, 1, 3, clothingShadow);
        }
        else if (facing == Vector2.up)
        {
            FillRect(texture, 6, 10, 1, 4, clothingShadow);
            FillRect(texture, 10, 10, 1, 4, clothingColor);
            texture.SetPixel(6, 14, skinShadow);
            texture.SetPixel(10, 14, skinColor);
        }
        else
        {
            FillRect(texture, 6, 4, 1, 5, clothingShadow);
            FillRect(texture, 10, 4, 1, 5, clothingColor);
            texture.SetPixel(6, 3, skinShadow);
            texture.SetPixel(10, 3, skinColor);
        }
    }

    private void DrawHead(
        Texture2D texture,
        Vector2 facing,
        Color skinShadow,
        Color eye)
    {
        if (facing == Vector2.left)
        {
            FillRect(texture, 5, 10, 5, 4, skinColor);
            FillRect(texture, 6, 13, 4, 1, hairColor);
            FillRect(texture, 9, 11, 1, 3, hairColor);
            texture.SetPixel(5, 11, eye);
            texture.SetPixel(5, 10, skinShadow);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 7, 10, 5, 4, skinColor);
            FillRect(texture, 7, 13, 4, 1, hairColor);
            FillRect(texture, 7, 11, 1, 3, hairColor);
            texture.SetPixel(11, 11, eye);
            texture.SetPixel(11, 10, skinShadow);
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
                texture.SetPixel(7, 11, eye);
                texture.SetPixel(9, 11, eye);
                texture.SetPixel(8, 10, skinShadow);
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
                texture.SetPixel(px, py, color);
            }
        }
    }

    private void OnDestroy()
    {
        for (int i = 0; i < sprites.Length; i++)
        {
            if (sprites[i] != null) Destroy(sprites[i]);
            if (textures[i] != null) Destroy(textures[i]);
            if (attackSprites[i] != null) Destroy(attackSprites[i]);
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
        Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.85f);
        Gizmos.DrawWireCube(
            transform.position + new Vector3(0f, 0.28f, 0f),
            new Vector3(0.65f, 0.75f, 0f));
        Gizmos.color = previousColor;
    }
}
