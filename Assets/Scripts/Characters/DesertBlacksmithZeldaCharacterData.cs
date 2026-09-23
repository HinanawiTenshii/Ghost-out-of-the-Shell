using UnityEngine;

/// <summary>Level 2 fortress smith: wrapped headcloth, rolled sleeves and leather apron.</summary>
public sealed class DesertBlacksmithZeldaCharacterData : BlacksmithZeldaCharacterData
{
    [Header("Desert Workwear / 沙漠工匠服饰")]
    [SerializeField] private Color headclothColor = new Color(0.61f, 0.60f, 0.51f, 1f);
    [SerializeField] private Color headclothHighlight = new Color(0.77f, 0.74f, 0.62f, 1f);
    [SerializeField] private Color headclothShadow = new Color(0.40f, 0.41f, 0.35f, 1f);
    [SerializeField] private Color eyeColor = new Color(0.08f, 0.075f, 0.06f, 1f);

    public override Color GhostFormEyeColor => eyeColor;

    // Same 16x16 grid, short feet, central profile hands and hammer attachment
    // points as the original smith. J/W/Q add a compact cloth wrap, not a cape.
    private static readonly string[] DesertFront = {
        "......JJJJ......",
        ".....WJJWWW.....",
        "....WWWWWWWW....",
        ".....FEFFEF.....",
        ".....SHHHHS.....",
        "....CCAAAACC....",
        "....CUALLAUC....",
        "....FFAAAAFF....",
        "....GGADLAGG....",
        "....GGBBBBGG....",
        ".....DAAAAD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };
    private static readonly string[] DesertBack = {
        "......JJJJ......",
        ".....WWWJJW.....",
        "....WWWWWWWW....",
        ".....QWWWWQ.....",
        ".....SQQQQS.....",
        "....CCACCACC....",
        "....CUCAACUC....",
        "....FFCAACFF....",
        "....GGCCCCGG....",
        "....GGBBBBGG....",
        ".....DCCCCD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };
    private static readonly string[] DesertLeft = {
        ".....JJJJ.......",
        "....WJJWWW......",
        "...WWWWWWWW.....",
        "....FEFWWW......",
        "....SHHHQS......",
        ".....AACCCC.....",
        ".....ALCCCD.....",
        ".....AAFFCD.....",
        ".....AAGGCD.....",
        ".....BBGGBB.....",
        ".....AACCCD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };
    private static readonly string[] DesertRight = MirrorRows(DesertLeft);

    private static string[] MirrorRows(string[] source)
    {
        var result = new string[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            char[] row = source[i].ToCharArray();
            System.Array.Reverse(row);
            result[i] = new string(row);
        }
        return result;
    }

    protected override string[] GetBodyRows(Vector2 facing)
    {
        return facing == Vector2.up ? DesertBack : facing == Vector2.left ? DesertLeft :
            facing == Vector2.right ? DesertRight : DesertFront;
    }

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'W': return headclothColor;
            case 'J': return headclothHighlight;
            case 'Q': return headclothShadow;
            case 'E': return eyeColor;
            default: return base.PixelColor(symbol);
        }
    }
}
