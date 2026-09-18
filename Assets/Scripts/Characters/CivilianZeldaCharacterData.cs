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
    // Kept for serialized compatibility; the full lower body now uses the boot color.
    [SerializeField, HideInInspector] private Color trousersColor = new Color(0.16f, 0.2f, 0.3f, 1f);
    [SerializeField] private Color skinColor = new Color(0.84f, 0.62f, 0.43f, 1f);
    [SerializeField] private Color hairColor = new Color(0.16f, 0.11f, 0.08f, 1f);
    [SerializeField] private Color attackVisualTint = new Color(0.85f, 0.72f, 0.56f, 0.8f);

    private bool visualsDirty = true;

    private readonly Sprite[] sprites = new Sprite[4];
    private readonly Texture2D[] textures = new Texture2D[4];
    private readonly Sprite[] attackSprites = new Sprite[4];
    private readonly Texture2D[] attackTextures = new Texture2D[4];

    public override bool CanAttack => !IsGhostForm;
    public override Color GhostFormEyeColor => new Color(0.05f, 0.12f, 0.2f, 1f);
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
        if (visualsDirty)
        {
            // Defer texture/cache destruction until the next render, not OnValidate.
            var animator = GetComponent<PixelCharacterWalkAnimator>();
            if (animator != null) animator.InvalidateFrames();
            ReleaseSprites();
            visualsDirty = false;
        }
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
                new Vector2(0.5f, 0.3125f),
                16f);
            sprites[i].name = "Civilian " + i;

            attackTextures[i] = CreateTexture(directions[i], true);
            attackSprites[i] = Sprite.Create(
                attackTextures[i],
                new Rect(0f, 0f, 16f, 16f),
                new Vector2(0.5f, 0.3125f),
                16f);
            attackSprites[i].name = "Civilian Attack " + i;
        }
    }

    // One-pixel shorter boots; a higher texture pivot retains the world-space ground baseline.
    // Full 16x16 four-way art. Row 4 rests directly on row 5: no neck.
    private static readonly string[] FrontBody = {
        "......HHHH......",
        ".....HHHHHH.....",
        ".....HFFFFH.....",
        ".....FEFFEF.....",
        ".....SFFFFS.....",
        "....CCCIICCC....",
        "....CDLCCLDC....",
        "....LLCCCCLL....",
        "....FFCCCCFF....",
        "....FFBBBBFF....",
        ".....KKKKKK.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private static readonly string[] BackBody = {
        "......HHHH......",
        ".....HHHHHH.....",
        ".....HHHHHH.....",
        ".....HHHHHH.....",
        ".....SSHHSS.....",
        "....CCCCCCCC....",
        "....CDCCCCDC....",
        "....LLCDCCLL....",
        "....FFCDCCFF....",
        "....FFBBBBFF....",
        ".....KKKKKK.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private static readonly string[] LeftBody = {
        ".....HHHH.......",
        "....HHHHHH......",
        "....HFFFHH......",
        "....FEFFHH......",
        "....SFFFHS......",
        ".....CICCCC.....",
        ".....CDLCCC.....",
        ".....CCLLCC.....",
        ".....CCFFCC.....",
        ".....BBFFBB.....",
        ".....KKKKKK.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private static readonly string[] RightBody = {
        ".......HHHH.....",
        "......HHHHHH....",
        "......HHFFFH....",
        "......HHFFEF....",
        "......SHFFFS....",
        ".....CCCCIC.....",
        ".....CCCLDC.....",
        ".....CCLLCC.....",
        ".....CCFFCC.....",
        ".....BBFFBB.....",
        ".....KKKKKK.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private Texture2D CreateTexture(Vector2 facing, bool attacking)
    {
        string[] rows = facing == Vector2.up ? BackBody :
            facing == Vector2.left ? LeftBody : facing == Vector2.right ? RightBody : FrontBody;
        var pixels = new Color[16 * 16];
        for (int row = 0; row < 16; row++)
        for (int x = 0; x < 16; x++)
            pixels[(15 - row) * 16 + x] = PixelColor(rows[row][x]);

        if (attacking)
        {
            RestoreBodyBehindAttackingArm(pixels, facing);
            int startX = facing == Vector2.left ? 2 : facing == Vector2.right ? 10 : 6;
            int startY = facing == Vector2.up ? 10 : facing == Vector2.down ? 4 : 6;
            for (int y = startY; y < startY + 3; y++)
            for (int x = startX; x < startX + 4; x++)
                pixels[y * 16 + x] = PixelColor(y == startY + 2 ? 'L' : 'C');
            int handX = facing == Vector2.left ? startX :
                facing == Vector2.right ? startX + 2 : startX + 1;
            int handY = facing == Vector2.up ? startY + 1 : startY;
            for (int y = handY; y < handY + 2; y++)
            for (int x = handX; x < handX + 2; x++)
                pixels[y * 16 + x] = PixelColor('F');
        }

        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            name = "Civilian " + facing + (attacking ? " Attack" : " Idle"),
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        // Idle pixels remain readable for the shared cached walking frames.
        texture.Apply(false, attacking);
        return texture;
    }

    private void RestoreBodyBehindAttackingArm(Color[] pixels, Vector2 facing)
    {
        if (facing == Vector2.down || facing == Vector2.up)
        {
            int outerX = facing == Vector2.down ? 4 : 11;
            int innerX = facing == Vector2.down ? 5 : 10;
            for (int y = 6; y <= 8; y++)
            {
                pixels[y * 16 + outerX] = Color.clear;
                pixels[y * 16 + innerX] = PixelColor(y == 6 ? 'B' : 'D');
            }
        }
        else
        {
            // Replace the central idle hand with the clothing it was covering.
            for (int y = 6; y <= 8; y++)
            for (int x = 7; x <= 8; x++)
                pixels[y * 16 + x] = PixelColor(y == 6 ? 'B' : 'C');
        }
    }

    private Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'C': return clothingColor;
            case 'D': return Color.Lerp(clothingColor, Color.black, 0.28f);
            case 'L': return Color.Lerp(clothingColor, Color.white, 0.2f);
            case 'I': return new Color(0.76f, 0.73f, 0.62f, 1f);
            case 'B': return new Color(0.27f, 0.20f, 0.14f, 1f);
            case 'K': return new Color(0.20f, 0.15f, 0.12f, 1f);
            case 'F': return skinColor;
            case 'S': return Color.Lerp(skinColor, Color.black, 0.25f);
            case 'H': return hairColor;
            case 'E': return GhostFormEyeColor;
            default: return Color.clear;
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

    private void OnDestroy() => ReleaseSprites();

    private void ReleaseSprites()
    {
        for (int i = 0; i < 4; i++)
        {
            ReleaseGenerated(sprites[i]);
            ReleaseGenerated(textures[i]);
            ReleaseGenerated(attackSprites[i]);
            ReleaseGenerated(attackTextures[i]);
            sprites[i] = null;
            textures[i] = null;
            attackSprites[i] = null;
            attackTextures[i] = null;
        }
    }

    private void ReleaseGenerated(Object generated)
    {
        if (generated == null) return;
        if (Application.isPlaying) Destroy(generated);
        else DestroyImmediate(generated);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        visualsDirty = true;
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
