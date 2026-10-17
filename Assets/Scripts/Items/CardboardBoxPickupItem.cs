using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stackable cardboard-box disguise and one-slot world container.
/// Ground boxes take two attacks; worn boxes redirect two character hits.
/// </summary>
public sealed class CardboardBoxPickupItem : PickupItemBase, IConsumableExplosive
{
    public override bool UsesSimpleUseGesture => false;
    private static readonly HashSet<CardboardBoxPickupItem> ActiveBoxes =
        new HashSet<CardboardBoxPickupItem>();

    [Header("Worn Box")]
    [SerializeField, Min(0f)] private float staminaDrainPerSecond = 2f;
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
    // Primitive fields are captured by the existing container save system.
    // Do not create a separate PlacedBomb until release: it would be saved twice.
    private bool storedBombArmed;
    private float storedBombProximityCheckRemaining;
    private float storedBombRemainingFuse;
    private string storedBombOwnerId;
    private ZeldaCharacterData storedBombOwner;
    private CameraVisionStreamingRegion storedBombRetention;
    private CameraVisionStreamingExempt ownedBombExemption;
    private ClockworkPuppetBoxDriver storedPuppetDriver;
    private int receivedGroundAttacks;
    private bool isDestroyed;
    private SpriteRenderer boxRenderer;
    private Vector3 baseLocalPosition;
    private float shakeTimer;

    public static IReadOnlyCollection<CardboardBoxPickupItem> WorldBoxes =>
        ActiveBoxes;

