using System.Collections.Generic;
using UnityEngine;

public enum ZeldaAttackVisualShape
{
    Box,
    Sword,
    Hammer,
    Rock,
    Shockwave,
    Book,
    TwinBlades,
    BattleAxe
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
    private static Sprite bookSprite;
    private static Sprite twinBladesSprite;
    private static Sprite profileBladeSprite;
    private static Sprite battleAxeSprite;

    // Symmetric cutting heads, a reinforced socket and a simple wooden haft.
    private static readonly string[] BattleAxePixels = {
        "..HH...GG...HH..",
        ".HHM...GG...MHH.",
        ".HMMGGGGGGGGMMH.",
        "HMMMGMHHHHMGMMMH",
        "HMMMGMHHHHMGMMMH",
        "HMMMGMHHHHMGMMMH",
        ".HMMGGGGGGGGMMH.",
        ".HHM...GG...MHH.",
        "..HH...GG...HH..",
        ".......WW.......",
        ".......WW.......",
        ".......GG.......",
        ".......WW.......",
        ".......WW.......",
        ".......WW.......",
        "......GGGG......",
    };

    // Coarse pixels match the characters: a two-tone blade, solid crossguard
    // and plain grip, without fine channels or alternating grip wraps.
    // Neutral values retain owner tint; 8 x 16 at 16 PPU keeps size/pivot intact.
    private static readonly string[] SwordPixels = {
        "...HH...",
        "..MHHM..",
        "..MHHM..",
        "..MHHM..",
        "..MHHM..",
        "..MHHM..",
        "..MHHM..",
        "..MHHM..",
        "..MHHM..",
        "..MHHM..",
        "..MHHM..",
        "GGGGGGGG",
        ".GGDDGG.",
        "...DD...",
        "...DD...",
        "..GGGG..",
    };

    // Same 16-PPU coarse blocks and neutral four-value palette as the sword.
    // A broad steel head, solid socket and plain grip, without fine surface texture.
    private static readonly string[] HammerPixels = {
        "................",
        "..GGHHHHHHHHGG..",
        "..GMHHHHHHHHMG..",
        "..GMMMMMMMMMMG..",
        "..GMMMMMMMMMMG..",
        "..GMMMMMMMMMMG..",
        "..GGGGDDDDGGGG..",
        "......GGGG......",
        "......DDDD......",
        "......DDDD......",
        "......DDDD......",
        "......DDDD......",
        "......DDDD......",
        "......DDDD......",
        "......GGGG......",
        "................",
    };

    // A small bound volume: D=outline/spine, C=cover, G=gold, P=page edges.
    private static readonly string[] BookPixels = {
        "............",
        ".DDDDDDDDD..",
        ".DGCCCCCCGD.",
        ".DGCCCCCCGD.",
        ".DGCCGGCCGD.",
        ".DGCCGGCCGD.",
        ".DGCCCCCCGD.",
        ".DGCCCCCCGD.",
        ".DGGGGGGGGD.",
        ".DPPPPPPPGD.",
        ".DDDDDDDDD..",
        "............"
    };
    private readonly HashSet<ZeldaCharacterData> damagedTargets = new HashSet<ZeldaCharacterData>();
    private readonly HashSet<DoorData> damagedDoors = new HashSet<DoorData>();
    private readonly HashSet<DollAttackShake> shakenDolls = new HashSet<DollAttackShake>();
    private readonly HashSet<CardboardBoxPickupItem> damagedCardboardBoxes =
        new HashSet<CardboardBoxPickupItem>();
    private readonly HashSet<BombPickupItem> detonatedBombPickups =
        new HashSet<BombPickupItem>();
    private readonly HashSet<PlacedBomb> detonatedPlacedBombs =
        new HashSet<PlacedBomb>();

    private SpriteRenderer spriteRenderer;
    private BoxCollider2D triggerCollider;
    private Rigidbody2D physicsBody;
    private ZeldaCharacterData owner;
    private bool canDamageOwner;
    private bool isDirectCharacterAttack;
    private Vector3 configuredMaximumScale = Vector3.one;
    private Color configuredTint;
    private float elapsedLifetime;

    // A plain managed result survives destruction of the short-lived hitbox.
    // Only attacks observed by AI allocate this, and only the intended target counts.
    public sealed class AttackOutcome
    {
        public Object Target { get; internal set; }
        public bool HitTarget { get; internal set; }
        public bool IsComplete { get; internal set; }
    }
    private AttackOutcome observedOutcome;

