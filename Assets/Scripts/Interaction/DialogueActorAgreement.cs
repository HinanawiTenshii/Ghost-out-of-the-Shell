using UnityEngine;

/// <summary>A one-time agreement tied to the accepting body, not the most recent visitor.</summary>
[DisallowMultipleComponent, AddComponentMenu("Interaction/Dialogue Actor Agreement (固定交谈者约定)")]
public sealed class DialogueActorAgreement : DialogueOptionEffect
{
    [SerializeField, InspectorName("约定记忆 ID（唯一）"), Tooltip("使用独立 ID，不要与角色对话的交谈记忆 ID 共用。接受时记录角色身份，跨场景与存档保留；陌生人交谈不会覆盖。")]
    private string memoryId;
    [SerializeField, InspectorName("接受者再次交谈段落")] private string acceptedNodeId = "reminder";
    [SerializeField, InspectorName("其他角色交谈段落")] private string strangerNodeId = "stranger";
    [SerializeField, InspectorName("接受时接取任务（可选）")] private QuestJournalEntryDefinition questOnAccept;
    [SerializeField, InspectorName("父任务未接取时的段落 ID（可选）")] private string prerequisiteNodeId;

    private bool QuestAvailable => string.IsNullOrWhiteSpace(questOnAccept.entryId) ||
        QuestJournalManager.GetOrCreate().CanTriggerEntry(questOnAccept.entryId, questOnAccept.parentEntryId);

    public bool IsAccepted => QuestJournalManager.Instance != null &&
        QuestJournalManager.Instance.HasRememberedDialogueActor(memoryId);

    // Agreements without a journal task are ordinary dialogue, not quest offers.
    internal bool HasAvailableQuest => isActiveAndEnabled &&
        !string.IsNullOrWhiteSpace(memoryId) && !string.IsNullOrWhiteSpace(questOnAccept.entryId) &&
        !IsAccepted && QuestAvailable &&
        !QuestJournalManager.GetOrCreate().ContainsEntry(questOnAccept.entryId) &&
        !QuestJournalManager.GetOrCreate().IsEntryCompleted(questOnAccept.entryId);

    public string GetStartNodeId(string initial, ZeldaFourWayMover actor)
    {
        if (isActiveAndEnabled && !QuestAvailable && !string.IsNullOrWhiteSpace(prerequisiteNodeId)) return prerequisiteNodeId;
        if (!isActiveAndEnabled || !IsAccepted) return initial;
        var data = actor != null ? actor.GetComponent<ZeldaCharacterData>() : null;
        return data == null || QuestJournalManager.Instance.HasDifferentDialogueActor(memoryId, data.IndividualAccessId)
            ? strangerNodeId : acceptedNodeId;
    }

    public override bool CanSelect(CharacterDialogue speaker, ZeldaFourWayMover player, out string reason)
    {
        reason = null;
        if (!QuestAvailable) { reason = "需要先接取父任务。"; return false; }
        var data = player != null ? player.GetComponent<ZeldaCharacterData>() : null;
        if (!isActiveAndEnabled || string.IsNullOrWhiteSpace(memoryId) || IsAccepted ||
            speaker == null || speaker.gameObject != gameObject ||
            speaker.FindNode(acceptedNodeId) == null || speaker.FindNode(strangerNodeId) == null ||
            data == null || data.IsDead || data.IsGhostLike)
        { reason = "现在无法接受这个约定。"; return false; }
        return true;
    }

    public override bool TryExecute(CharacterDialogue speaker, ZeldaFourWayMover player)
    {
        if (!CanSelect(speaker, player, out _)) return false;
        var journal = QuestJournalManager.GetOrCreate();
        journal.RememberDialogueActor(memoryId,
            player.GetComponent<ZeldaCharacterData>().IndividualAccessId);
        if (memoryId == QuestJournalManager.SandwormMortarAgreementId)
            journal.UnlockEngineerSafePassword();
        if (!string.IsNullOrWhiteSpace(questOnAccept.entryId))
            journal.AddOrUpdateEntry(questOnAccept.entryId, questOnAccept.title, questOnAccept.details,
                gameObject.scene.name, questOnAccept.parentEntryId);
        return true;
    }
}
