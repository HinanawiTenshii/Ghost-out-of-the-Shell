using UnityEngine;

/// <summary>Light desert scout on the shared 16 PPU humanoid rig, with configurable cloth and leather.</summary>
public sealed class DesertScoutZeldaCharacterData : CivilianZeldaCharacterData
{
    [Header("Scout Equipment / 沙漠斥候装束")]
    [Tooltip("短衣主色使用上方 Clothing Color；头巾、披肩和皮具可分别调整。")]
    [SerializeField] private Color headclothColor = new Color(.80f, .70f, .49f, 1f);
    [SerializeField] private Color cloakColor = new Color(.18f, .55f, .40f, 1f);
    [SerializeField] private Color leatherColor = new Color(.40f, .22f, .11f, 1f);
    [SerializeField] private Color buckleColor = new Color(.73f, .79f, .79f, 1f);

    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Sword;
    public override Color GhostFormEyeColor => new Color(.035f, .065f, .075f, 1f);

    // Wide wrapped head, two isolated eyes, covered mouth, light asymmetric
    // shoulder cape and a diagonal leather strap. Short boots retain the exact
    // civilian walking rig; hands remain at its four-way attack attachment points.
    private static readonly string[] ScoutFront = {
        "......JJJJ......",
        ".....WJJJJW.....",
        ".....WWQQWW.....",
        ".....FEFFEF.....",
        ".....WQQQQW.....",
        "....VVCCCCLL....",
        "....VUPCCCLC....",
        "....VVCPCCLL....",
        "....FFCCMPFF....",
        "....FFBPPBFF....",
        ".....CDCCDC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] ScoutBack = {
        "......JJJJ......",
        ".....WJJJJW.....",
        ".....WWQQWW.....",
        ".....QWWWWQ.....",
        ".....QQWWQQ.....",
        "....VTTVVTCC....",
        "....VTVUVVDC....",
        "....VTVUVVLL....",
        "....FFVUVVFF....",
        "....FFVVUBFF....",
        ".....CDCCDC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] ScoutLeft = {
        ".....JJJJ.......",
        "....WJJJJW......",
        "....WWQQWW......",
        "....FEFWWW......",
        "....WQQQWQ......",
        ".....CCCVVT.....",
        ".....PCCVUU.....",
        ".....CCLLVV.....",
        ".....CCFFVV.....",
        ".....BBFFPV.....",
        ".....CCCCDU.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] ScoutRight = {
        ".......JJJJ.....",
        "......WJJJJW....",
        "......WWQQWW....",
        "......WWWFEF....",
        "......QWQQQW....",
        ".....TVVCCC.....",
        ".....UUVCCP.....",
        ".....VVLLCC.....",
        ".....VVFFCC.....",
        ".....VPFFBB.....",
        ".....UDCCCC.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    protected override string[] GetBodyRows(Vector2 facing)
        => facing == Vector2.up ? ScoutBack : facing == Vector2.left ? ScoutLeft :
            facing == Vector2.right ? ScoutRight : ScoutFront;

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'W': return headclothColor;
            case 'J': return Color.Lerp(headclothColor, Color.white, .25f);
            case 'Q': return Color.Lerp(headclothColor, new Color(.36f, .26f, .15f), .30f);
            case 'V': return cloakColor;
            case 'T': return Color.Lerp(cloakColor, Color.white, .20f);
            case 'U': return Color.Lerp(cloakColor, Color.black, .25f);
            case 'P': return leatherColor;
            case 'B': return Color.Lerp(leatherColor, Color.black, .22f);
            case 'K': return Color.Lerp(leatherColor, Color.black, .35f);
            case 'M': return buckleColor;
            default: return base.PixelColor(symbol);
        }
    }
}
