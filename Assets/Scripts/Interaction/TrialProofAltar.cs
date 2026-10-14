using System;
using UnityEngine;

/// <summary>One-time trial-proof offering. Inventory and completion commit together before save callbacks.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("Interaction/Trial Proof Altar (试炼祭坛)")]
public sealed class TrialProofAltar : MonoBehaviour
{
    [Serializable] public struct RuntimeState { public bool offered; public string offeredItemId; }

    [Header("Offering / 供奉")]
    [SerializeField] private TrialProofPickupItem proofPrefab;
    [SerializeField] private CounterfeitTrialProofPickupItem counterfeitProofPrefab;
    [SerializeField, Tooltip("供奉成功后打开的双扇滑动门。未绑定时不会消耗证明。")]
    private DoubleSlidingDoor linkedDoor;
    [Header("Interaction / 靠近按 E")]
    [SerializeField, Min(.1f)] private float interactionDistance = 1.75f;
    [SerializeField] private Font promptFont;

    private bool offered, submitting, hasInitialDoorState, initialDoorOpen;
    private TrialProofAltarVisual visual;
    private TextMesh prompt;
    private Material promptMaterial;
    private InventoryItemCost[] offeringCost;
    private string offeredItemId;
    public bool IsComplete => offered;
    public DoubleSlidingDoor LinkedDoor => linkedDoor;
    public TrialProofPickupItem ProofPrefab => proofPrefab;
    public string OfferedItemId => offeredItemId;
    public PickupItemBase OfferedProofPrefab => offered ? ResolveProofPrefab(offeredItemId) : null;
    private bool InputBlocked => Time.timeScale <= 0f || CharacterPotionBuffs.IsRestoringWorld ||
        DocumentReader.IsInputBlocked || ClockworkPuppetRuntime.BlocksCharacterInput;

    public string InteractionMessage
    {
        get
        {
            if (offered) return "你已证明了自己";
            string id = AvailableOfferingId(PersistentInventory.Instance);
            return id == null ? "需要试炼证明" : id == CounterfeitTrialProofPickupItem.DefaultItemId
                ? "按[E]供奉赝品试炼证明" : "按[E]供奉试炼证明";
        }
    }
    private static bool IsProofId(string id) => id == TrialProofPickupItem.DefaultItemId ||
        id == CounterfeitTrialProofPickupItem.DefaultItemId;
    private static string AvailableOfferingId(PersistentInventory inventory)
    {
        if (inventory == null) return null;
        var selected = inventory.GetSlot(inventory.SelectedSlotIndex);
        // Respect an explicitly selected credential; otherwise spend a counterfeit before the genuine article.
        if (selected != null && !selected.IsEmpty && IsProofId(selected.ItemId)) return selected.ItemId;
        if (inventory.Contains(CounterfeitTrialProofPickupItem.DefaultItemId)) return CounterfeitTrialProofPickupItem.DefaultItemId;
        return inventory.Contains(TrialProofPickupItem.DefaultItemId) ? TrialProofPickupItem.DefaultItemId : null;
    }
    private PickupItemBase ResolveProofPrefab(string id)
    {
        if (id == CounterfeitTrialProofPickupItem.DefaultItemId)
        {
            // Backward-compatible with previously placed/unpacked altars whose new reference is still null.
            if (counterfeitProofPrefab == null)
                counterfeitProofPrefab = Resources.Load<GameObject>("PickupItems/CounterfeitTrialProofPickupItem")
                    ?.GetComponent<CounterfeitTrialProofPickupItem>();
            return counterfeitProofPrefab;
        }
        return id == TrialProofPickupItem.DefaultItemId ? proofPrefab : null;
    }

