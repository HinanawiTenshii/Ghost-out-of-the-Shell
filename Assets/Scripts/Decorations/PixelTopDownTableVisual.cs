using UnityEngine;

/// <summary>
/// Generates a three-color, cold-toned top-down table at runtime.
/// </summary>
[RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
public sealed class PixelTopDownTableVisual : MonoBehaviour
{
    private const int TextureWidth = 32;
    private const int TextureHeight = 24;
    private const float PixelsPerUnit = 16f;
    private static readonly Vector2 VisualSize = new Vector2(28f / PixelsPerUnit, 18f / PixelsPerUnit);
    private static readonly Vector2 VisualOffset = new Vector2(0f, -1f / PixelsPerUnit);

    [SerializeField] private int sortingOrder = 1;

    private static Sprite tableSprite;

    private void Awake()
    {
        EnsureSprite();

        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        spriteRenderer.sprite = tableSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = sortingOrder;

        BoxCollider2D boxCollider = GetComponent<BoxCollider2D>();
        boxCollider.isTrigger = false;
        boxCollider.size = VisualSize;
        boxCollider.offset = VisualOffset;
    }

    private static void EnsureSprite()
    {
        if (tableSprite != null)
        {
            return;
        }

        Texture2D texture = new Texture2D(TextureWidth, TextureHeight, TextureFormat.RGBA32, false);
        texture.name = "Pixel Top Down Table";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color darkBlue = new Color(0.07f, 0.13f, 0.22f, 1f);
        Color slateBlue = new Color(0.20f, 0.34f, 0.46f, 1f);
        Color coldHighlight = new Color(0.42f, 0.60f, 0.69f, 1f);
        FillRect(texture, 0, 0, TextureWidth, TextureHeight, clear);

        // Dark chamfered silhouette, 28 x 18 pixels at its widest points.
        FillRect(texture, 4, 2, 24, 18, darkBlue);
        FillRect(texture, 2, 4, 28, 14, darkBlue);

        // Recessed tabletop surface.
        FillRect(texture, 4, 4, 24, 14, slateBlue);
        FillRect(texture, 3, 6, 26, 10, slateBlue);

        // Cold rim highlight and a restrained central plank seam.
        FillRect(texture, 5, 5, 22, 1, coldHighlight);
        FillRect(texture, 4, 6, 1, 9, coldHighlight);
        FillRect(texture, 6, 16, 20, 1, coldHighlight);
        FillRect(texture, 15, 6, 1, 10, darkBlue);

        // Four visible leg caps reinforce the top-down perspective.
        FillRect(texture, 4, 4, 3, 3, darkBlue);
        FillRect(texture, 25, 4, 3, 3, darkBlue);
        FillRect(texture, 4, 15, 3, 3, darkBlue);
        FillRect(texture, 25, 15, 3, 3, darkBlue);
        SetPixel(texture, 5, 5, coldHighlight);
        SetPixel(texture, 26, 5, coldHighlight);
        SetPixel(texture, 5, 16, coldHighlight);
        SetPixel(texture, 26, 16, coldHighlight);

        texture.Apply(false, true);
        tableSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureWidth, TextureHeight),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit);
        tableSprite.name = "Pixel Top Down Table";
    }

    private static void FillRect(Texture2D texture, int startX, int startY, int width, int height, Color color)
    {
        for (int y = startY; y < startY + height; y++)
        for (int x = startX; x < startX + width; x++)
        {
            texture.SetPixel(x, y, color);
        }
    }

    private static void SetPixel(Texture2D texture, int x, int y, Color color)
    {
        texture.SetPixel(x, y, color);
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
        Gizmos.DrawWireCube(VisualOffset, new Vector3(VisualSize.x, VisualSize.y, 0f));
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
