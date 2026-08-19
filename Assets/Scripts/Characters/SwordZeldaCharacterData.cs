using UnityEngine;

public class SwordZeldaCharacterData : ZeldaCharacterData
{
    private static readonly Vector3 EditorVisualBoundsCenter = new Vector3(0f, 0.25f, 0f);
    private static readonly Vector3 EditorVisualBoundsSize = new Vector3(1f, 1f, 0f);
    [SerializeField] private int attackPower = 1;
    [SerializeField] private GameObject attackPrefab;
    [SerializeField] private float attackDuration = 0.25f;
    [SerializeField] private Vector2 attackSpawnOffset = new Vector2(0f, 0.8f);
    [SerializeField] private Vector2 attackSize = new Vector2(0.55f, 0.9f);
    [SerializeField] private Color idleTint = new Color(0.86f, 1f, 0.86f, 1f);
    [SerializeField] private Color movingTint = Color.white;
    [SerializeField] private Color attackVisualTint = new Color(1f, 0.85f, 0.2f, 0.65f);
    [SerializeField] private bool useYellowArmorHighlights;
    [SerializeField] private bool useMagentaArmorHighlights;

    private Sprite facingDownSprite;
    private Sprite facingUpSprite;
    private Sprite facingLeftSprite;
    private Sprite facingRightSprite;
    private Sprite attackingDownSprite;
    private Sprite attackingUpSprite;
    private Sprite attackingLeftSprite;
    private Sprite attackingRightSprite;

    public override bool CanAttack => true;
    public override int AttackPower => attackPower;
    public override GameObject AttackPrefab => attackPrefab;
    public override float AttackDuration => attackDuration;
    public override Vector2 AttackSpawnOffset => attackSpawnOffset;
    public override Vector2 AttackSize => attackSize;
    public override ZeldaAttackVisualShape AttackVisualShape => ZeldaAttackVisualShape.Sword;
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

    private void CreateDirectionSprites()
    {
        if (facingDownSprite != null)
        {
            return;
        }

        facingDownSprite = CreateCharacterSprite(Vector2.down);
        facingUpSprite = CreateCharacterSprite(Vector2.up);
        facingLeftSprite = CreateCharacterSprite(Vector2.left);
        facingRightSprite = CreateCharacterSprite(Vector2.right);
        attackingDownSprite = CreateAttackingCharacterSprite(Vector2.down);
        attackingUpSprite = CreateAttackingCharacterSprite(Vector2.up);
        attackingLeftSprite = CreateAttackingCharacterSprite(Vector2.left);
        attackingRightSprite = CreateAttackingCharacterSprite(Vector2.right);
    }

    private Sprite CreateAttackingCharacterSprite(Vector2 facing)
    {
        Sprite baseSprite = CreateCharacterSprite(facing);
        Texture2D sourceTexture = baseSprite.texture;
        Texture2D attackTexture = new Texture2D(sourceTexture.width, sourceTexture.height);
        attackTexture.filterMode = FilterMode.Point;

        for (int y = 0; y < sourceTexture.height; y++)
        {
            for (int x = 0; x < sourceTexture.width; x++)
            {
                attackTexture.SetPixel(x, y, sourceTexture.GetPixel(x, y));
            }
        }

        Color tunic = new Color(0.36f, 0.38f, 0.42f, 1f);
        Color gauntlet = GetArmorHighlightColor(new Color(0.48f, 0.53f, 0.57f, 1f));

        if (facing == Vector2.up)
        {
            FillRect(attackTexture, 6, 11, 4, 3, gauntlet);
            FillRect(attackTexture, 7, 8, 2, 4, tunic);
        }
        else if (facing == Vector2.left)
        {
            FillRect(attackTexture, 1, 6, 5, 2, gauntlet);
            FillRect(attackTexture, 5, 5, 3, 3, tunic);
        }
        else if (facing == Vector2.right)
        {
            FillRect(attackTexture, 10, 6, 5, 2, gauntlet);
            FillRect(attackTexture, 8, 5, 3, 3, tunic);
        }
        else
        {
            FillRect(attackTexture, 6, 5, 4, 3, gauntlet);
            FillRect(attackTexture, 7, 7, 2, 3, tunic);
        }

        attackTexture.Apply();
        return Sprite.Create(attackTexture, new Rect(0, 0, sourceTexture.width, sourceTexture.height), new Vector2(0.5f, 0.25f), 16f);
    }

