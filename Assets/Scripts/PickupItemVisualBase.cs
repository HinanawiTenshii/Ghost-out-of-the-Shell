using UnityEngine;

/// <summary>Base visual implementation for world pickup items and inventory icons.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PickupItemVisualBase : MonoBehaviour
{
    [SerializeField] private Color itemColor = Color.white;
    [SerializeField, Min(0.01f)] private float worldScale = 0.55f;

    private SpriteRenderer spriteRenderer;
    private Sprite runtimeSprite;
    private Texture2D runtimeTexture;

    public Color DisplayColor => itemColor;
    public Color InventoryTint => UsesEmbeddedColors ? Color.white : itemColor;

    /// <summary>
    /// Converts a saved instance tint into the correct UI tint. Multi-colour
    /// icons already contain their colours in the texture and must remain
    /// white here, while monochrome icons such as keys use the saved tint.
    /// </summary>
    public Color GetInventoryTint(Color instanceDisplayColor)
    {
        return UsesEmbeddedColors ? Color.white : instanceDisplayColor;
    }

    public virtual Sprite InventoryIcon
    {
        get
        {
            EnsureVisualSprite();
            return runtimeSprite;
        }
    }

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        EnsureVisualSprite();
        spriteRenderer.sprite = runtimeSprite;
        spriteRenderer.color = UsesEmbeddedColors ? Color.white : itemColor;
        transform.localScale = Vector3.one * worldScale;
    }

    /// <summary>
    /// Restores instance-specific visual data after an inventory item is
    /// recreated from its shared prefab.
    /// </summary>
    public virtual void SetDisplayColor(Color color)
    {
        itemColor = color;
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = UsesEmbeddedColors ? Color.white : itemColor;
        }
    }

    protected virtual void OnDestroy()
    {
        if (runtimeSprite != null)
        {
            Destroy(runtimeSprite);
        }

        if (runtimeTexture != null)
        {
            Destroy(runtimeTexture);
        }
    }

    protected virtual void EnsureVisualSprite()
    {
        if (runtimeSprite != null)
        {
            return;
        }

        string[] rows = GetPixelRows();
        int height = rows.Length;
        int width = rows[0].Length;

        runtimeTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        runtimeTexture.name = RuntimeSpriteName;
        runtimeTexture.filterMode = FilterMode.Point;
        runtimeTexture.wrapMode = TextureWrapMode.Clamp;

        for (int row = 0; row < height; row++)
        {
            int y = height - 1 - row;
            for (int x = 0; x < width; x++)
            {
                runtimeTexture.SetPixel(x, y, GetPixelColor(rows[row][x]));
            }
        }

        runtimeTexture.Apply(false, true);
        runtimeSprite = Sprite.Create(
            runtimeTexture,
            new Rect(0f, 0f, width, height),
            new Vector2(0.5f, 0.5f),
            Mathf.Max(width, height));
        runtimeSprite.name = RuntimeSpriteName;
    }

    protected virtual string RuntimeSpriteName => "Runtime Unknown Item";

    protected virtual bool UsesEmbeddedColors => false;

    protected virtual Color GetPixelColor(char pixel)
    {
        return pixel == '#' ? Color.white : Color.clear;
    }

    protected virtual string[] GetPixelRows()
    {
        return new[]
        {
            "###########",
            "#.........#",
            "#...###...#",
            "#..#...#..#",
            "#......#..#",
            "#.....#...#",
            "#....#....#",
            "#.........#",
            "#....#....#",
            "#.........#",
            "###########"
        };
    }

#if UNITY_EDITOR
    protected virtual void OnValidate()
    {
        worldScale = Mathf.Max(0.01f, worldScale);
    }
#endif
}
