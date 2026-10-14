using UnityEngine;

/// <summary>Raised-hand item attack with four authored gun views and an exact sprite-space muzzle.</summary>
[DefaultExecutionOrder(1100)]
public sealed class DisposableHandgunAttackVisual : MonoBehaviour
{
    private const float PixelsPerUnit = 24f;
    private static readonly Sprite[] sprites = new Sprite[4];
    private static Sprite flashSprite;
    private ZeldaFourWayMover mover;
    private ZeldaCharacterData user;
    private SpriteRenderer body, gun, flash;
    private Vector2 facing;
    private int view;
    private float elapsed, duration;

    // Down/up are separate end/top views; left is mirrored without turning the grip upside down.
    private static readonly string[] Right = {
        "............", "............", "............", "....D.......",
        "..GHHHHHHHD.", "..GSSSSSSSD.", ".WWGDDDDDDD.", ".WLG.G......",
        ".WLGGG......", ".WW.........", "............", "............"
    };
    private static readonly string[] Up = {
        "............", ".....DD.....", ".....HH.....", ".....HS.....",
        ".....HS.....", ".....HS.....", "....GHSG....", "....GSSG....",
        ".....WW.....", ".....WL.....", ".....WW.....", "............"
    };
    private static readonly string[] Down = {
        "............", ".....WW.....", ".....LW.....", ".....WW.....",
        "....GSSG....", "....GSSG....", ".....SS.....", ".....HS.....",
        ".....HS.....", ".....HD.....", ".....DD.....", "............"
    };
    private static readonly string[] Left = Mirror(Right);

    public static string[] GetPixelRows(int direction) => direction == 1 ? Up : direction == 2 ? Left : direction == 3 ? Right : Down;
    public static Vector2 GripPixel(int direction) => direction == 1 ? new Vector2(6, 2) :
        direction == 2 ? new Vector2(10, 3) : direction == 3 ? new Vector2(2, 3) : new Vector2(6, 9);
    public static Vector2 MuzzlePixel(int direction) => direction == 1 ? new Vector2(6, 11) :
        direction == 2 ? new Vector2(1, 6.5f) : direction == 3 ? new Vector2(11, 6.5f) : new Vector2(6, 1);

    private static string[] Mirror(string[] rows)
    {
        var result = new string[rows.Length];
        for (int i = 0; i < rows.Length; i++)
        {
            char[] chars = rows[i].ToCharArray();
            System.Array.Reverse(chars);
            result[i] = new string(chars);
        }
        return result;
    }

