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

    private bool visualsDirty = true;

    private readonly Sprite[] idleSprites = new Sprite[4];
    private readonly Sprite[] attackSprites = new Sprite[4];
    private readonly Texture2D[] idleTextures = new Texture2D[4];
    private readonly Texture2D[] attackTextures = new Texture2D[4];

    public override bool CanAttack => !IsGhostForm;
    public override Color GhostFormEyeColor => new Color(0.04f, 0.1f, 0.17f, 1f);
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
        if (visualsDirty)
        {
            // Defer texture/cache destruction until the next render, not OnValidate.
            var animator = GetComponent<PixelCharacterWalkAnimator>();
            if (animator != null) animator.InvalidateFrames();
            ReleaseSprites();
            visualsDirty = false;
        }
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

    // Full 16x16 four-way art. Row 4 rests directly on row 5: no neck.
    private static readonly string[] FrontBody = {
        "......JJHH......",
        ".....JHHHHH.....",
        ".....HFFFFH.....",
        ".....FEFFEF.....",
        ".....SFFFFS.....",
        "....WWTTTTWW....",
        "....CDTZCTDC....",
        "....TTTCLTTT....",
        "....FFTCLTFF....",
        "....FFTTTTFF....",
        "....DCTCCTCD....",
        "....DCTCCTCD....",
        "....DCTLCTCD....",
        "....TTTTTTTT....",
        ".....KK..KK.....",
        "................",
    };

    private static readonly string[] BackBody = {
        "......JJHH......",
        ".....JHHHHH.....",
        ".....HHHHHH.....",
        ".....HHHHHH.....",
        ".....SSHHSS.....",
        "....WWTTTTWW....",
        "....CDDDDDDC....",
        "....TTCDLCTT....",
        "....FFCDLCFF....",
        "....FFCDLCFF....",
        "....DCCDCCCD....",
        "....DCCDCCCD....",
        "....DCCDLCCD....",
        "....TTTTTTTT....",
        ".....KK..KK.....",
        "................",
    };

    private static readonly string[] LeftBody = {
        ".....JJHH.......",
        "....JHHHHH......",
        "....HFFFHH......",
        "....FEFFHH......",
        "....SFFFHS......",
        ".....WWTTWW.....",
        ".....TCCCDD.....",
        ".....TCTTDD.....",
        ".....TCFFDD.....",
        ".....TTFFTT.....",
        "....DTCCCCDD....",
        "....DTCCCCDD....",
        "....DTCLCCDD....",
        "....TTTTTTTT....",
        ".....KK..KK.....",
        "................",
    };

    private static readonly string[] RightBody = {
        ".......HHJJ.....",
        "......HHHHHJ....",
        "......HHFFFH....",
        "......HHFFEF....",
        "......SHFFFS....",
        ".....WWTTWW.....",
        ".....DDCCCT.....",
        ".....DDTTCT.....",
        ".....DDFFCT.....",
        ".....TTFFTT.....",
        "....DDCCCCTD....",
        "....DDCCCCTD....",
        "....DDCCLCTD....",
        "....TTTTTTTT....",
        ".....KK..KK.....",
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

        if (showCrown) DrawCrown(pixels, facing);

        if (attacking)
        {
            RestoreBodyBehindAttackingArm(pixels, facing);
            int startX = facing == Vector2.left ? 2 : facing == Vector2.right ? 10 : 6;
            int startY = facing == Vector2.up ? 10 : facing == Vector2.down ? 4 : 6;
            for (int y = startY; y < startY + 3; y++)
            for (int x = startX; x < startX + 4; x++)
                pixels[y * 16 + x] = PixelColor(y == startY + 2 ? 'T' : 'C');
            int handX = facing == Vector2.left ? startX :
                facing == Vector2.right ? startX + 2 : startX + 1;
            int handY = facing == Vector2.up ? startY + 1 : startY;
            for (int y = handY; y < handY + 2; y++)
            for (int x = handX; x < handX + 2; x++)
                pixels[y * 16 + x] = PixelColor('F');
        }

        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            name = "Noble " + facing + (attacking ? " Attack" : " Idle"),
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
                pixels[y * 16 + innerX] = PixelColor(y == 6 && facing == Vector2.down ? 'T' : 'D');
            }
        }
        else
        {
            // Replace the central idle hand with the clothing it was covering.
            for (int y = 6; y <= 8; y++)
            for (int x = 7; x <= 8; x++)
                pixels[y * 16 + x] = PixelColor(y == 6 ? 'T' : 'C');
        }
    }

    private Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'C': return robeColor;
            case 'D': return Color.Lerp(robeColor, Color.black, 0.34f);
            case 'L': return Color.Lerp(robeColor, Color.white, 0.18f);
            case 'T': return robeTrimColor;
            case 'W': return showCrown ? new Color(0.80f, 0.77f, 0.66f, 1f) : Color.Lerp(robeTrimColor, Color.white, 0.12f);
            case 'Z': return crownJewelColor;
            case 'K': return new Color(0.19f, 0.13f, 0.10f, 1f);
            case 'F': return skinColor;
            case 'S': return Color.Lerp(skinColor, Color.black, 0.24f);
            case 'H': return hairColor;
            case 'J': return Color.Lerp(hairColor, Color.white, 0.14f);
            case 'E': return GhostFormEyeColor;
            default: return Color.clear;
        }
    }

    private void DrawCrown(Color[] pixels, Vector2 facing)
    {
        // Three points and a low band fit inside the same canvas as the hair.
        int left = facing == Vector2.left ? 4 : facing == Vector2.right ? 6 : 5;
        for (int x = left; x < left + 6; x++)
        {
            pixels[15 * 16 + x] = Color.clear;
            pixels[14 * 16 + x] = crownColor;
        }
        pixels[15 * 16 + left] = crownColor;
        pixels[15 * 16 + left + 2] = crownColor;
        pixels[15 * 16 + left + 5] = crownColor;
        pixels[14 * 16 + left + 2] = crownJewelColor;
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
            ReleaseGenerated(idleSprites[i]);
            ReleaseGenerated(idleTextures[i]);
            ReleaseGenerated(attackSprites[i]);
            ReleaseGenerated(attackTextures[i]);
            idleSprites[i] = null;
            idleTextures[i] = null;
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
        Gizmos.color = new Color(0.42f, 0.62f, 1f, 0.9f);
        Gizmos.DrawWireCube(
            transform.position + new Vector3(0f, 0.27f, 0f),
            new Vector3(0.72f, 0.84f, 0f));
        Gizmos.color = previousColor;
    }
}
