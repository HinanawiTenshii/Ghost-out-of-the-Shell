using UnityEngine;

/// <summary>On-demand cross-scene dialogue gate backed by the existing saved quest journal.</summary>
[AddComponentMenu("Interaction/Dialogue Quest Gate (跨场景任务选项条件)")]
public sealed class DialogueQuestGateEffect : DialogueOptionEffect
{
    public enum RequiredStage { Accepted, Completed }
    [SerializeField, InspectorName("任务 ID")] private string questId;
    [SerializeField, InspectorName("要求阶段"), Tooltip("Accepted 包括已经完成的任务。无需引用另一个场景中的角色。")]
    private RequiredStage requiredStage;
    [SerializeField, InspectorName("未达成时隐藏选项")] private bool hideWhenUnavailable = true;
    [SerializeField, InspectorName("满足条件后执行（可选）"), Tooltip("为空时仅进入选项指定的下一段；可绑定购买等选项效果。请勿循环引用。")]
    private DialogueOptionEffect effect;
    [SerializeField, Tooltip("可选：成功选择后更新任务日志；交易失败不会更新。")]
    private QuestJournalEventChange[] journalChangesOnSuccess;
    private bool executing;

    private bool Qualified
    {
        get
        {
            var journal = QuestJournalManager.Instance;
            return isActiveAndEnabled && !string.IsNullOrWhiteSpace(questId) && journal != null &&
                journal.CanTriggerEntry(questId) &&
                (requiredStage == RequiredStage.Completed ? journal.IsEntryCompleted(questId) :
                    journal.ContainsEntry(questId) || journal.IsEntryCompleted(questId));
        }
    }

    public override bool IsVisible(CharacterDialogue speaker, ZeldaFourWayMover player)
    {
        if (!Qualified) return !hideWhenUnavailable;
        return effect == null || (effect != this && effect.IsVisible(speaker, player));
    }
    public override string GetChoiceText(CharacterDialogue speaker, ZeldaFourWayMover player, string authoredText)
        => effect != null && effect != this ? effect.GetChoiceText(speaker, player, authoredText) : authoredText;
    public override bool CanSelect(CharacterDialogue speaker, ZeldaFourWayMover player, out string reason)
    {
        reason = null;
        if (!Qualified || executing || effect == this)
        { reason = "还没有得知这件事。"; return false; }
        return effect == null || effect.CanSelect(speaker, player, out reason);
    }
    public override bool TryExecute(CharacterDialogue speaker, ZeldaFourWayMover player)
    {
        if (!CanSelect(speaker, player, out _)) return false;
        executing = true;
        try
        {
            if (effect != null && !effect.TryExecute(speaker, player)) return false;
            if (journalChangesOnSuccess != null && journalChangesOnSuccess.Length > 0)
                QuestJournalManager.GetOrCreate().ApplyEventChanges(journalChangesOnSuccess, gameObject.scene.name);
            return true;
        }
        finally { executing = false; }
    }
}
