using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Extensible base behaviour for items that can be picked up, used and dropped.</summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PickupItemVisualBase))]
public class PickupItemBase : MonoBehaviour, ISerializationCallbackReceiver
{
    public event Action<PickupItemBase> PickedUp;

    [Header("Item Identity")]
    [SerializeField] private string itemName = "未命名物品";
    [SerializeField, TextArea(2, 6), Tooltip("显示在物品信息界面中的说明。场景内的物品实例（包括钥匙）可以单独覆写。")]
    private string itemDescription = string.Empty;
    [SerializeField] private string itemId = "unknown_item";
    [Min(0), InspectorName("使用所需技巧值"), Tooltip("按R使用此物品的最低技巧值，0表示无要求。请在 Resources/PickupItems 中的物品预制体上统一配置；背包按物品ID读取该预制体。撬锁、开门等机关自身的要求保持独立。")]
    public int requiredUseSkill;
    // Old prefabs/scenes have no serialized requirement. Migrate once, then an
    // explicitly authored zero must remain zero even on a previously gated item.
    [SerializeField, HideInInspector] private bool useSkillRequirementInitialized;
    [SerializeField, Tooltip("Optional shared identity for referencing this specific placed item from another scene.")]
    private PickupItemPersistentIdentity persistentIdentity;
    [SerializeField] private string uniqueInstanceId;
    [SerializeField, Min(1)] private int pickupQuantity = 1;
    [SerializeField, Min(1)] private int maxStackSize = 1;
    [SerializeField, Min(0.1f)] private float pickupDistance = 1.25f;
    [SerializeField, Min(0f)] private float droppedPickupDelay = 0.35f;
    [SerializeField] private Vector2 pickupPromptOffset = new Vector2(0f, 0.9f);
    [SerializeField] private bool restorePreviousTaskOnPickup;
    [Header("Journal Changes When Picked Up")]
    [SerializeField, Tooltip("Add, update or remove journal entries after this item enters the inventory.")]
    private QuestJournalEventChange[] journalChangesOnPickup;

    private Collider2D triggerCollider;
    private float pickupLockedUntil;
    private GameObject pickupPromptObject;
    private TextMesh pickupPromptText;
    private Font pickupPromptFont;
    private Material pickupPromptMaterial;
    // InventoryChanged/PickedUp can synchronously re-enter, while Destroy waits
    // until frame end. A fungible stack no longer retains this object's unique ID.
    private bool pickupInProgress;
    private bool pickupCommitted;
    protected bool IsPickupBeingConsumed => pickupInProgress || pickupCommitted;
    internal bool IsAvailableForShop => !IsPickupBeingConsumed && CanAttemptPickup;

