using UnityEngine;

/// <summary>Fortress engineer: raised goggles, precision tools and a leather work apron.</summary>
public class EngineerZeldaCharacterData : BlacksmithZeldaCharacterData
{
    [Header("Engineer Accessories / 工程师配饰")]
    [Tooltip("工作服、围裙、手套沿用上方 Blacksmith Appearance 配置，Hammer Color 控制扳手金属色。")]
    [SerializeField] private Color capColor = new Color(0.16f, 0.37f, 0.41f, 1f);
    [SerializeField] private Color brassColor = new Color(0.83f, 0.62f, 0.28f, 1f);
    [SerializeField] private Color lensColor = new Color(0.4f, 0.89f, 0.94f, 1f);
    [SerializeField] private Color scarfColor = new Color(0.77f, 0.20f, 0.15f, 1f);
    [SerializeField] private Color toolColor = new Color(0.73f, 0.8f, 0.81f, 1f);

    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Wrench;

    // Raised twin lenses leave the two face pixels readable. The apron carries
    // a measuring rule and metal tools, sharing the smith's craft/skill cues.
    // Preserve the 16 PPU rig, two-row legs and the directional tool grip.
    private static readonly string[] EngineerFront = {
        "......JJJJ......",
        ".....WTTTTW.....",
        ".....TQTTQT.....",
        ".....FEFFEF.....",
        ".....SFFFFS.....",
        "....CCAVVACC....",
        "....CUALLAUC....",
        "....FFAOAAFF....",
        "....GGAOTAGG....",
        "....GGBBTBGG....",
        ".....DAMMAD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };
    private static readonly string[] EngineerBack = {
        "......JJJJ......",
        ".....WJWWJW.....",
        ".....WWWWWW.....",
        ".....BTTTTB.....",
        ".....SHHHHS.....",
        "....CCAVVACC....",
        "....CUACCAUC....",
        "....FFCAACFF....",
        "....GGCCCTGG....",
        "....GGBBABGG....",
        ".....DCOOAD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };
    private static readonly string[] EngineerLeft = {
        ".....JJJJ.......",
        "....WTTWWW......",
        "....TQTBBW......",
        "....FEFWHH......",
        "....SFFHHS......",
        ".....VACCAC.....",
        ".....ALCCAD.....",
        ".....OAFFCD.....",
        ".....OAGGCD.....",
        ".....BTGGBB.....",
        ".....AMCCAD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };
    private static readonly string[] EngineerRight = MirrorRows(EngineerLeft);

    private static string[] MirrorRows(string[] source)
    {
        var rows = new string[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            char[] row = source[i].ToCharArray();
            System.Array.Reverse(row);
            rows[i] = new string(row);
        }
        return rows;
    }

    protected override string[] GetBodyRows(Vector2 facing)
    {
        return facing == Vector2.up ? EngineerBack : facing == Vector2.left ? EngineerLeft :
            facing == Vector2.right ? EngineerRight : EngineerFront;
    }

    protected override void DrawHeldTool(Color[] pixels, Vector2 facing, bool attacking)
    {
        int x, y, dx, dy;
        if (attacking)
        {
            x = facing == Vector2.left ? 5 : facing == Vector2.right ? 10 : 7;
            y = facing == Vector2.up ? 10 : 7;
            dx = (int)facing.x; dy = (int)facing.y;
        }
        else
        {
            x = facing == Vector2.down ? 12 : facing == Vector2.up ? 3 :
                facing == Vector2.left ? 6 : 9;
            y = 5; dx = 0; dy = 1;
        }

        // A single steel shaft with an open U jaw, not a solid hammer head.
        // Tool coordinates rotate with the attack and always touch the glove.
        for (int depth = 0; depth < 3; depth++)
            PaintToolPixel(pixels, x + dx * depth, y + dy * depth, 'M');
        for (int side = -1; side <= 1; side++)
            PaintToolPixel(pixels, x + dx * 3 + dy * side, y + dy * 3 - dx * side, 'M');
        for (int depth = 4; depth <= 5; depth++)
        for (int side = -1; side <= 1; side += 2)
            PaintToolPixel(pixels, x + dx * depth + dy * side, y + dy * depth - dx * side,
                side > 0 ? 'N' : 'M');
    }

    private void PaintToolPixel(Color[] pixels, int x, int y, char symbol)
    {
        if (x >= 0 && x < 16 && y >= 0 && y < 16)
            pixels[y * 16 + x] = PixelColor(symbol);
    }

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'W': return capColor;
            case 'J': return Color.Lerp(capColor, Color.white, 0.22f);
            case 'T': return brassColor;
            case 'Q': return lensColor;
            case 'V': return scarfColor;
            case 'O': return toolColor;
            default: return base.PixelColor(symbol);
        }
    }
}
