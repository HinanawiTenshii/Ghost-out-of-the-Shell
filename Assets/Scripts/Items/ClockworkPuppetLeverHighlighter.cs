using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shows lever target outlines while the clockwork puppet is selected. The
/// outlines are drawn by the final UI-composite camera so walls and the
/// circular-vision darkness cannot hide them.
/// </summary>
public sealed class ClockworkPuppetLeverHighlighter : MonoBehaviour
{
    private sealed class Outline
    {
        public GameObject root;
        public SpriteRenderer renderer;
        public Sprite sourceSprite;
    }

    private static ClockworkPuppetLeverHighlighter instance;
    private readonly Dictionary<LeverData, Outline> outlines =
        new Dictionary<LeverData, Outline>();
    private readonly Dictionary<Sprite, Sprite> outlineSprites =
        new Dictionary<Sprite, Sprite>();
    private readonly List<Texture2D> outlineTextures = new List<Texture2D>();

    public static bool IsHighlightModeActive => IsPuppetSelected();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null) return;
        GameObject root = new GameObject("Clockwork Puppet Lever Highlights");
        instance = root.AddComponent<ClockworkPuppetLeverHighlighter>();
        DontDestroyOnLoad(root);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }

    private void LateUpdate()
    {
        bool selected = IsPuppetSelected();
        if (!selected)
        {
            SetAllVisible(false);
            return;
        }

        LeverData selectedTarget = FindNearestLeverToPlayer();
        HashSet<LeverData> alive = new HashSet<LeverData>();
        foreach (LeverData lever in LeverData.WorldLevers)
        {
            if (lever == null || !lever.isActiveAndEnabled || lever.VisualRenderer == null)
            {
                continue;
            }
            alive.Add(lever);
            Outline outline = GetOrCreateOutline(lever);
            outline.root.SetActive(true);
            UpdateOutline(outline, lever.VisualRenderer,
                lever == selectedTarget
                    ? new Color(0.25f, 1f, 0.38f, 1f)
                    : Color.white);
        }

        List<LeverData> stale = null;
        foreach (KeyValuePair<LeverData, Outline> pair in outlines)
        {
            if (pair.Key != null && alive.Contains(pair.Key)) continue;
            if (pair.Value.root != null) Destroy(pair.Value.root);
            if (stale == null) stale = new List<LeverData>();
            stale.Add(pair.Key);
        }
        if (stale != null)
        {
            for (int i = 0; i < stale.Count; i++) outlines.Remove(stale[i]);
        }
    }

    private static bool IsPuppetSelected()
    {
        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory == null) return false;
        PersistentInventory.Slot slot = inventory.GetSlot(inventory.SelectedSlotIndex);
        return slot != null && !slot.IsEmpty &&
            slot.ItemId == ClockworkPuppetPickupItem.DefaultItemId;
    }

    private static LeverData FindNearestLeverToPlayer()
    {
        ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null) return null;
        LeverData closest = null;
        float best = float.MaxValue;
        foreach (LeverData lever in LeverData.WorldLevers)
        {
            if (lever == null || !lever.isActiveAndEnabled) continue;
            float sqr = ((Vector2)lever.transform.position - (Vector2)mover.transform.position).sqrMagnitude;
            if (sqr < best)
            {
                best = sqr;
                closest = lever;
            }
        }
        return closest;
    }

    private Outline GetOrCreateOutline(LeverData lever)
    {
        Outline outline;
        if (outlines.TryGetValue(lever, out outline)) return outline;
        outline = new Outline
        {
            root = new GameObject(lever.name + " Puppet Target Outline")
        };
        int uiLayer = LayerMask.NameToLayer("UI");
        int overlayLayer = uiLayer >= 0 ? uiLayer : gameObject.layer;
        int topSortingLayerId = GetTopSortingLayerId();
        outline.root.layer = overlayLayer;
        outline.root.transform.SetParent(transform, false);
        outline.renderer = outline.root.AddComponent<SpriteRenderer>();
        outline.renderer.sortingLayerID = topSortingLayerId;
        outline.renderer.sortingOrder = 32000;
        outline.renderer.maskInteraction = SpriteMaskInteraction.None;
        outlines.Add(lever, outline);
        return outline;
    }

    private static int GetTopSortingLayerId()
    {
        int result = 0;
        int highestValue = int.MinValue;
        foreach (SortingLayer sortingLayer in SortingLayer.layers)
        {
            if (sortingLayer.value <= highestValue)
            {
                continue;
            }
            highestValue = sortingLayer.value;
            result = sortingLayer.id;
        }
        return result;
    }

    private void UpdateOutline(
        Outline outline,
        SpriteRenderer source,
        Color color)
    {
        if (outline == null || outline.renderer == null || source == null ||
            source.sprite == null)
        {
            return;
        }

        if (outline.sourceSprite != source.sprite)
        {
            outline.sourceSprite = source.sprite;
            outline.renderer.sprite = GetOrCreateOutlineSprite(source.sprite);
        }
        outline.root.transform.position = source.transform.position;
        outline.root.transform.rotation = source.transform.rotation;
        outline.root.transform.localScale = source.transform.lossyScale;
        outline.renderer.flipX = source.flipX;
        outline.renderer.flipY = source.flipY;
        outline.renderer.color = color;
    }

    private Sprite GetOrCreateOutlineSprite(Sprite source)
    {
        if (outlineSprites.TryGetValue(source, out Sprite cached) &&
            cached != null)
        {
            return cached;
        }

        Rect rect = source.rect;
        int width = Mathf.Max(1, Mathf.RoundToInt(rect.width));
        int height = Mathf.Max(1, Mathf.RoundToInt(rect.height));
        int originX = Mathf.RoundToInt(rect.x);
        int originY = Mathf.RoundToInt(rect.y);
        Color32[] sourcePixels = source.texture.GetPixels32();
        int textureWidth = source.texture.width;
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int sourceIndex = (originY + y) * textureWidth + originX + x;
                if (sourcePixels[sourceIndex].a > 8)
                {
                    continue;
                }

                bool touchesShape = false;
                for (int offsetY = -1; offsetY <= 1 && !touchesShape; offsetY++)
                {
                    for (int offsetX = -1; offsetX <= 1; offsetX++)
                    {
                        int neighbourX = x + offsetX;
                        int neighbourY = y + offsetY;
                        if ((offsetX == 0 && offsetY == 0) ||
                            neighbourX < 0 || neighbourX >= width ||
                            neighbourY < 0 || neighbourY >= height)
                        {
                            continue;
                        }
                        int neighbourIndex =
                            (originY + neighbourY) * textureWidth +
                            originX + neighbourX;
                        if (sourcePixels[neighbourIndex].a > 8)
                        {
                            touchesShape = true;
                            break;
                        }
                    }
                }
                if (touchesShape)
                {
                    pixels[y * width + x] = new Color32(255, 255, 255, 255);
                }
            }
        }

        Texture2D texture = new Texture2D(
            width,
            height,
            TextureFormat.RGBA32,
            false)
        {
            name = source.name + " Puppet Lever Outline",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Vector2 pivot = new Vector2(
            source.pivot.x / rect.width,
            source.pivot.y / rect.height);
        Sprite result = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            pivot,
            source.pixelsPerUnit);
        result.name = source.name + " Puppet Lever Outline";
        outlineTextures.Add(texture);
        outlineSprites[source] = result;
        return result;
    }

    private void SetAllVisible(bool visible)
    {
        foreach (Outline outline in outlines.Values)
        {
            if (outline.root != null) outline.root.SetActive(visible);
        }
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
        foreach (Sprite sprite in outlineSprites.Values)
        {
            if (sprite != null) Destroy(sprite);
        }
        for (int i = 0; i < outlineTextures.Count; i++)
        {
            if (outlineTextures[i] != null) Destroy(outlineTextures[i]);
        }
        outlineSprites.Clear();
        outlineTextures.Clear();
    }
}
