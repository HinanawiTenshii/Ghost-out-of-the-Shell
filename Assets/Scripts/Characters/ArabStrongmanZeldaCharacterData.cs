using UnityEngine;

/// <summary>Heavy Arab soldier: the soldier's headwrap, scarf and armor on the Strongman rig.</summary>
public sealed class ArabStrongmanZeldaCharacterData : StrongmanZeldaCharacterData
{
    [Header("Heavy Soldier Appearance / 阿拉伯重装士兵外观")]
    [SerializeField] private Color headclothColor = new Color(0.72f, 0.68f, 0.56f, 1f);
    [SerializeField] private Color headclothHighlight = new Color(0.86f, 0.81f, 0.68f, 1f);
    [SerializeField] private Color headclothShadow = new Color(0.49f, 0.46f, 0.37f, 1f);
    [SerializeField] private Color armorColor = new Color(0.40f, 0.45f, 0.47f, 1f);
    [SerializeField] private Color armorShadow = new Color(0.24f, 0.29f, 0.31f, 1f);
    [SerializeField] private Color armorHighlight = new Color(0.57f, 0.60f, 0.59f, 1f);
    [SerializeField] private Color leatherColor = new Color(0.31f, 0.20f, 0.12f, 1f);
    [SerializeField] private Color brassColor = new Color(0.64f, 0.49f, 0.27f, 1f);
    [SerializeField] private Color skinColor = new Color(0.72f, 0.47f, 0.29f, 1f);
    [SerializeField] private Color skinShadow = new Color(0.55f, 0.33f, 0.20f, 1f);
    [SerializeField] private Color gloveColor = new Color(0.43f, 0.30f, 0.17f, 1f);
    [SerializeField] private Color scarfShadow = new Color(0.34f, 0.37f, 0.39f, 1f);
    [SerializeField] private Color scarfColor = new Color(0.57f, 0.59f, 0.60f, 1f);
    [SerializeField] private Color tunicColor = new Color(0.52f, 0.47f, 0.34f, 1f);
    [SerializeField] private Color bootsColor = new Color(0.20f, 0.13f, 0.08f, 1f);
    [SerializeField] private Color eyeColor = new Color(0.08f, 0.075f, 0.06f, 1f);

    // The 20x20 canvas, feet and pivot match the existing Strongman walk rig.
    // W/H/Q=headwrap; F/S=skin; V/R=scarf; T=tunic; C/D/L=armor; B/A/G=leather/brass/gloves.
    private static readonly string[] Front = {
        ".......WHHHHW.......",
        "......WHHHHWWW......",
        ".....HWWWWWWWWH.....",
        "......WFEFFEFW......",
        "......QFFFFFFQ......",
        "......RVVVVVVR......",
        "...LLLRVVVVVCLLLL...",
        "...CCCLRVVCCDLCCC...",
        "...FFFCRVCCLCDFFF...",
        "...SSSCRVCCCCDSSS...",
        "...GGGCRVCCCCDGGG...",
        "....DDCLCCLCCCDD....",
        ".....DBBBAABBBD.....",
        "......TCTTTTCT......",
        "......KKK..KKK......",
        "......KKK..KKK......",
        "....................",
        "....................",
        "....................",
        "...................."
    };
    private static readonly string[] Back = {
        ".......WHHHHW.......",
        "......WWWHHHHW......",
        ".....HWWWWWWWWH.....",
        "......QWWWWWWQ......",
        "......QQWWWWQQ......",
        "......RVVVVVVR......",
        "...LLLCRVVVVCLLLL...",
        "...CCCLRVVVCDLCCC...",
        "...FFFCRVVVCCDFFF...",
        "...SSSCRRVVCCDSSS...",
        "...GGGCCRRRCCDGGG...",
        "....DDCLCCDCCCDD....",
        ".....DBBBBBBBBD.....",
        "......TCTTTTCT......",
        "......KKK..KKK......",
        "......KKK..KKK......",
        "....................",
        "....................",
        "....................",
        "...................."
    };
    private static readonly string[] Left = {
        ".....WHHHHW.........",
        "....WHHHHWWW........",
        "...HWWWWWWWWH.......",
        "....FEFFWWWQ........",
        "....FFFFWWQQ........",
        "....RVVVVVVR........",
        "......VLLLCCVR......",
        "......CLLLCCVR......",
        "......CCFFFCRR......",
        "......CCSSSCCR......",
        "......CCGGGCCD......",
        "......DCLLLCCD......",
        "......DBBBBBBD......",
        "......TCTTTTCT......",
        "......KKK..KKK......",
        "......KKK..KKK......",
        "....................",
        "....................",
        "....................",
        "...................."
    };

