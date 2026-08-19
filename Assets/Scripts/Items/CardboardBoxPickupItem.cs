using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stackable cardboard-box disguise and one-slot world container.
/// Ground boxes take two attacks; worn boxes redirect two character hits.
/// </summary>
public sealed class CardboardBoxPickupItem : PickupItemBase
{
    private static readonly HashSet<CardboardBoxPickupItem> ActiveBoxes =
        new HashSet<CardboardBoxPickupItem>();

    [Header("Worn Box")]
    [SerializeField, Min(0f)] private float staminaDrainPerSecond = 1f;
    [SerializeField, Range(0.1f, 1f)] private float wornMovementSpeedMultiplier = 0.5f;
    [SerializeField, Min(1)] private int maximumBlockedAttacks = 2;
    [SerializeField] private Vector2 wornVisualOffset = new Vector2(0f, 0.08f);

    [Header("Ground Box")]
    [SerializeField, Min(0.1f)] private float itemInsertionDistance = 1.35f;
    [SerializeField, Min(1)] private int groundAttackDurability = 2;
    [SerializeField, Min(1)] private int fragmentCount = 14;
    [SerializeField, Min(0f)] private float fragmentLifetime = 0.7f;
    [SerializeField, Min(0f)] private float fragmentSpeed = 2.8f;
    [SerializeField, Min(0f)] private float fragmentScale = 0.13f;

    private bool hasStoredItem;
    private string storedItemId;
    private string storedItemName;
    private string storedItemDescription;
    private string storedItemInstanceId;
    private bool storedItemHasVisualColor;
    private Color storedItemVisualColor = Color.white;
    private bool storedItemHasCharge;
    private float storedItemCharge;
    private PickupItemBase storedItemPrefab;
    private ClockworkPuppetBoxDriver storedPuppetDriver;
    private int receivedGroundAttacks;
    private bool isDestroyed;
    private SpriteRenderer boxRenderer;
    private Vector3 baseLocalPosition;
    private float shakeTimer;

    public static IReadOnlyCollection<CardboardBoxPickupItem> WorldBoxes =>
        ActiveBoxes;
    public float StaminaDrainPerSecond => staminaDrainPerSecond;
    public float WornMovementSpeedMultiplier => wornMovementSpeedMultiplier;
    public int MaximumBlockedAttacks => maximumBlockedAttacks;
    public Vector2 WornVisualOffset => wornVisualOffset;
    public int FragmentCount => fragmentCount;
    public float FragmentLifetime => fragmentLifetime;
    public float FragmentSpeed => fragmentSpeed;
    public float FragmentScale => fragmentScale;
    public bool HasStoredItem => hasStoredItem;
    public bool IsDestroyed => isDestroyed;
    public bool IsClockworkPuppetDriven => storedPuppetDriver != null;
    public bool IsClockworkPuppetMoving =>
        storedPuppetDriver != null && storedPuppetDriver.IsMoving;
    public int RemainingGroundBlockedAttacks => Mathf.Max(
        0,
        Mathf.Max(1, groundAttackDurability) - receivedGroundAttacks);
    public Vector2 WorldCenter => boxRenderer != null
        ? boxRenderer.bounds.center
        : transform.position;
    // A filled box remains a valid interaction target. Picking it up releases
    // its contents as a separate world pickup rather than requiring the
    // player to destroy the box first.
    protected override bool CanAttemptPickup => !isDestroyed;
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;

    protected override void Awake()
    {
        base.Awake();
        boxRenderer = GetComponent<SpriteRenderer>();
        baseLocalPosition = transform.localPosition;
    }

    private void OnEnable()
    {
        ActiveBoxes.Add(this);
    }

    protected override void OnDisable()
    {
        ActiveBoxes.Remove(this);
        base.OnDisable();
    }

    public override bool TryPickUp()
    {
        if (!CanAttemptPickup || IsPickupBlockedForControlledCharacter())
        {
            return false;
        }

        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory == null ||
            !inventory.TryAddItem(ItemId, 1, MaxStackSize))
        {
            return false;
        }

