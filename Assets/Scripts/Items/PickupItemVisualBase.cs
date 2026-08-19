using UnityEngine;

/// <summary>Base visual implementation for world pickup items and inventory icons.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public class PickupItemVisualBase : MonoBehaviour
{
    private const string BreathingVisualObjectName = "Pickup Breathing Visual";
    private const string LegacyOutlineObjectName = "Pickup Outline";
    private const float PickupBreathingScale = 0.035f;
    private const float PickupBreathingSpeed = 3.141593f;

    [SerializeField] private Color itemColor = Color.white;
    [SerializeField, Min(0.01f)] private float worldScale = 0.55f;

    private SpriteRenderer spriteRenderer;
    private Sprite runtimeSprite;
    private Texture2D runtimeTexture;
    private CardboardBoxPickupItem cardboardBox;
    private GameObject breathingVisualObject;
    private SpriteRenderer breathingVisualRenderer;

    public Color DisplayColor => itemColor;
    public Color InventoryTint => UsesEmbeddedColors ? Color.white : itemColor;
    public float WorldScale => worldScale;

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
        cardboardBox = GetComponent<CardboardBoxPickupItem>();
        RemoveLegacyOutline();
        EnsureBreathingVisual();
        UpdatePickupBreathing();
    }

    protected virtual void OnEnable()
    {
        if (spriteRenderer != null)
        {
            EnsureBreathingVisual();
            spriteRenderer.forceRenderingOff = breathingVisualRenderer != null;
        }
    }

    protected virtual void OnDisable()
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.forceRenderingOff = false;
        }
    }

    protected virtual void LateUpdate()
    {
        UpdatePickupBreathing();
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
        runtimeTexture.filterMode = RuntimeTextureFilterMode;
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

    private void EnsureBreathingVisual()
    {
        if (spriteRenderer == null)
        {
            return;
        }

        if (breathingVisualRenderer != null)
        {
            return;
        }

        Transform existing = null;
        for (int childIndex = transform.childCount - 1;
             childIndex >= 0;
             childIndex--)
        {
            Transform child = transform.GetChild(childIndex);
            if (child.name != BreathingVisualObjectName)
            {
                continue;
            }

            if (existing == null)
            {
                existing = child;
                continue;
            }

            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }

        breathingVisualObject = existing != null
            ? existing.gameObject
            : new GameObject(BreathingVisualObjectName);
        breathingVisualObject.layer = gameObject.layer;
        breathingVisualObject.transform.SetParent(transform, false);
        breathingVisualRenderer = breathingVisualObject.GetComponent<SpriteRenderer>();
        if (breathingVisualRenderer == null)
        {
            breathingVisualRenderer =
                breathingVisualObject.AddComponent<SpriteRenderer>();
        }
        SyncBreathingVisualRenderer();
        // Keep the authored renderer as the authoritative data source for
        // item scripts (damage flashes, instance colours and sprite changes),
        // while rendering its animated copy without scaling the collider.
        spriteRenderer.forceRenderingOff = true;
    }

    private void RemoveLegacyOutline()
    {
        for (int childIndex = transform.childCount - 1;
             childIndex >= 0;
             childIndex--)
        {
            Transform child = transform.GetChild(childIndex);
            if (child.name != LegacyOutlineObjectName)
            {
                continue;
            }

            child.gameObject.SetActive(false);
            Destroy(child.gameObject);
        }
    }

    private void SyncBreathingVisualRenderer()
    {
        if (breathingVisualRenderer == null || spriteRenderer == null)
        {
            return;
        }

        breathingVisualRenderer.sprite = spriteRenderer.sprite;
        breathingVisualRenderer.color = spriteRenderer.color;
        breathingVisualRenderer.flipX = spriteRenderer.flipX;
        breathingVisualRenderer.flipY = spriteRenderer.flipY;
        breathingVisualRenderer.sortingLayerID = spriteRenderer.sortingLayerID;
        breathingVisualRenderer.sortingOrder = spriteRenderer.sortingOrder;
        breathingVisualRenderer.maskInteraction = spriteRenderer.maskInteraction;
        breathingVisualRenderer.sharedMaterial = spriteRenderer.sharedMaterial;
        breathingVisualRenderer.enabled = spriteRenderer.enabled;
    }

    private void UpdatePickupBreathing()
    {
        if (breathingVisualRenderer == null || spriteRenderer == null)
        {
            return;
        }

        bool itemVisible = isActiveAndEnabled &&
            spriteRenderer.enabled &&
            gameObject.activeInHierarchy;
        bool filledCardboardBox = cardboardBox != null &&
            cardboardBox.HasStoredItem;
        SyncBreathingVisualRenderer();
        breathingVisualRenderer.enabled = itemVisible;

        if (!itemVisible)
        {
            return;
        }

        float wave =
            (Mathf.Sin(Time.time * PickupBreathingSpeed) + 1f) * 0.5f;
        float scale = filledCardboardBox
            ? 1f
            : 1f + (wave * 2f - 1f) * PickupBreathingScale;
        if (breathingVisualObject != null)
        {
            breathingVisualObject.transform.localScale =
                new Vector3(scale, scale, 1f);
            breathingVisualObject.transform.localRotation =
                Quaternion.identity;
        }
    }

    protected virtual string RuntimeSpriteName => "Runtime Unknown Item";

    protected virtual FilterMode RuntimeTextureFilterMode => FilterMode.Point;

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
