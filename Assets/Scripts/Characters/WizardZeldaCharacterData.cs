using UnityEngine;

/// <summary>Level 2 desert wizard with a wrapped pointed cap, indigo robe and a turquoise clasp.</summary>
public sealed class WizardZeldaCharacterData : ZeldaCharacterData
{
    private static readonly Vector3 EditorVisualBoundsCenter = new Vector3(0f, 0.375f, 0f);
    private static readonly Vector3 EditorVisualBoundsSize = new Vector3(1f, 1.25f, 0f);
    [Header("Basic Attack / 普通攻击")]
    [SerializeField] private int attackPower = 1;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.48f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.65f, 0.65f);
    [SerializeField] private Color attackVisualTint = new Color(0.38f, 0.75f, 0.96f, 0.85f);
    [Header("Wizard Appearance / 巫师外观")]
    [SerializeField] private Color robeColor = new Color(0.35f, 0.23f, 0.57f, 1f);
    [SerializeField] private Color robeShadow = new Color(0.2f, 0.13f, 0.36f, 1f);
    [SerializeField] private Color robeHighlight = new Color(0.49f, 0.34f, 0.69f, 1f);
    [SerializeField] private Color trimColor = new Color(0.72f, 0.54f, 0.25f, 1f);
    [SerializeField] private Color scarfColor = new Color(0.76f, 0.67f, 0.47f, 1f);
    [SerializeField] private Color faceColor = new Color(0.78f, 0.55f, 0.36f, 1f);
    [SerializeField] private Color eyeColor = new Color(0.08f, 0.1f, 0.16f, 1f);
    [SerializeField] private Color gemColor = new Color(0.22f, 0.78f, 0.80f, 1f);
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 7f;

    // 16x20 canvas at the same 16 PPU; four extra rows only enlarge the hat.
    // Top-to-bottom: A/S/L=indigo robe, G=gold edging, T=sand scarf,
    // F/E=skin/eyes. The continuous robe hem hides the legs in every pose.
    private static readonly string[] Front = {
        ".......LL.......",
        ".......LA.......",
        "......LAAL......",
        "......LAAS......",
        ".....LAAAAS.....",
        "....LAAAAAAS....",
        "...LAAAAAAAAS...",
        "..GGGGGCGGGGGG..",
        ".....FEFFEF.....",
        "....LGFTTFGL....",
        "....SATCCTAS....",
        "....GGATTAGG....",
        "....FFATTAFF....",
        "....FFGTTGFF....",
        "....SAGTTGAS....",
        "....SAAAAAAS....",
        "...SALAAAASAS...",
        "...SGGGGGGGGS...",
        "................",
        "................"
    };

    private static readonly string[] Back = {
        ".......LL.......",
        ".......LA.......",
        "......LAAL......",
        "......LAAS......",
        ".....LAAAAS.....",
        "....LAAAAAAS....",
        "...LAAAAAAAAS...",
        "..GGGGGGGGGGGG..",
        ".....TTTTTT.....",
        "....LGTTTTGL....",
        "....SATTTTAS....",
        "....GGATTAGG....",
        "....FFATTAFF....",
        "....FFAAAAFF....",
        "....SASAASAS....",
        "....SAAAAAAS....",
        "...SALAAAASAS...",
        "...SGGGGGGGGS...",
        "................",
        "................"
    };

    private static readonly string[] Left = {
        "......LL........",
        "......LA........",
        ".....LAAL.......",
        ".....LAAS.......",
        "....LAAAAS......",
        "...LAAAAAAS.....",
        "..LAAAAAAAAS....",
        ".GGGGGCGGGGGG...",
        "....FEFTTS......",
        ".....FTTTGS.....",
        ".....LTAAAS.....",
        ".....ATGGAS.....",
        ".....ATFFAS.....",
        ".....ATFFAS.....",
        ".....ATATAS.....",
        ".....SAAAAS.....",
        "....SALAAASS....",
        "....SGGGGGGS....",
        "................",
        "................"
    };

    private static readonly string[] Right = {
        "........LL......",
        "........AL......",
        ".......LAAL.....",
        ".......SAAL.....",
        "......SAAAAL....",
        ".....SAAAAAAL...",
        "....SAAAAAAAAL..",
        "...GGGGGGCGGGGG.",
        "......STTFEF....",
        ".....SGTTTF.....",
        ".....SAAATL.....",
        ".....SAGGTA.....",
        ".....SAFFTA.....",
        ".....SAFFTA.....",
        ".....SATATA.....",
        ".....SAAAAS.....",
        "....SSAAALAS....",
        "....SGGGGGGS....",
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
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Shockwave;
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
        var pixels = new Color[16 * 20];
        for (int row = 0; row < 20; row++)
        for (int x = 0; x < 16; x++)
        {
            char symbol = map[row][x];
            if (symbol == '.') continue;
            int y = 19 - row;
            // Animate cloth folds and lift one hem corner, never reveal separate legs.
            if (pose == 1 || pose == 2)
            {
                int liftedCorner = direction < 2 ? (pose == 1 ? 3 : 12) : (pose == 1 ? 4 : 11);
                if (y == 2 && x == liftedCorner) continue;
                if (y >= 2 && y <= 5 && symbol == 'A' && x == (pose == 1 ? 6 : 9))
                    symbol = 'S';
            }
            pixels[y * 16 + x] = PixelColor(symbol);
        }
        if (pose == 3)
        {
            RestoreBodyBehindAttackingArm(pixels, direction);
            // Sleeve/cuff and a distinct exposed hand; the normal melee hitbox is separate.
            int startX = direction == 2 ? 1 : direction == 3 ? 11 : 6;
            int startY = direction == 1 ? 10 : direction == 0 ? 4 : 6;
            for (int y = startY; y < startY + 3; y++)
            for (int x = startX; x < startX + 4; x++)
                pixels[y * 16 + x] = y == startY + 2 ? trimColor : robeColor;
            int handX = direction == 2 ? startX : direction == 3 ? startX + 2 : startX + 1;
            int handY = direction == 1 ? startY + 1 : startY;
            for (int y = handY; y < handY + 2; y++)
            for (int x = handX; x < handX + 2; x++)
                pixels[y * 16 + x] = faceColor;
        }
        var texture = new Texture2D(16, 20, TextureFormat.RGBA32, false)
        {
            name = "Wizard " + direction + " " + pose,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply();
        var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 20), new Vector2(0.5f, 0.2f), 16f);
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
                pixels[y * 16 + innerX] = robeShadow;
            }
        }
        else
        {
            // In profile the near arm covers the torso, so restore clothing rather
            // than making a transparent hole where the idle hand/cuff used to be.
            const int startX = 7; // The profile hand/cuff is centered on the torso.
            for (int y = 6; y <= 8; y++)
            for (int x = startX; x < startX + 2; x++)
                pixels[y * 16 + x] = y == 6 ? robeShadow :
                    ((direction == 2 ? x == 8 : x == 7) ? scarfColor : robeColor);
        }
    }

    private Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'A': return robeColor;
            case 'S': return robeShadow;
            case 'L': return robeHighlight;
            case 'G': return trimColor;
            case 'T': return scarfColor;
            case 'F': return faceColor;
            case 'E': return eyeColor;
            case 'C': return gemColor;
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
