using UnityEngine;

/// <summary>Senior engineer appearance; BuilderCharacterSkillData supplies its native construction skill.</summary>
public sealed class AdvancedEngineerZeldaCharacterData : EngineerZeldaCharacterData
{
    [Header("Advanced Engineer / 高级工程师")]
    [Tooltip("工具腰带与皮手套的颜色。外套使用 Apron Color，内衬使用 Shirt Color。其他配饰沿用工程师配置。")]
    [SerializeField] private Color toolBeltColor = new Color(0.40f, 0.24f, 0.12f, 1f);

    // Asymmetric wide cloth cap with a single side-mounted jeweller's loupe,
    // not the ordinary engineer's close-fitting cap and twin forehead goggles.
    // Light work coat, double brass fasteners, crossed back straps and a tool belt.
    // Keep the original 16 PPU foot/hand rig and inherit the same single wrench.
    private static readonly string[] AdvancedEngineerFront = {
        "...JJJJJJJ......",
        "..WJJJJWWWT.....",
        "....WWWWBTQT....",
        ".....FEFFET.....",
        ".....SFFFFS.....",
        "....AAVVVVAA....",
        "....ALTCCTLA....",
        "....FFATTAFF....",
        "....GGAOCAGG....",
        "....GGBTBBGG....",
        "....ABACCABA....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] AdvancedEngineerBack = {
        "......JJJJJJJ...",
        ".....TWWWJJJJW..",
        "....BTTBWWWW....",
        ".....HHHHHH.....",
        ".....SHHHHS.....",
        "....AAVVVVAA....",
        "....ALTATLAA....",
        "....FFATTAFF....",
        "....GGTAATGG....",
        "....GGBBBBGG....",
        "....ABALLABA....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] AdvancedEngineerLeft = {
        ".....JJJJJJJ....",
        "...WTJJJJWWW....",
        "...TQTWWWW......",
        "....TEFWHH......",
        "....SFFHHS......",
        ".....VVVAAA.....",
        ".....TCLAAL.....",
        ".....OCFFAA.....",
        ".....TCGGAL.....",
        ".....BTGGBB.....",
        "....ABCALABA....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };

    // The loupe belongs to one side only; the opposite profile shows its strap.
    private static readonly string[] AdvancedEngineerRight = {
        "....JJJJJJJ.....",
        "....WWWJJJJW....",
        "......WWBBBW....",
        "......HHWFEF....",
        "......SHHFFS....",
        ".....AAAVVV.....",
        ".....LAALCT.....",
        ".....AAFFCO.....",
        ".....LAGGCT.....",
        ".....BBGGTB.....",
        "....ABALACBA....",
        ".....PP..PP.....",
        ".....PP..PP.....",
        "................",
        "................",
        "................"
    };

    protected override string[] GetBodyRows(Vector2 facing)
    {
        return facing == Vector2.up ? AdvancedEngineerBack : facing == Vector2.left ? AdvancedEngineerLeft :
            facing == Vector2.right ? AdvancedEngineerRight : AdvancedEngineerFront;
    }

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'B': return toolBeltColor;
            case 'G': return Color.Lerp(toolBeltColor, Color.white, 0.18f);
            default: return base.PixelColor(symbol);
        }
    }
}
