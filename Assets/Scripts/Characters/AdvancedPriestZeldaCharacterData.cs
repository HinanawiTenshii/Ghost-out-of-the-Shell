using UnityEngine;

/// <summary>Crimson-and-gold cleric with a three-tier tiara. Inherits the priest's book attack and body rig.</summary>
public sealed class AdvancedPriestZeldaCharacterData : PriestZeldaCharacterData
{
    // Five extra head rows, still 16 PPU. G/T form three progressively wider
    // gold circlets, separated by red velvet; the small cross caps the crown.
    private static readonly string[] Front = {
        ".......TT.......",
        "......TTTT......",
        ".......TT.......",
        "......GTTG......",
        ".....GALLAG.....",
        ".....GTTTTG.....",
        "....GALLLLAG....",
        "....GTTTTTTG....",
        ".....FEFFEF.....",
        ".....FFFFFF.....",
        "...LAGTTTTGAL...",
        "...SAGATTAGAS...",
        "....GGATTAGG....",
        "....FFATTAFF....",
        "....FFGTTGFF....",
        "....SAGTTGAS....",
        "....SGGGGGGS....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };
    private static readonly string[] Back = {
        ".......TT.......",
        "......TTTT......",
        ".......TT.......",
        "......GTTG......",
        ".....GALLAG.....",
        ".....GTTTTG.....",
        "....GALLLLAG....",
        "....GTTTTTTG....",
        ".....AASSAA.....",
        ".....GGGGGG.....",
        "...LAGTTTTGAL...",
        "...SAGATTAGAS...",
        "....GGATTAGG....",
        "....FFASSAFF....",
        "....FFASSAFF....",
        "....SASAASAS....",
        "....SGGGGGGS....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };
    private static readonly string[] Left = {
        "......TT........",
        ".....TTTT.......",
        "......TT........",
        ".....GTTG.......",
        "....GALLAG......",
        "....GTTTTG......",
        "....GALAAS......",
        "....GTTTTG......",
        "....FEFAAS......",
        "....FFFAAS......",
        "....LGTGGAS.....",
        "....SAGAAAS.....",
        ".....AGGGAS.....",
        ".....AGFFAS.....",
        ".....AGFFAS.....",
        ".....AGATAS.....",
        ".....SGGGGS.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................"
    };
    private static readonly string[] Right = Mirror(Left);
    private static string[] Mirror(string[] rows)
    {
        var result = new string[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i].ToCharArray();
            System.Array.Reverse(row);
            result[i] = new string(row);
        }
        return result;
    }
    protected override string[] GetBodyRows(int direction) =>
        direction == 1 ? Back : direction == 2 ? Left : direction == 3 ? Right : Front;
}
