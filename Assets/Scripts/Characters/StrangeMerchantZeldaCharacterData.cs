using UnityEngine;
using UnityEngine.Serialization;

/// <summary>A hooded oddities trader; shares the civilian movement, possession and combat rig.</summary>
public sealed class StrangeMerchantZeldaCharacterData : CivilianZeldaCharacterData
{
    [Header("Strange Merchant / 奇怪的商人")]
    [SerializeField] private Color hoodColor = new Color(0.27f, 0.21f, 0.34f, 1f);
    [FormerlySerializedAs("maskColor")]
    [SerializeField] private Color trinketColor = new Color(0.82f, 0.81f, 0.66f, 1f);
    [SerializeField] private Color patchColor = new Color(0.36f, 0.45f, 0.39f, 1f);
    [SerializeField] private Color trimColor = new Color(0.68f, 0.52f, 0.28f, 1f);
    [SerializeField] private Color leatherColor = new Color(0.49f, 0.28f, 0.16f, 1f);

    // Keep the crooked hood and patched coat. A shadow-black face with paired
    // orange eyes replaces the mask; both profiles use mirrored facial pixels.
    // Preserve the shared 16 PPU hand/foot locations for possession and attacks.
    private static readonly string[] Front = {
        "........HHH.....",
        "......HHJJHH....",
        "....HHJJHHHQ....",
        "....QNENNENQ....",
        "....QNNNNNNQ....",
        "....PCTTCCDD....",
        "....PDCMCTDC....",
        "....PLTCCTLL....",
        "....FFACCTFF....",
        "....FFAGBBFF....",
        "....PCTCTCDD....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] Back = {
        ".....HHH........",
        "....HHJJHH......",
        "....QHHHJJHH....",
        "....QHHHHHHQ....",
        "....QQHQHHQQ....",
        "....DDCCTTCP....",
        "....CDCTTCDP....",
        "....LLCTTCLP....",
        "....FFCCCAFF....",
        "....FFBBGAFF....",
        "....DDCTCTCP....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] Left = {
        ".......HHH......",
        ".....HHJJHH.....",
        "...HHJJHHHQ.....",
        "....NENHHHQ.....",
        "....NNNQHHQ.....",
        ".....TCCCQD.....",
        ".....TMPCCD.....",
        ".....TCLLPD.....",
        ".....ACFFCD.....",
        ".....GBFFBB.....",
        ".....TDPCDD.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    private static readonly string[] Right = {
        ".....HHH........",
        "....HHJJHH......",
        ".....QHHHJJHH...",
        ".....QHHHNEN....",
        ".....QHHQNNN....",
        ".....DQCCCT.....",
        ".....DCCPMT.....",
        ".....DPLLCT.....",
        ".....DCFFCT.....",
        ".....BBFFBB.....",
        ".....DDCCTP.....",
        ".....KK..KK.....",
        ".....KK..KK.....",
        "................",
        "................",
        "................"
    };

    protected override string[] GetBodyRows(Vector2 facing) =>
        facing == Vector2.up ? Back : facing == Vector2.left ? Left :
        facing == Vector2.right ? Right : Front;

    protected override Color PixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'H': return hoodColor;
            case 'J': return Color.Lerp(hoodColor, Color.white, 0.18f);
            case 'Q': return Color.Lerp(hoodColor, Color.black, 0.30f);
            case 'M': return trinketColor;
            case 'N': return new Color(0.07f, 0.10f, 0.14f, 1f);
            case 'E': return new Color(1f, 0.65f, 0.20f, 1f);
            case 'P': return patchColor;
            case 'T': return trimColor;
            case 'A': return leatherColor;
            case 'G': return Color.Lerp(leatherColor, trimColor, 0.65f);
            case 'B': return Color.Lerp(leatherColor, Color.black, 0.25f);
            default: return base.PixelColor(symbol);
        }
    }
}
