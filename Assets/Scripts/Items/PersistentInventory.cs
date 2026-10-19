using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A character-independent, persistent inventory (five slots before expansion).
/// Items are identified by stable string IDs so future item systems can use it
/// without coupling inventory contents to the currently controlled character.
/// </summary>
public sealed partial class PersistentInventory : MonoBehaviour
{
    [Serializable]
    public struct RuntimeSlotState
    {
        public string itemId;
        public string itemName;
        public string itemDescription;
        public string itemInstanceId;
        public int quantity;
        public bool hasVisualColor;
        public Color visualColor;
        public bool hasStoredCharge;
        public float storedCharge;
        public string itemState;
    }

    [Serializable]
    public struct RuntimeState
    {
        public RuntimeSlotState[] slots;
        public int selectedSlotIndex;
    }

    [Serializable]
    public sealed class Slot
    {
        [SerializeField] private string itemId = string.Empty;
        [SerializeField] private string itemName = string.Empty;
        [SerializeField, TextArea(2, 6)] private string itemDescription = string.Empty;
        [SerializeField] private string itemInstanceId = string.Empty;
        [SerializeField, Min(0)] private int quantity;
        [SerializeField] private bool hasVisualColor;
        [SerializeField] private Color visualColor = Color.white;
        [SerializeField] private bool hasStoredCharge;
        [SerializeField] private float storedCharge;
        [SerializeField] private string itemState = string.Empty;

        public string ItemId => itemId;
        public string ItemName => itemName;
        public string ItemDescription => itemDescription ?? string.Empty;
        public string ItemInstanceId => itemInstanceId;
        public int Quantity => quantity;
        public bool HasVisualColor => hasVisualColor;
        public Color VisualColor => visualColor;
        public bool HasStoredCharge => hasStoredCharge;
        public float StoredCharge => storedCharge;
        public string ItemState => itemState ?? string.Empty;
        public bool IsEmpty => string.IsNullOrEmpty(itemId) || quantity <= 0;

        internal void SetStoredCharge(float value) { hasStoredCharge = true; storedCharge = Mathf.Max(0f, value); }

        internal void Set(
            string newItemId,
            int newQuantity,
            string newItemInstanceId = "",
            string newItemName = "",
            string newItemDescription = "",
            bool newHasVisualColor = false,
            Color newVisualColor = default(Color),
            bool newHasStoredCharge = false,
            float newStoredCharge = 0f,
            string newItemState = "")
        {
            itemId = newQuantity > 0 && !string.IsNullOrWhiteSpace(newItemId)
                ? newItemId.Trim()
                : string.Empty;
            quantity = string.IsNullOrEmpty(itemId) ? 0 : Mathf.Max(1, newQuantity);
            itemInstanceId = string.IsNullOrEmpty(itemId) || string.IsNullOrWhiteSpace(newItemInstanceId)
                ? string.Empty
                : newItemInstanceId.Trim();
            itemName = string.IsNullOrEmpty(itemId) || string.IsNullOrWhiteSpace(newItemName)
                ? string.Empty
                : newItemName.Trim();
            itemDescription = string.IsNullOrEmpty(itemId) || string.IsNullOrWhiteSpace(newItemDescription)
                ? string.Empty
                : newItemDescription.Trim();
            hasVisualColor = !string.IsNullOrEmpty(itemId) && newHasVisualColor;
            visualColor = hasVisualColor ? newVisualColor : Color.white;
            hasStoredCharge = !string.IsNullOrEmpty(itemId) &&
                newHasStoredCharge;
            storedCharge = hasStoredCharge
                ? Mathf.Max(0f, newStoredCharge)
                : 0f;
            itemState = string.IsNullOrEmpty(itemId) ? string.Empty : (newItemState ?? string.Empty);
        }

        internal void Clear()
        {
            itemId = string.Empty;
            itemName = string.Empty;
            itemDescription = string.Empty;
            itemInstanceId = string.Empty;
            quantity = 0;
            hasVisualColor = false;
            visualColor = Color.white;
            hasStoredCharge = false;
            storedCharge = 0f;
            itemState = string.Empty;
        }
    }

    [Serializable]
    private sealed class SaveData
    {
        public Slot[] slots;
    }

    private const int DefaultSlotCount = 5;

    [SerializeField, Min(1)] private int slotCount = DefaultSlotCount;
    [SerializeField] private bool persistBetweenScenes = true;
    [SerializeField] private bool saveToPlayerPrefs = true;
    [SerializeField] private bool clearInventoryOnSceneLoad = true;
    [SerializeField] private string saveKey = "CogitansExtensa.PersistentInventory.v1";
    [SerializeField] private Slot[] slots = new Slot[DefaultSlotCount];
    // Authoring baseline is separate from runtime expansion, so a fresh session
    // never inherits the capacity of a previously played character/save.
    private int initialSlotCount;
    [SerializeField, Min(0.001f)] private float mouseWheelThreshold = 0.01f;
    [SerializeField] private KeyCode useItemKey = KeyCode.R;
    [SerializeField] private KeyCode dropItemKey = KeyCode.Q;

    public static PersistentInventory Instance { get; private set; }

    public int SlotCount => slots.Length;
    public IReadOnlyList<Slot> Slots => slots;
    public int SelectedSlotIndex { get; private set; }
    public event Action InventoryChanged;
    public event Action<int> SelectedSlotChanged;

    public RuntimeState CaptureRuntimeState()
    {
        RuntimeSlotState[] capturedSlots =
            new RuntimeSlotState[slots.Length];
        for (int i = 0; i < slots.Length; i++)
        {
            capturedSlots[i] = new RuntimeSlotState
            {
                itemId = slots[i].ItemId,
                itemName = slots[i].ItemName,
                itemDescription = slots[i].ItemDescription,
                itemInstanceId = slots[i].ItemInstanceId,
                quantity = slots[i].Quantity,
                hasVisualColor = slots[i].HasVisualColor,
                visualColor = slots[i].VisualColor,
                hasStoredCharge = slots[i].HasStoredCharge,
                storedCharge = slots[i].StoredCharge,
                itemState = slots[i].ItemState
            };
        }

        return new RuntimeState
        {
            slots = capturedSlots,
            selectedSlotIndex = SelectedSlotIndex
        };
    }

