using UnityEngine;

/// <summary>Black-market craft worker: low hood, amber work goggles, face wrap and patched leather apron.</summary>
public sealed class StrangeBlacksmithZeldaCharacterData : BlacksmithZeldaCharacterData
{
    [Header("Black-Market Workwear / 黑市工匠服饰")]
    [SerializeField] private Color hoodColor = new Color(.24f, .27f, .32f, 1f);
    [SerializeField] private Color faceWrapColor = new Color(.10f, .13f, .17f, 1f);
    [SerializeField] private Color goggleColor = new Color(.96f, .66f, .27f, 1f);
    [SerializeField] private Color patchColor = new Color(.43f, .24f, .22f, 1f);
    [SerializeField] private Color rivetColor = new Color(.66f, .49f, .28f, 1f);
    public override Color GhostFormEyeColor => goggleColor;

    // Retain the existing 16 PPU smith rig, compact feet and hammer/raised-hand attachment points.
    private static readonly string[] Front = {
        "......HHHH......",
        ".....JHHHHH.....",
        "....QHHHHHHQ....",
        "....QMEMMEMQ....",
        ".....QVVVVQ.....",
        "....CCAAAACC....",
        "....CUATLADC....",
        "....GGAAKAGG....",
        "....GGAALAGG....",
        "....GGBBTBGG....",
        ".....DAXAAD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };
    private static readonly string[] Back = {
        "......HHHH......",
        ".....HHHHJH.....",
        "....QHHHHHHQ....",
        "....QHHHHHHQ....",
        ".....QQVVQQ.....",
        "....CCACCACC....",
        "....CUCTTCUC....",
        "....GGCAACGG....",
        "....GGCCXCGG....",
        "....GGBBTBGG....",
        ".....DCCXCD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };
    private static readonly string[] Left = {
        ".....HHHH.......",
        "....JHHHHH......",
        "...QHHHHHHQ.....",
        "....MEMHHHQ.....",
        "....VVVQHHQ.....",
        ".....AACCCC.....",
        ".....ATLCCD.....",
        ".....AAGGCD.....",
        ".....AKGGCD.....",
        ".....BBGGBT.....",
        ".....AXCCCD.....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };
    private static readonly string[] Right = MirrorRows(Left);
    private static string[] MirrorRows(string[] source)
    {
        var result = new string[source.Length];
        for (int i = 0; i < source.Length; i++)
        {
            var row = source[i].ToCharArray(); System.Array.Reverse(row); result[i] = new string(row);
        }
        return result;
    }
    protected override string[] GetBodyRows(Vector2 facing) => facing == Vector2.up ? Back :
        facing == Vector2.left ? Left : facing == Vector2.right ? Right : Front;
    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'H': return hoodColor;
            case 'J': return Color.Lerp(hoodColor, Color.white, .19f);
            case 'Q': return Color.Lerp(hoodColor, Color.black, .28f);
            case 'V': return faceWrapColor;
            case 'E': return goggleColor;
            case 'X': return patchColor;
            case 'T': return rivetColor;
            case 'K': return Color.Lerp(base.PixelColor('A'), Color.black, .38f);
            default: return base.PixelColor(symbol);
        }
    }
}
