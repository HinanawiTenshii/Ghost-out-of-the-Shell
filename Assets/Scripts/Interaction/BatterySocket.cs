using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>Transfers a single inventory battery and gates one lever/controller without changing its state.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(CameraVisionStreamingExempt))]
public sealed class BatterySocket : MonoBehaviour
{
    [Serializable]
    public struct RuntimeState
    {
        public bool installed;
        public PersistentInventory.RuntimeSlotState battery;
    }

    private static readonly HashSet<BatterySocket> Sockets = new HashSet<BatterySocket>();
    [SerializeField, Tooltip("拖入 WaterwayGateController 或 LeverData 组件。未绑定不影响其他机关。")]
    private MonoBehaviour linkedMechanism;
    [FormerlySerializedAs("batteryInstalled")]
    [SerializeField, InspectorName("默认带有电池"), Tooltip("勾选后首次进入场景自带一块电池；读档时优先使用存档中的安装状态。")]
    private bool startsWithBattery;
    [SerializeField, Min(.1f)] private float interactionDistance = 1.25f;
    [SerializeField] private Font promptFont;
    private PersistentInventory.RuntimeSlotState storedBattery;
    private bool batteryInstalled;
    private bool initialized;
    private bool transferring;
    private GameObject promptObject;
    private TextMesh promptText;
    private Material promptMaterial;

    public bool HasBattery => initialized ? batteryInstalled : startsWithBattery;
    public MonoBehaviour LinkedMechanism => linkedMechanism;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void InitializeRegistry()
    {
        Sockets.Clear();
        SceneManager.sceneLoaded -= RegisterSceneSockets;
        SceneManager.sceneLoaded += RegisterSceneSockets;
    }

    private static void RegisterSceneSockets(Scene scene, LoadSceneMode mode)
    {
        // Include initially inactive sockets: hiding a slot must not bypass its power requirement.
        foreach (var root in scene.GetRootGameObjects())
            foreach (var socket in root.GetComponentsInChildren<BatterySocket>(true)) Sockets.Add(socket);
        Sockets.RemoveWhere(socket => socket == null);
    }

    private void Awake() { Sockets.Add(this); EnsureInitialized(); }
    private void OnEnable() => Sockets.Add(this);

    // Do not deregister on disable: camera suspension must never bypass power requirements.
    public static bool IsPowered(Component mechanism)
    {
        if (mechanism == null) return true;
        foreach (var socket in Sockets)
            if (socket != null && socket.linkedMechanism == mechanism && !socket.HasBattery)
                return false;
        return true;
    }

    public void BindMechanism(MonoBehaviour mechanism)
    {
        if (mechanism != null && !(mechanism is LeverData) && !(mechanism is WaterwayGateController))
            throw new ArgumentException("Battery sockets support LeverData or WaterwayGateController.");
        linkedMechanism = mechanism;
    }

    private void EnsureInitialized()
    {
        if (initialized) return;
        initialized = true;
        batteryInstalled = startsWithBattery;
        EnsureBatteryIdentity();
    }

    private void EnsureBatteryIdentity()
    {
        if (!batteryInstalled || !string.IsNullOrEmpty(storedBattery.itemInstanceId)) return;
        storedBattery = new PersistentInventory.RuntimeSlotState {
            itemId = BatteryPickupItem.DefaultItemId,
            itemInstanceId = Guid.NewGuid().ToString("N"), quantity = 1,
            itemName = BatteryPickupItem.DefaultItemName,
            itemDescription = BatteryPickupItem.DefaultDescription
        };
    }

    public RuntimeState CaptureRuntimeState()
    {
        EnsureInitialized();
        EnsureBatteryIdentity();
        return new RuntimeState { installed = batteryInstalled, battery = storedBattery };
    }

    public void ApplyRuntimeState(RuntimeState state)
    {
        initialized = true; // Restored state wins even if this inactive object's Awake has not run yet.
        batteryInstalled = state.installed;
        storedBattery = state.battery;
        EnsureBatteryIdentity();
    }

    public bool TryInstall(PersistentInventory inventory)
    {
        EnsureInitialized();
        if (transferring || batteryInstalled || inventory == null) return false;
        var slot = inventory.GetSlot(inventory.SelectedSlotIndex);
        if (slot == null || slot.IsEmpty || slot.ItemId != BatteryPickupItem.DefaultItemId)
        {
            slot = null;
            foreach (var candidate in inventory.Slots)
                if (!candidate.IsEmpty && candidate.ItemId == BatteryPickupItem.DefaultItemId) { slot = candidate; break; }
        }
        if (slot == null) return false;
        var battery = new PersistentInventory.RuntimeSlotState {
            itemId = slot.ItemId, itemInstanceId = slot.ItemInstanceId, quantity = 1,
            itemName = slot.ItemName, itemDescription = slot.ItemDescription,
            hasVisualColor = slot.HasVisualColor, visualColor = slot.VisualColor,
            hasStoredCharge = slot.HasStoredCharge, storedCharge = slot.StoredCharge, itemState = slot.ItemState
        };
        transferring = true;
        try
        {
            bool removed = string.IsNullOrEmpty(battery.itemInstanceId)
                ? inventory.TryRemoveItem(battery.itemId, 1)
                : inventory.TryRemoveItemInstance(battery.itemInstanceId);
            if (!removed) return false;
            storedBattery = battery;
            batteryInstalled = true;
            EnsureBatteryIdentity();
            return true;
        }
        finally { transferring = false; }
    }

