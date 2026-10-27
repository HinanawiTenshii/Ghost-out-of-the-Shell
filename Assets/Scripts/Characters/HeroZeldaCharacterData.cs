using UnityEngine;

/// <summary>Sky-blue travelling hero; reuses the common sword combat and cached 16 PPU animation rig.</summary>
public sealed class HeroZeldaCharacterData : PaladinZeldaCharacterData
{
    [Header("Hero / 勇者外观")]
    [Tooltip("短袍与软帽使用 Armor Color / Shadow / Highlight；盾面使用 Cape Color。")]
    [SerializeField] private Color hairColor = new Color(0.88f, 0.66f, 0.25f, 1f);
    [SerializeField] private Color faceColor = new Color(0.96f, 0.76f, 0.54f, 1f);
    [SerializeField] private Color collarColor = new Color(0.88f, 0.93f, 0.89f, 1f);
    [SerializeField] private Color leatherColor = new Color(0.43f, 0.26f, 0.14f, 1f);
    [SerializeField] private Color bootsColor = new Color(0.25f, 0.17f, 0.12f, 1f);
    [SerializeField] private Color metalColor = new Color(0.69f, 0.8f, 0.85f, 1f);
    [SerializeField] private Color goldColor = new Color(0.94f, 0.75f, 0.32f, 1f);

    // A/S/H = sky-blue cloth, Y/L = golden hair, F/N = skin, E = eyes.
    // Keep the same two-pixel boots, pivot and body size as the other characters.
    private static readonly string[] Front = {
        "........HH......",
        "......HHAAAA....",
        ".....HAAAAAS....",
        ".....YYYYYY.....",
        "...FYYFFFFYYF...",
        "....YFEFFEFY....",
        ".....NFFFFN.....",
        "....HAOOOAAH....",
        "....FFAABAFF....",
        "....FFBBTBFF....",
        "....SAAOAAAS....",
        ".....HAAAAH.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Back = {
        "......HH........",
        "....AAAAHH......",
        "....SAAAAAH.....",
        ".....AAAYYY.....",
        ".....SAAYYY.....",
        "......SAYYY.....",
        ".....NYYYYN.....",
        "....HAABAAAH....",
        "....FFAABAFF....",
        "....FFBBBBFF....",
        "....SAAOAAAS....",
        ".....HAAAAH.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Left = {
        ".......HH.......",
        ".....HHAAAA.....",
        "....HAAAAAAS....",
        "....LYYYYYAS....",
        "...FFFYYYYY.....",
        "....FEFYYFY.....",
        "....NFFYYN......",
        ".....OOAAAA.....",
        ".....AAFFAS.....",
        ".....BBFFBB.....",
        "....SAAOAAAS....",
        ".....HAAAAH.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Right = {
        ".......HH.......",
        ".....AAAAHH.....",
        "....SAAAAAAH....",
        "....SAYYYYYL....",
        ".....YYYYYFFF...",
        ".....YFYYFEF....",
        "......NYYFFN....",
        ".....AAAAOO.....",
        ".....SAFFAA.....",
        ".....BBFFBB.....",
        "....SAAOAAAS....",
        ".....HAAAAH.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] ShieldFront = { "QQQQQ", "QCCCQ", "QCTCQ", "QCDCQ", ".QDQ.", "..Q.." };
    private static readonly string[] ShieldBack = { "QQQQQ", "QBBBQ", "QBTBQ", "QBBBQ", ".QBQ.", "..Q.." };

    protected override Sprite CreateSprite(int direction, int pose)
    {
        string[] rows = direction == 1 ? Back : direction == 2 ? Left : direction == 3 ? Right : Front;
        var pixels = new Color[16 * 16];
        for (int row = 0; row < 16; row++)
        for (int x = 0; x < 16; x++)
        {
            if (rows[row][x] == '.') continue;
            int y = 15 - row;
            if (row >= 12 && (pose == 1 || pose == 2) && (x < 8) == (pose == 1)) y++;
            pixels[y * 16 + x] = PixelColor(rows[row][x]);
        }

        DrawScabbard(pixels, direction, pose != 3);
        if (pose == 3) DrawReachingArm(pixels, direction);
        DrawShield(pixels, direction);

        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) {
            name = "Hero " + direction + " " + pose,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply(); // Keep readable for ghost-form recoloring and item poses.
        var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(.5f, .25f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private void DrawShield(Color[] pixels, int direction)
    {
        if (direction == 3)
        {
            // Far arm: a slim shield edge, rather than another frontal shield.
            for (int y = 4; y <= 8; y++) pixels[y * 16 + 4] = metalColor;
            for (int y = 4; y <= 7; y++) pixels[y * 16 + 5] = base.PixelColor('D');
            return;
        }
        string[] rows = direction == 1 ? ShieldBack : ShieldFront;
        int left = direction == 1 ? 9 : direction == 2 ? 6 : 2;
        for (int row = 0; row < rows.Length; row++)
        for (int x = 0; x < 5; x++)
            if (rows[row][x] != '.') pixels[(8 - row) * 16 + left + x] = PixelColor(rows[row][x]);
    }

    private void DrawScabbard(Color[] pixels, int direction, bool sheathed)
    {
        int x = direction == 0 ? 12 : direction == 1 ? 3 : direction == 2 ? 11 : 3;
        for (int y = 3; y <= 7; y++) pixels[y * 16 + x] = base.PixelColor('D');
        pixels[2 * 16 + x] = metalColor;
        if (!sheathed) return;
        for (int y = 9; y <= 10; y++) pixels[y * 16 + x] = leatherColor;
        for (int side = -1; side <= 1; side++) pixels[8 * 16 + x + side] = metalColor;
    }

    private void DrawReachingArm(Color[] pixels, int direction)
    {
        // Remove the sword arm at rest before extending it into the shared
        // sword-effect grip; the shield arm remains in place.
        if (direction < 2)
        {
            int outer = direction == 0 ? 11 : 4;
            int inner = direction == 0 ? 10 : 5;
            for (int y = 6; y <= 8; y++)
            {
                pixels[y * 16 + outer] = Color.clear;
                pixels[y * 16 + inner] = PixelColor(y == 6 ? 'B' : 'S');
            }
        }
        else
        {
            for (int y = 6; y <= 7; y++)
            for (int x = 7; x <= 8; x++) pixels[y * 16 + x] = PixelColor(y == 6 ? 'B' : 'A');
        }
        int startX = direction == 2 ? 1 : direction == 3 ? 11 : 6;
        int startY = direction == 1 ? 10 : direction == 0 ? 4 : 6;
        for (int y = startY; y < startY + 3; y++)
        for (int x = startX; x < startX + 4; x++) pixels[y * 16 + x] = PixelColor(y == startY + 2 ? 'H' : 'A');
        int handX = direction == 2 ? startX : direction == 3 ? startX + 2 : startX + 1;
        int handY = direction == 1 ? startY + 1 : startY;
        for (int y = handY; y < handY + 2; y++)
        for (int x = handX; x < handX + 2; x++) pixels[y * 16 + x] = faceColor;
    }

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'Y': return hairColor;
            case 'L': return Color.Lerp(hairColor, Color.white, .25f);
            case 'F': return faceColor;
            case 'N': return Color.Lerp(faceColor, Color.black, .18f);
            case 'E': return GhostFormEyeColor;
            case 'O': return collarColor;
            case 'B': return leatherColor;
            case 'K': return bootsColor;
            case 'Q': return metalColor;
            case 'T': return goldColor;
            default: return base.PixelColor(symbol);
        }
    }
}