    // Shop transactions own inventory mutation and deliberately avoid opening a
    // second modal item-description window on top of the conversation.
    internal virtual void CompleteShopPurchase(PersistentInventory inventory)
    {
        if (ItemId == GoldCoinPickupItem.DefaultItemId && PickupQuantity > 1)
            PickupQuantity--;
        else
        {
            pickupCommitted = true;
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
        OnPickedUp(inventory);
        PickedUp?.Invoke(this);
    }

    public string ItemId => itemId;
    public string ItemName => itemName;
    public string ItemDescription => FormatDescriptionWithRequirements(itemDescription);
    protected virtual int DefaultRequiredUseSkill => 0;
    public int RequiredUseSkill
    {
        get { InitializeUseSkillRequirement(); return Math.Max(0, requiredUseSkill); }
    }
    public virtual string SkillRequirementDescription => $"使用需要技巧值：{RequiredUseSkill}。";
    protected virtual string AdditionalSkillRequirementDescription => string.Empty;

    // Serialization callbacks can run off the main thread: only scalar values
    // and pure C# defaults are used here, never Unity object/editor APIs.
    private void InitializeUseSkillRequirement()
    {
        if (useSkillRequirementInitialized) return;
        if (requiredUseSkill == 0) requiredUseSkill = DefaultRequiredUseSkill;
        requiredUseSkill = Math.Max(0, requiredUseSkill);
        useSkillRequirementInitialized = true;
    }
    public void OnBeforeSerialize() => InitializeUseSkillRequirement();
    public void OnAfterDeserialize() => InitializeUseSkillRequirement();

    public string FormatDescriptionWithRequirements(string description)
    {
        string text = (description ?? string.Empty).TrimEnd();
        string extra = AdditionalSkillRequirementDescription;
        // Inventory/save descriptions may contain an older generated footer.
        // Replace ONLY complete trailing generated lines; leave authored prose
        // and custom per-instance descriptions intact.
        while (text.Length > 0)
        {
            int lineStart = text.LastIndexOf('\n') + 1;
            string line = text.Substring(lineStart).Trim();
            const string prefix = "使用需要技巧值：";
            bool useLine = line.StartsWith(prefix, StringComparison.Ordinal) && line.EndsWith("。", StringComparison.Ordinal) &&
                int.TryParse(line.Substring(prefix.Length, Math.Max(0, line.Length - prefix.Length - 1)), out _);
            if (!useLine && (string.IsNullOrEmpty(extra) || line != extra)) break;
            text = text.Substring(0, lineStart).TrimEnd();
        }
        if (!string.IsNullOrEmpty(extra)) text = string.IsNullOrEmpty(text) ? extra : text + "\n" + extra;
        return string.IsNullOrEmpty(text) ? SkillRequirementDescription : text + "\n" + SkillRequirementDescription;
    }
    public PickupItemPersistentIdentity PersistentIdentity => persistentIdentity;
    public string ConfiguredUniqueInstanceId => uniqueInstanceId;
    public string UniqueInstanceId
    {
        get
        {
            EnsureUniqueInstanceId();
            return uniqueInstanceId;
        }
    }
    public int MaxStackSize => maxStackSize;
    // Kept in the existing serialized field so scene travel/disk snapshots
    // preserve a dropped currency pile without introducing another save format.
    public int PickupQuantity
    {
        get => Mathf.Max(1, pickupQuantity);
        protected set => pickupQuantity = Mathf.Max(1, value);
    }
    protected float PickupDistance => pickupDistance;
    protected bool IsPickupLocked => Time.unscaledTime < pickupLockedUntil;
    public PickupItemVisualBase ItemVisual => GetComponent<PickupItemVisualBase>();
    public virtual string DescriptionDiscoveryId =>
        "item:" + GetType().FullName + ":" + ItemId + ":" + ItemName;
    public virtual bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => true;
    public virtual bool HasInventoryCharge => false;
    public virtual float InventoryCharge => 0f;
    public virtual string InventoryState => string.Empty;
    public virtual bool UsePlacesInWorld => false;
    public virtual bool CanUseFromInventory => true;
    public virtual bool UsesSimpleUseGesture => true;
    protected virtual bool UsesTriggerCollider => true;
    protected virtual bool CanAttemptPickup => true;

    protected virtual void Awake()
    {
        InitializeUseSkillRequirement();
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = UsesTriggerCollider;
        itemName = string.IsNullOrWhiteSpace(itemName) ? "未命名物品" : itemName.Trim();
        itemId = string.IsNullOrWhiteSpace(itemId) ? "unknown_item" : itemId.Trim();
        QuestJournalInteractionMarker.Configure(
            gameObject,
            false,
            string.Empty,
            journalChangesOnPickup);
        // SceneTravelStateManager assigns deterministic scene identities from
        // sceneLoaded, after Awake and before Start. Defer generating an ID so
        // a scene-authored pickup does not receive a new random value whenever
        // its scene is revisited.
    }

    protected virtual void Update()
    {
        ZeldaFourWayMover controlledMover = FindControlledMover();
        UpdatePickupPrompt(controlledMover);

        if (DocumentReader.IsInputBlocked ||
            Time.unscaledTime < pickupLockedUntil ||
            !Input.GetKeyDown(KeyCode.E))
        {
            return;
        }

        if (controlledMover == null ||
            !CanAttemptPickup ||
            IsGhostControlledMover(controlledMover) ||
            Vector2.Distance(transform.position, controlledMover.transform.position) > pickupDistance)
        {
            return;
        }

        ZeldaInteractionArbiter.Submit(
            this,
            controlledMover,
            KeyCode.E,
            transform.position,
            PickUpFromInteraction);
    }

    private void PickUpFromInteraction()
    {
        TryPickUp();
    }

    private void UpdatePickupPrompt(ZeldaFourWayMover controlledMover)
    {
        bool canPrompt = !DocumentReader.IsInputBlocked
            && Time.unscaledTime >= pickupLockedUntil
            && CanAttemptPickup
            && controlledMover != null
            && !IsGhostControlledMover(controlledMover)
            && Vector2.Distance(transform.position, controlledMover.transform.position) <= pickupDistance;
        if (!canPrompt)
        {
            SetPickupPromptVisible(false);
            return;
        }

        // Gameplay selection is independent of the optional HUD prompt.
        ZeldaInteractionArbiter.OfferInteraction(
            this,
            controlledMover,
            KeyCode.E,
            transform.position,
            SetPickupPromptVisible);
        EnsurePickupPrompt();
        if (pickupPromptObject == null)
            return;

        pickupPromptObject.transform.position =
            controlledMover.GetOverheadWorldPosition(pickupPromptOffset);
        pickupPromptObject.transform.rotation = Quaternion.identity;
        pickupPromptFont.RequestCharactersInTexture(
            "按[E]拾取",
            72,
            FontStyle.Normal);
        pickupPromptMaterial.mainTexture = pickupPromptFont.material.mainTexture;
    }

    private void EnsurePickupPrompt()
    {
        if (pickupPromptObject != null)
            return;

        ZeldaHealthHeartsUI ui = ZeldaHealthHeartsUI.Instance;
        pickupPromptFont = ui != null ? ui.PermissionLabelFont : null;
        if (pickupPromptFont == null)
            return;

        pickupPromptFont.RequestCharactersInTexture(
            "按[E]拾取",
            72,
            FontStyle.Normal);

        pickupPromptObject = new GameObject(name + " Pickup Prompt");
        pickupPromptText = pickupPromptObject.AddComponent<TextMesh>();
        pickupPromptText.text = "按[E]拾取";
        pickupPromptText.font = pickupPromptFont;
        pickupPromptText.fontSize = 72;
        pickupPromptText.characterSize = 0.035f;
        pickupPromptText.anchor = TextAnchor.MiddleCenter;
        pickupPromptText.alignment = TextAlignment.Center;
        pickupPromptText.color = ZeldaUiPalette.Primary;

        MeshRenderer promptRenderer = pickupPromptObject.GetComponent<MeshRenderer>();
        int highestSortingLayerId = 0;
        int highestSortingLayerValue = int.MinValue;
        foreach (SortingLayer sortingLayer in SortingLayer.layers)
        {
            if (sortingLayer.value > highestSortingLayerValue)
            {
                highestSortingLayerValue = sortingLayer.value;
                highestSortingLayerId = sortingLayer.id;
            }
        }

        promptRenderer.sortingLayerID = highestSortingLayerId;
        promptRenderer.sortingOrder = short.MaxValue - 2;
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(promptRenderer);
        pickupPromptMaterial = new Material(pickupPromptFont.material)
        {
            name = name + " Pickup Prompt Font Material",
            hideFlags = HideFlags.HideAndDontSave
        };
        pickupPromptMaterial.mainTexture = pickupPromptFont.material.mainTexture;
        promptRenderer.sharedMaterial = pickupPromptMaterial;
        pickupPromptObject.SetActive(false);
    }

    private void SetPickupPromptVisible(bool visible)
    {
        if (pickupPromptObject != null
            && pickupPromptObject.activeSelf != visible)
        {
            pickupPromptObject.SetActive(visible);
        }
    }

    public virtual bool TryPickUp()
    {
        if (pickupInProgress || pickupCommitted || IsPickupBlockedForControlledCharacter())
        {
            return false;
        }

        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory == null) return false;
        pickupInProgress = true;
        if (!inventory.TryAddUniqueItem(
                itemId,
                UniqueInstanceId,
                PickupQuantity,
                maxStackSize,
                ItemName,
                ItemDescription,
                ItemVisual != null,
                ItemVisual != null ? ItemVisual.DisplayColor : Color.white,
                HasInventoryCharge,
                InventoryCharge,
                InventoryState))
        {
            pickupInProgress = false;
            return false;
        }

        pickupCommitted = true;
        pickupInProgress = false;
        try
        {
            OnPickedUp(inventory);
            NotifyPickedUp();
            if (restorePreviousTaskOnPickup && ZeldaHealthHeartsUI.Instance != null)
            {
                ZeldaHealthHeartsUI.Instance.RestorePreviousTaskPointerTarget();
            }
        }
        finally { Destroy(gameObject); }
        return true;
    }

