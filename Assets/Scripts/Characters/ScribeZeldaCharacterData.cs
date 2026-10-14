using UnityEngine;

/// <summary>Level 2 clerk: cloth cap, long coat, parchment roll and leather document pouch.</summary>
public sealed class ScribeZeldaCharacterData : CivilianZeldaCharacterData
{
    [Header("Scribe Accessories / 书记官配饰")]
    [Tooltip("服装主色使用上方 Configurable Clothing / Clothing Color；配饰独立着色。")]
    [SerializeField] private Color capColor = new Color(0.31f, 0.19f, 0.48f, 1f);
    [SerializeField] private Color trimColor = new Color(0.86f, 0.80f, 0.65f, 1f);
    [SerializeField] private Color leatherColor = new Color(0.49f, 0.28f, 0.13f, 1f);

    [SerializeField] private Color parchmentColor = new Color(0.96f, 0.89f, 0.70f, 1f);
    [SerializeField] private Color sealColor = new Color(0.78f, 0.17f, 0.22f, 1f);

    // Same 16px rig as the residents: no neck, short boots and central profile hands.
    // A low cloth cap, pale collar, rolled parchment and document pouch distinguish the clerk.
    private static readonly string[] ScribeFront = {
        "......JJJJ......",
        ".....WJJJJW.....",
        ".....WTTTTW.....",
        ".....FEFFEF.....",
        ".....SFFFFS.....",
        "....CCTIITCC....",
        "....CDTCCPDC....",
        "....LLNOOPLL....",
        "....FFNNAPFF....",
        "....FFNRBBFF....",
        ".....CDTTDC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] ScribeBack = {
        "......JJJJ......",
        ".....WJJJJW.....",
        ".....WTTTTW.....",
        ".....QWHHWQ.....",
        ".....SHHHHS.....",
        "....CCTTTTCC....",
        "....CDPCCCDC....",
        "....LLCPCCLL....",
        "....FFCCPPFF....",
        "....FFBPABFF....",
        ".....CDCCDC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] ScribeLeft = {
        ".....JJJJ.......",
        "....WJJJJW......",
        "....WTTTTW......",
        "....FEFWHQ......",
        "....SFFHHS......",
        ".....TCCCTD.....",
        ".....TCCPCD.....",
        ".....NOOLCD.....",
        ".....NNFFCD.....",
        ".....NRFFBB.....",
        ".....TDCCDD.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] ScribeRight = {
        ".......JJJJ.....",
        "......WJJJJW....",
        "......WTTTTW....",
        "......QHWFEF....",
        "......SHHFFS....",
        ".....DTCCCT.....",
        ".....DCPCCT.....",
        ".....DPLLCT.....",
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
        return facing == Vector2.up ? ScribeBack : facing == Vector2.left ? ScribeLeft :
            facing == Vector2.right ? ScribeRight : ScribeFront;
    }

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            // Separate one-pixel eyes: no dark beard pixels directly below them.
            case 'E': return new Color(0.025f, 0.055f, 0.09f, 1f);
            case 'W': return capColor;
            case 'J': return Color.Lerp(capColor, Color.white, 0.18f);
            case 'Q': return Color.Lerp(capColor, Color.black, 0.22f);
            case 'I': return trimColor;
            case 'N': return parchmentColor;
            case 'O': return Color.Lerp(parchmentColor, leatherColor, 0.30f);
            case 'R': return sealColor;
            case 'T': return trimColor;
            case 'P': return leatherColor;
            case 'A': return Color.Lerp(leatherColor, trimColor, 0.55f);
            case 'B': return Color.Lerp(leatherColor, Color.black, 0.25f);
            default: return base.PixelColor(symbol);
        }
    }
}
