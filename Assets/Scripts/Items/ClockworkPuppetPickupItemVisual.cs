using UnityEngine;

/// <summary>
/// Layered bacteriophage-shaped clockwork puppet. Its segmented neck and tail
/// display remaining magic, while four articulated fibres grip mechanisms.
/// </summary>
public sealed class ClockworkPuppetPickupItemVisual : PickupItemVisualBase
{
    private const int PixelWidth = 37;
    private const int PixelHeight = 43;
    internal static readonly string[] PixelRows = BuildPixelRows();

    protected override string RuntimeSpriteName => "Runtime Clockwork Bacteriophage Puppet";
    protected override bool UsesEmbeddedColors => true;
    protected override string[] GetPixelRows() => PixelRows;
    protected override Color GetPixelColor(char pixel) => GetLayerColor(pixel);

    private static Color GetLayerColor(char pixel)
    {
        switch (pixel)
        {
            // This is the same grey/red family used by Behemoth. 'O' is a
            // soft armour shadow rather than a black contour, so the shape is
            // separated exclusively by neighbouring colour planes.
            case 'O': return new Color(0.322f, 0.35f, 0.35f, 1f);
            case 'D': return new Color(0.18f, 0.21f, 0.22f, 1f);
            case 'P': return new Color(0.46f, 0.50f, 0.50f, 1f);
            case 'M': return new Color(0.61f, 0.64f, 0.64f, 1f);
            case 'H': return new Color(0.74f, 0.77f, 0.77f, 1f);
            case 'S': return new Color(0.24f, 0.27f, 0.28f, 1f);
            case 'Y': return new Color(0.82f, 0.035f, 0.025f, 1f);
            case 'L': return new Color(0.852f, 0.209f, 0.201f, 1f);
            case 'R': return new Color(0.62f, 0.025f, 0.018f, 1f);
            default: return Color.clear;
        }
    }

    private static string[] BuildPixelRows()
    {
        char[,] pixels = new char[PixelHeight, PixelWidth];
        for (int y = 0; y < PixelHeight; y++)
            for (int x = 0; x < PixelWidth; x++)
                pixels[y, x] = '.';

        // Angular capsid with asymmetrical facet lighting.
        int[] left =  {16,15,14,13,12,11,10,9,9,9,9,9,9,9,10,11,13,15};
        int[] right = {20,21,22,23,24,25,26,27,27,27,27,27,27,27,26,25,23,21};
        for (int y = 0; y < left.Length; y++)
            DrawHeadSpan(pixels, y, left[y], right[y]);

        // The collar and five tail segments are the magic display. Alternating
        // red values keep every charged segment visually separate.
        DrawFilledSpan(pixels, 18, 15, 21, 'O', 'L');
        DrawFilledSpan(pixels, 19, 14, 22, 'O', 'Y');
        DrawFilledSpan(pixels, 20, 15, 21, 'O', 'R');
        for (int y = 21; y <= 30; y++)
        {
            int segment = (y - 21) / 2;
            DrawFilledSpan(pixels, y, 15, 21, 'O', segment % 2 == 0 ? 'Y' : 'R');
            Put(pixels, 16, y, 'L');
            Put(pixels, 20, y, 'R');
        }

        // Base plate and central injection spike.
        DrawFilledSpan(pixels, 31, 11, 25, 'O', 'M');
        DrawFilledSpan(pixels, 32, 8, 28, 'O', 'P');
        DrawFilledSpan(pixels, 33, 12, 24, 'O', 'D');
        for (int y = 34; y <= 40; y++)
            DrawFilledSpan(pixels, y, 17, 19, 'O', y % 2 == 0 ? 'H' : 'P');
        Put(pixels, 18, 41, 'O');

        // Four articulated bacteriophage fibres/claws. Two broad outer feet
        // stabilise the body while the shorter inner pair grips a lever stem.
        DrawFibre(pixels, 12, 32, 7, 29, 2, 42, true);
        DrawFibre(pixels, 15, 33, 12, 30, 10, 42, false);
        DrawFibre(pixels, 24, 32, 29, 29, 34, 42, false);
        DrawFibre(pixels, 21, 33, 24, 30, 26, 42, true);

        string[] rows = new string[PixelHeight];
        for (int y = 0; y < PixelHeight; y++)
        {
            char[] row = new char[PixelWidth];
            for (int x = 0; x < PixelWidth; x++) row[x] = pixels[y, x];
            rows[y] = new string(row);
        }
        return rows;
    }

