/// <summary>A crude trial credential; passive inventory item accepted by the same offering altar.</summary>
public sealed class CounterfeitTrialProofPickupItem : PickupItemBase
{
    public const string DefaultItemId = "counterfeit_trial_proof";

    public override bool CanUseFromInventory => false;
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;
}
