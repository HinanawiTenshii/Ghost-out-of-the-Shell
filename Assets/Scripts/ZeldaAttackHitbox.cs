using System.Collections.Generic;
using UnityEngine;

public enum ZeldaAttackVisualShape
{
    Box,
    Sword,
    Hammer,
    Rock,
    Shockwave
}

[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(BoxCollider2D))]
[RequireComponent(typeof(Rigidbody2D))]
public class ZeldaAttackHitbox : MonoBehaviour
{
    [SerializeField] private float lifetime = 0.25f;
    [SerializeField] private int attackPower = 1;
    [SerializeField] private Color visualTint = new Color(1f, 0.85f, 0.2f, 0.65f);
    [SerializeField] private ZeldaAttackVisualShape visualShape;

    private static Sprite boxSprite;
    private static Sprite swordSprite;
    private static Sprite hammerSprite;
    private static Sprite rockSprite;
    private static Sprite shockwaveSprite;
    private readonly HashSet<ZeldaCharacterData> damagedTargets = new HashSet<ZeldaCharacterData>();
    private readonly HashSet<DoorData> damagedDoors = new HashSet<DoorData>();
    private readonly HashSet<DollAttackShake> shakenDolls = new HashSet<DollAttackShake>();

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D triggerCollider;
    private Rigidbody2D physicsBody;
    private ZeldaCharacterData owner;
    private bool canDamageOwner;
    private Vector3 configuredMaximumScale = Vector3.one;
    private Color configuredTint;
    private float elapsedLifetime;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        triggerCollider = GetComponent<BoxCollider2D>();
        physicsBody = GetComponent<Rigidbody2D>();
        if (physicsBody == null)
        {
            physicsBody = gameObject.AddComponent<Rigidbody2D>();
        }

        // Trigger messages are not generated between two static Collider2D
        // objects. Attack hitboxes therefore own a kinematic body so static
        // DoorData targets such as Glass are detected consistently.
        physicsBody.bodyType = RigidbodyType2D.Kinematic;
        physicsBody.gravityScale = 0f;
        physicsBody.simulated = true;
        physicsBody.constraints = RigidbodyConstraints2D.FreezeAll;

        CreateSprites();
        ApplyVisual();
        spriteRenderer.color = visualTint;
        spriteRenderer.sortingOrder = 2;

