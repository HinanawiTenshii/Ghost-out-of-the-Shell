using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// A character-independent, persistent five-slot inventory.
/// Items are identified by stable string IDs so future item systems can use it
/// without coupling inventory contents to the currently controlled character.
/// </summary>
public sealed class PersistentInventory : MonoBehaviour
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

        public string ItemId => itemId;
        public string ItemName => itemName;
        public string ItemDescription => itemDescription ?? string.Empty;
        public string ItemInstanceId => itemInstanceId;
        public int Quantity => quantity;
        public bool HasVisualColor => hasVisualColor;
        public Color VisualColor => visualColor;
        public bool HasStoredCharge => hasStoredCharge;
        public float StoredCharge => storedCharge;
        public bool IsEmpty => string.IsNullOrEmpty(itemId) || quantity <= 0;

        internal void Set(
            string newItemId,
            int newQuantity,
            string newItemInstanceId = "",
            string newItemName = "",
            string newItemDescription = "",
            bool newHasVisualColor = false,
            Color newVisualColor = default(Color),
            bool newHasStoredCharge = false,
            float newStoredCharge = 0f)
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
                storedCharge = slots[i].StoredCharge
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
                    savedSlot.storedCharge);
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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        EnsureSlots();
        SelectedSlotIndex = 0;
        RebuildItemPrefabCache();
        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        Load();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (DocumentReader.IsInputBlocked)
        {
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
        int availableCapacity = 0;

        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i].IsEmpty)
            {
                availableCapacity += safeMaxStack;
            }
            else if (slots[i].ItemId == normalizedId && string.IsNullOrEmpty(slots[i].ItemInstanceId))
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
                !string.IsNullOrEmpty(slots[i].ItemInstanceId))
            {
                continue;
            }

            int added = Mathf.Min(remaining, Mathf.Max(0, safeMaxStack - slots[i].Quantity));
            slots[i].Set(
                normalizedId,
                slots[i].Quantity + added,
                string.Empty,
                slots[i].ItemName,
                slots[i].ItemDescription);
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
            slots[i].Set(normalizedId, added);
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
        float storedCharge = 0f)
    {
        if (string.IsNullOrWhiteSpace(itemId) ||
            string.IsNullOrWhiteSpace(itemInstanceId) ||
            quantity != 1)
        {
            return false;
        }

        string normalizedId = itemId.Trim();
        string normalizedInstanceId = itemInstanceId.Trim();
        if (ContainsItemInstance(normalizedInstanceId))
        {
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
                    storedCharge);
                NotifyChanged();
                SelectSlot(i);
                return true;
            }
        }

        return false;
    }

    public bool ContainsItemInstance(string itemInstanceId)
    {
        return FindSlotByInstanceId(itemInstanceId) >= 0;
    }

    public bool TryRemoveItemInstance(string itemInstanceId)
    {
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
                    slots[i].ItemDescription);
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
        ZeldaFourWayMover controlledMover =
            ZeldaRuntimeRegistry.GetControlledMover();
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

        CardboardBoxPickupItem nearbyBox =
            CardboardBoxPickupItem.FindNearestAvailableContainer(position);
        if (nearbyBox != null &&
            itemPrefab.CanBeStoredInCardboardBox(user) &&
            nearbyBox.TryStoreItem(slot, itemPrefab, user))
        {
            bool removedFromInventory = string.IsNullOrEmpty(slot.ItemInstanceId)
                ? TryRemoveItem(slot.ItemId, 1)
                : TryRemoveItemInstance(slot.ItemInstanceId);
            if (!removedFromInventory)
            {
                nearbyBox.CancelLastStoredItem();
                return false;
            }

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
        bool consumed = runtimeItem.ExecuteUse(user);
        if (!consumed)
        {
            Destroy(runtimeItem.gameObject);
            return false;
        }

        return string.IsNullOrEmpty(slot.ItemInstanceId)
            ? TryRemoveItem(slot.ItemId, 1)
            : TryRemoveItemInstance(slot.ItemInstanceId);
    }

    public bool TryDropSelectedItem()
    {
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

        string droppedItemId = slot.ItemId;
        string droppedItemName = slot.ItemName;
        string droppedItemDescription = slot.ItemDescription;
        string droppedItemInstanceId = slot.ItemInstanceId;
        bool droppedItemHasVisualColor = slot.HasVisualColor;
        Color droppedItemVisualColor = slot.VisualColor;
        bool droppedItemHasStoredCharge = slot.HasStoredCharge;
        float droppedItemStoredCharge = slot.StoredCharge;
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
        droppedItem.MarkAsDropped();

        bool removed = string.IsNullOrEmpty(droppedItemInstanceId)
            ? TryRemoveItem(droppedItemId, 1)
            : TryRemoveItemInstance(droppedItemInstanceId);
        if (!removed)
        {
            Destroy(droppedItem.gameObject);
            return false;
        }

        return true;
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

    public void Save()
    {
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

    public void Load()
    {
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
                        data.slots[i].StoredCharge);
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
        InventoryChanged?.Invoke();
        Save();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Asset references are normally stable, but rebuilding here also
        // repairs caches after editor play-mode reloads and scene transitions
        // involving persistent inventory objects.
        RebuildItemPrefabCache();

        if (preserveOnNextSceneLoad)
        {
            preserveOnNextSceneLoad = false;
            InventoryChanged?.Invoke();
            SelectedSlotChanged?.Invoke(SelectedSlotIndex);
            return;
        }

        SelectedSlotIndex = 0;
        SelectedSlotChanged?.Invoke(SelectedSlotIndex);
        if (clearInventoryOnSceneLoad)
        {
            ClearAll();
        }
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
