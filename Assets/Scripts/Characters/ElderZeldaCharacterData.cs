using UnityEngine;

/// <summary>Bald, white-bearded tutorial elder in a plain red robe; uses the existing 16-PPU character rig.</summary>
public sealed class ElderZeldaCharacterData : PriestZeldaCharacterData
{
    public override bool IsImmuneToDamage => true;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Rock;

    // T is the inherited vestment colour, used here for the white hair and beard.
    private static readonly string[] Front = {
        "................",
        "......FFFF......",
        ".....FFFFFF.....",
        "....TFEFFEFT....",
        "....TTFFFFTT....",
        "....LTTTTTTL....",
        "...LAATTTTAAL...",
        "...LAAATTAAAL...",
        "...LFFAAAAFFL...",
        "...SFFAAAAFFS...",
        "....LAAGGAAL....",
        "....SASAASAS....",
        "....SSSSSSSS....",
        ".....KK..KK.....",
        "................",
        "................"
    };
    private static readonly string[] Back = {
        "................",
        "......FFFF......",
        ".....FFFFFF.....",
        "....TTFFFFTT....",
        "....TTTTTTTT....",
        "....LAAAAAAL....",
        "...LAAAAAAAAL...",
        "...LAAASSAAAL...",
        "...LFFASSAFFL...",
        "...SFFASSAFFS...",
        "....LAAGGAAL....",
        "....SASAASAS....",
        "....SSSSSSSS....",
        ".....KK..KK.....",
        "................",
        "................"
    };
    private static readonly string[] Left = {
        "................",
        "......FFFF......",
        ".....FFFFFF.....",
        "....FEFFFTT.....",
        "...FFFFTTTT.....",
        "....TTTTTAAL....",
        "....TTTTAAAS....",
        ".....TTAAAAS....",
        ".....LAFFAAS....",
        ".....SAFFAAS....",
        ".....AAGGAAS....",
        ".....SAASAAS....",
        ".....SSSSSSS....",
        ".....KK..KK.....",
        "................",
        "................"
    };
    private static readonly string[] Right = Mirror(Left);
    protected override string[] GetBodyRows(int direction) => direction == 1 ? Back : direction == 2 ? Left : direction == 3 ? Right : Front;

    private static string[] Mirror(string[] rows)
    {
        var result = new string[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            char[] row = rows[i].ToCharArray();
            System.Array.Reverse(row); result[i] = new string(row);
        }
        return result;
    }
}