    private void Awake() { RememberDoorState(); visual = GetComponent<TrialProofAltarVisual>(); }
    private void OnEnable()
    {
        if (offered) RestoreLinkedDoor();
        RefreshVisual();
    }
    private void RememberDoorState()
    {
        if (hasInitialDoorState || linkedDoor == null) return;
        initialDoorOpen = linkedDoor.IsOpenRequested;
        hasInitialDoorState = true;
    }
    private ZeldaFourWayMover Nearby()
    {
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        if (mover == null || !mover.isActiveAndEnabled ||
            ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) != gameObject.scene ||
            ((Vector2)(mover.transform.position - transform.position)).sqrMagnitude > interactionDistance * interactionDistance)
            return null;
        var data = mover.GetComponent<ZeldaCharacterData>();
        return data != null && !data.IsDead && !data.IsGhostLike ? mover : null;
    }
    private void Update()
    {
        if (!offered && !InputBlocked && Input.GetKeyDown(KeyCode.E))
        {
            var mover = Nearby();
            if (mover != null) ZeldaInteractionArbiter.Submit(this, mover, KeyCode.E, transform.position, Interact);
        }
    }
    private void Interact()
    {
        TryOffer(out string reason);
        if (!string.IsNullOrEmpty(reason)) ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup(reason);
    }
    public bool TryOffer() => TryOffer(out _);
    public bool TryOffer(out string reason)
    {
        reason = null;
        if (!isActiveAndEnabled || submitting || InputBlocked) return false;
        var mover = Nearby();
        if (mover == null || mover.IsAttacking) return false;
        if (offered) { reason = "你已证明了自己"; return false; }
        var inventory = PersistentInventory.Instance;
        string offeringId = AvailableOfferingId(inventory);
        if (offeringId == null)
        { reason = "需要试炼证明"; return false; }
        var offeringPrefab = ResolveProofPrefab(offeringId);
        if (linkedDoor == null || linkedDoor.gameObject.scene != gameObject.scene || offeringPrefab == null)
        { reason = "祭坛尚未绑定滑动门或试炼证明"; return false; }
        RememberDoorState();
        if (!mover.TryBeginItemAttack(.24f)) return false;
        submitting = true;
        try
        {
            if (offeringCost == null || offeringCost[0].item != offeringPrefab)
                offeringCost = new[] { new InventoryItemCost { item = offeringPrefab, quantity = 1 } };
            // Consume only the chosen type, never both; commit before inventory/save callbacks.
            bool consumed = inventory.TryExchangeItems(offeringCost, null, () => {
                offered = true;
                offeredItemId = offeringId;
                linkedDoor.Open();
                RefreshVisual();
            });
            if (!consumed) { mover.EndItemAttack(); reason = "未能供奉，请稍后再试"; return false; }
            reason = "你已证明了自己";
            return true;
        }
        finally { submitting = false; }
    }
    public RuntimeState CaptureRuntimeState() => new RuntimeState { offered = offered, offeredItemId = offeredItemId };
    public void ApplyRuntimeState(RuntimeState state)
    {
        RememberDoorState();
        offered = state.offered;
        // Older saves only recorded completion and therefore always represent the genuine proof.
        offeredItemId = offered ? (IsProofId(state.offeredItemId) ? state.offeredItemId : TrialProofPickupItem.DefaultItemId) : null;
        RefreshVisual();
        RestoreLinkedDoor();
    }
    // Also called after the save system restores every leaf transform, including inactive streamed doors.
    public void RestoreLinkedDoor()
    {
        RememberDoorState();
        if (linkedDoor != null) linkedDoor.SetOpenImmediately(offered || initialDoorOpen);
    }
    private void RefreshVisual()
    {
        if (visual == null) visual = GetComponent<TrialProofAltarVisual>();
        if (visual != null) visual.SetOffered(offered, OfferedProofPrefab);
    }
    private void LateUpdate()
    {
        var mover = InputBlocked ? null : Nearby();
        if (mover == null) { ShowPrompt(false); return; }
        ZeldaInteractionArbiter.OfferInteraction(this, mover, KeyCode.E, transform.position, ShowPrompt);
        EnsurePrompt();
        if (prompt == null) return;
        string message = InteractionMessage;
        if (prompt.text != message)
        {
            prompt.text = message;
            prompt.font.RequestCharactersInTexture(message, 72, FontStyle.Normal);
        }
        promptMaterial.mainTexture = prompt.font.material.mainTexture;
        prompt.transform.position = mover.GetOverheadWorldPosition(new Vector2(0, .9f));
        prompt.transform.rotation = Quaternion.identity;
    }
    private void EnsurePrompt()
    {
        if (prompt != null) return;
        var font = promptFont != null ? promptFont : ZeldaHealthHeartsUI.Instance?.PermissionLabelFont;
        if (font == null) return;
        var go = new GameObject("Trial Altar Interaction Prompt") { hideFlags = HideFlags.HideAndDontSave };
        // Scene-local, unparented: scale/rotation of the altar must not shrink the interaction text.
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, gameObject.scene);
        prompt = go.AddComponent<TextMesh>();
        prompt.font = font; prompt.fontSize = 72; prompt.characterSize = .035f;
        prompt.anchor = TextAnchor.MiddleCenter; prompt.alignment = TextAlignment.Center;
        prompt.color = ZeldaUiPalette.Primary;
        promptMaterial = new Material(font.material) { hideFlags = HideFlags.HideAndDontSave };
        var renderer = go.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = promptMaterial;
        int value = int.MinValue;
        foreach (var layer in SortingLayer.layers)
            if (layer.value > value) { value = layer.value; renderer.sortingLayerID = layer.id; }
        renderer.sortingOrder = short.MaxValue - 2;
        ZeldaPossessionProgressBar.ConfigureOverlayRenderer(renderer);
        go.SetActive(false);
    }
    private void ShowPrompt(bool value)
    {
        if (prompt != null && prompt.gameObject.activeSelf != value) prompt.gameObject.SetActive(value);
    }
    private void OnDisable() => ShowPrompt(false);
    private void OnDestroy()
    {
        if (prompt != null) Destroy(prompt.gameObject);
        if (promptMaterial != null) Destroy(promptMaterial);
    }
    private void OnValidate() { interactionDistance = Mathf.Max(.1f, interactionDistance); }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, .8f, .32f, .8f);
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
        if (linkedDoor != null) Gizmos.DrawLine(transform.position, linkedDoor.transform.position);
    }
}
