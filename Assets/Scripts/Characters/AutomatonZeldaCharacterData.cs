using UnityEngine;

/// <summary>Human-shaped red/steel automaton; coarse 16-PPU armor and integral blade hands.</summary>
public sealed class AutomatonZeldaCharacterData : ZeldaCharacterData
{
    [Header("Automaton Combat / 自动人偶战斗")]
    [SerializeField] private int attackPower = 2;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.32f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.55f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.85f, 0.9f);
    [Header("Steel And Energy / 金属与能量")]
    [SerializeField] private Color armorColor = new Color(0.46f, 0.5f, 0.5f, 1f);
    [SerializeField] private Color armorShadow = new Color(0.18f, 0.21f, 0.22f, 1f);
    [SerializeField] private Color armorHighlight = new Color(0.68f, 0.72f, 0.73f, 1f);
    [SerializeField] private Color energyColor = new Color(0.82f, 0.035f, 0.025f, 1f);
    [SerializeField] private Color eyeColor = new Color(1f, 0.75f, 0.12f, 1f);
    [SerializeField] private Color bladeColor = new Color(0.86f, 0.9f, 0.91f, 1f);
    [SerializeField, Min(1f)] private float walkFramesPerSecond = 7f;
    private readonly Sprite[,] frames = new Sprite[4, 4];
    private bool visualsDirty = true;
    public override bool CanAttack => !IsGhostForm;
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.TwinBlades;
    public override Color AttackVisualTint => Color.white;
    public override Color GhostFormEyeColor => eyeColor;

    private static readonly string[] Front = {
        "................",
        "................",
        ".....HHHHHH.....",
        "....HMMMMMMD....",
        "....DMYDDYMD....",
        "....DMDDDDMD....",
        ".....DMMMMD.....",
        "..HRRHHMMHHRRH..",
        "..MRRDDMMDDRRM..",
        "...DDHMRRMHDD...",
        "...SSHMRRMHSS...",
        "...CBHMMMMHBC...",
        "...CBDDDDDDBC...",
        "...CBMMDDMMBC...",
        "...CBMHDDHMBC...",
        "...CBMD..DMBC...",
        "....BMM..MMB....",
        ".....DD..DD.....",
        "................",
        "................",
    };
    private static readonly string[] Back = {
        "................",
        "................",
        ".....HHHHHH.....",
        "....HMMMMMMD....",
        "....DMDMMDMD....",
        "....DMDMMDMD....",
        ".....DMMMMD.....",
        "..HRRHHMMHHRRH..",
        "..MRRDDMMDDRRM..",
        "...DDHMRRMHDD...",
        "...SSHMRRMHSS...",
        "...CBHMDDMHBC...",
        "...CBDDDDDDBC...",
        "...CBMMDDMMBC...",
        "...CBMHDDHMBC...",
        "...CBMD..DMBC...",
        "....BMM..MMB....",
        ".....DD..DD.....",
        "................",
        "................",
    };
    private static readonly string[] Left = {
        "................",
        "................",
        ".....HHHHH......",
        "....HMMMMMD.....",
        "....YDMRRMD.....",
        "....MDMMMMD.....",
        ".....DMMMD......",
        "....HHHRRMH.....",
        "....MMDMRRDD....",
        ".....MDHHMDD....",
        ".....MMSSMD.....",
        ".....MMBCMD.....",
        ".....DDBCDD.....",
        ".....MMBCMD.....",
        ".....MHBCDD.....",
        ".....MDBCD......",
        ".....MM.BM......",
        ".....DD.DD......",
        "................",
        "................",
    };
    private static readonly string[] Right = {
        "................",
        "................",
        "......HHHHH.....",
        ".....DMMMMMH....",
        ".....DMRRMDY....",
        ".....DMMMMDM....",
        "......DMMMD.....",
        ".....HMRRHHH....",
        "....DDRRMDMM....",
        "....DDMHHDM.....",
        ".....DMSSMM.....",
        ".....DMCBMM.....",
        ".....DDCBDD.....",
        ".....DMCBMM.....",
        ".....DDCBHM.....",
        "......DCBDM.....",
        "......MB.MM.....",
        "......DD.DD.....",
        "................",
        "................",
    };

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

    public Sprite GetInventorySprite()
    {
        if (visualsDirty) { ReleaseSprites(); visualsDirty = false; }
        if (frames[0, 0] == null) frames[0, 0] = CreateSprite(0, 0);
        return frames[0, 0];
    }

    public void InvalidateVisuals() => visualsDirty = true;

    private Sprite CreateSprite(int direction, int pose)
    {
        Color[] pixels = new Color[16 * 20];
        for (int row = 0; row < 20; row++)
        for (int x = 0; x < 16; x++)
        {
            char symbol = GetPoseSymbol(direction, pose, x, row);
            pixels[(19 - row) * 16 + x] = PixelColor(symbol);
        }
        var texture = new Texture2D(16, 20, TextureFormat.RGBA32, false) {
            name = "Automaton " + direction + " " + pose,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 16, 20), new Vector2(0.5f, 0.2f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }
    private static char GetPoseSymbol(int direction, int pose, int x, int row)
    {
        string[] map = direction == 1 ? Back : direction == 2 ? Left : direction == 3 ? Right : Front;
        char symbol = map[row][x];
        int profileX = direction == 3 ? 15 - x : x;
        if (pose == 3 && direction >= 2 && row >= 15 && row <= 16 && profileX >= 8 && profileX <= 9)
            return row == 15 ? 'D' : 'M'; // Far leg revealed when the near blade lifts.
        // Lift only the bottom boot row, never separate thighs from hips.
        if ((pose == 1 || pose == 2) && row == 17 && ((pose == 1) == (x < 8))) return '.';
        // Attack hitbox draws both blades. Restore armor under the profile arm.
        if (pose == 3 && (symbol == 'B' || symbol == 'C' || symbol == 'S'))
        {
            bool overTorso = direction >= 2 && profileX >= 5 && profileX <= 10 && row <= 14;
            symbol = overTorso ? (row == 12 ? 'D' : 'M') : '.';
        }
        return symbol;
    }
    private Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'M': return armorColor;
            case 'D': return armorShadow;
            case 'H': return armorHighlight;
            case 'R': return energyColor;
            case 'S': return energyColor;
            case 'Y': return eyeColor;
            case 'B': return bladeColor;
            case 'C': return armorColor;
            default: return Color.clear;
        }
    }
    protected override void OnValidate()
    {
        base.OnValidate();
        attackPower = Mathf.Max(0, attackPower);
        attackDuration = Mathf.Max(0.05f, attackDuration);
        attackSize = Vector2.Max(Vector2.one * 0.1f, attackSize);
        walkFramesPerSecond = Mathf.Max(1f, walkFramesPerSecond);
        visualsDirty = true;
    }
    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Matrix4x4 oldMatrix = Gizmos.matrix; Color oldColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0f, 0.375f, 0f), new Vector3(1f, 1.25f, 0f));
        Gizmos.matrix = oldMatrix; Gizmos.color = oldColor;
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
