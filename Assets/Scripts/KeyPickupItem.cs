public sealed class KeyPickupItem : PickupItemBase
{
    public override string DescriptionDiscoveryId
    {
        get
        {
            // Doors identify keys by their persistent/unique instance ID, so
            // keys for different doors receive separate first-pickup entries
            // even when they share the same prefab, item ID and display name.
            string identity = PersistentIdentity != null
                ? PersistentIdentity.StableInstanceId
                : UniqueInstanceId;
            return "key-door:" + identity;
        }
    }

    protected override bool ApplyUseEffect(ZeldaCharacterData user)
    {
        // This template key intentionally has no use effect and is not consumed.
        return false;
    }

    protected override void OnPickedUp(PersistentInventory inventory)
    {
        base.OnPickedUp(inventory);

        // Level0's GoldenKey resolves the objective introduced by KeyFile.
        // Use both scene and placed-object name so other keys created from the
        // same prefab never complete this quest accidentally.
        if (gameObject.scene.name == "Level0" && name == "GoldenKey")
        {
            QuestJournalManager journal = QuestJournalManager.GetOrCreate();
            journal.RecordQuestCompletion("level0.obtain_gate_key");
        }

        if (gameObject.scene.name == "Level1-Floor3")
        {
            QuestJournalManager journal = QuestJournalManager.GetOrCreate();
            journal.RecordQuestCompletion("level1.find_castle_gate_key");
        }
    }
}
