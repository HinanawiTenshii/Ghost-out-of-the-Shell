using UnityEngine;

/// <summary>
/// Generates a white pixel weapon rack holding one single-color sword and
/// two single-color spears. This component is visual-only.
/// </summary>
[ExecuteAlways]
[RequireComponent(typeof(SpriteRenderer))]
public sealed class PixelWeaponRackVisual : MonoBehaviour
{
    private const int TextureWidth = 32;
    private const int TextureHeight = 18;
    private const float PixelsPerUnit = 16f;
    private static readonly Vector3 EditorBoundsSize =
        new Vector3(TextureWidth / PixelsPerUnit, TextureHeight / PixelsPerUnit, 0f);

    [SerializeField] private int sortingOrder = 1;

    private static Sprite weaponRackSprite;

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
        spriteRenderer.sprite = weaponRackSprite;
        spriteRenderer.color = Color.white;
        spriteRenderer.sortingOrder = sortingOrder;
    }

    private static void EnsureSprite()
    {
        if (weaponRackSprite != null)
        {
            return;
        }

        Texture2D texture = new Texture2D(
            TextureWidth,
            TextureHeight,
            TextureFormat.RGBA32,
            false);
        texture.name = "Pixel Weapon Rack";
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color rackWhite = Color.white;
        Color swordColor = new Color(0.48f, 0.62f, 0.72f, 1f);
        Color spearColor = new Color(0.30f, 0.55f, 0.58f, 1f);
        FillRect(texture, 0, 0, TextureWidth, TextureHeight, clear);

        // A wider, lower white rack drawn directly at the new aspect ratio.
        FillRect(texture, 3, 3, 2, 12, rackWhite);
        FillRect(texture, 27, 3, 2, 12, rackWhite);
        FillRect(texture, 3, 5, 26, 2, rackWhite);
        FillRect(texture, 3, 12, 26, 2, rackWhite);
        FillRect(texture, 2, 2, 4, 2, rackWhite);
        FillRect(texture, 26, 2, 4, 2, rackWhite);

        // Shortened left spear, redrawn rather than vertically compressed.
        FillRect(texture, 8, 4, 1, 10, spearColor);
        texture.SetPixel(7, 14, spearColor);
        FillRect(texture, 8, 14, 1, 3, spearColor);
        texture.SetPixel(9, 14, spearColor);

        // Shortened right spear, preserving the same one-color construction.
        FillRect(texture, 23, 4, 1, 10, spearColor);
        texture.SetPixel(22, 14, spearColor);
        FillRect(texture, 23, 14, 1, 3, spearColor);
        texture.SetPixel(24, 14, spearColor);

        // The sword is separately redrawn with a compact blade, clear guard and grip.
        FillRect(texture, 15, 5, 2, 7, swordColor);
        texture.SetPixel(15, 4, swordColor);
        texture.SetPixel(16, 4, swordColor);
        FillRect(texture, 13, 12, 6, 1, swordColor);
        FillRect(texture, 15, 13, 2, 3, swordColor);
        FillRect(texture, 14, 16, 4, 1, swordColor);

        texture.Apply(false, true);
        weaponRackSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureWidth, TextureHeight),
            new Vector2(0.5f, 0.5f),
            PixelsPerUnit);
        weaponRackSprite.name = "Pixel Weapon Rack";
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
