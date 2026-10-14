/// <summary>Passive golden gem used as the material for forging a trial credential.</summary>
public sealed class GoldenGemPickupItem : PickupItemBase
{
    public const string DefaultItemId = "golden_gem";
    public override bool CanUseFromInventory => false;
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;
    protected override void OnPickedUp(PersistentInventory inventory)
    {
        base.OnPickedUp(inventory);
        QuestJournalManager.GetOrCreate().RecordCounterfeitGemAcquired();
    }
}
