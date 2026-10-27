using UnityEngine;

public sealed partial class PersistentInventory
{
    private bool TryUseMemo(MemoPickupItem template, Slot source, ZeldaCharacterData user)
    {
        var mover = user != null ? user.GetComponent<ZeldaFourWayMover>() : null;
        if (itemExchangeInProgress || user == null || user.IsDead || user.IsGhostLike || mover == null ||
            !mover.isActiveAndEnabled || ZeldaRuntimeRegistry.GetControlledMover() != mover ||
            mover.IsPossessionInProgress || Time.timeScale <= 0f || DocumentReader.IsInputBlocked ||
            GameSaveSystem.IsLoading || ClockworkPuppetRuntime.BlocksCharacterInput || SoulMarkRuntime.IsTransferring ||
            source == null || source.IsEmpty || source.ItemId != MemoPickupItem.DefaultItemId) return false;

        itemExchangeInProgress = true;
        try
        {
            // Commit consumption and capacity together, before any callbacks can
            // save or re-enter R. Preserve existing Slot objects and selection.
            slotCount = slots.Length + 1;
            EnsureSlots();
            DecrementSlot(source);
            template.PlayUseGesture(user);
            CharacterPotionBuffs.NotifyAction(user);
            NotifyChanged();
            ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("物品栏容量 +1");
            GameSaveSystem.RequestAutoSave("memo_used", true);
            return true;
        }
        finally { itemExchangeInProgress = false; }
    }
}
