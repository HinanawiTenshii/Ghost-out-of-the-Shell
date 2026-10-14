/// <summary>Alchemy ingredient: R remains inert; submit the selected powder to a cauldron with E.</summary>
public sealed class HerbPowderPickupItem : PickupItemBase
{
    public override bool CanUseFromInventory => false;
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;
}
