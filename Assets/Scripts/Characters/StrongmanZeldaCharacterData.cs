using UnityEngine;

public class StrongmanZeldaCharacterData : ZeldaCharacterData
{
    [SerializeField] private int attackPower = 2;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.3f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.65f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.85f, 0.75f);
    [SerializeField] private Color idleTint = new Color(1f, 0.92f, 0.82f, 1f);
    [SerializeField] private Color movingTint = Color.white;
    [SerializeField] private Color attackVisualTint = new Color(1f, 0.62f, 0.18f, 0.55f);

    private static Sprite facingDownSprite;
    private static Sprite facingUpSprite;
    private static Sprite facingLeftSprite;
    private static Sprite facingRightSprite;
    private static Sprite attackingDownSprite;
    private static Sprite attackingUpSprite;
    private static Sprite attackingLeftSprite;
    private static Sprite attackingRightSprite;

    public override bool CanAttack => true;
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Hammer;
    public override Color AttackVisualTint => attackVisualTint;

    public override void ApplyCharacterVisual(SpriteRenderer spriteRenderer, Vector2 facingDirection, bool isMoving, bool isAttacking)
    {
        if (spriteRenderer == null)
        {
            return;
        }

        CreateDirectionSprites();

        Vector2 visualDirection = ToNearestCardinalDirection(facingDirection);
        if (visualDirection == Vector2.up)
        {
            spriteRenderer.sprite = isAttacking ? attackingUpSprite : facingUpSprite;
        }
        else if (visualDirection == Vector2.left)
        {
            spriteRenderer.sprite = isAttacking ? attackingLeftSprite : facingLeftSprite;
        }
        else if (visualDirection == Vector2.right)
        {
            spriteRenderer.sprite = isAttacking ? attackingRightSprite : facingRightSprite;
        }
        else
        {
            spriteRenderer.sprite = isAttacking ? attackingDownSprite : facingDownSprite;
        }

        Color baseColor = isMoving ? movingTint : idleTint;
        spriteRenderer.color = GetDamageFeedbackTint(baseColor);
    }

    private static void CreateDirectionSprites()
    {
        if (facingDownSprite != null)
        {
            return;
        }

        facingDownSprite = CreateCharacterSprite(Vector2.down, false);
        facingUpSprite = CreateCharacterSprite(Vector2.up, false);
        facingLeftSprite = CreateCharacterSprite(Vector2.left, false);
        facingRightSprite = CreateCharacterSprite(Vector2.right, false);
        attackingDownSprite = CreateCharacterSprite(Vector2.down, true);
        attackingUpSprite = CreateCharacterSprite(Vector2.up, true);
        attackingLeftSprite = CreateCharacterSprite(Vector2.left, true);
        attackingRightSprite = CreateCharacterSprite(Vector2.right, true);
    }

    private static Sprite CreateCharacterSprite(Vector2 facing, bool attacking)
    {
        const int width = 20;
        const int height = 20;
        Texture2D texture = new Texture2D(width, height);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color skin = new Color(1f, 0.72f, 0.45f, 1f);
        Color shadowSkin = new Color(0.82f, 0.48f, 0.28f, 1f);
        Color orange = new Color(1f, 0.42f, 0.05f, 1f);
        Color darkOrange = new Color(0.68f, 0.2f, 0.02f, 1f);
        Color boots = new Color(0.16f, 0.09f, 0.05f, 1f);
        Color hair = new Color(0.18f, 0.09f, 0.03f, 1f);
        Color eye = new Color(0.05f, 0.05f, 0.04f, 1f);

        FillRect(texture, 0, 0, width, height, clear);

        FillRect(texture, 6, 3, 3, 3, boots);
        FillRect(texture, 11, 3, 3, 3, boots);
        FillRect(texture, 5, 6, 10, 7, orange);
        FillRect(texture, 4, 8, 12, 4, orange);
        FillRect(texture, 5, 6, 1, 7, darkOrange);
        FillRect(texture, 14, 6, 1, 7, darkOrange);
        FillRect(texture, 8, 12, 4, 2, skin);
        FillRect(texture, 7, 14, 6, 3, skin);
        FillRect(texture, 7, 17, 6, 2, hair);

        if (attacking)
        {
            DrawAttackingArms(texture, facing, skin, shadowSkin);
        }
        else
        {
            FillRect(texture, 2, 8, 3, 5, skin);
            FillRect(texture, 15, 8, 3, 5, skin);
            FillRect(texture, 2, 7, 3, 2, shadowSkin);
            FillRect(texture, 15, 7, 3, 2, shadowSkin);
        }

        if (facing == Vector2.up)
        {
            FillRect(texture, 7, 15, 6, 3, hair);
        }
        else if (facing == Vector2.left)
        {
            FillRect(texture, 6, 15, 5, 3, skin);
            FillRect(texture, 5, 16, 3, 2, hair);
            SetPixelSafe(texture, 6, 15, eye);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 9, 15, 5, 3, skin);
            FillRect(texture, 12, 16, 3, 2, hair);
            SetPixelSafe(texture, 13, 15, eye);
        }
        else
        {
            SetPixelSafe(texture, 8, 15, eye);
            SetPixelSafe(texture, 11, 15, eye);
            FillRect(texture, 9, 14, 2, 1, shadowSkin);
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.25f), 16f);
    }

    private static void DrawAttackingArms(Texture2D texture, Vector2 facing, Color skin, Color shadowSkin)
    {
        if (facing == Vector2.up)
        {
            FillRect(texture, 4, 13, 3, 5, skin);
            FillRect(texture, 13, 13, 3, 5, skin);
            FillRect(texture, 4, 17, 3, 2, shadowSkin);
            FillRect(texture, 13, 17, 3, 2, shadowSkin);
        }
        else if (facing == Vector2.left)
        {
            FillRect(texture, 0, 9, 6, 4, skin);
            FillRect(texture, 0, 8, 3, 2, shadowSkin);
            FillRect(texture, 14, 8, 3, 5, skin);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 14, 9, 6, 4, skin);
            FillRect(texture, 17, 8, 3, 2, shadowSkin);
            FillRect(texture, 3, 8, 3, 5, skin);
        }
        else
        {
            FillRect(texture, 3, 7, 4, 5, skin);
            FillRect(texture, 13, 7, 4, 5, skin);
            FillRect(texture, 3, 6, 4, 2, shadowSkin);
            FillRect(texture, 13, 6, 4, 2, shadowSkin);
        }
    }

    private static Vector2 ToNearestCardinalDirection(Vector2 direction)
    {
        if (direction.sqrMagnitude <= 0f)
        {
            return Vector2.down;
        }

        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y))
        {
            return direction.x > 0f ? Vector2.right : Vector2.left;
        }

        return direction.y > 0f ? Vector2.up : Vector2.down;
    }

    private static void FillRect(Texture2D texture, int startX, int startY, int width, int height, Color color)
    {
        for (int y = startY; y < startY + height; y++)
        {
            for (int x = startX; x < startX + width; x++)
            {
                SetPixelSafe(texture, x, y, color);
            }
        }
    }

    private static void SetPixelSafe(Texture2D texture, int x, int y, Color color)
    {
        if (x < 0 || x >= texture.width || y < 0 || y >= texture.height)
        {
            return;
        }

        texture.SetPixel(x, y, color);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        attackPower = Mathf.Max(0, attackPower);
        attackDuration = Mathf.Max(0f, attackDuration);
        attackSize.x = Mathf.Max(0f, attackSize.x);
        attackSize.y = Mathf.Max(0f, attackSize.y);
    }
}
