using UnityEngine;

/// <summary>Conical straw hat and cross-collar robe; inherits the paladin's sword combat contract.</summary>
public sealed class EasternSwordsmanZeldaCharacterData : PaladinZeldaCharacterData
{
    [Header("Eastern Swordsman / 斗笠剑客")]
    [Tooltip("长衫沿用 Armor Color / Shadow / Highlight；束带沿用 Cape Color。属性与攻击参数沿用高级圣骑士。")]
    [SerializeField] private Color strawColor = new Color(0.72f, 0.48f, 0.22f, 1f);
    [SerializeField] private Color faceColor = new Color(0.88f, 0.65f, 0.44f, 1f);
    [SerializeField] private Color hairColor = new Color(0.18f, 0.12f, 0.09f, 1f);
    [SerializeField] private Color collarColor = new Color(0.85f, 0.9f, 0.85f, 1f);
    [SerializeField] private Color scabbardColor = new Color(0.37f, 0.19f, 0.13f, 1f);
    [SerializeField] private Color metalColor = new Color(0.73f, 0.81f, 0.83f, 1f);
    [SerializeField] private Color bootsColor = new Color(0.22f, 0.16f, 0.12f, 1f);

    // Same 16 PPU and foot baseline as Paladin. Four stepped tiers make the
    // broad straw cone readable from every direction without increasing the body.
    // A/S/H = robe; W/J/Q = straw; O = collar; B = sash; F/N/E = face.
    private static readonly string[] Front = {
        ".......JJ.......",
        ".....JJWWWW.....",
        "...JJWWWQWWWW...",
        "..QQQQQQQQQQQQ..",
        ".....FEFFEF.....",
        ".....NFFFFN.....",
        "....AAOOOAAA....",
        "....HHAOAAHH....",
        "....FFAABAFF....",
        "....FFBBBBFF....",
        "....SAABBAAS....",
        "....SAHBHAAS....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Back = {
        ".......JJ.......",
        ".....WWWWJJ.....",
        "...WWWWQWWWJJ...",
        "..QQQQQQQQQQQQ..",
        ".....IIVVII.....",
        ".....NIIVIN.....",
        "....AAOVOAAA....",
        "....HHAVAAHH....",
        "....FFAHAAFF....",
        "....FFBBBBFF....",
        "....SAAHAAAS....",
        "....SASHSAAS....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Left = {
        "......JJ........",
        "....JJWWWW......",
        "..JJWWWQWWWW....",
        ".QQQQQQQQQQQQ...",
        "....FEFIII......",
        "....NFFIVI......",
        ".....OOAAVA.....",
        ".....OAHHAS.....",
        ".....AAFFAS.....",
        ".....BBFFBB.....",
        "....SAABHAAS....",
        "....SAAHSAAS....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    private static readonly string[] Right = {
        "........JJ......",
        "......WWWWJJ....",
        "....WWWWQWWWJJ..",
        "...QQQQQQQQQQQQ.",
        "......IIIFEF....",
        "......IVIFFN....",
        ".....AVAAOO.....",
        ".....SAHHAO.....",
        ".....SAFFAA.....",
        ".....BBFFBB.....",
        "....SAAHBAAS....",
        "....SAASHAAS....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };

    protected override Sprite CreateSprite(int direction, int pose)
    {
        string[] map = direction == 1 ? Back : direction == 2 ? Left : direction == 3 ? Right : Front;
        var pixels = new Color[16 * 16];
        for (int row = 0; row < 16; row++)
        for (int x = 0; x < 16; x++)
        {
            char symbol = map[row][x];
            if (symbol == '.') continue;
            int y = 15 - row;
            // Lift only a boot, not the hem, hat or collision body.
            if (row >= 12 && (pose == 1 || pose == 2) && (x < 8) == (pose == 1)) y++;
            pixels[y * 16 + x] = PixelColor(symbol);
        }

        DrawScabbard(pixels, direction, pose != 3);
        if (pose == 3)
        {
            RestoreRestingArm(pixels, direction);
            // Match the common wide-sleeve sword/item gesture and its muzzle grip.
            int startX = direction == 2 ? 1 : direction == 3 ? 11 : 6;
            int startY = direction == 1 ? 10 : direction == 0 ? 4 : 6;
            for (int y = startY; y < startY + 3; y++)
            for (int x = startX; x < startX + 4; x++)
                pixels[y * 16 + x] = PixelColor(y == startY + 2 ? 'H' : 'A');
            int handX = direction == 2 ? startX : direction == 3 ? startX + 2 : startX + 1;
            int handY = direction == 1 ? startY + 1 : startY;
            for (int y = handY; y < handY + 2; y++)
            for (int x = handX; x < handX + 2; x++) pixels[y * 16 + x] = faceColor;
        }

        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) {
            name = "Eastern Swordsman " + direction + " " + pose,
            filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixels(pixels);
        texture.Apply(); // Ghost recoloring needs readable frames, including attack poses.
        var sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.25f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private void DrawScabbard(Color[] pixels, int direction, bool sheathed)
    {
        // One sword only. During attack the common sword effect is the drawn
        // blade; leave the empty scabbard on the belt, without a second hilt.
        int x = direction == 0 ? 12 : direction == 1 ? 3 : direction == 2 ? 11 : 4;
        for (int y = 3; y <= 6; y++) pixels[y * 16 + x] = scabbardColor;
        pixels[2 * 16 + x] = metalColor;
        pixels[6 * 16 + x] = metalColor;
        if (!sheathed) return;
        for (int y = 8; y <= 9; y++) pixels[y * 16 + x] = base.PixelColor('C');
        for (int side = -1; side <= 1; side++) pixels[7 * 16 + x + side] = metalColor;
    }

    private void RestoreRestingArm(Color[] pixels, int direction)
    {
        if (direction < 2)
        {
            int outerX = direction == 0 ? 4 : 11;
            int innerX = direction == 0 ? 5 : 10;
            for (int y = 6; y <= 8; y++)
            {
                pixels[y * 16 + outerX] = Color.clear;
                pixels[y * 16 + innerX] = PixelColor(y == 6 ? 'B' : 'S');
            }
        }
        else
        {
            for (int y = 6; y <= 8; y++)
            for (int x = 7; x <= 8; x++) pixels[y * 16 + x] = PixelColor(y == 6 ? 'B' : 'A');
        }
    }

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'W': return strawColor;
            case 'J': return Color.Lerp(strawColor, Color.white, 0.25f);
            case 'Q': return Color.Lerp(strawColor, new Color(0.30f, 0.17f, 0.08f, 1f), 0.35f);
            case 'F': return faceColor;
            case 'N': return Color.Lerp(faceColor, Color.black, 0.2f);
            case 'E': return GhostFormEyeColor;
            case 'I': return hairColor;
            case 'V': return Color.Lerp(hairColor, Color.white, 0.12f);
            case 'O': return collarColor;
            case 'B': return base.PixelColor('C');
            case 'K': return bootsColor;
            default: return base.PixelColor(symbol);
        }
    }
}
