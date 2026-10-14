using UnityEngine;

/// <summary>Level 2 civilian dancer. Shares the resident's four-way movement and interaction rig.</summary>
public sealed class DancerZeldaCharacterData : CivilianZeldaCharacterData
{
    [Header("Dancer Accessories / 舞女配饰")]
    [Tooltip("舞裙和上衣使用 Configurable Clothing 中的 Clothing Color；飘带与金属饰品可单独调整。")]
    [SerializeField] private Color sashColor = new Color(0.10f, 0.79f, 0.69f, 1f);
    [SerializeField] private Color jewelryColor = new Color(1f, 0.73f, 0.22f, 1f);

    // 16 PPU, the same short-foot rig and baseline as other civilians. The flared
    // skirt, asymmetric sash, headband and bracelets distinguish the dancer.
    // Front eyes stay separated; the face is not covered by a dark horizontal bar.
    private static readonly string[] DancerFront = {
        "......HHHH......",
        ".....HAAAHH.....",
        ".....TTJVTT.....",
        "....HFEFFEFH....",
        "....TSFFFFST....",
        "....CCTIITCC....",
        "....LCLCCLCL....",
        "....TTTBBTTT....",
        "....FFCVVCFF....",
        "....FFLCVDFF....",
        "...DCLCCVCLCD...",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] DancerBack = {
        "......HHHH......",
        ".....HAAAHH.....",
        ".....TTVVTT.....",
        "....HAAAHHHH....",
        "....THAAHHHT....",
        "....CCHAAHCC....",
        "....LCDHHDCL....",
        "....TTTBBTTT....",
        "....FFVVCCFF....",
        "....FFDVCLFF....",
        "...DCLVCCCLCD...",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] DancerLeft = {
        ".....HHHH.......",
        "....HAAAHH......",
        "....TJVTTT......",
        "....FEFHHHH.....",
        "....SFFTHAH.....",
        ".....TCCAHH.....",
        ".....CLLCHH.....",
        ".....CCTTCD.....",
        ".....CCFFCD.....",
        "....DLVFFCCD....",
        "....DLVCCLCD....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    // Keep the sash on the same side of the body instead of mirroring it.
    private static readonly string[] DancerRight = {
        ".......HHHH.....",
        "......HAAAHH....",
        "......TTTVJT....",
        ".....HHHHFEF....",
        ".....HAHTFFS....",
        ".....HHACCT.....",
        ".....HHCLLC.....",
        ".....DCTTCC.....",
        ".....DCFFCC.....",
        "....DCCFFCLD....",
        "....DCLCCCLD....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    protected override string[] GetBodyRows(Vector2 facing)
    {
        return facing == Vector2.up ? DancerBack : facing == Vector2.left ? DancerLeft :
            facing == Vector2.right ? DancerRight : DancerFront;
    }

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'E': return new Color(0.025f, 0.055f, 0.09f, 1f);
            case 'V': return sashColor;
            case 'T': case 'B': return jewelryColor;
            case 'I': return Color.Lerp(jewelryColor, Color.white, 0.35f);
            case 'J': return Color.Lerp(sashColor, Color.white, 0.32f);
            case 'A': return Color.Lerp(base.PixelColor('H'), new Color(0.55f, 0.32f, 0.21f, 1f), 0.30f);
            case 'K': return Color.Lerp(base.PixelColor('C'), new Color(0.20f, 0.09f, 0.17f, 1f), 0.52f);
            default: return base.PixelColor(symbol);
        }
    }
}