    private static void DrawHeadSpan(char[,] pixels, int y, int left, int right)
    {
        for (int x = left; x <= right; x++)
        {
            char layer;
            if (x == left || x == right) layer = 'O';
            else if (x <= left + 1 || x >= right - 1) layer = 'D';
            else if (x < 18 && y < 11) layer = 'H';
            else if (x < 21) layer = 'M';
            else layer = 'P';
            Put(pixels, x, y, layer);
        }
    }

    private static void DrawFilledSpan(char[,] pixels, int y, int left, int right, char edge, char fill)
    {
        for (int x = left; x <= right; x++)
            Put(pixels, x, y, x == left || x == right ? edge : fill);
    }

    private static void DrawFibre(
        char[,] pixels, int startX, int startY, int jointX, int jointY,
        int endX, int endY, bool highlighted)
    {
        DrawLine(pixels, startX, startY, jointX, jointY, 'O', 1);
        DrawLine(pixels, jointX, jointY, endX, endY, 'O', 1);
        char inner = highlighted ? 'H' : 'P';
        DrawLine(pixels, startX, startY, jointX, jointY, inner, 0);
        DrawLine(pixels, jointX, jointY, endX, endY, inner, 0);
        Put(pixels, jointX, jointY, 'S');
        Put(pixels, endX, endY, 'O');
    }

    private static void DrawLine(char[,] pixels, int x0, int y0, int x1, int y1, char value, int thickness)
    {
        int dx = Mathf.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Mathf.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int error = dx + dy;
        while (true)
        {
            for (int oy = -thickness; oy <= thickness; oy++)
                for (int ox = -thickness; ox <= thickness; ox++)
                    Put(pixels, x0 + ox, y0 + oy, value);
            if (x0 == x1 && y0 == y1) break;
            int twiceError = error * 2;
            if (twiceError >= dy) { error += dy; x0 += sx; }
            if (twiceError <= dx) { error += dx; y0 += sy; }
        }
    }

    private static void Put(char[,] pixels, int x, int y, char value)
    {
        if (x >= 0 && x < PixelWidth && y >= 0 && y < PixelHeight)
            pixels[y, x] = value;
    }

    public static Sprite CreateRuntimeSprite(out Texture2D texture)
    {
        texture = new Texture2D(PixelWidth, PixelHeight, TextureFormat.RGBA32, false)
        {
            name = "Runtime Deployed Clockwork Bacteriophage Puppet",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        for (int row = 0; row < PixelHeight; row++)
        {
            int y = PixelHeight - 1 - row;
            for (int x = 0; x < PixelWidth; x++)
                texture.SetPixel(x, y, GetLayerColor(PixelRows[row][x]));
        }
        // Keep the deployed texture readable because its tail segments update
        // only when the visible energy step changes.
        texture.Apply(false, false);
        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, PixelWidth, PixelHeight),
            new Vector2(0.5f, 0.04f),
            PixelHeight);
        sprite.name = "Runtime Deployed Clockwork Bacteriophage Puppet";
        return sprite;
    }

    public static void ApplyRuntimeEnergy(Texture2D texture, float normalizedEnergy)
    {
        if (texture == null) return;
        int activeSegments = Mathf.Clamp(
            Mathf.CeilToInt(Mathf.Clamp01(normalizedEnergy) * 6f),
            0,
            6);
        for (int row = 18; row <= 30; row++)
        {
            int segment = row <= 20 ? 0 : 1 + (row - 21) / 2;
            bool active = segment < activeSegments;
            int textureY = PixelHeight - 1 - row;
            for (int x = 0; x < PixelWidth; x++)
            {
                char pixel = PixelRows[row][x];
                if (pixel != 'Y' && pixel != 'L' && pixel != 'R') continue;
                Color color;
                if (active)
                {
                    color = GetLayerColor(pixel);
                }
                else
                {
                    float shade = segment % 2 == 0 ? 0.46f : 0.36f;
                    if (pixel == 'L') shade += 0.11f;
                    else if (pixel == 'R') shade -= 0.07f;
                    color = new Color(shade, shade + 0.025f, shade + 0.03f, 1f);
                }
                texture.SetPixel(x, textureY, color);
            }
        }
        texture.Apply(false, false);
    }
}
