/// <summary>A passive trial credential. Altar submission is deliberately not implemented here.</summary>
public sealed class TrialProofPickupItem : PickupItemBase
{
    public const string DefaultItemId = "trial_proof";
    public const string SandwormRewardIdentityPrefix = "sandworm-proof:";

    public override bool CanUseFromInventory => false;
    public override bool CanBeStoredInCardboardBox(ZeldaCharacterData user) => false;
    protected override bool ApplyUseEffect(ZeldaCharacterData user) => false;

    protected override void OnPickedUp(PersistentInventory inventory)
    {
        base.OnPickedUp(inventory);
        // Keep provenance in the pickup identity: inventory, drops and save
        // restoration already preserve it. Other proofs must not finish this hunt.
        // Older sandworm rewards used this prefix only on their world stable ID.
        var stable = GetComponent<SceneTravelStableId>();
        if (UniqueInstanceId.StartsWith(SandwormRewardIdentityPrefix, System.StringComparison.Ordinal) ||
            (stable != null && stable.StableId != null &&
             stable.StableId.StartsWith(SandwormRewardIdentityPrefix, System.StringComparison.Ordinal)))
            QuestJournalManager.GetOrCreate().RecordQuestCompletion(QuestJournalManager.Level2SandwormQuestId);
    }
}
