using UnityEngine;

/// <summary>A one-shot inheritance, not an inventory item. The ability belongs to the receiving body.</summary>
public sealed class SilenceSealRelicPickupItem : PickupItemBase
{
    public const string DefaultItemId = "silence_seal_relic";
    private bool consumed;

    public override bool EntersInventoryOnPickup => false;
    public override bool CanUseFromInventory => false;
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;
    protected override bool CanAttemptPickup => !consumed && !IsPickupBeingConsumed;

    public override bool TryPickUp()
    {
        var mover = ZeldaRuntimeRegistry.GetControlledMover();
        var owner = mover != null ? mover.CharacterData : null;
        if (!CanAttemptPickup || !isActiveAndEnabled || IsPickupLocked || IsPickupBlockedForControlledCharacter() || owner == null ||
            owner.IsDead || owner.IsGhostLike || !mover.isActiveAndEnabled ||
            DocumentReader.IsInputBlocked || Time.timeScale <= 0f || GameSaveSystem.IsLoading ||
            ZeldaRuntimeRegistry.GetGameplayScene(gameObject) != ZeldaRuntimeRegistry.GetGameplayScene(mover.gameObject) ||
            Vector2.Distance(transform.position, mover.transform.position) > PickupDistance) return false;

        if (!owner.CanReceiveRelicCharacterSkill)
        {
            ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("当前角色已有角色能力，无法承接圣印");
            return false;
        }

        // Guard before ValuesChanged/PickedUp callbacks: they can re-enter in this frame.
        consumed = true;
        if (!owner.TryGrantRelicCharacterSkill(SilenceOathRuntime.SkillId))
        { consumed = false; return false; }

        gameObject.SetActive(false);
        try
        {
            PlayerGrowthAttributes.GetOrCreate().RecordPossessedCharacter(owner);
            base.OnPickedUp(PersistentInventory.Instance);
            NotifyPickedUp();
        }
        finally { Destroy(gameObject); }
        GameSaveSystem.RequestAutoSave("relic_skill_granted:" + SilenceOathRuntime.SkillId, true);
        return true;
    }
}
