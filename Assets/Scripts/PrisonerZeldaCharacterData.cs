using UnityEngine;

/// <summary>A smaller unhelmeted prisoner character with a temporary box attack visual.</summary>
public sealed class PrisonerZeldaCharacterData : ZeldaCharacterData
{
    private static readonly Vector3 EditorVisualBoundsCenter = new Vector3(0f, 0.28f, 0f);
    private static readonly Vector3 EditorVisualBoundsSize = new Vector3(0.75f, 0.75f, 0f);

    [SerializeField] private int attackPower = 1;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.22f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.5f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.48f, 0.48f);
    [SerializeField] private Color idleTint = new Color(0.94f, 0.96f, 1f, 1f);
    [SerializeField] private Color movingTint = Color.white;
    [SerializeField] private Color attackVisualTint = new Color(0.55f, 0.52f, 0.45f, 0.85f);

    private static readonly Sprite[] IdleSprites = new Sprite[4];
    private static readonly Sprite[] AttackSprites = new Sprite[4];

    public override bool CanAttack => true;
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Rock;
    public override Color AttackVisualTint => attackVisualTint;

    public override void ApplyCharacterVisual(SpriteRenderer spriteRenderer, Vector2 facingDirection, bool isMoving, bool isAttacking)
    {
        if (spriteRenderer == null) return;
        EnsureSprites();
        int direction = DirectionIndex(facingDirection);
        spriteRenderer.sprite = isAttacking ? AttackSprites[direction] : IdleSprites[direction];
        spriteRenderer.color = GetDamageFeedbackTint(isMoving ? movingTint : idleTint);
    }

    private static void EnsureSprites()
    {
        if (IdleSprites[0] != null) return;
        Vector2[] directions = { Vector2.down, Vector2.up, Vector2.left, Vector2.right };
        for (int i = 0; i < directions.Length; i++)
        {
            IdleSprites[i] = CreateSprite(directions[i], false);
            AttackSprites[i] = CreateSprite(directions[i], true);
        }
    }

    private static Sprite CreateSprite(Vector2 facing, bool attacking)
    {
        const int size = 16;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color shirt = new Color(0.68f, 0.51f, 0.32f, 1f);
        Color shirtLight = new Color(0.82f, 0.66f, 0.43f, 1f);
        Color shirtDark = new Color(0.44f, 0.31f, 0.19f, 1f);
        Color pants = new Color(0.07f, 0.25f, 0.13f, 1f);
        Color pantsLight = new Color(0.12f, 0.36f, 0.20f, 1f);
        Color skin = new Color(0.88f, 0.64f, 0.42f, 1f);
        Color skinShadow = new Color(0.62f, 0.39f, 0.24f, 1f);
        Color hair = new Color(0.16f, 0.10f, 0.07f, 1f);
        Color eye = new Color(0.05f, 0.12f, 0.22f, 1f);

        FillRect(texture, 0, 0, size, size, clear);
        // A broad, compact torso and short legs give the prisoner a chunkier chibi silhouette.
        FillRect(texture, 6, 3, 2, 3, pants);
        FillRect(texture, 9, 3, 2, 3, pants);
        FillRect(texture, 7, 4, 1, 2, pantsLight);
        FillRect(texture, 10, 4, 1, 2, pantsLight);
        FillRect(texture, 5, 6, 7, 5, shirt);
        FillRect(texture, 5, 6, 1, 5, shirtDark);
        FillRect(texture, 8, 7, 3, 3, shirtLight);
        FillRect(texture, 5, 6, 7, 1, shirtDark);

        DrawHead(texture, facing, skin, skinShadow, hair, eye);
        DrawArms(texture, facing, attacking, shirt, shirtDark, skin, skinShadow);

        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.25f), 16f);
        sprite.name = "Prisoner " + DirectionName(facing) + (attacking ? " Attack" : " Idle");
        return sprite;
    }

    private static void DrawHead(Texture2D texture, Vector2 facing, Color skin, Color shadow, Color hair, Color eye)
    {
        if (facing == Vector2.up)
        {
            FillRect(texture, 6, 10, 5, 4, skin);
            FillRect(texture, 6, 12, 5, 2, hair);
            FillRect(texture, 6, 10, 1, 3, shadow);
        }
        else if (facing == Vector2.left)
        {
            FillRect(texture, 5, 10, 5, 4, skin);
            FillRect(texture, 6, 13, 4, 1, hair);
            FillRect(texture, 9, 11, 1, 3, hair);
            texture.SetPixel(5, 11, eye);
            texture.SetPixel(5, 10, shadow);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 7, 10, 5, 4, skin);
            FillRect(texture, 7, 13, 4, 1, hair);
            FillRect(texture, 7, 11, 1, 3, hair);
            texture.SetPixel(11, 11, eye);
            texture.SetPixel(11, 10, shadow);
        }
        else
        {
            FillRect(texture, 6, 10, 5, 4, skin);
            FillRect(texture, 6, 13, 5, 1, hair);
            FillRect(texture, 6, 12, 1, 2, hair);
            texture.SetPixel(7, 11, eye);
            texture.SetPixel(9, 11, eye);
            texture.SetPixel(8, 10, shadow);
        }
    }

    private static void DrawArms(Texture2D texture, Vector2 facing, bool attacking, Color shirt, Color shirtDark, Color skin, Color shadow)
    {
        if (!attacking)
        {
            FillRect(texture, 4, 7, 1, 4, shirtDark);
            FillRect(texture, 12, 7, 1, 4, shirt);
            texture.SetPixel(4, 7, skin);
            texture.SetPixel(12, 7, skin);
            return;
        }

        if (facing == Vector2.left)
        {
            FillRect(texture, 2, 7, 4, 2, shirt);
            FillRect(texture, 1, 7, 2, 2, skin);
            FillRect(texture, 12, 7, 1, 3, shirtDark);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 11, 7, 4, 2, shirt);
            FillRect(texture, 14, 7, 2, 2, skin);
            FillRect(texture, 4, 7, 1, 3, shirtDark);
        }
        else if (facing == Vector2.up)
        {
            FillRect(texture, 5, 10, 2, 4, shirtDark);
            FillRect(texture, 10, 10, 2, 4, shirt);
            texture.SetPixel(5, 14, skin);
            texture.SetPixel(11, 14, skin);
        }
        else
        {
            FillRect(texture, 5, 5, 2, 4, shirtDark);
            FillRect(texture, 10, 5, 2, 4, shirt);
            texture.SetPixel(5, 4, shadow);
            texture.SetPixel(11, 4, skin);
        }
    }

    private static int DirectionIndex(Vector2 direction)
    {
        if (Mathf.Abs(direction.x) > Mathf.Abs(direction.y)) return direction.x < 0f ? 2 : 3;
        return direction.y > 0f ? 1 : 0;
    }

    private static string DirectionName(Vector2 direction)
    {
        if (direction == Vector2.up) return "Up";
        if (direction == Vector2.left) return "Left";
        if (direction == Vector2.right) return "Right";
        return "Down";
    }

    private static void FillRect(Texture2D texture, int x, int y, int width, int height, Color color)
    {
        for (int py = y; py < y + height; py++)
        for (int px = x; px < x + width; px++) texture.SetPixel(px, py, color);
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        attackPower = Mathf.Max(0, attackPower);
        attackDuration = Mathf.Max(0f, attackDuration);
        attackSize.x = Mathf.Max(0f, attackSize.x);
        attackSize.y = Mathf.Max(0f, attackSize.y);
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
        Gizmos.DrawWireCube(EditorVisualBoundsCenter, EditorVisualBoundsSize);
        Gizmos.matrix = previousMatrix;
        Gizmos.color = previousColor;
    }
}
