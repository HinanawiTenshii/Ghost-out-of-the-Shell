using UnityEngine;

/// <summary>A four-direction chef with civilian gameplay and a short-legged silhouette.</summary>
public sealed class ChefZeldaCharacterData : ZeldaCharacterData
{
    private static readonly Vector3 EditorVisualBoundsCenter = new Vector3(0f, 0.3125f, 0f);
    private static readonly Vector3 EditorVisualBoundsSize = new Vector3(1f, 1.25f, 0f);
    [Header("Civilian Combat / 平民攻击")]
    [SerializeField] private int attackPower = 1;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.2f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.48f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.42f, 0.36f);
    [SerializeField] private Color attackVisualTint = Color.white;
    [Header("Chef Appearance / 厨师外观")]
    [SerializeField] private Color clothingColor = new Color(0.78f, 0.84f, 0.83f, 1f);
    [SerializeField] private Color hatColor = new Color(0.94f, 0.95f, 0.89f, 1f);
    [SerializeField] private Color apronColor = new Color(0.91f, 0.91f, 0.80f, 1f);
    [SerializeField] private Color neckerchiefColor = new Color(0.78f, 0.15f, 0.12f, 1f);
    [SerializeField] private Color skinColor = new Color(0.76f, 0.52f, 0.34f, 1f);
    [SerializeField] private Color hairColor = new Color(0.20f, 0.13f, 0.09f, 1f);
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 7f;

    // Same 16 PPU and foot baseline as civilians; extra canvas height is for the toque.
    private static readonly string[] Front = {
        "................",
        ".....WWWWWW.....",
        "....WWWWWWWW....",
        "....SWWWWWWS....",
        ".....WWWWWW.....",
        ".....SSSSSS.....",
        ".....HFFFFH.....",
        ".....FEFFEF.....",
        ".....FFFFFF.....",
        "....CCCRRCCC....",
        "....CCECCECC....",
        "....SCAAAACS....",
        "....SCAAAACS....",
        "....SCAAAACS....",
        ".....AAAAAA.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private static readonly string[] Back = {
        "................",
        ".....WWWWWW.....",
        "....WWWWWWWW....",
        "....SWWWWWWS....",
        ".....WWWWWW.....",
        ".....SSSSSS.....",
        ".....HHHHHH.....",
        ".....HHHHHH.....",
        ".....HFFFFH.....",
        "....CCRCCRCC....",
        "....CCCCCCCC....",
        "....SCCCCCCS....",
        "....SSSAASSS....",
        "....SCCAACCS....",
        ".....CCCCCC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private static readonly string[] Left = {
        "................",
        "....WWWWWW......",
        "...WWWWWWWW.....",
        "...SWWWWWWS.....",
        "....WWWWWW......",
        "....SSSSSS......",
        "....FFFHHH......",
        "....FEFHHH......",
        "....FFFFHH......",
        ".....RCCCCS.....",
        ".....ACCCCS.....",
        ".....ACCCCS.....",
        ".....ACCCCS.....",
        ".....ACCCCS.....",
        ".....AACCCS.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private static readonly string[] Right = {
        "................",
        "......WWWWWW....",
        ".....WWWWWWWW...",
        ".....SWWWWWWS...",
        "......WWWWWW....",
        "......SSSSSS....",
        "......HHHFFF....",
        "......HHHFEF....",
        "......HHFFFF....",
        ".....SCCCCR.....",
        ".....SCCCCA.....",
        ".....SCCCCA.....",
        ".....SCCCCA.....",
        ".....SCCCCA.....",
        ".....SCCCAA.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................",
    };

    private readonly Sprite[,] frames = new Sprite[4, 4];
    private bool visualsDirty = true;
    public override bool CanAttack => !IsGhostForm;
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Rock;
    public override Color AttackVisualTint => attackVisualTint;
    public override Color GhostFormEyeColor => new Color(0.05f, 0.10f, 0.14f, 1f);

    public override void ApplyCharacterVisual(SpriteRenderer renderer, Vector2 facing, bool isMoving, bool isAttacking)
    {
        if (renderer == null) return;
        if (visualsDirty) { ReleaseSprites(); visualsDirty = false; }
        int direction = Mathf.Abs(facing.x) > Mathf.Abs(facing.y) ? (facing.x < 0f ? 2 : 3) : (facing.y > 0f ? 1 : 0);
        int pose = isAttacking ? 3 : isMoving ? 1 + (Mathf.FloorToInt(Time.time * walkFramesPerSecond) % 2) : 0;
        if (frames[direction, pose] == null) frames[direction, pose] = CreateSprite(direction, pose);
        renderer.sprite = frames[direction, pose];
        renderer.color = GetDamageFeedbackTint(Color.white);
    }

    private Sprite CreateSprite(int direction, int pose)
    {
        Color[] pixels = new Color[16 * 20];
        for (int row = 0; row < 20; row++)
        for (int x = 0; x < 16; x++)
            pixels[(19 - row) * 16 + x] = PixelColor(GetPoseSymbol(direction, pose, x, row));
        var texture = new Texture2D(16, 20, TextureFormat.RGBA32, false) {
            name = "Chef " + direction + " " + pose,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 16, 20), new Vector2(0.5f, 0.25f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static char GetPoseSymbol(int direction, int pose, int x, int row)
    {
        string[] map = direction == 1 ? Back : direction == 2 ? Left : direction == 3 ? Right : Front;
        char symbol = map[row][x];
        if ((pose == 1 || pose == 2) && row == 16 && ((pose == 1) == (x < 8))) return '.';
        if (direction < 2)
        {
            // Keep the off-hand, but replace the attacking hand instead of duplicating it.
            int offHandX = direction == 0 ? 4 : 10;
            int activeHandX = direction == 0 ? 10 : 4;
            if (row >= 12 && row <= 13 && x >= offHandX && x < offHandX + 2) return 'F';
            if (pose != 3 && row >= 12 && row <= 13 && x >= activeHandX && x < activeHandX + 2) return 'F';
            int attackRow = direction == 0 ? 13 : 9;
            if (pose == 3 && row >= attackRow && row < attackRow + 2 && x >= 7 && x <= 8) return 'F';
        }
        else
        {
            // The near arm is centered in profile; the far arm is occluded by the body.
            if (pose != 3 && x >= 7 && x <= 8 && row >= 12 && row <= 13) return 'F';
            int attackX = direction == 2 ? 3 : 11;
            if (pose == 3 && x >= attackX && x < attackX + 2 && row >= 10 && row <= 11) return 'F';
        }
        return symbol;
    }

    private Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'C': return clothingColor;
            case 'S': return Color.Lerp(clothingColor, new Color(0.18f, 0.25f, 0.29f, 1f), 0.38f);
            case 'W': return hatColor;
            case 'A': return apronColor;
            case 'R': return neckerchiefColor;
            case 'F': return skinColor;
            case 'H': return hairColor;
            case 'E': return GhostFormEyeColor;
            case 'K': return new Color(0.22f, 0.13f, 0.07f, 1f);
            default: return Color.clear;
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        attackPower = Mathf.Max(0, attackPower);
        attackDuration = Mathf.Max(0f, attackDuration);
        attackSize = Vector2.Max(Vector2.zero, attackSize);
        walkFramesPerSecond = Mathf.Max(1f, walkFramesPerSecond);
        visualsDirty = true;
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(EditorVisualBoundsCenter, EditorVisualBoundsSize);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }

    private void OnDestroy() => ReleaseSprites();
    private void ReleaseSprites()
    {
        for (int d = 0; d < 4; d++)
        for (int p = 0; p < 4; p++)
        {
            Sprite sprite = frames[d, p];
            if (sprite == null) continue;
            Texture2D texture = sprite.texture;
            if (Application.isPlaying) { Destroy(sprite); Destroy(texture); }
            else { DestroyImmediate(sprite); DestroyImmediate(texture); }
            frames[d, p] = null;
        }
    }
}
