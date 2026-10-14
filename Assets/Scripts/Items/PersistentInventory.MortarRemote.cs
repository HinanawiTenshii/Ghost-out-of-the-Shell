using System;
using UnityEngine;

public sealed partial class PersistentInventory
{
    private void TickMortarRemoteCooldowns(float delta)
    {
        if (delta <= 0f || GameSaveSystem.IsLoading) return;
        foreach (var slot in slots)
            if (slot != null && !slot.IsEmpty && slot.ItemId == MortarRemotePickupItem.DefaultItemId && slot.HasStoredCharge)
                slot.SetStoredCharge(MortarRemotePickupItem.SanitizeCooldown(slot.StoredCharge - delta));
        // No InventoryChanged/PlayerPrefs write or icon rebuild for each tick.
        // Save capture reads these scalar values directly; only the cooldown UI polls them.
    }

    private bool TryUseMortarRemote(MortarRemotePickupItem template, Slot slot, ZeldaCharacterData user)
    {
        if (user == null || user.IsDead || user.IsGhostLike || Time.timeScale <= 0f ||
            DocumentReader.IsInputBlocked || GameSaveSystem.IsLoading || ClockworkPuppetRuntime.BlocksCharacterInput)
            return false;
        var scene = ZeldaRuntimeRegistry.GetGameplayScene(user.gameObject);
        if (!scene.IsValid() || !scene.isLoaded || !string.Equals(scene.name, MortarRemotePickupItem.AllowedScene, StringComparison.OrdinalIgnoreCase))
        {
            ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("遥控器只能在城外沙漠中使用");
            return false;
        }
        if (slot.HasStoredCharge && MortarRemotePickupItem.SanitizeCooldown(slot.StoredCharge) > 0f)
        {
            ZeldaHealthHeartsUI.Instance?.ShowNotificationPopup("臼炮正在装填，请稍候");
            return false;
        }
        if (!MortarAirstrike.TryCall(user, template.CallDelay)) return false;
        slot.SetStoredCharge(MortarRemotePickupItem.Cooldown);
        template.PlayUseGesture(user);
        CharacterPotionBuffs.NotifyAction(user);
        NotifyChanged();
        return true;
    }
}
