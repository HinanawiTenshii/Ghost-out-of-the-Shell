using UnityEngine;

/// <summary>Sky-blue plate armor, sealed helmet and a blue shoulder cape.</summary>
public sealed class PaladinZeldaCharacterData : ZeldaCharacterData
{
    private static readonly Vector3 EditorVisualBoundsCenter = new Vector3(0f, 0.25f, 0f);
    private static readonly Vector3 EditorVisualBoundsSize = new Vector3(1f, 1f, 0f);
    [Header("Sword Attack / 剑攻击")]
    [SerializeField] private int attackPower = 1;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.25f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.6f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.55f, 0.9f);
    [SerializeField] private Color attackVisualTint = new Color(0.5f, 0.85f, 1f, 0.65f);
    [Header("Paladin Appearance / 圣骑士外观")]
    [SerializeField] private Color armorColor = new Color(0.23f, 0.54f, 0.69f, 1f);
    [SerializeField] private Color armorShadow = new Color(0.13f, 0.33f, 0.48f, 1f);
    [SerializeField] private Color armorHighlight = new Color(0.48f, 0.63f, 0.69f, 1f);
    [SerializeField] private Color capeColor = new Color(0.10f, 0.26f, 0.58f, 1f);
    [SerializeField] private Color capeShadow = new Color(0.07f, 0.16f, 0.35f, 1f);
    [SerializeField] private Color visorColor = new Color(0.02f, 0.06f, 0.10f, 1f);
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 7f;

    // Same 16 x 16 color-block construction and proportions as SwordGuy.
    // A=armor, S=shadow, H=highlight, C=cape, D=cape fold, V=visor.
    private static readonly string[] Front = {
        "......HHHH......",
        ".....AAAAAA.....",
        ".....SAAAAS.....",
        ".....SVVVVS.....",
        ".....SAVVAS.....",
        "...CCCSVVSSH....",
        "...DCCSSSSSA....",
        "...DCCHAAHSA....",
        "...DCCAHASS.....",
        "...DCCSSHSS.....",
        "...DDCAAASS.....",
        "...DDSAAAAS.....",
        ".....AA..AA.....",
        ".....SS..SS.....",
        "................",
        "................"
    };
    private static readonly string[] Back = {
        "......HHHH......",
        ".....AAAAAA.....",
        ".....SASSAS.....",
        ".....SASSAS.....",
        ".....SASSAS.....",
        "....HSAAAASCC...",
        "....SSSSSSCCD...",
        "....SSHSSHCCD...",
        ".....SASSACCD...",
        ".....SSHHSCCD...",
        ".....SAHHACDD...",
        ".....SAAAASDD...",
        ".....AA..AA.....",
        ".....SS..SS.....",
        "................",
        "................"
    };
    private static readonly string[] Left = {
        ".....HHHH.......",
        "....AAAAAA......",
        "....HAAAAS......",
        "....VAAAAS......",
        "....VAAAAS......",
        "....SCCCCCHH....",
        "....HHSSSSAA....",
        "....SSCCDDAA....",
        "....SSCCDDS.....",
        ".....CCCDDS.....",
        ".....CCCDDS.....",
        ".....SAADDS.....",
        ".....AA..AA.....",
        ".....SS..SS.....",
        "................",
        "................"
    };
    private static readonly string[] Right = {
        ".......HHHH.....",
        "......AAAAAA....",
        "......SAAAAH....",
        "......SAAAAV....",
        "......SAAAAV....",
        "....CCSAAAAS....",
        "....CCSSSSHA....",
        "....DCAAHSSA....",
        "....DCAHAAS.....",
        "....DCSHHSS.....",
        "....DCAHHAS.....",
        "....DSAAAAS.....",
        ".....AA..AA.....",
        ".....SS..SS.....",
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
    public override Color GhostFormEyeColor => visorColor;

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
            // Alternate plated boots without shifting the collision body or helmet.
            if (row >= 12 && (pose == 1 || pose == 2))
                y += (x < 8) == (pose == 1) ? 1 : 0;
            // A lifted boot remains behind the bottom edge of the draped cape.
            if (y != 15 - row && (map[15 - y][x] == 'C' || map[15 - y][x] == 'D')) continue;
            pixels[y * 16 + x] = PixelColor(symbol);
        }
        if (pose == 3)
        {
            // Replace the resting hand with this pose, never draw both hands of the
            // same arm. The raised gauntlet emerges beyond the shoulder cape.
            int startX = direction == 2 ? 1 : direction == 3 ? 11 : 6;
            int startY = direction == 1 ? 10 : direction == 0 ? 4 : 6;
            for (int y = startY; y < startY + 3; y++)
            for (int x = startX; x < startX + 4; x++)
                pixels[y * 16 + x] = y == startY + 2 ? armorHighlight : GauntletColor;
        }
        else
        {
            // The cape is on the character's left: fully hides that hand from
            // front/back, with only the fingertips peeking out in left profile.
            // The uncovered profile hand stays at the centre of the torso.
            if (direction == 0) DrawRestingGauntlet(pixels, map, 10, 8);
            else if (direction == 1) DrawRestingGauntlet(pixels, map, 4, 8);
            else if (direction == 2) DrawRestingGauntlet(pixels, map, 6, 9);
            else DrawRestingGauntlet(pixels, map, 8, 8);
        }
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            name = "Paladin " + direction + " " + pose,
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

    private Color GauntletColor => Color.Lerp(armorHighlight, Color.white, 0.12f);

    private void DrawRestingGauntlet(Color[] pixels, string[] map, int startX, int firstRow)
    {
        for (int row = firstRow; row < firstRow + 3; row++)
        for (int x = startX; x < startX + 2; x++)
        {
            // Keep the draped cloth in front of the arm, rather than painting
            // a bright hand on top of the cape or exposing an entire second arm.
            if (map[row][x] == 'C' || map[row][x] == 'D') continue;
            pixels[(15 - row) * 16 + x] = row == firstRow ? armorShadow : GauntletColor;
        }
    }

    private Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'A': return armorColor;
            case 'S': return armorShadow;
            case 'H': return armorHighlight;
            case 'C': return capeColor;
            case 'D': return capeShadow;
            case 'V': return visorColor;
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
