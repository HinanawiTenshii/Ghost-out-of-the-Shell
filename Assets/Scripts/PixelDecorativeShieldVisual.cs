using UnityEngine;

/// <summary>Generates a character-scale, three-color pixel shield with a red cross.</summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelDecorativeShieldVisual : MonoBehaviour
{
    private const int TextureSize = 16;
    private const float PixelsPerUnit = 16f;
    private static readonly Vector3 EditorBoundsSize = new Vector3(1f, 1f, 0f);

    [SerializeField] private int sortingOrder = 1;

    private static Sprite shieldSprite;

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
        spriteRenderer.sprite = shieldSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = sortingOrder;
    }

    private static void EnsureSprite()
    {
        if (shieldSprite != null)
        {
            return;
        }

        Texture2D texture = new Texture2D(
            TextureSize,
            TextureSize,
            TextureFormat.RGBA32,
            false);
        texture.name = "Three Color Red Cross Pixel Shield";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color outlineColor = new Color(0.72f, 0.79f, 0.82f, 1f);
        Color shieldColor = new Color(0.12f, 0.18f, 0.23f, 1f);
        Color crossColor = new Color(0.78f, 0.12f, 0.12f, 1f);
        FillRect(texture, 0, 0, TextureSize, TextureSize, clear);

        // Flat-topped shield silhouette that tapers only toward the lower point.
        FillRect(texture, 7, 1, 2, 1, outlineColor);
        FillRect(texture, 5, 2, 6, 1, outlineColor);
        FillRect(texture, 4, 3, 8, 1, outlineColor);
        FillRect(texture, 3, 4, 10, 1, outlineColor);
        FillRect(texture, 2, 5, 12, 10, outlineColor);

        // Single-color shield face inset inside the outline.
        FillRect(texture, 6, 3, 4, 1, shieldColor);
        FillRect(texture, 5, 4, 6, 1, shieldColor);
        FillRect(texture, 4, 5, 8, 1, shieldColor);
        FillRect(texture, 3, 6, 10, 8, shieldColor);

        // Bold red cross, kept completely within the shield face.
        FillRect(texture, 7, 5, 2, 8, crossColor);
        FillRect(texture, 4, 8, 8, 2, crossColor);

        texture.Apply(false, true);
        shieldSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit);
        shieldSprite.name = "Three Color Red Cross Pixel Shield";
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
        Gizmos.DrawWireCube(Vector3.zero, EditorBoundsSize);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