    public bool ExecuteUse(ZeldaCharacterData user)
    {
        if (!CharacterSkillRequirements.Require(user, RequiredUseSkill)) return false;
        var mover = user != null ? user.GetComponent<ZeldaFourWayMover>() : null;
        if (UsesSimpleUseGesture && mover != null && mover.IsAttacking) return false;
        bool consumed = ApplyUseEffect(user);
        if (consumed)
        {
            // Drinking Veil creates a new veil; do not immediately cancel that new effect.
            if (!(this is PotionPickupItem potion) || potion.Kind != AlchemyPotionKind.Veil)
                CharacterPotionBuffs.NotifyAction(user);
            PlayUseGesture(user);
            Destroy(gameObject);
        }

        return consumed;
    }

    public void PlayUseGesture(ZeldaCharacterData user)
    {
        if (UsesSimpleUseGesture && user != null)
            user.GetComponent<ZeldaFourWayMover>()?.TryBeginItemAttack(.24f);
    }

    /// <summary>Short forward placement; a blocked destination/path falls back to the user's feet.</summary>
    public static Vector3 ResolvePlacementPosition(ZeldaCharacterData user, Vector3 fallback,
        Vector2 footprint, Vector2 centerOffset, GameObject ignoredItem)
    {
        var mover = user != null ? user.GetComponent<ZeldaFourWayMover>() : null;
        if (mover == null) return fallback;
        Vector3 origin = user.transform.position;
        Vector2 facing = mover.FacingDirection;
        Vector2 direction = Mathf.Abs(facing.x) > Mathf.Abs(facing.y)
            ? (facing.x < 0f ? Vector2.left : Vector2.right)
            : (facing.y > 0f ? Vector2.up : Vector2.down);
        const float reach = .65f;
        Vector2 size = new Vector2(Mathf.Max(.05f, footprint.x), Mathf.Max(.05f, footprint.y));
        size += Vector2.one * .04f;
        Vector3 destination = origin + (Vector3)(direction * reach);
        // Include trigger walls too, independent of project-wide queriesHitTriggers settings.
        var filter = new ContactFilter2D { useTriggers = true };
        var hits = new List<RaycastHit2D>();
        Physics2D.BoxCast((Vector2)origin + centerOffset, size, 0f, direction, filter, hits, reach);
        foreach (var hit in hits)
            if (BlocksPlacement(hit.collider, user, ignoredItem)) return origin;
        // Explicit endpoint overlap also works when queriesStartInColliders is disabled.
        var overlaps = new List<Collider2D>();
        Physics2D.OverlapBox((Vector2)destination + centerOffset, size, 0f, filter, overlaps);
        foreach (var collider in overlaps)
            if (BlocksPlacement(collider, user, ignoredItem)) return origin;
        return destination;
    }

