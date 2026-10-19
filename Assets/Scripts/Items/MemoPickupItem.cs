/// <summary>Inventory consumable: permanently expands the consciousness-owned inventory.</summary>
public sealed class MemoPickupItem : PickupItemBase
{
    public const string DefaultItemId = "memo";
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    // PersistentInventory commits capacity and consumption as one transaction.
    // Direct effect calls must not create free slots without consuming an item.
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;
}