    public AttackOutcome ObserveTarget(Object target)
    {
        observedOutcome = new AttackOutcome { Target = target };
        return observedOutcome;
    }

    private void RecordTargetContact(Object target)
    {
        if (observedOutcome != null && observedOutcome.Target == target)
            observedOutcome.HitTarget = true;
    }

    private void OnDisable()
    {
        if (observedOutcome != null) observedOutcome.IsComplete = true;
    }

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
        if (CancelForStunnedOwner()) return;
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
        bool allowOwnerDamage = false,
        bool directCharacterAttack = false)
    {
        lifetime = attackLifetime;
        attackPower = inheritedAttackPower;
        visualTint = tint;
        owner = attackOwner;
        canDamageOwner = allowOwnerDamage;
        isDirectCharacterAttack = directCharacterAttack;
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
        if (CancelForStunnedOwner()) return;
        ZeldaCharacterData target = other.GetComponentInParent<ZeldaCharacterData>();
        bool isDamageableOwner = target == owner && canDamageOwner;
        if (target != null && (target != owner || isDamageableOwner) &&
            target.CanTakeAttackDamage &&
            !target.IsDead && !damagedTargets.Contains(target))
        {
            damagedTargets.Add(target);
            RecordTargetContact(target);
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
        if (door != null && !door.IsDestroyed && !damagedDoors.Contains(door))
        {
            damagedDoors.Add(door);
            door.ReceiveAttack(
                attackPower,
                owner,
                isDirectCharacterAttack);
        }

        BombPickupItem bombPickup = other.GetComponentInParent<BombPickupItem>();
        var crystal = other.GetComponentInParent<StabilityCrystalPickupItem>();
        if (crystal != null) crystal.BurstFromAttack();
        if (bombPickup != null && detonatedBombPickups.Add(bombPickup))
        {
            bombPickup.DetonateFromAttack(owner);
        }

        PlacedBomb placedBomb = other.GetComponentInParent<PlacedBomb>();
        if (placedBomb != null && detonatedPlacedBombs.Add(placedBomb))
        {
            placedBomb.DetonateImmediately();
        }

        CardboardBoxPickupItem cardboardBox =
            other.GetComponentInParent<CardboardBoxPickupItem>();
        if (cardboardBox == null || cardboardBox.IsDestroyed ||
            damagedCardboardBoxes.Contains(cardboardBox))
        {
            return;
        }

        damagedCardboardBoxes.Add(cardboardBox);
        RecordTargetContact(cardboardBox);
        cardboardBox.ReceiveAttack(attackPower, owner);
    }

    private bool CancelForStunnedOwner()
    {
        // Do not cancel independent bomb explosions just because their owner is stunned.
        if (canDamageOwner || owner == null) return false;
        var ai = owner.GetComponent<ZeldaCharacterAiBase>();
        var automaton = ai as AutomatonCharacterAi;
        bool dormantAutomaton = automaton != null && automaton.isActiveAndEnabled &&
            !automaton.HostilityActivated;
        if (!owner.IsGhostForm && !dormantAutomaton &&
            (ai == null || !ai.isActiveAndEnabled || !ai.IsStunned)) return false;
        triggerCollider.enabled = false;
        if (spriteRenderer != null) spriteRenderer.enabled = false;
        Destroy(gameObject);
        return true;
    }

    private void ApplyVisual()
    {
        if (visualShape == ZeldaAttackVisualShape.BattleAxe)
        {
            if (battleAxeSprite == null) battleAxeSprite = CreateBattleAxeSprite();
            spriteRenderer.sprite = battleAxeSprite;
        }
        else if (visualShape == ZeldaAttackVisualShape.TwinBlades)
        {
            // In profile the far blade is hidden behind the near blade, just as
            // on the owner's side-facing sprite. Hitbox and damage stay unchanged.
            Vector3 attackDirection = transform.up;
            if (Mathf.Abs(attackDirection.x) > Mathf.Abs(attackDirection.y))
            {
                if (profileBladeSprite == null) profileBladeSprite = CreateTwinBladesSprite(true);
                spriteRenderer.sprite = profileBladeSprite;
            }
            else
            {
                if (twinBladesSprite == null) twinBladesSprite = CreateTwinBladesSprite(false);
                spriteRenderer.sprite = twinBladesSprite;
            }
        }
        else if (visualShape == ZeldaAttackVisualShape.Sword)
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
        else if (visualShape == ZeldaAttackVisualShape.Book)
        {
            if (bookSprite == null) bookSprite = CreateBookSprite();
            spriteRenderer.sprite = bookSprite;
        }
        else
        {
            spriteRenderer.sprite = boxSprite;
        }
    }

    private static Sprite CreateBattleAxeSprite()
    {
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) {
            name = "Coarse Double-Headed Battle Axe", filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
        };
        for (int row = 0; row < 16; row++)
        for (int x = 0; x < 16; x++)
        {
            char symbol = BattleAxePixels[row][x];
            texture.SetPixel(x, 15 - row, symbol == 'W'
                ? new Color(0.35f, 0.22f, 0.13f, 1f) : SwordPixelColor(symbol));
        }
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.18f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateTwinBladesSprite(bool profile)
    {
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) {
            name = profile ? "Automaton Profile Blade Hand" : "Automaton Twin Blade Hands", filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
        };
        for (int row = 0; row < 16; row++)
        for (int x = 0; x < 16; x++)
        {
            int localX = profile ? x - 6 : (x < 8 ? x - 3 : x - 9);
            Color color = Color.clear;
            if (row == 1 && localX == 2) color = new Color(0.86f, 0.9f, 0.91f, 1f);
            if (row >= 2 && row <= 11 && localX >= 1 && localX <= 2)
                color = localX == 2 ? new Color(0.86f, 0.9f, 0.91f, 1f) : new Color(0.46f, 0.5f, 0.5f, 1f);
            if (row >= 12 && row <= 13 && localX >= 0 && localX <= 3)
                color = new Color(0.82f, 0.035f, 0.025f, 1f);
            if (row >= 14 && localX >= 1 && localX <= 2)
                color = new Color(0.18f, 0.21f, 0.22f, 1f);
            texture.SetPixel(x, 15 - row, color);
        }
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.18f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateBookSprite()
    {
        var texture = new Texture2D(12, 12, TextureFormat.RGBA32, false)
        {
            name = "Priest Attack Book",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        for (int row = 0; row < 12; row++)
        for (int x = 0; x < 12; x++)
            texture.SetPixel(x, 11 - row, BookPixelColor(BookPixels[row][x]));
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 12, 12),
            new Vector2(0.5f, 0.5f), 12f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Color BookPixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'D': return new Color(0.20f, 0.15f, 0.12f, 1f);
            case 'C': return new Color(0.40f, 0.25f, 0.23f, 1f);
            case 'G': return new Color(0.78f, 0.62f, 0.29f, 1f);
            case 'P': return new Color(0.92f, 0.90f, 0.79f, 1f);
            default: return Color.clear;
        }
    }

    private static Sprite CreateHammerSprite()
    {
        var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
        {
            name = "Coarse Hammer Attack",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        for (int row = 0; row < 16; row++)
        for (int x = 0; x < 16; x++)
            texture.SetPixel(x, 15 - row, SwordPixelColor(HammerPixels[row][x]));
        texture.Apply();
        // Preserve the original 1x1 world footprint and grip pivot; hitbox is independent.
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16),
            new Vector2(0.5f, 0.2f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Sprite CreateSwordSprite()
    {
        var texture = new Texture2D(8, 16, TextureFormat.RGBA32, false)
        {
            name = "Coarse Sword Attack",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        for (int row = 0; row < 16; row++)
        for (int x = 0; x < 8; x++)
            texture.SetPixel(x, 15 - row, SwordPixelColor(SwordPixels[row][x]));
        texture.Apply();
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 8, 16),
            new Vector2(0.5f, 0.18f), 16f);
        sprite.name = texture.name;
        sprite.hideFlags = HideFlags.HideAndDontSave;
        return sprite;
    }

    private static Color SwordPixelColor(char symbol)
    {
        switch (symbol)
        {
            case 'H': return new Color(0.96f, 0.96f, 0.96f, 1f);
            case 'M': return new Color(0.80f, 0.80f, 0.80f, 1f);
            case 'G': return new Color(0.64f, 0.64f, 0.64f, 1f);
            case 'D': return new Color(0.38f, 0.38f, 0.38f, 1f);
            default: return Color.clear;
        }
    }

    private static void CreateSprites()
    {
        if (boxSprite != null && shockwaveSprite != null && swordSprite != null && hammerSprite != null)
        {
            return;
        }

        Texture2D boxTexture = new Texture2D(8, 8);
        boxTexture.filterMode = FilterMode.Point;

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
        rockTexture.Apply();
        shockwaveTexture.Apply(false, true);

        boxSprite = Sprite.Create(boxTexture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8f);
        swordSprite = CreateSwordSprite();
        hammerSprite = CreateHammerSprite();
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
