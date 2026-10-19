using System;
using System.Collections.Generic;
using UnityEngine;

// Stored on character runtime snapshots so a quest NPC keeps its role after possession/travel.
public enum QuestCriticalCharacterRole { None, BaitMerchant, GemMerchant, CounterfeitCraftsman, KnightCaptain, Gravekeeper }

public sealed partial class QuestJournalManager
{
    [Serializable] public sealed class SavedFailure { public string id, reason; }
    private readonly Dictionary<string, string> failureEvidence = new Dictionary<string, string>();
    private bool baitToolsDelivered, baitBombAcquired, counterfeitGemAcquired, counterfeitProofAcquired;
    public bool BaitToolsDelivered => baitToolsDelivered;
    public bool IsEntryFailed(string id) => !string.IsNullOrEmpty(id) && ContainsEntry(id) && failureEvidence.ContainsKey(id);
    public bool IsEntryFinished(string id) => IsEntryCompleted(id) || IsEntryFailed(id);
    public string GetFailureReason(string id) => id != null && failureEvidence.TryGetValue(id, out var reason) ? reason : null;

    // Resolve only at death/save, not through per-frame scene/name scans. The cached role
    // survives reconstruction from a generic character prefab in another scene.
    public static QuestCriticalCharacterRole ResolveCriticalRole(ZeldaCharacterData data)
    {
        if (data == null) return QuestCriticalCharacterRole.None;
        if (data.QuestFailureRole != QuestCriticalCharacterRole.None) return data.QuestFailureRole;
        var quest = data.GetComponent<DialogueQuestProgression>();
        QuestCriticalCharacterRole role = QuestCriticalCharacterRole.None;
        if (quest != null)
        {
            if (quest.QuestId == SandwormBaitQuestId) role = QuestCriticalCharacterRole.BaitMerchant;
            else if (quest.QuestId == CounterfeitQuestId) role = QuestCriticalCharacterRole.CounterfeitCraftsman;
            else if (quest.QuestId == GravekeeperQuestId) role = QuestCriticalCharacterRole.Gravekeeper;
        }
        if (role == QuestCriticalCharacterRole.None)
            foreach (var offer in data.GetComponents<DialogueRecipeExchangeEffect>())
                if (offer.Merchandise is GoldenGemPickupItem) { role = QuestCriticalCharacterRole.GemMerchant; break; }
        var squad = data.GetComponent<SquadPatrolCharacterAi>();
        if (squad != null && squad.Ambush != null && squad.Ambush.IsCaptain(squad)) role = QuestCriticalCharacterRole.KnightCaptain;
        data.QuestFailureRole = role;
        return role;
    }

    // Called after the ritual has reserved its soul/crystal. Neither disabling a streamed
    // object nor unloading a scene is a death; those paths never call this method.
    public void RecordQuestCharacterDeath(ZeldaCharacterData data)
    {
        if (data == null || !data.IsDead || data is GhostZeldaCharacterData || GameSaveSystem.IsLoading ||
            (SceneTravelStateManager.Instance != null && SceneTravelStateManager.Instance.IsSceneTravelRestoreInProgress)) return;
        RecordQuestInventoryEvidence();
        switch (ResolveCriticalRole(data))
        {
            case QuestCriticalCharacterRole.BaitMerchant:
                if (!baitBombAcquired) RecordQuestFailure(SandwormBaitQuestId, "尚未取得诱饵炸弹，诡异商人已经死亡。无法继续向他购买诱饵炸弹。");
                break;
            case QuestCriticalCharacterRole.GemMerchant:
                if (!counterfeitGemAcquired && !counterfeitProofAcquired)
                    RecordQuestFailure(CounterfeitQuestId, "尚未取得金色宝石，宝石商人已经死亡。无法继续向他购买制作赝品所需的宝石。");
                break;
            case QuestCriticalCharacterRole.CounterfeitCraftsman:
                if (!counterfeitProofAcquired) RecordQuestFailure(CounterfeitQuestId, "尚未取得赝品试炼证明，赝品工匠已经死亡。无法继续完成赝品制作。");
                break;
            case QuestCriticalCharacterRole.KnightCaptain:
                RecordQuestFailure(KnightAmbushQuestId, "尚未附身骑士长，骑士长已经死亡。无法再取得他的身体与权限。");
                break;
            case QuestCriticalCharacterRole.Gravekeeper:
                if (!churchRelicPasswordKnown) RecordQuestFailure(GravekeeperQuestId, "尚未获知圣物间密码，守墓人已经死亡。无法再向他复命并获得密码。");
                break;
        }
        EnsureSummoningTargetsConfigured();
        string identity = data.PersistentCharacterId;
        if (!string.IsNullOrEmpty(identity) && summoningTargetIds.Contains(identity) && !summoningConsumedIds.Contains(identity))
            RecordQuestFailure(SummoningQuestId, "一名指定角色在未被法阵接收为祭品时死亡，无法再集齐六个指定灵魂。");
    }

