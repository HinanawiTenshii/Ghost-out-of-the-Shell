using UnityEngine;

/// <summary>Large red casing, steel caps and a simple plus mark, matching the mechanical props.</summary>
public sealed class BatteryPickupItemVisual : PickupItemVisualBase
{
    protected override void OnEnable()
    {
        // Old scene overrides must not send the pickup to the post-mask camera.
        if (gameObject.layer == LayerMask.NameToLayer("Visible Non Blocking")) gameObject.layer = 0;
        base.OnEnable();
        Transform breathingVisual = transform.Find("Pickup Breathing Visual");
        if (breathingVisual != null) breathingVisual.gameObject.layer = gameObject.layer;
    }

    protected override string RuntimeSpriteName => "Red Steel Battery";
    protected override bool UsesEmbeddedColors => true;
    protected override Color GetPixelColor(char pixel) => PixelColor(pixel);
    public static Color PixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'D': return new Color(.17f, .21f, .23f);
            case 'S': return new Color(.46f, .51f, .53f);
            case 'H': return new Color(.78f, .83f, .83f);
            case 'R': return new Color(.80f, .10f, .07f);
            case 'L': return new Color(1f, .28f, .17f);
            case 'B': return new Color(.42f, .06f, .05f);
            default: return Color.clear;
        }
    }
    protected override string[] GetPixelRows() => new[] {
        "................", "......HHHH......", "......SSSS......",
        "....HHHHHHHH....", "...HSSSSSSSSD...", "...HLRRRRRRBD...",
        "...HLRRHHRRBD...", "...HLRHHHHRBD...", "...HLRRHHRRBD...",
        "...HLRRRRRRBD...", "...HLRRRRRRBD...", "...HLRRRRRRBD...",
        "...HSSSSSSSSD...", "....DDDDDDDD....", "................", "................"
    };
}
