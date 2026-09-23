using UnityEngine;

/// <summary>Desert merchant artwork using civilian movement, combat and editable clothing.</summary>
public sealed class DesertMerchantZeldaCharacterData : CivilianZeldaCharacterData
{
    [Header("Merchant Accessories / 商人配饰")]
    [Tooltip("服装主色使用上方 Configurable Clothing / Clothing Color；配饰独立着色。")]
    [SerializeField] private Color headclothColor = new Color(0.87f, 0.84f, 0.72f, 1f);
    [SerializeField] private Color trimColor = new Color(0.87f, 0.63f, 0.20f, 1f);
    [SerializeField] private Color leatherColor = new Color(0.49f, 0.28f, 0.13f, 1f);

    // Same 16px rig as the residents: no neck, short boots and central profile hands.
    // Cream wrap, gold-edged coat and a leather coin pouch distinguish the merchant.
    private static readonly string[] MerchantFront = {
        "......JJJJ......",
        ".....WJJJJW.....",
        ".....WWTTWW.....",
        ".....FEFFEF.....",
        ".....SFFFFS.....",
        "....CCTIITCC....",
        "....CDTCCTDC....",
        "....LLTCCTLL....",
        "....FFAPCTFF....",
        "....FFPPBBFF....",
        ".....CDTTDC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] MerchantBack = {
        "......JJJJ......",
        ".....WJJJJW.....",
        ".....WWTTWW.....",
        ".....WWWWWW.....",
        ".....QWWWWQ.....",
        "....CCTTTTCC....",
        "....CDCCCCDC....",
        "....LLCDCCLL....",
        "....FFCDCCFF....",
        "....FFBBBBFF....",
        ".....CDCCDC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] MerchantLeft = {
        ".....JJJJ.......",
        "....WJJJJW......",
        "....WWTTWW......",
        "....FEFWWW......",
        "....SFFHWQ......",
        ".....TCCCTD.....",
        ".....TCCCCD.....",
        ".....TCLLCD.....",
        ".....TCFFCD.....",
        ".....BBFFBB.....",
        ".....TDCCDD.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] MerchantRight = {
        ".......JJJJ.....",
        "......WJJJJW....",
        "......WWTTWW....",
        "......WWWFEF....",
        "......QWHFFS....",
        ".....DTCCCT.....",
        ".....DCCCCT.....",
        ".....DCLLCT.....",
        ".....PAFFCT.....",
        ".....PPFFBB.....",
        ".....DDCCDT.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    protected override string[] GetBodyRows(Vector2 facing)
    {
        return facing == Vector2.up ? MerchantBack : facing == Vector2.left ? MerchantLeft :
            facing == Vector2.right ? MerchantRight : MerchantFront;
    }

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            // Separate one-pixel eyes: no dark beard pixels directly below them.
            case 'E': return new Color(0.025f, 0.055f, 0.09f, 1f);
            case 'W': return headclothColor;
            case 'J': return Color.Lerp(headclothColor, Color.white, 0.18f);
            case 'Q': return Color.Lerp(headclothColor, Color.black, 0.22f);
            case 'I': return headclothColor;
            case 'T': return trimColor;
            case 'P': return leatherColor;
            case 'A': return Color.Lerp(leatherColor, trimColor, 0.55f);
            case 'B': return Color.Lerp(leatherColor, Color.black, 0.25f);
            default: return base.PixelColor(symbol);
        }
    }
}
