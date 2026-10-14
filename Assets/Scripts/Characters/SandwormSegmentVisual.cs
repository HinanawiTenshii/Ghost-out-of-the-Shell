using UnityEngine;

public enum SandwormSegmentKind { Head, Body, Tail }

/// <summary>
/// Eyeless, chibi top-down pixel art: plump armor, short paddles and heavy mandibles.
/// Flat color blocks on a native 16-PPU grid match the existing character sprites.
/// Local +X points forward. Five shared sprites per state; no extra renderers.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
[AddComponentMenu("Characters/Sandworm Segment Visual")]
public sealed class SandwormSegmentVisual : MonoBehaviour
{
    [SerializeField] private SandwormSegmentKind kind = SandwormSegmentKind.Body;
    [SerializeField, Range(0, 2)] private int variant;

    private const float PixelsPerUnit = 16f;
    // Keep the authored geometry and world-space dimensions independent from
    // texture resolution. Changing PPU must not enlarge the boss or its parts.
    private const float DesignPixelsPerUnit = 32f;
    private static readonly Sprite[] SharedSprites = new Sprite[10];
    private static readonly Texture2D[] SharedTextures = new Texture2D[10];
    private static int users;
    private SpriteRenderer segmentRenderer;
    private bool registered;
    private bool dirty = true;
    private bool undergroundOverlay;

    public SandwormSegmentKind Kind => kind;
    public int Variant => variant;
    public Vector2 NominalSize => GetNominalSize(kind);
    public bool HasUndergroundOverlay => undergroundOverlay;

    public void SetUndergroundOverlay(bool enabled)
    {
        if (undergroundOverlay == enabled && !dirty && segmentRenderer != null && segmentRenderer.sprite != null) return;
        undergroundOverlay = enabled;
        dirty = true;
        ApplyVisual();
    }

    public static Vector2 GetNominalSize(SandwormSegmentKind segmentKind)
    {
        switch (segmentKind)
        {
            case SandwormSegmentKind.Head: return new Vector2(52f / DesignPixelsPerUnit, 60f / DesignPixelsPerUnit);
            case SandwormSegmentKind.Tail: return new Vector2(60f / DesignPixelsPerUnit, 48f / DesignPixelsPerUnit);
            default: return new Vector2(36f / DesignPixelsPerUnit, 56f / DesignPixelsPerUnit);
        }
    }

    public void Configure(SandwormSegmentKind kind, int variant = 0)
    {
        this.kind = kind;
        this.variant = Mathf.Clamp(variant, 0, 2);
        dirty = true;
        ApplyVisual();
    }

    private void OnEnable()
    {
        dirty = true;
        ApplyVisual();
    }

    private void OnValidate()
    {
        variant = Mathf.Clamp(variant, 0, 2);
        dirty = true; // Never generate Unity assets from the serialization callback.
    }

    private void Update()
    {
        if (dirty || segmentRenderer == null || segmentRenderer.sprite == null)
            ApplyVisual();
    }

    private void ApplyVisual()
    {
        if (segmentRenderer == null) segmentRenderer = GetComponent<SpriteRenderer>();
        if (segmentRenderer == null) return;
        if (!registered) { registered = true; users++; }
        int index = kind == SandwormSegmentKind.Head ? 0 :
            kind == SandwormSegmentKind.Tail ? 4 : 1 + Mathf.Clamp(variant, 0, 2);
        if (undergroundOverlay) index += 5;
        if (SharedSprites[index] == null) BuildSprite(index, kind, variant, undergroundOverlay);
        segmentRenderer.sprite = SharedSprites[index];
        // Keep authored material, color, layer and sorting: the normal world
        // camera's vision pass already masks this renderer.
        dirty = false;
    }

