using System;
using UnityEngine;

/// <summary>One-time, transactional installation on the Floor2 mortar. No offscene object references.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Interaction/Fortress Mortar Remote Installation (臼炮遥控装置)")]
public sealed class FortressMortarRemoteInstallation : MonoBehaviour
{
    [Serializable] public struct RuntimeState { public bool installed; }
    [SerializeField] private string installationScene = "Level2-Floor2";
    [SerializeField, Min(.1f), Tooltip("从臼炮碰撞边缘计算的安装距离。")]
    private float interactionDistance = 1.3f;
    [SerializeField] private Font promptFont;
    private bool installed;
    private Collider2D body;
    private TextMesh prompt;
    private Material promptMaterial;
    private GameObject mountedDevice;
    public bool IsInstalled => installed;
    public RuntimeState CaptureRuntimeState() => new RuntimeState { installed = installed };
    public void ApplyRuntimeState(RuntimeState state)
    {
        installed = state.installed; RefreshMountedVisual();
        if (installed) QuestJournalManager.GetOrCreate().RecordSandwormMortarInstallation();
    }
    private bool Blocked => DocumentReader.IsInputBlocked || Time.timeScale <= 0f ||
        GameSaveSystem.IsLoading || CharacterPotionBuffs.IsRestoringWorld || ClockworkPuppetRuntime.BlocksCharacterInput;
    private void Awake() { body = GetComponent<Collider2D>(); }
    private void OnEnable() { RefreshMountedVisual(); }
    private ZeldaFourWayMover Nearby()
    {
        if (!string.Equals(gameObject.scene.name, installationScene, StringComparison.OrdinalIgnoreCase)) return null;
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !mover.isActiveAndEnabled || mover.CharacterData == null ||
            mover.CharacterData.IsDead || mover.CharacterData.IsGhostLike ||
            ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != gameObject.scene) return null;
        Vector2 position = mover.transform.position;
        Vector2 nearest = body != null && body.enabled ? body.ClosestPoint(position) : (Vector2)transform.position;
        if ((position - nearest).sqrMagnitude > interactionDistance * interactionDistance) return null;
        var hit = Physics2D.Linecast(position, nearest, LayerMask.GetMask("Blocks", "Hidden Blocks"));
        if (hit.collider != null && !hit.transform.IsChildOf(transform) && !hit.transform.IsChildOf(mover.transform)) return null;
        return mover;
    }
    private bool SelectedDevice => PersistentInventory.Instance?.GetSlot(PersistentInventory.Instance.SelectedSlotIndex)?.ItemId ==
        MortarControlDevicePickupItem.DefaultItemId;
    public string InteractionMessage => installed ? "已安装远程控制装置" :
        SelectedDevice ? "按[E]安装远程控制装置" : "需要远程控制装置";
    private void Update()
    {
        var mover = Blocked ? null : Nearby();
        if (mover == null) { ShowPrompt(false); return; }
        EnsurePrompt();
        if (prompt != null)
        {
            string message = InteractionMessage;
            if (prompt.text != message) { prompt.text = message; prompt.font.RequestCharactersInTexture(message, 72); }
            promptMaterial.mainTexture = prompt.font.material.mainTexture;
            prompt.transform.position = mover.GetOverheadWorldPosition(new Vector2(0,.9f));
        }
        ZeldaInteractionArbiter.OfferInteraction(this, mover, KeyCode.E, transform.position, ShowPrompt);
        if (Input.GetKeyDown(KeyCode.E)) ZeldaInteractionArbiter.Submit(this, mover, KeyCode.E, transform.position, InstallFromInput);
    }
    private void InstallFromInput()
    {
        if (!TryInstall()) ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup(InteractionMessage);
    }
    public bool TryInstall()
    {
        if (!isActiveAndEnabled || Blocked || installed || !SelectedDevice) return false;
        var mover = Nearby();
        if (mover == null || mover.IsAttacking) return false;
        bool success = PersistentInventory.Instance.TryExchangeSelectedItem(MortarControlDevicePickupItem.DefaultItemId,
            MortarRemotePickupItem.DefaultItemId, () => { installed = true; RefreshMountedVisual(); });
        if (!success) return false;
        QuestJournalManager.GetOrCreate().RecordSandwormMortarInstallation();
        mover.TryBeginItemAttack(.24f);
        ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("装置已安装，获得臼炮遥控器");
        GameSaveSystem.RequestAutoSave("mortar_remote_installed", true);
        return true;
    }
    private void RefreshMountedVisual()
    {
        if (installed && mountedDevice == null)
        {
            var template = Resources.Load<GameObject>("PickupItems/MortarControlDevicePickupItem")?.GetComponent<PickupItemVisualBase>();
            if (template == null) return;
            mountedDevice = new GameObject("Mounted Remote Receiver", typeof(SpriteRenderer)) { hideFlags = HideFlags.DontSave };
            mountedDevice.transform.SetParent(transform, false);
            mountedDevice.transform.localPosition = new Vector3(.78f, -.55f, -.01f);
            mountedDevice.transform.localScale = Vector3.one * .62f;
            var renderer = mountedDevice.GetComponent<SpriteRenderer>();
            renderer.sprite = template.InventoryIcon; renderer.color = Color.white; renderer.sortingOrder = 0;
        }
        if (mountedDevice != null) mountedDevice.SetActive(installed);
    }
    private void EnsurePrompt()
    {
        if (prompt != null) return;
        var font = promptFont != null ? promptFont : ZeldaHealthHeartsUI.Instance?.PermissionLabelFont;
        if (font == null) return;
        var go = new GameObject("Mortar Installation Prompt") { hideFlags = HideFlags.HideAndDontSave };
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        prompt = go.AddComponent<TextMesh>(); prompt.font = font; prompt.fontSize = 72; prompt.characterSize = .035f;
        prompt.anchor = TextAnchor.MiddleCenter; prompt.alignment = TextAlignment.Center; prompt.color = ZeldaUiPalette.Primary;
        promptMaterial = new Material(font.material) { hideFlags = HideFlags.HideAndDontSave };
        var renderer = go.GetComponent<MeshRenderer>(); renderer.sharedMaterial = promptMaterial;
        renderer.sortingOrder = short.MaxValue - 2; ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        go.SetActive(false);
    }
    private void ShowPrompt(bool show) { if (prompt != null) prompt.gameObject.SetActive(show); }
    private void OnDisable() => ShowPrompt(false);
    private void OnDestroy() { if (prompt != null) Destroy(prompt.gameObject); if (promptMaterial != null) Destroy(promptMaterial); }
}