    private readonly Sprite[,] desertFrames = new Sprite[4, 2];
    private bool visualsDirty = true;
    public override Color GhostFormEyeColor => eyeColor;

    public override void ApplyCharacterVisual(SpriteRenderer renderer, Vector2 facing, bool isMoving, bool isAttacking)
    {
        if (renderer == null) return;
        if (visualsDirty)
        {
            PixelCharacterWalkAnimator animator = GetComponent<PixelCharacterWalkAnimator>();
            if (animator != null) animator.InvalidateFrames();
            ReleaseDesertSprites();
            visualsDirty = false;
        }
        int direction = Mathf.Abs(facing.x) > Mathf.Abs(facing.y) ? (facing.x < 0f ? 2 : 3) : (facing.y > 0f ? 1 : 0);
        int pose = isAttacking ? 1 : 0;
        if (desertFrames[direction, pose] == null)
            desertFrames[direction, pose] = CreateDesertSprite(direction, isAttacking);
        renderer.sprite = desertFrames[direction, pose];
        renderer.color = GetDamageFeedbackTint(GetMovementTint(isMoving));
    }

    private Sprite CreateDesertSprite(int direction, bool attacking)
    {
        Texture2D texture = CreateDesertTexture(direction, attacking);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 20, 20), new Vector2(0.5f, 0.3f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private Texture2D CreateDesertTexture(int direction, bool attacking)
    {
        string[] map = direction == 1 ? Back : direction >= 2 ? Left : Front;
        var pixels = new Color[400];
        for (int row = 0; row < 20; row++)
        for (int x = 0; x < 20; x++)
            pixels[(19 - row) * 20 + x] = DesertPixelColor(map[row][direction == 3 ? 19 - x : x]);
        if (attacking)
        {
            if (direction < 2)
            {
                int outerX = direction == 0 ? 3 : 14;
                PaintDesert(pixels, outerX, 9, 3, 5, '.');
                PaintDesert(pixels, direction == 0 ? 5 : 14, 9, 1, 4, 'D');
            }
            else
            {
                // Replace the centered near arm with clothing before extending it.
                PaintDesert(pixels, direction == 2 ? 8 : 9, 9, 3, 4, 'C');
            }
            int startX = direction == 2 ? 2 : direction == 3 ? 12 : 7;
            int startY = direction == 1 ? 12 : direction == 0 ? 5 : 9;
            PaintDesert(pixels, startX, startY, 6, 3, 'F');
            int handX = direction == 2 ? startX : direction == 3 ? startX + 3 : startX + 1;
            int handY = direction == 1 ? startY + 1 : startY;
            PaintDesert(pixels, handX, handY, 3, 2, 'G');
        }
        var texture = new Texture2D(20, 20, TextureFormat.RGBA32, false)
        {
            name = "Arab Strongman " + direction + (attacking ? " Attack" : " Idle"),
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        // Keep readable for the shared walking animator and ghost-form recoloring.
        texture.Apply(false, false);
        return texture;
    }

    private void PaintDesert(Color[] pixels, int x, int y, int width, int height, char symbol)
    {
        for (int py = y; py < y + height; py++)
        for (int px = x; px < x + width; px++)
            pixels[py * 20 + px] = DesertPixelColor(symbol);
    }

    private Color DesertPixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'W': return headclothColor;
            case 'H': return headclothHighlight;
            case 'Q': return headclothShadow;
            case 'C': return armorColor;
            case 'D': return armorShadow;
            case 'L': return armorHighlight;
            case 'B': return leatherColor;
            case 'A': return brassColor;
            case 'F': return skinColor;
            case 'S': return skinShadow;
            case 'G': return gloveColor;
            case 'R': return scarfShadow;
            case 'V': return scarfColor;
            case 'T': return tunicColor;
            case 'K': return bootsColor;
            case 'E': return eyeColor;
            default: return Color.clear;
        }
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        visualsDirty = true;
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying) return;
        Matrix4x4 oldMatrix = Gizmos.matrix;
        Color oldColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0f, 0.25f, 0f), new Vector3(1.25f, 1.25f, 0f));
        Gizmos.matrix = oldMatrix;
        Gizmos.color = oldColor;
    }

    private void OnDestroy() => ReleaseDesertSprites();
    private void ReleaseDesertSprites()
    {
        for (int d = 0; d < 4; d++)
        for (int p = 0; p < 2; p++)
        {
            Sprite sprite = desertFrames[d, p];
            if (sprite == null) continue;
            Texture2D texture = sprite.texture;
            if (Application.isPlaying) { Destroy(sprite); Destroy(texture); }
            else { DestroyImmediate(sprite); DestroyImmediate(texture); }
            desertFrames[d, p] = null;
        }
    }
}
