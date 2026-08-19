using UnityEngine;

/// <summary>
/// Shared colors for runtime HUD, menus, prompts and attribute overlays.
/// White texture pixels remain neutral masks and are tinted with these colors.
/// </summary>
public static class ZeldaUiPalette
{
    public static readonly Color Primary =
        new Color32(95, 248, 233, 255);

    public static readonly Color Ghost =
        new Color32(40, 130, 210, 255);
}
