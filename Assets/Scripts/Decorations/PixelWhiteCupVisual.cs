using UnityEngine;

/// <summary>Generates a small white pixel cup for decoration.</summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelWhiteCupVisual : MonoBehaviour
{
    private const int TextureSize = 16;
    private const float PixelsPerUnit = 16f;
    private static readonly Vector3 EditorBoundsCenter = new Vector3(0.0625f, -0.03125f, 0f);
    private static readonly Vector3 EditorBoundsSize = new Vector3(0.75f, 0.625f, 0f);

    [SerializeField] private int sortingOrder = 1;

    private static Sprite cupSprite;

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
        spriteRenderer.sprite = cupSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = sortingOrder;
    }

    private static void EnsureSprite()
    {
        if (cupSprite != null)
        {
            return;
        }

        Texture2D texture = new Texture2D(
            TextureSize,
            TextureSize,
            TextureFormat.RGBA32,
            false);
        texture.name = "Small White Pixel Cup";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = new Color(0.96f, 0.98f, 1f, 1f);
        Color paleShadow = new Color(0.68f, 0.76f, 0.82f, 1f);
        FillRect(texture, 0, 0, TextureSize, TextureSize, clear);

        // Cup bowl with a slightly tapered lower edge.
        FillRect(texture, 4, 5, 8, 6, paleShadow);
        FillRect(texture, 5, 3, 6, 2, paleShadow);
        FillRect(texture, 5, 5, 6, 5, white);
        FillRect(texture, 6, 3, 4, 2, white);

        // Bright rim and a small visible inner opening.
        FillRect(texture, 4, 10, 8, 2, white);
        FillRect(texture, 6, 10, 4, 1, paleShadow);

        // Squared pixel handle with an open center.
        FillRect(texture, 12, 6, 2, 4, white);
        FillRect(texture, 13, 7, 2, 2, white);
        texture.SetPixel(12, 7, clear);
        texture.SetPixel(12, 8, clear);

        texture.Apply(false, true);
        cupSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit);
        cupSprite.name = "Small White Pixel Cup";
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
        Gizmos.DrawWireCube(EditorBoundsCenter, EditorBoundsSize);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