    private static void BuildSprite(int index, SandwormSegmentKind segmentKind, int bodyVariant, bool underground)
    {
        Vector2 size = GetNominalSize(segmentKind) * PixelsPerUnit;
        int width = Mathf.RoundToInt(size.x), height = Mathf.RoundToInt(size.y);
        var art = new char[width, height];
        if (segmentKind == SandwormSegmentKind.Head) DrawHead(art);
        else if (segmentKind == SandwormSegmentKind.Tail) DrawTail(art);
        else DrawBody(art, bodyVariant);

        var pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                Color32 color = PixelColor(art[x, y]);
                // Alpha-over, not a multiplicative SpriteRenderer tint. Bake the
                // constant mask once while retaining the original silhouette alpha.
                if (underground && color.a != 0)
                {
                    byte alpha = color.a;
                    color = Color32.Lerp(color, SandwormStateController.UndergroundMaskColor,
                        SandwormStateController.UndergroundMaskOpacity);
                    color.a = alpha;
                }
                pixels[(height - 1 - y) * width + x] = color;
            }
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "Sandworm " + segmentKind + " Chibi 16 PPU " + bodyVariant + (underground ? " Underground" : ""),
            hideFlags = HideFlags.HideAndDontSave,
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp
        };
        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
            new Vector2(.5f, .5f), PixelsPerUnit, 0, SpriteMeshType.FullRect);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        SharedTextures[index] = texture;
        SharedSprites[index] = sprite;
    }

    private static Color32 PixelColor(char pixel)
    {
        // The reference's green mouth is an annotation, not emissive tissue or eyes.
        // Warm chitin and bone colors remain readable against the blue levels.
        switch (pixel)
        {
            case 'D': return new Color32(82, 49, 31, 255);
            case 'S': return new Color32(137, 81, 42, 255);
            case 'B': return new Color32(191, 132, 66, 255);
            case 'H': return new Color32(235, 184, 102, 255);
            case 'E': return new Color32(255, 225, 154, 255);
            case 'M': return new Color32(49, 29, 27, 255);
            default: return new Color32(0, 0, 0, 0);
        }
    }

    private static void DrawBody(char[,] art, int bodyVariant)
    {
        // Native 16-PPU coordinates. Short, broad paddles replace the long spikes.
        // Their four-pixel tips and stepped corners read like the NPCs' chunky limbs.
        Polygon(art, 'D', 3,2, 7,2, 10,4, 11,8, 6,11, 2,7, 2,3);
        Polygon(art, 'B', 3,3, 7,3, 9,5, 9,8, 6,9, 3,6);
        Polygon(art, 'H', 3,3, 7,3, 8,4, 8,5, 4,5, 3,4);
        Polygon(art, 'D', 5,18, 11,20, 10,24, 7,26, 3,26, 2,25, 2,22);
        Polygon(art, 'S', 5,20, 9,21, 9,23, 7,25, 3,25, 3,23);
        Polygon(art, 'B', 5,20, 9,21, 8,23, 5,24, 3,24, 3,23);

        // A wide recessed collar keeps the articulated silhouette connected.
        Polygon(art, 'D', 0,9, 3,6, 11,6, 16,9, 18,12, 18,17, 15,20, 10,22, 3,22, 0,19);
        Polygon(art, 'S', 0,10, 3,8, 7,8, 7,20, 3,20, 0,18);
        Polygon(art, 'B', 1,10, 3,8, 7,8, 7,10, 3,10, 1,12);

        // One plump shield, one broad top highlight and one lower shadow.
        // No thin dorsal ridge, scratch texture, or bright isolated flecks.
        Polygon(art, 'D', 7,6, 12,6, 15,8, 17,11, 18,13, 18,17, 16,20, 13,22, 7,22, 4,19, 3,16, 3,12, 4,9);
        Polygon(art, 'B', 7,7, 12,7, 15,9, 16,12, 17,14, 17,17, 15,20, 12,21, 8,21, 5,18, 4,16, 4,12, 5,9);
        Polygon(art, 'H', 7,7, 12,7, 14,8, 15,10, 15,11, 12,10, 8,10, 6,12, 5,13, 5,10);
        Polygon(art, 'S', 5,17, 8,19, 12,19, 16,17, 15,20, 12,21, 8,21, 5,18);

        // Small, broad shade variations avoid a noisy tiled surface.
        if (bodyVariant == 1)
            Polygon(art, 'H', 8,10, 12,10, 13,11, 9,11, 7,12, 6,12);
        else if (bodyVariant == 2)
            Polygon(art, 'S', 9,18, 13,18, 15,17, 15,19, 12,20, 9,20);
    }

    private static void DrawHead(char[,] art)
    {
        // Keep the plump, eyeless rear shield. Give the front half to the jaws,
        // rather than enlarging the canvas, body spacing or physical hitbox.
        Polygon(art, 'D', 5,5, 12,5, 16,7, 19,11, 20,16, 18,21, 14,24, 11,26, 5,25, 2,22, 0,18, 0,12, 2,8);
        Polygon(art, 'B', 5,6, 12,6, 16,8, 18,12, 19,16, 17,20, 13,23, 10,25, 5,24, 3,21, 1,18, 1,12, 3,8);
        Polygon(art, 'H', 5,6, 12,6, 15,8, 17,10, 16,11, 12,9, 6,9, 3,12, 2,14, 2,11, 3,8);
        Polygon(art, 'S', 2,18, 5,21, 10,22, 14,20, 18,17, 17,21, 13,24, 10,25, 5,24, 3,21);
        Polygon(art, 'E', 6,7, 10,7, 12,8, 7,8, 5,9, 4,9);

        // Broad, recessed maw separates the two pincers and stays visible when masked.
        Polygon(art, 'D', 14,8, 20,8, 23,11, 26,13, 26,18, 23,21, 19,23, 14,21, 12,18, 12,13);
        Polygon(art, 'M', 16,10, 21,11, 25,14, 25,18, 22,20, 19,21, 15,19, 14,16, 14,13);

        // Heavy forward-swept mandibles: broad roots, long outer arms, inward hooks.
        // Their short two-pixel cutting edges retain the chibi scale, not needle spikes.
        Polygon(art, 'D', 9,8, 11,4, 14,2, 20,2, 24,4, 26,7, 26,11, 24,14, 22,12, 23,10, 22,7, 18,6, 14,7, 12,10);
        Polygon(art, 'B', 10,8, 12,5, 15,3, 20,3, 23,5, 25,8, 25,11, 24,12, 24,9, 22,6, 18,5, 14,6, 12,8);
        Polygon(art, 'H', 12,5, 15,3, 20,3, 23,5, 24,7, 21,6, 18,5, 15,5, 12,7);
        Polygon(art, 'S', 10,8, 13,6, 16,6, 14,8, 12,10);
        Polygon(art, 'E', 23,7, 25,9, 25,11, 24,13, 23,12, 23,10, 22,9);

        Polygon(art, 'D', 9,22, 11,26, 14,28, 20,28, 24,26, 26,23, 26,19, 24,16, 22,18, 23,20, 22,23, 18,24, 14,23, 12,20);
        Polygon(art, 'S', 10,22, 12,25, 15,27, 20,27, 23,25, 25,22, 25,19, 24,18, 24,21, 22,24, 18,25, 14,24, 12,22);
        Polygon(art, 'B', 11,22, 14,25, 19,26, 23,24, 24,21, 24,19, 23,20, 22,23, 18,24, 14,23, 12,21);
        Polygon(art, 'H', 12,23, 15,25, 19,25, 22,24, 22,25, 19,26, 14,25);
        Polygon(art, 'E', 23,23, 25,21, 25,19, 24,17, 23,18, 23,20, 22,21);

        // A second blunt tooth on each inner rim makes the bite read at gameplay scale.
        Polygon(art, 'D', 16,7, 20,7, 21,10, 20,13, 18,14, 16,11);
        Polygon(art, 'H', 17,8, 20,8, 20,11, 18,13, 17,11);
        Polygon(art, 'E', 18,9, 20,9, 20,11, 18,12);
        Polygon(art, 'D', 16,23, 20,23, 21,20, 20,17, 18,16, 16,19);
        Polygon(art, 'H', 17,22, 20,22, 20,19, 18,17, 17,19);
        Polygon(art, 'E', 18,21, 20,21, 20,19, 18,18);
    }

    private static void DrawTail(char[,] art)
    {
        // A stubby rounded taper with two broad shell plates, not a sharp stinger.
        Polygon(art, 'D', 21,5, 21,3, 23,2, 26,2, 28,4, 28,8, 24,10);
        Polygon(art, 'B', 22,5, 22,4, 24,3, 26,3, 27,5, 27,7, 24,8);
        Polygon(art, 'H', 23,4, 26,4, 27,5, 24,5, 23,6);
        Polygon(art, 'D', 22,15, 28,16, 28,20, 26,22, 23,22, 21,21, 21,18);
        Polygon(art, 'S', 23,17, 27,17, 27,20, 25,21, 23,21, 22,20);
        Polygon(art, 'B', 23,17, 27,17, 27,19, 25,20, 23,20);

        Polygon(art, 'D', 2,10, 5,8, 10,7, 16,4, 23,4, 28,6, 30,9, 30,16, 28,19, 23,21, 16,20, 10,17, 5,16, 2,14);
        Polygon(art, 'B', 3,11, 6,9, 11,8, 16,5, 23,5, 27,7, 29,10, 29,15, 27,18, 23,20, 16,19, 11,16, 6,15, 3,13);
        Polygon(art, 'H', 5,10, 11,8, 17,5, 23,5, 27,7, 28,9, 22,8, 17,8, 12,10, 6,12, 4,12);
        Polygon(art, 'S', 5,13, 11,14, 17,16, 23,17, 28,14, 27,18, 23,20, 16,19, 11,16, 6,15);

        // One broad overlap seam and a smaller rounded end cap.
        Polygon(art, 'D', 20,5, 22,7, 23,10, 23,14, 22,18, 20,20, 22,20, 24,17, 25,14, 25,10, 24,7, 22,5);
        Polygon(art, 'H', 23,6, 27,7, 28,9, 25,9, 24,8);
        Polygon(art, 'S', 10,8, 12,9, 13,11, 13,14, 12,17, 14,18, 15,14, 15,10, 13,7);
    }

    // Rasterize authored flat shapes once, not meshes or per-frame geometry.
    private static void Polygon(char[,] art, char color, params int[] vertices)
    {
        // All art coordinates are authored directly on the native pixel grid.
        // No texture resizing, smoothing, or fractional alpha is used.
        int width = art.GetLength(0), height = art.GetLength(1), count = vertices.Length / 2;
        int x0 = width - 1, y0 = height - 1, x1 = 0, y1 = 0;
        for (int i = 0; i < count; i++)
        {
            x0 = Mathf.Min(x0, vertices[i * 2]); x1 = Mathf.Max(x1, vertices[i * 2]);
            y0 = Mathf.Min(y0, vertices[i * 2 + 1]); y1 = Mathf.Max(y1, vertices[i * 2 + 1]);
        }
        for (int y = Mathf.Max(0, y0); y < Mathf.Min(height, y1); y++)
            for (int x = Mathf.Max(0, x0); x < Mathf.Min(width, x1); x++)
            {
                float px = x + .5f, py = y + .5f;
                bool inside = false;
                for (int i = 0, j = count - 1; i < count; j = i++)
                {
                    float ax = vertices[i * 2], ay = vertices[i * 2 + 1];
                    float bx = vertices[j * 2], by = vertices[j * 2 + 1];
                    if ((ay > py) != (by > py) && px < (bx - ax) * (py - ay) / (by - ay) + ax)
                        inside = !inside;
                }
                if (inside) art[x, y] = color;
            }
    }

    private void OnDestroy()
    {
        if (!registered) return;
        registered = false;
        users = Mathf.Max(0, users - 1);
        if (users != 0) return;
        for (int i = 0; i < SharedSprites.Length; i++)
        {
            Release(SharedSprites[i]);
            Release(SharedTextures[i]);
            SharedSprites[i] = null;
            SharedTextures[i] = null;
        }
    }

    private static void Release(Object asset)
    {
        if (asset == null) return;
        if (Application.isPlaying) Destroy(asset);
        else DestroyImmediate(asset);
    }
}
