using UnityEngine;

/// <summary>Strongman-sized paladin with broad sky-blue armor, a left cape and a double-headed axe.</summary>
public sealed class HeavyPaladinZeldaCharacterData : ZeldaCharacterData
{
    private static readonly Vector3 EditorVisualBoundsCenter = new Vector3(0f, 0.25f, 0f);
    private static readonly Vector3 EditorVisualBoundsSize = new Vector3(1.25f, 1.25f, 0f);
    [Header("Battle Axe Attack / 双面战斧攻击")]
    [SerializeField] private int attackPower = 3;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.65f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.95f, 0.9f);
    [SerializeField] private Color attackVisualTint = Color.white;
    [Header("Heavy Paladin Appearance / 重装圣骑士外观")]
    [SerializeField] private Color armorColor = new Color(0.23f, 0.54f, 0.69f, 1f);
    [SerializeField] private Color armorShadow = new Color(0.13f, 0.33f, 0.48f, 1f);
    [SerializeField] private Color armorHighlight = new Color(0.48f, 0.63f, 0.69f, 1f);
    [SerializeField] private Color capeColor = new Color(0.10f, 0.26f, 0.58f, 1f);
    [SerializeField] private Color capeShadow = new Color(0.07f, 0.16f, 0.35f, 1f);
    [SerializeField] private Color visorColor = new Color(0.02f, 0.06f, 0.10f, 1f);
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 7f;

    // Strongman's 20x20 canvas, 16 PPU, short plated legs and broad shoulders.
    private static readonly string[] Front = {
        ".......HHHHHH.......",
        "......AAAAAAAA......",
        "......SAAAAAAS......",
        "......SAVVVVAS......",
        "......SAAVVAAS......",
        "......SSSVVSSS......",
        "...DCCCHSSSSSHHHH...",
        "...DCCCHHAAHHSAAA...",
        "...DCCCAHAAHASAAA...",
        "...DCCCAASSAASSSS...",
        "...DCCCSAHHHASSSS...",
        "...DCCCSAAAAASSSS...",
        "....DDCSSHHSSSSS....",
        "......SAAAAAAS......",
        "......AAA..AAA......",
        "......SSS..SSS......",
        "....................",
        "....................",
        "....................",
        "....................",
    };
    private static readonly string[] Back = {
        ".......HHHHHH.......",
        "......AAAAAAAA......",
        "......SAAAAAAS......",
        "......SASSASAS......",
        "......SASSASAS......",
        "......SSAAAASS......",
        "...HHHHSSSSSHCCCD...",
        "...AAASHHAAHHCCCD...",
        "...AAASAHAAHACCCD...",
        "...SSSSAASSAACCCD...",
        "...SSSSAHHHASCCCD...",
        "...SSSSAAAAASCCCD...",
        "....SSSSSHHSSCDD....",
        "......SAAAAAAS......",
        "......AAA..AAA......",
        "......SSS..SSS......",
        "....................",
        "....................",
        "....................",
        "....................",
    };
    private static readonly string[] Left = {
        "......HHHHHH........",
        ".....AAAAAAAS.......",
        ".....HAAAAASS.......",
        ".....VAAAAASS.......",
        ".....VAAAAASS.......",
        ".....SSAAAASS.......",
        ".....HHCCCCDHHH.....",
        ".....SACCCDDAAS.....",
        ".....SACCCDDAAS.....",
        ".....SACCCDDAAS.....",
        ".....SACCCDDAAS.....",
        ".....SACCCDDAAS.....",
        ".....SAAAAAAAAS.....",
        "......SAAAAAAS......",
        "......AAA..AAA......",
        "......SSS..SSS......",
        "....................",
        "....................",
        "....................",
        "....................",
    };
    private static readonly string[] Right = {
        "........HHHHHH......",
        ".......SAAAAAAA.....",
        ".......SSAAAAAH.....",
        ".......SSAAAAAV.....",
        ".......SSAAAAAV.....",
        ".......SSAAAASS.....",
        ".....CCCSSSSSHH.....",
        ".....DCCSSSSSAS.....",
        ".....DCCSSSSSAS.....",
        ".....DCCSSSSSAS.....",
        ".....DCCSSSSSAS.....",
        ".....DCCSSSSSAS.....",
        ".....SAAAAAAAAS.....",
        "......SAAAAAAS......",
        "......AAA..AAA......",
        "......SSS..SSS......",
        "....................",
        "....................",
        "....................",
        "....................",
    };

    private readonly Sprite[,] frames = new Sprite[4, 4];
    private bool visualsDirty = true;
    public override bool CanAttack => !IsGhostForm;
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.BattleAxe;
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
        Color[] pixels = new Color[20 * 20];
        for (int row = 0; row < 20; row++)
        for (int x = 0; x < 20; x++)
            pixels[(19 - row) * 20 + x] = PixelColor(GetPoseSymbol(direction, pose, x, row));
        var texture = new Texture2D(20, 20, TextureFormat.RGBA32, false) {
            name = "Heavy Paladin " + direction + " " + pose,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 20, 20), new Vector2(0.5f, 0.3f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static char GetPoseSymbol(int direction, int pose, int x, int row)
    {
        string[] map = direction == 1 ? Back : direction == 2 ? Left : direction == 3 ? Right : Front;
        char symbol = map[row][x];
        // Cloth always covers the arm underneath it.
        if (symbol == 'C' || symbol == 'D') return symbol;
        if ((pose == 1 || pose == 2) && row == 15 && ((pose == 1) == (x < 10))) return '.';
        if (pose == 3)
        {
            // Only the raised gauntlet is drawn in attack poses.
            int startX = direction == 2 ? 2 : direction == 3 ? 13 : 8;
            int startRow = direction == 1 ? 6 : direction == 0 ? 11 : 11;
            if (x >= startX && x < startX + 5 && row >= startRow && row < startRow + 3)
                return row == startRow ? 'H' : 'G';
        }
        else
        {
            int handX = direction == 0 ? 14 : direction == 1 ? 3 : direction == 2 ? 8 : 9;
            int handRow = direction == 2 ? 10 : 9;
            if (x >= handX && x < handX + 3 && row >= handRow && row < handRow + 3)
                return row == handRow ? 'S' : 'G';
        }
        return symbol;
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
            case 'G': return Color.Lerp(armorHighlight, Color.white, 0.12f);
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