    public bool RecordQuestFailure(string id, string reason)
    {
        // Completion facts can precede acceptance (notably possessing the captain).
        if (string.IsNullOrWhiteSpace(id) || IsEntryCompleted(id) || pendingCompletionEvidence.Contains(id) || failureEvidence.ContainsKey(id)) return false;
        failureEvidence[id] = string.IsNullOrWhiteSpace(reason) ? "关键条件已无法达成。" : reason;
        pendingUpdateEvidence.Remove(id); pendingDetailEvidence.Remove(id);
        ApplyRecordedFailure(id);
        return true;
    }

    private bool ApplyRecordedFailure(string id, bool notify = true)
    {
        if (!failureEvidence.TryGetValue(id, out var reason) || !TryGetEntry(id, out var entry) || IsEntryCompleted(id)) return false;
        const string separator = "\n\n任务失败：";
        if (!entry.Details.Contains(separator)) entry.Update(entry.Id, entry.Title, entry.Details + separator + reason, entry.SourceScene);
        pendingCompletionEvidence.Remove(id);
        if (TrackedEntryId == id) TrackedEntryId = FindLatestIncompleteEntryId(id);
        if (notify)
        {
            JournalChanged?.Invoke(); TrackedQuestChanged?.Invoke();
            JournalEntriesChanged?.Invoke(QuestJournalChangeKind.Failed);
        }
        return true;
    }

    // Inventory changes, acceptance and load are the only checks. Acquired items stay
    // recorded even if spent, dropped, or carried into a different scene afterwards.
    public void RecordQuestInventoryEvidence()
    {
        var bag = PersistentInventory.Instance;
        if (bag == null) return;
        if (bag.Contains("decoy_bomb")) baitBombAcquired = true;
        if (bag.Contains(GoldenGemPickupItem.DefaultItemId)) counterfeitGemAcquired = true;
        if (bag.Contains("counterfeit_trial_proof")) counterfeitProofAcquired = true;
        if (baitBombAcquired && ContainsEntry(SandwormBaitQuestId) && !IsEntryFinished(SandwormBaitQuestId))
        {
            AddOrUpdateEntry(SandwormBaitQuestId, SandwormBaitTitle, "已取得诱饵炸弹，可用于引诱沙虫并使其吞下炸弹。", "Level2-Floor1");
            CompleteEntry(SandwormBaitQuestId);
        }
    }

    public void RecordBaitToolsDelivered(string sourceScene)
    {
        if (IsEntryFailed(SandwormBaitQuestId)) return;
        baitToolsDelivered = true;
        if (IsEntryCompleted(SandwormBaitQuestId)) return;
        AddOrUpdateEntry(SandwormBaitQuestId, SandwormBaitTitle,
            SandwormBaitCompletedDetails + "\n\n当前目标：取得一颗诱饵炸弹。", sourceScene);
        RecordQuestInventoryEvidence();
    }

    private void CaptureFailureState(SaveState state)
    {
        state.failureVersion = 1;
        foreach (var pair in failureEvidence) state.failures.Add(new SavedFailure { id = pair.Key, reason = pair.Value });
        state.baitToolsDelivered = baitToolsDelivered; state.baitBombAcquired = baitBombAcquired;
        state.counterfeitGemAcquired = counterfeitGemAcquired; state.counterfeitProofAcquired = counterfeitProofAcquired;
    }

    private void RestoreFailureState(SaveState state)
    {
        ResetFailureState();
        if (state.failures != null) foreach (var failure in state.failures)
            if (failure != null && !string.IsNullOrEmpty(failure.id) && !IsEntryCompleted(failure.id)) failureEvidence[failure.id] = failure.reason ?? "关键条件已无法达成。";
        baitToolsDelivered = state.baitToolsDelivered;
        baitBombAcquired = state.baitBombAcquired;
        counterfeitGemAcquired = state.counterfeitGemAcquired || EntryDetailsContain(CounterfeitQuestId, CounterfeitGemAcquired);
        counterfeitProofAcquired = state.counterfeitProofAcquired || IsEntryCompleted(CounterfeitQuestId) || pendingCompletionEvidence.Contains(CounterfeitQuestId);
        // Legacy completion meant tool delivery. Preserve the shop unlock, but now
        // require the actual bomb to finish this quest. Never fabricate acquisition.
        if (state.failureVersion == 0 && (completedEntryIds.Remove(SandwormBaitQuestId) || pendingCompletionEvidence.Remove(SandwormBaitQuestId)))
        {
            baitToolsDelivered = true;
            if (TryGetEntry(SandwormBaitQuestId, out var bait))
                bait.Update(bait.Id, bait.Title, SandwormBaitCompletedDetails + "\n\n当前目标：取得一颗诱饵炸弹。", bait.SourceScene);
        }
        foreach (string id in failureEvidence.Keys) ApplyRecordedFailure(id, false);
    }

    private void ResetFailureState()
    {
        failureEvidence.Clear(); baitToolsDelivered = false; baitBombAcquired = false;
        counterfeitGemAcquired = false; counterfeitProofAcquired = false;
    }
}
