using UnityEngine;

/// <summary>Generates a two-color upright pixel sword for decoration.</summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelDecorativeSwordVisual : MonoBehaviour
{
    private const int TextureWidth = 16;
    private const int TextureHeight = 24;
    private const float PixelsPerUnit = 16f;
    private static readonly Vector3 EditorBoundsSize =
        new Vector3(TextureWidth / PixelsPerUnit, TextureHeight / PixelsPerUnit, 0f);

    [SerializeField] private int sortingOrder = 1;

    private static Sprite swordSprite;

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
        spriteRenderer.sprite = swordSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = sortingOrder;
    }

    private static void EnsureSprite()
    {
        if (swordSprite != null)
        {
            return;
        }

        Texture2D texture = new Texture2D(
            TextureWidth,
            TextureHeight,
            TextureFormat.RGBA32,
            false);
        texture.name = "Two Color Decorative Pixel Sword";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color bladeColor = new Color(0.66f, 0.79f, 0.86f, 1f);
        Color hiltColor = new Color(0.48f, 0.29f, 0.14f, 1f);
        FillRect(texture, 0, 0, TextureWidth, TextureHeight, clear);

        // One-color blade with a stepped pixel point.
        FillRect(texture, 6, 8, 4, 12, bladeColor);
        FillRect(texture, 7, 20, 2, 2, bladeColor);
        texture.SetPixel(7, 22, bladeColor);
        texture.SetPixel(8, 22, bladeColor);

        // One second color is shared by guard, grip and pommel.
        FillRect(texture, 3, 7, 10, 2, hiltColor);
        FillRect(texture, 7, 3, 2, 4, hiltColor);
        FillRect(texture, 6, 1, 4, 2, hiltColor);

        texture.Apply(false, true);
        swordSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureWidth, TextureHeight),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit);
        swordSprite.name = "Two Color Decorative Pixel Sword";
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