    private static bool BlocksPlacement(Collider2D collider, ZeldaCharacterData user, GameObject ignoredItem)
    {
        if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy ||
            collider.transform.IsChildOf(user.transform) ||
            (ignoredItem != null && collider.transform.IsChildOf(ignoredItem.transform))) return false;
        if (collider.gameObject.scene != ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject)) return false;
        int layer = collider.gameObject.layer;
        return !collider.isTrigger || layer == LayerMask.NameToLayer("Blocks") ||
            layer == LayerMask.NameToLayer("Hidden Blocks");
    }

    public void MarkAsDropped()
    {
        pickupLockedUntil = Time.unscaledTime + droppedPickupDelay;
        OnDropped();
    }

    public void SetUniqueInstanceId(string instanceId)
    {
        uniqueInstanceId = string.IsNullOrWhiteSpace(instanceId)
            ? Guid.NewGuid().ToString("N")
            : instanceId.Trim();
    }

    public void SetItemName(string newItemName)
    {
        itemName = string.IsNullOrWhiteSpace(newItemName)
            ? "未命名物品"
            : newItemName.Trim();
    }

    public void SetItemDescription(string newItemDescription)
    {
        itemDescription = string.IsNullOrWhiteSpace(newItemDescription)
            ? string.Empty
            : newItemDescription.Trim();
    }

    public virtual void ApplyInventoryCharge(bool hasCharge, float charge)
    {
    }

    public virtual void ApplyInventoryState(string state) { }

    /// <summary>
    /// Override in derived item behaviours. The base template consumes and destroys itself.
    /// Return true when one inventory item should be removed.
    /// </summary>
    protected virtual bool ApplyUseEffect(ZeldaCharacterData user)
    {
        return true;
    }

    protected virtual void OnPickedUp(PersistentInventory inventory)
    {
        if (journalChangesOnPickup == null || journalChangesOnPickup.Length == 0)
            return;

        QuestJournalManager.GetOrCreate().ApplyEventChanges(
            journalChangesOnPickup,
            gameObject.scene.name);
    }

    protected void NotifyPickedUp()
    {
        // Pickup feedback applies to every item, independently of its use-animation exceptions.
        // Start before a first-pickup description window can pause the game.
        FindControlledMover()?.TryBeginItemAttack(.24f);
        // Keep first-pickup information handling on the shared notification
        // path. Derived items such as BombPickupItem override TryPickUp, but
        // they already call this method after successfully entering inventory.
        ItemDescriptionWindow.GetOrCreate().NotifyPickedUp(this);
        PickedUp?.Invoke(this);
    }

    protected virtual void OnDropped()
    {
    }

    public virtual void OnReleasedFromCardboardBox(
        ZeldaCharacterAiBase observingAi)
    {
    }

    protected static bool IsPickupBlockedForControlledCharacter()
    {
        ActiveZeldaCharacterDataSource source =
            ActiveZeldaCharacterDataSource.Instance;
        ZeldaFourWayMover controlledMover =
            source != null && source.ActiveMover != null
                ? source.ActiveMover
                : ZeldaRuntimeRegistry.GetControlledMover();
        return IsGhostControlledMover(controlledMover);
    }

    public static bool IsGhostControlledMover(ZeldaFourWayMover mover)
    {
        return mover != null &&
               mover.GetComponent<ZeldaCharacterData>() != null && mover.GetComponent<ZeldaCharacterData>().IsGhostLike;
    }

    private ZeldaFourWayMover FindControlledMover()
    {
        ActiveZeldaCharacterDataSource source = ActiveZeldaCharacterDataSource.Instance;
        if (source != null && source.ActiveMover != null && source.ActiveMover.isActiveAndEnabled)
        {
            return source.ActiveMover;
        }

        return ZeldaRuntimeRegistry.GetControlledMover();
    }

    private void EnsureUniqueInstanceId()
    {
        if (persistentIdentity != null)
        {
            uniqueInstanceId = persistentIdentity.StableInstanceId;
            return;
        }

        if (string.IsNullOrWhiteSpace(uniqueInstanceId))
        {
            SceneTravelStableId sceneIdentity =
                GetComponent<SceneTravelStableId>();
            if (sceneIdentity != null &&
                !string.IsNullOrWhiteSpace(sceneIdentity.StableId) &&
                gameObject.scene.IsValid())
            {
                uniqueInstanceId =
                    "scene-pickup:" + gameObject.scene.name + ":" +
                    sceneIdentity.StableId;
            }
            else
            {
                // Runtime-created and dropped items are not part of the
                // authored hierarchy and must keep genuinely unique IDs.
                uniqueInstanceId = Guid.NewGuid().ToString("N");
            }
        }
        else
        {
            uniqueInstanceId = uniqueInstanceId.Trim();
        }
    }

    protected virtual void OnDisable()
    {
        SetPickupPromptVisible(false);
    }

    protected virtual void OnDestroy()
    {
        if (pickupPromptObject != null)
        {
            Destroy(pickupPromptObject);
        }

        if (pickupPromptMaterial != null)
        {
            Destroy(pickupPromptMaterial);
        }
    }

#if UNITY_EDITOR
    protected virtual void Reset()
    {
        requiredUseSkill = DefaultRequiredUseSkill;
        useSkillRequirementInitialized = true;
    }
    protected virtual void OnValidate()
    {
        InitializeUseSkillRequirement();
        requiredUseSkill = Mathf.Max(0, requiredUseSkill);
        itemName = string.IsNullOrWhiteSpace(itemName) ? "未命名物品" : itemName.Trim();
        itemDescription = string.IsNullOrWhiteSpace(itemDescription)
            ? string.Empty
            : itemDescription.Trim();
        itemId = string.IsNullOrWhiteSpace(itemId) ? "unknown_item" : itemId.Trim();
        pickupQuantity = Mathf.Max(1, pickupQuantity);
        maxStackSize = Mathf.Max(1, maxStackSize);
        pickupDistance = Mathf.Max(0.1f, pickupDistance);
        droppedPickupDelay = Mathf.Max(0f, droppedPickupDelay);

        Collider2D itemCollider = GetComponent<Collider2D>();
        if (itemCollider != null)
        {
            itemCollider.isTrigger = UsesTriggerCollider;
        }
    }
#endif
}
