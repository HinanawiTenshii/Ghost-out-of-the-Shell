using UnityEngine;

/// <summary>
/// Generates a leafless, branching pixel tree with a vertical sky-blue gradient.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelSkyBlueBranchTreeVisual : MonoBehaviour
{
    private const int TextureSize = 48;
    private const float PixelsPerUnit = 16f;
    private static readonly Vector3 EditorBoundsSize = new Vector3(3f, 3f, 0f);

    [SerializeField] private int sortingOrder = 1;

    private static Sprite treeSprite;

    private void Awake()
    {
        ApplyVisual();
    }

    private void OnEnable()
    {
        ApplyVisual();
    }

    private void ApplyVisual()
    {
        EnsureSprite();
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = treeSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = sortingOrder;
    }

    private static void EnsureSprite()
    {
        if (treeSprite != null)
        {
            return;
        }

        Texture2D texture = new Texture2D(
            TextureSize,
            TextureSize,
            TextureFormat.RGBA32,
            false);
        texture.name = "Sky Blue Gradient Branch Tree";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        FillRect(texture, 0, 0, TextureSize, TextureSize, new Color(1f, 1f, 1f, 0f));

        // Main trunk and its two dominant forks.
        DrawBranch(texture, 24, 1, 24, 14, 4);
        DrawBranch(texture, 24, 14, 17, 26, 3);
        DrawBranch(texture, 24, 14, 31, 27, 3);

        // Left crown: long, sparse and slightly cropped like the reference.
        DrawBranch(texture, 17, 26, 8, 29, 2);
        DrawBranch(texture, 8, 29, 1, 35, 2);
        DrawBranch(texture, 1, 35, 0, 41, 1);
        DrawBranch(texture, 8, 29, 4, 23, 1);
        DrawBranch(texture, 4, 23, 0, 20, 1);
        DrawBranch(texture, 17, 26, 19, 37, 2);
        DrawBranch(texture, 19, 37, 15, 45, 1);
        DrawBranch(texture, 15, 45, 13, 47, 1);
        DrawBranch(texture, 19, 37, 24, 44, 1);
        DrawBranch(texture, 24, 44, 24, 47, 1);
        DrawBranch(texture, 13, 28, 10, 36, 1);
        DrawBranch(texture, 10, 36, 5, 41, 1);
        DrawBranch(texture, 10, 36, 12, 44, 1);

        // Right crown: broader main branch with several thin upward forks.
        DrawBranch(texture, 31, 27, 40, 30, 2);
        DrawBranch(texture, 40, 30, 47, 36, 1);
        DrawBranch(texture, 40, 30, 45, 27, 1);
        DrawBranch(texture, 45, 27, 47, 27, 1);
        DrawBranch(texture, 31, 27, 34, 38, 2);
        DrawBranch(texture, 34, 38, 31, 47, 1);
        DrawBranch(texture, 34, 38, 40, 44, 1);
        DrawBranch(texture, 40, 44, 43, 47, 1);
        DrawBranch(texture, 29, 24, 27, 35, 1);
        DrawBranch(texture, 27, 35, 30, 43, 1);
        DrawBranch(texture, 27, 35, 23, 40, 1);

        // A few short secondary twigs keep the silhouette organic.
        DrawBranch(texture, 5, 32, 1, 30, 1);
        DrawBranch(texture, 15, 32, 20, 33, 1);
        DrawBranch(texture, 20, 33, 23, 36, 1);
        DrawBranch(texture, 37, 29, 41, 34, 1);
        DrawBranch(texture, 41, 34, 46, 33, 1);
        DrawBranch(texture, 36, 42, 35, 47, 1);

        texture.Apply(false, true);
        treeSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0f),
            PixelsPerUnit);
        treeSprite.name = "Sky Blue Gradient Branch Tree";
    }

    private static void DrawBranch(
        Texture2D texture,
        int startX,
        int startY,
        int endX,
        int endY,
        int thickness)
    {
        int x = startX;
        int y = startY;
        int deltaX = Mathf.Abs(endX - startX);
        int stepX = startX < endX ? 1 : -1;
        int deltaY = -Mathf.Abs(endY - startY);
        int stepY = startY < endY ? 1 : -1;
        int error = deltaX + deltaY;
        int totalSteps = Mathf.Max(Mathf.Abs(endX - startX), Mathf.Abs(endY - startY));
        int currentStep = 0;
        int endThickness = Mathf.Max(1, thickness - 1);

        while (true)
        {
            float progress = totalSteps > 0 ? currentStep / (float)totalSteps : 1f;
            int taperedThickness = Mathf.RoundToInt(
                Mathf.Lerp(thickness, endThickness, progress));
            DrawBranchPixel(texture, x, y, taperedThickness);
            if (x == endX && y == endY)
            {
                break;
            }

            int doubledError = error * 2;
            if (doubledError >= deltaY)
            {
                error += deltaY;
                x += stepX;
            }
            if (doubledError <= deltaX)
            {
                error += deltaX;
                y += stepY;
            }
            currentStep++;
        }
    }

    private static void DrawBranchPixel(Texture2D texture, int centerX, int centerY, int thickness)
    {
        thickness = Mathf.Max(1, thickness);
        int minimumOffset = -thickness / 2;
        int maximumOffset = minimumOffset + thickness - 1;
        Color color = GetSkyGradient(centerY);
        for (int offsetY = minimumOffset; offsetY <= maximumOffset; offsetY++)
        for (int offsetX = minimumOffset; offsetX <= maximumOffset; offsetX++)
        {
            SetPixelSafe(texture, centerX + offsetX, centerY + offsetY, color);
        }
    }

    private static Color GetSkyGradient(int pixelY)
    {
        float height = Mathf.Clamp01(pixelY / (TextureSize - 1f));
        Color bottom = new Color(0.08f, 0.38f, 0.68f, 0.58f);
        Color middle = new Color(0.16f, 0.67f, 0.94f, 0.72f);
        Color top = new Color(0.65f, 0.91f, 1f, 0.82f);
        return height < 0.55f
            ? Color.Lerp(bottom, middle, height / 0.55f)
            : Color.Lerp(middle, top, (height - 0.55f) / 0.45f);
    }

    private static void FillRect(
        Texture2D texture,
        int startX,
        int startY,
        int width,
        int height,
        Color color)
    {
        for (int y = startY; y < startY + height; y++)
        for (int x = startX; x < startX + width; x++)
        {
            texture.SetPixel(x, y, color);
        }
    }

    private static void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x >= 0 && x < TextureSize && y >= 0 && y < TextureSize)
        {
            texture.SetPixel(x, y, color);
        }
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying)
        {
            return;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Color previousColor = Gizmos.color;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(0.35f, 0.9f, 1f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0f, 1.5f, 0f), EditorBoundsSize);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
