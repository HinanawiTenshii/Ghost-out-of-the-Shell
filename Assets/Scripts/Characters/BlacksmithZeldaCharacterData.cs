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

    private bool visualsDirty = true;

    private readonly Sprite[] idleSprites = new Sprite[4];
    private readonly Sprite[] attackSprites = new Sprite[4];
    private readonly Texture2D[] idleTextures = new Texture2D[4];
    private readonly Texture2D[] attackTextures = new Texture2D[4];

    public override bool CanAttack => !IsGhostForm;
    public override Color GhostFormEyeColor => new Color(0.04f, 0.1f, 0.16f, 1f);
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
        if (visualsDirty)
        {
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

    // Four-way compact silhouettes: head touches shoulders, no neck.
    // Two-pixel legs; profile hands sit in the middle of the torso.
    private static readonly string[] FrontBody = {
        "......HHHH......",
        ".....HHHHHH.....",
        ".....HFFFFH.....",
        ".....FEFFEF.....",
        ".....SHHHHS.....",
        "....CCAAAACC....",
        "....CDALLADC....",
        "....UUAAAAUU....",
        "....GGADLAGG....",
        "....GGBBBBGG....",
        ".....DAAAAD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
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
        "....CCACCACC....",
        "....CDCAACDC....",
        "....UUCAACUU....",
        "....GGCCCCGG....",
        "....GGBBBBGG....",
        ".....DCCCCD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................",
    };

    private static readonly string[] LeftBody = {
        ".....HHHH.......",
        "....HHHHHH......",
        "....HFFFHH......",
        "....FEFFHH......",
        "....SHHHHS......",
        ".....AACCCC.....",
        ".....ALCCCD.....",
        ".....AAUUCD.....",
        ".....AAGGCD.....",
        ".....BBGGBB.....",
        ".....AACCCD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................",
    };

    private static readonly string[] RightBody = {
        ".......HHHH.....",
        "......HHHHHH....",
        "......HHFFFH....",
        "......HHFFEF....",
        "......SHHHHS....",
        ".....CCCCAA.....",
        ".....DCCCLA.....",
        ".....DCUUAA.....",
        ".....DCGGAA.....",
        ".....BBGGBB.....",
        ".....DCCCAA.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................",
    };

    private Texture2D CreateTexture(Vector2 facing, bool attacking)
    {
        const int size = 16;
        string[] rows = facing == Vector2.up ? BackBody :
            facing == Vector2.left ? LeftBody : facing == Vector2.right ? RightBody : FrontBody;
        var pixels = new Color[size * size];
        for (int row = 0; row < size; row++)
        for (int x = 0; x < size; x++)
            pixels[(size - 1 - row) * size + x] = PixelColor(rows[row][x]);

        if (attacking)
        {
            RestoreBodyBehindAttackingArm(pixels, facing);
            int startX = facing == Vector2.left ? 2 : facing == Vector2.right ? 10 : 6;
            int startY = facing == Vector2.up ? 10 : facing == Vector2.down ? 4 : 6;
            Paint(pixels, startX, startY, 4, 3, 'C');
            int handX = facing == Vector2.left ? startX : facing == Vector2.right ? startX + 2 : startX + 1;
            int handY = facing == Vector2.up ? startY + 1 : startY;
            Paint(pixels, handX, handY, 2, 2, 'G');
        }
        DrawHammer(pixels, facing, attacking);

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Blacksmith " + facing + (attacking ? " Attack" : " Idle"),
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply(false, attacking);
        return texture;
    }

    private void RestoreBodyBehindAttackingArm(Color[] pixels, Vector2 facing)
    {
        if (facing == Vector2.down || facing == Vector2.up)
        {
            // Hammer-bearing arm is on the right in front, left from the back.
            int outerX = facing == Vector2.down ? 11 : 4;
            int innerX = facing == Vector2.down ? 10 : 5;
            for (int y = 6; y <= 8; y++)
            {
                pixels[y * 16 + outerX] = Color.clear;
                pixels[y * 16 + innerX] = PixelColor(y == 6 ? 'B' : 'D');
            }
        }
        else
        {
            for (int y = 6; y <= 8; y++)
            for (int x = 7; x <= 8; x++)
                pixels[y * 16 + x] = PixelColor(y == 6 ? 'B' : 'C');
        }
    }

    private void Paint(Color[] pixels, int x, int y, int width, int height, char symbol)
    {
        for (int py = y; py < y + height; py++)
        for (int px = x; px < x + width; px++)
            pixels[py * 16 + px] = PixelColor(symbol);
    }

    private Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'C': return shirtColor;
            case 'D': return Color.Lerp(shirtColor, Color.black, 0.32f);
            case 'U': return Color.Lerp(shirtColor, Color.white, 0.16f);
            case 'A': return apronColor;
            case 'L': return Color.Lerp(apronColor, Color.white, 0.18f);
            case 'B': return Color.Lerp(apronColor, Color.black, 0.3f);
            case 'G': return apronColor;
            case 'P': return trousersColor;
            case 'F': return skinColor;
            case 'S': return Color.Lerp(skinColor, Color.black, 0.24f);
            case 'H': return hairColor;
            case 'E': return GhostFormEyeColor;
            case 'R': return new Color(0.29f, 0.16f, 0.08f, 1f);
            case 'M': return hammerColor;
            case 'N': return Color.Lerp(hammerColor, Color.white, 0.22f);
            default: return Color.clear;
        }
    }

    private void DrawHammer(Color[] pixels, Vector2 facing, bool attacking)
    {
        if (attacking)
        {
            if (facing == Vector2.left || facing == Vector2.right)
            {
                bool left = facing == Vector2.left;
                Paint(pixels, left ? 1 : 10, 8, 5, 1, 'R');
                Paint(pixels, left ? 0 : 14, 6, 2, 4, 'M');
                Paint(pixels, left ? 0 : 15, 9, 1, 1, 'N');
            }
            else
            {
                bool up = facing == Vector2.up;
                Paint(pixels, 7, up ? 10 : 3, 1, 5, 'R');
                Paint(pixels, 6, up ? 14 : 2, 4, 2, 'M');
                Paint(pixels, 6, up ? 15 : 3, 3, 1, 'N');
            }
            return;
        }

        // The handle touches the glove, not a floating decoration at a fixed side.
        int handleX = facing == Vector2.down ? 12 : facing == Vector2.up ? 3 :
            facing == Vector2.left ? 6 : 9;
        int headY = facing == Vector2.down || facing == Vector2.up ? 9 : 8;
        Paint(pixels, handleX, 5, 1, headY - 4, 'R');
        Paint(pixels, handleX - 1, headY, 3, 2, 'M');
        Paint(pixels, handleX, headY + 1, 1, 1, 'N');
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
            ReleaseGenerated(idleSprites[i]); ReleaseGenerated(idleTextures[i]);
            ReleaseGenerated(attackSprites[i]); ReleaseGenerated(attackTextures[i]);
            idleSprites[i] = null; idleTextures[i] = null;
            attackSprites[i] = null; attackTextures[i] = null;
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
        Gizmos.color = new Color(1f, 0.55f, 0.2f, 0.9f);
        Gizmos.DrawWireCube(
            transform.position + new Vector3(0f, 0.28f, 0f),
            new Vector3(0.72f, 0.8f, 0f));
        Gizmos.color = previousColor;
    }
}
