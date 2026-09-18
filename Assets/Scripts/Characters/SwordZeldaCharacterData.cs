using UnityEngine;

public class SwordZeldaCharacterData : ZeldaCharacterData
{
    private static readonly Vector3 EditorVisualBoundsCenter = new Vector3(0f, 0.25f, 0f);
    private static readonly Vector3 EditorVisualBoundsSize = new Vector3(1f, 1f, 0f);
    [SerializeField] private int attackPower = 1;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.25f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.8f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.55f, 0.9f);
    [SerializeField] private Color idleTint = new Color(0.86f, 1f, 0.86f, 1f);
    [SerializeField] private Color movingTint = Color.white;
    [SerializeField] private Color attackVisualTint = new Color(1f, 0.85f, 0.2f, 0.65f);
    [SerializeField] private bool useYellowArmorHighlights;
    [SerializeField] private bool useMagentaArmorHighlights;

    private Sprite facingDownSprite;
    private Sprite facingUpSprite;
    private Sprite facingLeftSprite;
    private Sprite facingRightSprite;
    private Sprite attackingDownSprite;
    private Sprite attackingUpSprite;
    private Sprite attackingLeftSprite;
    private Sprite attackingRightSprite;

    public override bool CanAttack => !IsGhostForm;
    public override Color GhostFormEyeColor => new Color(0.04f, 0.18f, 0.48f, 1f);
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Sword;
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

        Color baseColor = isMoving ? movingTint : idleTint;
        spriteRenderer.color = GetDamageFeedbackTint(baseColor);
    }

    private void CreateDirectionSprites()
    {
        if (facingDownSprite != null)
        {
            return;
        }

        facingDownSprite = CreateCharacterSprite(Vector2.down);
        facingUpSprite = CreateCharacterSprite(Vector2.up);
        facingLeftSprite = CreateCharacterSprite(Vector2.left);
        facingRightSprite = CreateCharacterSprite(Vector2.right);
        attackingDownSprite = CreateCharacterSprite(Vector2.down, true);
        attackingUpSprite = CreateCharacterSprite(Vector2.up, true);
        attackingLeftSprite = CreateCharacterSprite(Vector2.left, true);
        attackingRightSprite = CreateCharacterSprite(Vector2.right, true);
    }

    // One-pixel shorter boots; a higher texture pivot retains the world-space ground baseline.
    // Full 16x16 silhouettes: the helmet ends at row 4 and rests directly on
    // row 5's shoulders. There is deliberately no neck or narrow skin bridge.
    // Short legs end at y=3. The animator lifts a foot within y=3..4.
    private static readonly string[] FrontBody = {
        "......HHHH......",
        ".....AAAAAA.....",
        ".....SAAAAS.....",
        ".....AFEFEA.....",
        ".....AFFFFA.....",
        "....LHAAAAHL....",
        "....ASQQPPSA....",
        "....HHPPPPHH....",
        "....GGAPPAGG....",
        "....GGBQQBGG....",
        ".....AATTAA.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private static readonly string[] BackBody = {
        "......HHHH......",
        ".....AAAAAA.....",
        ".....SAHHAS.....",
        ".....SAAAAS.....",
        ".....SSSSSS.....",
        "....LHAAAAHL....",
        "....ASTDDTSA....",
        "....HHTDDTHH....",
        "....GGTDDTGG....",
        "....GGBBBBGG....",
        ".....TDTTDT.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private static readonly string[] LeftBody = {
        ".....HHHH.......",
        "....HAAAAS......",
        "....AAAASS......",
        "....FEAAAS......",
        "....FFAAAS......",
        ".....LHAAHL.....",
        ".....ATTTAS.....",
        ".....ATHHAS.....",
        ".....ATGGAS.....",
        ".....HBGGBB.....",
        ".....TTDDAS.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private static readonly string[] RightBody = {
        ".......HHHH.....",
        "......SAAAAH....",
        "......SSAAAA....",
        "......SAAAEF....",
        "......SAAAFF....",
        ".....LHAAHL.....",
        ".....SATTTA.....",
        ".....SAHHTA.....",
        ".....SAGGTA.....",
        ".....BBGGBH.....",
        ".....SADDTT.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private Sprite CreateCharacterSprite(Vector2 facing, bool attacking = false)
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
                pixels[y * 16 + x] = PixelColor(y == startY + 2 ? 'H' : 'A');
            int handX = facing == Vector2.left ? startX :
                facing == Vector2.right ? startX + 2 : startX + 1;
            int handY = facing == Vector2.up ? startY + 1 : startY;
            for (int y = handY; y < handY + 2; y++)
            for (int x = handX; x < handX + 2; x++)
                pixels[y * 16 + x] = PixelColor('G');
        }

        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            name = "SwordGuy " + facing + (attacking ? " Attack" : " Idle"),
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply(); // Keep idle pixels readable for PixelCharacterWalkAnimator.
        var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.3125f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
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
                pixels[y * 16 + innerX] = PixelColor(y == 6 ? 'B' : 'T');
            }
        }
        else
        {
            // The near arm lies over the centre of the torso in both profiles.
            // Restore the underlying armor/belt before extending that arm.
            for (int y = 6; y <= 8; y++)
            for (int x = 7; x <= 8; x++)
                pixels[y * 16 + x] = PixelColor(y == 6 ? 'B' : 'T');
        }
    }

    private Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'T': return new Color(0.36f, 0.38f, 0.42f, 1f);
            case 'D': return new Color(0.20f, 0.22f, 0.26f, 1f);
            // Neutral steel breastplate/buckle, independent of rank-colored trim.
            case 'P': return new Color(0.48f, 0.53f, 0.56f, 1f);
            case 'Q': return new Color(0.62f, 0.67f, 0.69f, 1f);
            case 'L': return GetArmorHighlightColor(new Color(0.52f, 0.54f, 0.57f, 1f));
            case 'S': return new Color(0.16f, 0.20f, 0.24f, 1f);
            case 'A': return new Color(0.38f, 0.44f, 0.48f, 1f);
            case 'H': return GetArmorHighlightColor(new Color(0.62f, 0.68f, 0.70f, 1f));
            case 'F': return new Color(1f, 0.78f, 0.48f, 1f);
            case 'G': return new Color(0.60f, 0.64f, 0.64f, 1f);
            case 'K': return new Color(0.24f, 0.13f, 0.05f, 1f);
            case 'B': return new Color(0.26f, 0.19f, 0.12f, 1f);
            case 'E': return new Color(0.04f, 0.18f, 0.48f, 1f);
            default: return Color.clear;
        }
    }

    private Color GetArmorHighlightColor(Color normalColor)
    {
        if (useMagentaArmorHighlights)
        {
            return new Color(0.95f, 0.12f, 0.72f, 1f);
        }

        return useYellowArmorHighlights
            ? new Color(0.95f, 0.75f, 0.12f, 1f)
            : normalColor;
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
        ReleaseSprite(facingDownSprite);
        ReleaseSprite(facingUpSprite);
        ReleaseSprite(facingLeftSprite);
        ReleaseSprite(facingRightSprite);
        ReleaseSprite(attackingDownSprite);
        ReleaseSprite(attackingUpSprite);
        ReleaseSprite(attackingLeftSprite);
        ReleaseSprite(attackingRightSprite);
    }

    private void ReleaseSprite(Sprite sprite)
    {
        if (sprite == null) return;
        Texture2D texture = sprite.texture;
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

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(EditorVisualBoundsCenter, EditorVisualBoundsSize);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
