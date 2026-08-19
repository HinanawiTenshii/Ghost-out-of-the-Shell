using UnityEngine;

/// <summary>Rounded pixel-art shell using the purple bomb palette.</summary>
public sealed class PurpleBombShellPickupItemVisual : PickupItemVisualBase
{
    [SerializeField] private Color shellColor =
        new Color(0.54f, 0.25f, 0.82f, 1f);
    [SerializeField] private Color shellShadowColor =
        new Color(0.31f, 0.12f, 0.52f, 1f);
    [SerializeField] private Color shellHighlightColor =
        new Color(0.72f, 0.43f, 0.94f, 1f);

    protected override string RuntimeSpriteName =>
        "Runtime Pixel Purple Bomb Shell";
    protected override bool UsesEmbeddedColors => true;

    protected override Color GetPixelColor(char pixel)
    {
        switch (pixel)
        {
            case 'P': return shellColor;
            case 'D': return shellShadowColor;
            case 'H': return shellHighlightColor;
            default: return Color.clear;
        }
    }

    protected override string[] GetPixelRows()
    {
        return new[]
        {
            ".....DDD.....",
            "....DPPPD....",
            "...DPPPPPD...",
            "..DPPHPPPPD..",
            ".DPPHPPPPPPD.",
            ".DPPPPPPPPPD.",
            ".DPPPPPPPPPD.",
            "..DPPPPPPPD..",
            "...DPPPPPD...",
            "....DDDDD...."
        };
    }
}
