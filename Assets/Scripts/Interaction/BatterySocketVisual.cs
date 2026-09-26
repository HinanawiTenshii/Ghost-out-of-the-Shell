using UnityEngine;

/// <summary>One small procedural sprite; red/grey steel housing with a visibly empty or filled recess.</summary>
[ExecuteAlways, DisallowMultipleComponent, RequireComponent(typeof(SpriteRenderer))]
public sealed class BatterySocketVisual : MonoBehaviour
{
    private Texture2D texture;
    private Sprite sprite;
    private SpriteRenderer target;
    private BatterySocket socket;
    private bool lastInstalled;

    private void OnEnable() { socket = GetComponent<BatterySocket>(); Refresh(); }
    private void Update()
    {
        if (sprite == null || lastInstalled != (socket != null && socket.HasBattery)) Refresh();
    }
    private void Refresh()
    {
        // Migrate old scene overrides: this layer is drawn AFTER the vision mask.
        // Default remains solid but does not participate in Blocks sight raycasts.
        if (gameObject.layer == LayerMask.NameToLayer("Visible Non Blocking")) gameObject.layer = 0;
        if (target == null) target = GetComponent<SpriteRenderer>();
        if (texture == null)
        {
            texture = new Texture2D(20, 24, TextureFormat.RGBA32, false) {
                name = "Battery Socket Red Steel", filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            sprite = Sprite.Create(texture, new Rect(0, 0, 20, 24), new Vector2(.5f, .5f), 24,
                0, SpriteMeshType.FullRect);
            sprite.name = "Battery Socket"; sprite.hideFlags = HideFlags.HideAndDontSave;
        }
        texture.SetPixels(new Color[20 * 24]);
        Fill(1, 1, 18, 22, 'D'); Fill(2, 2, 16, 20, 'S');
        Fill(2, 21, 16, 2, 'H'); Fill(2, 3, 2, 17, 'R'); Fill(16, 3, 2, 17, 'R');
        Fill(5, 4, 10, 16, 'D'); Fill(7, 18, 6, 2, 'H'); Fill(7, 4, 6, 2, 'H');
        lastInstalled = socket != null && socket.HasBattery;
        if (lastInstalled)
        {
            Fill(7, 6, 6, 12, 'B'); Fill(7, 7, 5, 10, 'R'); Fill(7, 7, 1, 10, 'L');
            Fill(7, 6, 6, 2, 'S'); Fill(7, 16, 6, 2, 'H');
            Fill(9, 11, 1, 4, 'H'); Fill(8, 12, 3, 1, 'H');
        }
        Fill(3, 20, 2, 1, 'H'); Fill(15, 20, 2, 1, 'H');
        Fill(3, 2, 2, 1, 'H'); Fill(15, 2, 2, 1, 'H');
        texture.Apply(false, false); target.sprite = sprite;
    }
    private void Fill(int x, int y, int width, int height, char color)
    {
        Color value = BatteryPickupItemVisual.PixelColor(color);
        for (int row = y; row < y + height; row++)
            for (int col = x; col < x + width; col++) texture.SetPixel(col, row, value);
    }
    private void OnDestroy()
    {
        if (target != null && target.sprite == sprite) target.sprite = null;
        Release(sprite); Release(texture);
    }
    private static void Release(Object value)
    {
        if (value == null) return;
        if (Application.isPlaying) Destroy(value); else DestroyImmediate(value);
    }
}