    public bool TryUninstall(PersistentInventory inventory)
    {
        EnsureInitialized();
        if (transferring || !batteryInstalled || inventory == null) return false;
        EnsureBatteryIdentity();
        transferring = true;
        try
        {
            // Only cut power after inventory accepts the item. Full inventories leave everything unchanged.
            if (!inventory.TryAddUniqueItem(BatteryPickupItem.DefaultItemId, storedBattery.itemInstanceId,
                1, 1, storedBattery.itemName, storedBattery.itemDescription,
                storedBattery.hasVisualColor, storedBattery.visualColor,
                storedBattery.hasStoredCharge, storedBattery.storedCharge, storedBattery.itemState)) return false;
            batteryInstalled = false;
            storedBattery = default;
            return true;
        }
        finally { transferring = false; }
    }

    private ZeldaFourWayMover NearbyMover()
    {
        if (!isActiveAndEnabled || DocumentReader.IsInputBlocked || Time.timeScale <= 0f
            || ClockworkPuppetRuntime.BlocksCharacterInput) return null;
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !mover.isActiveAndEnabled
            || ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != gameObject.scene
            || Vector2.Distance(transform.position, mover.transform.position) > interactionDistance) return null;
        var data = mover.GetComponent<ZeldaCharacterData>();
        return data != null && !data.IsDead && !data.IsGhostLike ? mover : null;
    }

    private void Update()
    {
        var mover = NearbyMover();
        if (mover == null) { ShowPrompt(false); return; }
        ZeldaInteractionArbiter.OfferInteraction(this, mover, KeyCode.E, transform.position, ShowPrompt);
        if (Input.GetKeyDown(KeyCode.E))
            ZeldaInteractionArbiter.Submit(this, mover, KeyCode.E, transform.position, Interact);
    }

    private void Interact()
    {
        if (NearbyMover() == null) return;
        bool removing = HasBattery;
        bool success = removing ? TryUninstall(PersistentInventory.Instance) : TryInstall(PersistentInventory.Instance);
        ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup(success
            ? (removing ? "已卸下魔力电池" : "已安装魔力电池")
            : (removing ? "物品栏已满，无法卸下魔力电池" : "背包中没有魔力电池"));
    }

    private void ShowPrompt(bool visible)
    {
        var mover = visible ? NearbyMover() : null;
        if (mover == null) { if (promptObject != null) promptObject.SetActive(false); return; }
        if (promptFont == null) promptFont = ZeldaHealthHeartsUI.Instance?.PermissionLabelFont;
        if (promptFont == null) return;
        if (promptObject == null)
        {
            promptObject = new GameObject("Battery Socket Prompt", typeof(TextMesh)) { hideFlags = HideFlags.HideAndDontSave };
            promptObject.transform.SetParent(transform, false);
            promptText = promptObject.GetComponent<TextMesh>();
            promptText.font = promptFont; promptText.fontSize = 72; promptText.characterSize = .035f;
            promptText.anchor = TextAnchor.MiddleCenter; promptText.alignment = TextAlignment.Center;
            promptText.color = ZeldaUiPalette.Primary;
            promptMaterial = new Material(promptFont.material) { hideFlags = HideFlags.HideAndDontSave };
            var renderer = promptObject.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = promptMaterial; renderer.sortingOrder = short.MaxValue - 2;
            ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        }
        promptText.text = HasBattery ? "按[E]卸下魔力电池" : "按[E]安装魔力电池";
        promptFont.RequestCharactersInTexture(promptText.text, 72, FontStyle.Normal);
        promptMaterial.mainTexture = promptFont.material.mainTexture;
        promptObject.transform.position = mover.GetOverheadWorldPosition(new Vector2(0f, .9f));
        promptObject.transform.rotation = Quaternion.identity;
        promptObject.SetActive(true);
    }

    private void OnDisable() { if (promptObject != null) promptObject.SetActive(false); }
    private void OnDestroy()
    {
        Sockets.Remove(this);
        if (promptObject != null) Destroy(promptObject);
        if (promptMaterial != null) Destroy(promptMaterial);
    }
    private void OnValidate()
    {
        interactionDistance = Mathf.Max(.1f, interactionDistance);
        if (linkedMechanism != null && !(linkedMechanism is LeverData) && !(linkedMechanism is WaterwayGateController))
        {
            Debug.LogWarning("电池槽只能绑定拉杆 LeverData 或水闸控制器 WaterwayGateController。", this);
            linkedMechanism = null;
        }
    }
}
