using UnityEngine;

/// <summary>A passive quest hand-in item; it intentionally has no R action.</summary>
public sealed class SynthesisToolsPickupItem : PickupItemBase
{
    public const string DefaultItemId = "synthesis_tools";

    public override bool CanUseFromInventory => false;

    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;

    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;

    protected override void OnPickedUp(PersistentInventory inventory)
    {
        base.OnPickedUp(inventory);
        QuestJournalManager.GetOrCreate().RefreshSandwormSupportInventory();
    }
}
