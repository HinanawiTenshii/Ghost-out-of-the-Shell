using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>Transfers one inventory battery and supplies a linked mechanism; unpowered gate controllers spring back.</summary>
[DisallowMultipleComponent, RequireComponent(typeof(BoxCollider2D), typeof(CameraVisionStreamingExempt))]
public sealed class BatterySocket : MonoBehaviour, IConsumableExplosive
{
    [Serializable]
    public struct RuntimeState
    {
        public bool installed;
        public PersistentInventory.RuntimeSlotState battery;
    }

    private static readonly HashSet<BatterySocket> Sockets = new HashSet<BatterySocket>();
    [SerializeField, Tooltip("可拖入整个水闸控制器、拉杆、警报器或带面板的电门柱对象，将自动识别同一对象上的功能组件。未绑定不影响其他机关。")]
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
    private BatteryPickupItem.ChargeState charge;
    private GameObject promptObject;
    private TextMesh promptText;
    private Material promptMaterial;

    public bool HasBattery => initialized ? batteryInstalled : startsWithBattery;
    public int RemainingCharge => HasBattery ? (initialized ? charge.remaining : BatteryPickupItem.DefaultCapacity) : 0;
    public int MaximumCharge => initialized && HasBattery ? charge.capacity : BatteryPickupItem.DefaultCapacity;
    public bool HasPower => HasBattery && RemainingCharge > 0;
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
        foreach (var socket in Sockets) socket.NotifyMechanismPowerState();
    }

    private void Awake() { Sockets.Add(this); EnsureInitialized(); NotifyMechanismPowerState(); MapPointOfInterest.AttachMechanism(this); }
    private void OnEnable() => Sockets.Add(this);

    // Do not deregister on disable: camera suspension must never bypass power requirements.
    public static bool IsPowered(Component mechanism)
    {
        if (mechanism == null) return true;
        foreach (var socket in Sockets)
            if (socket != null && socket.linkedMechanism == mechanism && !socket.HasPower)
                return false;
        return true;
    }

    // Some devices (alarms) require an actual supply; legacy levers/controllers
    // intentionally keep their existing unbound = usable behaviour.
    public static bool HasConnectedSocket(Component mechanism)
    {
        if (mechanism == null) return false;
        foreach (var socket in Sockets)
            if (socket != null && socket.linkedMechanism == mechanism) return true;
        return false;
    }

    public void BindMechanism(MonoBehaviour mechanism)
    {
        if (!TryResolveMechanism(mechanism, out MonoBehaviour resolved))
            throw new ArgumentException("电池槽只能绑定拉杆、水闸控制器、警报器或装有面板的电门柱。");
        linkedMechanism = resolved;
        NotifyMechanismPowerState();
    }

    private void NotifyMechanismPowerState()
    {
        // Event-driven: no new per-frame socket/controller polling. A normal
        // lever keeps its position, and restoring power never replays a toggle.
        if (linkedMechanism is WaterwayGateController controller)
            controller.RefreshBatteryPowerState();
    }

    private static bool TryResolveMechanism(MonoBehaviour candidate, out MonoBehaviour resolved)
    {
        resolved = null;
        if (SupportsMechanism(candidate)) { resolved = candidate; return true; }
        // Unity's generic MonoBehaviour object field may select the visual script
        // when a whole GameObject is dragged in. Resolve only on that same object;
        // never accidentally bind an unrelated device in a parent/child hierarchy.
        foreach (MonoBehaviour component in candidate.GetComponents<MonoBehaviour>())
        {
            if (component == null || !SupportsMechanism(component)) continue;
            resolved = component;
            return true;
        }
        return false;
    }

    private static bool SupportsMechanism(MonoBehaviour mechanism)
    {
        return mechanism == null || mechanism is LeverData || mechanism is WaterwayGateController || mechanism is AlarmDevice
            || (mechanism is ElectricGatePillar pillar && pillar.HasPanel);
    }

    // Atomic preflight before charging. Each bound socket is charged once, never once per damage arc.
    public static bool TryConsumeForMechanisms(Component first, Component second)
    {
        foreach (var socket in Sockets)
            if (socket != null && socket.linkedMechanism != null && (socket.linkedMechanism == first || socket.linkedMechanism == second)
                && (socket.transferring || !socket.HasPower)) return false;
        foreach (var socket in Sockets)
            if (socket != null && socket.linkedMechanism != null && (socket.linkedMechanism == first || socket.linkedMechanism == second))
                socket.TryConsumeCharge();
        return true;
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
        if (!batteryInstalled) return;
        if (string.IsNullOrEmpty(storedBattery.itemInstanceId))
        {
            storedBattery.itemId = BatteryPickupItem.DefaultItemId;
            storedBattery.itemInstanceId = Guid.NewGuid().ToString("N");
            storedBattery.quantity = 1;
            if (string.IsNullOrEmpty(storedBattery.itemName)) storedBattery.itemName = BatteryPickupItem.DefaultItemName;
            if (string.IsNullOrEmpty(storedBattery.itemDescription)) storedBattery.itemDescription = BatteryPickupItem.DefaultDescription;
        }
        charge = BatteryPickupItem.ReadCharge(storedBattery.itemState, storedBattery.hasStoredCharge, storedBattery.storedCharge);
        StoreCharge();
    }

    private void StoreCharge()
    {
        storedBattery.hasStoredCharge = true;
        storedBattery.storedCharge = charge.remaining;
        storedBattery.itemState = BatteryPickupItem.EncodeCharge(charge.remaining, charge.capacity);
    }

    /// <summary>Wire an explicit event on the linked device to this method, passing that device.</summary>
    public void ConsumeChargeFrom(MonoBehaviour source)
    {
        if (source != null && source == linkedMechanism) TryConsumeCharge();
    }

    // No automatic subscription to gate open/close events: existing water gates consume nothing.
    public bool TryConsumeCharge()
    {
        EnsureInitialized();
        if (transferring || !HasPower) return false;
        charge.remaining--;
        StoreCharge();
        NotifyMechanismPowerState();
        return true;
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
        NotifyMechanismPowerState();
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
            NotifyMechanismPowerState();
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
            NotifyMechanismPowerState();
            return true;
        }
        finally { transferring = false; }
    }

    /// <summary>Only the installed battery is destroyed; the empty socket remains reusable.</summary>
    public bool TryConsumeExplosive(out int attackPower)
    {
        attackPower = 0;
        if (!isActiveAndEnabled || transferring) return false;
        EnsureInitialized();
        if (!batteryInstalled) return false;
        batteryInstalled = false;
        storedBattery = default;
        attackPower = BatteryPickupItem.ExplosionAttackPower;
        NotifyMechanismPowerState();
        return true;
    }

    public void ReceiveAttack(int damage, ZeldaCharacterData attacker)
    {
        if (damage <= 0 || !isActiveAndEnabled) return;
        EnsureInitialized();
        if (!batteryInstalled || transferring) return;
        transferring = true;
        try
        {
            PlacedBomb explosion = BatteryPickupItem.CreateDetonation(this, attacker);
            if (explosion == null) return;
            // Cut power and clear saved identity before emitting a chain-reaction hitbox.
            batteryInstalled = false;
            storedBattery = default;
            NotifyMechanismPowerState();
            explosion.DetonateImmediately();
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
        var mover = NearbyMover();
        if (mover == null || mover.IsAttacking) return;
        bool removing = HasBattery;
        bool success = removing ? TryUninstall(PersistentInventory.Instance) : TryInstall(PersistentInventory.Instance);
        if (success) mover.TryBeginItemAttack(.24f);
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
        if (TryResolveMechanism(linkedMechanism, out MonoBehaviour resolved))
        {
            linkedMechanism = resolved;
        }
        else
        {
            Debug.LogWarning("电池槽只能绑定拉杆、水闸控制器、警报器或装有面板的电门柱。", this);
            linkedMechanism = null;
        }
    }
}
