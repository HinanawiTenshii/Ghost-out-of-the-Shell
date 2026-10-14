using UnityEngine;

/// <summary>Red crystal transmitter kit / compact hand remote, using the existing pixel item pipeline.</summary>
public sealed class MortarControlPickupItemVisual : PickupItemVisualBase
{
    [SerializeField] private bool remote;
    protected override string RuntimeSpriteName => remote ? "Mortar Remote" : "Mortar Control Device";
    protected override bool UsesEmbeddedColors => true;
    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color32(48, 59, 68, 255);
            case 'S': return new Color32(132, 149, 153, 255);
            case 'H': return new Color32(208, 218, 212, 255);
            case 'r': return new Color32(111, 43, 43, 255);
            case 'R': return new Color32(207, 65, 54, 255);
            case 'G': return new Color32(208, 163, 91, 255);
            default: return Color.clear;
        }
    }
    protected override string[] GetPixelRows() => remote ? new[] {
        "................", ".......HH.......", ".......SS.......", ".......SS.......",
        "....DDDDDDDD....", "...DHHHHHHHSD...", "...DSrrrrrrSD...", "...DSrRRRRrSD...",
        "...DSrRHHRrSD...", "...DSrRRRRrSD...", "...DSrrrrrrSD...", "...DSSSSSSSSD...",
        "...DSDDGGDDSD...", "...DSDDGGDDSD...", "....DDDDDDDD....", "................"
    } : new[] {
        "....................", ".........HH.........", ".........SS.........", "....HH...SS...HH....",
        "....SS...SS...SS....", "..DDDDDDDDDDDDDDDD..", ".DHHHHHHHHHHHHHHHSD.", ".DSrrrrrrrrrrrrrrSD.",
        ".DSrGGrrrrrrrrGGrSD.", ".DSrGGrrRRRRrrGGrSD.", ".DSrrrrRRHHRRrrrrSD.", ".DSrrrrRRHHRRrrrrSD.",
        ".DSrGGrrRRRRrrGGrSD.", ".DSrGGrrrrrrrrGGrSD.", ".DSSSSSSSSSSSSSSSSD.", "..DDDDDDDDDDDDDDDD..",
        "....SS........SS....", "....SS........SS....", "....................", "...................."
    };
}
