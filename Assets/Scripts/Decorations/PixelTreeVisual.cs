using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PixelTreeVisual : MonoBehaviour
{
    private const string CrownObjectName = "Crown";

    private static Sprite trunkSprite;
    private static Sprite crownSprite;

    private void Awake()
    {
        SpriteRenderer spriteRenderer = GetComponent<SpriteRenderer>();
        CreateTreeSprite();

        spriteRenderer.sprite = trunkSprite;
        spriteRenderer.sortingOrder = 1;
        spriteRenderer.color = Color.white;

        SpriteRenderer crownRenderer = GetOrCreateCrownRenderer();
        crownRenderer.sprite = crownSprite;
        crownRenderer.sortingOrder = 3;
        crownRenderer.color = Color.white;
    }

    private static void CreateTreeSprite()
    {
        if (trunkSprite != null)
        {
            return;
        }

        const int width = 24;
        const int height = 28;
        Texture2D texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color leafDark = new Color(0.05f, 0.32f, 0.13f, 1f);
        Color leafMid = new Color(0.10f, 0.48f, 0.18f, 1f);
        Color leafLight = new Color(0.22f, 0.66f, 0.25f, 1f);
        Color trunk = new Color(0.43f, 0.23f, 0.09f, 1f);
        Color trunkLight = new Color(0.60f, 0.36f, 0.14f, 1f);

        Texture2D trunkTexture = new Texture2D(width, height);
        trunkTexture.filterMode = FilterMode.Point;

        Texture2D crownTexture = new Texture2D(width, height);
        crownTexture.filterMode = FilterMode.Point;

        FillRect(trunkTexture, 0, 0, width, height, clear);
        FillRect(crownTexture, 0, 0, width, height, clear);

        FillRect(crownTexture, 7, 8, 10, 5, leafDark);
        FillRect(crownTexture, 5, 11, 14, 5, leafMid);
        FillRect(crownTexture, 3, 15, 18, 5, leafDark);
        FillRect(crownTexture, 6, 19, 12, 5, leafMid);
        FillRect(crownTexture, 9, 23, 6, 3, leafDark);

        FillRect(crownTexture, 8, 13, 4, 3, leafLight);
        FillRect(crownTexture, 13, 17, 4, 3, leafLight);
        FillRect(crownTexture, 10, 21, 3, 2, leafLight);

        FillRect(trunkTexture, 10, 2, 4, 13, trunk);
        FillRect(trunkTexture, 12, 2, 2, 13, trunkLight);

        trunkTexture.Apply();
        crownTexture.Apply();

        trunkSprite = Sprite.Create(trunkTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.14f), 16f);
        crownSprite = Sprite.Create(crownTexture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.14f), 16f);
    }

    private SpriteRenderer GetOrCreateCrownRenderer()
    {
        Transform crown = transform.Find(CrownObjectName);
        if (crown == null)
        {
            GameObject crownObject = new GameObject(CrownObjectName);
            crownObject.transform.SetParent(transform, false);
            crown = crownObject.transform;
        }

        SpriteRenderer crownRenderer = crown.GetComponent<SpriteRenderer>();
        if (crownRenderer == null)
        {
            crownRenderer = crown.gameObject.AddComponent<SpriteRenderer>();
        }

        return crownRenderer;
    }

    private static void FillRect(Texture2D texture, int startX, int startY, int width, int height, Color color)
    {
        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                texture.SetPixel(x, y, color);
            }
        }
    }
}
