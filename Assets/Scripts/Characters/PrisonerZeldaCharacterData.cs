using UnityEngine;

/// <summary>A smaller unhelmeted prisoner character with a temporary box attack visual.</summary>
public sealed class PrisonerZeldaCharacterData : ZeldaCharacterData
{
    private static readonly Vector3 EditorVisualBoundsCenter = new Vector3(0f, 0.28f, 0f);
    private static readonly Vector3 EditorVisualBoundsSize = new Vector3(0.75f, 0.75f, 0f);

    [SerializeField] private int attackPower = 1;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.22f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.5f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.48f, 0.48f);
    [SerializeField] private Color idleTint = new Color(0.94f, 0.96f, 1f, 1f);
    [SerializeField] private Color movingTint = Color.white;
    [SerializeField] private Color attackVisualTint = new Color(0.55f, 0.52f, 0.45f, 0.85f);

    private readonly Sprite[] IdleSprites = new Sprite[4];
    private readonly Sprite[] AttackSprites = new Sprite[4];

    public override bool CanAttack => !IsGhostForm;
    public override Color GhostFormEyeColor => new Color(0.05f, 0.12f, 0.22f, 1f);
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Rock;
    public override Color AttackVisualTint => attackVisualTint;

    public override void ApplyCharacterVisual(SpriteRenderer spriteRenderer, Vector2 facingDirection, bool isMoving, bool isAttacking)
    {
        if (spriteRenderer == null) return;
        EnsureSprites();
        int direction = DirectionIndex(facingDirection);
        spriteRenderer.sprite = isAttacking ? AttackSprites[direction] : IdleSprites[direction];
        spriteRenderer.color = GetDamageFeedbackTint(isMoving ? movingTint : idleTint);
    }

    private void EnsureSprites()
    {
        if (IdleSprites[0] != null) return;
        Vector2[] directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
        for (int i = 0; i < directions.Length; i++)
        {
            IdleSprites[i] = CreateSprite(directions[i], false);
            AttackSprites[i] = CreateSprite(directions[i], true);
        }
    }

    private Sprite CreateSprite(Vector2 facing, bool attacking)
    {
        Texture2D texture = CreateTexture(facing, attacking);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, 16f, 16f), new Vector2(0.5f, 0.3125f), 16f);
        sprite.name = "Prisoner " + DirectionName(facing) + (attacking ? " Attack" : " Idle");
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    // One-pixel shorter boots; a higher texture pivot retains the world-space ground baseline.
    // Full 16x16 four-way art. Row 4 rests directly on row 5: no neck.
    private static readonly string[] FrontBody = {
        "......HHHH......",
        ".....HFFFFH.....",
        ".....FFFFFF.....",
        ".....FEFFEF.....",
        ".....SSFFSS.....",
        "....CCDDCCCC....",
        "....CDLCCCDC....",
        "....LLCCCPLL....",
        "....FFCCPPFF....",
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
        ".....SHHHHS.....",
        ".....SFFFFS.....",
        ".....SSFFSS.....",
        "....CCCCCCCC....",
        "....CDCCCCDC....",
        "....LLCDCCLL....",
        "....FFCDPCFF....",
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
        "....HFFFFH......",
        "....FFFFFH......",
        "....FEFFFS......",
        "....SFFFSS......",
        ".....CDCCCC.....",
        ".....CDLCCC.....",
        ".....CCLLPC.....",
        ".....CCFFPC.....",
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
        "......HFFFFH....",
        "......HFFFFF....",
        "......SFFFEF....",
        "......SSFFFS....",
        ".....CCCCDC.....",
        ".....CCCLDC.....",
        ".....CPLLCC.....",
        ".....CPFFCC.....",
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
            name = "Prisoner " + facing + (attacking ? " Attack" : " Idle"),
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
            case 'C': return new Color(0.68f, 0.51f, 0.32f, 1f);
            case 'D': return new Color(0.44f, 0.31f, 0.19f, 1f);
            case 'L': return new Color(0.82f, 0.66f, 0.43f, 1f);
            case 'P': return new Color(0.44f, 0.42f, 0.33f, 1f);
            case 'T': return new Color(0.07f, 0.25f, 0.13f, 1f);
            case 'B': return new Color(0.49f, 0.40f, 0.25f, 1f);
            case 'K': return new Color(0.25f, 0.19f, 0.13f, 1f);
            case 'F': return new Color(0.88f, 0.64f, 0.42f, 1f);
            case 'S': return new Color(0.62f, 0.39f, 0.24f, 1f);
            case 'H': return new Color(0.16f, 0.10f, 0.07f, 1f);
            case 'E': return GhostFormEyeColor;
            default: return Color.clear;
        }
    }

    private static int DirectionIndex(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) return direction.x < 0f ? 2 : 3;
        return direction.y > 0f ? 1 : 0;
    }

    private static string DirectionName(Vector2 direction)
    {
        if (direction == Vector2.up) return "Up";
        if (direction == Vector2.left) return "Left";
        if (direction == Vector2.right) return "Right";
        return "Down";
    }

    private void OnDestroy()
    {
        for (int i = 0; i < 4; i++)
        {
            ReleaseSprite(IdleSprites[i]);
            ReleaseSprite(AttackSprites[i]);
        }
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