        OnPickedUp(inventory);
        NotifyPickedUp();
        if (hasStoredItem)
        {
            // Picking up a container is not the same as breaking it. Release
            // the contents safely, without invoking destruction reactions
            // such as a stored bomb exploding.
            ReleaseStoredItem(null, false);
        }
        Destroy(gameObject);
        return true;
    }

    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        if (user == null || user is GhostZeldaCharacterData)
        {
            return false;
        }

        ZeldaFourWayMover mover = user.GetComponent<ZeldaFourWayMover>();
        if (mover == null || !mover.isActiveAndEnabled ||
            mover.ActiveCardboardBox != null)
        {
            return false;
        }

        CardboardBoxWearState state =
            user.gameObject.AddComponent<CardboardBoxWearState>();
        state.Configure(this, mover);
        // This object is the short-lived inventory-use instance rather than a
        // box placed in the world, so it must never be considered by AI.
        ActiveBoxes.Remove(this);
        return true;
    }

    public static CardboardBoxPickupItem FindNearestAvailableContainer(
        Vector2 worldPosition)
    {
        CardboardBoxPickupItem closest = null;
        float closestSquaredDistance = float.MaxValue;
        foreach (CardboardBoxPickupItem box in ActiveBoxes)
        {
            if (box == null || !box.isActiveAndEnabled || box.isDestroyed ||
                box.hasStoredItem)
            {
                continue;
            }

            float squaredDistance =
                (box.WorldCenter - worldPosition).sqrMagnitude;
            float maximumDistance = Mathf.Max(0.1f, box.itemInsertionDistance);
            if (squaredDistance > maximumDistance * maximumDistance ||
                squaredDistance >= closestSquaredDistance)
            {
                continue;
            }

            closest = box;
            closestSquaredDistance = squaredDistance;
        }

        return closest;
    }

    public bool TryStoreItem(
        PersistentInventory.Slot slot,
        PickupItemBase itemPrefab,
        ZeldaCharacterData user)
    {
        if (slot == null || slot.IsEmpty || itemPrefab == null ||
            hasStoredItem || isDestroyed ||
            !itemPrefab.CanBeStoredInCardboardBox(user))
        {
            return false;
        }

        storedItemId = slot.ItemId;
        storedItemName = slot.ItemName;
        storedItemDescription = slot.ItemDescription;
        storedItemInstanceId = slot.ItemInstanceId;
        storedItemHasVisualColor = slot.HasVisualColor;
        storedItemVisualColor = slot.VisualColor;
        storedItemHasCharge = slot.HasStoredCharge;
        storedItemCharge = slot.StoredCharge;
        storedItemPrefab = itemPrefab;
        hasStoredItem = true;
        ClockworkPuppetPickupItem puppet = itemPrefab as ClockworkPuppetPickupItem;
        if (puppet != null)
        {
            storedPuppetDriver = GetComponent<ClockworkPuppetBoxDriver>();
            if (storedPuppetDriver == null)
            {
                storedPuppetDriver = gameObject.AddComponent<ClockworkPuppetBoxDriver>();
            }
            storedPuppetDriver.Configure(
                this,
                puppet,
                storedItemHasCharge
                    ? storedItemCharge
                    : puppet.MaximumMagic,
                storedItemInstanceId,
                storedItemName,
                storedItemDescription,
                storedItemHasVisualColor,
                storedItemVisualColor);
        }
        return true;
    }

    public void MoveGroundBoxTowards(Vector2 destination, float maximumDistanceDelta)
    {
        if (isDestroyed)
        {
            return;
        }
        transform.position = Vector2.MoveTowards(
            transform.position,
            destination,
            Mathf.Max(0f, maximumDistanceDelta));
        // LateUpdate restores this position after hit-shake, so autonomous
        // movement must advance the restoration anchor as well.
        baseLocalPosition = transform.localPosition;
    }

    public void MoveGroundBoxWithNavigation(
        Rigidbody2D physicsBody,
        Vector2 displacement)
    {
        if (isDestroyed || physicsBody == null ||
            displacement.sqrMagnitude <= 0f)
        {
            return;
        }
        Vector2 nextPosition = physicsBody.position + displacement;
        physicsBody.MovePosition(nextPosition);
        if (transform.parent != null)
        {
            baseLocalPosition = transform.parent.InverseTransformPoint(nextPosition);
        }
        else
        {
            baseLocalPosition = new Vector3(
                nextPosition.x,
                nextPosition.y,
                transform.localPosition.z);
        }
    }

    public bool TryDetachStoredClockworkPuppet(
        ClockworkPuppetPickupItem expectedPuppet)
    {
        ClockworkPuppetPickupItem storedPuppet =
            storedItemPrefab as ClockworkPuppetPickupItem;
        if (!hasStoredItem || expectedPuppet == null || storedPuppet == null ||
            storedPuppet.ItemId != expectedPuppet.ItemId)
        {
            return false;
        }
        ClearStoredItem();
        return true;
    }

    public bool DestroyStoredClockworkPuppet()
    {
        if (!hasStoredItem ||
            !(storedItemPrefab is ClockworkPuppetPickupItem))
        {
            return false;
        }

        ClearStoredItem();
        return true;
    }

    public void CancelLastStoredItem()
    {
        ClearStoredItem();
    }

    public void ReceiveAttack(int attackPower, ZeldaCharacterData attacker)
    {
        if (isDestroyed || attackPower <= 0)
        {
            return;
        }

        receivedGroundAttacks++;
        shakeTimer = 0.2f;
        if (receivedGroundAttacks >= Mathf.Max(1, groundAttackDurability))
        {
            BreakGroundBox(attacker != null
                ? attacker.GetComponent<ZeldaCharacterAiBase>()
                : null);
        }
    }

    private void LateUpdate()
    {
        if (shakeTimer <= 0f)
        {
            transform.localPosition = baseLocalPosition;
            return;
        }

        shakeTimer = Mathf.Max(0f, shakeTimer - Time.deltaTime);
        float shake = Mathf.Sin(Time.time * 90f) * 0.06f;
        transform.localPosition = baseLocalPosition + Vector3.right * shake;
    }

    private void BreakGroundBox(ZeldaCharacterAiBase observingAi)
    {
        if (isDestroyed)
        {
            return;
        }

        isDestroyed = true;
        SpawnBreakFragments(WorldCenter, boxRenderer);
        if (storedItemPrefab is ClockworkPuppetPickupItem)
        {
            // A deployed puppet is physically inside this box. Destroying
            // the container destroys the mechanism instead of turning it
            // back into a recoverable pickup.
            ClearStoredItem();
        }
        else
        {
            ReleaseStoredItem(observingAi, true);
        }
        Destroy(gameObject);
    }

    private void ReleaseStoredItem(
        ZeldaCharacterAiBase observingAi,
        bool invokeReleaseEffect)
    {
        if (!hasStoredItem || storedItemPrefab == null)
        {
            ClearStoredItem();
            return;
        }

        if (storedPuppetDriver != null)
        {
            storedItemHasCharge = true;
            storedItemCharge = storedPuppetDriver.RemainingMagic;
        }

        PickupItemBase released = Instantiate(
            storedItemPrefab.gameObject,
            transform.position,
            Quaternion.identity).GetComponent<PickupItemBase>();
        released.gameObject.SetActive(true);
        released.SetUniqueInstanceId(storedItemInstanceId);
        if (!string.IsNullOrWhiteSpace(storedItemName))
        {
            released.SetItemName(storedItemName);
        }
        if (!string.IsNullOrWhiteSpace(storedItemDescription))
        {
            released.SetItemDescription(storedItemDescription);
        }
        if (storedItemHasVisualColor && released.ItemVisual != null)
        {
            released.ItemVisual.SetDisplayColor(storedItemVisualColor);
        }
        released.ApplyInventoryCharge(
            storedItemHasCharge,
            storedItemCharge);
        released.MarkAsDropped();
        if (invokeReleaseEffect)
        {
            released.OnReleasedFromCardboardBox(observingAi);
        }
        ClearStoredItem();
    }

    private void ClearStoredItem()
    {
        if (storedPuppetDriver != null)
        {
            Destroy(storedPuppetDriver);
            storedPuppetDriver = null;
        }
        hasStoredItem = false;
        storedItemId = string.Empty;
        storedItemName = string.Empty;
        storedItemDescription = string.Empty;
        storedItemInstanceId = string.Empty;
        storedItemHasVisualColor = false;
        storedItemVisualColor = Color.white;
        storedItemHasCharge = false;
        storedItemCharge = 0f;
        storedItemPrefab = null;
    }

    public void SpawnGroundBox(Vector3 worldPosition)
    {
        SpawnGroundBox(ItemId, worldPosition, groundAttackDurability);
    }

    public static CardboardBoxPickupItem SpawnGroundBox(
        string itemId,
        Vector3 worldPosition,
        int remainingDurability = 2)
    {
        PersistentInventory inventory = PersistentInventory.Instance;
        PickupItemBase prefab = inventory != null
            ? inventory.ResolveItemPrefab(itemId)
            : null;
        if (prefab == null)
        {
            return null;
        }

        PickupItemBase groundBox = Instantiate(
            prefab.gameObject,
            worldPosition,
            Quaternion.identity).GetComponent<PickupItemBase>();
        groundBox.gameObject.SetActive(true);
        groundBox.SetUniqueInstanceId(string.Empty);
        groundBox.MarkAsDropped();
        CardboardBoxPickupItem cardboardBox =
            groundBox as CardboardBoxPickupItem;
        if (cardboardBox != null)
        {
            int durability = Mathf.Max(1, cardboardBox.groundAttackDurability);
            cardboardBox.receivedGroundAttacks = durability -
                Mathf.Clamp(remainingDurability, 1, durability);
        }
        return cardboardBox;
    }

    public void SpawnBreakFragments(
        Vector3 worldPosition,
        SpriteRenderer sourceRenderer)
    {
        Color fragmentColor = new Color(0.72f, 0.55f, 0.32f, 1f);
        int sortingLayerId = 0;
        int sortingOrder = 3;
        if (sourceRenderer != null)
        {
            fragmentColor = sourceRenderer.color;
            sortingLayerId = sourceRenderer.sortingLayerID;
            sortingOrder = sourceRenderer.sortingOrder + 1;
        }

        int count = Mathf.Max(1, fragmentCount);
        for (int i = 0; i < count; i++)
        {
            float angle = 360f * i / count + Random.Range(-12f, 12f);
            Vector2 direction = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));
            GameObject fragmentObject = new GameObject("Cardboard Fragment");
            fragmentObject.transform.position = worldPosition;
            fragmentObject.transform.localScale = Vector3.one *
                Mathf.Max(0f, fragmentScale) * Random.Range(0.7f, 1.25f);
            SpriteRenderer fragmentRenderer =
                fragmentObject.AddComponent<SpriteRenderer>();
            fragmentRenderer.sprite = DoorFragmentVisual.FragmentSprite;
            fragmentRenderer.color = fragmentColor;
            fragmentRenderer.sortingLayerID = sortingLayerId;
            fragmentRenderer.sortingOrder = sortingOrder;
            DoorFragmentVisual fragment =
                fragmentObject.AddComponent<DoorFragmentVisual>();
            fragment.Configure(
                direction * fragmentSpeed * Random.Range(0.7f, 1.2f),
                fragmentLifetime,
                Random.Range(-540f, 540f));
        }
    }

    protected override void OnDestroy()
    {
        ActiveBoxes.Remove(this);
        base.OnDestroy();
    }

#if UNITY_EDITOR
    protected override void OnValidate()
    {
        base.OnValidate();
        staminaDrainPerSecond = Mathf.Max(0f, staminaDrainPerSecond);
        wornMovementSpeedMultiplier = Mathf.Clamp(
            wornMovementSpeedMultiplier, 0.1f, 1f);
        maximumBlockedAttacks = Mathf.Max(1, maximumBlockedAttacks);
        itemInsertionDistance = Mathf.Max(0.1f, itemInsertionDistance);
        groundAttackDurability = Mathf.Max(1, groundAttackDurability);
        fragmentCount = Mathf.Max(1, fragmentCount);
        fragmentLifetime = Mathf.Max(0f, fragmentLifetime);
        fragmentSpeed = Mathf.Max(0f, fragmentSpeed);
        fragmentScale = Mathf.Max(0f, fragmentScale);
    }
#endif
}
