using UnityEngine;

/// <summary>Level 2 soldier: wrapped headcloth, mail armor and a rank-colored scarf.</summary>
public sealed class ArabSoldierZeldaCharacterData : ZeldaCharacterData
{
    private static readonly Vector3 EditorVisualBoundsCenter = new Vector3(0f, 0.25f, 0f);
    private static readonly Vector3 EditorVisualBoundsSize = new Vector3(1f, 1f, 0f);
    [Header("Sword Attack / 剑攻击")]
    [SerializeField] private int attackPower = 1;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.25f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.6f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.55f, 0.9f);
    [SerializeField] private Color attackVisualTint = new Color(1f, 0.85f, 0.2f, 0.65f);
    [Header("Soldier Appearance / 士兵外观")]
    [SerializeField] private Color armorColor = new Color(0.40f, 0.45f, 0.47f, 1f);
    [SerializeField] private Color armorShadow = new Color(0.24f, 0.29f, 0.31f, 1f);
    [SerializeField] private Color armorHighlight = new Color(0.57f, 0.60f, 0.59f, 1f);
    [SerializeField] private Color headclothColor = new Color(0.68f, 0.65f, 0.56f, 1f);
    [SerializeField] private Color headclothHighlight = new Color(0.85f, 0.81f, 0.69f, 1f);
    [SerializeField] private Color headclothShadow = new Color(0.43f, 0.41f, 0.35f, 1f);
    [SerializeField] private Color faceColor = new Color(0.69f, 0.46f, 0.29f, 1f);
    [SerializeField] private Color eyeColor = new Color(0.06f, 0.05f, 0.04f, 1f);
    [SerializeField] private Color tunicColor = new Color(0.52f, 0.47f, 0.34f, 1f);
    [SerializeField] private Color beltColor = new Color(0.28f, 0.19f, 0.10f, 1f);
    [SerializeField] private Color bootsColor = new Color(0.22f, 0.14f, 0.08f, 1f);
    [Header("Rank Scarf / 等级围巾")]
    [SerializeField] private Color scarfColor = new Color(0.57f, 0.59f, 0.60f, 1f);
    [SerializeField] private Color scarfShadow = new Color(0.34f, 0.37f, 0.39f, 1f);
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 7f;

    // 16x16 color blocks; the only rank-specific colors are C/R (scarf and folds).
    // W/H/D=headcloth, F/E=face/eyes, A/S/L=armor, T=tunic, B=belt, K=boots.
    private static readonly string[] Front = {
        "......HHHH......",
        ".....WHHWW......",
        "....HWWWWWWH....",
        ".....WFEFEW.....",
        ".....DFFFFD.....",
        "....RCCCCCCR....",
        "....ARCCCLLA....",
        "....LLRCASLL....",
        "....FFRCAAFF....",
        "....FFBCBBFF....",
        ".....TSTTST.....",
        ".....TSTTST.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Back = {
        "......HHHH......",
        ".....WWHHW......",
        "....HWWWWWWH....",
        ".....DWWWWD.....",
        ".....DDWWDD.....",
        "....RCCCCCCR....",
        "....ALRCCSLA....",
        "....LLRCCALL....",
        "....FFRRRAFF....",
        "....FFBBBBFF....",
        ".....TSTTST.....",
        ".....TSTTST.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Left = {
        ".....HHHH.......",
        "....WHHWWW......",
        "...HWWWWWWH.....",
        "....FEFWWW......",
        "....FFFDDD......",
        "....CCCCCCR.....",
        ".....CLAAAC.....",
        ".....ASLLAC.....",
        ".....ASFFAR.....",
        ".....LBFFBB.....",
        ".....TSSTST.....",
        ".....TSSTST.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Right = {
        ".......HHHH.....",
        "......WWWHHW....",
        ".....HWWWWWWH...",
        "......WWWFEF....",
        "......DDDFFF....",
        ".....RCCCCCC....",
        ".....CAAALC.....",
        ".....CALLSA.....",
        ".....RAFFSA.....",
        ".....BBFFBL.....",
        ".....TSTSST.....",
        ".....TSTSST.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private readonly Sprite[,] frames = new Sprite[4, 4];
    private bool visualsDirty = true;
    public override bool CanAttack => !IsGhostForm;
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Sword;
    public override Color AttackVisualTint => attackVisualTint;
    public override Color GhostFormEyeColor => eyeColor;

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
        string[] map = direction == 1 ? Back : direction == 2 ? Left : direction == 3 ? Right : Front;
        var pixels = new Color[16 * 16];
        for (int row = 0; row < 16; row++)
        for (int x = 0; x < 16; x++)
        {
            char symbol = map[row][x];
            if (symbol == '.') continue;
            int y = 15 - row;
            // Alternate boots without shifting the collision body or helmet.
            if (row >= 12 && (pose == 1 || pose == 2))
                y += (x < 8) == (pose == 1) ? 1 : 0;
            pixels[y * 16 + x] = PixelColor(symbol);
        }
        if (pose == 3)
        {
            RestoreBodyBehindAttackingArm(pixels, direction);
            // Sleeve/cuff and a distinct exposed hand; actual sword uses the common attack prefab.
            int startX = direction == 2 ? 1 : direction == 3 ? 11 : 6;
            int startY = direction == 1 ? 10 : direction == 0 ? 4 : 6;
            for (int y = startY; y < startY + 3; y++)
            for (int x = startX; x < startX + 4; x++)
                pixels[y * 16 + x] = y == startY + 2 ? armorHighlight : armorColor;
            int handX = direction == 2 ? startX : direction == 3 ? startX + 2 : startX + 1;
            int handY = direction == 1 ? startY + 1 : startY;
            for (int y = handY; y < handY + 2; y++)
            for (int x = handX; x < handX + 2; x++)
                pixels[y * 16 + x] = faceColor;
        }
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            name = "ArabSoldier " + direction + " " + pose,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.25f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private void RestoreBodyBehindAttackingArm(Color[] pixels, int direction)
    {
        if (direction < 2)
        {
            // The same anatomical arm appears on opposite sides in front/back views.
            // Remove its hanging silhouette; keep the opposite idle hand intact.
            int outerX = direction == 0 ? 4 : 11;
            int innerX = direction == 0 ? 5 : 10;
            for (int y = 6; y <= 8; y++)
            {
                pixels[y * 16 + outerX] = Color.clear;
                pixels[y * 16 + innerX] = y == 6 ? beltColor : armorShadow;
            }
        }
        else
        {
            // In profile the near arm covers the torso, so restore clothing rather
            // than making a transparent hole where the idle hand/cuff used to be.
            const int startX = 7; // The profile hand/cuff is centered on the torso.
            for (int y = 6; y <= 8; y++)
            for (int x = startX; x < startX + 2; x++)
                pixels[y * 16 + x] = y == 6 ? beltColor : armorColor;
        }
    }

    private Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'A': return armorColor;
            case 'S': return armorShadow;
            case 'L': return armorHighlight;
            case 'W': return headclothColor;
            case 'H': return headclothHighlight;
            case 'D': return headclothShadow;
            case 'F': return faceColor;
            case 'E': return eyeColor;
            case 'T': return tunicColor;
            case 'B': return beltColor;
            case 'K': return bootsColor;
            case 'C': return scarfColor;
            case 'R': return scarfShadow;
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
