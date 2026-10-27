using System;
using System.IO;

/// <summary>
/// Read-only, non-Unity smoke checks for semantic autosave hook coverage. Run with
/// Add-Type -Path Tools/AutoSaveProgressSmoke.cs; [AutoSaveProgressSmoke]::RunSourceChecks((Get-Location).Path)
/// Does not open Unity, scene assets, or any player save directory.
/// </summary>
public static class AutoSaveProgressSmoke
{
    static int checks;

    public static int RunSourceChecks(string projectRoot)
    {
        checks = 0;
        string quest = Read(projectRoot, "Quests/QuestJournalManager.cs");
        string acceptance = Method(quest, "public bool AddOrUpdateEntry(");
        Has(acceptance, "if (progressChanged) RequestQuestAutoSave", "duplicate updates do not checkpoint");
        Has(acceptance, "RequestQuestAutoSave(normalizedId, \"quest_accepted\")", "all scene-authored quest acceptance is covered");
        Ordered(acceptance, "RecordQuestInventoryEvidence();", "\"quest_accepted\"", "acceptance checkpoint follows inventory evidence");
        Has(Method(quest, "public bool CompleteEntry("), "\"quest_completed\"", "shared quest completion checkpoints");
        Has(Method(quest, "public bool AppendEntryDetails("), "\"quest_detail\"", "shared stage clues checkpoint");
        Has(Method(quest, "public bool RecordQuestCompletion(string entryId, bool"), "if (!pendingCompletionEvidence.Add(normalizedId)) return false;", "completion evidence deduplicated");
        Has(Method(quest, "private bool RecordPendingDetail("), "\"quest_detail_evidence\"", "clues before acceptance persist");
        Has(Method(quest, "private bool RecordPendingUpdate("), "previous.details == change.details", "pending updates deduplicated");
        Has(Method(quest, "private void LoadInitialEntriesForScene("), "\"quest_initial_objectives\"", "early-level initial objectives covered");
        string restore = Method(quest, "public void ApplySaveState(");
        Has(restore, "restoringAutoSaveProgress = true", "journal restore suppresses requests");
        Has(restore, "finally { restoringAutoSaveProgress = wasRestoring; }", "restore guard is exception-safe");
        string gate = Method(quest, "private void RequestQuestAutoSave(");
        foreach (string required in new[] { "restoringAutoSaveProgress", "GameSaveSystem.IsLoading", "IsSceneTravelRestoreInProgress", "failureEvidence.ContainsKey", "ArenaQuestStage.Defeated", "ArenaQuestStage.WinnerDied" })
            Has(gate, required, "semantic guard: " + required);
        foreach (string signature in new[] { "public bool TrackEntry(", "public void ResetAllEntries(", "public bool RemoveEntry(" })
            NoRequest(Method(quest, signature), "non-progress journal method: " + signature);
        NoRequest(Method(Read(projectRoot, "Quests/QuestJournalManager.Hierarchy.cs"), "public void SetEntryCollapsed("), "fold state is not a checkpoint");
        NoRequest(Method(Read(projectRoot, "Quests/QuestJournalManager.Failures.cs"), "public bool RecordQuestFailure("), "quest failure is not a checkpoint");

        string passwords = Read(projectRoot, "Quests/QuestJournalManager.KnownPasswords.cs");
        foreach (string suffix in new[] { "TavernRoom", "EngineerSafe", "EntertainmentRoom", "WarehouseGate", "ChurchRelic" })
        {
            string body = Method(passwords, "public void Unlock" + suffix + "Password(");
            Has(body, "if (", "password unlock idempotence: " + suffix);
            Has(body, "RequestQuestAutoSave", "password checkpoint: " + suffix);
        }
        NoRequest(Method(passwords, "private void RestoreKnownPasswords("), "password restore is not a checkpoint");

        string growth = Read(projectRoot, "Items/PlayerGrowthAttributes.cs");
        string discovery = Method(growth, "public void RecordPossessedCharacter(");
        Ordered(discovery, "discoveredCharacterSkills.Contains(id)", "RequestAutoSave", "only first skill discovery checkpoints, not every possession");
        Ordered(Method(growth, "public bool TryUpgradeSkill("), "collectibleCount -= skill.cost;", "RequestAutoSave", "skill upgrades saved after spending points");
        foreach (string signature in new[] { "public void ApplyRuntimeState(", "private void Update(", "public void AddCollectibles(", "public int SpendPotionEnergy(" })
            NoRequest(Method(growth, signature), "no per-frame/resource/restore hooks: " + signature);
        Ordered(Method(Read(projectRoot, "Items/GrowthCollectible.cs"), "public bool TryCollect("), "Destroy(gameObject);", "RequestAutoSave", "DATA saved after collectible removal");
        Ordered(Method(Read(projectRoot, "Items/SilenceSealRelicPickupItem.cs"), "public override bool TryPickUp("), "finally { Destroy(gameObject); }", "RequestAutoSave", "relic saved after consume/grant transaction");
        Ordered(Method(Read(projectRoot, "Items/PersistentInventory.Memo.cs"), "private bool TryUseMemo("), "DecrementSlot(source);", "RequestAutoSave", "MEMO saved after consume/expand transaction");
        Ordered(Method(Read(projectRoot, "Items/PersistentInventory.ShopPurchase.cs"), "public bool TryPurchaseItem("), "item.CompleteShopPurchase(this);", "RequestAutoSave", "shop saves after stock/payment commit");
        Ordered(Method(Read(projectRoot, "Items/PersistentInventory.RecipeExchange.cs"), "public bool TryExchangeItems("), "onCommitted?.Invoke();", "RequestAutoSave", "recipe saves after all committed callbacks");
        string pickup = Read(projectRoot, "Items/PickupItemBase.cs");
        string notify = Method(pickup, "protected void NotifyPickedUp(");
        Has(notify, "else GameSaveSystem.MarkAutoSaveProgress();", "ordinary pickups only dirty fallback");
        Ordered(notify, "PickedUp?.Invoke(this);", "RequestAutoSave", "quest pickups saved after callbacks");
        foreach (string type in new[] { "KeyPickupItem", "FusePickupItem", "PurpleBombShellPickupItem", "SkyBlueGunpowderPickupItem", "TrialProofPickupItem", "MemoPickupItem" })
            Has(pickup, "this is " + type, "important pickup classification: " + type);
        Has(pickup, "ItemId == \"purple_bomb\"", "Level1 super bomb uses actual prefab ID");
        Has(Read(projectRoot, "Interaction/SpecificItemSubmissionStation.cs"), "\"craft_completed\"", "Level1 synthesis completion checkpoint");
        Has(Read(projectRoot, "Interaction/AlchemyCauldron.cs"), "if(wasReady || IsReady) GameSaveSystem.RequestAutoSave", "brewing and bottling commit checkpoints");
        return checks;
    }

    static string Read(string root, string file) { return File.ReadAllText(Path.Combine(root, "Assets/Scripts/" + file)); }
    static void Check(bool value, string label) { if (!value) throw new Exception("AUTOSAVE_PROGRESS_FAIL: " + label); checks++; }
    static void Has(string text, string value, string label) { Check(text.Contains(value), label); }
    static void NoRequest(string body, string label) { Check(!body.Contains("RequestAutoSave") && !body.Contains("RequestQuestAutoSave"), label); }
    static void Ordered(string body, string first, string last, string label)
    { int a = body.IndexOf(first, StringComparison.Ordinal), b = body.IndexOf(last, StringComparison.Ordinal); Check(a >= 0 && b > a, label); }
    static string Method(string source, string signature)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) throw new Exception("Missing method: " + signature);
        int opening = source.IndexOf('{', start), depth = 0;
        for (int i = opening; i < source.Length; i++)
        {
            if (source[i] == '{') depth++;
            if (source[i] == '}' && --depth == 0) return source.Substring(opening, i - opening + 1);
        }
        throw new Exception("Unbalanced method: " + signature);
    }
}