    public static bool TryFire(ZeldaCharacterData user, ZeldaFourWayMover mover, float speed)
    {
        if (user == null || mover == null || user.IsDead || user.IsGhostLike ||
            float.IsNaN(speed) || float.IsInfinity(speed) || speed <= 0f) return false;
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject);
        if (!scene.IsValid() || !scene.isLoaded) return false;
        float duration = Mathf.Clamp(user.AttackDuration, .28f, .45f);
        if (!mover.TryBeginItemAttack(duration)) return false;
        var root = new GameObject("Hand Cannon Firing Visual");
        root.transform.SetParent(user.transform, false);
        var visual = root.AddComponent<DisposableHandgunAttackVisual>();
        visual.user = user;
        visual.mover = mover;
        visual.body = user.GetComponent<SpriteRenderer>();
        visual.duration = duration;
        visual.facing = mover.FacingDirection;
        visual.view = Mathf.Abs(visual.facing.x) > Mathf.Abs(visual.facing.y)
            ? (visual.facing.x < 0 ? 2 : 3) : (visual.facing.y > 0 ? 1 : 0);
        visual.gun = root.AddComponent<SpriteRenderer>();
        visual.gun.sprite = GetSprite(visual.view);
        var flashObject = new GameObject("Muzzle Flash");
        flashObject.transform.SetParent(root.transform, false);
        visual.flash = flashObject.AddComponent<SpriteRenderer>();
        if (flashSprite == null) flashSprite = CreateSprite(new[] { "HH", "HH" }, new Vector2(1, 1), "Hand Cannon Flash");
        visual.flash.sprite = flashSprite;
        visual.flash.color = new Color(1f, .87f, .42f);
        visual.ApplyPose();
        if (DisposableHandgunBullet.FireFromMuzzle(user, visual.facing, speed, visual.MuzzleWorld)) return true;
        // A failed spawn must not consume the item or leave the player in an attack pose.
        visual.gameObject.SetActive(false);
        Destroy(root);
        return false;
    }

    public static Sprite GetSprite(int direction)
    {
        if (sprites[direction] == null)
            sprites[direction] = CreateSprite(GetPixelRows(direction), GripPixel(direction), "Hand Cannon " + direction);
        return sprites[direction];
    }

    private static Sprite CreateSprite(string[] rows, Vector2 pivot, string name)
    {
        int width = rows[0].Length, height = rows.Length;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        for (int row = 0; row < height; row++) for (int x = 0; x < width; x++)
            texture.SetPixel(x, height - 1 - row, DisposableHandgunPickupItemVisual.PixelColor(rows[row][x]));
        texture.Apply(false, true);
        var sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(pivot.x / width, pivot.y / height), PixelsPerUnit);
        sprite.name = name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private Vector3 MuzzleWorld => transform.TransformPoint((MuzzlePixel(view) - GripPixel(view)) / PixelsPerUnit);

    private void LateUpdate()
    {
        if (user == null || user.IsDead || user.IsGhostLike || mover == null ||
            !mover.isActiveAndEnabled || !mover.IsUsingItemAttack)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        elapsed += Time.deltaTime;
        if (elapsed >= duration)
        {
            gameObject.SetActive(false);
            Destroy(gameObject);
            return;
        }
        ApplyPose();
    }

    private void ApplyPose()
    {
        Vector3 grip = user.transform.position;
        if (body != null && body.sprite != null)
        {
            Vector2 local = (BodyGripPixel(user, view) - body.sprite.pivot) / body.sprite.pixelsPerUnit;
            if (body.flipX) local.x = -local.x;
            if (body.flipY) local.y = -local.y;
            grip = body.transform.TransformPoint(local);
        }
        // Root is attached for lifetime/scene ownership, but views are already world-facing.
        transform.rotation = Quaternion.identity;
        Vector3 parentScale = user.transform.lossyScale;
        transform.localScale = new Vector3(parentScale.x < 0f ? -1f : 1f, parentScale.y < 0f ? -1f : 1f, 1f);
        float recoil = Mathf.Sin(Mathf.Clamp01(elapsed / .12f) * Mathf.PI) * .025f;
        transform.position = grip - (Vector3)(facing * recoil);
        gun.sortingLayerID = body != null ? body.sortingLayerID : 0;
        gun.sortingOrder = (body != null ? body.sortingOrder : 1) + 1;
        flash.sortingLayerID = gun.sortingLayerID;
        flash.sortingOrder = gun.sortingOrder + 1;
        flash.transform.position = MuzzleWorld;
        flash.enabled = elapsed < .045f;
    }

    // Grip centres of the existing authored attack poses, in each body's sprite pixels.
    // TransformPoint + actual pivot/PPU preserve attachment on scaled characters.
    public static Vector2 BodyGripPixel(ZeldaCharacterData character, int direction)
    {
        if (character is AutomatonZeldaCharacterData)
            return new Vector2(direction == 2 ? 6 : direction == 3 ? 10 : 8, direction == 1 ? 11 : 7);
        if (character is BehemothZeldaCharacterData)
            return direction >= 2 ? new Vector2(16, 4.5f) : new Vector2(direction == 0 ? 4.5f : 27.5f, 4.5f);
        if (character is HeavyPaladinZeldaCharacterData)
            return new Vector2(direction == 2 ? 3.5f : direction == 3 ? 16.5f : 10.5f, direction == 1 ? 12 : 7);
        if (character is StrongmanZeldaCharacterData)
            return new Vector2(direction == 2 ? 3.5f : direction == 3 ? 16.5f : 9.5f, direction == 1 ? 14 : direction == 0 ? 6 : 10);
        if (character is ChefZeldaCharacterData)
            return new Vector2(direction == 2 ? 4 : direction == 3 ? 12 : 8, direction == 1 ? 10 : direction == 0 ? 6 : 9);
        bool wideSleeve = character is ArabSoldierZeldaCharacterData || character is AlchemistZeldaCharacterData ||
            character is PaladinZeldaCharacterData || character is PriestZeldaCharacterData || character is WizardZeldaCharacterData;
        return new Vector2(direction == 2 ? (wideSleeve ? 2 : 3) : direction == 3 ? (wideSleeve ? 14 : 13) : 8,
            direction == 1 ? 12 : direction == 0 ? 5 : 7);
    }

    private void OnDisable()
    {
        if (mover != null) mover.EndItemAttack();
    }
}
