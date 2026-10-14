/// <summary>Fungible currency for future shops; R never consumes or places it.</summary>
public sealed class GoldCoinPickupItem : PickupItemBase
{
    public const string DefaultItemId = "gold_coin";
    public const int StackLimit = 20;

    // One world visual represents the entire selected inventory stack.
    internal void ConfigureDroppedStack(int quantity) => PickupQuantity = quantity;

    public override bool CanUseFromInventory => false;
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;
}