        triggerCollider.isTrigger = true;
        triggerCollider.size = Vector2.one;
    }

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }

    private void Update()
    {
        if (visualShape != ZeldaAttackVisualShape.Shockwave)
        {
            return;
        }

        elapsedLifetime += Time.deltaTime;
        float progress = Mathf.Clamp01(
            elapsedLifetime / Mathf.Max(0.01f, lifetime));
        float easedProgress = 1f - (1f - progress) * (1f - progress);
        transform.localScale = Vector3.Lerp(
            configuredMaximumScale * 0.08f,
            configuredMaximumScale,
            easedProgress);

        if (spriteRenderer != null)
        {
            Color fadingTint = configuredTint;
            fadingTint.a *= 1f - progress;
            spriteRenderer.color = fadingTint;
        }
    }

    public void Configure(
        float attackLifetime,
        Vector2 size,
        Color tint,
        int inheritedAttackPower,
        ZeldaCharacterData attackOwner,
        ZeldaAttackVisualShape inheritedVisualShape,
        bool allowOwnerDamage = false)
    {
        lifetime = attackLifetime;
        attackPower = inheritedAttackPower;
        visualTint = tint;
        owner = attackOwner;
        canDamageOwner = allowOwnerDamage;
        visualShape = inheritedVisualShape;

        configuredMaximumScale = new Vector3(size.x, size.y, 1f);
        configuredTint = visualTint;
        elapsedLifetime = 0f;
        transform.localScale =
            visualShape == ZeldaAttackVisualShape.Shockwave
                ? configuredMaximumScale * 0.08f
                : configuredMaximumScale;

        if (spriteRenderer != null)
        {
            ApplyVisual();
            spriteRenderer.color = visualTint;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDamage(other);
    }

    private void TryDamage(Collider2D other)
    {
        ZeldaCharacterData target = other.GetComponentInParent<ZeldaCharacterData>();
        bool isDamageableOwner = target == owner && canDamageOwner;
        if (target != null && (target != owner || isDamageableOwner) &&
            target.CanTakeAttackDamage &&
            !target.IsDead && !damagedTargets.Contains(target))
        {
            damagedTargets.Add(target);
            // Self-inflicted bomb damage should not broadcast the character
            // as its own attacker to AI hostility logic.
            target.TakeDamage(attackPower, target == owner ? null : owner);
        }

        DollAttackShake doll = other.GetComponentInParent<DollAttackShake>();
        if (doll != null && !shakenDolls.Contains(doll))
        {
            shakenDolls.Add(doll);
            doll.TriggerAttackShake();
        }

        DoorData door = other.GetComponentInParent<DoorData>();
        if (door == null || door.IsDestroyed || damagedDoors.Contains(door))
        {
            return;
        }

        damagedDoors.Add(door);
        door.ReceiveAttack(attackPower);
    }

    private void ApplyVisual()
    {
        if (visualShape == ZeldaAttackVisualShape.Sword)
        {
            spriteRenderer.sprite = swordSprite;
        }
        else if (visualShape == ZeldaAttackVisualShape.Hammer)
        {
            spriteRenderer.sprite = hammerSprite;
        }
        else if (visualShape == ZeldaAttackVisualShape.Rock)
        {
            spriteRenderer.sprite = rockSprite;
        }
        else if (visualShape == ZeldaAttackVisualShape.Shockwave)
        {
            spriteRenderer.sprite = shockwaveSprite;
        }
        else
        {
            spriteRenderer.sprite = boxSprite;
        }
    }

    private static void CreateSprites()
    {
        if (boxSprite != null && shockwaveSprite != null)
        {
            return;
        }

        Texture2D boxTexture = new Texture2D(8, 8);
        boxTexture.filterMode = FilterMode.Point;

        Texture2D swordTexture = new Texture2D(8, 16);
        swordTexture.filterMode = FilterMode.Point;

        Texture2D hammerTexture = new Texture2D(12, 12);
        hammerTexture.filterMode = FilterMode.Point;

        Texture2D rockTexture = new Texture2D(12, 12);
        rockTexture.filterMode = FilterMode.Point;

        Texture2D shockwaveTexture = new Texture2D(32, 32);
        shockwaveTexture.filterMode = FilterMode.Bilinear;
        shockwaveTexture.wrapMode = TextureWrapMode.Clamp;

        Color clear = new Color(1f, 1f, 1f, 0f);
        Color white = Color.white;

        for (int y = 0; y < 8; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                bool border = x == 0 || x == 7 || y == 0 || y == 7;
                boxTexture.SetPixel(x, y, border ? white : clear);
            }
        }

        for (int y = 0; y < 16; y++)
        {
            for (int x = 0; x < 8; x++)
            {
                swordTexture.SetPixel(x, y, clear);
            }
        }

        FillRect(swordTexture, 2, 5, 4, 9, white);
        swordTexture.SetPixel(1, 12, white);
        swordTexture.SetPixel(6, 12, white);
        FillRect(swordTexture, 3, 14, 2, 2, white);
        FillRect(swordTexture, 0, 4, 8, 1, white);
        FillRect(swordTexture, 2, 1, 4, 3, white);
        FillRect(swordTexture, 1, 0, 6, 1, white);

        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                hammerTexture.SetPixel(x, y, clear);
            }
        }

        FillRect(hammerTexture, 2, 7, 8, 4, white);
        FillRect(hammerTexture, 5, 2, 2, 6, white);
        FillRect(hammerTexture, 5, 0, 2, 2, white);

        for (int y = 0; y < 12; y++)
        {
            for (int x = 0; x < 12; x++)
            {
                rockTexture.SetPixel(x, y, clear);
            }
        }

        // Irregular but square-bounded silhouette, sized to match the complete hitbox.
        FillRect(rockTexture, 4, 1, 4, 1, white);
        FillRect(rockTexture, 3, 2, 6, 1, white);
        FillRect(rockTexture, 2, 3, 8, 2, white);
        FillRect(rockTexture, 1, 5, 10, 3, white);
        FillRect(rockTexture, 2, 8, 8, 2, white);
        FillRect(rockTexture, 4, 10, 4, 1, white);

        // Chipped corners and cracks make the small sprite read as a rough stone.
        rockTexture.SetPixel(1, 5, clear);
        rockTexture.SetPixel(10, 7, clear);
        rockTexture.SetPixel(4, 7, clear);
        rockTexture.SetPixel(5, 6, clear);
        rockTexture.SetPixel(6, 5, clear);
        rockTexture.SetPixel(7, 4, clear);
        rockTexture.SetPixel(8, 8, clear);
        rockTexture.SetPixel(7, 9, clear);

        Vector2 shockwaveCenter = new Vector2(15.5f, 15.5f);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                float distance = Vector2.Distance(
                    new Vector2(x, y),
                    shockwaveCenter);
                bool outerRing = distance >= 12.2f && distance <= 14.8f;
                bool innerRing = distance >= 8.2f && distance <= 9.6f;
                float alpha = outerRing ? 1f : innerRing ? 0.48f : 0f;
                shockwaveTexture.SetPixel(
                    x,
                    y,
                    new Color(1f, 1f, 1f, alpha));
            }
        }

        boxTexture.Apply();
        swordTexture.Apply();
        hammerTexture.Apply();
        rockTexture.Apply();
        shockwaveTexture.Apply(false, true);

        boxSprite = Sprite.Create(boxTexture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        swordSprite = Sprite.Create(swordTexture, new Rect(0, 0, 8, 16), new Vector2(0.5f, 0.18f), 16f);
        hammerSprite = Sprite.Create(hammerTexture, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.2f), 12f);
        rockSprite = Sprite.Create(rockTexture, new Rect(0, 0, 12, 12), new Vector2(0.5f, 0.5f), 12f);
        shockwaveSprite = Sprite.Create(
            shockwaveTexture,
            new Rect(0, 0, 32, 32),
            new Vector2(0.5f, 0.5f),
            32f);
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