    // NPC-only occlusion: do not turn the pickup trigger into solid geometry
    // or move it onto Blocks (either would also change player vision/physics).
    internal static float ClipNpcVisionRay(Vector2 origin, Vector2 direction,
        float distance, Transform observer, Transform ignoredTarget)
    {
        foreach (CardboardBoxPickupItem box in ActiveBoxes)
        {
            if (box == null || !box.isActiveAndEnabled || box.isDestroyed)
                continue;
            distance = CardboardBoxNpcVision.Clip(origin, direction, distance,
                observer, ignoredTarget, box.transform, box.boxRenderer);
        }
        return CardboardBoxWearState.ClipNpcVisionRay(
            origin, direction, distance, observer, ignoredTarget);
    }
    public float StaminaDrainPerSecond => staminaDrainPerSecond;
    public float WornMovementSpeedMultiplier => wornMovementSpeedMultiplier;
    public int MaximumBlockedAttacks => maximumBlockedAttacks;
    public Vector2 WornVisualOffset => wornVisualOffset;
    public int FragmentCount => fragmentCount;
    public float FragmentLifetime => fragmentLifetime;
    public float FragmentSpeed => fragmentSpeed;
    public float FragmentScale => fragmentScale;
    public bool HasStoredItem => hasStoredItem;
    public void PrepareContentsForSave()
    {
        if (storedPuppetDriver != null)
        {
            storedItemHasCharge = true;
            storedItemCharge = storedPuppetDriver.RemainingMagic;
        }
    }
    public void RestoreSavedContents(bool wasRemoteControlled)
    {
        baseLocalPosition = transform.localPosition;
        if (!hasStoredItem) return;
        foreach (var item in SaveGameCatalog.LoadPickupTemplates())
            if (item.ItemId == storedItemId) { storedItemPrefab = item; break; }
        if (storedBombArmed) RefreshStoredBombRetention();
        var puppet = storedItemPrefab as ClockworkPuppetPickupItem;
        if (puppet == null) return;
        storedPuppetDriver = GetComponent<ClockworkPuppetBoxDriver>();
        if (storedPuppetDriver == null) storedPuppetDriver = gameObject.AddComponent<ClockworkPuppetBoxDriver>();
        storedPuppetDriver.Configure(this, puppet, storedItemHasCharge ? storedItemCharge : puppet.MaximumMagic,
            storedItemInstanceId, storedItemName, storedItemDescription, storedItemHasVisualColor,
            storedItemVisualColor, wasRemoteControlled);
    }
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
            // Safe contents remain recoverable. An already lit bomb instead
            // stays in the world with its remaining fuse; pickup cannot defuse it.
            ReleaseStoredItem(null, false);
        }
        ActiveBoxes.Remove(this);
        Destroy(gameObject);
        return true;
    }

    internal override void CompleteShopPurchase(PersistentInventory inventory)
    {
        base.CompleteShopPurchase(inventory);
        // Match normal pickup: buying the container must not silently delete
        // something the player stored in it, nor defuse an already lit bomb.
        if (hasStoredItem) ReleaseStoredItem(null, false);
    }

    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        if (user == null || user.IsGhostLike)
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

        PersistentInventory.Instance?.RemoveClockworkPuppetRemote(storedItemInstanceId);
        ClearStoredItem();
        return true;
    }

    public void CancelLastStoredItem()
    {
        ClearStoredItem();
    }

    public bool TryConsumeExplosive(out int attackPower)
    {
        attackPower = 0;
        if (!isActiveAndEnabled || isDestroyed || !hasStoredItem) return false;
        if (storedItemPrefab is BombPickupItem bomb) attackPower = bomb.ExplosionAttackPower;
        else if (storedItemPrefab is BatteryPickupItem) attackPower = BatteryPickupItem.ExplosionAttackPower;
        else if (!(storedItemPrefab is StabilityCrystalPickupItem)) return false;
        ClearStoredItem(); // Also cancels a hidden lit fuse and its streaming lease.
        return true;
    }

    public void ArmStoredBomb(ZeldaCharacterData user)
    {
        var bomb = storedItemPrefab as BombPickupItem;
        if (!hasStoredItem || isDestroyed || storedBombArmed || bomb == null) return;
        storedBombArmed = true;
        storedBombProximityCheckRemaining = 0f;
        storedBombRemainingFuse = bomb.FuseDuration;
        storedBombOwner = user;
        storedBombOwnerId = user != null ? user.IndividualAccessId : string.Empty;
        RefreshStoredBombRetention();
    }

    private void RefreshStoredBombRetention()
    {
        var bomb = storedItemPrefab as BombPickupItem;
        if (!storedBombArmed || bomb == null) return;
        if (storedBombRetention == null)
        {
            if (GetComponent<CameraVisionStreamingExempt>() == null)
                ownedBombExemption = gameObject.AddComponent<CameraVisionStreamingExempt>();
            storedBombRetention = gameObject.AddComponent<CameraVisionStreamingRegion>();
        }
        storedBombRetention.Configure(bomb.StreamingRetentionRadius);
    }

    private ZeldaCharacterData ResolveStoredBombOwner()
    {
        if (storedBombOwner != null) return storedBombOwner;
        if (string.IsNullOrEmpty(storedBombOwnerId)) return null;
        foreach (var mover in ZeldaRuntimeRegistry.Movers)
        {
            if (mover == null) continue;
            var candidate = mover.GetComponent<ZeldaCharacterData>();
            if (candidate != null && candidate.IndividualAccessId == storedBombOwnerId)
                return storedBombOwner = candidate;
        }
        return null;
    }

    private void TickStoredBomb(float deltaTime)
    {
        if (!hasStoredItem || !storedBombArmed || isDestroyed) return;
        if (storedItemPrefab is BombPickupItem bomb)
        {
            if (bomb.IsProximity)
            {
                if (deltaTime <= 0f) return;
                storedBombProximityCheckRemaining -= deltaTime;
                if (storedBombProximityCheckRemaining > 0f) return;
                storedBombProximityCheckRemaining = BombProximitySensor.CheckInterval;
                if (BombProximitySensor.HasTarget(transform.position, bomb.ExplosionSize, gameObject.scene,
                    ResolveStoredBombOwner(), storedBombOwnerId)) BreakGroundBox(null);
                return;
            }
            if (bomb.IsDecoy) return;
        }
        storedBombRemainingFuse = Mathf.Max(0f, storedBombRemainingFuse - deltaTime);
        if (storedBombRemainingFuse <= 0f) BreakGroundBox(null);
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
        if (!GameSaveSystem.IsLoading) TickStoredBomb(Time.deltaTime);
        if (isDestroyed) return;
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
            PersistentInventory.Instance?.RemoveClockworkPuppetRemote(storedItemInstanceId);
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

        var armedBomb = storedItemPrefab as BombPickupItem;
        if (storedBombArmed && armedBomb != null)
        {
            PlacedBomb releasedBomb = armedBomb.CreateConfiguredPlacedBomb(
                transform.position, ResolveStoredBombOwner());
            releasedBomb.ExplosionSource = transform;
            // The owner may be gone or in another scene; the explosion belongs
            // to the container's scene, never the owner's current scene.
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(
                releasedBomb.gameObject, gameObject.scene);
            releasedBomb.RestoreRemainingFuse(storedBombRemainingFuse);
            releasedBomb.RestoreProximityOwnerId(storedBombOwnerId);
            releasedBomb.RestoreSavedOwner(ResolveStoredBombOwner());
            ClearStoredItem();
            if (invokeReleaseEffect) releasedBomb.DetonateImmediately();
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
        storedBombArmed = false;
        storedBombProximityCheckRemaining = 0f;
        storedBombRemainingFuse = 0f;
        storedBombOwnerId = string.Empty;
        storedBombOwner = null;
        if (storedBombRetention != null)
        {
            storedBombRetention.enabled = false;
            Destroy(storedBombRetention);
            storedBombRetention = null;
        }
        if (ownedBombExemption != null)
        {
            Destroy(ownedBombExemption);
            ownedBombExemption = null;
        }
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

/// <summary>Ray clipping shared by ground and worn boxes, independent of pickup triggers.</summary>
internal static class CardboardBoxNpcVision
{
    internal static float Clip(Vector2 origin, Vector2 direction, float distance,
        Transform observer, Transform ignoredTarget, Transform owner, SpriteRenderer renderer)
    {
        if (distance <= 0f || renderer == null || renderer.sprite == null || !renderer.enabled ||
            !renderer.gameObject.activeInHierarchy ||
            (observer != null && owner.IsChildOf(observer)) ||
            (ignoredTarget != null && owner.IsChildOf(ignoredTarget)))
            return distance;

        // Reject distant boxes before transforming the segment. No scene scan,
        // new colliders, or physics-layer changes.
        Vector2 end = origin + direction * distance;
        Bounds world = renderer.bounds;
        if (Mathf.Max(origin.x, end.x) < world.min.x || Mathf.Min(origin.x, end.x) > world.max.x ||
            Mathf.Max(origin.y, end.y) < world.min.y || Mathf.Min(origin.y, end.y) > world.max.y)
            return distance;

        // The usual same-scene path avoids scene-name lookups on every ray.
        // Traveling wearers live in DontDestroyOnLoad and belong to the active floor.
        if (observer != null && owner.gameObject.scene != observer.gameObject.scene &&
            ZeldaRuntimeRegistry.GetGameplayScene(owner.gameObject) !=
            ZeldaRuntimeRegistry.GetGameplayScene(observer.gameObject))
            return distance;

        Bounds sprite = renderer.sprite.bounds;
        // The 24x20 box art has loose lower flaps and transparent margins.
        // Only its main body casts a sight shadow. Sprite-local coordinates
        // also preserve the different ground/worn pivots, flips and transforms.
        Vector2 min = (Vector2)sprite.min + Vector2.Scale(sprite.size, new Vector2(4f / 24f, 4f / 20f));
        Vector2 max = (Vector2)sprite.min + Vector2.Scale(sprite.size, new Vector2(23f / 24f, 18f / 20f));
        if (renderer.flipX) { float x = min.x; min.x = -max.x; max.x = -x; }
        if (renderer.flipY) { float y = min.y; min.y = -max.y; max.y = -y; }
        Vector2 start = renderer.transform.InverseTransformPoint(origin);
        Vector2 delta = (Vector2)renderer.transform.InverseTransformPoint(end) - start;
        // Boxes remain passable. An NPC crossing one must not blind itself.
        if (start.x >= min.x && start.x <= max.x && start.y >= min.y && start.y <= max.y)
            return distance;
        float enter = 0f, exit = 1f;
        if (!ClipAxis(start.x, delta.x, min.x, max.x, ref enter, ref exit) ||
            !ClipAxis(start.y, delta.y, min.y, max.y, ref enter, ref exit))
            return distance;
        return distance * Mathf.Clamp01(enter);
    }

    private static bool ClipAxis(float start, float delta, float min, float max,
        ref float enter, ref float exit)
    {
        if (Mathf.Abs(delta) < .000001f)
            return start >= min && start <= max;
        float a = (min - start) / delta, b = (max - start) / delta;
        enter = Mathf.Max(enter, Mathf.Min(a, b));
        exit = Mathf.Min(exit, Mathf.Max(a, b));
        return enter <= exit;
    }
}