    public void ApplyRuntimeState(RuntimeState state)
    {
        // The serialized array already records capacity, including empty slots.
        // This also reads old five-slot saves without a new save schema.
        slotCount = Mathf.Max(InitialSlotCount, state.slots != null ? state.slots.Length : 0);
        EnsureSlots();
        for (int i = 0; i < slots.Length; i++)
        {
            if (state.slots != null && i < state.slots.Length)
            {
                RuntimeSlotState savedSlot = state.slots[i];
                slots[i].Set(
                    savedSlot.itemId,
                    savedSlot.quantity,
                    savedSlot.itemInstanceId,
                    savedSlot.itemName,
                    savedSlot.itemDescription,
                    savedSlot.hasVisualColor,
                    savedSlot.visualColor,
                    savedSlot.hasStoredCharge,
                    savedSlot.storedCharge,
                    savedSlot.itemState);
            }
            else
            {
                slots[i].Clear();
            }
        }

        SelectedSlotIndex = Mathf.Clamp(
            state.selectedSlotIndex,
            0,
            Mathf.Max(0, slots.Length - 1));
        NotifyChanged();
        SelectedSlotChanged?.Invoke(SelectedSlotIndex);
    }

    private readonly Dictionary<string, PickupItemBase> itemPrefabById =
        new Dictionary<string, PickupItemBase>();
    private bool preserveOnNextSceneLoad;
    private bool preserveCapacityOnNextSceneLoad;
    private bool potionUseInProgress;
    private bool itemExchangeInProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            // Destroy is deferred; the duplicate must not receive scene-load
            // notifications or update/save its empty inventory in the meantime.
            enabled = false;
            Destroy(gameObject);
            return;
        }

        Instance = this;
        initialSlotCount = Mathf.Max(1, slotCount);
        EnsureSlots();
        SelectedSlotIndex = 0;
        RebuildItemPrefabCache();
        if (persistBetweenScenes)
        {
            // Scene authoring groups (for example Level2's SceneAssets) are
            // scene-owned. Unity can only preserve root GameObjects.
            if (transform.parent != null)
            {
                transform.SetParent(null, true);
            }
            DontDestroyOnLoad(gameObject);
        }

        // Initialize a newly created inventory here, not only in sceneLoaded:
        // entering Play Mode does not report a normal Single scene load, and
        // an inventory activated later may miss that notification entirely.
        // Runtime travel keeps this instance; explicit save loading restores
        // its snapshot through ApplyRuntimeState after the scene is ready.
        if (clearInventoryOnSceneLoad)
        {
            ResetForNewSession();
        }
        else
        {
            Load();
        }
    }

    private void OnEnable()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        TickMortarRemoteCooldowns(Time.deltaTime);
        if (DocumentReader.IsInputBlocked)
        {
            return;
        }

        if (ClockworkPuppetRuntime.BlocksCharacterInput)
        {
            // Allow dismissing the projected link while controlling its puppet;
            // all ordinary inventory actions remain blocked in this mode.
            if (ClockworkPuppetRuntime.IsRemoteControlActive && Input.GetKeyDown(dropItemKey) &&
                GetSlot(SelectedSlotIndex)?.ItemId == ClockworkPuppetRemotePickupItem.DefaultItemId)
                TryDropSelectedItem();
            return;
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            Slot selectedSlot = GetSlot(SelectedSlotIndex);
            if (selectedSlot != null && !selectedSlot.IsEmpty)
            {
                ItemDescriptionWindow.GetOrCreate().OpenForSlot(selectedSlot);
            }
            return;
        }

        float mouseWheel = Input.mouseScrollDelta.y;
        if (mouseWheel > mouseWheelThreshold)
        {
            SelectSlot((SelectedSlotIndex - 1 + slots.Length) % slots.Length);
        }
        else if (mouseWheel < -mouseWheelThreshold)
        {
            SelectSlot((SelectedSlotIndex + 1) % slots.Length);
        }

        if (Input.GetKeyDown(useItemKey))
        {
            TryUseSelectedItem();
        }

        if (Input.GetKeyDown(dropItemKey))
        {
            TryDropSelectedItem();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Save();
            Instance = null;
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            Save();
        }
    }

    private void OnApplicationQuit()
    {
        Save();
    }

    public Slot GetSlot(int index)
    {
        return IsValidSlotIndex(index) ? slots[index] : null;
    }

    public int GetQuantity(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }

        int total = 0;
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].IsEmpty && slots[i].ItemId == itemId)
            {
                total += slots[i].Quantity;
            }
        }

        return total;
    }

    public bool Contains(string itemId, int quantity = 1)
    {
        return quantity <= 0 || GetQuantity(itemId) >= quantity;
    }

    public bool TryAddItem(string itemId, int quantity = 1, int maxStackSize = 99)
    {
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return false;
        }

        string normalizedId = itemId.Trim();
        int safeMaxStack = Mathf.Max(1, maxStackSize);
        // Currency is fungible even when awarded through a future shop/reward
        // API using TryAddItem's default limit, rather than a world pickup.
        var coinPrefab = normalizedId == GoldCoinPickupItem.DefaultItemId
            ? ResolveItemPrefab(normalizedId) : null;
        if (normalizedId == GoldCoinPickupItem.DefaultItemId)
            safeMaxStack = Mathf.Min(safeMaxStack, GoldCoinPickupItem.StackLimit);
        if (normalizedId == BatteryPickupItem.DefaultItemId) safeMaxStack = 1;
        // Older crystals and handguns carried unique IDs. These fungible items
        // may join a stack without changing keys or individually charged items.
        bool legacyFungibleStack = normalizedId == "stability_crystal" || normalizedId == "disposable_handgun" || AlchemyRecipeCatalog.IsStackableSupply(normalizedId);
        if (normalizedId == "stability_crystal") safeMaxStack = 10;
        if (normalizedId == "disposable_handgun") safeMaxStack = 3;
        if (AlchemyRecipeCatalog.IsStackableSupply(normalizedId)) safeMaxStack = 3;
        bool alchemySupply = AlchemyRecipeCatalog.IsStackableSupply(normalizedId);
        string supplyState = alchemySupply ? (ResolveItemPrefab(normalizedId)?.InventoryState ?? string.Empty) : string.Empty;
        int availableCapacity = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsEmpty)
            {
                availableCapacity += safeMaxStack;
            }
            else if (slots[i].ItemId == normalizedId && (legacyFungibleStack || string.IsNullOrEmpty(slots[i].ItemInstanceId)) &&
                (!alchemySupply || (SameAlchemyState(normalizedId,slots[i].ItemState,supplyState) &&
                    !slots[i].HasStoredCharge && (!slots[i].HasVisualColor || slots[i].VisualColor == Color.white))))
            {
                availableCapacity += Mathf.Max(0, safeMaxStack - slots[i].Quantity);
            }
        }

        if (availableCapacity < quantity)
        {
            return false;
        }

        int remaining = quantity;
        int lastModifiedSlot = -1;
        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (slots[i].IsEmpty ||
                slots[i].ItemId != normalizedId ||
                (!legacyFungibleStack && !string.IsNullOrEmpty(slots[i].ItemInstanceId)) ||
                (alchemySupply && (!SameAlchemyState(normalizedId,slots[i].ItemState,supplyState) ||
                    slots[i].HasStoredCharge || (slots[i].HasVisualColor && slots[i].VisualColor != Color.white))))
            {
                continue;
            }

            int added = Mathf.Min(remaining, Mathf.Max(0, safeMaxStack - slots[i].Quantity));
            slots[i].Set(
                normalizedId,
                slots[i].Quantity + added,
                string.Empty,
                slots[i].ItemName,
                slots[i].ItemDescription,
                slots[i].HasVisualColor, slots[i].VisualColor,
                slots[i].HasStoredCharge, slots[i].StoredCharge, slots[i].ItemState);
            remaining -= added;
            if (added > 0)
            {
                lastModifiedSlot = i;
            }
        }

        for (int i = 0; i < slots.Length && remaining > 0; i++)
        {
            if (!slots[i].IsEmpty)
            {
                continue;
            }

            int added = Mathf.Min(remaining, safeMaxStack);
            slots[i].Set(normalizedId, added,
                newItemName: coinPrefab != null ? coinPrefab.ItemName : string.Empty,
                newItemDescription: coinPrefab != null ? coinPrefab.ItemDescription : string.Empty,
                newItemState:supplyState);
            remaining -= added;
            lastModifiedSlot = i;
        }

        NotifyChanged();
        if (lastModifiedSlot >= 0)
        {
            SelectSlot(lastModifiedSlot);
        }
        return true;
    }

    public bool TryAddUniqueItem(
        string itemId,
        string itemInstanceId,
        int quantity = 1,
        int maxStackSize = 1,
        string itemName = "",
        string itemDescription = "",
        bool hasVisualColor = false,
        Color visualColor = default(Color),
        bool hasStoredCharge = false,
        float storedCharge = 0f,
        string itemState = "")
    {
        if (string.IsNullOrWhiteSpace(itemId) ||
            string.IsNullOrWhiteSpace(itemInstanceId) ||
            quantity <= 0)
        {
            return false;
        }

        string normalizedId = itemId.Trim();
        string normalizedInstanceId = itemInstanceId.Trim();
        // Keep the pickup base's one-shot guards/notifications, but do not
        // carry a per-coin identity into a shared currency stack.
        if (normalizedId == GoldCoinPickupItem.DefaultItemId)
            return TryAddItem(normalizedId, quantity, maxStackSize);
        // Only currency accepts a whole world pile. Unique items and the
        // existing supply-pickup path still process exactly one item.
        if (quantity != 1) return false;
        if (AlchemyRecipeCatalog.IsStackableSupply(normalizedId))
            return TryAddAlchemySupply(normalizedId, itemName, itemDescription, hasVisualColor,
                visualColor, hasStoredCharge, storedCharge, itemState);
        if (ContainsItemInstance(normalizedInstanceId))
        {
            // A deployed puppet and its energy remote share one stable identity.
            // Recover into that exact slot even with a full bag; other duplicates stay invalid.
            int existing = FindSlotByInstanceId(normalizedInstanceId);
            if (normalizedId == ClockworkPuppetPickupItem.DefaultItemId &&
                slots[existing].ItemId == ClockworkPuppetRemotePickupItem.DefaultItemId)
            {
                slots[existing].Set(normalizedId,1,normalizedInstanceId,itemName,itemDescription,
                    hasVisualColor,visualColor,hasStoredCharge,storedCharge,itemState);
                NotifyChanged();SelectSlot(existing);return true;
            }
            return false;
        }

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsEmpty)
            {
                slots[i].Set(
                    normalizedId,
                    1,
                    normalizedInstanceId,
                    itemName,
                    itemDescription,
                    hasVisualColor,
                    visualColor,
                    hasStoredCharge,
                    storedCharge,
                    itemState);
                NotifyChanged();
                SelectSlot(i);
                return true;
            }
        }

        return false;
    }

    // Three-unit supplies may join legacy single-item slots. Preserve configured
    // bottle throw parameters; differently configured bottles never share a stack.
    private bool TryAddAlchemySupply(string id, string name, string description, bool hasColor,
        Color color, bool hasCharge, float charge, string state)
    {
        int index = -1;
        for (int i = 0; i < slots.Length; i++)
        {
            var s = slots[i];
            if (!s.IsEmpty && s.ItemId == id && s.Quantity < 3 &&
                SameAlchemyState(id, s.ItemState, state) && s.HasStoredCharge == hasCharge &&
                (!hasCharge || Mathf.Approximately(s.StoredCharge,charge)) &&
                (s.HasVisualColor ? s.VisualColor : Color.white) == (hasColor ? color : Color.white))
            { index = i; break; }
        }
        if (index < 0) for (int i = 0; i < slots.Length; i++) if (slots[i].IsEmpty) { index=i; break; }
        if (index < 0) return false;
        slots[index].Set(id,slots[index].IsEmpty ? 1 : slots[index].Quantity+1,string.Empty,
            name,description,hasColor,color,hasCharge,charge,state);
        NotifyChanged(); SelectSlot(index); return true;
    }

    [Serializable] private sealed class AlchemyBottleState { public int version=1; public float speed=6f, radius=5f; }
    private static bool SameAlchemyState(string id, string a, string b)
    {
        if (id != GlassBottlePickupItem.DefaultItemId) return true;
        if ((a ?? string.Empty) == (b ?? string.Empty)) return true;
        // Empty state in pre-brewing saves means the original bottle defaults.
        try
        {
            var first=string.IsNullOrEmpty(a) ? new AlchemyBottleState() : JsonUtility.FromJson<AlchemyBottleState>(a);
            var second=string.IsNullOrEmpty(b) ? new AlchemyBottleState() : JsonUtility.FromJson<AlchemyBottleState>(b);
            return first!=null && second!=null && first.version==second.version &&
                Mathf.Approximately(first.speed,second.speed) && Mathf.Approximately(first.radius,second.radius);
        }
        catch (ArgumentException) { return false; }
    }

    /// <summary>Consume precisely the selected supply, retaining the rest of its stack's data.</summary>
    public bool TryConsumeSelectedItem(string expectedId, Action onCommitted = null)
    {
        if (itemExchangeInProgress || potionUseInProgress) return false;
        var slot = GetSlot(SelectedSlotIndex);
        if (slot == null || slot.IsEmpty || slot.ItemId != expectedId) return false;
        DecrementSlot(slot); onCommitted?.Invoke(); NotifyChanged(); return true;
    }

    /// <summary>One selected input becomes one result, with no partial mutation on a full bag.</summary>
    public bool TryExchangeSelectedItem(string expectedId, string resultId, Action onCommitted = null)
    {
        if (itemExchangeInProgress || potionUseInProgress) return false;
        var source = GetSlot(SelectedSlotIndex);
        if (source == null || source.IsEmpty || source.ItemId != expectedId) return false;
        var result = ResolveItemPrefab(resultId); if (result == null) return false;
        bool hasColor = result.ItemVisual != null;
        Color color = hasColor ? result.ItemVisual.DisplayColor : Color.white;
        string state = result.InventoryState;
        int destination = -1;
        // Returned bottles follow the same three-unit stacking/state rules as
        // drinking a potion; never merge bottles with different throw settings.
        if (AlchemyRecipeCatalog.IsStackableSupply(result.ItemId))
            for (int i = 0; i < slots.Length; i++)
            {
                var candidate = slots[i];
                if (candidate != source && !candidate.IsEmpty && candidate.ItemId == result.ItemId && candidate.Quantity < 3 &&
                    SameAlchemyState(result.ItemId, candidate.ItemState, state) &&
                    candidate.HasStoredCharge == result.HasInventoryCharge &&
                    (!candidate.HasStoredCharge || Mathf.Approximately(candidate.StoredCharge, result.InventoryCharge)) &&
                    (candidate.HasVisualColor ? candidate.VisualColor : Color.white) == color)
                { destination = i; break; }
            }
        if (destination < 0 && source.Quantity == 1) destination = SelectedSlotIndex;
        if (destination < 0) for (int i=0;i<slots.Length;i++) if (slots[i].IsEmpty) { destination=i; break; }
        if (destination < 0) return false;
        // All checks above precede writes; one notification sees only the complete exchange.
        itemExchangeInProgress = true;
        try
        {
            DecrementSlot(source);
            var output = slots[destination];
            if (output.IsEmpty)
                output.Set(result.ItemId, 1, string.Empty, result.ItemName, result.ItemDescription,
                    hasColor, color, result.HasInventoryCharge, result.InventoryCharge, state);
            else
                output.Set(output.ItemId, output.Quantity + 1, string.Empty, output.ItemName, output.ItemDescription,
                    output.HasVisualColor, output.VisualColor, output.HasStoredCharge, output.StoredCharge, output.ItemState);
            onCommitted?.Invoke(); NotifyChanged(); SelectSlot(destination); return true;
        }
        finally { itemExchangeInProgress = false; }
    }

    private static void DecrementSlot(Slot slot)
    {
        if (slot.Quantity <= 1) { slot.Clear(); return; }
        slot.Set(slot.ItemId,slot.Quantity-1,slot.ItemInstanceId,slot.ItemName,slot.ItemDescription,
            slot.HasVisualColor,slot.VisualColor,slot.HasStoredCharge,slot.StoredCharge,slot.ItemState);
    }

    public bool ContainsItemInstance(string itemInstanceId)
    {
        return FindSlotByInstanceId(itemInstanceId) >= 0;
    }

    public bool TryReplacePuppetWithRemote(string instanceId)
    {
        int index = FindSlotByInstanceId(instanceId);
        if (index < 0 || slots[index].ItemId != ClockworkPuppetPickupItem.DefaultItemId || slots[index].Quantity != 1) return false;
        slots[index].Set(ClockworkPuppetRemotePickupItem.DefaultItemId,1,instanceId,
            ClockworkPuppetRemotePickupItem.DefaultItemName,ClockworkPuppetRemotePickupItem.DefaultDescription);
        NotifyChanged();return true;
    }

    public bool RemoveClockworkPuppetRemote(string instanceId)
    {
        int index = FindSlotByInstanceId(instanceId);
        if (index < 0 || slots[index].ItemId != ClockworkPuppetRemotePickupItem.DefaultItemId) return false;
        slots[index].Clear();NotifyChanged();return true;
    }

    public bool TryRemoveItemInstance(string itemInstanceId)
    {
        if (itemExchangeInProgress) return false;
        int slotIndex = FindSlotByInstanceId(itemInstanceId);
        if (slotIndex < 0)
        {
            return false;
        }

        slots[slotIndex].Clear();
        NotifyChanged();
        return true;
    }

    public bool TryRemoveItem(string itemId, int quantity = 1)
    {
        if (itemExchangeInProgress) return false;
        if (string.IsNullOrWhiteSpace(itemId) || quantity <= 0)
        {
            return false;
        }

        string normalizedId = itemId.Trim();
        if (!Contains(normalizedId, quantity))
        {
            return false;
        }

        int remaining = quantity;
        for (int i = slots.Length - 1; i >= 0 && remaining > 0; i--)
        {
            if (slots[i].IsEmpty || slots[i].ItemId != normalizedId)
            {
                continue;
            }

            int removed = Mathf.Min(remaining, slots[i].Quantity);
            int nextQuantity = slots[i].Quantity - removed;
            if (nextQuantity <= 0)
            {
                slots[i].Clear();
            }
            else
            {
                slots[i].Set(
                    normalizedId,
                    nextQuantity,
                    string.Empty,
                    slots[i].ItemName,
                    slots[i].ItemDescription,
                    slots[i].HasVisualColor, slots[i].VisualColor,
                    slots[i].HasStoredCharge, slots[i].StoredCharge, slots[i].ItemState);
            }

            remaining -= removed;
        }

        NotifyChanged();
        return true;
    }

    public bool SetSlot(int index, string itemId, int quantity)
    {
        if (!IsValidSlotIndex(index))
        {
            return false;
        }

        slots[index].Set(itemId, quantity);
        NotifyChanged();
        return true;
    }

    public bool ClearSlot(int index)
    {
        if (!IsValidSlotIndex(index) || slots[index].IsEmpty)
        {
            return false;
        }

        slots[index].Clear();
        NotifyChanged();
        return true;
    }

    public bool SwapSlots(int firstIndex, int secondIndex)
    {
        if (!IsValidSlotIndex(firstIndex) || !IsValidSlotIndex(secondIndex))
        {
            return false;
        }

        if (firstIndex == secondIndex)
        {
            return true;
        }

        Slot first = slots[firstIndex];
        slots[firstIndex] = slots[secondIndex];
        slots[secondIndex] = first;
        NotifyChanged();
        return true;
    }

    public bool SelectSlot(int index)
    {
        if (!IsValidSlotIndex(index))
        {
            return false;
        }

        if (SelectedSlotIndex == index)
        {
            return true;
        }

        SelectedSlotIndex = index;
        SelectedSlotChanged?.Invoke(SelectedSlotIndex);
        return true;
    }

    public bool TryUseSelectedItem()
    {
        bool leavingBox = ZeldaRuntimeRegistry.GetControlledMover()?.ActiveCardboardBox != null;
        Slot selected = GetSlot(SelectedSlotIndex);
        if (leavingBox && selected != null && !selected.IsEmpty &&
            ResolveItemPrefab(selected.ItemId)?.UsePlacesInWorld == true) leavingBox = false;
        bool used = TryUseSelectedItemCore();
        if (used && !leavingBox) LevelRunStatistics.RecordItemUse();
        return used;
    }

    private bool TryUseSelectedItemCore()
    {
        if (potionUseInProgress || itemExchangeInProgress) return false;
        ZeldaFourWayMover controlledMover =
            ZeldaRuntimeRegistry.GetControlledMover();
        Slot placementSlot = GetSlot(SelectedSlotIndex);
        // Passive components (e.g. batteries) use E at their receiving mechanism,
        // never R, including the nearby cardboard-box storage shortcut.
        if (placementSlot != null && !placementSlot.IsEmpty &&
            ResolveItemPrefab(placementSlot.ItemId)?.CanUseFromInventory == false)
            return false;
        // Character-items have identical R/Q semantics, even beside/inside a box.
        if (placementSlot != null && !placementSlot.IsEmpty &&
            ResolveItemPrefab(placementSlot.ItemId)?.UsePlacesInWorld == true)
        {
            if (!CharacterSkillRequirements.Require(controlledMover != null ? controlledMover.CharacterData : null,
                ResolveItemPrefab(placementSlot.ItemId).RequiredUseSkill)) return false;
            bool placed = TryDropSelectedItem();
            if (placed && controlledMover != null) CharacterPotionBuffs.NotifyAction(controlledMover.GetComponent<ZeldaCharacterData>());
            return placed;
        }
        if (controlledMover != null &&
            controlledMover.ActiveCardboardBox != null)
        {
            controlledMover.ActiveCardboardBox.RequestExit();
            return true;
        }

        Slot slot = GetSlot(SelectedSlotIndex);
        if (slot == null || slot.IsEmpty)
        {
            return false;
        }

        PickupItemBase itemPrefab = ResolveItemPrefab(slot.ItemId);
        if (itemPrefab == null)
        {
            Debug.LogWarning($"No pickup prefab in Resources/PickupItems has item ID '{slot.ItemId}'.", this);
            return false;
        }

        Vector3 position = GetControlledCharacterPosition();
        ZeldaCharacterData user = controlledMover != null
            ? controlledMover.GetComponent<ZeldaCharacterData>()
            : null;

        if (itemPrefab.UsesSimpleUseGesture && controlledMover != null && controlledMover.IsAttacking)
            return false;

        var magicRemote = itemPrefab as ClockworkPuppetRemotePickupItem;
        // Before box insertion, arming, spawning, animation or inventory removal.
        if (!CharacterSkillRequirements.Require(user, itemPrefab.RequiredUseSkill)) return false;
        if (magicRemote != null) return magicRemote.TryReconnect(user,slot.ItemInstanceId);
        if (itemPrefab is MortarRemotePickupItem mortarRemote) return TryUseMortarRemote(mortarRemote, slot, user);
        if (itemPrefab is MemoPickupItem memo) return TryUseMemo(memo, slot, user);

        // Old/debug slots may not yet have an individual identity. Assign it before
        // deployment so the runtime, container and replacement remote all agree.
        if (itemPrefab is ClockworkPuppetPickupItem)
        {
            if (slot.Quantity != 1 || user == null || user.IsDead || user.IsGhostLike) return false;
            if (string.IsNullOrEmpty(slot.ItemInstanceId))
                slot.Set(slot.ItemId,1,Guid.NewGuid().ToString("N"),slot.ItemName,slot.ItemDescription,
                    slot.HasVisualColor,slot.VisualColor,slot.HasStoredCharge,slot.StoredCharge,slot.ItemState);
        }

        CardboardBoxPickupItem nearbyBox =
            CardboardBoxPickupItem.FindNearestAvailableContainer(position);
        if (nearbyBox != null && user != null && !user.IsGhostLike &&
            itemPrefab.CanBeStoredInCardboardBox(user) &&
            nearbyBox.TryStoreItem(slot, itemPrefab, user))
        {
            var puppetDriver = nearbyBox.GetComponent<ClockworkPuppetBoxDriver>();
            bool replacedByRemote = itemPrefab is ClockworkPuppetPickupItem &&
                puppetDriver != null && puppetDriver.RemainingMagic > 0f && nearbyBox.HasStoredItem &&
                TryReplacePuppetWithRemote(slot.ItemInstanceId);
            bool removedFromInventory = replacedByRemote || (string.IsNullOrEmpty(slot.ItemInstanceId)
                ? TryRemoveItem(slot.ItemId, 1)
                : TryRemoveItemInstance(slot.ItemInstanceId));
            if (!removedFromInventory)
            {
                nearbyBox.CancelLastStoredItem();
                return false;
            }

            nearbyBox.ArmStoredBomb(user);
            itemPrefab.PlayUseGesture(user);
            CharacterPotionBuffs.NotifyAction(user);
            return true;
        }

        PickupItemBase runtimeItem = Instantiate(itemPrefab.gameObject, position, Quaternion.identity)
            .GetComponent<PickupItemBase>();
        if (!runtimeItem.gameObject.activeSelf)
        {
            runtimeItem.gameObject.SetActive(true);
        }
        runtimeItem.SetUniqueInstanceId(slot.ItemInstanceId);
        runtimeItem.SetItemName(slot.ItemName);
        if (!string.IsNullOrWhiteSpace(slot.ItemDescription))
        {
            runtimeItem.SetItemDescription(slot.ItemDescription);
        }
        if (slot.HasVisualColor && runtimeItem.ItemVisual != null)
        {
            runtimeItem.ItemVisual.SetDisplayColor(slot.VisualColor);
        }
        runtimeItem.ApplyInventoryCharge(
            slot.HasStoredCharge,
            slot.StoredCharge);
        try { runtimeItem.ApplyInventoryState(slot.ItemState); }
        catch (Exception exception)
        {
            Destroy(runtimeItem.gameObject);
            Debug.LogWarning("Could not restore item state: " + exception.Message, this);
            return false;
        }
        if (runtimeItem is PotionPickupItem potion)
            return TryUsePotionAndReturnBottle(potion, user, slot);

        bool consumed = runtimeItem.ExecuteUse(user);
        if (!consumed)
        {
            Destroy(runtimeItem.gameObject);
            return false;
        }

        var deployedPuppetItem = runtimeItem as ClockworkPuppetPickupItem;
        if (deployedPuppetItem != null && deployedPuppetItem.LastDeployedPuppet != null &&
            !deployedPuppetItem.LastDeployedPuppet.IsBroken)
            return TryReplacePuppetWithRemote(slot.ItemInstanceId);

        bool removedAfterUse = string.IsNullOrEmpty(slot.ItemInstanceId)
            ? (AlchemyRecipeCatalog.IsStackableSupply(slot.ItemId) ? TryConsumeSelectedItem(slot.ItemId) : TryRemoveItem(slot.ItemId, 1))
            : TryRemoveItemInstance(slot.ItemInstanceId);
        // Only exposed, armed R deployment is a witnessed hostile act.
        // Container insertion returns above; Q drops never notify witnesses.
        if (removedAfterUse) NotifyBombPlacement(itemPrefab, user);
        return removedAfterUse;
    }

    private bool TryUsePotionAndReturnBottle(PotionPickupItem potion, ZeldaCharacterData user, Slot source)
    {
        var bottle = ResolveItemPrefab(GlassBottlePickupItem.DefaultItemId);
        int sourceIndex = Array.IndexOf(slots, source);
        if (bottle == null || sourceIndex < 0 || user == null || user.IsDead || user.IsGhostLike)
        {
            Destroy(potion.gameObject);
            return false;
        }

        bool hasColor = bottle.ItemVisual != null;
        Color color = hasColor ? bottle.ItemVisual.DisplayColor : Color.white;
        string state = bottle.InventoryState;
        int destination = -1;
        for (int i = 0; i < slots.Length; i++)
        {
            var candidate = slots[i];
            if (!candidate.IsEmpty && candidate.ItemId == bottle.ItemId && candidate.Quantity < 3 &&
                SameAlchemyState(bottle.ItemId, candidate.ItemState, state) &&
                candidate.HasStoredCharge == bottle.HasInventoryCharge &&
                (!candidate.HasStoredCharge || Mathf.Approximately(candidate.StoredCharge, bottle.InventoryCharge)) &&
                (candidate.HasVisualColor ? candidate.VisualColor : Color.white) == color)
            { destination = i; break; }
        }
        // Normal potions never stack, so the consumed potion always provides room,
        // including a completely full inventory. Keep malformed/debug stacks safe too.
        if (destination < 0 && source.Quantity == 1) destination = sourceIndex;
        if (destination < 0)
            for (int i = 0; i < slots.Length; i++) if (slots[i].IsEmpty) { destination = i; break; }
        if (destination < 0)
        {
            Destroy(potion.gameObject);
            return false;
        }

        var sourceBefore = new Slot(); CopySlot(source, sourceBefore);
        var destinationBefore = new Slot(); CopySlot(slots[destination], destinationBefore);
        bool consumed = false;
        potionUseInProgress = true;
        try
        {
            // Stage the entire exchange before HP/death callbacks can save the bag.
            // Do not notify observers until the effect also succeeds.
            DecrementSlot(source);
            var result = slots[destination];
            if (result.IsEmpty)
                result.Set(bottle.ItemId, 1, string.Empty, bottle.ItemName, bottle.ItemDescription,
                    hasColor, color, bottle.HasInventoryCharge, bottle.InventoryCharge, state);
            else
                result.Set(result.ItemId, result.Quantity + 1, string.Empty, result.ItemName, result.ItemDescription,
                    result.HasVisualColor, result.VisualColor, result.HasStoredCharge, result.StoredCharge, result.ItemState);

            consumed = potion.ExecuteUse(user);
            if (!consumed) return false;
            NotifyChanged();
            SelectSlot(destination);
            return true;
        }
        finally
        {
            if (!consumed)
            {
                CopySlot(sourceBefore, source);
                if (destination != sourceIndex) CopySlot(destinationBefore, slots[destination]);
            }
            Destroy(potion.gameObject);
            potionUseInProgress = false;
        }
    }

    private static void CopySlot(Slot source, Slot destination)
    {
        destination.Set(source.ItemId, source.Quantity, source.ItemInstanceId, source.ItemName, source.ItemDescription,
            source.HasVisualColor, source.VisualColor, source.HasStoredCharge, source.StoredCharge, source.ItemState);
    }

    public bool TryDropSelectedItem()
    {
        if (potionUseInProgress || itemExchangeInProgress) return false;
        Slot slot = GetSlot(SelectedSlotIndex);
        if (slot == null || slot.IsEmpty)
        {
            return false;
        }

        // This is a projected control link, not a physical pickup or box payload.
        if (slot.ItemId == ClockworkPuppetRemotePickupItem.DefaultItemId)
        {
            ClockworkPuppetRemotePickupItem.Disconnect(slot.ItemInstanceId);
            return RemoveClockworkPuppetRemote(slot.ItemInstanceId);
        }

        PickupItemBase itemPrefab = ResolveItemPrefab(slot.ItemId);
        if (itemPrefab == null)
        {
            Debug.LogWarning($"No pickup prefab in Resources/PickupItems has item ID '{slot.ItemId}'.", this);
            return false;
        }

        // Q stores bombs safely; other items retain their existing drop behavior.
        if (itemPrefab is BombPickupItem)
        {
            ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
            ZeldaCharacterData user = mover != null ? mover.GetComponent<ZeldaCharacterData>() : null;
            CardboardBoxPickupItem box = CardboardBoxPickupItem.FindNearestAvailableContainer(
                GetControlledCharacterPosition());
            if (box != null && user != null && !user.IsDead && !user.IsGhostLike)
            {
                if (mover.IsAttacking) return false;
                if (box.TryStoreItem(slot, itemPrefab, user))
                {
                    bool storedRemoved = string.IsNullOrEmpty(slot.ItemInstanceId)
                        ? TryRemoveItem(slot.ItemId, 1)
                        : TryRemoveItemInstance(slot.ItemInstanceId);
                    if (!storedRemoved)
                    {
                        box.CancelLastStoredItem();
                        return false;
                    }
                    itemPrefab.PlayUseGesture(user);
                    return true;
                }
            }
        }

        string droppedItemId = slot.ItemId;
        string droppedItemName = slot.ItemName;
        string droppedItemDescription = slot.ItemDescription;
        string droppedItemInstanceId = slot.ItemInstanceId;
        string droppedItemState = slot.ItemState;
        bool droppedItemHasVisualColor = slot.HasVisualColor;
        Color droppedItemVisualColor = slot.VisualColor;
        bool droppedItemHasStoredCharge = slot.HasStoredCharge;
        float droppedItemStoredCharge = slot.StoredCharge;
        ZeldaCharacterData placingUser = null;
        if (itemPrefab.UsePlacesInWorld)
        {
            ZeldaFourWayMover mover = ZeldaRuntimeRegistry.GetControlledMover();
            ZeldaCharacterData user = mover != null ? mover.GetComponent<ZeldaCharacterData>() : null;
            if (user == null || user.IsDead || mover.IsAttacking) return false;
            placingUser = user;
        }
        Vector3 position = GetControlledCharacterPosition();
        PickupItemBase droppedItem = Instantiate(itemPrefab.gameObject, position, Quaternion.identity)
            .GetComponent<PickupItemBase>();
        if (!droppedItem.gameObject.activeSelf)
        {
            droppedItem.gameObject.SetActive(true);
        }
        droppedItem.SetUniqueInstanceId(droppedItemInstanceId);
        droppedItem.SetItemName(droppedItemName);
        if (!string.IsNullOrWhiteSpace(droppedItemDescription))
        {
            droppedItem.SetItemDescription(droppedItemDescription);
        }
        if (droppedItemHasVisualColor && droppedItem.ItemVisual != null)
        {
            droppedItem.ItemVisual.SetDisplayColor(droppedItemVisualColor);
        }
        droppedItem.ApplyInventoryCharge(
            droppedItemHasStoredCharge,
            droppedItemStoredCharge);
        try { droppedItem.ApplyInventoryState(droppedItemState); }
        catch (Exception exception)
        {
            Destroy(droppedItem.gameObject);
            Debug.LogWarning("Could not restore dropped item state: " + exception.Message, this);
            return false;
        }
        if (itemPrefab.UsePlacesInWorld)
        {
            // Use the restored character item's actual size, not the prefab's default scale.
            var collider = droppedItem.GetComponent<Collider2D>();
            Vector2 size = collider != null ? (Vector2)collider.bounds.size : Vector2.one * .5f;
            Vector2 offset = collider != null
                ? (Vector2)(collider.bounds.center - droppedItem.transform.position) : Vector2.zero;
            droppedItem.transform.position = PickupItemBase.ResolvePlacementPosition(
                placingUser, position, size, offset, droppedItem.gameObject);
        }
        if (droppedItem is GoldCoinPickupItem coinPile)
            coinPile.ConfigureDroppedStack(slot.Quantity);
        droppedItem.MarkAsDropped();

        bool removed;
        if (droppedItem is GoldCoinPickupItem)
        {
            // Do not remove by ID: there may be other coin stacks in the bag.
            // Prepare the pile first, then commit this exact slot before events.
            slot.Clear();
            removed = true;
            NotifyChanged();
        }
        else
        {
            removed = string.IsNullOrEmpty(droppedItemInstanceId)
                ? (AlchemyRecipeCatalog.IsStackableSupply(droppedItemId) ? TryConsumeSelectedItem(droppedItemId) : TryRemoveItem(droppedItemId, 1))
                : TryRemoveItemInstance(droppedItemInstanceId);
        }
        if (!removed)
        {
            Destroy(droppedItem.gameObject);
            return false;
        }

        if (itemPrefab.UsePlacesInWorld) itemPrefab.PlayUseGesture(placingUser);
        return true;
    }

    private static void NotifyBombPlacement(PickupItemBase itemPrefab, ZeldaCharacterData user)
    {
        // A decoy has a beacon rather than a burning fuse; witnesses attack the
        // visible device instead of treating its placement as a player assault.
        if (itemPrefab is BombPickupItem bomb && !bomb.IsDecoy)
            ZeldaCharacterAiBase.NotifyPlayerBombPlacement(user);
    }

    public PickupItemBase ResolveItemPrefab(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return null;
        }

        PickupItemBase itemPrefab;
        if (itemPrefabById.TryGetValue(itemId, out itemPrefab) && itemPrefab != null)
        {
            return itemPrefab;
        }

        RebuildItemPrefabCache();
        itemPrefabById.TryGetValue(itemId, out itemPrefab);
        return itemPrefab;
    }

    public void ClearAll()
    {
        for (int i = 0; i < slots.Length; i++)
        {
            slots[i].Clear();
        }

        NotifyChanged();
    }

    private int InitialSlotCount => initialSlotCount > 0 ? initialSlotCount : DefaultSlotCount;

    private void ResetForNewSession()
    {
        slotCount = InitialSlotCount;
        EnsureSlots();
        SelectedSlotIndex = 0;
        ClearAll();
        SelectedSlotChanged?.Invoke(SelectedSlotIndex);
    }

    public void ResetForProfile()
    {
        preserveOnNextSceneLoad = false;
        preserveCapacityOnNextSceneLoad = false;
        ResetForNewSession();
    }

    public void Save()
    {
        // Profile snapshots own gameplay persistence. The legacy global cache
        // must not leak items between campaigns or be mutated during restoration.
        if (GameSaveSystem.Instance != null) return;
        if (!saveToPlayerPrefs || string.IsNullOrWhiteSpace(saveKey))
        {
            return;
        }

        SaveData data = new SaveData { slots = slots };
        PlayerPrefs.SetString(saveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Keeps inventory contents and selection during the next scene load.
    /// Ordinary scene starts retain the project's existing clear-on-load rule.
    /// </summary>
    public void PreserveForNextSceneLoad()
    {
        preserveOnNextSceneLoad = true;
        Save();
    }

    // A next-level entrance intentionally clears carried items, unlike travel
    // within a level. Permanent consciousness upgrades still survive it.
    public void PreserveCapacityForNextSceneLoad(bool preserve = true)
    {
        preserveCapacityOnNextSceneLoad = preserve;
    }

    public void Load()
    {
        if (GameSaveSystem.Instance != null) return;
        if (!saveToPlayerPrefs || string.IsNullOrWhiteSpace(saveKey) || !PlayerPrefs.HasKey(saveKey))
        {
            return;
        }

        string json = PlayerPrefs.GetString(saveKey, string.Empty);
        if (string.IsNullOrEmpty(json))
        {
            return;
        }

        try
        {
            SaveData data = JsonUtility.FromJson<SaveData>(json);
            if (data == null || data.slots == null)
            {
                return;
            }

            slotCount = Mathf.Max(InitialSlotCount, data.slots.Length);
            EnsureSlots();
            SelectedSlotIndex = Mathf.Clamp(SelectedSlotIndex, 0, slots.Length - 1);
            for (int i = 0; i < slots.Length; i++)
            {
                if (i < data.slots.Length && data.slots[i] != null)
                {
                    slots[i].Set(
                        data.slots[i].ItemId,
                        data.slots[i].Quantity,
                        data.slots[i].ItemInstanceId,
                        data.slots[i].ItemName,
                        data.slots[i].ItemDescription,
                        data.slots[i].HasVisualColor,
                        data.slots[i].VisualColor,
                        data.slots[i].HasStoredCharge,
                        data.slots[i].StoredCharge,
                        data.slots[i].ItemState);
                }
                else
                {
                    slots[i].Clear();
                }
            }

            InventoryChanged?.Invoke();
        }
        catch (Exception exception)
        {
            Debug.LogWarning($"Could not load inventory save data: {exception.Message}", this);
        }
    }

    private void NotifyChanged()
    {
        QuestJournalManager.Instance?.RecordQuestInventoryEvidence();
        InventoryChanged?.Invoke();
        Save();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Only the surviving inventory handles a gameplay-scene replacement.
        // Auxiliary additive loads must not consume a pending travel preserve.
        if (Instance != this || mode != LoadSceneMode.Single)
        {
            return;
        }

        // Asset references are normally stable, but rebuilding here also
        // repairs caches after editor play-mode reloads and scene transitions
        // involving persistent inventory objects.
        RebuildItemPrefabCache();

        if (preserveOnNextSceneLoad)
        {
            preserveOnNextSceneLoad = false;
            preserveCapacityOnNextSceneLoad = false;
            InventoryChanged?.Invoke();
            SelectedSlotChanged?.Invoke(SelectedSlotIndex);
            return;
        }

        if (clearInventoryOnSceneLoad)
        {
            if (preserveCapacityOnNextSceneLoad)
            {
                SelectedSlotIndex = 0;
                ClearAll();
                SelectedSlotChanged?.Invoke(SelectedSlotIndex);
            }
            else ResetForNewSession();
        }
        else { SelectedSlotIndex = 0; SelectedSlotChanged?.Invoke(SelectedSlotIndex); }
        preserveCapacityOnNextSceneLoad = false;
    }

    private void RebuildItemPrefabCache()
    {
        itemPrefabById.Clear();
        GameObject[] itemPrefabs = Resources.LoadAll<GameObject>("PickupItems");
        for (int i = 0; i < itemPrefabs.Length; i++)
        {
            PickupItemBase item = itemPrefabs[i].GetComponent<PickupItemBase>();
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId))
            {
                continue;
            }

            if (itemPrefabById.ContainsKey(item.ItemId))
            {
                Debug.LogWarning($"Duplicate pickup item ID '{item.ItemId}' in Resources/PickupItems.", itemPrefabs[i]);
                continue;
            }

            itemPrefabById.Add(item.ItemId, item);
        }
    }

    private Vector3 GetControlledCharacterPosition()
    {
        ZeldaFourWayMover controlledMover =
            ZeldaRuntimeRegistry.GetControlledMover();
        return controlledMover != null
            ? controlledMover.transform.position
            : Vector3.zero;
    }

    private bool IsValidSlotIndex(int index)
    {
        return index >= 0 && index < slots.Length;
    }

    private int FindSlotByInstanceId(string itemInstanceId)
    {
        if (string.IsNullOrWhiteSpace(itemInstanceId))
        {
            return -1;
        }

        string normalizedInstanceId = itemInstanceId.Trim();
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i].IsEmpty && slots[i].ItemInstanceId == normalizedInstanceId)
            {
                return i;
            }
        }

        return -1;
    }

    private void EnsureSlots()
    {
        slotCount = Mathf.Max(1, slotCount);
        if (slots != null && slots.Length == slotCount)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] == null)
                {
                    slots[i] = new Slot();
                }
            }

            return;
        }

        Slot[] resizedSlots = new Slot[slotCount];
        for (int i = 0; i < resizedSlots.Length; i++)
        {
            resizedSlots[i] = slots != null && i < slots.Length && slots[i] != null
                ? slots[i]
                : new Slot();
        }

        slots = resizedSlots;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        slotCount = Mathf.Max(1, slotCount);
        mouseWheelThreshold = Mathf.Max(0.001f, mouseWheelThreshold);
        EnsureSlots();
    }
#endif
}
