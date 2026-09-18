using UnityEngine;

public class StrongmanZeldaCharacterData : ZeldaCharacterData
{
    [SerializeField] private int attackPower = 2;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.65f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.85f, 0.75f);
    [SerializeField] private Color idleTint = new Color(1f, 0.92f, 0.82f, 1f);
    [SerializeField] private Color movingTint = Color.white;
    [SerializeField] private Color attackVisualTint = new Color(1f, 0.62f, 0.18f, 0.55f);

    private Sprite facingDownSprite;
    private Sprite facingUpSprite;
    private Sprite facingLeftSprite;
    private Sprite facingRightSprite;
    private Sprite attackingDownSprite;
    private Sprite attackingUpSprite;
    private Sprite attackingLeftSprite;
    private Sprite attackingRightSprite;

    public override bool CanAttack => !IsGhostForm;
    public override Color GhostFormEyeColor => new Color(0.05f, 0.05f, 0.04f, 1f);
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Hammer;
    public override Color AttackVisualTint => attackVisualTint;

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
            spriteRenderer.sprite = isAttacking ? attackingUpSprite : facingUpSprite;
        }
        else if (visualDirection == Vector2.left)
        {
            spriteRenderer.sprite = isAttacking ? attackingLeftSprite : facingLeftSprite;
        }
        else if (visualDirection == Vector2.right)
        {
            spriteRenderer.sprite = isAttacking ? attackingRightSprite : facingRightSprite;
        }
        else
        {
            spriteRenderer.sprite = isAttacking ? attackingDownSprite : facingDownSprite;
        }

        Color baseColor = GetMovementTint(isMoving);
        spriteRenderer.color = GetDamageFeedbackTint(baseColor);
    }

    protected Color GetMovementTint(bool isMoving) => isMoving ? movingTint : idleTint;

    private void CreateDirectionSprites()
    {
        if (facingDownSprite != null)
        {
            return;
        }

        facingDownSprite = CreateCharacterSprite(Vector2.down, false);
        facingUpSprite = CreateCharacterSprite(Vector2.up, false);
        facingLeftSprite = CreateCharacterSprite(Vector2.left, false);
        facingRightSprite = CreateCharacterSprite(Vector2.right, false);
        attackingDownSprite = CreateCharacterSprite(Vector2.down, true);
        attackingUpSprite = CreateCharacterSprite(Vector2.up, true);
        attackingLeftSprite = CreateCharacterSprite(Vector2.left, true);
        attackingRightSprite = CreateCharacterSprite(Vector2.right, true);
    }

    private Sprite CreateCharacterSprite(Vector2 facing, bool attacking)
    {
        var texture = CreateTexture(facing, attacking);
        // y=4 feet minus y=6 pivot retains the original world-space ground line.
        var sprite = Sprite.Create(texture, new Rect(0, 0, 20, 20), new Vector2(0.5f, 0.3f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    // Four-way compact silhouettes: head touches shoulders, no neck.
    // Two-pixel legs; profile hands sit in the middle of the torso.
    private static readonly string[] FrontBody = {
        "....................",
        ".......HHHHHH.......",
        "......HHHHHHHH......",
        "......HFFFFFFH......",
        "......FEFFFFEF......",
        "......SFFFFFFS......",
        "...FFFCCCCCCCCFFF...",
        "...FFFCCLLLLCCFFF...",
        "...SSSCLLLLLLCSSS...",
        "...GGGCCLLLLCCGGG...",
        "...GGGCCCLLCCCGGG...",
        "....DDDCCLLCCDDD....",
        ".....DDBBBBBBDD.....",
        "......DCCCCCCD......",
        "......KKK..KKK......",
        "......KKK..KKK......",
        "....................",
        "....................",
        "....................",
        "....................",
    };

    private static readonly string[] BackBody = {
        "....................",
        ".......HHHHHH.......",
        "......HHHHHHHH......",
        "......HHHHHHHH......",
        "......HHHHHHHH......",
        "......SSHHHHSS......",
        "...FFFCCCCCCCCFFF...",
        "...FFFCCDDDDCCFFF...",
        "...SSSCCCDDCCCSSS...",
        "...GGGCCCDDCCCGGG...",
        "...GGGCCCDDCCCGGG...",
        "....DDDCCDDCCDDD....",
        ".....DDBBBBBBDD.....",
        "......DCCCCCCD......",
        "......KKK..KKK......",
        "......KKK..KKK......",
        "....................",
        "....................",
        "....................",
        "....................",
    };

    private static readonly string[] LeftBody = {
        "....................",
        ".....HHHHHH.........",
        "....HHHHHHHH........",
        "....HFFFFHHH........",
        "....FEFFFHHH........",
        "....SFFFFHHS........",
        "......CCCCCCCD......",
        "......CCCSSSCD......",
        "......CCCFFFCD......",
        "......CCCGGGCD......",
        "......CCCGGGCD......",
        "......DCCCLCCD......",
        "......DBBBBBBD......",
        "......DCCCCCCD......",
        "......KKK..KKK......",
        "......KKK..KKK......",
        "....................",
        "....................",
        "....................",
        "....................",
    };

    private static readonly string[] RightBody = {
        "....................",
        ".........HHHHHH.....",
        "........HHHHHHHH....",
        "........HHHFFFFH....",
        "........HHHFFFEF....",
        "........SHHFFFFS....",
        "......DCCCCCCC......",
        "......DCSSSCCC......",
        "......DCFFFCCC......",
        "......DCGGGCCC......",
        "......DCGGGCCC......",
        "......DCCLCCCD......",
        "......DBBBBBBD......",
        "......DCCCCCCD......",
        "......KKK..KKK......",
        "......KKK..KKK......",
        "....................",
        "....................",
        "....................",
        "....................",
    };

    private Texture2D CreateTexture(Vector2 facing, bool attacking)
    {
        const int size = 20;
        string[] rows = facing == Vector2.up ? BackBody :
            facing == Vector2.left ? LeftBody : facing == Vector2.right ? RightBody : FrontBody;
        var pixels = new Color[size * size];
        for (int row = 0; row < size; row++)
        for (int x = 0; x < size; x++)
            pixels[(size - 1 - row) * size + x] = PixelColor(rows[row][x]);

        if (attacking)
        {
            RestoreBodyBehindAttackingArm(pixels, facing);
            int startX = facing == Vector2.left ? 2 : facing == Vector2.right ? 12 : 7;
            int startY = facing == Vector2.up ? 12 : facing == Vector2.down ? 5 : 9;
            Paint(pixels, startX, startY, 6, 3, 'F');
            int handX = facing == Vector2.left ? startX : facing == Vector2.right ? startX + 3 : startX + 1;
            int handY = facing == Vector2.up ? startY + 1 : startY;
            Paint(pixels, handX, handY, 3, 2, 'G');
        }

        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            name = "Strongman " + facing + (attacking ? " Attack" : " Idle"),
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
            int outerX = facing == Vector2.down ? 3 : 14;
            Paint(pixels, outerX, 9, 3, 5, '.');
            // Preserve a connected clothing edge behind the retracted arm.
            Paint(pixels, facing == Vector2.down ? 5 : 14, 9, 1, 4, 'D');
        }
        else
        {
            int handX = facing == Vector2.left ? 9 : 8;
            Paint(pixels, handX, 9, 3, 4, 'C');
        }
    }

    private void Paint(Color[] pixels, int x, int y, int width, int height, char symbol)
    {
        for (int py = y; py < y + height; py++)
        for (int px = x; px < x + width; px++)
            pixels[py * 20 + px] = PixelColor(symbol);
    }

    private Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'C': return new Color(1f, 0.42f, 0.05f, 1f);
            case 'D': return new Color(0.68f, 0.2f, 0.02f, 1f);
            case 'L': return new Color(1f, 0.53f, 0.16f, 1f);
            case 'B': return new Color(0.30f, 0.18f, 0.10f, 1f);
            case 'K': return new Color(0.16f, 0.09f, 0.05f, 1f);
            case 'F': return new Color(1f, 0.72f, 0.45f, 1f);
            case 'S': return new Color(0.82f, 0.48f, 0.28f, 1f);
            case 'G': return new Color(0.82f, 0.48f, 0.28f, 1f);
            case 'H': return new Color(0.18f, 0.09f, 0.03f, 1f);
            case 'E': return GhostFormEyeColor;
            default: return Color.clear;
        }
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

    private void OnDestroy()
    {
        ReleaseSprite(facingDownSprite); ReleaseSprite(facingUpSprite);
        ReleaseSprite(facingLeftSprite); ReleaseSprite(facingRightSprite);
        ReleaseSprite(attackingDownSprite); ReleaseSprite(attackingUpSprite);
        ReleaseSprite(attackingLeftSprite); ReleaseSprite(attackingRightSprite);
    }

    private void ReleaseSprite(Sprite sprite)
    {
        if (sprite == null) return;
        var texture = sprite.texture;
        if (Application.isPlaying) { Destroy(sprite); Destroy(texture); }
        else { DestroyImmediate(sprite); DestroyImmediate(texture); }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        attackPower = Mathf.Max(0, attackPower);
        attackDuration = Mathf.Max(0f, attackDuration);
        attackSize.x = Mathf.Max(0f, attackSize.x);
        attackSize.y = Mathf.Max(0f, attackSize.y);
    }
}
