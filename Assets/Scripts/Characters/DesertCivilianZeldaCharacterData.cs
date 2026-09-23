using UnityEngine;

/// <summary>Desert resident sharing civilian gameplay and configurable clothing.</summary>
public sealed class DesertCivilianZeldaCharacterData : CivilianZeldaCharacterData
{
    [Header("Desert Headcloth / 沙漠居民头巾")]
    [Tooltip("勾选佩戴包裹式头巾；取消后显示头发。四向站立、行走和攻击同步更新。")]
    [SerializeField] private bool wearHeadcloth = true;
    [SerializeField] private Color headclothColor = new Color(0.8f, 0.79f, 0.73f, 1f);

    // Rounded wrap without a hat brim. The cloth end hangs from the character's
    // left side; profiles are deliberately not mirrored so it stays on that side.
    // Preserve the inherited foot rig and central profile hand/attack attachments.
    private static readonly string[] DesertFront = {
        "......JJJJ......",
        ".....WJJJJW.....",
        ".....WWQQWW.....",
        ".....FEFFEW.....",
        ".....SFFFFW.....",
        "....LLCCCQWL....",
        "....LLCCCJWL....",
        "....LLCCCWWL....",
        "....FFCCCWFF....",
        "....FFBBBBFF....",
        ".....CDCCDC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] DesertBack = {
        "......JJJJ......",
        ".....WJJJJW.....",
        ".....WWQQWW.....",
        ".....WWWWWW.....",
        ".....QWWWWQ.....",
        "....LWQCCCLL....",
        "....LWJCCDLL....",
        "....LWWDCCLL....",
        "....FFWCCCFF....",
        "....FFBBBBFF....",
        ".....CDCCDC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] DesertLeft = {
        ".....JJJJ.......",
        "....WJJJJW......",
        "....WWQQWW......",
        "....FEFWWW......",
        "....SFFFWW......",
        ".....CCCWWD.....",
        ".....CCCWJD.....",
        ".....CCLLWD.....",
        ".....CCFFWD.....",
        ".....BBFFBB.....",
        ".....CCCCDD.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] DesertRight = {
        ".......JJJJ.....",
        "......WJJJJW....",
        "......WWQQWW....",
        "......WWWFEF....",
        "......WWFFFS....",
        ".....QWCCCC.....",
        ".....DWCCCC.....",
        ".....DCLLCC.....",
        ".....DCFFCC.....",
        ".....BBFFBB.....",
        ".....DDCCCC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] BareFront = {
        "......HHHH......",
        ".....HHHHHH.....",
        ".....HFFFFH.....",
        ".....FEFFEF.....",
        ".....SFFFFS.....",
        "....LLCCCCLL....",
        "....LLCDCCLL....",
        "....LLCCCCLL....",
        "....FFCCCCFF....",
        "....FFBBBBFF....",
        ".....CDCCDC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] BareBack = {
        "......HHHH......",
        ".....HHHHHH.....",
        ".....HHHHHH.....",
        ".....HHHHHH.....",
        ".....SSHHSS.....",
        "....LLCCCCLL....",
        "....LLCDCCLL....",
        "....LLCDCCLL....",
        "....FFCCCCFF....",
        "....FFBBBBFF....",
        ".....CDCCDC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] BareLeft = {
        ".....HHHH.......",
        "....HHHHHH......",
        "....HFFFHH......",
        "....FEFFHH......",
        "....SFFFHS......",
        ".....CCCCCD.....",
        ".....CLCCCD.....",
        ".....CCLLCD.....",
        ".....CCFFCD.....",
        ".....BBFFBB.....",
        ".....CCCCDD.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] BareRight = MirrorRows(BareLeft);

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
        if (!wearHeadcloth)
            return facing == Vector2.up ? BareBack : facing == Vector2.left ? BareLeft :
                facing == Vector2.right ? BareRight : BareFront;

        return facing == Vector2.up ? DesertBack : facing == Vector2.left ? DesertLeft :
            facing == Vector2.right ? DesertRight : DesertFront;
    }

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'W': return headclothColor;
            case 'J': return Color.Lerp(headclothColor, Color.white, 0.18f);
            case 'Q': return Color.Lerp(headclothColor, Color.black, 0.22f);
            default: return base.PixelColor(symbol);
        }
    }
}
