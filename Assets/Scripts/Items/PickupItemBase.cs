using System;
using UnityEngine;

/// <summary>Extensible base behaviour for items that can be picked up, used and dropped.</summary>
[RequireComponent(typeof(Collider2D))]
[RequireComponent(typeof(PickupItemVisualBase))]
public class PickupItemBase : MonoBehaviour
{
    public event Action<PickupItemBase> PickedUp;

    [Header("Item Identity")]
    [SerializeField] private string itemName = "未命名物品";
    [SerializeField, TextArea(2, 6), Tooltip("显示在物品信息界面中的说明。场景内的物品实例（包括钥匙）可以单独覆写。")]
    private string itemDescription = string.Empty;
    [SerializeField] private string itemId = "unknown_item";
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

    public string ItemId => itemId;
    public string ItemName => itemName;
    public string ItemDescription => itemDescription ?? string.Empty;
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
    public PickupItemVisualBase ItemVisual => GetComponent<PickupItemVisualBase>();
    public virtual string DescriptionDiscoveryId =>
        "item:" + GetType().FullName + ":" + ItemId + ":" + ItemName;
    public virtual bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => true;
    public virtual bool HasInventoryCharge => false;
    public virtual float InventoryCharge => 0f;
    protected virtual bool CanAttemptPickup => true;

    protected virtual void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        triggerCollider.isTrigger = true;
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

        EnsurePickupPrompt();
        if (pickupPromptObject == null)
            return;

        pickupPromptObject.transform.position =
            controlledMover.GetOverheadWorldPosition(pickupPromptOffset);
        pickupPromptObject.transform.rotation = Quaternion.identity;
        ZeldaInteractionArbiter.OfferInteraction(
            this,
            controlledMover,
            KeyCode.E,
            transform.position,
            SetPickupPromptVisible);

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
        if (IsPickupBlockedForControlledCharacter())
        {
            return false;
        }

        PersistentInventory inventory = PersistentInventory.Instance;
        if (inventory == null ||
            !inventory.TryAddUniqueItem(
                itemId,
                UniqueInstanceId,
                pickupQuantity,
                maxStackSize,
                ItemName,
                ItemDescription,
                ItemVisual != null,
                ItemVisual != null ? ItemVisual.DisplayColor : Color.white,
                HasInventoryCharge,
                InventoryCharge))
        {
            return false;
        }

        OnPickedUp(inventory);
        NotifyPickedUp();
        if (restorePreviousTaskOnPickup && ZeldaHealthHeartsUI.Instance != null)
        {
            ZeldaHealthHeartsUI.Instance.RestorePreviousTaskPointerTarget();
        }
        Destroy(gameObject);
        return true;
    }

    public bool ExecuteUse(ZeldaCharacterData user)
    {
        bool consumed = ApplyUseEffect(user);
        if (consumed)
        {
            Destroy(gameObject);
        }

        return consumed;
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
               mover.GetComponent<GhostZeldaCharacterData>() != null;
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
    protected virtual void OnValidate()
    {
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
            itemCollider.isTrigger = true;
        }
    }
#endif
}
