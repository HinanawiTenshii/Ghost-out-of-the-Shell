using UnityEngine;

/// <summary>Passive installation kit. Select it and interact with a mortar using E.</summary>
public sealed class MortarControlDevicePickupItem : PickupItemBase
{
    public const string DefaultItemId = "mortar_control_device";
    public override bool CanUseFromInventory => false;
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;
    protected override void OnPickedUp(PersistentInventory inventory)
    {
        base.OnPickedUp(inventory);
        QuestJournalManager.GetOrCreate().RefreshSandwormSupportInventory();
    }
}