    private Sprite CreateCharacterSprite(Vector2 facing)
    {
        const int size = 16;
        Texture2D texture = new Texture2D(size, size);
        texture.filterMode = FilterMode.Point;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color tunicDark = new Color(0.20f, 0.22f, 0.26f, 1f);
        Color tunic = new Color(0.36f, 0.38f, 0.42f, 1f);
        Color tunicLight = GetArmorHighlightColor(new Color(0.52f, 0.54f, 0.57f, 1f));
        Color helmetDark = new Color(0.16f, 0.20f, 0.24f, 1f);
        Color helmet = new Color(0.38f, 0.44f, 0.48f, 1f);
        Color helmetLight = GetArmorHighlightColor(new Color(0.62f, 0.68f, 0.70f, 1f));
        Color face = new Color(1f, 0.78f, 0.48f, 1f);
        Color boots = new Color(0.24f, 0.13f, 0.05f, 1f);
        Color belt = new Color(0.16f, 0.10f, 0.06f, 1f);
        Color eye = new Color(0.04f, 0.18f, 0.48f, 1f);

        FillRect(texture, 0, 0, size, size, clear);
        FillRect(texture, 5, 4, 6, 7, tunic);
        FillRect(texture, 5, 4, 1, 7, tunicDark);
        FillRect(texture, 10, 4, 1, 7, tunicDark);
        FillRect(texture, 7, 5, 2, 5, tunicLight);
        FillRect(texture, 5, 6, 6, 1, belt);
        SetMarker(texture, 7, 6, helmetLight);
        FillRect(texture, 4, 8, 2, 3, helmetDark);
        FillRect(texture, 10, 8, 2, 3, helmet);
        FillRect(texture, 5, 2, 2, 2, boots);
        FillRect(texture, 9, 2, 2, 2, boots);

        if (facing == Vector2.up)
        {
            FillRect(texture, 5, 10, 6, 5, helmet);
            FillRect(texture, 5, 10, 2, 1, helmetDark);
            FillRect(texture, 6, 14, 4, 1, helmetLight);
            FillRect(texture, 7, 11, 2, 3, helmetDark);
            SetMarker(texture, 7, 13, helmetLight);
        }
        else if (facing == Vector2.left)
        {
            FillRect(texture, 4, 9, 5, 4, face);
            FillRect(texture, 3, 11, 7, 3, helmet);
            FillRect(texture, 4, 14, 5, 1, helmetLight);
            FillRect(texture, 9, 11, 1, 3, helmetDark);
            FillRect(texture, 3, 10, 2, 2, helmet);
            SetMarker(texture, 3, 11, helmetLight);
            texture.SetPixel(4, 10, eye);
        }
        else if (facing == Vector2.right)
        {
            FillRect(texture, 7, 9, 5, 4, face);
            FillRect(texture, 6, 11, 7, 3, helmet);
            FillRect(texture, 7, 14, 5, 1, helmetLight);
            FillRect(texture, 6, 11, 1, 3, helmetDark);
            FillRect(texture, 11, 10, 2, 2, helmet);
            SetMarker(texture, 11, 11, helmetLight);
            texture.SetPixel(10, 10, eye);
        }
        else
        {
            FillRect(texture, 5, 9, 6, 4, face);
            FillRect(texture, 5, 12, 6, 3, helmet);
            FillRect(texture, 6, 15, 4, 1, helmetLight);
            FillRect(texture, 5, 12, 1, 2, helmetDark);
            FillRect(texture, 10, 12, 1, 2, helmetDark);
            texture.SetPixel(7, 11, eye);
            texture.SetPixel(9, 11, eye);
            SetMarker(texture, 7, 8, helmetLight);
        }

        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.25f), 16f);
    }

    private Color GetArmorHighlightColor(Color normalColor)
    {
        if (useMagentaArmorHighlights)
        {
            return new Color(0.95f, 0.12f, 0.72f, 1f);
        }

        return useYellowArmorHighlights
            ? new Color(0.95f, 0.75f, 0.12f, 1f)
            : normalColor;
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
                texture.SetPixel(x, y, color);
            }
        }
    }

    private static void SetMarker(Texture2D texture, int x, int y, Color color)
    {
        texture.SetPixel(x, y, color);
        texture.SetPixel(x + 1, y, color);
        texture.SetPixel(x, y + 1, color);
        texture.SetPixel(x + 1, y + 1, color);
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
