using UnityEngine;

/// <summary>Uses the existing saved journal as the single source of quest/dialogue progress.</summary>
[DisallowMultipleComponent, AddComponentMenu("Interaction/Dialogue Quest Progression (任务对话阶段)")]
public sealed class DialogueQuestProgression : MonoBehaviour
{
    [SerializeField, InspectorName("任务 ID（唯一）")] private string questId;
    [SerializeField, InspectorName("父任务 ID（可选）")] private string parentQuestId;
    [SerializeField, InspectorName("父任务未接取时的段落 ID（可选）")] private string prerequisiteNodeId;
    [SerializeField, InspectorName("任务标题")] private string questTitle;
    [SerializeField, TextArea(3, 8), InspectorName("接取后的日志内容")] private string questDetails;
    [SerializeField, TextArea(3, 8), InspectorName("完成后的日志内容")] private string completedDetails;
    [SerializeField, InspectorName("接取后开始段落 ID")] private string acceptedNodeId = "waiting";
    [SerializeField, InspectorName("完成后开始段落 ID")] private string completedNodeId = "shop";
    public string QuestId => questId;
    public bool IsCompleted => QuestJournalManager.Instance != null && QuestJournalManager.Instance.IsEntryCompleted(questId);
    public bool IsAccepted => QuestJournalManager.Instance != null && QuestJournalManager.Instance.ContainsEntry(questId);
    public bool IsConfigured => isActiveAndEnabled && !string.IsNullOrWhiteSpace(questId);
    public bool CanTrigger => IsConfigured && QuestJournalManager.GetOrCreate().CanTriggerEntry(questId, parentQuestId);
    public string GetStartNodeId(string initial)
        => !CanTrigger && !string.IsNullOrWhiteSpace(prerequisiteNodeId) ? prerequisiteNodeId :
            IsCompleted ? completedNodeId : IsAccepted ? acceptedNodeId : initial;
    public bool AcceptQuest()
    {
        if (!CanTrigger || IsAccepted || IsCompleted) return false;
        return QuestJournalManager.GetOrCreate().AddOrUpdateEntry(questId, questTitle, questDetails, gameObject.scene.name, parentQuestId);
    }
    public void CompleteQuest()
    {
        if (!CanTrigger || IsCompleted) return;
        var journal = QuestJournalManager.GetOrCreate();
        journal.AddOrUpdateEntry(questId, questTitle, completedDetails, gameObject.scene.name, parentQuestId);
        journal.CompleteEntry(questId);
    }
}
